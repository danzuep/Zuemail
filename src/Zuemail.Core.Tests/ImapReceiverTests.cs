using System.Net;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;
using Zuemail.MailKit.Extensions;
using Zuemail.MailKit.Abstractions;
using Xunit;

namespace Zuemail.Core.Tests;

public class ImapReceiverTests
{
    [Fact]
    public void Constructor_RequiresHostAndCopiesOptions()
    {
        var invalid = new ImapOptions();
        Assert.Throws<ArgumentException>(() => new ImapReceiver(Options.Create(invalid)));

        var options = new ImapOptions { Host = "imap.example.com", Username = "user", Password = "secret" };
        using var receiver = new ImapReceiver(Options.Create(options));
        options.Username = "changed";
        options.AuthenticationMechanismsToRemove.Add("XOAUTH2");
        var copiedOptions = Assert.IsType<ImapOptions>(new ImapOptions
        {
            Host = "imap.example.com",
            Username = "user",
            Password = "secret",
            AuthenticationMechanismsToRemove = new List<string> { "PLAIN" }
        }.Copy());
        var secondCopy = Assert.IsType<ImapOptions>(copiedOptions.Copy());
        copiedOptions.AuthenticationMechanismsToRemove.Add("XOAUTH2");

        Assert.Equal("user", copiedOptions.Username);
        Assert.Single(secondCopy.AuthenticationMechanismsToRemove);
        Assert.NotSame(receiver.ImapClient, receiver.Clone().ImapClient);
    }

    [Fact]
    public async Task ConnectAuthenticated_ReusesConnectionAndForwardsCancellation()
    {
        var connected = false;
        var authenticated = false;
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.IsConnected).Returns(() => connected);
        client.SetupGet(value => value.IsAuthenticated).Returns(() => authenticated);
        client.SetupGet(value => value.AuthenticationMechanisms).Returns(new HashSet<string>());
        client.Setup(value => value.ConnectAsync("imap.example.com", 993, SecureSocketOptions.Auto, It.IsAny<CancellationToken>()))
            .Callback<string, int, SecureSocketOptions, CancellationToken>((_, _, _, _) => connected = true)
            .Returns(Task.CompletedTask);
        client.Setup(value => value.AuthenticateAsync(It.IsAny<ICredentials>(), It.IsAny<CancellationToken>()))
            .Callback<ICredentials, CancellationToken>((_, _) => authenticated = true)
            .Returns(Task.CompletedTask);
        var options = new ImapOptions { Host = "imap.example.com", Port = 993, Username = "user", Password = "pass" };
        using var receiver = new ImapReceiver(Options.Create(options), imapClient: client.Object);
        using var cancellation = new CancellationTokenSource();

        await receiver.ConnectAuthenticatedImapClientAsync(cancellation.Token);
        await receiver.ConnectAuthenticatedImapClientAsync(cancellation.Token);

