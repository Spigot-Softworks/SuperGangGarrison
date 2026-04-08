using System.Net;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerAdminFoundationTests
{
    [Fact]
    public void PluginCommandRegistryRequiresPermissionForProtectedCommands()
    {
        var registry = new PluginCommandRegistry();
        registry.RegisterBuiltIn(
            "kick",
            "Kick a player.",
            "kick <slot>",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(["ok"]),
            OpenGarrisonServerAdminPermissions.ManagePlayers);

        var unauthorizedContext = CreateCommandContext(OpenGarrisonServerAdminIdentity.CreateUnauthenticated());
        Assert.True(registry.TryExecute("kick 3", unauthorizedContext, CancellationToken.None, out var unauthorizedResponse));
        Assert.Contains("requires", Assert.Single(unauthorizedResponse), StringComparison.Ordinal);

        var authorizedContext = CreateCommandContext(new OpenGarrisonServerAdminIdentity(
            "Admin",
            OpenGarrisonServerAdminAuthority.RconSession,
            OpenGarrisonServerAdminPermissions.FullAccess,
            SourceSlot: 3));
        Assert.True(registry.TryExecute("kick 3", authorizedContext, CancellationToken.None, out var authorizedResponse));
        Assert.Equal("ok", Assert.Single(authorizedResponse));
    }

    [Fact]
    public void ServerCvarRegistryRedactsProtectedValuesAndAppliesTypedUpdates()
    {
        var registry = new ServerCvarRegistry();
        var rconPassword = "secret";
        var autoBalance = false;
        registry.RegisterString(
            "sv_rcon_password",
            "RCON password",
            string.Empty,
            () => rconPassword,
            value =>
            {
                rconPassword = value;
                return null;
            },
            isProtected: true);
        registry.RegisterBoolean(
            "sv_autobalance",
            "Auto-balance",
            defaultValue: false,
            () => autoBalance,
            value => autoBalance = value);

        Assert.True(registry.TryGet("sv_rcon_password", out var protectedCvar));
        Assert.Equal("<protected>", protectedCvar.CurrentValue);

        Assert.True(registry.TrySet("sv_autobalance", "on", out var updatedCvar, out var errorMessage));
        Assert.Equal(string.Empty, errorMessage);
        Assert.True(autoBalance);
        Assert.Equal("true", updatedCvar.CurrentValue);
    }

    [Fact]
    public void AdminChatRouterAuthenticatesAndReplaysReservedCommandPrivately()
    {
        AdminCommandCapturePlugin.Reset();

        var now = TimeSpan.Zero;
        var sessionManager = new ServerAdminSessionManager("secret", () => now);
        var client = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", now);
        var messages = new List<(byte Slot, string Text)>();
        var rootPath = CreateTempRoot();
        var host = new OpenGarrison.Server.PluginHost(
            static () => throw new InvalidOperationException("Unexpected world access."),
            new PluginCommandRegistry(),
            new FakeServerReadOnlyState(),
            new FakeServerAdminOperations(),
            new FakeServerCvarRegistry(),
            new FakeServerScheduler(),
            slot => slot == client.Slot
                ? ServerAdminSessionManager.GetClientIdentity(client)
                : OpenGarrisonServerAdminIdentity.CreateUnauthenticated(slot),
            static (_, _, _, _, _, _, _) => { },
            static (_, _, _, _, _, _) => { },
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "maps"),
            _ => { });
        host.LoadPlugins([typeof(AdminCommandCapturePlugin).Assembly]);

        var router = new ServerAdminChatRouter(
            sessionManager,
            () => host,
            (slot, text) => messages.Add((slot, text)));

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_status", teamOnly: false));
        Assert.Contains(messages, message => message.Slot == 1 && message.Text.Contains("!gt_auth <password>", StringComparison.Ordinal));
        Assert.Empty(AdminCommandCapturePlugin.HandledCommands);

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_auth secret", teamOnly: false));
        Assert.Contains(messages, message => message.Slot == 1 && message.Text.Contains("granted", StringComparison.OrdinalIgnoreCase));

        var handled = Assert.Single(AdminCommandCapturePlugin.HandledCommands);
        Assert.Equal("!gt_status", handled.Text);
        Assert.True(handled.Identity.IsAuthenticated);
        Assert.Equal(OpenGarrisonServerAdminAuthority.RconSession, handled.Identity.Authority);
        Assert.Equal((byte)1, handled.Identity.SourceSlot);
    }

    private static OpenGarrisonServerCommandContext CreateCommandContext(OpenGarrisonServerAdminIdentity identity)
    {
        return new OpenGarrisonServerCommandContext(
            new FakeServerReadOnlyState(),
            new FakeServerAdminOperations(),
            new FakeServerCvarRegistry(),
            new FakeServerScheduler(),
            identity,
            OpenGarrisonServerCommandSource.PrivateChat);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeServerReadOnlyState : IOpenGarrisonServerReadOnlyState
    {
        public string ServerName => "test";
        public string LevelName => "ctf_test";
        public int MapAreaIndex => 1;
        public int MapAreaCount => 1;
        public GameModeKind GameMode => GameModeKind.CaptureTheFlag;
        public MatchPhase MatchPhase => MatchPhase.Running;
        public int RedCaps => 0;
        public int BlueCaps => 0;

        public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId) => [];

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot) => [];

        public bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value)
        {
            value = 0;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value)
        {
            value = false;
            return false;
        }
    }

    private sealed class FakeServerAdminOperations : IOpenGarrisonServerAdminOperations
    {
        public void BroadcastSystemMessage(string text)
        {
        }

        public void SendSystemMessage(byte slot, string text)
        {
        }

        public bool TryDisconnect(byte slot, string reason) => true;

        public bool TryMoveToSpectator(byte slot) => true;

        public bool TrySetTeam(byte slot, PlayerTeam team) => true;

        public bool TrySetClass(byte slot, PlayerClass playerClass) => true;

        public bool TrySetGameplayLoadout(byte slot, string loadoutId) => true;

        public bool TrySetGameplaySecondaryItem(byte slot, string? itemId) => true;

        public bool TrySetGameplayAcquiredItem(byte slot, string? itemId) => true;

        public bool TryGrantGameplayItem(byte slot, string itemId) => true;

        public bool TryRevokeGameplayItem(byte slot, string itemId) => true;

        public bool TrySetGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot) => true;

        public bool TryForceKill(byte slot) => true;

        public bool TrySetCapLimit(int capLimit) => true;

        public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false) => true;

        public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1) => true;
    }

    private sealed class FakeServerCvarRegistry : IOpenGarrisonServerCvarRegistry
    {
        public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll() => [];

        public bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
        {
            cvar = default;
            return false;
        }

        public bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            cvar = default;
            errorMessage = "unsupported";
            return false;
        }
    }

    private sealed class FakeServerScheduler : IOpenGarrisonServerScheduler
    {
        public TimeSpan Uptime => TimeSpan.Zero;

        public Guid ScheduleOnce(TimeSpan delay, Action callback, string? description = null) => Guid.NewGuid();

        public Guid ScheduleRepeating(TimeSpan interval, Action callback, string? description = null, bool runImmediately = false) => Guid.NewGuid();

        public bool Cancel(Guid timerId) => false;

        public bool IsScheduled(Guid timerId) => false;

        public IReadOnlyList<OpenGarrisonServerScheduledTaskInfo> GetScheduledTasks() => [];
    }
}

public sealed class AdminCommandCapturePlugin : IOpenGarrisonServerPlugin, IOpenGarrisonServerChatCommandHooks
{
    public static List<(string Text, OpenGarrisonServerAdminIdentity Identity)> HandledCommands { get; } = [];

    public string Id => "tests.server.admin-capture";

    public string DisplayName => "Admin Command Capture";

    public Version Version => new(1, 0, 0);

    public static void Reset()
    {
        HandledCommands.Clear();
    }

    public void Initialize(IOpenGarrisonServerPluginContext context)
    {
    }

    public bool TryHandleChatMessage(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e)
    {
        if (!e.Text.StartsWith("!gt_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        HandledCommands.Add((e.Text, context.Identity));
        return true;
    }

    public void Shutdown()
    {
    }
}
