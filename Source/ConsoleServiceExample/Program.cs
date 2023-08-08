using MailKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Zuemail.Core.Models;
using Zuemail.Core.Services;
using Zuemail.Core.Extensions;
using Zuemail.MailKit.Services;
using Zuemail.MailKit.Models;

using var loggerFactory = LoggerFactory.Create(_ => _.SetMinimumLevel(LogLevel.Trace).AddDebug().AddConsole()); //.AddSimpleConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "HH:mm:ss.f "; })
var logger = loggerFactory.CreateLogger<Program>();

var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

var emailSenderOptions = configuration.GetRequiredSection(EmailOptions.SectionName).Get<SmtpOptions>()!;
using var smtpSender = MailKitSmtpSender.Create(emailSenderOptions.Host, logger: loggerFactory.CreateLogger<MailKitSmtpSender>()); //SmtpSender.Create("localhost");
var writtenEmail = smtpSender.WriteEmail
    .From("my.name@example.com")
    .To("YourName@example.com")
    .Subject("Hello World")
    .BodyText("Optional text/plain content.")
    .BodyHtml("Optional text/html content.<br/>")
    .Attach("appsettings.json")
    .SaveAsTemplate();
bool isSent = await smtpSender.TrySendAsync(writtenEmail.Email);
logger.LogInformation("Email 1 {result}.", isSent ? "sent" : "failed to send");

isSent = await writtenEmail.To("new@io").TrySendAsync();
logger.LogInformation("Email 2 {result}.", isSent ? "sent" : "failed to send");

var emailReceiverOptions = Options.Create(configuration.GetRequiredSection(EmailOptions.SectionName).Get<ImapOptions>()!);
//var imapLogger = new MailKitProtocolLogger(loggerFactory.CreateLogger<MailKitProtocolLogger>());
using var imapReceiver = new MailKitImapReceiver(emailReceiverOptions, loggerFactory.CreateLogger<MailKitImapReceiver>()); //ImapReceiver.Create("localhost");

var mailFolderReader = imapReceiver.ReadFrom("INBOX").Skip(5).Take(2);
var messageSummaries = await mailFolderReader.GetMessageSummariesAsync(MessageSummaryItems.UniqueId);
//logger.LogDebug("Email(s) received: {fields}", messageSummaries.FirstOrDefault()?.Fields);
logger.LogDebug("Email(s) received: {ids}.", messageSummaries.Select(m => m.UniqueId).ToEnumeratedString());

var mimeMessages = await imapReceiver.ReadMail.Skip(0).Take(1).GetMimeMessagesAsync();
logger.LogDebug("Email(s) received: {ids}.", mimeMessages.Select(m => m.MessageId).ToEnumeratedString());

await imapReceiver.MonitorFolder
    .SetMessageSummaryItems().SetIgnoreExistingMailOnConnect()
    .OnMessageArrival(OnArrivalAsync)
    .IdleAsync();

Task OnArrivalAsync(IMessageSummary m)
{
    Console.WriteLine(m.UniqueId);
    return Task.CompletedTask;
}
