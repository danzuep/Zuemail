# Zuemail Parity Plan

This is the remaining, trimmed work list for useful feature parity with MailKitSimplified. It builds on Zuemail's Core/MailKit project split and current fluent builder/handler; it does not propose an AST, visitor, gateway project, or new hosting layer.

The implemented surface and known limits are listed in [features.md](features.md). Mark work done there only after focused tests and builds pass.

## Priority 1: Correctness and Live Verification

- [ ] Add local SMTP integration tests for Core-message and MimeMessage sends, MIME alternatives/attachments, Bcc envelope delivery, validation rejection, cancellation, and injected-client ownership. Use a local test server such as smtp4dev; no real account credentials.
- [ ] Add local IMAP integration tests for connect/authenticate, read-only/read-write folder behavior, UID/search retrieval, UID-scoped delete/expunge, append/move, cancellation, IDLE event callbacks, NOOP fallback, and reconnect. Test both an IDLE-capable server and the fallback path where practical.
- [x] Add a provider-owned asynchronous protocol logger with timestamp/prefix formatting, MailKit secret detection, configurable sinks, and deterministic drain/disposal tests.
- [ ] Verify protocol-log wire behavior and redaction against a local SMTP/IMAP server; ensure logs never reveal passwords or tokens.
- [ ] Validate options at startup: required host, timeout range/overflow, port/security combinations, and nonempty credentials where the selected auth mode requires them. Keep secrets redacted from `ToString()` and logs.
- [ ] Resolve `EmailOptions.Default` and compatibility lazy client accessors deliberately: retain only where compatibility requires them, document safe use, and ensure no new path shares mutable options or accidentally creates multiple clients.

## Priority 2: IMAP Feature Gaps

- [ ] Add a configured multi-account receiver factory (`IImapReceiverFactory`/mailbox options) only for applications that need several accounts. Each account/folder receiver gets copied options and an independent client; test account isolation and disposal.
- [ ] Extend `IMailReader` with the highest-value MailKitSimplified retrieval modes: envelope/body-structure-only messages, cancellable `IAsyncEnumerable<MimeMessage>` or callback batches, and saving selected messages as `.eml`. Keep current `Top`/`Skip`/`Take`/UID/SearchQuery semantics and folder lifetime correct during streaming.
- [ ] Complete folder copy convenience overloads for a summary/UID and multiple UIDs, by folder object or folder name. Preserve source/destination ownership and cancellation; never expunge unrelated deleted mail.
- [ ] Add convenience receiver entry points for selecting another folder without mutating shared account options. Each returned reader/monitor must have a clear independent folder/client lifetime.
- [ ] Measure monitor cost on large folders. If full summary snapshots are expensive, implement UID/event-driven incremental tracking with bounded memory and a resync path after reconnect; do not add queues just for abstraction's sake.
- [ ] Exercise nested special-use folder resolution and provider-specific folder names (Gmail, Outlook, Yahoo) against real/test-server behavior; keep lookup overrides configurable rather than hard-coded-only.

## Priority 3: Safer SMTP and Attachment Usability

- [ ] Add a typed attachment API for copied bytes and fresh-stream factories with filename, content type, and optional content ID; preserve `Attach(string, object)` as a compatibility adapter.
- [ ] Make attachment opening deferred, cancellable, and replayable for retries by contract. Specify ownership: factory streams transfer to the MIME message; legacy streams remain borrowed. Dispose partial MIME graphs on failures.
- [ ] Verify MIME conversion preserves reply-to, duplicate/custom headers supported by the model, inline resources, attachments, and Bcc delivery without exposing Bcc in serialized headers.
- [ ] Test `TrySendAsync` failure classification and cancellation with fake clients; keep transient connection failure distinct from ambiguous delivery after SMTP acceptance. Do not add automatic SMTP retries until duplicate-send policy is explicit.
- [ ] Consider SMTP client pooling only after a benchmark demonstrates handshake/client-creation cost is material. A pool must serialize use of each MailKit client and honor owned-versus-injected disposal.

## Priority 4: Samples and Release Readiness

- [ ] Update the console sample and README with nested `Email:Smtp`/`Email:Imap` configuration, SMTP and IMAP DI registration, reader filters, monitor callbacks, and attachment ownership.
- [ ] Check NuGet package resource paths/readme/license metadata for Core and MailKit independently; build packages with the intended CI command.
- [ ] Add CI gates for `dotnet test --project src/Zuemail.Core.Tests/Zuemail.Core.Tests.csproj`, `dotnet test --project src/Zuemail.MailKit.Tests/Zuemail.MailKit.Tests.csproj`, and both library TFMs.
- [ ] Keep MAUI framework/workload upgrades separate from library parity. Upgrade only with an available/required workload and its own build validation.

## Deliberately Deferred

- Cloud-provider adapters until there is a selected provider/use case.
- Generic decorators, Scrutor, HTTP resilience, and automatic SMTP retries until policy/duplicate semantics justify them.
- Channel queues, hosted background workers, and durable outbox processing until an application needs queued/durable delivery.
- Connection pooling until load measurements justify its complexity.
- Typed immutable envelope redesign, ordered header AST, HTML visitor/sanitizer, and additional projects. The current message model and Core/MailKit boundary remain the base.
- Persistent per-account pools until load measurements justify them; current providers own individual clients.
- Incremental large-folder monitor state until a consumer or measurements justify it; current implementation resynchronizes summaries after events/reconnect.

## Verification Baseline

- Core: 40 tests pass; MailKit: 39 tests pass, including protocol logger formatting/redaction and disposal tests.
- `dotnet build src/Zuemail.slnx -p:GeneratePackageOnBuild=false` succeeds for Core/MailKit on `netstandard2.0` and `net10.0`, plus the tests and console.
- Tests are mocked/unit-level. Live SMTP/IMAP interoperability and protocol-log behavior remain unverified.
