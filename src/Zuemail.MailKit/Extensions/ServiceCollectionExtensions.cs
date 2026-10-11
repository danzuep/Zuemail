using System;
using System.IO.Abstractions;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Zuemail.Core.Models;

namespace MailKitSimplified.Sender
{
    [ExcludeFromCodeCoverage]
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add the MailKitSimplified.SmtpSender configuration and services.
        /// Adds IOptions<<see cref="EmailSenderOptions"/>>.
        /// </summary>
        /// <param name="services">Collection of service descriptors.</param>
        /// <param name="emailOptions">Email sender options.</param>
        /// <returns><see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddMailKitSimplifiedEmailSender(this IServiceCollection services, Action<EmailOptions> emailOptions)
        {
            if (emailOptions == null)
                throw new ArgumentNullException(nameof(emailOptions));
            services.Configure(emailOptions);
            return services;
        }
    }
}
