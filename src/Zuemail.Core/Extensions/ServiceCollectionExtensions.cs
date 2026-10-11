using Zuemail.Core.Abstractions;
using Zuemail.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Zuemail.Core.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureEmail(this IServiceCollection services, Action<EmailOptions> configure)
        {
            services.Configure(configure);
            return services;
        }

        public static IServiceCollection ConfigureDefaultEmail(this IServiceCollection services, Action<EmailMessage> configure, string sectionName = "Template")
        {
            services.Configure(configure);
            return services;
        }

        /// <summary>
        /// Adds IOptions<<see cref="EmailOptions"/>> configuration.
        /// </summary>
        /// <param name="services">Collection of service descriptors.</param>
        /// <param name="configuration">Application configuration properties.</param>
        /// <param name="sectionName">SMTP configuration section name.</param>
        /// <returns><see cref="IServiceCollection"/>.</returns>
        private static IServiceCollection ConfigureEmailService(this IServiceCollection services, IConfiguration configuration, string sectionName = EmailOptions.SectionName)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            var emailSection = configuration.GetRequiredSection(sectionName);
            services.Configure<EmailOptions>(emailSection);
            return services;
        }

        private static IServiceCollection ConfigureEmailTemplate(this IServiceCollection services, IConfiguration configuration, string sectionName = "Template")
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            var templateSection = configuration.GetRequiredSection(sectionName);
            services.Configure<EmailMessage>(templateSection);
            return services;
        }
    }
}