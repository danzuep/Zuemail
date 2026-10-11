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
        public SmtpCapabilities SmtpCapabilitiesToRemove
        {
            get => (SmtpCapabilities)CapabilitiesToRemove;
            set => CapabilitiesToRemove = (ulong)value;
        }

        public Func<ISmtpClient, Task> SmtpAuthenticationCapabilities
        {
            get => AuthenticationCapabilities;
            set => AuthenticationCapabilities = (Func<IMailService, Task>)value;
        }

        public Lazy<SmtpClient> SmtpClient => new Lazy<SmtpClient>(() =>
            ProtocolLogger().Value == null ? new SmtpClient() { Timeout = TimeoutMs } :
                new SmtpClient(ProtocolLogger().Value) { Timeout = TimeoutMs });

        public async ValueTask ConnectAuthenticateAsync(CancellationToken cancellationToken = default) =>
            await SmtpClient.Value.GetConnectedAuthenticatedAsync<SmtpClient>(this, cancellationToken).ConfigureAwait(false);

        public override EmailOptions Copy()
        {
            var copy = MemberwiseClone() as SmtpOptions;
            copy.ProtocolLog = ProtocolLog.Copy();
            return copy;
        }
    }
}