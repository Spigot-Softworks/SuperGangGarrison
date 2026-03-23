using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;
using OpenGarrison.Server.Plugins;
using OpenGarrison.Server.Plugins.ChatVoting;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class ChatVotingPluginTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "OpenGarrison-chat-voting-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryHandleChatMessage_VotemapPassesImmediatelyForSingleEligiblePlayer()
    {
        EnsureContentRootInitialized();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var state = new TestState(
            [
                new OpenGarrisonServerPlayerInfo(1, "Alice", false, true, PlayerTeam.Red, PlayerClass.Scout, "127.0.0.1:8191"),
            ]);
        var admin = new TestAdminOperations();
        var plugin = new ChatVotingPlugin(() => now);
        var context = new TestPluginContext(_tempDirectory, state, admin);
        plugin.Initialize(context);
        admin.OnTryChangeMap = () =>
        {
            plugin.OnMapChanging(new MapChangingEvent("Truefort", 1, 1, "Egypt", 1, false, null));
            plugin.OnMapChanged(new MapChangedEvent("Egypt", 1, 1, GameModeKind.CaptureTheFlag));
        };
        var chatContext = new OpenGarrisonServerChatMessageContext(state, admin);

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Alice", "!votemap Egypt", PlayerTeam.Red)));

        Assert.Equal(("Egypt", 1, false), admin.LastMapChange);
        Assert.Contains(admin.BroadcastMessages, message => message.Contains("Vote passed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(admin.BroadcastMessages, message => message.Contains("started votemap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryHandleChatMessage_VotemapRequiresMajorityWhenTwoPlayersAreEligible()
    {
        EnsureContentRootInitialized();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var state = new TestState(
            [
                new OpenGarrisonServerPlayerInfo(1, "Alice", false, true, PlayerTeam.Red, PlayerClass.Scout, "127.0.0.1:8191"),
                new OpenGarrisonServerPlayerInfo(2, "Bob", false, true, PlayerTeam.Blue, PlayerClass.Soldier, "127.0.0.1:8192"),
            ]);
        var admin = new TestAdminOperations();
        var plugin = new ChatVotingPlugin(() => now);
        var context = new TestPluginContext(_tempDirectory, state, admin);
        plugin.Initialize(context);
        var chatContext = new OpenGarrisonServerChatMessageContext(state, admin);

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Alice", "!votemap Egypt", PlayerTeam.Red)));
        Assert.Contains(admin.BroadcastMessages, message => message.Contains("started votemap", StringComparison.OrdinalIgnoreCase));

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(2, "Bob", "!vote yes", PlayerTeam.Blue)));
        Assert.Equal(("Egypt", 1, false), admin.LastMapChange);
        Assert.Null(admin.LastNextRoundMap);
        Assert.Contains(admin.BroadcastMessages, message => message.Contains("Vote passed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryHandleChatMessage_VoteNextRoundQueuesNextMapAfterEnoughYesVotes()
    {
        EnsureContentRootInitialized();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var state = new TestState(
            [
                new OpenGarrisonServerPlayerInfo(1, "Alice", false, true, PlayerTeam.Red, PlayerClass.Scout, "127.0.0.1:8191"),
                new OpenGarrisonServerPlayerInfo(2, "Bob", false, true, PlayerTeam.Blue, PlayerClass.Soldier, "127.0.0.1:8192"),
                new OpenGarrisonServerPlayerInfo(3, "Cara", false, true, PlayerTeam.Blue, PlayerClass.Medic, "127.0.0.1:8193"),
            ]);
        var admin = new TestAdminOperations();
        var plugin = new ChatVotingPlugin(() => now);
        var context = new TestPluginContext(_tempDirectory, state, admin);
        plugin.Initialize(context);
        var chatContext = new OpenGarrisonServerChatMessageContext(state, admin);

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Alice", "!votenextround Dirtbowl 2", PlayerTeam.Red)));
        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(2, "Bob", "!yes", PlayerTeam.Blue)));

        Assert.Equal(("Dirtbowl", 2), admin.LastNextRoundMap);
        Assert.Null(admin.LastMapChange);
        Assert.Contains(admin.BroadcastMessages, message => message.Contains("next round", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OnServerHeartbeat_ExpiresVoteWhenDurationElapses()
    {
        EnsureContentRootInitialized();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var state = new TestState(
            [
                new OpenGarrisonServerPlayerInfo(1, "Alice", false, true, PlayerTeam.Red, PlayerClass.Scout, "127.0.0.1:8191"),
                new OpenGarrisonServerPlayerInfo(2, "Bob", false, true, PlayerTeam.Blue, PlayerClass.Soldier, "127.0.0.1:8192"),
            ]);
        var admin = new TestAdminOperations();
        var plugin = new ChatVotingPlugin(() => now);
        var context = new TestPluginContext(_tempDirectory, state, admin);
        plugin.Initialize(context);
        var chatContext = new OpenGarrisonServerChatMessageContext(state, admin);

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Alice", "!votemap Egypt", PlayerTeam.Red)));

        now = now.AddSeconds(31);
        plugin.OnServerHeartbeat(TimeSpan.FromSeconds(31));

        Assert.Null(admin.LastMapChange);
        Assert.Null(admin.LastNextRoundMap);
        Assert.Contains(admin.BroadcastMessages, message => message.Contains("Vote expired", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryHandleChatMessage_SpectatorCannotStartVoteByDefault()
    {
        EnsureContentRootInitialized();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var state = new TestState(
            [
                new OpenGarrisonServerPlayerInfo(1, "Spec", true, true, null, null, "127.0.0.1:8191"),
                new OpenGarrisonServerPlayerInfo(2, "Bob", false, true, PlayerTeam.Blue, PlayerClass.Soldier, "127.0.0.1:8192"),
            ]);
        var admin = new TestAdminOperations();
        var plugin = new ChatVotingPlugin(() => now);
        var context = new TestPluginContext(_tempDirectory, state, admin);
        plugin.Initialize(context);
        var chatContext = new OpenGarrisonServerChatMessageContext(state, admin);

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Spec", "!votemap Egypt", null)));
        Assert.Contains(admin.DirectMessages, message => message.Text.Contains("non-spectators", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(admin.BroadcastMessages);
    }

    [Fact]
    public void TryHandleChatMessage_CancelVoteStartsCooldownAndBlocksImmediateRevote()
    {
        EnsureContentRootInitialized();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var state = new TestState(
            [
                new OpenGarrisonServerPlayerInfo(1, "Alice", false, true, PlayerTeam.Red, PlayerClass.Scout, "127.0.0.1:8191"),
                new OpenGarrisonServerPlayerInfo(2, "Bob", false, true, PlayerTeam.Blue, PlayerClass.Soldier, "127.0.0.1:8192"),
            ]);
        var admin = new TestAdminOperations();
        var plugin = new ChatVotingPlugin(() => now);
        var context = new TestPluginContext(_tempDirectory, state, admin);
        plugin.Initialize(context);
        var chatContext = new OpenGarrisonServerChatMessageContext(state, admin);

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Alice", "!votemap Egypt", PlayerTeam.Red)));
        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Alice", "!cancelvote", PlayerTeam.Red)));
        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(2, "Bob", "!votemap Egypt", PlayerTeam.Blue)));

        Assert.Contains(admin.BroadcastMessages, message => message.Contains("canceled the vote", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(admin.DirectMessages, message => message.Text.Contains("cooldown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OnClientDisconnected_AllowsSingleRemainingEligiblePlayerToCarryVote()
    {
        EnsureContentRootInitialized();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var state = new TestState(
            [
                new OpenGarrisonServerPlayerInfo(1, "Alice", false, true, PlayerTeam.Red, PlayerClass.Scout, "127.0.0.1:8191"),
                new OpenGarrisonServerPlayerInfo(2, "Bob", false, true, PlayerTeam.Blue, PlayerClass.Soldier, "127.0.0.1:8192"),
            ]);
        var admin = new TestAdminOperations();
        var plugin = new ChatVotingPlugin(() => now);
        var context = new TestPluginContext(_tempDirectory, state, admin);
        plugin.Initialize(context);
        var chatContext = new OpenGarrisonServerChatMessageContext(state, admin);

        Assert.True(plugin.TryHandleChatMessage(chatContext, new ChatReceivedEvent(1, "Alice", "!votemap Egypt", PlayerTeam.Red)));

        state.SetPlayers(
            [
                new OpenGarrisonServerPlayerInfo(1, "Alice", false, true, PlayerTeam.Red, PlayerClass.Scout, "127.0.0.1:8191"),
            ]);
        plugin.OnClientDisconnected(new ClientDisconnectedEvent(2, "Bob", "127.0.0.1:8192", "quit", true));

        Assert.Equal(("Egypt", 1, false), admin.LastMapChange);
        Assert.Contains(admin.BroadcastMessages, message => message.Contains("Vote passed", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static void EnsureContentRootInitialized()
    {
        var contentDirectory = ProjectSourceLocator.FindDirectory("OpenGarrison.Core/Content");
        Assert.False(string.IsNullOrWhiteSpace(contentDirectory));
        ContentRoot.Initialize(contentDirectory!);
    }

    private sealed class TestPluginContext : IOpenGarrisonServerPluginContext
    {
        public TestPluginContext(string rootDirectory, IOpenGarrisonServerReadOnlyState serverState, IOpenGarrisonServerAdminOperations adminOperations)
        {
            PluginDirectory = Path.Combine(rootDirectory, "plugins");
            ConfigDirectory = Path.Combine(rootDirectory, "config");
            MapsDirectory = Path.Combine(rootDirectory, "maps");
            Directory.CreateDirectory(PluginDirectory);
            Directory.CreateDirectory(ConfigDirectory);
            Directory.CreateDirectory(MapsDirectory);
            ServerState = serverState;
            AdminOperations = adminOperations;
        }

        public string PluginId => "chat.voting.test";

        public string PluginDirectory { get; }

        public string ConfigDirectory { get; }

        public string MapsDirectory { get; }

        public IOpenGarrisonServerReadOnlyState ServerState { get; }

        public IOpenGarrisonServerAdminOperations AdminOperations { get; }

        public void RegisterCommand(IOpenGarrisonServerCommand command)
        {
        }

        public void Log(string message)
        {
        }
    }

    private sealed class TestState(IReadOnlyList<OpenGarrisonServerPlayerInfo> players) : IOpenGarrisonServerReadOnlyState
    {
        private IReadOnlyList<OpenGarrisonServerPlayerInfo> _players = players;

        public string ServerName => "Test Server";

        public string LevelName => "Truefort";

        public int MapAreaIndex => 1;

        public int MapAreaCount => 1;

        public GameModeKind GameMode => GameModeKind.CaptureTheFlag;

        public MatchPhase MatchPhase => MatchPhase.Running;

        public int RedCaps => 0;

        public int BlueCaps => 0;

        public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers() => _players;

        public void SetPlayers(IReadOnlyList<OpenGarrisonServerPlayerInfo> players)
        {
            _players = players;
        }
    }

    private sealed class TestAdminOperations : IOpenGarrisonServerAdminOperations
    {
        public List<string> BroadcastMessages { get; } = [];

        public List<(byte Slot, string Text)> DirectMessages { get; } = [];

        public (string LevelName, int AreaIndex, bool PreservePlayerStats)? LastMapChange { get; private set; }

        public (string LevelName, int AreaIndex)? LastNextRoundMap { get; private set; }

        public Action? OnTryChangeMap { get; set; }

        public void BroadcastSystemMessage(string text)
        {
            BroadcastMessages.Add(text);
        }

        public void SendSystemMessage(byte slot, string text)
        {
            DirectMessages.Add((slot, text));
        }

        public bool TryDisconnect(byte slot, string reason) => true;

        public bool TryMoveToSpectator(byte slot) => true;

        public bool TrySetTeam(byte slot, PlayerTeam team) => true;

        public bool TrySetClass(byte slot, PlayerClass playerClass) => true;

        public bool TryForceKill(byte slot) => true;

        public bool TrySetCapLimit(int capLimit) => true;

        public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false)
        {
            LastMapChange = (levelName, mapAreaIndex, preservePlayerStats);
            OnTryChangeMap?.Invoke();
            return true;
        }

        public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1)
        {
            LastNextRoundMap = (levelName, mapAreaIndex);
            return true;
        }
    }
}
