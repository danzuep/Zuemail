using System.Net;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using Zuemail.Core.Models;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;
using Xunit;

namespace Zuemail.MailKit.Tests;

public class MailKitSmtpSenderTests
{
    [Fact]
    public async Task SendAsync_CoreMessage_ConnectsAuthenticatesAndSendsWithSameClient()
    {
        var connected = false;
        var authenticated = false;
        var client = new Mock<ISmtpClient>();
        client.SetupGet(value => value.IsConnected).Returns(() => connected);
        client.SetupGet(value => value.IsAuthenticated).Returns(() => authenticated);
        client.SetupGet(value => value.AuthenticationMechanisms).Returns(new HashSet<string>());
        client.Setup(value => value.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.Auto, It.IsAny<CancellationToken>()))
            .Callback<string, int, SecureSocketOptions, CancellationToken>((_, _, _, _) => connected = true)
            .Returns(Task.CompletedTask);
        client.Setup(value => value.AuthenticateAsync(It.IsAny<ICredentials>(), It.IsAny<CancellationToken>()))
            .Callback<ICredentials, CancellationToken>((_, _) => authenticated = true)
            .Returns(Task.CompletedTask);
        client.Setup(value => value.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
            .ReturnsAsync("250 accepted");

        using var sender = new MailKitSmtpSender(Options.Create(new SmtpOptions
        {
            Host = "smtp.example.com",
            Port = 587,
            Username = "sender",
            Password = "secret"
        }), smtpClient: client.Object);
        var message = new EmailMessage();
        message.From.Add(EmailContact.Create("sender@example.com"));
        message.To.Add(EmailContact.Create("recipient@example.com"));
        message.BodyText = "Hello";

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.True(connected);
        Assert.True(authenticated);
        client.Verify(value => value.ConnectAsync("smtp.example.com", 587, SecureSocketOptions.Auto,
            TestContext.Current.CancellationToken), Times.Once);
        client.Verify(value => value.AuthenticateAsync(It.IsAny<ICredentials>(), TestContext.Current.CancellationToken), Times.Once);
        client.Verify(value => value.SendAsync(It.IsAny<MimeMessage>(), TestContext.Current.CancellationToken,
            It.IsAny<ITransferProgress>()), Times.Once);
        client.Verify(value => value.Dispose(), Times.Never);
    }

    [Fact]
    public void SmtpAuthenticationCapability_UsesTypedAdapter()
    {
        var options = new SmtpOptions
        {
            SmtpAuthenticationCapabilities = _ => Task.CompletedTask
        };

        Assert.NotNull(options.Copy());
        Assert.NotNull(options.SmtpAuthenticationCapabilities);
        Assert.Equal("Email:Smtp", SmtpOptions.SectionName);
        Assert.Equal("Email:Imap", ImapOptions.SectionName);
    }

    [Fact]
    public async Task SmtpAuthenticationCapability_CopyRetainsTypedCallback()
    {
        var invoked = false;
        var options = new SmtpOptions
        {
            Host = "smtp.example.com",
            SmtpAuthenticationCapabilities = _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            }
        };
        var copy = Assert.IsType<SmtpOptions>(options.Copy());
        var client = new Mock<ISmtpClient>();

        await copy.AuthenticationCapabilities(client.Object);

        Assert.True(invoked);
    }
}