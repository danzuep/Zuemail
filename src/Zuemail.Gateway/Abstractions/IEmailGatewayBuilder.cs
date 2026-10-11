using Microsoft.Extensions.DependencyInjection;

namespace Zuemail.Gateway.Abstractions
{
    public partial interface IEmailGatewayBuilder
    {
        IServiceCollection Services { get; }
    }
}