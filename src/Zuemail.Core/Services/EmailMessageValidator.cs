using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Zuemail.Core.Abstractions;
using Zuemail.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Zuemail.Core.Services
{
    public class EmailMessageValidator : IValidationStrategy<IEmailMessage>
    {
        private readonly ILogger<EmailMessageValidator> logger;

        public EmailMessageValidator(ILogger<EmailMessageValidator> logger = null)
        {
            this.logger = logger ?? NullLogger<EmailMessageValidator>.Instance;
        }

        public virtual Task<bool> ValidateAsync(IEmailMessage email)
        {
            bool isValid = false;
            if (email != null)
            {
                var from = email.From.Select(m => m.EmailAddress);
                var toCcBcc = email.To.Select(m => m.EmailAddress)
                    .Concat(email.Cc.Select(m => m.EmailAddress))
                    .Concat(email.Bcc.Select(m => m.EmailAddress));
                isValid = ValidateEmailAddresses(from, toCcBcc, logger);
#if DEBUG
                if (email.ReplyTo.Count == 0 && email.From.Count == 0)
                    email.ReplyTo.Add(EmailContact.Create("Unmonitored", $"noreply@localhost"));
                if (email.From.Count == 0)
                    email.From.Add(EmailContact.Create("LocalHost", $"{Guid.NewGuid():N}@localhost"));
#endif
            }
            return Task.FromResult(isValid);
        }

        public static bool ValidateEmailAddresses(IEnumerable<string> sourceEmailAddresses, IEnumerable<string> destinationEmailAddresses, ILogger logger)
        {
            if (sourceEmailAddresses is null)
                throw new ArgumentNullException(nameof(sourceEmailAddresses));
            if (destinationEmailAddresses is null)
                throw new ArgumentNullException(nameof(destinationEmailAddresses));
            bool isValid = true;
            int sourceEmailAddressCount = 0, destinationEmailAddressCount = 0;
            foreach (var from in sourceEmailAddresses)
            {
                if (!from.Contains('@'))
                {
                    logger.LogWarning($"From address is invalid ({from})");
                    isValid = false;
                }
                foreach (var to in destinationEmailAddresses)
                {
                    if (!to.Contains('@'))
                    {
                        logger.LogWarning($"To address is invalid ({to})");
                        isValid = false;
                    }
                    if (to.Equals(from, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning($"Circular reference, To ({to}) == From ({from})");
                        isValid = false;
                    }
                    destinationEmailAddressCount++;
                }
                sourceEmailAddressCount++;
            }
            if (sourceEmailAddressCount == 0)
                logger.LogWarning("Source email address not specified");
            else if (destinationEmailAddressCount == 0)
                logger.LogWarning("Destination email address not specified");
            isValid &= sourceEmailAddressCount > 0 && destinationEmailAddressCount > 0;
            return isValid;
        }
    }
}