        client.Verify(value => value.ConnectAsync("imap.example.com", 993, SecureSocketOptions.Auto, cancellation.Token), Times.Once);
        client.Verify(value => value.AuthenticateAsync(It.IsAny<ICredentials>(), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task ConnectAuthenticated_UsesCustomAuthenticationAndRemovesConfiguredMechanisms()
    {
        var authenticated = false;
        var mechanisms = new HashSet<string> { "XOAUTH2", "PLAIN" };
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.IsConnected).Returns(true);
        client.SetupGet(value => value.IsAuthenticated).Returns(() => authenticated);
        client.SetupGet(value => value.AuthenticationMechanisms).Returns(mechanisms);
        var options = new ImapOptions
        {
            Host = "imap.example.com",
            AuthenticationMechanismsToRemove = new List<string> { "XOAUTH2" },
            CustomAuthentication = (imap, _) =>
            {
                Assert.Same(client.Object, imap);
                authenticated = true;
                return Task.CompletedTask;
            }
        };
        using var receiver = new ImapReceiver(Options.Create(options), imapClient: client.Object);

        await receiver.ConnectAuthenticatedImapClientAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("XOAUTH2", mechanisms);
        Assert.Contains("PLAIN", mechanisms);
    }

    [Fact]
    public async Task ConnectAuthenticated_NullMechanismListIsTreatedAsEmpty()
    {
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.IsConnected).Returns(true);
        client.SetupGet(value => value.IsAuthenticated).Returns(true);
        client.SetupGet(value => value.AuthenticationMechanisms).Returns(new HashSet<string>());
        using var receiver = new ImapReceiver(Options.Create(new ImapOptions
        {
            Host = "imap.example.com",
            AuthenticationMechanismsToRemove = null!
        }), imapClient: client.Object);

        Assert.Same(client.Object, await receiver.ConnectAuthenticatedImapClientAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConnectAuthenticated_ForceReconnectsAndReauthenticatesOnce()
    {
        var connected = false;
        var authenticated = false;
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.IsConnected).Returns(() => connected);
        client.SetupGet(value => value.IsAuthenticated).Returns(() => authenticated);
        client.SetupGet(value => value.AuthenticationMechanisms).Returns(new HashSet<string>());
        client.Setup(value => value.ConnectAsync("imap.example.com", 993, SecureSocketOptions.Auto, It.IsAny<CancellationToken>()))
            .Callback<string, int, SecureSocketOptions, CancellationToken>((_, _, _, _) => connected = true)
            .Returns(Task.CompletedTask);
        client.Setup(value => value.DisconnectAsync(true, It.IsAny<CancellationToken>()))
            .Callback<bool, CancellationToken>((_, _) => { connected = false; authenticated = false; })
            .Returns(Task.CompletedTask);
        client.Setup(value => value.AuthenticateAsync(It.IsAny<ICredentials>(), It.IsAny<CancellationToken>()))
            .Callback<ICredentials, CancellationToken>((_, _) => authenticated = true)
            .Returns(Task.CompletedTask);
        using var receiver = new ImapReceiver(Options.Create(new ImapOptions
        {
            Host = "imap.example.com", Port = 993, Username = "user", Password = "pass"
        }), imapClient: client.Object);

        await receiver.ConnectAuthenticatedImapClientAsync(TestContext.Current.CancellationToken);
        await receiver.ConnectAuthenticatedImapClientAsync(TestContext.Current.CancellationToken, force: true);

        client.Verify(value => value.ConnectAsync("imap.example.com", 993, SecureSocketOptions.Auto,
            TestContext.Current.CancellationToken), Times.Exactly(2));
        client.Verify(value => value.AuthenticateAsync(It.IsAny<ICredentials>(), TestContext.Current.CancellationToken), Times.Exactly(2));
    }

    [Fact]
    public async Task ImapAuthenticationCapabilities_UsesTypedAdapterAfterAuthentication()
    {
        var authenticated = false;
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.IsConnected).Returns(true);
        client.SetupGet(value => value.IsAuthenticated).Returns(() => authenticated);
        client.SetupGet(value => value.AuthenticationMechanisms).Returns(new HashSet<string>());
        client.Setup(value => value.AuthenticateAsync(It.IsAny<ICredentials>(), It.IsAny<CancellationToken>()))
            .Callback<ICredentials, CancellationToken>((_, _) => authenticated = true)
            .Returns(Task.CompletedTask);
        var capabilitiesCalled = false;
        var options = new ImapOptions
        {
            Host = "imap.example.com",
            ImapAuthenticationCapabilities = imapClient =>
            {
                Assert.Same(client.Object, imapClient);
                Assert.True(authenticated);
                capabilitiesCalled = true;
                return Task.CompletedTask;
            }
        };
        using var receiver = new ImapReceiver(Options.Create(options), imapClient: client.Object);

        await receiver.ConnectAuthenticatedImapClientAsync(TestContext.Current.CancellationToken);

        Assert.True(capabilitiesCalled);
        var copy = Assert.IsType<ImapOptions>(options.Copy());
        Assert.NotNull(copy.ImapAuthenticationCapabilities);
    }

