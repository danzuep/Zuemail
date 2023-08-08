using CommunityToolkit.Common;
using CommunityToolkit.Diagnostics;
using MailKit;
using Zuemail.Core.Abstractions;
using Zuemail.Core.Models;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Zuemail.MailKit.Extensions
{
    public static class EmailMessageConverter
    {
        public static MimeMessage ToMimeMessage(this IEmailMessage email, CancellationToken cancellationToken = default)
        {
            var mimeMessage = new MimeMessage();

            foreach (var header in email.Headers)
                mimeMessage.Headers.Add(header.Key, header.Value);

            var from = email.From.Select(m => new MailboxAddress(m.Name, m.EmailAddress));
            mimeMessage.From.AddRange(from);

            var replyTo = email.ReplyTo.Select(m => new MailboxAddress(m.Name, m.EmailAddress));
            mimeMessage.ReplyTo.AddRange(replyTo);

            var to = email.To.Select(m => new MailboxAddress(m.Name, m.EmailAddress));
            mimeMessage.To.AddRange(to);

            var cc = email.Cc.Select(m => new MailboxAddress(m.Name, m.EmailAddress));
            mimeMessage.Cc.AddRange(cc);

            var bcc = email.Bcc.Select(m => new MailboxAddress(m.Name, m.EmailAddress));
            mimeMessage.Bcc.AddRange(bcc);

            mimeMessage.Subject = email.Subject ?? string.Empty;

            var bodyBuilder = new BodyBuilder
            {
                TextBody = email.BodyText,
                HtmlBody = email.BodyHtml
            };
            var attachments = email.Attachments
                .Where(a => a.Value is MimeEntity)
                .Select(a => a.Value as MimeEntity);
            foreach (var attachment in attachments)
                bodyBuilder.Attachments.Add(attachment);
            var byteArrays = email.Attachments
                .Where(a => a.Value is byte[]);
            foreach (var byteArray in byteArrays)
                bodyBuilder.Attachments.Add(byteArray.Key, byteArray.Value as byte[]);
            var streams = email.Attachments
                .Where(a => a.Value is Stream);
            foreach (var stream in streams)
                bodyBuilder.Attachments.Add(stream.Key, stream.Value as Stream, cancellationToken);
            var filePaths = email.Attachments
                .Where(a => a.Value is null)
                .Select(a => a.Key);
            foreach (var filePath in filePaths)
                bodyBuilder.Attachments.Add(filePath, cancellationToken);
            mimeMessage.Body = bodyBuilder.ToMessageBody();

            return mimeMessage;
        }

        public static IEnumerable<MimeMessage> ToMimeMessages(this IEnumerable<EmailMessage> values) =>
            values?.Select(c => c?.ToMimeMessage()).Where(c => c != null) ?? Array.Empty<MimeMessage>();

        public static IEmailContact ToEmailContact(this MailboxAddress mailboxAddress)
        {
            Guard.IsNotNull(mailboxAddress, nameof(mailboxAddress));
            return EmailContact.Create(mailboxAddress.Address, mailboxAddress.Name);
        }

        public static IList<IEmailContact> ToEmailContacts(this InternetAddressList mailboxAddresses)
        {
            Guard.IsNotNull(mailboxAddresses, nameof(mailboxAddresses));
            return mailboxAddresses.Mailboxes.Select(m => m.ToEmailContact()).ToList();
        }

        public static EmailMessage ToEmailMessage(this MimeMessage mimeMessage, CancellationToken cancellationToken = default)
        {
            Guard.IsNotNull(mimeMessage, nameof(mimeMessage));
            var email = new EmailMessage
            {
                Headers = mimeMessage.Headers.ToDictionary(h => h.Field, h => h.Value),
                From = mimeMessage.From.ToEmailContacts(),
                ReplyTo = mimeMessage.ReplyTo.ToEmailContacts(),
                To = mimeMessage.To.ToEmailContacts(),
                Cc = mimeMessage.Cc.ToEmailContacts(),
                Bcc = mimeMessage.Bcc.ToEmailContacts(),
                Attachments = mimeMessage.Attachments
                    .ToDictionary(a => a.ContentId, a => (object)a),
                Subject = mimeMessage.Subject ?? string.Empty,
                BodyText = mimeMessage.TextBody?.Trim() ??
                    mimeMessage.HtmlBody?.DecodeHtml() ?? string.Empty
            };
            email.BodyHtml = mimeMessage.HtmlBody?.Trim() ?? email.BodyText;
            return email;
        }

        public static IEnumerable<EmailMessage> ToEmailMessages(this IEnumerable<MimeMessage> values) =>
            values?.Select(c => c?.ToEmailMessage()).Where(c => c != null) ?? Array.Empty<EmailMessage>();
    }
}