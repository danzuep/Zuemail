# Dependency Injection

The application binds SMTP and IMAP settings separately and registers only the provider services it uses.

```csharp
services.AddZuemailSmtpSender(configuration);
services.AddZuemailImapReceiver(configuration);
```

The expected configuration structure is:

```json
{
  "Email": {
    "Smtp": { "Host": "localhost", "Port": 25 },
    "Imap": { "Host": "localhost", "Port": 143, "MailFolderName": "INBOX" }
  }
}
```

Action-based configuration is available when settings come from code:

```csharp
services.AddZuemailSmtpSender(options => { options.Host = "localhost"; options.Port = 25; });
```

```csharp
services.AddZuemailImapReceiver(options => { options.Host = "localhost"; options.Port = 143; options.MailFolderName = "INBOX"; });
```

SMTP and IMAP registrations are transient so each resolved sender/receiver owns an independent client. Register logging through the host as usual. Do not register connected MailKit clients as shared singletons.
