using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Zuemail.Core.Models;
using Zuemail.Core.Services;
using Zuemail.MailKit.Services;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Extensions;

using var loggerFactory = LoggerFactory.Create(_ => _.SetMinimumLevel(LogLevel.Trace).AddDebug().AddConsole()); //.AddSimpleConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "HH:mm:ss.f "; })
var logger = loggerFactory.CreateLogger<Program>();

var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json").Build();

var emailSenderOptions = configuration.GetRequiredSection(SmtpOptions.SectionName).Get<SmtpOptions>()!;
using var smtpSender = new EmailSender(Options.Create(emailSenderOptions), new MimeMessageValidator(),
    loggerFactory.CreateLogger<EmailSender>());
var writtenEmail = new EmailMessageBuilder()
    .Compose(email => email
        .From("my.name@example.com")
        .Subject("Hello World")
        .BodyText("Optional text/plain content.")
        .BodyHtml("Optional text/html content.<br/>"))
    .Attach(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
    .SaveAsTemplate();
var firstEmail = writtenEmail.Copy().To("YourName@example.com").Build();
bool isSent = await smtpSender.TrySendAsync(firstEmail);
logger.LogInformation("Email 1 {result}.", isSent ? "sent" : "failed to send");

var secondEmail = writtenEmail.Copy().To("new@io").Build();
isSent = await smtpSender.TrySendAsync(secondEmail);
logger.LogInformation("Email 2 {result}.", isSent ? "sent" : "failed to send");

var emailReceiverOptions = configuration.GetRequiredSection(ImapOptions.SectionName).Get<ImapOptions>()!;
using var imapReceiver = new EmailReceiver(Options.Create(emailReceiverOptions), loggerFactory.CreateLogger<EmailReceiver>());
await imapReceiver.ConnectAuthenticatedImapClientAsync();
var mailFolderClient = imapReceiver.MailFolderClient;
var inbox = await mailFolderClient.ConnectAsync();
if (inbox.Count > 5)
{
    var messageSummaries = await imapReceiver.ReadMail.Skip(5).Take(2)
        .GetMessageSummariesAsync(CancellationToken.None);
    logger.LogDebug("Email(s) received: {ids}.", string.Join(", ", messageSummaries.Select(message => message.UniqueId)));
}
if (inbox.Count > 0)
{
    var messages = await imapReceiver.ReadMail.Top(1).GetMimeMessagesAsync(CancellationToken.None);
    foreach (var message in messages)
    {
        using (message)
            logger.LogDebug("Email received: {id}.", message.MessageId);
    }
}

if (imapReceiver.ImapClient.Capabilities.HasFlag(ImapCapabilities.Idle))
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, args) => { args.Cancel = true; cancellation.Cancel(); };
    var monitor = imapReceiver.MonitorFolder
        .SetIgnoreExistingMailOnConnect()
        .SetMessageSummaryItems(MessageSummaryItems.Envelope)
        .OnMessageArrival(summary => logger.LogInformation("New message {uid}: {subject}", summary.UniqueId, summary.Envelope?.Subject));
    try
    {
        await monitor.IdleAsync(cancellation.Token);
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    {
    }
}
await imapReceiver.DisconnectAsync();
