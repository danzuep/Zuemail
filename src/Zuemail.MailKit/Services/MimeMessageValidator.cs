namespace Zuemail.MailKit.Services;

public sealed class MimeMessageValidator : IValidationStrategy<MimeMessage>
{
    private readonly ILogger<MimeMessageValidator> _logger;

    public MimeMessageValidator(ILogger<MimeMessageValidator> logger = null)
    {
        _logger = logger ?? NullLogger<MimeMessageValidator>.Instance;
    }

    public Task<bool> ValidateAsync(MimeMessage mimeMessage)
    {
        if (mimeMessage == null)
            return Task.FromResult(false);
        var from = mimeMessage.From.Mailboxes.Select(mailbox => mailbox.Address);
        var recipients = mimeMessage.To.Mailboxes.Select(mailbox => mailbox.Address)
            .Concat(mimeMessage.Cc.Mailboxes.Select(mailbox => mailbox.Address))
            .Concat(mimeMessage.Bcc.Mailboxes.Select(mailbox => mailbox.Address));
        var isValid = EmailMessageValidator.ValidateEmailAddresses(from, recipients, _logger);
        return Task.FromResult(isValid);
    }
}