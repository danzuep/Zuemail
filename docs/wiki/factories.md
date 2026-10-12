# Factories and Account Lifetimes

Client factories live on the provider options and create fresh SMTP/IMAP clients with the configured timeout and protocol logger:

```csharp
var smtpClient = smtpOptions.CreateSmtpClient();
```

```csharp
var imapClient = imapOptions.CreateImapClient();
```

Normally let `MailKitSmtpSender` or `ImapReceiver` create and own the client. Constructors accept an injected client for testing or caller-managed ownership; in that case the provider does not dispose the injected client.

`ImapReceiver.Clone()` creates another receiver with copied settings and an independent client:

```csharp
using var otherReceiver = (ImapReceiver)receiver.Clone();
```

Dependency injection currently registers one configured IMAP account per receiver. A multi-account `IImapReceiverFactory` and monitor factory are not implemented; construct/register separate options and receivers explicitly when an application needs multiple accounts. Do not share a client across folders being monitored concurrently.
