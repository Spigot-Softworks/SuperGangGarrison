#nullable enable

using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerStatsServiceTests
{
    [Fact]
    public void AccountAttachIsCorrelatedAndANewerRequestSuspendsOldAwards()
    {
        var world = CreateWorld();
        var client = CreateClient(1);
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var sent = new List<IProtocolMessage>();
        var api = new FakeStatsApiClient
        {
            Validate = token => Task.FromResult(new GameplaySessionValidationResponse
            {
                Valid = token == "valid-token",
                ClientId = client.ClientInstanceId.ToString("D"),
                ExpiresAtIso = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                Profile = Profile("account-one", 500, 500, 4),
            }),
        };
        using var service = CreateService(world, clients, api, sent: sent);

        service.HandleAttach(client, new GameplayAccountAttachRequestMessage(1, "valid-token"));
        service.Tick();

        Assert.True(client.HasAttachedGameplayAccount);
        Assert.Equal("account-one", client.AccountId);
        Assert.Equal("OG2-ABCD-EFGH", client.VerifiedFriendCode);
        var attached = Assert.Single(sent.OfType<GameplayAccountAttachResultMessage>());
        Assert.Equal(1UL, attached.RequestId);
        Assert.True(attached.Attached);

        api.Validate = _ => Task.FromResult(new GameplaySessionValidationResponse { Valid = false });
        service.HandleAttach(client, new GameplayAccountAttachRequestMessage(2, "rejected-token"));

        Assert.False(client.HasAttachedGameplayAccount);
        service.Tick();
        var rejected = Assert.Single(sent.OfType<GameplayAccountAttachResultMessage>(), result => result.RequestId == 2);
        Assert.False(rejected.Attached);

        service.HandleAttach(client, new GameplayAccountAttachRequestMessage(1, "valid-token"));
        service.Tick();
        Assert.Equal(2, sent.OfType<GameplayAccountAttachResultMessage>().Count());
    }

    [Fact]
    public void AccountSwitchAlwaysReplacesVerifiedFriendCodeEvenWithLowerProfileRevision()
    {
        var world = CreateWorld();
        var client = CreateClient(1);
        client.AccountProfileRevision = 99;
        client.FriendCode = "OG2-ABCD-EFGH";
        client.VerifiedFriendCode = "OG2-ABCD-EFGH";
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var api = new FakeStatsApiClient
        {
            Validate = _ => Task.FromResult(new GameplaySessionValidationResponse
            {
                Valid = true,
                ClientId = client.ClientInstanceId.ToString("D"),
                ExpiresAtIso = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                Profile = Profile("account-two", 0, 0, 1, "OG2-MNPQ-RSTU"),
            }),
        };
        using var service = CreateService(world, clients, api);

        service.HandleAttach(client, new GameplayAccountAttachRequestMessage(1, "account-two-token"));
        service.Tick();

        Assert.Equal("account-two", client.AccountId);
        Assert.Equal("OG2-MNPQ-RSTU", client.VerifiedFriendCode);
        Assert.Equal("OG2-MNPQ-RSTU", client.FriendCode);
    }

    [Fact]
    public void AccountAttachRejectsATokenIssuedToAnotherClientIdentity()
    {
        var world = CreateWorld();
        var client = CreateClient(1);
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var sent = new List<IProtocolMessage>();
        var api = new FakeStatsApiClient
        {
            Validate = _ => Task.FromResult(new GameplaySessionValidationResponse
            {
                Valid = true,
                ClientId = Guid.NewGuid().ToString("N"),
                ExpiresAtIso = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                Profile = Profile("another-device-account", 0, 0, 1, "OG2-MNPQ-RSTU"),
            }),
        };
        using var service = CreateService(world, clients, api, sent: sent);

        service.HandleAttach(client, new GameplayAccountAttachRequestMessage(1, "stolen-token"));
        service.Tick();

        Assert.False(client.HasAttachedGameplayAccount);
        Assert.Empty(client.VerifiedFriendCode);
        var result = Assert.Single(sent.OfType<GameplayAccountAttachResultMessage>());
        Assert.False(result.Attached);
    }

    [Fact]
    public void AuthoritativeScoreAndDamageProduceBoundedStatAwards()
    {
        var world = CreateWorld();
        var attacker = JoinPlayer(world, 1, PlayerTeam.Red);
        var victim = JoinPlayer(world, 2, PlayerTeam.Blue);
        var clients = new Dictionary<byte, ClientSession>
        {
            [1] = CreateAttachedClient(1, "attacker"),
            [2] = CreateAttachedClient(2, "victim"),
        };
        var api = new FakeStatsApiClient();
        using var service = CreateService(world, clients, api);
        service.Tick();

        attacker.AddPoints(1.5f);
        attacker.AddKill();
        victim.AddDeath();
        world.AdvanceOneTick();
        service.Tick();

        Assert.Contains(api.Awards, award =>
            award.EventType == "score"
            && award.RawValue == 150
            && award.PointsDelta == 150
            && award.CreditsDelta == 150);
        Assert.Contains(api.Awards, award => award.EventType == "kills" && award.RawValue == 1);
        Assert.Contains(api.Awards, award => award.EventType == "deaths" && award.RawValue == 1);

        var damage = new OpenGarrisonServerDamageEvent(
            (long)world.Frame,
            Amount: 100,
            TargetKind: DamageTargetKind.Player,
            TargetEntityId: victim.Id,
            WasFatal: false,
            AttackerPlayerId: attacker.Id,
            AttackerName: "Attacker",
            AttackerTeam: PlayerTeam.Red,
            AssistedByPlayerId: 0,
            AssistedByName: string.Empty,
            AssistedByTeam: null,
            VictimPlayerId: victim.Id,
            VictimName: "Victim",
            VictimTeam: PlayerTeam.Blue,
            WorldX: victim.X,
            WorldY: victim.Y,
            Flags: DamageEventFlags.None);
        service.HandleDamage(damage);
        service.HandleDamage(damage);
        Assert.DoesNotContain(api.Awards, award => award.EventType is "damage_dealt" or "damage_received");
        service.HandleDamage(damage);

        Assert.Contains(api.Awards, award => award.EventType == "damage_dealt" && award.RawValue == 300);
        Assert.Contains(api.Awards, award => award.EventType == "damage_received" && award.RawValue == 300);
        Assert.All(
            api.Awards.Where(award => award.EventType.StartsWith("damage_", StringComparison.Ordinal)),
            award =>
            {
                Assert.Equal(0, award.PointsDelta);
                Assert.Equal(0, award.CreditsDelta);
            });
    }

    [Fact]
    public void DisabledStatsAdvanceTheBaselineWithoutBackAwarding()
    {
        var world = CreateWorld();
        var player = JoinPlayer(world, 1, PlayerTeam.Red);
        var client = CreateAttachedClient(1, "account-one");
        var clients = new Dictionary<byte, ClientSession> { [1] = client };
        var api = new FakeStatsApiClient();
        var enabled = false;
        using var service = CreateService(world, clients, api, enabledGetter: () => enabled);
        service.Tick();

        player.AddPoints(2f);
        world.AdvanceOneTick();
        service.Tick();
        Assert.Empty(api.Awards);

        enabled = true;
        player.AddPoints(0.5f);
        world.AdvanceOneTick();
        service.Tick();

        var score = Assert.Single(api.Awards, award => award.EventType == "score");
        Assert.Equal(50, score.PointsDelta);
    }

    [Fact]
    public void LastToDieRunRecordsUseAuthoritativeScoresAndOneSubmissionPerAccount()
    {
        var world = CreateWorld();
        var first = CreateAttachedClient(1, "shared-account");
        var second = CreateAttachedClient(2, "shared-account");
        var clients = new Dictionary<byte, ClientSession>
        {
            [1] = first,
            [2] = second,
        };
        var api = new FakeStatsApiClient();
        using var service = CreateService(world, clients, api);
        var runId = Guid.NewGuid();

        service.HandleLastToDieRunEnded(
            runId,
            roundNumber: 12,
            new Dictionary<byte, int> { [1] = 450, [2] = 700 },
            OpenGarrison.Core.LastToDie.LastToDieDifficulty.Hardcore);

        var request = Assert.Single(api.LastToDieRuns);
        Assert.Equal(runId.ToString("N"), request.RunId);
        Assert.Equal(700, request.ScoreUnits);
        Assert.Equal(12, request.RoundNumber);
        Assert.Equal("hardcore", request.Difficulty);
        Assert.Equal(OpenGarrison.Core.LastToDie.LastToDieRuleset.CurrentVersion, request.PolicyVersion);
        Assert.Equal($"{runId:N}:shared-account", request.SubmissionId);
    }

    [Fact]
    public void MapTransitionFlushesDamageEarnedBeforePlayersReturnToJoinState()
    {
        var world = CreateWorld();
        var attacker = JoinPlayer(world, 1, PlayerTeam.Red);
        var victim = JoinPlayer(world, 2, PlayerTeam.Blue);
        var clients = new Dictionary<byte, ClientSession>
        {
            [1] = CreateAttachedClient(1, "attacker"),
            [2] = CreateAttachedClient(2, "victim"),
        };
        var api = new FakeStatsApiClient();
        using var service = CreateService(world, clients, api);
        service.HandleDamage(new OpenGarrisonServerDamageEvent(
            (long)world.Frame,
            Amount: 100,
            TargetKind: DamageTargetKind.Player,
            TargetEntityId: victim.Id,
            WasFatal: false,
            AttackerPlayerId: attacker.Id,
            AttackerName: "Attacker",
            AttackerTeam: PlayerTeam.Red,
            AssistedByPlayerId: 0,
            AssistedByName: string.Empty,
            AssistedByTeam: null,
            VictimPlayerId: victim.Id,
            VictimName: "Victim",
            VictimTeam: PlayerTeam.Blue,
            WorldX: victim.X,
            WorldY: victim.Y,
            Flags: DamageEventFlags.None));
        Assert.Empty(api.Awards);

        world.ResetPlayersToAwaitingJoinForFreshMap();
        service.HandleMapTransition();

        Assert.Contains(api.Awards, award => award.EventType == "damage_dealt" && award.RawValue == 100);
        Assert.Contains(api.Awards, award => award.EventType == "damage_received" && award.RawValue == 100);
    }

    [Fact]
    public async Task LateAwardResponseCannotOverwriteANewerAccountAttachment()
    {
        var world = CreateWorld();
        var player = JoinPlayer(world, 1, PlayerTeam.Red);
        var client = CreateAttachedClient(1, "account-one");
        client.LifetimePoints = 100;
        var clients = new Dictionary<byte, ClientSession> { [1] = client };
        var delayedAward = new TaskCompletionSource<StatAwardResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeStatsApiClient
        {
            Award = _ => delayedAward.Task,
            Validate = _ => Task.FromResult(new GameplaySessionValidationResponse
            {
                Valid = true,
                ClientId = client.ClientInstanceId.ToString("D"),
                ExpiresAtIso = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
                Profile = Profile("account-two", 250, 250, 2),
            }),
        };
        using var service = CreateService(world, clients, api);
        service.Tick();
        player.AddPoints(1f);
        world.AdvanceOneTick();
        service.Tick();
        Assert.Single(api.Awards);

        service.HandleAttach(client, new GameplayAccountAttachRequestMessage(1, "account-two-token"));
        service.Tick();
        Assert.Equal("account-two", client.AccountId);
        Assert.Equal(250, client.LifetimePoints);

        delayedAward.SetResult(new StatAwardResponse
        {
            Applied = true,
            EventId = api.Awards[0].EventId,
            Profile = Profile("account-one", 9999, 9999, 99),
        });
        await Task.Delay(10);
        service.Tick();

        Assert.Equal("account-two", client.AccountId);
        Assert.Equal(250, client.LifetimePoints);
        Assert.Equal(250, client.WalletBalance);
    }

    [Fact]
    public void PointsCommandsUseTheSinglePointsNamespace()
    {
        var world = CreateWorld();
        var client = CreateAttachedClient(1, "account-one");
        var clients = new Dictionary<byte, ClientSession> { [1] = client };
        var systemMessages = new List<string>();
        var api = new FakeStatsApiClient
        {
            OwnPoints = new PlayerPointsResponse
            {
                Profile = Profile("account-one", 1234, 900, 7),
                GlobalRank = 3,
            },
            Leaderboard = new PointsLeaderboardResponse
            {
                Total = 1,
                Limit = 5,
                Entries =
                [
                    new PointsLeaderboardEntry
                    {
                        Rank = 1,
                        DisplayName = "Leader",
                        LifetimePoints = 5000,
                    },
                ],
            },
        };
        using var service = CreateService(world, clients, api, systemMessages: systemMessages);

        Assert.False(service.TryHandleChatCommand(client, "!point me"));
        Assert.True(service.TryHandleChatCommand(client, "!points me"));
        service.Tick();
        Assert.Contains(systemMessages, message => message.Contains("1,234 points", StringComparison.Ordinal));
        Assert.Contains(systemMessages, message => message.Contains("rank #3", StringComparison.Ordinal));

        systemMessages.Clear();
        Assert.True(service.TryHandleChatCommand(client, "!points leaderboard"));
        service.Tick();
        Assert.Contains(systemMessages, message => message.Contains("Points leaderboard", StringComparison.Ordinal));
        Assert.Contains(systemMessages, message => message.Contains("#1 Leader: 5,000 points", StringComparison.Ordinal));

        systemMessages.Clear();
        Assert.True(service.TryHandleChatCommand(client, "!points help"));
        Assert.Contains(systemMessages, message => message.StartsWith("!points", StringComparison.Ordinal));
    }

    private static PlayerStatsService CreateService(
        SimulationWorld world,
        Dictionary<byte, ClientSession> clients,
        IOpenGarrisonStatsApiClient api,
        Func<bool>? enabledGetter = null,
        List<IProtocolMessage>? sent = null,
        List<string>? systemMessages = null)
    {
        sent ??= [];
        systemMessages ??= [];
        return new PlayerStatsService(
            world,
            clients,
            enabledGetter ?? (() => true),
            modeEligible: true,
            (_, message) => sent.Add(message),
            (_, message) => systemMessages.Add(message),
            _ => { },
            api: api);
    }

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(world.TryLoadLevel("ctf_truefort"));
        return world;
    }

    private static PlayerEntity JoinPlayer(SimulationWorld world, byte slot, PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static ClientSession CreateClient(byte slot)
    {
        return new ClientSession(
            slot,
            slot * 100,
            new IPEndPoint(IPAddress.Loopback, 9000 + slot),
            $"Player {slot}",
            TimeSpan.Zero,
            Guid.NewGuid())
        {
            IsAuthorized = true,
        };
    }

    private static ClientSession CreateAttachedClient(byte slot, string accountId)
    {
        var client = CreateClient(slot);
        client.AccountId = accountId;
        client.GameplayToken = $"token-{slot}";
        client.GameplayTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
        client.AccountProfileRevision = 1;
        return client;
    }

    private static StatsAccountProfile Profile(
        string accountId,
        long points,
        long wallet,
        long revision,
        string friendCode = "OG2-ABCD-EFGH")
        => new()
        {
            AccountId = accountId,
            FriendCode = friendCode,
            DisplayName = "Player",
            LifetimePoints = points,
            WalletBalance = wallet,
            ProfileRevision = revision,
        };

    private sealed class FakeStatsApiClient : IOpenGarrisonStatsApiClient
    {
        public Func<string, Task<GameplaySessionValidationResponse>> Validate { get; set; }
            = _ => Task.FromResult(new GameplaySessionValidationResponse { Valid = false });

        public List<StatAwardRequest> Awards { get; } = [];

        public Func<StatAwardRequest, Task<StatAwardResponse>>? Award { get; set; }

        public PlayerPointsResponse OwnPoints { get; set; } = new();

        public PointsLeaderboardResponse Leaderboard { get; set; } = new();

        public List<LastToDieRunRequest> LastToDieRuns { get; } = [];

        public Task<GameplaySessionValidationResponse> ValidateGameplaySessionAsync(
            string token,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Validate(token);
        }

        public Task<StatAwardResponse> AwardAsync(StatAwardRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Awards.Add(request);
            if (Award is not null)
            {
                return Award(request);
            }

            return Task.FromResult(new StatAwardResponse
            {
                Applied = true,
                EventId = request.EventId,
                Profile = Profile("account", request.PointsDelta, request.CreditsDelta, Awards.Count + 1),
            });
        }

        public Task<PlayerPointsResponse> GetPlayerPointsAsync(string token, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OwnPoints);
        }

        public Task<PointsLeaderboardResponse> GetLeaderboardAsync(
            int limit,
            int offset,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Leaderboard.Limit = limit;
            Leaderboard.Offset = offset;
            return Task.FromResult(Leaderboard);
        }

        public Task<LastToDieRunResponse> RecordLastToDieRunAsync(
            LastToDieRunRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastToDieRuns.Add(request);
            return Task.FromResult(new LastToDieRunResponse
            {
                Applied = true,
                SubmissionId = request.SubmissionId,
            });
        }
    }
}
