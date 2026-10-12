using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Models;

namespace Zuemail.MailKit.Services;

/// <summary>Compatibility alias for <see cref="EmailReceiver"/>.</summary>
[Obsolete("Use EmailReceiver and IEmailReceiver instead.")]
public sealed class ImapReceiver : EmailReceiver, IImapReceiver
{
    public ImapReceiver(IOptions<ImapOptions> options, ILogger<ImapReceiver> logger = null,
        IProtocolLogger protocolLogger = null, IImapClient imapClient = null)
        : base(options, logger, protocolLogger, imapClient)
    {
    }

    public static ImapReceiver Create(string host, ushort port = 0, string username = null, string password = null,
        string folderName = null, ILogger<ImapReceiver> logger = null, IProtocolLogger protocolLogger = null,
        IImapClient imapClient = null)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentNullException(nameof(host));
        var options = new ImapOptions
        {
            Host = host,
            Port = port,
            Username = username ?? string.Empty,
            Password = password ?? string.Empty,
            MailFolderName = string.IsNullOrWhiteSpace(folderName) ? "INBOX" : folderName
        };
        return new ImapReceiver(Options.Create(options), logger, protocolLogger, imapClient);
    }

    IImapReceiver IImapReceiver.Clone()
    {
        var options = ReceiverOptions.Copy() as ImapOptions;
        return new ImapReceiver(Options.Create(options), logger: null);
    }
}