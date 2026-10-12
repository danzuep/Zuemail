using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Extensions;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;
using Xunit;

namespace Zuemail.MailKit.Tests;

public class MailKitConfigurationTests
{
    [Fact]
    public void NestedSmtpAndImapSections_BindAndRegisterSeparately()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "localhost:25",
            ["Email:Smtp:Username"] = "smtp-user",
            ["Email:Imap:Host"] = "imap.example.com:993",
            ["Email:Imap:Username"] = "imap-user",
            ["Email:Imap:MailFolderName"] = "INBOX"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddZuemailEmailSender(configuration);
        services.AddZuemailEmailReceiver(configuration);
        using var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredService<Zuemail.Core.Abstractions.IEmailSender<Zuemail.Core.Abstractions.IEmailMessage>>();
        using var receiver = provider.GetRequiredService<IEmailReceiver>();

        Assert.Equal("localhost:25 smtp-user", sender.ToString());
        Assert.EndsWith("INBOX", receiver.ToString());
        Assert.Contains("imap.example.com", receiver.ToString());
        Assert.Contains(":993", receiver.ToString());
    }

    [Fact]
    public void RenamedTypes_KeepLegacyAliasesUsable()
    {
#pragma warning disable CS0618
        using var legacySender = MailKitSmtpSender.Create("localhost:25");
        using var legacyReceiver = ImapReceiver.Create("localhost:143");
        using IImapReceiver legacyClone = ((IImapReceiver)legacyReceiver).Clone();
#pragma warning restore CS0618

        Assert.IsAssignableFrom<EmailSender>(legacySender);
        Assert.IsAssignableFrom<EmailReceiver>(legacyReceiver);
        Assert.Contains("localhost:143", legacyClone.ToString());
    }
}