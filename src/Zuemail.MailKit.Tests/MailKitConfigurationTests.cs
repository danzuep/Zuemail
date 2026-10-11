using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Extensions;
using Zuemail.MailKit.Models;
using Xunit;

namespace Zuemail.MailKit.Tests;

public class MailKitConfigurationTests
{
    [Fact]
    public void NestedSmtpAndImapSections_BindAndRegisterSeparately()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "smtp.example.com",
            ["Email:Smtp:Port"] = "587",
            ["Email:Smtp:Username"] = "smtp-user",
            ["Email:Imap:Host"] = "imap.example.com",
            ["Email:Imap:Port"] = "993",
            ["Email:Imap:Username"] = "imap-user",
            ["Email:Imap:MailFolderName"] = "INBOX"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddZuemailSmtpSender(configuration);
        services.AddZuemailImapReceiver(configuration);
        using var provider = services.BuildServiceProvider();

        var smtp = provider.GetRequiredService<IOptions<SmtpOptions>>().Value;
        var imap = provider.GetRequiredService<IOptions<ImapOptions>>().Value;
        using var receiver = provider.GetRequiredService<IImapReceiver>();

        Assert.Equal("smtp.example.com", smtp.Host);
        Assert.Equal((ushort)587, smtp.Port);
        Assert.Equal("imap.example.com", imap.Host);
        Assert.Equal((ushort)993, imap.Port);
        Assert.Equal("INBOX", imap.MailFolderName);
        Assert.Contains("imap.example.com", receiver.ToString());
    }
}