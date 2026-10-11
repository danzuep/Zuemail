using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Options;
using Moq;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;
using Xunit;

namespace Zuemail.Core.Tests;

public class MailFolderMonitorTests
{
    [Fact]
    public void SetIdleMinutes_ValidatesProtocolLimit()
    {
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.Clone()).Returns(receiver.Object);
        using var monitor = new MailFolderMonitor(receiver.Object);

        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.SetIdleMinutes(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.SetIdleMinutes(30));
        Assert.Same(monitor, monitor.SetIdleMinutes(9));
    }

    [Fact]
    public async Task IdleAsync_ProcessesExistingMessagesUnlessConfiguredToIgnoreThem()
    {
        var cancellation = new CancellationTokenSource();
        var summary = new Mock<IMessageSummary>();
        summary.SetupGet(value => value.UniqueId).Returns(new UniqueId(7u));
        summary.SetupGet(value => value.Flags).Returns(MessageFlags.None);
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.SetupGet(value => value.Count).Returns(1);
        folder.Setup(value => value.FetchAsync(0, -1, It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IMessageSummary> { summary.Object });
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.Capabilities).Returns(ImapCapabilities.Idle);
        client.Setup(value => value.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Callback<CancellationToken, CancellationToken>((_, _) => cancellation.Cancel())
            .Returns(Task.CompletedTask);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.Clone()).Returns(receiver.Object);
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        receiver.Setup(value => value.ConnectAuthenticatedImapClientAsync(It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(client.Object);
        var arrivals = new List<IMessageSummary>();
        using var monitor = new MailFolderMonitor(receiver.Object);
        monitor.OnMessageArrival(summary => arrivals.Add(summary)).SetIgnoreExistingMailOnConnect(false);

        await monitor.IdleAsync(cancellation.Token);

        Assert.Same(summary.Object, Assert.Single(arrivals));
        client.Verify(value => value.IdleAsync(It.IsAny<CancellationToken>(), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task IdleAsync_CancellationBeforeIdleStillHonorsInitialIgnoreSetting()
    {
        var summary = new Mock<IMessageSummary>();
        summary.SetupGet(value => value.UniqueId).Returns(new UniqueId(3u));
        summary.SetupGet(value => value.Flags).Returns(MessageFlags.None);
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.SetupGet(value => value.Count).Returns(1);
        folder.Setup(value => value.FetchAsync(0, -1, It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IMessageSummary> { summary.Object });
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.Clone()).Returns(receiver.Object);
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        var arrivals = 0;
        using var monitor = new MailFolderMonitor(receiver.Object);
        monitor.OnMessageArrival(_ => arrivals++).SetIgnoreExistingMailOnConnect();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await monitor.IdleAsync(cancellation.Token);

        Assert.Equal(0, arrivals);
    }

    [Theory]
    [InlineData("arrival")]
    [InlineData("flags")]
    [InlineData("departure")]
    public async Task IdleAsync_DispatchesFolderChanges(string change)
    {
        using var cancellation = new CancellationTokenSource();
        var phase = 0;
        var original = Summary(10, MessageFlags.None);
        var updated = Summary(10, change == "flags" ? MessageFlags.Seen : MessageFlags.None);
        var added = Summary(11, MessageFlags.None);
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.SetupGet(value => value.Count).Returns(() => phase == 0 ? 1 : change == "departure" ? 0 : 2);
        folder.Setup(value => value.FetchAsync(0, -1, It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => phase == 0 ? new List<IMessageSummary> { original.Object } :
                change == "departure" ? new List<IMessageSummary>() :
                change == "flags" ? new List<IMessageSummary> { updated.Object } : new List<IMessageSummary> { original.Object, added.Object });
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.Capabilities).Returns(ImapCapabilities.Idle);
        client.Setup(value => value.IdleAsync(It.IsAny<CancellationToken>(), It.IsAny<CancellationToken>()))
            .Callback<CancellationToken, CancellationToken>((_, _) =>
            {
                phase++;
                folder.Raise(value => value.CountChanged += null, EventArgs.Empty);
            })
            .Returns(Task.CompletedTask);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.Clone()).Returns(receiver.Object);
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        receiver.Setup(value => value.ConnectAuthenticatedImapClientAsync(It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(client.Object);
        var received = new List<IMessageSummary>();
        using var monitor = new MailFolderMonitor(receiver.Object);
        monitor.SetIgnoreExistingMailOnConnect();
        switch (change)
        {
            case "arrival": monitor.OnMessageArrival(summary => { received.Add(summary); cancellation.Cancel(); }); break;
            case "flags": monitor.OnMessageFlagsChanged(summary => { received.Add(summary); cancellation.Cancel(); }); break;
            default: monitor.OnMessageDeparture(summary => { received.Add(summary); cancellation.Cancel(); }); break;
        }

        await monitor.IdleAsync(cancellation.Token);

        Assert.Single(received);
        Assert.Equal(change == "arrival" ? 11u : 10u, received[0].UniqueId.Id);
        if (change == "flags")
            Assert.Equal(MessageFlags.Seen, received[0].Flags);
    }

    private static Mock<IMessageSummary> Summary(uint uid, MessageFlags flags)
    {
        var summary = new Mock<IMessageSummary>();
        summary.SetupGet(value => value.UniqueId).Returns(new UniqueId(uid));
        summary.SetupGet(value => value.Flags).Returns(flags);
        return summary;
    }
}