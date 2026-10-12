using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Zuemail.Core.Abstractions;
using Zuemail.MailKit.Models;

namespace Zuemail.MailKit.Services;

/// <summary>Compatibility alias for <see cref="EmailSender"/>.</summary>
[Obsolete("Use EmailSender instead.")]
public sealed class MailKitSmtpSender : EmailSender
{
    public MailKitSmtpSender(IOptions<SmtpOptions> senderOptions,
        IValidationStrategy<MimeMessage> validation = null,
        ILogger<MailKitSmtpSender> logger = null,
        ISmtpClient smtpClient = null)
        : base(senderOptions, validation, logger, smtpClient)
    {
    }

    public static MailKitSmtpSender Create(string smtpHost, ISmtpClient smtpClient = null,
        IValidationStrategy<MimeMessage> validation = null, ILogger<MailKitSmtpSender> logger = null)
    {
        if (string.IsNullOrWhiteSpace(smtpHost))
            throw new ArgumentNullException(nameof(smtpHost));
        var options = new SmtpOptions();
        options.SetHost(smtpHost);
        return new MailKitSmtpSender(Options.Create(options), validation, logger, smtpClient);
    }
}