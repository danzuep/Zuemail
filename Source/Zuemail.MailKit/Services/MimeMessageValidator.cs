namespace Zuemail.MailKit.Services;

public sealed class MimeMessageValidator : IValidationStrategy<MimeMessage>
{
    private readonly ILogger<MimeMessageValidator> logger;

    public MimeMessageValidator()
    {
    }

    public MimeMessageValidator(ILogger<MimeMessageValidator> logger = null)
    {
        this.logger = logger ?? NullLogger<MimeMessageValidator>.Instance;
    }

    public Task<bool> ValidateAsync(MimeMessage mimeMessage)
    {
        bool isValid = false;
        if (mimeMessage != null)
        {
            if (!mimeMessage.BodyParts.Any())
                mimeMessage.Body = new TextPart { Text = string.Empty };
            var from = mimeMessage.From.Mailboxes.Select(m => m.Address);
            var toCcBcc = mimeMessage.To.Mailboxes.Select(m => m.Address)
                .Concat(mimeMessage.Cc.Mailboxes.Select(m => m.Address))
                .Concat(mimeMessage.Bcc.Mailboxes.Select(m => m.Address));
            isValid = EmailMessageValidator.ValidateEmailAddresses(from, toCcBcc, logger);
#if DEBUG
            if (mimeMessage.ReplyTo.Count == 0 && mimeMessage.From.Count == 0)
                mimeMessage.ReplyTo.Add(new MailboxAddress("Unmonitored", $"noreply@localhost"));
            if (mimeMessage.From.Count == 0)
                mimeMessage.From.Add(new MailboxAddress("LocalHost", $"{Guid.NewGuid():N}@localhost"));
#endif
        }
        return Task.FromResult(isValid);
    }
}