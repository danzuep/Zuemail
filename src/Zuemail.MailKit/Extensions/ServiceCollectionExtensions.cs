using System;
using System.IO.Abstractions;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Zuemail.Core.Models;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;

namespace Zuemail.MailKit.Extensions
{
    [ExcludeFromCodeCoverage]
    public static class ServiceCollectionExtensions
    {
        /// <summary>Configures IMAP account options from a configuration section.</summary>
        public static IServiceCollection ConfigureImap(this IServiceCollection services, IConfiguration configuration,
            string sectionName = ImapOptions.SectionName)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            services.Configure<ImapOptions>(configuration.GetRequiredSection(sectionName));
            return services;
        }

        /// <summary>Configures IMAP options from an action.</summary>
        public static IServiceCollection ConfigureImap(this IServiceCollection services, Action<ImapOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            services.Configure(configure);
            return services;
        }

        /// <summary>Registers a fresh IMAP receiver per resolution.</summary>
        public static IServiceCollection AddZuemailImapReceiver(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            services.AddTransient<IImapReceiver, ImapReceiver>();
            return services;
        }

        /// <summary>Configures and registers an IMAP receiver from a configuration section.</summary>
        public static IServiceCollection AddZuemailImapReceiver(this IServiceCollection services,
            IConfiguration configuration, string sectionName = ImapOptions.SectionName)
        {
            return services.ConfigureImap(configuration, sectionName).AddZuemailImapReceiver();
        }

        /// <summary>Configures and registers an IMAP receiver from an options action.</summary>
        public static IServiceCollection AddZuemailImapReceiver(this IServiceCollection services,
            Action<ImapOptions> configure)
        {
            return services.ConfigureImap(configure).AddZuemailImapReceiver();
        }

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
