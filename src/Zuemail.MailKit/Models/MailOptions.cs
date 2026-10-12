using Zuemail.Core.Models;
using MailKit.Security;
using MailKit;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Zuemail.MailKit.Services;

namespace Zuemail.MailKit.Models
{
    public class MailOptions : EmailOptions
    {
        private int _protocolLoggerState;
        private IProtocolLogger _protocolLogger;

        public virtual ProtocolLoggerOptions ProtocolLog { get; set; } = new ProtocolLoggerOptions();

        public virtual Lazy<IProtocolLogger> ProtocolLogger() =>
            new Lazy<IProtocolLogger>(GetOrCreateProtocolLogger);

        private IProtocolLogger GetOrCreateProtocolLogger()
        {
            var spinner = new SpinWait();
            while (true)
            {
                var state = Volatile.Read(ref _protocolLoggerState);
                if (state == 2)
                    return _protocolLogger;
                if (state == 3)
                    return null;
                if (state == 0 && Interlocked.CompareExchange(ref _protocolLoggerState, 1, 0) == 0)
                {
                    try
                    {
                        _protocolLogger = !string.IsNullOrWhiteSpace(ProtocolLog?.FilePath)
                            ? new MailKitProtocolLogger(ProtocolLog)
                            : null;
                        Volatile.Write(ref _protocolLoggerState, 2);
                        return _protocolLogger;
                    }
                    catch
                    {
                        Volatile.Write(ref _protocolLoggerState, 0);
                        throw;
                    }
                }
                spinner.SpinOnce();
            }
        }

        /// <summary>Disposes a protocol logger created by these options, if any.</summary>
        public void DisposeProtocolLogger()
        {
            var spinner = new SpinWait();
            while (true)
            {
                var state = Volatile.Read(ref _protocolLoggerState);
                if (state == 3)
                    return;
                if (state == 1)
                {
                    spinner.SpinOnce();
                    continue;
                }
                if (Interlocked.CompareExchange(ref _protocolLoggerState, 3, state) == state)
                {
                    (Interlocked.Exchange(ref _protocolLogger, null) as IDisposable)?.Dispose();
                    return;
                }
            }
        }

        /// <summary>Asynchronously drains and disposes a provider protocol logger.</summary>
        public async ValueTask DisposeProtocolLoggerAsync()
        {
            var spinner = new SpinWait();
            while (true)
            {
                var state = Volatile.Read(ref _protocolLoggerState);
                if (state == 3)
                    return;
                if (state == 1)
                {
                    await Task.Yield();
                    continue;
                }
                if (Interlocked.CompareExchange(ref _protocolLoggerState, 3, state) == state)
                {
                    var protocolLogger = Interlocked.Exchange(ref _protocolLogger, null);
                    if (protocolLogger is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else
                        (protocolLogger as IDisposable)?.Dispose();
                    return;
                }
                spinner.SpinOnce();
            }
        }

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
            copy.ProtocolLog = ProtocolLog?.Copy() ?? new ProtocolLoggerOptions();
            copy.AuthenticationMechanismsToRemove = new List<string>(AuthenticationMechanismsToRemove ?? new List<string>());
            copy.Credential = new NetworkCredential(Username ?? string.Empty, Password ?? string.Empty);
            copy._protocolLogger = null;
            copy._protocolLoggerState = 0;
            return copy;
        }
    }
}