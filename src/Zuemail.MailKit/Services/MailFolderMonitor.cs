using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zuemail.MailKit.Abstractions;

namespace Zuemail.MailKit.Services;

/// <summary>Monitors one folder using IMAP IDLE when available and NOOP polling otherwise.</summary>
public sealed class MailFolderMonitor : IMailFolderMonitor
{
    private readonly IEmailReceiver _idleReceiver;
    private readonly IEmailReceiver _fetchReceiver;
    private readonly ILogger<MailFolderMonitor> _logger;
    private readonly SemaphoreSlim _changeSignal = new(0, 1);
    private CancellationTokenSource _activeIdle;
    private Func<IMessageSummary, Task> _arrival;
    private Func<IMessageSummary, Task> _flagsChanged;
    private Func<IMessageSummary, Task> _departure;
    private bool _ignoreExisting;
    private bool _disposed;
    private byte _idleMinutes = 9;
    private byte _maxRetries = 3;
    private TimeSpan _retryDelay = TimeSpan.FromSeconds(2);
    private MessageSummaryItems _items = MessageSummaryItems.Envelope | MessageSummaryItems.Flags;

    public MailFolderMonitor(IEmailReceiver receiver, ILogger<MailFolderMonitor> logger = null)
    {
        if (receiver == null)
            throw new ArgumentNullException(nameof(receiver));
        _idleReceiver = receiver.Clone();
        _fetchReceiver = receiver.Clone();
        _logger = logger ?? NullLogger<MailFolderMonitor>.Instance;
        _arrival = summary => Task.CompletedTask;
        _flagsChanged = summary => Task.CompletedTask;
        _departure = summary => Task.CompletedTask;
    }

    public IMailFolderMonitor SetIdleMinutes(byte minutes = 9)
    {
        if (minutes == 0 || minutes > 29)
            throw new ArgumentOutOfRangeException(nameof(minutes), "IDLE duration must be between 1 and 29 minutes.");
        _idleMinutes = minutes;
        return this;
    }

    public IMailFolderMonitor SetMaxRetries(byte retries = 3)
    {
        if (retries == 0)
            throw new ArgumentOutOfRangeException(nameof(retries));
        _maxRetries = retries;
        return this;
    }

