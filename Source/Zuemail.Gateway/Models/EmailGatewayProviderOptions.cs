using System;
using System.ComponentModel.DataAnnotations;

namespace Zuemail.Gateway.Models
{
    public partial class EmailGatewayProviderOptions
    {
        [Required]
        public string Host { get; set; }
        public ushort Port { get; set; } = 0;
        public virtual BasicCredentail Credential { get; set; }

        public override string ToString() => $"{Host}:{Port} ({Credential})";
    }
}
