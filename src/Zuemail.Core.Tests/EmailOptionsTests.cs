using Zuemail.Core.Models;
using Xunit;

namespace Zuemail.Core.Tests;

public class EmailOptionsTests
{
    [Fact]
    public void Create_HostOnly_ReturnsIndependentOptions()
    {
        var original = EmailOptions.Default.Copy();

        var first = EmailOptions.Create("smtp.example.com");
        first.Username = "first-user";
        var second = EmailOptions.Create("imap.example.com");

        Assert.Equal("smtp.example.com", first.Host);
        Assert.Equal("imap.example.com", second.Host);
        Assert.NotSame(first, second);
        Assert.Equal(original.Host, EmailOptions.Default.Host);
        Assert.NotEqual("first-user", second.Username);
    }

    [Theory]
    [InlineData("smtp.example.com:587", "smtp.example.com", (ushort)587)]
    [InlineData("smtp.example.com", "smtp.example.com", (ushort)0)]
    [InlineData("[2001:db8::1]:993", "2001:db8::1", (ushort)993)]
    [InlineData("[2001:db8::1]", "2001:db8::1", (ushort)0)]
    [InlineData("2001:db8::1", "2001:db8::1", (ushort)0)]
    public void SetHost_ParsesHostAndOptionalPort(string input, string expectedHost, ushort expectedPort)
    {
        var options = new EmailOptions().SetHost(input);

        Assert.Equal(expectedHost, options.Host);
        Assert.Equal(expectedPort, options.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("host:abc")]
    [InlineData("[2001:db8::1")]
    [InlineData("[2001:db8::1]:abc")]
    public void SetHost_InvalidInput_Throws(string input)
    {
        Assert.ThrowsAny<ArgumentException>(() => new EmailOptions().SetHost(input));
    }
}