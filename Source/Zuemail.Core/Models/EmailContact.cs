using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Zuemail.Core.Abstractions;

namespace Zuemail.Core.Models
{
    public class EmailContact : IEmailContact
    {
        public string Name { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; }

        public EmailContact() { }

        public static IEmailContact Create(string emailAddress, string name = null) =>
            new EmailContactParser(emailAddress, name);

        public IEmailContact Copy() => new EmailContact
        {
            Name = this.Name,
            EmailAddress = this.EmailAddress
        };

        public override string ToString() => $"\"{Name}\" <{EmailAddress}>";
    }
}
