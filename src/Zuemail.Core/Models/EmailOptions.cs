using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

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
            var options = Default.SetHost(host);
            return options;
        }

        public virtual EmailOptions SetHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentNullException(nameof(host));
            var array = host.Split(':');
            if (array.Length > 1 && ushort.TryParse(array.LastOrDefault(), out ushort port))
            {
                Host = array.First();
                Port = port;
            }
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