using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Zuemail.MailKit.Abstractions;

namespace Zuemail.MailKit.Services;

/// <summary>Provides read/write folder access and common message operations for one receiver.</summary>
public sealed class MailFolderClient : IMailFolderClient
{
    private static readonly string[][] SpecialFolderNames =
    {
        new[] { "Sent", "Sent Items", "[Gmail]/Sent Mail", "Sent Messages" },
        new[] { "Drafts", "Draft", "[Gmail]/Drafts" },
        new[] { "Junk", "Junk E-mail", "[Gmail]/Spam" },
        new[] { "Trash", "Deleted Items", "[Gmail]/Trash" }
    };

    private readonly IEmailReceiver _receiver;
    private IMailFolder _folder;
    private bool _disposed;

    public MailFolderClient(IEmailReceiver receiver) =>
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));

    public string MailFolderName => _folder?.FullName ?? _receiver.ToString();
    public int MailFolderCount => _folder?.Count ?? 0;
    public IMailFolder SentFolder => GetSpecialFolder(SpecialFolder.Sent);
    public IMailFolder DraftsFolder => GetSpecialFolder(SpecialFolder.Drafts);
    public IMailFolder JunkFolder => GetSpecialFolder(SpecialFolder.Junk);
    public IMailFolder TrashFolder => GetSpecialFolder(SpecialFolder.Trash);

    public async ValueTask<IMailFolder> ConnectAsync(bool enableWrite = false, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var folder = await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        _folder = folder;
        return await OpenAsync(folder, enableWrite, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IMailFolder> GetFolderAsync(string fullName, bool createIfNotFound = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("A folder name is required.", nameof(fullName));
        var client = await _receiver.ConnectAuthenticatedImapClientAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await client.GetFolderAsync(fullName, cancellationToken).ConfigureAwait(false);
        }
        catch (FolderNotFoundException)
        {
            if (!createIfNotFound)
                return null;
            var folderNamespace = client.PersonalNamespaces.FirstOrDefault()
                ?? client.SharedNamespaces.FirstOrDefault()
                ?? client.OtherNamespaces.FirstOrDefault();
            var parent = folderNamespace == null ? client.Inbox : client.GetFolder(folderNamespace);
            return await parent.CreateAsync(fullName, true, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IMailFolder> GetFolderAsync(IEnumerable<string> possibleNames = null,
        CancellationToken cancellationToken = default)
    {
        if (possibleNames == null)
            return _folder ?? await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        var candidates = possibleNames.Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (candidates.Length == 0)
            return _folder ?? await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        var available = await _receiver.GetMailFolderNamesAsync(cancellationToken).ConfigureAwait(false);
        var match = available.FirstOrDefault(name => candidates.Contains(name, StringComparer.OrdinalIgnoreCase))
            ?? available.FirstOrDefault(name => candidates.Contains(name.Split('/').Last(), StringComparer.OrdinalIgnoreCase));
        return match == null ? null : await GetFolderAsync(match, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddFlagsAsync(IEnumerable<UniqueId> uniqueIds, MessageFlags flags, bool silent = true,
        CancellationToken cancellationToken = default)
    {
        if (uniqueIds == null)
            throw new ArgumentNullException(nameof(uniqueIds));
        var ids = uniqueIds.Distinct().OrderBy(uid => uid.Id).ToList();
        if (ids.Count == 0)
            return 0;
        var (folder, closeWhenFinished) = await OpenWriteAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var request = new StoreFlagsRequest(StoreAction.Add, flags) { Silent = silent };
            await folder.StoreAsync(ids, request, cancellationToken).ConfigureAwait(false);
            if (flags.HasFlag(MessageFlags.Deleted))
                await folder.ExpungeAsync(ids, cancellationToken).ConfigureAwait(false);
            return ids.Count;
        }
        finally
        {
            if (closeWhenFinished && folder.IsOpen)
                await folder.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<int> AddFlagsAsync(SearchQuery query, MessageFlags flags, bool silent = true,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        var (folder, closeWhenFinished) = await OpenWriteAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var count = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matches = await folder.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                if (matches.Count == 0)
                    return count;
                var request = new StoreFlagsRequest(StoreAction.Add, flags) { Silent = silent };
                await folder.StoreAsync(matches, request, cancellationToken).ConfigureAwait(false);
                count += matches.Count;
                if (flags.HasFlag(MessageFlags.Deleted))
                    await folder.ExpungeAsync(matches, cancellationToken).ConfigureAwait(false);
                else
                    return count;
            }
        }
        finally
        {
            if (closeWhenFinished && folder.IsOpen)
                await folder.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public Task<int> DeleteMessagesAsync(IEnumerable<IMessageSummary> summaries, CancellationToken cancellationToken = default)
    {
        if (summaries == null)
            throw new ArgumentNullException(nameof(summaries));
        return AddFlagsAsync(summaries.Select(summary => summary.UniqueId), MessageFlags.Deleted, cancellationToken: cancellationToken);
    }

    public Task<int> DeleteMessagesAsync(TimeSpan olderThan, SearchQuery filter = null, CancellationToken cancellationToken = default)
    {
        var cutoff = olderThan.Days > 0 && olderThan.Ticks % TimeSpan.TicksPerDay == 0
            ? DateTime.Now.Date.Subtract(olderThan)
            : DateTime.Now.Subtract(olderThan);
        SearchQuery query = SearchQuery.DeliveredBefore(cutoff);
        if (filter != null)
            query = query.And(filter);
        return AddFlagsAsync(query, MessageFlags.Deleted, cancellationToken: cancellationToken);
    }

    public async Task<UniqueId?> AppendSentMessageAsync(MimeMessage message, MessageFlags flags = MessageFlags.Seen,
        CancellationToken cancellationToken = default, ITransferProgress transferProgress = null)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        await _receiver.ConnectAuthenticatedImapClientAsync(cancellationToken).ConfigureAwait(false);
        var folder = SentFolder;
        return await folder.AppendAsync(message, flags, cancellationToken, transferProgress).ConfigureAwait(false);
    }

    public async Task<UniqueId?> CopyToAsync(IMessageSummary summary, SpecialFolder destination = SpecialFolder.Drafts,
        CancellationToken cancellationToken = default)
    {
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));
        var target = GetSpecialFolder(destination);
        return await summary.Folder.CopyToAsync(summary.UniqueId, target, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UniqueId?> MoveToAsync(UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        var source = _folder ?? await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        return await MoveOneAsync(source, uid, destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UniqueId?> MoveToAsync(UniqueId uid, string destination, CancellationToken cancellationToken = default)
    {
        var target = await GetFolderAsync(destination, createIfNotFound: true, cancellationToken).ConfigureAwait(false);
        return await MoveToAsync(uid, target, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UniqueId?> MoveToAsync(IMessageSummary summary, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));
        return await MoveOneAsync(summary.Folder, summary.UniqueId, destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UniqueId?> MoveToAsync(IMessageSummary summary, string destination, CancellationToken cancellationToken = default)
    {
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));
        var target = await GetFolderAsync(destination, createIfNotFound: true, cancellationToken).ConfigureAwait(false);
        return await MoveOneAsync(summary.Folder, summary.UniqueId, target, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UniqueIdMap> MoveToAsync(IEnumerable<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
    {
        if (uids == null || destination == null)
            throw new ArgumentNullException(uids == null ? nameof(uids) : nameof(destination));
        var source = _folder ?? await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        var closeWhenFinished = !source.IsOpen;
        try
        {
            if (!source.IsOpen)
                await source.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            return await source.MoveToAsync(uids.ToList(), destination, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (closeWhenFinished && source.IsOpen)
                await source.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<UniqueIdMap> MoveToAsync(IEnumerable<UniqueId> uids, string destination, CancellationToken cancellationToken = default)
    {
        var target = await GetFolderAsync(destination, createIfNotFound: true, cancellationToken).ConfigureAwait(false);
        return await MoveToAsync(uids, target, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UniqueId?> MoveOneAsync(IMailFolder source, UniqueId uid, IMailFolder destination, CancellationToken cancellationToken)
    {
        if (source == null || destination == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(destination));
        var closeWhenFinished = !source.IsOpen;
        try
        {
            if (!source.IsOpen)
                await source.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
            return await source.MoveToAsync(uid, destination, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (closeWhenFinished && source.IsOpen)
                await source.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<(IMailFolder Folder, bool Close)> OpenWriteAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var folder = _folder ?? await _receiver.ConnectMailFolderAsync(cancellationToken).ConfigureAwait(false);
        _folder = folder;
        var close = !folder.IsOpen;
        if (close)
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
        else if (folder.Access != FolderAccess.ReadWrite)
            throw new InvalidOperationException("The selected folder is already open read-only. Close it before requesting write access.");
        return (folder, close);
    }

    private IMailFolder GetSpecialFolder(SpecialFolder specialFolder)
    {
        var client = _receiver.ImapClient;
        var index = specialFolder switch
        {
            SpecialFolder.Sent => 0,
            SpecialFolder.Drafts => 1,
            SpecialFolder.Junk => 2,
            SpecialFolder.Trash => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(specialFolder))
        };
        if ((client.Capabilities & (ImapCapabilities.SpecialUse | ImapCapabilities.XList)) != 0)
        {
            try { return client.GetFolder(specialFolder); }
            catch (FolderNotFoundException) { }
        }
        var candidates = SpecialFolderNames[index];
        var pending = new Queue<IMailFolder>();
        foreach (var folderNamespace in client.PersonalNamespaces.Concat(client.SharedNamespaces).Concat(client.OtherNamespaces))
        {
            var root = client.GetFolder(folderNamespace);
            foreach (var folder in root.GetSubfolders(false, CancellationToken.None))
                pending.Enqueue(folder);
        }
        while (pending.Count > 0)
        {
            var folder = pending.Dequeue();
            if (candidates.Contains(folder.Name, StringComparer.OrdinalIgnoreCase) ||
                candidates.Contains(folder.FullName, StringComparer.OrdinalIgnoreCase))
                return folder;
            foreach (var child in folder.GetSubfolders(false, CancellationToken.None))
                pending.Enqueue(child);
        }
        throw new InvalidOperationException($"The {specialFolder} folder could not be found.");
    }

    private async Task<IMailFolder> OpenAsync(IMailFolder folder, bool enableWrite, CancellationToken cancellationToken)
    {
        var desired = enableWrite ? FolderAccess.ReadWrite : FolderAccess.ReadOnly;
        if (!folder.IsOpen || (enableWrite && folder.Access != FolderAccess.ReadWrite))
            await folder.OpenAsync(desired, cancellationToken).ConfigureAwait(false);
        _folder = folder;
        return folder;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MailFolderClient));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_folder != null && _folder.IsOpen)
            await _folder.CloseAsync(false, CancellationToken.None).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_folder != null && _folder.IsOpen)
            _folder.Close(false, CancellationToken.None);
        _disposed = true;
    }
}