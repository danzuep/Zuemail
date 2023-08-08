using Zuemail.Gateway.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Zuemail.Gateway.Extensions
{
    public static partial class EmailServiceCollectionExtensions
    {
        public static IServiceCollection AddEmailOptions(this IServiceCollection services, Action<IEmailGatewayBuilder> configure) { throw null; }
    }
}