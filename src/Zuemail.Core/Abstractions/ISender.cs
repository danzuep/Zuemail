using System.Threading;
using System.Threading.Tasks;

namespace Zuemail.Core.Abstractions
{
    public interface ISender<T>
    {
        /// <summary>
        /// Send an item asynchronously.
        /// </summary>
        /// <param name="item">Item to send.</param>
        /// <param name="cancellationToken">Stop the item from sending.</param>
        Task SendAsync(T item, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempt to send an item asynchronously.
        /// </summary>
        /// <param name="item">Item to send.</param>
        /// <param name="cancellationToken">Stop the item from sending.</param>
        /// <returns>True if the item sent successfully.</returns>
        Task<bool> TrySendAsync(T item, CancellationToken cancellationToken = default);
    }
}
