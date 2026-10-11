# Zuemail Architecture

## Project Dependencies

Core defines the message model and fluent/send abstractions. The MailKit project implements transport integration against those Core contracts. Applications compose the projects and provide configuration. Core must not depend on MailKit or MimeKit.

```mermaid
flowchart TD
    App[Console or application]
    Core[Zuemail.Core<br/>Messages, fluent builder, handler, validation]
    Provider[Zuemail.MailKit<br/>SMTP/IMAP, MIME mapping, TLS/authentication]
    MailKit[MailKit and MimeKit]

    App --> Core
    App --> Provider
    Provider --> Core
    Provider --> MailKit
```

## Compose and Send

Composition is synchronous and transport-free. The handler validates before invoking the sender. A successful send resets the working message from a copied template; unsuccessful or cancelled sends retain the message for inspection or retry.

```mermaid
sequenceDiagram
    actor App
    participant Builder as EmailMessageBuilder (Core)
    participant Handler as EmailMessageHandler (Core)
    participant Validator as Validation strategy (Core)
    participant Sender as MailKit sender
    participant Client as SMTP client

    App->>Builder: Compose defaults and message fields
    Builder-->>App: Build independent IEmailMessage
    App->>Handler: SendAsync / TrySendAsync
    Handler->>Validator: Validate message and cancellation
    alt valid
        Handler->>Sender: SendAsync(message)
        Sender->>Sender: Map to MimeMessage
        Sender->>Client: Connect/authenticate/send
        Client-->>Sender: Delivery result
        Sender-->>Handler: Success
        Handler->>Handler: Reset from copied template
    else invalid, cancelled, or send failure
        Validator-->>Handler: Failure or cancellation
        Handler->>Handler: Retain current message
    end
```

## Connect and Monitor IMAP

The console uses `IImapReceiver` to connect/authenticate and provides a fluent reader, folder client, and event-based monitor. The reader fetches INBOX summaries/messages with read-only folder access. The monitor uses IDLE only when the server advertises that capability, renews the IDLE wait, falls back to NOOP polling when IDLE is unavailable, dispatches arrivals/flag changes/departures, and supports cancellation/reconnect retries.

```mermaid
sequenceDiagram
    actor App as Console application
    participant Config as IConfiguration
    participant Options as ImapOptions
    participant Receiver as IImapReceiver
    participant Client as MailKit ImapClient
    participant Inbox as INBOX (read-only)

    App->>Config: Bind Email section to ImapOptions
    App->>Receiver: Resolve receiver
    App->>Receiver: ConnectAuthenticatedImapClientAsync
    Receiver->>Client: ConnectAsync(host, port, TLS)
    Receiver->>Client: AuthenticateAsync(credentials)
    App->>Receiver: ReadMail.Skip().Take()
    App->>Inbox: OpenAsync(ReadOnly)
    opt Inbox has messages
        App->>Inbox: Fetch summaries
        Inbox-->>App: Message summaries / UIDs
        App->>Inbox: GetMessageAsync(index)
        Inbox-->>App: MimeMessage
    end
    alt Server advertises IMAP IDLE
        loop Until Ctrl+C
            App->>Receiver: MonitorFolder.IdleAsync
            Receiver->>Inbox: Subscribe to folder-change events
            Receiver->>Client: IdleAsync (bounded wait)
            Inbox-->>Receiver: Count/flags/expunge event
            Receiver->>Inbox: Fetch updated summaries
            Receiver-->>App: Arrival/flags/departure callback
        end
    else IDLE unavailable
        App->>App: Skip monitoring
    end
    App->>Client: DisconnectAsync(true)
```

## Design Patterns in Use

- **Builder:** `EmailMessageBuilder` gathers message fields through fluent methods. `Compose(Action<IEmailMessageBuilder>)` applies reusable defaults, and `Build()` returns a detached message without sending.
- **Prototype-style copying and templates:** `EmailMessage.Copy()` and builder `Copy()` create independent working state. `SaveAsTemplate()` preserves reusable defaults; the handler starts/reset messages from copies. Collections, contacts, and byte arrays are copied, while legacy streams and other objects remain borrowed references.
- **Strategy:** `IValidationStrategy<IEmailMessage>` allows validation to be supplied independently of the handler. `EmailMessageValidator` is the default; `ICancellableValidationStrategy<T>` adds cancellation without breaking legacy strategies.
- **Adapter:** `EmailMessageConverter` maps Core messages to/from MimeKit types. `MailKitSmtpSender` adapts the generic sender contract to SMTP transport. IMAP currently uses MailKit directly in the console, so an IMAP receiver adapter is not yet present.
- **Options/configuration:** `EmailOptions`, `SmtpOptions`, and `ImapOptions` separate neutral connection values from MailKit-specific TLS, capabilities, and protocol behavior. Configuration binding happens in the application composition root.
- **Dependency injection / composition root:** the console chooses options, validators, loggers, and provider clients. Core contracts do not depend on the MailKit project; the provider references Core.
- **Resource ownership:** `using`/`Dispose` and `await using`/`IAsyncDisposable` scope network clients and messages. This is deterministic resource management, not connection pooling; pooling is not currently implemented.

Retries, decorators, channel-backed queues, durable outbox processing, and an IMAP receiver abstraction are not represented as implemented patterns. They remain optional future work when concrete usage requires them.

## Ownership Boundaries

- Core owns generic message composition, copy/template behavior, and validation contracts. Its builder does not connect or send.
- MailKit owns MIME conversion and protocol-specific SMTP/IMAP behavior. Provider settings include TLS, authentication capabilities, and protocol logging.
- The application owns configuration and chooses when to call a sender/receiver. Avoid sharing mutable templates between independent handlers; builders copy configured templates.
- Message copies clone collections, contacts, and byte-array attachments. Legacy streams and arbitrary non-byte attachment values remain borrowed references.
- The console project is an example composition root, not another library layer. It uses the Zuemail IMAP receiver abstraction rather than handling raw IMAP client lifecycle itself.
