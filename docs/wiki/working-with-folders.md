# Working with IMAP Folders

Get the configured folder or look up a folder by full name:

```csharp
var inbox = await receiver.MailFolderClient.ConnectAsync(enableWrite: false, cancellationToken: cancellationToken);
```

```csharp
var archive = await receiver.MailFolderClient.GetFolderAsync("Archive/2026", createIfNotFound: true, cancellationToken: cancellationToken);
```

Discover folders across available namespaces:

```csharp
var names = await receiver.GetMailFolderNamesAsync(cancellationToken);
```

Add flags or delete only selected summaries. Deletion uses UID-scoped expunge:

```csharp
await receiver.MailFolderClient.AddFlagsAsync(summaries.Select(summary => summary.UniqueId), MessageFlags.Seen, cancellationToken: cancellationToken);
```

```csharp
var deleted = await receiver.MailFolderClient.DeleteMessagesAsync(summaries, cancellationToken);
```

Move a summary or append an outgoing message to Sent:

```csharp
var movedUid = await receiver.MailFolderClient.MoveToAsync(summary, receiver.MailFolderClient.SentFolder, cancellationToken);
```

```csharp
var sentUid = await receiver.MailFolderClient.AppendSentMessageAsync(mimeMessage, cancellationToken: cancellationToken);
```

Folder mutations require write access. A folder already open read-only is not silently upgraded; close/reopen it with write access first. Common special-folder names are searched when the server lacks special-use support. Verify provider-specific folder names with the target server.
