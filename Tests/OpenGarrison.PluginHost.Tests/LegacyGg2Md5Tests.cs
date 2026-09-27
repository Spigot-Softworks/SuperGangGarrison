using System.Security.Cryptography;
using System.Text;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2Md5Tests
{
    [Theory]
    [InlineData("", "d41d8cd98f00b204e9800998ecf8427e")]
    [InlineData("a", "0cc175b9c0f1b6a831c399e269772661")]
    [InlineData("abc", "900150983cd24fb0d6963f7d28e17f72")]
    [InlineData("message digest", "f96b697d7cb7938d525a2f31aaf161d0")]
    [InlineData("abcdefghijklmnopqrstuvwxyz", "c3fcd3d76192e4007dfb496cca67e13b")]
    public void MatchesRfcVectors(string input, string expected)
        => Assert.Equal(expected, LegacyGg2Md5.ComputeHex(Encoding.ASCII.GetBytes(input)));

    [Fact]
    public void MatchesGg2PngAcrossBlockBoundary()
    {
        var path = ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "StockMaps", "Gg2", "koth_corinth.png"));
        Assert.False(string.IsNullOrEmpty(path));
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 64);
        Assert.Equal(Convert.ToHexStringLower(MD5.HashData(bytes)), LegacyGg2Md5.ComputeHex(bytes));
    }
}
