using Zuemail.Core.Models;
using MailKit.Security;
using MailKit;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Zuemail.MailKit.Models
{
    public class MailOptions : EmailOptions
    {
        public virtual ProtocolLoggerOptions ProtocolLog { get; set; } = new ProtocolLoggerOptions();

        public virtual Lazy<IProtocolLogger> ProtocolLogger() =>
            new Lazy<IProtocolLogger>(() => ProtocolLog.CreateProtocolLogger());

        public virtual NetworkCredential Credential
        {
            get => new NetworkCredential(Username ?? string.Empty, Password ?? string.Empty);
            set
            {
                if (value != null)
                {
                    Username = value.UserName ?? string.Empty;
                    Password = value.Password ?? string.Empty;
                }
            }
        }

        public virtual SecureSocketOptions SocketOptions { get; set; } = SecureSocketOptions.Auto;

        /// <summary>
        /// Use for Compress, XOAUTH2, OAUTHBEARER etc.
        /// </summary>
        public virtual Func<IMailService, Task> AuthenticationCapabilities { get; set; } = null;

        public virtual ulong CapabilitiesToRemove { get; set; }

        public virtual IList<string> AuthenticationMechanismsToRemove { get; set; } = new List<string>();

        public virtual EmailOptions SetSocketOptions(SecureSocketOptions socketOptions)
        {
            SocketOptions = socketOptions;
            return this;
        }

        public virtual EmailOptions SetCredential(NetworkCredential credential)
        {
            Credential = credential;
            return this;
        }

        public virtual EmailOptions SetProtocolLog(string logFilePath, bool append = false)
        {
            ProtocolLog.FilePath = logFilePath;
            ProtocolLog.AppendToExisting = append;
            return this;
        }

        public virtual EmailOptions SetAuthenticationCapabilities(Func<IMailService, Task> customAuthenticationCapabilities)
        {
            AuthenticationCapabilities = customAuthenticationCapabilities;
            return this;
        }

        public virtual EmailOptions RemoveCapabilities(ulong capabilities)
        {
            CapabilitiesToRemove = capabilities;
            return this;
        }

        public virtual EmailOptions RemoveAuthenticationMechanism(string authenticationMechanismsName)
        {
            AuthenticationMechanismsToRemove.Add(authenticationMechanismsName);
            return this;
        }

        private readonly int _2minsMs = 120000; // 2 mins in milliseconds
        public virtual int TimeoutMs => (int)(Timeout?.TotalMilliseconds ?? _2minsMs);

        internal Lazy<T> DefaultMailService<T>() where T : IMailService, new() =>
            new Lazy<T>(() => new T() { Timeout = TimeoutMs });

        public virtual Lazy<T> MailService<T>() where T : IMailService, new() => DefaultMailService<T>();

        public override EmailOptions Copy()
        {
            var copy = MemberwiseClone() as MailOptions;
            copy.ProtocolLog = ProtocolLog.Copy();
            return copy;
        }
    }
}