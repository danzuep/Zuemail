using System;

namespace Zuemail.Core.Abstractions
{
    /// <summary>
    /// Fluent email sender of type <see cref="T"/>.
    /// </summary>
    public interface IEmailSender<T> : ISender<T>, IDisposable
    {
        /// <summary>
        /// Write an email fluently with an <see cref="IEmailMessageHandler"/>.
        /// </summary>
        IEmailMessageHandler WriteEmail { get; }

        /// <summary>
        /// Copy this email sender to re-use it.
        /// </summary>
        /// <returns>Shallow copy of this email sender.</returns>
        IEmailMessage Copy();

        /// <summary>
        /// Email envelope (from, to, subject etc.) in a readable format.
        /// </summary>
        /// <returns>Email summary in plain text.</returns>
        string ToString();

    }
}
