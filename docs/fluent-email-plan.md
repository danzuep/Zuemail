# Zuemail: Practical Fluent Email Plan

## Phase 1: Shared Build Configuration

- [x] Adapt MailKitSimplified's source build props as `Source/Directory.Build.props`, with Zuemail URLs and shared authors/style settings.
- [x] Share `netstandard2.0;net10.0` across Core and MailKit; remove their duplicate framework/URL declarations. Keep console/app target selection separate.
- [x] Remove the unused Gateway project and its solution, console, and shared-build references.
- [x] Set C# 14 for library builds so existing global usings/file-scoped namespaces also compile for `netstandard2.0`; keep runtime targeting separate from language version.
- [x] Pin the installed .NET 10 SDK (`10.0.401`) in `global.json` with `latestPatch` roll-forward and previews disabled.
- [x] Add a small xUnit executable test project on `net10.0` using Microsoft.Testing.Platform v2 and direct `dotnet test --project <project>` execution; avoid legacy adapters.
- [ ] Check package resource paths independently; decide the MAUI framework/workload upgrade separately from the library work. Do not silently change the app's platform targets.

## Phase 2: Independent Messages and Reusable Composition

- [x] Replace message/builder shallow copying with independent headers, recipient lists, contacts, attachment dictionaries, and byte arrays. Preserve documented caller ownership of legacy streams.
- [x] Stop new builders from sharing mutable `EmailMessage.Default` state. Clone defaults and prove callers cannot mutate each other's messages. Option factories remain a phase 4 task.
- [x] Add `Compose(Action<IEmailMessageBuilder>)` to the fluent contract and implementation; invoke it immediately and return the same builder. Reuse actions for signatures, campaigns, recipients, and standard headers.
- [x] Add `Body(string plainText, string html = null)` as a convenience over the existing body properties. No content tree or multipart domain abstraction is needed.
- [x] Add `Build()` returning an independent message snapshot without file access or transport I/O; keep existing `Email` and `Copy()` behavior documented.
- [x] Make `SaveAsTemplate()` capture independent state, initialize builders from independent configured defaults, and ensure composing/copying a template does not mutate the original.

## Phase 3: Send Handler and Validation

- [x] Keep `EmailMessageHandler` as the execution boundary over `EmailMessageBuilder` and the sender interface; do not add a client or send methods to the plain builder.
- [x] Supply a default validation strategy when none is configured; allow an explicit alternate strategy without null dereferences.
- [x] Respect a failed validation result instead of sending anyway. Validate From and the combined To/Cc/Bcc set once, reject header injection, and never manufacture addresses or mutate messages in validation.
- [x] Propagate cancellation through handler validation and transport. Existing `IValidationStrategy<T>` implementations remain supported; optional `ICancellableValidationStrategy<T>` receives the token. Legacy strategies are checked before/after execution but cannot be interrupted by the handler.
- [x] Reset from an independent template after confirmed success. Retain the composed message after failure, cancellation, or `TrySendAsync(false)`; document explicit reset behavior.
- [x] Test repeated sends, template reset, cancellation, validation failure, and message retention with a fake sender before connecting to a real server.
- [x] Use a simple composition-then-send pattern in the console instead of calling terminal handler methods on `IEmailMessageBuilder`. A broader handler-interface redesign is not required for this sample.

## Phase 4: Options, Attachments and MailKit Execution

- [x] Fix `EmailOptions.Create()` to return independent defaults, and `SetHost()` to set host-only inputs as well as host/port inputs. Support bracketed IPv6 and reject malformed port strings.
- [ ] Keep host/port/credentials/optional `TimeSpan` timeout in Core options; keep `SecureSocketOptions`, authentication mechanisms/capabilities, protocol logging, and MailKit delegates in provider options.
- [ ] Make option copies independent for mutable collections and protocol-log settings; redact credentials and validate timeout ranges/conversion before applying them to clients.
- [x] Add explicit SMTP/IMAP client factories used by sender/receiver owners; retain the old lazy properties only as obsolete compatibility wrappers. Never store connected clients in neutral options.
- [x] Adapt authentication delegates with typed wrappers, not casts between `Func<ISmtpClient, Task>`/`Func<IImapClient, Task>` and `Func<IMailService, Task>`.
- [ ] Add a small typed attachment descriptor supporting copied bytes or a fresh-stream factory, with filename/content type/optional content ID. Keep legacy `Attach(string, object)` as a compatibility path.
- [ ] Add fluent typed `Attach(...)`; open files/streams only during cancellable MIME materialization. Define consumer-owned factory streams versus borrowed legacy streams and dispose owned resources on success/failure.
- [ ] Use MimeKit `BodyBuilder` for text/HTML, attachments, and linked resources. Reuse a focused mapping helper; do not write a MIME engine, AST, or visitor.
- [ ] Ensure the provider connects/authenticates before sending, preserves Bcc envelope delivery without exposing Bcc on the wire, and disposes or reuses the correct owned client/message resources.

