using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
using MimeKit;

namespace Zuemail.MailKit.Abstractions;

/// <summary>Performs folder lookup and message mutations with explicit access management.</summary>
public interface IMailFolderClient : IDisposable, IAsyncDisposable
{
    string MailFolderName { get; }
    int MailFolderCount { get; }
    IMailFolder SentFolder { get; }
    IMailFolder DraftsFolder { get; }
    IMailFolder JunkFolder { get; }
    IMailFolder TrashFolder { get; }
    ValueTask<IMailFolder> ConnectAsync(bool enableWrite = false, CancellationToken cancellationToken = default);
    Task<IMailFolder> GetFolderAsync(string fullName, bool createIfNotFound = false, CancellationToken cancellationToken = default);
    Task<IMailFolder> GetFolderAsync(IEnumerable<string> possibleNames = null, CancellationToken cancellationToken = default);
    Task<int> AddFlagsAsync(IEnumerable<UniqueId> uniqueIds, MessageFlags flags, bool silent = true, CancellationToken cancellationToken = default);
    Task<int> AddFlagsAsync(SearchQuery query, MessageFlags flags, bool silent = true, CancellationToken cancellationToken = default);
    Task<int> DeleteMessagesAsync(IEnumerable<IMessageSummary> summaries, CancellationToken cancellationToken = default);
    Task<int> DeleteMessagesAsync(TimeSpan olderThan, SearchQuery filter = null, CancellationToken cancellationToken = default);
    Task<UniqueId?> AppendSentMessageAsync(MimeMessage message, MessageFlags flags = MessageFlags.Seen, CancellationToken cancellationToken = default, ITransferProgress transferProgress = null);
    Task<UniqueId?> CopyToAsync(IMessageSummary summary, SpecialFolder destination = SpecialFolder.Drafts, CancellationToken cancellationToken = default);
    Task<UniqueId?> MoveToAsync(UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default);
    Task<UniqueId?> MoveToAsync(UniqueId uid, string destination, CancellationToken cancellationToken = default);
    Task<UniqueId?> MoveToAsync(IMessageSummary summary, IMailFolder destination, CancellationToken cancellationToken = default);
    Task<UniqueId?> MoveToAsync(IMessageSummary summary, string destination, CancellationToken cancellationToken = default);
    Task<UniqueIdMap> MoveToAsync(IEnumerable<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default);
    Task<UniqueIdMap> MoveToAsync(IEnumerable<UniqueId> uids, string destination, CancellationToken cancellationToken = default);
}