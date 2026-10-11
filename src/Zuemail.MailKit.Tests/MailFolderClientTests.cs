using MailKit;
using MailKit.Net.Imap;
using Moq;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Services;
using Xunit;

namespace Zuemail.MailKit.Tests;

public class MailFolderClientTests
{
    [Fact]
    public void SentFolder_FindsNestedFoldersAcrossNamespaces()
    {
        var sent = new Mock<IMailFolder>();
        sent.SetupGet(value => value.Name).Returns("Sent Mail");
        sent.SetupGet(value => value.FullName).Returns("[Gmail]/Sent Mail");
        var gmail = new Mock<IMailFolder>();
        gmail.SetupGet(value => value.Name).Returns("[Gmail]");
        gmail.SetupGet(value => value.FullName).Returns("[Gmail]");
        gmail.Setup(value => value.GetSubfolders(false, It.IsAny<CancellationToken>())).Returns(new List<IMailFolder> { sent.Object });
        var root = new Mock<IMailFolder>();
        root.Setup(value => value.GetSubfolders(false, It.IsAny<CancellationToken>())).Returns(new List<IMailFolder> { gmail.Object });
        var client = new Mock<IImapClient>();
        client.SetupGet(value => value.Capabilities).Returns(ImapCapabilities.None);
        client.SetupGet(value => value.PersonalNamespaces).Returns(new FolderNamespaceCollection { new FolderNamespace('/', "") });
        client.SetupGet(value => value.SharedNamespaces).Returns(new FolderNamespaceCollection());
        client.SetupGet(value => value.OtherNamespaces).Returns(new FolderNamespaceCollection());
        client.Setup(value => value.GetFolder(It.IsAny<FolderNamespace>())).Returns(root.Object);
        var receiver = new Mock<IImapReceiver>();
        receiver.SetupGet(value => value.ImapClient).Returns(client.Object);
        using var folderClient = new MailFolderClient(receiver.Object);

        Assert.Same(sent.Object, folderClient.SentFolder);
    }

    [Fact]
    public async Task AddFlags_OpensWriteAccessAndClosesOnlyTheFolderItOpened()
    {
        var open = false;
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(() => open);
        folder.SetupGet(value => value.Access).Returns(() => open ? FolderAccess.ReadWrite : FolderAccess.None);
        folder.Setup(value => value.OpenAsync(FolderAccess.ReadWrite, It.IsAny<CancellationToken>()))
            .Callback<FolderAccess, CancellationToken>((_, _) => open = true)
            .ReturnsAsync(FolderAccess.ReadWrite);
        folder.Setup(value => value.CloseAsync(false, It.IsAny<CancellationToken>()))
            .Callback<bool, CancellationToken>((_, _) => open = false)
            .Returns(Task.CompletedTask);
        folder.Setup(value => value.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId>());
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var folderClient = new MailFolderClient(receiver.Object);

        var count = await folderClient.AddFlagsAsync(new[] { new UniqueId(1u) }, MessageFlags.Seen,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        folder.Verify(value => value.OpenAsync(FolderAccess.ReadWrite, TestContext.Current.CancellationToken), Times.Once);
        folder.Verify(value => value.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddFlags_RejectsExistingReadOnlyFolderWithoutMutatingIt()
    {
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.SetupGet(value => value.Access).Returns(FolderAccess.ReadOnly);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var folderClient = new MailFolderClient(receiver.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => folderClient.AddFlagsAsync(
            new[] { new UniqueId(1u) }, MessageFlags.Seen, cancellationToken: TestContext.Current.CancellationToken));

        folder.Verify(value => value.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        folder.Verify(value => value.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteMessages_UsesAllRecipientSummariesAndSetsDeletedFlag()
    {
        var summary = new Mock<IMessageSummary>();
        summary.SetupGet(value => value.UniqueId).Returns(new UniqueId(42u));
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.SetupGet(value => value.Access).Returns(FolderAccess.ReadWrite);
        folder.Setup(value => value.StoreAsync(It.Is<IList<UniqueId>>(uids => uids.SequenceEqual(new[] { new UniqueId(42u) })),
                It.IsAny<IStoreFlagsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId>());
        folder.Setup(value => value.ExpungeAsync(It.Is<IList<UniqueId>>(uids => uids.SequenceEqual(new[] { new UniqueId(42u) })),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var folderClient = new MailFolderClient(receiver.Object);

        Assert.Equal(1, await folderClient.DeleteMessagesAsync(new[] { summary.Object }, TestContext.Current.CancellationToken));
        folder.Verify(value => value.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(),
            TestContext.Current.CancellationToken), Times.Once);
        folder.Verify(value => value.ExpungeAsync(It.IsAny<IList<UniqueId>>(), TestContext.Current.CancellationToken), Times.Once);
    }
}