## Phase 5: Configuration, Verification and Documentation

- [x] Add action/configuration DI registration for separate `Email:Smtp` and `Email:Imap` sections; named account/template support remains only for a demonstrated need.
- [ ] Add a simple cancellation-aware null/fake sender for dry runs and unit tests; do not count simulated sends as delivered mail.
- [ ] Test composition, independent snapshots/defaults, templates, reset policies, host parsing, attachment ownership/replay, MIME alternatives/linked resources, and Core/provider dependency boundaries.
- [ ] Add local SMTP integration tests covering connection/authentication, cancellation, Bcc, and repeated sending; do not depend on real account credentials in tests.
- [ ] Verify both library TFMs and update the console sample/README with concise fluent usage, DI registration, ownership, and migration notes.
- [x] Replace the console's nonexistent receiver wrapper with native MailKit IMAP APIs for reading and IDLE monitoring; build the sample against Core and MailKit only.
- [x] Replace the console's raw MailKit IMAP lifecycle with Zuemail `IImapReceiver`, `IMailReader`, `IMailFolderClient`, and `IMailFolderMonitor` APIs.
- [x] Add DI registration for configured and action-based IMAP options with transient receiver/client ownership.

## Later, Only When Needed

- Add retries/timeouts/telemetry behind the sender interface after defining transient versus ambiguous delivery. Do not blindly retry SMTP after possible acceptance; do not require Scrutor or HTTP resilience packages for simple SMTP usage.
- Add bounded Channels/background workers only for a real queued workload, documenting backpressure and lack of crash durability.
- Add connection pooling only after measuring concurrent-send/handshake costs; maintain exclusive client ownership and bounded capacity.
- Add a cloud provider only after selecting a concrete requirement/provider. Do not make it a gate for finishing the first reliable MailKit workflow.

## First Implementation Sequence

1. Verify the shared build props; add focused Core tests without upgrading the MAUI app.
2. Fix independent message/default/template copies before exposing more reuse.
3. Add `Compose()`, `Body()`, and detached `Build()`; demonstrate reusable defaults in tests.
4. Fix default validation and send/reset behavior, with fake-sender tests.
5. Fix host/options parsing and client ownership; add typed attachments using MimeKit `BodyBuilder`.
6. Finish configuration/sample usage and local SMTP verification. Leave pooling, queueing, cloud adapters, and elaborate policy frameworks for demonstrated needs.

## Fluent Shape

The builder methods below are implemented. Sending still uses an explicitly configured provider.

```csharp
Action<IEmailMessageBuilder> welcome = email => email
    .From("sender@example.com")
    .Header("X-Campaign", "welcome")
    .Body("Welcome", "<p>Welcome</p>");

var message = new EmailMessageBuilder()
    .Compose(welcome)
    .To("recipient@example.com")
    .Subject("Hello")
    .Build();

await sender.SendAsync(message, cancellationToken);
```

## Completion Criteria

- [x] Independently composed messages/defaults/templates isolate mutable message collections, contacts, and byte arrays; legacy stream/object attachments remain explicitly borrowed.
- [ ] Fluent construction performs no network/file I/O; explicit sending respects validation and cancellation.
- [ ] Core remains unaware of MailKit, while provider client/MIME resource ownership is explicit and tested.
- [ ] The simple MailKit workflow passes focused tests and both library TFMs before optional throughput features are added.

## Verified Setup

- Shared library framework and repository metadata are inherited from `Source/Directory.Build.props`; the URL is `https://github.com/danzuep/Zuemail`.
- Core and MailKit compile for `netstandard2.0` and `net10.0`. Library validation uses `-p:GeneratePackageOnBuild=false`; package resource/path validation remains outstanding.
- MSBuild evaluation confirms the console has `TargetFramework=net10.0` and no inherited `TargetFrameworks`. MAUI platform targets are unchanged; its workloads/build were not validated.
- Gateway has been removed; no replacement project is introduced.
- The console now builds using `Microsoft.NET.Sdk` without warnings or errors. Configuration/attachment paths resolve from the executable directory; SMTP options are passed directly to the provider, and IMAP reads/IDLE use the existing MailKit client APIs.
- The initial setup did not execute the console against a live server or introduce automated tests. Subsequent verified composition/handler/test work is recorded below; provider lifecycle and live-server verification remain outstanding.

## Verified Composition and Handler Increment

