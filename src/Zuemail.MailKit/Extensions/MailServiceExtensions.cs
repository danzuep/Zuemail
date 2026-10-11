using MailKit;
using MailKit.Security;
using System.Threading;
using System.Threading.Tasks;
using Zuemail.MailKit.Models;
using MailKit.Net.Smtp;
using MailKit.Net.Imap;
using System;

namespace Zuemail.MailKit.Extensions
{
    public static class MailServiceExtensions
    {
        public static async ValueTask<IMailService> GetConnectedAuthenticatedAsync<T>(this IMailService client, MailOptions clientOptions, CancellationToken cancellationToken = default) where T : IMailService, new()
        {
            if (client == null)
                return null;
            if (clientOptions == null)
                throw new ArgumentNullException(nameof(clientOptions));
            if (!client.IsConnected)
            {
                if (string.IsNullOrWhiteSpace(clientOptions.Host))
                    throw new ArgumentException($"{nameof(SmtpOptions.Host)} is not set.");
                await client.ConnectAsync(clientOptions.Host, clientOptions.Port, clientOptions.SocketOptions, cancellationToken).ConfigureAwait(false);
                if (clientOptions.CapabilitiesToRemove != 0)
                {
                    if (client is SmtpClient smtpClient)
                        smtpClient.Capabilities &= ~(SmtpCapabilities)clientOptions.CapabilitiesToRemove;
                    if (client is ImapClient imapClient)
                        imapClient.Capabilities &= ~(ImapCapabilities)clientOptions.CapabilitiesToRemove;
                }
            }
            if (!client.IsAuthenticated)
            {
                if (clientOptions.AuthenticationCapabilities != null)
                {
                    await clientOptions.AuthenticationCapabilities(client).ConfigureAwait(false);
                }
                else
                {
                    var ntlm = client.AuthenticationMechanisms.Contains("NTLM") ?
                        new SaslMechanismNtlm(clientOptions.Credential) : null;
                    if (ntlm?.Workstation != null)
                        await client.AuthenticateAsync(ntlm, cancellationToken).ConfigureAwait(false);
                    else
                        await client.AuthenticateAsync(clientOptions.Credential, cancellationToken).ConfigureAwait(false);
                }
            }
            return client;
        }
    }
}
