# Zuemail

Zuemail is a small .NET email library built around a fluent, reusable message builder and a separate send handler. Compose messages from contacts, headers, subjects, plain-text/HTML bodies, and attachments, then explicitly send them through the MailKit provider. Core message construction and validation stay separate from SMTP/IMAP transport concerns.

## Projects

- `src/Zuemail.Core` contains message and contact models, fluent builder/handler contracts, templates, validation, and transport-neutral options.
- `src/Zuemail.MailKit` adapts Core messages to MimeKit and provides MailKit SMTP/IMAP integration, authentication, TLS, and protocol options.
- `src/ConsoleServiceExample` demonstrates composing messages and using the MailKit clients.
- `src/Zuemail.Core.Tests` contains transport-independent Core tests.
- `src/Zuemail.MailKit.Tests` tests SMTP/IMAP integration and provider configuration. Both test projects use Microsoft.Testing.Platform.

The Core and MailKit libraries target `netstandard2.0` and `net10.0`; the console and tests target `net10.0`. SMTP and IMAP settings bind separately under `Email:Smtp` and `Email:Imap`. See [usage examples](docs/usage.md), [the feature inventory](docs/features.md), [the architecture diagrams](docs/architecture.md), and [the remaining parity plan](docs/fluent-email-plan.md).

## Fluent Composition

Reusable actions define shared message details; copies allow per-message changes without sharing mutable collections. Building a message does not connect to a server or send it. Delivery happens only when an explicitly configured sender is called.

```csharp
Action<IEmailMessageBuilder> welcome = email => email
    .From("sender@example.com")
    .Header("X-Campaign", "welcome")
    .Body("Welcome", "<p>Welcome</p>");

var template = new EmailMessageBuilder().Compose(welcome).SaveAsTemplate();
var message = template.Copy()
    .To("recipient@example.com")
    .Subject("Hello")
    .Build();

// Send the built message with a configured IEmailSender<IEmailMessage>.
```

## Build and Test

```sh
dotnet build src/Zuemail.slnx -p:GeneratePackageOnBuild=false
dotnet test --project src/Zuemail.Core.Tests/Zuemail.Core.Tests.csproj
dotnet test --project src/Zuemail.MailKit.Tests/Zuemail.MailKit.Tests.csproj
```

SMTP and IMAP settings are separate nested sections under `Email:Smtp` and `Email:Imap`; see [the architecture guide](docs/architecture.md).

The console sample uses configured mail servers and performs real network operations when run; building it does not send or receive mail. Use [smtp4dev](https://github.com/rnwood/smtp4dev) or similar to try it out locally, e.g. `docker run -d -p 3000:80 -p 25:25 -p 143:143 rnwood/smtp4dev`.
