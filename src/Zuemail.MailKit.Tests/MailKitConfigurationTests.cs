using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Extensions;
using Xunit;

namespace Zuemail.MailKit.Tests;

public class MailKitConfigurationTests
{
    [Fact]
    public void NestedSmtpAndImapSections_BindAndRegisterSeparately()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "smtp.example.com:587",
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

        Assert.Equal("smtp.example.com:587 smtp-user", sender.ToString());
        Assert.EndsWith("INBOX", receiver.ToString());
        Assert.Contains("imap.example.com", receiver.ToString());
        Assert.Contains(":993", receiver.ToString());
    }

}