- `dotnet test --project tests/Zuemail.Core.Tests/Zuemail.Core.Tests.csproj`: 30 passed, 0 failed, 0 skipped using the pinned SDK and MTP runner.
- `dotnet build Source/Zuemail.slnx -p:GeneratePackageOnBuild=false`: passes for Core/MailKit on both library targets, the console, and Core tests. Tests are included in the solution.
- `Compose()`, `Body()`, detached `Build()`, default/configured-template cloning, and independent `Copy()`/`SaveAsTemplate()` are implemented. Streams and arbitrary non-byte attachment objects remain borrowed; file paths are not opened during copying/building.
- Copying does not access the lazily computed body preview or mutate the source text. Header/attachment dictionary comparers are retained when their source is a `Dictionary`.
- The handler uses a nonmutating default validator, respects custom validation results, forwards cancellation, and resets only after a successful send. `SendAsync()` throws on invalid content; `TrySendAsync()` returns false. Sender exceptions/cancellation retain the message and are not swallowed by the handler.
- The default validator uses BCL address parsing, accepts To/Cc/Bcc recipients and legitimate self-addressed mail, rejects header injection, and does not invent debug-only sender/reply addresses.
- The console now demonstrates reusable composition and independent per-recipient messages instead of sharing live builder state. It was compiled, not executed against a server.
- Next: phase 4 option factories/host parsing and MailKit client ownership, followed by typed attachments. MailKit's direct sender paths still need their provider-specific validation/lifecycle/cancellation work; Core fake-sender tests do not claim those paths are fixed.

## Verified IMAP Increment

- Added `IImapReceiver` with explicit injected/owned client behavior, cancellation-aware connect/authenticate, forced reconnect, typed authentication/capability setup, folder resolution/listing, disconnect, clone, and sync/async disposal.
- Added `ImapOptions` settings for folder/access, explicit SASL/custom auth, removable capabilities/mechanisms, and protocol-client configuration. Copies isolate mutable mechanism lists and credentials; capability delegates use typed adapters and run after authentication.
- Added fluent summary/MIME reader selection with `Top`, `Skip`, `Take`, continuous batches, UID ranges, `SearchQuery`, summary fields, cancellation, and transfer progress. MIME retrieval holds folders open while downloading and disposes partial results on failure.
- Added folder operations for special-folder resolution, create/lookup, read/write access, flags, query deletion, UID-scoped expunge, copy/move, and Sent append. Existing read-only folders are not silently upgraded for mutations.
- Added monitor callbacks for arrivals, flag changes, and departures; existing-message policy; capability-aware IDLE with bounded waits; NOOP fallback; cancellation; retry/backoff; reconnecting both clients/folders and reattaching events.
- Added `ConfigureImap()` and `AddZuemailImapReceiver()` action/configuration registrations. The console uses a dedicated `Imap` options section and the abstractions; no live server was used.
- `dotnet test --project src/Zuemail.Core.Tests/Zuemail.Core.Tests.csproj`: 40 passed, 0 failed, 0 skipped.
- `dotnet test --project src/Zuemail.MailKit.Tests/Zuemail.MailKit.Tests.csproj`: 34 passed, 0 failed, 0 skipped.
- `dotnet build src/Zuemail.slnx -p:GeneratePackageOnBuild=false`: passed for Core and MailKit on `netstandard2.0`/`net10.0`, console and tests.
- These are mocked unit tests; live IMAP-server integration, server-specific special-use behavior, and protocol logger resource ownership remain follow-up verification. SMTP implementation is unchanged.
- Follow-up hardening covers forced reconnect/authentication, post-auth capability setup, null-bound mechanism lists, option-copy isolation, nested/shared special-folder discovery, concurrent monitor signals, startup and dropped-IDLE reconnection of both clients/folders, continuously advancing batches, and keeping folders open through MIME retrieval.
- IMAP tests have moved from Core.Tests into the provider-specific MailKit.Tests project; Core.Tests no longer references MailKit or Moq.
- SMTP and IMAP bind `Email:Smtp`/`Email:Imap`; both have matching configuration/DI entry points, fresh-client factories, typed capability adapters, and explicit ownership. SMTP Core-message and MimeMessage sends share validation/connect/send behavior and preserve cancellation/injected-client ownership.
- Moved all IMAP mocks out of `Zuemail.Core.Tests` into `Zuemail.MailKit.Tests`; Core tests now reference Core only. The provider test project covers SMTP client identity/ownership, IMAP, and nested configuration.
- SMTP and IMAP remain separate protocol adapters but share the options/client-ownership shape. SMTP's Core and MimeKit overloads now converge on one validate/connect/authenticate/send path. MIME validation no longer invents debug addresses or mutates an invalid message.
- The console consumes the user's nested settings shape unchanged: `Email:Smtp` and `Email:Imap`. The edited appsettings file remains a user change in the worktree.