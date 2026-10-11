using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Zuemail.Core.Models;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;
using Zuemail.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MimeKit;

namespace Zuemail.MailKit.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Configures SMTP options from a configuration section.</summary>
        public static IServiceCollection ConfigureSmtp(this IServiceCollection services, IConfiguration configuration,
            string sectionName = SmtpOptions.SectionName)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            services.Configure<SmtpOptions>(configuration.GetRequiredSection(sectionName));
            return services;
        }

        /// <summary>Configures SMTP options from an action.</summary>
        public static IServiceCollection ConfigureSmtp(this IServiceCollection services, Action<SmtpOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            services.Configure(configure);
            return services;
        }

        /// <summary>Registers SMTP message validation and a MailKit sender.</summary>
        public static IServiceCollection AddZuemailSmtpSender(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            services.TryAddTransient<IValidationStrategy<MimeMessage>, MimeMessageValidator>();
            services.AddTransient<MailKitSmtpSender>();
            services.AddTransient<IEmailSender<IEmailMessage>>(provider => provider.GetRequiredService<MailKitSmtpSender>());
            return services;
        }

        /// <summary>Configures and registers SMTP from a configuration section.</summary>
        public static IServiceCollection AddZuemailSmtpSender(this IServiceCollection services,
            IConfiguration configuration, string sectionName = SmtpOptions.SectionName) =>
            services.ConfigureSmtp(configuration, sectionName).AddZuemailSmtpSender();

        /// <summary>Configures and registers SMTP from an options action.</summary>
        public static IServiceCollection AddZuemailSmtpSender(this IServiceCollection services,
            Action<SmtpOptions> configure) => services.ConfigureSmtp(configure).AddZuemailSmtpSender();

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
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (emailOptions == null)
                throw new ArgumentNullException(nameof(emailOptions));
            services.Configure(emailOptions);
            return services;
        }
    }
}
