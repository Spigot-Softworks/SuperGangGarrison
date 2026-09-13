#nullable enable

using System.Net;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerManagementConfigurationTests
{
    [Fact]
    public void ClaimedFriendCodeCannotGrantConfiguredIdentity()
    {
        var service = CreateOwnerService("#12ABEF");
        var client = CreateClient();
        client.FriendCode = "OG2-ABCD-EFGH";
        client.AccountId = "attacker-account";
        client.GameplayToken = "valid-looking-token";
        client.GameplayTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

        Assert.False(service.ApplyVerifiedIdentity(client));
        Assert.Equal(OpenGarrisonServerAdminPermissions.None, client.ConfiguredAdminPermissions);
        Assert.False(ServerAdminSessionManager.GetClientIdentity(client).IsAuthenticated);
        Assert.Empty(client.ServerTitleText);
    }

    [Fact]
    public void ApiVerifiedFriendCodeGrantsRolePermissionsAndHexTitleUntilExpiry()
    {
        var service = CreateOwnerService("#12ABEF");
        var client = CreateClient();
        client.FriendCode = "client-claimed-code";
        client.VerifiedFriendCode = "og2 abcdefgh";
        client.AccountId = "verified-account";
        client.GameplayToken = "verified-token";
        client.GameplayTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

        Assert.True(service.ApplyVerifiedIdentity(client));
        Assert.Equal(OpenGarrisonServerAdminPermissions.FullAccess, client.ConfiguredAdminPermissions);
        Assert.Equal("[Owner]", client.ServerTitleText);
        Assert.Equal(0x12ABEFu, client.ServerTitleColorRgb);
        Assert.False(client.ServerTitleRainbow);
        var identity = ServerAdminSessionManager.GetClientIdentity(client);
        Assert.True(identity.IsAuthenticated);
        Assert.Equal(OpenGarrisonServerAdminAuthority.ServerConfiguration, identity.Authority);

        client.GameplayTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        Assert.False(ServerAdminSessionManager.GetClientIdentity(client).IsAuthenticated);
    }

    [Fact]
    public void RainbowTitleAndModeratorDefaultsAreParsed()
    {
        var configuration = new ServerManagementConfiguration
        {
            Players =
            [
                new ServerManagedPlayerConfiguration
                {
                    FriendCode = "OG2-MNPQ-RSTU",
                    Role = "moderator",
                    Title = new ServerManagedPlayerTitleConfiguration { Text = "[Mod]", Color = "RaInBoW" },
                },
            ],
        };
        var service = new ServerManagementService(configuration);

        Assert.True(service.TryGetGrant("mnpq rstu", out var grant));
        Assert.Equal(
            OpenGarrisonServerAdminPermissions.ViewServerState
                | OpenGarrisonServerAdminPermissions.ManagePlayers
                | OpenGarrisonServerAdminPermissions.ManageMatch,
            grant.Permissions);
        Assert.Equal("[Mod]", grant.TitleText);
        Assert.True(grant.TitleRainbow);
    }

    [Fact]
    public void MissingConfigurationCreatesDisabledExampleWithoutGrantingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"opengarrison-management-{Guid.NewGuid():N}");
        var path = Path.Combine(root, ServerManagementConfiguration.DefaultFileName);
        try
        {
            var configuration = ServerManagementConfiguration.LoadOrCreate(path);
            Assert.True(File.Exists(path));
            Assert.False(Assert.Single(configuration.Players).Enabled);
            var service = new ServerManagementService(configuration);
            Assert.False(service.TryGetGrant("OG2-YOUR-CODE", out _));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static ServerManagementService CreateOwnerService(string color)
        => new(new ServerManagementConfiguration
        {
            Players =
            [
                new ServerManagedPlayerConfiguration
                {
                    FriendCode = "OG2-ABCD-EFGH",
                    Role = "Owner",
                    Title = new ServerManagedPlayerTitleConfiguration { Text = "[Owner]", Color = color },
                },
            ],
        });

    private static ClientSession CreateClient()
        => new(
            1,
            1,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Player",
            TimeSpan.Zero,
            Guid.NewGuid());
}
