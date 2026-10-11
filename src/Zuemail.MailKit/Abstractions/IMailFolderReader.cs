using System;

namespace Zuemail.MailKit.Abstractions;

/// <summary>A disposable reader bound to an IMAP receiver.</summary>
public interface IMailFolderReader : IMailReader, IDisposable, IAsyncDisposable
{
    /// <summary>Creates an independent reader with the same selection.</summary>
    IMailFolderReader Copy();
}