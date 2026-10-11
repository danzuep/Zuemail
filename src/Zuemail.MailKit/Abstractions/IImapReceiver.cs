using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;

namespace Zuemail.MailKit.Abstractions;

/// <summary>Owns an IMAP connection and creates folder-scoped read, monitor, and mutation helpers.</summary>
public interface IImapReceiver : IAsyncDisposable, IDisposable
{
    /// <summary>Creates a fluent reader for the configured folder.</summary>
    IMailFolderReader ReadMail { get; }

    /// <summary>Creates an IDLE monitor for the configured folder.</summary>
    IMailFolderMonitor MonitorFolder { get; }

    /// <summary>Creates a folder client for the configured folder.</summary>
    IMailFolderClient MailFolderClient { get; }

    /// <summary>Gets the underlying MailKit client.</summary>
    IImapClient ImapClient { get; }

    /// <summary>Connects and authenticates, reusing a healthy connection by default.</summary>
    ValueTask<IImapClient> ConnectAuthenticatedImapClientAsync(CancellationToken cancellationToken = default, bool force = false);

    /// <summary>Lists folders across personal, shared, and other namespaces.</summary>
    Task<IList<string>> GetMailFolderNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Connects to the configured folder without changing its access unless options request it.</summary>
    ValueTask<IMailFolder> ConnectMailFolderAsync(CancellationToken cancellationToken = default);

    /// <summary>Moves a summary to the server's Sent folder.</summary>
    [Obsolete("Use MailFolderClient.MoveToAsync instead.")]
    Task<UniqueId?> MoveToSentAsync(IMessageSummary messageSummary, CancellationToken cancellationToken = default);

    /// <summary>Disconnects the underlying client.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a receiver with copied options and an independent client.</summary>
    IImapReceiver Clone();
}