    [Fact]
    public async Task ConnectMailFolder_DefaultsToInbox_AndHonorsConfiguredAccess()
    {
        var isOpen = false;
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.FullName).Returns("INBOX");
        folder.SetupGet(value => value.IsOpen).Returns(() => isOpen);
        folder.Setup(value => value.OpenAsync(FolderAccess.ReadOnly, It.IsAny<CancellationToken>()))
            .Callback<FolderAccess, CancellationToken>((_, _) => isOpen = true)
            .ReturnsAsync(FolderAccess.ReadOnly);
        var client = ConnectedClient(folder.Object);
        var options = new ImapOptions { Host = "imap.example.com", MailFolderAccess = FolderAccess.ReadOnly };
        using var receiver = new ImapReceiver(Options.Create(options), imapClient: client.Object);

        var result = await receiver.ConnectMailFolderAsync(TestContext.Current.CancellationToken);

        Assert.Same(folder.Object, result);
        folder.Verify(value => value.OpenAsync(FolderAccess.ReadOnly, TestContext.Current.CancellationToken), Times.Once);
        client.VerifyGet(value => value.Inbox, Times.Once);
    }

    [Fact]
    public async Task GetMailFolderNames_IncludesInboxAndDeduplicatesNamespaces()
    {
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.FullName).Returns("INBOX");
        var client = ConnectedClient(folder.Object);
        var receiver = new ImapReceiver(Options.Create(new ImapOptions { Host = "imap.example.com" }), imapClient: client.Object);

        var result = await receiver.GetMailFolderNamesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "INBOX" }, result);
    }

    [Fact]
    public async Task Dispose_DoesNotDisposeInjectedClient()
    {
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.IsConnected).Returns(false);
        var receiver = new ImapReceiver(Options.Create(new ImapOptions { Host = "imap.example.com" }), imapClient: client.Object);

        await receiver.DisposeAsync();

        client.Verify(value => value.Dispose(), Times.Never);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.GetMailFolderNamesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Create_ParsesHostnameAndBracketedIpv6()
    {
        using var hostname = ImapReceiver.Create("imap.example.com:993");
        using var ipv6 = ImapReceiver.Create("[2001:db8::1]:993");
        Assert.Contains("imap.example.com:993", hostname.ToString());
        Assert.Contains("2001:db8::1:993", ipv6.ToString());
        Assert.Throws<ArgumentException>(() => ImapReceiver.Create("[2001:db8::1"));
    }

    [Fact]
    public void AddZuemailImapReceiver_ConfiguresOptionsAndCreatesIndependentReceivers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddZuemailImapReceiver(options =>
        {
            options.Host = "imap.example.com";
            options.Username = "user";
        });
        using var provider = services.BuildServiceProvider();
        using var first = provider.GetRequiredService<IImapReceiver>();
        using var second = provider.GetRequiredService<IImapReceiver>();

        Assert.NotSame(first, second);
        Assert.NotSame(first.ImapClient, second.ImapClient);
        Assert.Contains("imap.example.com", first.ToString());
    }

    private static Mock<IImapClient> ConnectedClient(IMailFolder inbox)
    {
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.IsConnected).Returns(true);
        client.SetupGet(value => value.IsAuthenticated).Returns(true);
        client.SetupGet(value => value.Capabilities).Returns(ImapCapabilities.None);
        client.SetupGet(value => value.Inbox).Returns(inbox);
        client.SetupGet(value => value.PersonalNamespaces).Returns(new FolderNamespaceCollection());
        client.SetupGet(value => value.SharedNamespaces).Returns(new FolderNamespaceCollection());
        client.SetupGet(value => value.OtherNamespaces).Returns(new FolderNamespaceCollection());
        return client;
    }
}