    public IMailFolderMonitor SetRetryDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay));
        _retryDelay = delay;
        return this;
    }

    public IMailFolderMonitor SetIgnoreExistingMailOnConnect(bool ignoreExisting = true)
    {
        _ignoreExisting = ignoreExisting;
        return this;
    }

    public IMailFolderMonitor SetMessageSummaryItems(MessageSummaryItems items = MessageSummaryItems.Envelope)
    {
        _items = items | MessageSummaryItems.UniqueId | MessageSummaryItems.Flags;
        return this;
    }

    public IMailFolderMonitor OnMessageArrival(Func<IMessageSummary, Task> callback)
    {
        _arrival = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    public IMailFolderMonitor OnMessageFlagsChanged(Func<IMessageSummary, Task> callback)
    {
        _flagsChanged = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    public IMailFolderMonitor OnMessageDeparture(Func<IMessageSummary, Task> callback)
    {
        _departure = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    public IMailFolderMonitor OnMessageArrival(Action<IMessageSummary> callback) =>
        OnMessageArrival(Wrap(callback));

    public IMailFolderMonitor OnMessageFlagsChanged(Action<IMessageSummary> callback) =>
        OnMessageFlagsChanged(Wrap(callback));

    public IMailFolderMonitor OnMessageDeparture(Action<IMessageSummary> callback) =>
        OnMessageDeparture(Wrap(callback));

    public async Task IdleAsync(CancellationToken cancellationToken = default, bool handleExceptions = true)
    {
        ThrowIfDisposed();
        if (cancellationToken.IsCancellationRequested)
            return;
        IMailFolder idleFolder = null;
        IMailFolder fetchFolder = null;
        var idleOpened = false;
        var fetchOpened = false;
        byte initialFailures = 0;
        while (true)
        {
            try
            {
                idleFolder = await _idleReceiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
                fetchFolder = await _fetchReceiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
                idleOpened = !idleFolder.IsOpen;
                fetchOpened = !fetchFolder.IsOpen;
                if (idleOpened)
                    await idleFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
                if (fetchOpened)
                    await fetchFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (IsRecoverable(ex) && initialFailures + 1 < _maxRetries)
            {
                initialFailures++;
                _logger.LogWarning(ex, "Initial IMAP folder connection failed; retry {Retry}/{MaxRetries}.", initialFailures, _maxRetries);
                await ReconnectAsync(cancellationToken).ConfigureAwait(false);
                var retryDelay = TimeSpan.FromMilliseconds(Math.Min(30000,
                    _retryDelay.TotalMilliseconds * Math.Pow(2, initialFailures - 1)));
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (handleExceptions && !(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Initial IMAP folder connection failed; monitoring was not started.");
                return;
            }
        }
        EventHandler<EventArgs> changed = (_, _) => SignalChange();
        if (idleOpened)
            await idleFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
        if (fetchOpened)
            await fetchFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

        var known = new Dictionary<UniqueId, IMessageSummary>();
        try
        {
            var initial = await FetchSnapshotAsync(fetchFolder, cancellationToken).ConfigureAwait(false);
            foreach (var summary in initial)
                known[summary.UniqueId] = summary;
            if (!_ignoreExisting)
                foreach (var summary in initial)
                    await _arrival(summary).ConfigureAwait(false);

            idleFolder.CountChanged += changed;
            idleFolder.MessageFlagsChanged += OnFlagsChanged;
            idleFolder.MessageExpunged += OnMessageExpunged;
            try
            {
                var client = await _idleReceiver.ConnectAuthenticatedImapClientAsync(cancellationToken).ConfigureAwait(false);
                byte failures = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (client.Capabilities.HasFlag(ImapCapabilities.Idle))
                        {
                            using var idleTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(_idleMinutes));
                            using var done = CancellationTokenSource.CreateLinkedTokenSource(idleTimeout.Token);
                            _activeIdle = done;
                            if (_changeSignal.CurrentCount > 0)
                                done.Cancel();
                            try
                            {
                                await client.IdleAsync(done.Token, cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                            }
                            finally
                            {
                                _activeIdle = null;
                            }
                        }
                        else
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
                            await client.NoOpAsync(cancellationToken).ConfigureAwait(false);
                        }
                        if (_changeSignal.CurrentCount > 0)
                            await _changeSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                        var current = await FetchSnapshotAsync(fetchFolder, cancellationToken).ConfigureAwait(false);
                        await DispatchChangesAsync(known, current).ConfigureAwait(false);
                        failures = 0;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex) when (IsRecoverable(ex) && failures + 1 < _maxRetries)
                    {
                        failures++;
                        _logger.LogWarning(ex, "IMAP monitoring failed; retry {Retry}/{MaxRetries} after reconnect.", failures, _maxRetries);
                        idleFolder.CountChanged -= changed;
                        idleFolder.MessageFlagsChanged -= OnFlagsChanged;
                        idleFolder.MessageExpunged -= OnMessageExpunged;
                        await ReconnectAsync(cancellationToken).ConfigureAwait(false);
                        client = await _idleReceiver.ConnectAuthenticatedImapClientAsync(cancellationToken).ConfigureAwait(false);
                        idleFolder = await _idleReceiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
                        fetchFolder = await _fetchReceiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
                        if (!idleFolder.IsOpen)
                        {
                            await idleFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
                            idleOpened = true;
                        }
                        if (!fetchFolder.IsOpen)
                        {
                            await fetchFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
                            fetchOpened = true;
                        }
                        idleFolder.CountChanged += changed;
                        idleFolder.MessageFlagsChanged += OnFlagsChanged;
                        idleFolder.MessageExpunged += OnMessageExpunged;
                        var retryDelay = TimeSpan.FromMilliseconds(Math.Min(30000,
                            _retryDelay.TotalMilliseconds * Math.Pow(2, failures - 1)));
                        await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (handleExceptions && !(ex is OperationCanceledException))
                    {
                        _logger.LogError(ex, "IMAP monitoring stopped after an unrecoverable error.");
                        break;
                    }
                }
            }
            finally
            {
                idleFolder.CountChanged -= changed;
                idleFolder.MessageFlagsChanged -= OnFlagsChanged;
                idleFolder.MessageExpunged -= OnMessageExpunged;
            }
        }
        finally
        {
            if (fetchOpened && fetchFolder.IsOpen)
                await fetchFolder.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
            if (idleOpened && idleFolder.IsOpen)
                await idleFolder.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
        }

        void OnFlagsChanged(object sender, MessageFlagsChangedEventArgs args) => SignalChange();
        void OnMessageExpunged(object sender, MessageEventArgs args) => SignalChange();
    }

    private async Task<IList<IMessageSummary>> FetchSnapshotAsync(IMailFolder folder, CancellationToken cancellationToken)
    {
        if (folder.Count == 0)
            return new List<IMessageSummary>();
        return await folder.FetchAsync(0, -1, _items, cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchChangesAsync(IDictionary<UniqueId, IMessageSummary> known, IList<IMessageSummary> current)
    {
        var now = current.ToDictionary(summary => summary.UniqueId);
        foreach (var summary in current)
        {
            if (!known.TryGetValue(summary.UniqueId, out var previous))
                await _arrival(summary).ConfigureAwait(false);
            else if (previous.Flags != summary.Flags)
                await _flagsChanged(summary).ConfigureAwait(false);
        }
        foreach (var previous in known.Values)
            if (!now.ContainsKey(previous.UniqueId))
                await _departure(previous).ConfigureAwait(false);
        known.Clear();
        foreach (var summary in current)
            known[summary.UniqueId] = summary;
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        await _idleReceiver.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        await _fetchReceiver.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        await _idleReceiver.ConnectAuthenticatedImapClientAsync(cancellationToken, force: true).ConfigureAwait(false);
        await _fetchReceiver.ConnectAuthenticatedImapClientAsync(cancellationToken, force: true).ConfigureAwait(false);
    }

    private void SignalChange()
    {
        try
        {
            if (_changeSignal.CurrentCount == 0)
                _changeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
        try
        {
            _activeIdle?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static bool IsRecoverable(Exception exception) => exception is ImapProtocolException
        or ImapCommandException or ServiceNotConnectedException or ServiceNotAuthenticatedException
        or System.IO.IOException or SocketException;

    private static Func<IMessageSummary, Task> Wrap(Action<IMessageSummary> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        return summary => { callback(summary); return Task.CompletedTask; };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        _changeSignal.Dispose();
        await _idleReceiver.DisposeAsync().ConfigureAwait(false);
        await _fetchReceiver.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _changeSignal.Dispose();
        _idleReceiver.Dispose();
        _fetchReceiver.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MailFolderMonitor));
    }
}