using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Microsoft.Extensions.Options;
using Moq;
using Zuemail.MailKit.Abstractions;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;
using Xunit;

namespace Zuemail.Core.Tests;

public class MailFolderReaderTests
{
    [Fact]
    public async Task Top_ReturnsNewestSummariesFirst_AndClosesOnlyOpenedFolder()
    {
        var isOpen = false;
        var summaries = new List<IMessageSummary>
        {
            Summary(7, 7), Summary(8, 8), Summary(9, 9)
        };
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.Count).Returns(10);
        folder.SetupGet(value => value.IsOpen).Returns(() => isOpen);
        folder.Setup(value => value.OpenAsync(FolderAccess.ReadOnly, It.IsAny<CancellationToken>()))
            .Callback<FolderAccess, CancellationToken>((_, _) => isOpen = true)
            .ReturnsAsync(FolderAccess.ReadOnly);
        folder.Setup(value => value.CloseAsync(false, It.IsAny<CancellationToken>()))
            .Callback<bool, CancellationToken>((_, _) => isOpen = false)
            .Returns(Task.CompletedTask);
        folder.Setup(value => value.FetchAsync(7, 9, It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var reader = new MailFolderReader(receiver.Object);

        var result = await reader.Top(3).GetMessageSummariesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { 9, 8, 7 }, result.Select(summary => summary.Index));
        folder.Verify(value => value.OpenAsync(FolderAccess.ReadOnly, TestContext.Current.CancellationToken), Times.Once);
        folder.Verify(value => value.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Range_FetchesUidRangeAndAppliesTakeLimit()
    {
        var range = new UniqueIdRange(new UniqueId(20u), new UniqueId(24u));
        var summaries = Enumerable.Range(20, 5).Select(uid => Summary((uint)uid, uid)).ToList();
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.Setup(value => value.FetchAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var reader = new MailFolderReader(receiver.Object);

        var result = await reader.Range(range.Start, range.End).Take(2).GetMessageSummariesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new uint[] { 20, 21 }, result.Select(summary => summary.UniqueId.Id));
    }

    [Fact]
    public async Task Query_SkipsNewestMatchesAndFetchesAscendingUidSet()
    {
        var matches = new List<UniqueId> { new(1), new(2), new(3), new(4) };
        var expected = new List<UniqueId> { new(2), new(3) };
        var summaries = new List<IMessageSummary> { Summary(2, 1), Summary(3, 2) };
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.Setup(value => value.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(matches);
        folder.Setup(value => value.FetchAsync(It.Is<IList<UniqueId>>(uids => uids.SequenceEqual(expected)),
            It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(summaries);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var reader = new MailFolderReader(receiver.Object);

        var result = await reader.Query(SearchQuery.NotSeen).Skip(1).Take(2).GetMessageSummariesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new uint[] { 2, 3 }, result.Select(summary => summary.UniqueId.Id));
    }

    [Fact]
    public async Task TakeZero_DoesNotConnectToFolder()
    {
        var receiver = new Mock<IImapReceiver>();
        using var reader = new MailFolderReader(receiver.Object);

        var result = await reader.Take(0).GetMessageSummariesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result);
        receiver.Verify(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TakeContinuous_AdvancesToNextBatchBetweenCalls()
    {
        var fetchedIndexes = new List<(int Start, int End)>();
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.Count).Returns(6);
        folder.SetupGet(value => value.IsOpen).Returns(true);
        folder.Setup(value => value.FetchAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, IFetchRequest, CancellationToken>((start, end, _, _) => fetchedIndexes.Add((start, end)))
            .ReturnsAsync((int start, int end, IFetchRequest _, CancellationToken _) =>
                Enumerable.Range(start, end - start + 1).Select(index => Summary((uint)(index + 1), index)).ToList());
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var reader = new MailFolderReader(receiver.Object);

        await reader.Take(2, continuous: true).GetMessageSummariesAsync(TestContext.Current.CancellationToken);
        await reader.GetMessageSummariesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { (0, 1), (2, 3) }, fetchedIndexes);
    }

    [Fact]
    public async Task GetMimeMessages_KeepsFolderOpenUntilMessageDownloadCompletes()
    {
        var open = false;
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(value => value.IsOpen).Returns(() => open);
        folder.Setup(value => value.OpenAsync(FolderAccess.ReadOnly, It.IsAny<CancellationToken>()))
            .Callback<FolderAccess, CancellationToken>((_, _) => open = true)
            .ReturnsAsync(FolderAccess.ReadOnly);
        folder.Setup(value => value.CloseAsync(false, It.IsAny<CancellationToken>()))
            .Callback<bool, CancellationToken>((_, _) => open = false)
            .Returns(Task.CompletedTask);
        var summary = new Mock<IMessageSummary>();
        summary.SetupGet(value => value.UniqueId).Returns(new UniqueId(4u));
        summary.SetupGet(value => value.Folder).Returns(folder.Object);
        folder.SetupGet(value => value.Count).Returns(1);
        folder.Setup(value => value.FetchAsync(0, 0, It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IMessageSummary> { summary.Object });
        var message = new MimeMessage();
        folder.Setup(value => value.GetMessageAsync(new UniqueId(4u), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
            .Callback<UniqueId, CancellationToken, ITransferProgress>((_, _, _) => Assert.True(open))
            .ReturnsAsync(message);
        var receiver = new Mock<IImapReceiver>();
        receiver.Setup(value => value.ConnectMailFolderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        using var reader = new MailFolderReader(receiver.Object);

        var result = await reader.GetMimeMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Same(message, Assert.Single(result));
        Assert.False(open);
    }

    private static IMessageSummary Summary(uint uid, int index)
    {
        var summary = new Mock<IMessageSummary>();
        summary.SetupGet(value => value.UniqueId).Returns(new UniqueId(uid));
        summary.SetupGet(value => value.Index).Returns(index);
        summary.SetupGet(value => value.Flags).Returns(MessageFlags.None);
        return summary.Object;
    }
}