using System;
using System.ComponentModel.DataAnnotations;

namespace Zuemail.Core.Models
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public static readonly string Localhost = "localhost";

        public static EmailOptions Default { get; set; } = new EmailOptions();

        [Required(ErrorMessage = "Host is required")]
        public string Host { get; set; } = string.Empty;

        public ushort Port { get; set; } = 0;

        public string Username { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Duration)]
        public TimeSpan? Timeout { get; set; } = null;

        public static EmailOptions Create(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentNullException(nameof(host));
            var options = new EmailOptions();
            options.SetHost(host);
            return options;
        }

        public virtual EmailOptions SetHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentNullException(nameof(host));

            string parsedHost = host;
            ushort? parsedPort = null;
            if (host[0] == '[')
            {
                var closingBracket = host.IndexOf(']');
                if (closingBracket < 0)
                    throw new ArgumentException("The bracketed IPv6 host is invalid.", nameof(host));
                parsedHost = host.Substring(1, closingBracket - 1);
                var suffix = host.Substring(closingBracket + 1);
                if (suffix.Length > 0)
                {
                    if (suffix[0] != ':' || !ushort.TryParse(suffix.Substring(1), out var ipv6Port))
                        throw new ArgumentException("The host port is invalid.", nameof(host));
                    parsedPort = ipv6Port;
                }
            }
            else
            {
                var colonIndex = host.LastIndexOf(':');
                if (colonIndex > 0 && host.IndexOf(':') == colonIndex)
                {
                    if (!ushort.TryParse(host.Substring(colonIndex + 1), out var hostPort))
                        throw new ArgumentException("The host port is invalid.", nameof(host));
                    parsedHost = host.Substring(0, colonIndex);
                    parsedPort = hostPort;
                }
            }

            if (string.IsNullOrWhiteSpace(parsedHost))
                throw new ArgumentException("A host is required.", nameof(host));
            Host = parsedHost;
            if (parsedPort.HasValue)
                Port = parsedPort.Value;
            return this;
        }

        public virtual EmailOptions SetPort(ushort port = 0)
        {
            Port = port;
            return this;
        }

        public virtual EmailOptions SetCredential(string username, string password)
        {
            Username = username;
            Password = password;
            return this;
        }

        public virtual EmailOptions SetTimeout(TimeSpan? connectionTimeout)
        {
            if (connectionTimeout.HasValue)
                Timeout = connectionTimeout.Value;
            return this;
        }

        public virtual EmailOptions Copy() => MemberwiseClone() as EmailOptions;

        public override string ToString() => Host;
    }
}