using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
using MimeKit;

namespace Zuemail.MailKit.Abstractions;

/// <summary>Fluent selection and retrieval of messages from one IMAP folder.</summary>
public interface IMailReader
{
    IMailReader Top(int count);
    IMailReader Skip(int count);
    IMailReader Take(int count, bool continuous = false);
    IMailReader Range(UniqueId start, UniqueId end, bool continuous = false);
    IMailReader Range(UniqueId start, int batchSize = 0, bool continuous = true);
    IMailReader Query(SearchQuery query);
    IMailReader Items(MessageSummaryItems items);
    Task<IList<IMessageSummary>> GetMessageSummariesAsync(CancellationToken cancellationToken = default);
    Task<IList<MimeMessage>> GetMimeMessagesAsync(CancellationToken cancellationToken = default, ITransferProgress transferProgress = null);
}