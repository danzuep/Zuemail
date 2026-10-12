using System.Text;
using MailKit;
using Moq;
using Zuemail.Core.Models;
using Zuemail.MailKit.Models;
using Zuemail.MailKit.Services;
using Xunit;

namespace Zuemail.MailKit.Tests;

public class MailKitProtocolLoggerTests
{
    [Fact]
    public void LogConnectAndServerBytes_ApplyPrefixAndTimestamp()
    {
        using var output = new StringWriter();
        using (var logger = new MailKitProtocolLogger(new ProtocolLoggerOptions
        {
            TimestampFormat = "yyyy",
            ServerPrefix = "SERVER: "
        }, output: output))
        {
            logger.LogConnect(new Uri("imap://example.com"));
            var bytes = Encoding.UTF8.GetBytes("* OK ready\r\n");
            logger.LogServer(bytes, 0, bytes.Length);
        }

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith(DateTimeOffset.Now.Year.ToString() + " SERVER: * OK ready", lines[1]);
        Assert.Contains("Connected to imap://example.com", lines[0]);
    }

    [Fact]
    public void LogClient_RedactsDetectorMatchesAndPreservesPartialLinePrefix()
    {
        using var output = new StringWriter();
        using (var logger = new MailKitProtocolLogger(new ProtocolLoggerOptions
        {
            ClientPrefix = "CLIENT: "
        }, output: output))
        {
            var detector = new Mock<IAuthenticationSecretDetector>();
            detector.Setup(value => value.DetectSecrets(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((byte[] bytes, int offset, int count) =>
                {
                    var content = Encoding.UTF8.GetString(bytes, offset, count);
                    var secretIndex = content.IndexOf("secret", StringComparison.Ordinal);
                    return secretIndex < 0 ? new List<AuthenticationSecret>() :
                        new List<AuthenticationSecret> { new(offset + secretIndex, "secret".Length) };
                });
            logger.AuthenticationSecretDetector = detector.Object;
            var first = Encoding.UTF8.GetBytes("AUTH ");
            var second = Encoding.UTF8.GetBytes("secret\r\n");
            logger.LogClient(first, 0, first.Length);
            logger.LogClient(second, 0, second.Length);
        }

        var outputText = output.ToString();
        Assert.Contains("CLIENT: AUTH ", outputText);
        Assert.Contains("********", outputText);
        Assert.DoesNotContain("secret", outputText);
        Assert.Equal(1, outputText.Split("CLIENT: ", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task DisposeAsync_DrainsQueuedRecordsBeforeReturning()
    {
        var output = new StringWriter();
        await using var logger = new MailKitProtocolLogger(new ProtocolLoggerOptions(), output: output);
        var bytes = Encoding.UTF8.GetBytes("one\r\ntwo\r\n");

        logger.LogServer(bytes, 0, bytes.Length);
        await logger.DisposeAsync();

        Assert.Contains("one", output.ToString());
        Assert.Contains("two", output.ToString());
        Assert.True(logger.IsDisposed);
    }

    [Fact]
    public void MailOptions_CreateIndependentLoggersFromCopiedOptions()
    {
        var path = Path.Combine(Path.GetTempPath(), "zuemail-protocol-" + Guid.NewGuid().ToString("N") + ".log");
        var copyPath = Path.Combine(Path.GetTempPath(), "zuemail-protocol-copy-" + Guid.NewGuid().ToString("N") + ".log");
        var options = new SmtpOptions { Host = "smtp.example.com" };
        options.ProtocolLog.FilePath = path;
        var copy = Assert.IsType<SmtpOptions>(options.Copy());
        copy.ProtocolLog.FilePath = copyPath;

        var first = options.ProtocolLogger().Value;
        var second = copy.ProtocolLogger().Value;

        Assert.IsType<MailKitProtocolLogger>(first);
        Assert.IsType<MailKitProtocolLogger>(second);
        Assert.NotSame(first, second);
        options.DisposeProtocolLogger();
        copy.DisposeProtocolLogger();
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(copyPath))
            File.Delete(copyPath);
    }

    [Fact]
    public void MailOptions_WithoutPath_DoesNotCreateLogger()
    {
        var options = new ImapOptions { Host = "imap.example.com" };

        Assert.Null(options.ProtocolLogger().Value);
    }

    [Fact]
    public void MailOptions_ConcurrentLoggerAccess_CreatesOneOwnedLogger()
    {
        var path = Path.Combine(Path.GetTempPath(), "zuemail-protocol-race-" + Guid.NewGuid().ToString("N") + ".log");
        var options = new SmtpOptions { Host = "smtp.example.com" };
        options.ProtocolLog.FilePath = path;
        var loggers = new System.Collections.Concurrent.ConcurrentBag<IProtocolLogger>();

        Parallel.For(0, 32, _ => loggers.Add(options.ProtocolLogger().Value));

        var logger = Assert.IsType<MailKitProtocolLogger>(Assert.Single(loggers.Distinct()));
        logger.Dispose();
        options.DisposeProtocolLogger();
        if (File.Exists(path))
            File.Delete(path);
    }
}