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

        /// <summary>Registers SMTP message validation and an email sender.</summary>
        public static IServiceCollection AddZuemailEmailSender(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            services.TryAddTransient<IValidationStrategy<MimeMessage>, MimeMessageValidator>();
            services.AddTransient(provider => new EmailSender(
                provider.GetRequiredService<IOptions<SmtpOptions>>(),
                provider.GetRequiredService<IValidationStrategy<MimeMessage>>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger<EmailSender>()));
            services.AddTransient<IEmailSender<IEmailMessage>>(provider => provider.GetRequiredService<EmailSender>());
            return services;
        }

        /// <summary>Configures and registers the SMTP-backed email sender from a configuration section.</summary>
        public static IServiceCollection AddZuemailEmailSender(this IServiceCollection services,
            IConfiguration configuration, string sectionName = SmtpOptions.SectionName) =>
            services.ConfigureSmtp(configuration, sectionName).AddZuemailEmailSender();

        /// <summary>Configures and registers the SMTP-backed email sender from an options action.</summary>
        public static IServiceCollection AddZuemailEmailSender(this IServiceCollection services,
            Action<SmtpOptions> configure) => services.ConfigureSmtp(configure).AddZuemailEmailSender();

        /// <summary>Compatibility alias for <see cref="AddZuemailEmailSender(IServiceCollection, IConfiguration, string)"/>.</summary>
        [Obsolete("Use AddZuemailEmailSender instead.")]
        public static IServiceCollection AddZuemailSmtpSender(this IServiceCollection services,
            IConfiguration configuration, string sectionName = SmtpOptions.SectionName) =>
            services.AddZuemailEmailSender(configuration, sectionName);

        /// <summary>Compatibility alias for <see cref="AddZuemailEmailSender(IServiceCollection, Action{SmtpOptions})"/>.</summary>
        [Obsolete("Use AddZuemailEmailSender instead.")]
        public static IServiceCollection AddZuemailSmtpSender(this IServiceCollection services,
            Action<SmtpOptions> configure) => services.AddZuemailEmailSender(configure);

        /// <summary>Registers the SMTP-backed email sender.</summary>
        [Obsolete("Use AddZuemailEmailSender instead.")]
        public static IServiceCollection AddZuemailSmtpSender(this IServiceCollection services)
        {
            return services.AddZuemailEmailSender();
        }

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

        /// <summary>Registers a fresh email receiver per resolution.</summary>
        public static IServiceCollection AddZuemailEmailReceiver(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            services.AddTransient<IEmailReceiver>(provider => new EmailReceiver(
                provider.GetRequiredService<IOptions<ImapOptions>>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger<EmailReceiver>()));
            return services;
        }

        /// <summary>Configures and registers an email receiver from a configuration section.</summary>
        public static IServiceCollection AddZuemailEmailReceiver(this IServiceCollection services,
            IConfiguration configuration, string sectionName = ImapOptions.SectionName)
        {
            return services.ConfigureImap(configuration, sectionName).AddZuemailEmailReceiver();
        }

        /// <summary>Configures and registers an email receiver from an options action.</summary>
        public static IServiceCollection AddZuemailEmailReceiver(this IServiceCollection services,
            Action<ImapOptions> configure)
        {
            return services.ConfigureImap(configure).AddZuemailEmailReceiver();
        }

        /// <summary>Compatibility alias for <see cref="AddZuemailEmailReceiver(IServiceCollection, IConfiguration, string)"/>.</summary>
        [Obsolete("Use AddZuemailEmailReceiver instead.")]
        public static IServiceCollection AddZuemailImapReceiver(this IServiceCollection services,
            IConfiguration configuration, string sectionName = ImapOptions.SectionName) =>
            services.AddZuemailEmailReceiver(configuration, sectionName);

        /// <summary>Compatibility alias for <see cref="AddZuemailEmailReceiver(IServiceCollection, Action{ImapOptions})"/>.</summary>
        [Obsolete("Use AddZuemailEmailReceiver instead.")]
        public static IServiceCollection AddZuemailImapReceiver(this IServiceCollection services,
            Action<ImapOptions> configure) => services.AddZuemailEmailReceiver(configure);

        /// <summary>Registers the IMAP-backed email receiver.</summary>
        [Obsolete("Use AddZuemailEmailReceiver instead.")]
        public static IServiceCollection AddZuemailImapReceiver(this IServiceCollection services)
        {
            return services.AddZuemailEmailReceiver();
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
