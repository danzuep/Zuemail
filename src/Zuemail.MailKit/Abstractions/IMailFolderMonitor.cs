using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit;

namespace Zuemail.MailKit.Abstractions;

/// <summary>Processes folder changes while an IMAP client waits with IDLE or NOOP polling.</summary>
public interface IMailFolderMonitor : IDisposable, IAsyncDisposable
{
    IMailFolderMonitor SetIdleMinutes(byte minutes = 9);
    IMailFolderMonitor SetMaxRetries(byte retries = 3);
    IMailFolderMonitor SetRetryDelay(TimeSpan delay);
    IMailFolderMonitor SetIgnoreExistingMailOnConnect(bool ignoreExisting = true);
    IMailFolderMonitor SetMessageSummaryItems(MessageSummaryItems items = MessageSummaryItems.Envelope);
    IMailFolderMonitor OnMessageArrival(Func<IMessageSummary, Task> callback);
    IMailFolderMonitor OnMessageFlagsChanged(Func<IMessageSummary, Task> callback);
    IMailFolderMonitor OnMessageDeparture(Func<IMessageSummary, Task> callback);
    IMailFolderMonitor OnMessageArrival(Action<IMessageSummary> callback);
    IMailFolderMonitor OnMessageFlagsChanged(Action<IMessageSummary> callback);
    IMailFolderMonitor OnMessageDeparture(Action<IMessageSummary> callback);
    Task IdleAsync(CancellationToken cancellationToken = default, bool handleExceptions = true);
}