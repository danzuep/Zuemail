# Sending Email

Build a reusable message template using separate plain-text and HTML definitions:

```csharp
var template = new EmailMessageBuilder().From("sender@example.com").Subject("Hello").BodyText("Plain text").BodyHtml("<p>HTML</p>").SaveAsTemplate();
```

Create a detached message for each recipient and send it through an explicitly configured provider:

```csharp
var message = template.Copy().To("recipient@example.com").Build();
```

```csharp
var sent = await smtpSender.TrySendAsync(message, cancellationToken);
```

`SendAsync()` propagates validation and transport failures. `TrySendAsync()` returns `false` for handled send failures and propagates cancellation. Both Core-message and MimeKit overloads validate, connect/authenticate, and send through the sender-owned client. Injected clients remain caller-owned.

Current attachments use `Attach(string, object)` and MimeKit `BodyBuilder` mapping. Byte arrays are supported; existing streams are borrowed. A typed replayable attachment API is planned but not yet available.

Protocol logs can be configured at `Email:Smtp:ProtocolLog`. Authentication secrets recognized by MailKit are masked. Use one log path per simultaneously active client, and dispose/async-dispose the sender to flush and close the logger.
