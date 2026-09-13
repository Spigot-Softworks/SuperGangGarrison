using System.Text.Json;
using OpenGarrison.ClientShared;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientIdentityDocumentTests
{
    [Fact]
    public void NewPersistentIdentityUsesEightCharacterFriendCode()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "client-identity.json");
            var created = ClientIdentityDocument.LoadOrCreate(path);

            Assert.Matches("^OG2-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$", created.FriendCode);
            Assert.True(ClientIdentityDocument.TryNormalizeFriendCode(created.FriendCode, out var normalized));
            Assert.Equal(created.FriendCode, normalized);

            var reloaded = ClientIdentityDocument.LoadOrCreate(path);
            Assert.Equal(created.ClientId, reloaded.ClientId);
            Assert.Equal(created.ClientSecret, reloaded.ClientSecret);
            Assert.Equal(created.FriendCode, reloaded.FriendCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExistingLongFriendCodeRemainsValidAndUnchanged()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "client-identity.json");
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new ClientIdentityDocument
                {
                    ClientId = "legacy-client",
                    ClientSecret = "legacy-secret",
                    FriendCode = "OG2-ABCD-EFGH-JKLM",
                }));

            var loaded = ClientIdentityDocument.LoadOrCreate(path);

            Assert.Equal("legacy-client", loaded.ClientId);
            Assert.Equal("legacy-secret", loaded.ClientSecret);
            Assert.Equal("OG2-ABCD-EFGH-JKLM", loaded.FriendCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("OG2-ABCD-EFGH", true)]
    [InlineData("OG2-ABCD-EFGH-JKLM", true)]
    [InlineData("OG2-ABCD-EFGH-JKLM-NPQR", true)]
    [InlineData("OG2-ABCD-EFGH-JK", false)]
    [InlineData("OG2-ABCD-0FGH", false)]
    public void FriendCodeNormalizationMatchesApiLengthsAndAlphabet(string value, bool expected)
    {
        Assert.Equal(expected, ClientIdentityDocument.TryNormalizeFriendCode(value, out _));
    }

    [Fact]
    public void ApplyingAccountProfileUpdatesPortableFieldsButPreservesDeviceCredentials()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "client-identity.json");
            var identity = ClientIdentityDocument.LoadOrCreate(path);
            var originalClientId = identity.ClientId;
            var originalClientSecret = identity.ClientSecret;

            Assert.True(identity.ApplyAccountProfile(
                new AccountProfileResponse
                {
                    FriendCode = "OG2-WXYZ-2345",
                    DisplayName = "Recovered Player",
                    PlayerCardJson = "{\"class\":\"Medic\",\"team\":\"Red\"}",
                },
                path));

            var reloaded = ClientIdentityDocument.LoadOrCreate(path);
            Assert.Equal(originalClientId, reloaded.ClientId);
            Assert.Equal(originalClientSecret, reloaded.ClientSecret);
            Assert.Equal("OG2-WXYZ-2345", reloaded.FriendCode);
            Assert.Equal("Recovered Player", reloaded.DisplayName);
            Assert.Equal("Medic", reloaded.PlayerCard.Class);
            Assert.Equal("Red", reloaded.PlayerCard.Team);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengarrison-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
