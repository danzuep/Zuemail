using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using MailKit;
using MailKit.Security;
using MailKit.Net.Imap;
using System.Collections.Generic;
using Zuemail.MailKit.Extensions;
using Zuemail.Core.Models;

namespace Zuemail.MailKit.Models
{
    public sealed class ImapOptions : MailOptions
    {
        /// <summary>The configuration section used for IMAP accounts.</summary>
        public new const string SectionName = "Imap";

        /// <summary>Gets or sets the folder opened by default.</summary>
        public string MailFolderName { get; set; } = "INBOX";

        /// <summary>Gets or sets optional access opened when connecting a folder.</summary>
        public FolderAccess MailFolderAccess { get; set; } = FolderAccess.None;

        /// <summary>Gets or sets an optional explicit SASL mechanism.</summary>
        public SaslMechanism AuthenticationMechanism { get; set; }

        /// <summary>Gets or sets additional authentication setup, for example OAuth token setup.</summary>
        public Func<IImapClient, CancellationToken, Task> CustomAuthentication { get; set; }

        public ImapCapabilities ImapCapabilitiesToRemove
        {
            get => (ImapCapabilities)CapabilitiesToRemove;
            set => CapabilitiesToRemove = (ulong)value;
        }

        public Func<IImapClient, Task> ImapAuthenticationCapabilities
        {
            get => AuthenticationCapabilities == null ? null : client => AuthenticationCapabilities(client as IImapClient
                ?? throw new ArgumentException("The MailKit service is not an IMAP client.", nameof(client)));
            set => AuthenticationCapabilities = value == null ? null : service => value(service as IImapClient
                ?? throw new ArgumentException("The MailKit service is not an IMAP client.", nameof(service)));
        }

        public override Func<IMailService, Task> AuthenticationCapabilities { get; set; } = CompressAsync;

        private static async Task CompressAsync(IMailService service)
        {
            var client = service as IImapClient ?? throw new ArgumentException("The MailKit service is not an IMAP client.", nameof(service));
            if (client.Capabilities.HasFlag(ImapCapabilities.Compress))
                await client.CompressAsync().ConfigureAwait(false);
        }

        public override EmailOptions Copy()
        {
            var copy = (ImapOptions)base.Copy();
            copy.MailFolderName = MailFolderName;
            copy.MailFolderAccess = MailFolderAccess;
            copy.AuthenticationMechanism = AuthenticationMechanism;
            copy.CustomAuthentication = CustomAuthentication;
            var authentication = ImapAuthenticationCapabilities;
            copy.ImapAuthenticationCapabilities = authentication;
            return copy;
        }

        public Lazy<ImapClient> ImapClient => new Lazy<ImapClient>(() =>
            ProtocolLogger().Value == null ? new ImapClient() { Timeout = TimeoutMs } :
                new ImapClient(ProtocolLogger().Value) { Timeout = TimeoutMs });

        public async ValueTask ConnectAuthenticateAsync(CancellationToken cancellationToken = default) =>
            await ImapClient.Value.GetConnectedAuthenticatedAsync<ImapClient>(this, cancellationToken).ConfigureAwait(false);

    }
}