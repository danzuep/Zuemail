using System;

namespace Zuemail.MailKit.Abstractions;

/// <summary>Compatibility alias for <see cref="IEmailReceiver"/>.</summary>
[Obsolete("Use IEmailReceiver instead.")]
public interface IImapReceiver : IEmailReceiver
{
    /// <summary>Clones the legacy receiver interface.</summary>
    new IImapReceiver Clone();
}