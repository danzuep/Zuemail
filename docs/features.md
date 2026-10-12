# Zuemail Features

Verified inventory for the current `main` branch. Unit tests exercise these behaviors; live SMTP/IMAP servers have not been used as part of verification.

## Core Message Composition

- `EmailMessageBuilder` supports reusable `Compose(Action<IEmailMessageBuilder>)`, separate `BodyText(text)` and `BodyHtml(html)` definitions, recipients, headers, subject, and legacy attachments.
- `Build()`, builder `Copy()`, `SaveAsTemplate()`, and `EmailMessage.Copy()` isolate mutable headers, recipient lists/contacts, attachment dictionaries, and byte arrays. Legacy streams and other non-byte attachment objects remain borrowed references.
- `EmailMessageHandler` is the send boundary. It has default/replaceable validation, respects validation failures, forwards cancellation, resets from an independent template only after success, and retains the message on failure/cancellation.
- Core validation checks sender and combined To/Cc/Bcc addresses, reply-to contacts, and header delimiters without inventing addresses or mutating the message.
- Neutral options are under Core. `EmailOptions.Create()` returns independent state; host parsing supports host-only, host/port, bracketed IPv6, and unbracketed IPv6 literals.

## MailKit SMTP

- `EmailSender` sends Core `IEmailMessage` and MimeKit `MimeMessage` inputs through one validate/connect/authenticate/send path. `MailKitSmtpSender` remains as an obsolete compatibility subclass.
- SMTP options support `Email:Smtp` binding, credentials, timeout, TLS mode, capability/authentication configuration, and protocol logging. New sends use an explicit client factory; the sender tracks whether it owns or was given its SMTP client.
- `MailKitProtocolLogger` formats timestamped/prefixed traffic, masks MailKit-detected authentication secrets, queues output asynchronously, drains on disposal, and supports configured files, stderr, or `ILogger`. Copied SMTP/IMAP options own separate logger instances.
- MIME mapping uses `BodyBuilder` and supports the existing text/HTML and legacy byte, stream, file-path, and `MimeEntity` attachment forms.
- SMTP authentication callbacks use typed adapters. Cancellation is propagated rather than converted into an ordinary `TrySendAsync(false)` result.
- MimeKit extensions provide reply/forward helpers and message conversion.

## MailKit IMAP

- `IEmailReceiver`/`EmailReceiver` own created clients, preserve injected-client ownership, connect/authenticate with cancellation, support forced reconnect, list namespaces/folders, resolve the configured folder, clone configuration, and disconnect/dispose safely. `IImapReceiver`/`ImapReceiver` remain obsolete compatibility aliases.
- `IMailFolderReader` fluently selects by `Top`, `Skip`, `Take`, continuous batches, UID ranges, and `SearchQuery`, and fetches message summaries or full `MimeMessage` instances with cancellation/transfer progress.
- `IMailFolderClient` supports read/write access, folder lookup/creation, common Sent/Drafts/Junk/Trash resolution, flags, deletion with UID-scoped expunge, append to Sent, copy, and move operations.
- `IMailFolderMonitor` dispatches arrival, flag-change, and departure callbacks; controls treatment of existing messages; uses IMAP IDLE when available and NOOP polling otherwise; and supports cancellation, capped retry delays, and reconnecting its separate monitor/fetch receivers.
- IMAP options support the nested `Email:Imap` section, folder/access selection, credentials, TLS, SASL/custom authentication, capability/mechanism filtering, compression capability hooks, timeout, and protocol logging.
- SMTP and IMAP both have configuration/action DI helpers and transient provider registrations using `EmailSender`/`IEmailReceiver`. The former SMTP/IMAP-specific names and registration methods remain obsolete compatibility aliases.

## Project and Verification Boundaries

- `Zuemail.Core` has no MailKit/MimeKit reference. `Zuemail.MailKit` references Core and owns both transport adapters.
- `Zuemail.Core.Tests` references Core only. `Zuemail.MailKit.Tests` contains SMTP/IMAP provider, options/configuration, and client/folder lifecycle tests. Both use xUnit v3 with Microsoft.Testing.Platform.
- Verified: `dotnet build src/Zuemail.slnx -p:GeneratePackageOnBuild=false` passed for both library TFMs, tests, and console.
- Concise guides for setup, DI, factories, SMTP, IMAP, and folders are in [docs/wiki](wiki/home.md).

## Current Limits

- No live-server SMTP/IMAP integration tests have been run; server-specific authentication, special-use folders, and protocol-log wire behavior still need verification.
- IMAP summary/message selection does not yet expose envelope/body-only retrieval, EML saving, async streaming, or callback batch processing through `IMailReader`.
- There is no multi-account `IEmailReceiverFactory`/mailbox configuration API. DI currently registers one transient receiver from one configured `ImapOptions` instance.
- The monitor refreshes a folder summary snapshot after notifications. This is simple and tested, but may be expensive for large mailboxes; optimize only with measurement and UID-based incremental semantics.
- Folder copying currently has fewer convenience overloads than moving; MIME attachment APIs remain the existing object-based contract rather than a replayable typed descriptor.
- Legacy `Lazy<SmtpClient>`/`Lazy<ImapClient>` properties remain as obsolete compatibility surfaces. New sender/receiver ownership uses explicit client factories.
- Pooling, cloud providers, SMTP retries, durable queues/outboxes, and background channel workers are not implemented.
