using System;
using System.Net;
using MailKit;
using System.Threading.Tasks;
using System.Threading;
using MailKit.Net.Smtp;
using Zuemail.MailKit.Extensions;
using Zuemail.Core.Models;

namespace Zuemail.MailKit.Models
{
    public sealed class SmtpOptions : MailOptions
    {
        /// <summary>The configuration section used for SMTP accounts.</summary>
        public new const string SectionName = EmailOptions.SectionName + ":Smtp";

        public SmtpCapabilities SmtpCapabilitiesToRemove
        {
            get => (SmtpCapabilities)CapabilitiesToRemove;
            set => CapabilitiesToRemove = (ulong)value;
        }

        public Func<ISmtpClient, Task> SmtpAuthenticationCapabilities
        {
            get => AuthenticationCapabilities == null ? null : client => AuthenticationCapabilities(client as ISmtpClient
                ?? throw new ArgumentException("The MailKit service is not an SMTP client.", nameof(client)));
            set => AuthenticationCapabilities = value == null ? null : service => value(service as ISmtpClient
                ?? throw new ArgumentException("The MailKit service is not an SMTP client.", nameof(service)));
        }

        /// <summary>Creates a fresh SMTP client with this option set's timeout and logger.</summary>
        public SmtpClient CreateSmtpClient(IProtocolLogger protocolLogger = null)
        {
            protocolLogger ??= ProtocolLogger().Value;
            var client = protocolLogger == null ? new SmtpClient() : new SmtpClient(protocolLogger);
            client.Timeout = TimeoutMs;
            return client;
        }

        public ValueTask ConnectAuthenticateAsync(CancellationToken cancellationToken = default) =>
            ConnectAuthenticateAsync(CreateSmtpClient(), cancellationToken);

        /// <summary>Connects/authenticates the client owned by a sender.</summary>
        public async ValueTask ConnectAuthenticateAsync(ISmtpClient client, CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            await client.GetConnectedAuthenticatedAsync<SmtpClient>(this, cancellationToken).ConfigureAwait(false);
        }

        public override EmailOptions Copy()
        {
            var copy = (SmtpOptions)base.Copy();
            var authentication = SmtpAuthenticationCapabilities;
            copy.SmtpAuthenticationCapabilities = authentication;
            return copy;
        }
    }
}