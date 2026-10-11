namespace Zuemail.Core.Abstractions
{
    /// <summary>
    /// Simple email contact based on the RFC standard.
    /// </summary>
    public interface IEmailContact
    {
        /// <summary>
        /// The "name" or "display-name" of the mailbox. See
        /// <see href="https://www.rfc-editor.org/rfc/rfc5322#section-3.4">RFC 5322 name-addr and display-name</see>.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// The "email" address or "addr-spec" of the mailbox. See
        /// <see href="https://www.rfc-editor.org/rfc/rfc5322#section-3.4.1">RFC 5322 addr-spec</see>.
        /// </summary>
        string EmailAddress { get; set; }

        /// <summary>
        /// Copy this email contact to re-use it.
        /// </summary>
        /// <returns>Shallow copy of this email contact.</returns>
        IEmailContact Copy();

        /// <summary>
        /// Email contact name and address as a string.
        /// </summary>
        /// <returns></returns>
#if NET6_0_OR_GREATER
        string ToString() => $"\"{Name}\" <{EmailAddress}>";
#else
        string ToString();
#endif
    }
}
