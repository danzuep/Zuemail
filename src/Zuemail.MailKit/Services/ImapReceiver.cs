using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zuemail.Core.Models;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Models;

namespace Zuemail.MailKit.Services;

/// <summary>MailKit-backed IMAP receiver with explicit client ownership and lazy helper creation.</summary>
public sealed class ImapReceiver : IImapReceiver
{
    private readonly ImapOptions _options;
    private readonly ILogger<ImapReceiver> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly bool _ownsClient;
    private IImapClient _client;
    private bool _disposed;

    public ImapReceiver(IOptions<ImapOptions> options, ILogger<ImapReceiver> logger = null,
        IProtocolLogger protocolLogger = null, IImapClient imapClient = null)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        _options = options.Value?.Copy() as ImapOptions ?? throw new ArgumentException("IMAP options are required.", nameof(options));
        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new ArgumentException("An IMAP host is required.", nameof(options));
        if (_options.TimeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The IMAP timeout must be positive.");

        _logger = logger ?? NullLogger<ImapReceiver>.Instance;
        _ownsClient = imapClient == null;
        _client = imapClient ?? CreateClient(protocolLogger);
    }

    public static ImapReceiver Create(string host, ushort port = 0, string username = null, string password = null,
        string folderName = null, ILogger<ImapReceiver> logger = null, IProtocolLogger protocolLogger = null,
        IImapClient imapClient = null)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentNullException(nameof(host));
        if (port == 0)
        {
            var parsed = ParseHostAndPort(host);
            host = parsed.Host;
            port = parsed.Port;
        }
        var options = new ImapOptions
        {
            Host = host,
            Port = port,
            Username = username ?? string.Empty,
            Password = password ?? string.Empty,
            MailFolderName = string.IsNullOrWhiteSpace(folderName) ? "INBOX" : folderName
        };
        return new ImapReceiver(Microsoft.Extensions.Options.Options.Create(options), logger, protocolLogger, imapClient);
    }

    public IImapClient ImapClient => _client ?? throw new ObjectDisposedException(nameof(ImapReceiver));
    public IMailFolderReader ReadMail => new MailFolderReader(this);
    public IMailFolderClient MailFolderClient => new MailFolderClient(this);
    public IMailFolderMonitor MonitorFolder => new MailFolderMonitor(this);

    public async ValueTask<IImapClient> ConnectAuthenticatedImapClientAsync(CancellationToken cancellationToken = default, bool force = false)
    {
        ThrowIfDisposed();
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = ImapClient;
            if (force && client.IsConnected)
                await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            if (!client.IsConnected)
            {
                await client.ConnectAsync(_options.Host, _options.Port, _options.SocketOptions, cancellationToken).ConfigureAwait(false);
                if (_options.ImapCapabilitiesToRemove != ImapCapabilities.None)
                    client.Capabilities &= ~_options.ImapCapabilitiesToRemove;
                _logger.LogDebug("Connected to IMAP server {Host}.", _options.Host);
            }

            foreach (var mechanism in _options.AuthenticationMechanismsToRemove ?? Enumerable.Empty<string>())
                client.AuthenticationMechanisms.Remove(mechanism);

            var authenticatedNow = false;
            if (!client.IsAuthenticated)
            {
                if (_options.CustomAuthentication != null)
                    await _options.CustomAuthentication(client, cancellationToken).ConfigureAwait(false);
                else if (_options.AuthenticationMechanism != null)
                    await client.AuthenticateAsync(_options.AuthenticationMechanism, cancellationToken).ConfigureAwait(false);
                else
                    await client.AuthenticateAsync(_options.Credential, cancellationToken).ConfigureAwait(false);
                authenticatedNow = true;
            }
            if (authenticatedNow && _options.ImapAuthenticationCapabilities != null)
                await _options.ImapAuthenticationCapabilities(client).ConfigureAwait(false);
            return client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask<IMailFolder> ConnectMailFolderAsync(CancellationToken cancellationToken = default)
    {
        var client = await ConnectAuthenticatedImapClientAsync(cancellationToken).ConfigureAwait(false);
        var name = _options.MailFolderName;
        IMailFolder folder = string.IsNullOrWhiteSpace(name) || name.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox
            : await client.GetFolderAsync(name, cancellationToken).ConfigureAwait(false);
        if (_options.MailFolderAccess != FolderAccess.None && !folder.IsOpen)
            await folder.OpenAsync(_options.MailFolderAccess, cancellationToken).ConfigureAwait(false);
        return folder;
    }

    public async Task<IList<string>> GetMailFolderNamesAsync(CancellationToken cancellationToken = default)
    {
        var client = await ConnectAuthenticatedImapClientAsync(cancellationToken).ConfigureAwait(false);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { client.Inbox.FullName };
        var namespaces = client.PersonalNamespaces.Concat(client.SharedNamespaces).Concat(client.OtherNamespaces);
        foreach (var folderNamespace in namespaces)
        {
            var folders = await client.GetFoldersAsync(folderNamespace, subscribedOnly: false, cancellationToken).ConfigureAwait(false);
            foreach (var folder in folders)
                await AddFolderAndChildrenAsync(folder, names, cancellationToken).ConfigureAwait(false);
        }
        return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task AddFolderAndChildrenAsync(IMailFolder folder, ISet<string> names, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!names.Add(folder.FullName))
            return;
        var children = await folder.GetSubfoldersAsync(subscribedOnly: false, cancellationToken).ConfigureAwait(false);
        foreach (var child in children)
            await AddFolderAndChildrenAsync(child, names, cancellationToken).ConfigureAwait(false);
    }

    [Obsolete("Use MailFolderClient.MoveToAsync instead.")]
    public async Task<UniqueId?> MoveToSentAsync(IMessageSummary messageSummary, CancellationToken cancellationToken = default)
    {
        if (messageSummary == null)
            throw new ArgumentNullException(nameof(messageSummary));
        var sentFolder = await MailFolderClient.GetFolderAsync(new[] { "Sent", "Sent Items", "[Gmail]/Sent Mail" }, cancellationToken).ConfigureAwait(false);
        if (sentFolder == null)
            throw new InvalidOperationException("The Sent folder could not be found.");
        return await MailFolderClient.MoveToAsync(messageSummary, sentFolder, cancellationToken).ConfigureAwait(false);
    }

    public IImapReceiver Clone() => new ImapReceiver(Microsoft.Extensions.Options.Options.Create(_options.Copy() as ImapOptions ?? throw new InvalidOperationException()),
        _logger, protocolLogger: null);

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _client == null)
            return;
        if (_client.IsConnected)
            await _client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_client != null)
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            if (_ownsClient)
                _client.Dispose();
        }
        _client = null;
        _connectionLock.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    internal ImapOptions ReceiverOptions => _options;

    internal static (string Host, ushort Port) ParseHostAndPort(string host)
    {
        if (host.StartsWith("[", StringComparison.Ordinal))
        {
            var bracket = host.IndexOf(']');
            if (bracket < 0)
                throw new ArgumentException("The bracketed IPv6 host is invalid.", nameof(host));
            if (bracket == host.Length - 1)
                return (host.Substring(1, bracket - 1), 0);
            if (host[bracket + 1] != ':' || !ushort.TryParse(host.Substring(bracket + 2), out var ipv6Port))
                throw new ArgumentException("The host port is invalid.", nameof(host));
            return (host.Substring(1, bracket - 1), ipv6Port);
        }
        var colon = host.LastIndexOf(':');
        if (colon > 0 && host.IndexOf(':') == colon && ushort.TryParse(host.Substring(colon + 1), out var port))
            return (host.Substring(0, colon), port);
        return (host, 0);
    }

    private IImapClient CreateClient(IProtocolLogger protocolLogger)
    {
        return _options.CreateImapClient(protocolLogger);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ImapReceiver));
    }

    public override string ToString() => $"{_options.Host}:{_options.Port} {_options.Username} {_options.MailFolderName}";
}