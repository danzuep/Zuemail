using System.Threading.Tasks;
using System.Threading;

namespace Zuemail.Core.Abstractions
{
    /// <summary>
    /// Interface for writing and sending fluent emails.
    /// </summary>
    public interface IEmailMessageHandler : IEmailMessageBuilder
    {
        /// <summary>
        /// Send the email asynchronously and reset the email to the saved defaults.
        /// </summary>
        /// <param name="cancellationToken">Stop the email from sending.</param>
        Task SendAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to send the email asynchronously and reset the email to the saved defaults.
        /// </summary>
        /// <param name="cancellationToken">Stop the email from sending.</param>
        /// <returns>True if the email sent successfully.</returns>
        Task<bool> TrySendAsync(CancellationToken cancellationToken = default);
    }
}
