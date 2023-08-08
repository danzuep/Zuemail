using Zuemail.Gateway.Abstractions;
using Zuemail.Gateway.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Zuemail.Gateway.Extensions
{
    /// <summary>
    /// Produces instances of <see cref="IEmailGateway"/> classes based on the given providers.
    /// The structure mirrors that of the <see cref="Microsoft.Extensions.Logging.LoggerFactory"/>.
    /// </summary>
    public partial class EmailGatewayFactory : IEmailGatewayFactory, IDisposable
    {
        /// <summary>
        /// Creates a new <see cref="EmailGatewayFactory"/> instance.
        /// </summary>
        public EmailGatewayFactory() : this(Array.Empty<IEmailGateway>())
        {
        }

        /// <summary>
        /// Creates a new <see cref="EmailGatewayFactory"/> instance.
        /// </summary>
        /// <param name="providers">The providers to use in producing <see cref="IEmailGateway"/> instances.</param>
        public EmailGatewayFactory(IEnumerable<IEmailGateway> providers) : this(providers, new EmailGatewayOptions())
        {
        }

        /// <summary>
        /// Creates a new <see cref="EmailGatewayFactory"/> instance.
        /// </summary>
        /// <param name="providers">The providers to use in producing <see cref="IEmailGateway"/> instances.</param>
        /// <param name="emailGatewayOptions">The filter options to use.</param>
        public EmailGatewayFactory(IEnumerable<IEmailGateway> providers, EmailGatewayOptions emailGatewayOptions) : this(providers, Options.Create(emailGatewayOptions))
        {
        }

        /// <summary>
        /// Creates a new <see cref="EmailGatewayFactory"/> instance.
        /// </summary>
        /// <param name="providers">The providers to use in producing <see cref="IEmailGateway"/> instances.</param>
        /// <param name="emailGatewayOptions">The filter options to use.</param>
        public EmailGatewayFactory(IEnumerable<IEmailGateway> providers, IOptions<EmailGatewayOptions> emailGatewayOptions)
        {
        }

        /// <summary>
        /// Creates new instance of <see cref="IEmailGatewayFactory"/> configured using provided <paramref name="configure"/> delegate.
        /// </summary>
        /// <param name="configure">A delegate to configure the <see cref="IEmailGatewayBuilder"/>.</param>
        /// <returns>The <see cref="IEmailGatewayFactory"/> that was created.</returns>
        public static IEmailGatewayFactory Create(Action<IEmailGatewayBuilder> configure)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddEmailOptions(configure);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var gatewayFactory = serviceProvider.GetRequiredService<IEmailGatewayFactory>();
            return new DisposingEmailGatewayFactory(gatewayFactory, serviceProvider);
        }

        public void AddProvider(IEmailGatewayProvider provider)
        {
            throw new NotImplementedException();
        }

        public void AddProvider(EmailGatewayProviderOptions providerOptions) { }

        public IEmailGateway CreateGateway(string categoryName)
        {
            throw new NotImplementedException();
        }

        //public IEmail CreateEmail(string categoryName) { throw null; }

        public void Dispose() { }

        private sealed class DisposingEmailGatewayFactory : IEmailGatewayFactory
        {
            private readonly IEmailGatewayFactory _emailGatewayFactory;

            private readonly ServiceProvider _serviceProvider;

            public DisposingEmailGatewayFactory(IEmailGatewayFactory loggerFactory, ServiceProvider serviceProvider)
            {
                _emailGatewayFactory = loggerFactory;
                _serviceProvider = serviceProvider;
            }

            public void AddProvider(IEmailGatewayProvider provider)
            {
                _emailGatewayFactory.AddProvider(provider);
            }

            public IEmailGateway CreateGateway(string categoryName)
            {
                return _emailGatewayFactory.CreateGateway(categoryName);
            }

            public void Dispose()
            {
                _serviceProvider.Dispose();
            }
        }
    }

    public static partial class EmailGatewayBuilderExtensions
    {
        public static IEmailGatewayBuilder AddProvider(this IEmailGatewayBuilder builder, IEmailGateway provider) { throw null; }
        public static IEmailGatewayBuilder ClearProviders(this IEmailGatewayBuilder builder) { throw null; }
        public static IEmailGatewayBuilder Configure(this IEmailGatewayBuilder builder, Action<EmailGatewayFactoryOptions> action) { throw null; }
        public static IEmailGatewayBuilder SetMinimumLevel(this IEmailGatewayBuilder builder, LogLevel level) { throw null; }
    }
}