using Zuemail.Core.Abstractions;
using Zuemail.Core.Models;
using Microsoft.Extensions.Options;

namespace Zuemail.Core.Services
{
    public class EmailMessageBuilder : IEmailMessageBuilder
    {
        public EmailMessageBuilder(IOptions<IEmailMessage> options = null)
        {
            Template = options?.Value;
        }

        public virtual IEmailMessage Email { get; protected set; } = EmailMessage.Default;

        public virtual IEmailMessage Template { get; set; } = null;

        public virtual IEmailMessageBuilder SaveAsTemplate()
        {
            Template = Email.Copy();
            return this;
        }

        public virtual IEmailMessageBuilder Header(string key, string value)
        {
            Email.Headers.Add(key, value);
            return this;
        }

        public virtual IEmailMessageBuilder From(string emailAddress, string name = null)
        {
            Email.From.Add(EmailContact.Create(emailAddress, name));
            return this;
        }

        public virtual IEmailMessageBuilder To(string emailAddress, string name = null)
        {
            Email.To.Add(EmailContact.Create(emailAddress, name));
            return this;
        }

        public virtual IEmailMessageBuilder Cc(string emailAddress, string name = null)
        {
            Email.Cc.Add(EmailContact.Create(emailAddress, name));
            return this;
        }

        public virtual IEmailMessageBuilder Bcc(string emailAddress, string name = null)
        {
            Email.Bcc.Add(EmailContact.Create(emailAddress, name));
            return this;
        }

        public virtual IEmailMessageBuilder Subject(string subject)
        {
            Email.Subject = subject ?? string.Empty;
            return this;
        }

        public virtual IEmailMessageBuilder Subject(string prefix, string suffix)
        {
            Email.Subject = $"{prefix}{Email.Subject}{suffix}";
            return this;
        }

        public virtual IEmailMessageBuilder Attach(string key, object value = null)
        {
            Email.Attachments.Add(key, value);
            return this;
        }

        public virtual IEmailMessageBuilder BodyText(string plainText)
        {
            Email.BodyText = plainText ?? string.Empty;
            return this;
        }

        public virtual IEmailMessageBuilder BodyHtml(string htmlText)
        {
            Email.BodyHtml = htmlText ?? string.Empty;
            return this;
        }

        public virtual IEmailMessageBuilder Copy() => MemberwiseClone() as IEmailMessageBuilder;
    }
}
