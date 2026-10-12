# Zuemail Mail Guide

Zuemail combines a transport-independent Core message builder/handler with a MailKit provider for SMTP and IMAP. Core owns message construction and validation; MailKit owns MIME conversion, protocol clients, TLS/authentication, and folder operations.

Composition is fluent and does not send or connect. SMTP and IMAP are configured independently under `Email:Smtp` and `Email:Imap`. The provider tracks whether it created a client or received an injected client, and async disposal drains owned protocol logs.

## Guides

- [Dependency injection](dependency-injection.md)
- [Factories and account lifetimes](factories.md)
- [Sending email](sender.md)
- [Receiving email](receiver.md)
- [Working with folders](working-with-folders.md)

For the implemented feature list see [features.md](../features.md); for short snippets see [usage.md](../usage.md).

## Local Test Server

smtp4dev can provide local SMTP and IMAP endpoints:

```sh
docker run -d -p 3000:80 -p 25:25 -p 143:143 rnwood/smtp4dev
```

The console example performs network operations when run. Unit tests use mocked MailKit clients and do not contact a server.
