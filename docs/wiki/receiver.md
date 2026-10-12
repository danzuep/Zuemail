# Receiving Email

Resolve or construct an `EmailReceiver` with `ImapOptions`, then explicitly connect/authenticate:

```csharp
var client = await receiver.ConnectAuthenticatedImapClientAsync(cancellationToken);
```

Read newest messages or summaries without downloading all body content:

```csharp
var messages = await receiver.ReadMail.Top(10).GetMimeMessagesAsync(cancellationToken);
```

```csharp
var summaries = await receiver.ReadMail.Query(SearchQuery.NotSeen).Items(MessageSummaryItems.Envelope).GetMessageSummariesAsync(cancellationToken);
```

Dispose returned `MimeMessage` objects after use. Readers open a folder read-only when they need to fetch and close it only if they opened it.

Monitor for arrivals, flag changes, and departures:

```csharp
await receiver.MonitorFolder.SetIgnoreExistingMailOnConnect().OnMessageArrival(summary => HandleArrivalAsync(summary)).IdleAsync(cancellationToken);
```

The monitor uses IMAP IDLE where supported, otherwise NOOP polling; it reconnects transient failures and limits retry delay. The monitor uses separate cloned receivers for IDLE and fetch work because a MailKit client is not re-entrant. Register it with `AddZuemailEmailReceiver(configuration)`.

For summaries use `UniqueId`, `Envelope`, `Flags`, and `BodyStructure` as needed. Envelope/body-only MIME construction and async streaming are not yet exposed by `IMailReader`.
