using MimeKit;
using MailKitSimplified.Receiver.Extensions;
using CommunityToolkit.Common;
using CommunityToolkit.Diagnostics;

namespace Zuemail.Extensions
{
    public static class EmailConverter
    {
        public static Models.Email Convert(this MimeMessage mimeMessage)
        {
            Guard.IsNotNull(mimeMessage, nameof(mimeMessage));
            var email = new Models.Email();
            email.MessageId = mimeMessage.MessageId;
            email.Date = mimeMessage.Date.ToString();
            email.From = mimeMessage.From.ToString();
            email.To = mimeMessage.To.ToString();
            email.Cc = mimeMessage.Cc.ToString();
            email.Bcc = mimeMessage.Bcc.ToString();
            email.Headers = mimeMessage.Headers.ToEnumeratedString();
            email.Attachments = mimeMessage.Attachments?.Count().ToString() ?? "0";
            email.Subject = mimeMessage.Subject ?? string.Empty;
            email.BodyText = mimeMessage.TextBody?.Trim() ??
                mimeMessage.HtmlBody?.DecodeHtml() ?? string.Empty;
            email.BodyHtml = mimeMessage.HtmlBody?.Trim() ?? email.BodyText;
            return email;
        }

        public static IEnumerable<Models.Email> Convert<TIn>(this IEnumerable<TIn>? values) where TIn : MimeMessage =>
            values?.Select(c => c?.Convert()!).Where(c => c != null) ?? Array.Empty<Models.Email>();
    }
}
