using System;
using System.Threading;
using System.Threading.Tasks;
using Zuemail.Core.Abstractions;
using Zuemail.Core.Models;
using Microsoft.Extensions.Options;

namespace Zuemail.Core.Services
{
    public sealed class EmailMessageHandler : EmailMessageBuilder, IEmailMessageHandler
    {
        private IValidationStrategy<IEmailMessage> _validationStrategy;
        private readonly IEmailSender<IEmailMessage> _emailSender;

        public EmailMessageHandler(IEmailSender<IEmailMessage> emailSender, IOptions<IEmailMessage> options = null, IValidationStrategy<IEmailMessage> validationStrategy = null) : base(options)
        {
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _validationStrategy = validationStrategy;
        }

        public static EmailMessageHandler Create(IEmailSender<IEmailMessage> emailSender, IEmailMessage template = null) =>
            new EmailMessageHandler(emailSender) { Template = template };

        public EmailMessageHandler SetStrategy(IValidationStrategy<IEmailMessage> validator)
        {
            _validationStrategy = validator;
            return this;
        }

        public void Send(CancellationToken cancellationToken = default) =>
            SendAsync(cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        public bool TrySend(CancellationToken cancellationToken = default) =>
            TrySendAsync(cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        public async Task SendAsync(CancellationToken cancellationToken = default)
        {
            _ = await _validationStrategy.ValidateAsync(Email).ConfigureAwait(false);
            await _emailSender.SendAsync(Email, cancellationToken).ConfigureAwait(false);
            Email = Template != null ? Template.Copy() : EmailMessage.Default;
        }

        public async Task<bool> TrySendAsync(CancellationToken cancellationToken = default)
        {
            _ = await _validationStrategy.ValidateAsync(Email).ConfigureAwait(false);
            bool isSent = await _emailSender.TrySendAsync(Email, cancellationToken).ConfigureAwait(false);
            Email = Template != null ? Template.Copy() : EmailMessage.Default;
            return isSent;
        }
    }
}
