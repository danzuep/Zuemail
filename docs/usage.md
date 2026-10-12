# Zuemail Usage

Examples assume `Zuemail.Core`, `Zuemail.MailKit`, and the needed Microsoft.Extensions packages are referenced. Fluent construction does not perform network or file I/O; sending, reading, and monitoring do.

## Compose a Reusable Message

```csharp
Action<IEmailMessageBuilder> defaults = email => email.From("sender@example.com").Header("X-Campaign", "welcome").BodyText("Welcome").BodyHtml("<p>Welcome</p>");
```

```csharp
var message = new EmailMessageBuilder().Compose(defaults).To("recipient@example.com").Subject("Hello").Build();
```

## Send Through SMTP

```csharp
var sent = await smtpSender.TrySendAsync(message, cancellationToken);
```

The sender can be created directly with `IOptions<SmtpOptions>` or registered from the `Email:Smtp` configuration section using `AddZuemailEmailSender(configuration)`.

When `ProtocolLog.FilePath` is set in the `Smtp` or `Imap` section, the provider creates an owned async protocol logger, redacts authentication secrets with MailKit's detector, and drains queued output when its sender/receiver is disposed. Use a separate path per concurrently active protocol client.

## Read IMAP Messages

```csharp
var summaries = await receiver.ReadMail.Query(SearchQuery.NotSeen).Top(25).Items(MessageSummaryItems.Envelope).GetMessageSummariesAsync(cancellationToken);
```

```csharp
var messages = await receiver.ReadMail.Range(firstUid, batchSize: 100).GetMimeMessagesAsync(cancellationToken);
```

Dispose returned `MimeMessage` instances after processing them.

## Move or Delete Messages

```csharp
var movedUid = await receiver.MailFolderClient.MoveToAsync(summary, receiver.MailFolderClient.SentFolder, cancellationToken);
```

```csharp
var deletedCount = await receiver.MailFolderClient.DeleteMessagesAsync(summaries, cancellationToken);
```

Delete marks and expunges only the selected UIDs.

## Monitor Folder Changes

```csharp
await receiver.MonitorFolder.SetIgnoreExistingMailOnConnect().SetMessageSummaryItems(MessageSummaryItems.Envelope).OnMessageArrival(summary => ProcessAsync(summary)).IdleAsync(cancellationToken);
```

The monitor uses IMAP IDLE when available and polling otherwise. Configure and bind IMAP separately under `Email:Imap` with `AddZuemailEmailReceiver(configuration)`.

## Configuration Shape

```json
{
  "Email": {
    "Username": "",
    "Password": "",
    "Smtp": {
      "Host": "localhost",
      "Port": 25,
      "ProtocolLog": { "FilePath": "Logs/smtp.log", "TimestampFormat": "default" }
    },
    "Imap": {
      "Host": "localhost",
      "Port": 143,
      "MailFolderName": "INBOX",
      "ProtocolLog": { "FilePath": "Logs/imap.log", "TimestampFormat": "default" }
    }
  }
}
```

See [features.md](features.md) for implemented behavior and limits, and [architecture.md](architecture.md) for dependency and lifecycle diagrams.
