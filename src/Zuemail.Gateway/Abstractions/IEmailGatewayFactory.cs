using System;

namespace Zuemail.Gateway.Abstractions
{
    /// <summary>
    /// Represents a type used to configure the logging system and create instances of <see cref="IEmailGateway"/> from
    /// the registered <see cref="IEmailGatewayProvider"/>s.
    /// </summary>
    public interface IEmailGatewayFactory : IDisposable
    {
        /// <summary>
        /// Creates a new <see cref="IEmailGateway"/> instance.
        /// </summary>
        /// <param name="categoryName">The category name for messages produced by the gateway.</param>
        /// <returns>The <see cref="IEmailGateway"/>.</returns>
        IEmailGateway CreateGateway(string categoryName);

        /// <summary>
        /// Adds an <see cref="IEmailGatewayProvider"/> to the gateway system.
        /// </summary>
        /// <param name="provider">The <see cref="IEmailGatewayProvider"/>.</param>
        void AddProvider(IEmailGatewayProvider provider);
    }
}