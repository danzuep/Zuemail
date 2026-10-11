using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using MailKit;
using MailKit.Net.Imap;
using Zuemail.MailKit.Extensions;
using Zuemail.Core.Models;

namespace Zuemail.MailKit.Models
{
    public sealed class ImapOptions : MailOptions
    {
        public ImapCapabilities ImapCapabilitiesToRemove
        {
            get => (ImapCapabilities)CapabilitiesToRemove;
            set => CapabilitiesToRemove = (ulong)value;
        }

        public Func<IImapClient, Task> ImapAuthenticationCapabilities
        {
            get => AuthenticationCapabilities;
            set => AuthenticationCapabilities = (Func<IMailService, Task>)value;
        }

        public override Func<IMailService, Task> AuthenticationCapabilities { get; set; } = CompressAsync;

        private static async Task CompressAsync(IMailService mailService)
        {
            var imapClient = (IImapClient)mailService;
            if (imapClient.Capabilities.HasFlag(ImapCapabilities.Compress))
                await imapClient.CompressAsync().ConfigureAwait(false);
        }

        public Lazy<ImapClient> ImapClient => new Lazy<ImapClient>(() =>
            ProtocolLogger().Value == null ? new ImapClient() { Timeout = TimeoutMs } :
                new ImapClient(ProtocolLogger().Value) { Timeout = TimeoutMs });

        public async ValueTask ConnectAuthenticateAsync(CancellationToken cancellationToken = default) =>
            await ImapClient.Value.GetConnectedAuthenticatedAsync<ImapClient>(this, cancellationToken).ConfigureAwait(false);

        public override EmailOptions Copy()
        {
            var copy = MemberwiseClone() as ImapOptions;
            copy.ProtocolLog = ProtocolLog.Copy();
            return copy;
        }
    }
}