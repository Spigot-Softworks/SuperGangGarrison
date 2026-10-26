using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class GameplayContentSyncTests
{
    private const string PluginOwner = "server-plugins:test/kano";
    private const string BehaviorId = "plugin.test.kano.knife";
    private const string KnifeItemId = "plugin.test.kano.weapon.knife";
    private const string KanoClassId = "plugin.test.kano";
    private const string ScoutLoadoutId = "scout.plugin-test-knife";

    [Fact]
    public void RemovingOwnedRegistrationsRestoresStockState()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var stockScoutLoadouts = registry.GetClassDefinition("scout").Loadouts.Keys.ToArray();
        var stockModPackCount = registry.ModPacks.Count;

        using (registry.BeginRegistrationScope(PluginOwner))
        {
            RegisterKanoContent(registry);
            Assert.True(registry.TryRegisterGameplayAbilityExecutor(
                "plugin.test.kano.executor",
                new DelegateGameplayAbilityExecutor(static _ => GameplayAbilityResult.Ignored),
                out var error), error);
        }

        // Registrations outside a scope are permanent.
        Assert.True(registry.TryRegisterGameplayClass(CreateKanoClass("plugin.unowned.class"), out var unownedError), unownedError);

        Assert.True(registry.HasOwnedRegistrations(PluginOwner));
        Assert.True(registry.RemoveOwnedRegistrations(PluginOwner));

        Assert.False(registry.TryGetClassBinding(KanoClassId, out _));
        Assert.False(registry.TryGetItem(KnifeItemId, out _));
        Assert.False(registry.TryGetGameplayAbilityExecutor("plugin.test.kano.executor", out _));
        Assert.Throws<InvalidOperationException>(() => registry.GetRequiredPrimaryWeaponBinding(BehaviorId));
        Assert.Equal(stockScoutLoadouts, registry.GetClassDefinition("scout").Loadouts.Keys.ToArray());
        Assert.DoesNotContain(registry.CustomSlotClassBindings, binding => binding.ClassId == KanoClassId);
        Assert.True(registry.TryGetClassBinding("plugin.unowned.class", out _));
        Assert.Equal(stockModPackCount + 1, registry.ModPacks.Count);
        Assert.False(registry.RemoveOwnedRegistrations(PluginOwner));

        // Removal makes the same ids registrable again (plugin reload, practice restart).
        using (registry.BeginRegistrationScope(PluginOwner))
        {
            RegisterKanoContent(registry);
        }

        Assert.True(registry.TryGetClassBinding(KanoClassId, out _));
    }

    [Fact]
    public void ReplicatedContentRoundTripsToClientRegistryAndClearsOnRemoval()
    {
        var server = GameplayRuntimeRegistry.CreateStock();
        using (server.BeginRegistrationScope(PluginOwner))
        {
            RegisterKanoContent(server);
        }

        var content = server.CreateReplicatedContent();
        Assert.Single(content.Classes);
        Assert.Single(content.Items);
        Assert.Single(content.Loadouts);
        Assert.Single(content.PrimaryWeaponBehaviors);
        Assert.DoesNotContain(content.Items, item => item.Item.Id == "weapon.scattergun");

        var payload = content.SerializeToUtf8Json();
        Assert.True(GameplayReplicatedContent.TryDeserialize(payload, out var received, out var error), error);

        var client = GameplayRuntimeRegistry.CreateStock();
        var stockScoutLoadouts = client.GetClassDefinition("scout").Loadouts.Keys.ToArray();
        client.SealAbilityDefinitions();
        client.ApplyReplicatedContent(received, GameplayRuntimeRegistry.ReplicatedContentOwnerKey);

        Assert.True(client.TryGetClassBinding(KanoClassId, out var binding));
        Assert.Equal(PlayerClass.Custom, binding.PlayerClass);
        Assert.Contains(client.CustomSlotClassBindings, candidate => candidate.ClassId == KanoClassId);
        var kano = client.CreateCharacterClassDefinition(KanoClassId);
        Assert.Equal("Kano", kano.DisplayName);
        Assert.Equal(150, kano.MaxHealth);
        Assert.Equal("Kano Knife", kano.PrimaryWeapon.DisplayName);
        Assert.Equal(PrimaryWeaponKind.Custom, client.GetRequiredPrimaryWeaponBinding(BehaviorId).WeaponKind);
        Assert.Contains(ScoutLoadoutId, client.GetClassDefinition("scout").Loadouts.Keys);

        Assert.True(client.RemoveOwnedRegistrations(GameplayRuntimeRegistry.ReplicatedContentOwnerKey));
        Assert.False(client.TryGetClassBinding(KanoClassId, out _));
        Assert.False(client.TryGetItem(KnifeItemId, out _));
        Assert.Equal(stockScoutLoadouts, client.GetClassDefinition("scout").Loadouts.Keys.ToArray());
    }

    [Fact]
    public void ApplyingContentAlreadyPresentRegistersNothing()
    {
        // A client sharing a process with the server already has the server's content.
        var registry = GameplayRuntimeRegistry.CreateStock();
        using (registry.BeginRegistrationScope(PluginOwner))
        {
            RegisterKanoContent(registry);
        }

        var content = registry.CreateReplicatedContent();
        Assert.True(GameplayReplicatedContent.TryDeserialize(content.SerializeToUtf8Json(), out var received, out var error), error);
        registry.ApplyReplicatedContent(received, GameplayRuntimeRegistry.ReplicatedContentOwnerKey);

        Assert.False(registry.HasOwnedRegistrations(GameplayRuntimeRegistry.ReplicatedContentOwnerKey));
        Assert.True(GameplayRuntimeRegistry.CreateStock().CreateReplicatedContent().IsEmpty);
    }

    [Fact]
    public void ContentSyncMessageRoundTripsThroughCodec()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        using (registry.BeginRegistrationScope(PluginOwner))
        {
            RegisterKanoContent(registry);
        }

        var payload = registry.CreateReplicatedContent().SerializeToUtf8Json();
        var bytes = ProtocolCodec.Serialize(new GameplayContentSyncMessage(payload));

        Assert.True(ProtocolCodec.TryDeserialize(bytes, out var message));
        var decoded = Assert.IsType<GameplayContentSyncMessage>(message);
        Assert.Equal(payload, decoded.ContentUtf8Json);
        Assert.False(GameplayReplicatedContent.TryDeserialize("{\"schemaVersion\":99,\"content\":null}"u8, out _, out _));
    }

    [Fact]
    public void ServerSendsGameplayContentRightAfterWelcome()
    {
        var world = new SimulationWorld();
        var clients = new Dictionary<byte, ClientSession>();
        var sent = new List<string>();
        var sessionManager = new ServerSessionManager(
            world,
            clients,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            nowProvider: () => TimeSpan.Zero,
            serverPassword: null,
            passwordRequired: false,
            clientTimeoutSeconds: 20,
            passwordTimeoutSeconds: 20,
            passwordRetrySeconds: 5,
            getPasswordRateLimitReason: static _ => null,
            recordPasswordFailure: static _ => { },
            clearPasswordFailures: static _ => { },
            sendMessage: static (_, _) => { },
            log: static _ => { });
        var dispatcher = new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 999,
            static _ => null,
            static _ => { },
            static () => (false, string.Empty, string.Empty),
            (_, message) => sent.Add(message.GetType().Name),
            static _ => { },
            static _ => { },
            static (_, _, _) => { },
            static (_, _) => { },
            static _ => { },
            sendGameplayContent: _ => sent.Add("GameplayContent"));

        dispatcher.Dispatch(
            new HelloMessage("Kano Fan", ProtocolVersion.Current, 0),
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 8190));

        var welcomeIndex = sent.IndexOf(nameof(WelcomeMessage));
        Assert.True(welcomeIndex >= 0, string.Join(", ", sent));
        Assert.Equal("GameplayContent", sent[welcomeIndex + 1]);
    }

    [Fact]
    public void PracticePluginRuntimeLoadsServerLuaPluginsAndRemovesThemOnDispose()
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenGarrison.PracticePluginTests", Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(root, "Plugins", "Server", "Test.PracticeKano");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.json"), """
            {
              "schemaVersion": 1,
              "id": "test.practice-kano",
              "displayName": "Practice Kano",
              "version": "1.0.0",
              "type": "Server",
              "runtime": "Lua",
              "entryPoint": "main.lua",
              "compatibility": { "hostApiVersion": "1.0" }
            }
            """);
        File.WriteAllText(Path.Combine(pluginDirectory, "main.lua"), """
            local plugin = {}
            function plugin.initialize(host)
                host.register_gameplay_class({
                    classId = "test.practice.kano",
                    displayName = "Kano",
                    movement = { maxHealth = 150 },
                    loadout = { loadoutId = "test.practice.kano.stock", displayName = "Stock", primaryItemId = "weapon.scattergun" }
                })
            end
            return plugin
            """);

        var logs = new List<string>();
        var registry = CharacterClassCatalog.RuntimeRegistry;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        try
        {
            for (var session = 0; session < 2; session += 1)
            {
                using var runtime = new PracticePluginRuntime(
                    world,
                    Path.Combine(root, "Plugins"),
                    Path.Combine(root, "config"),
                    Path.Combine(root, "Maps"),
                    logs.Add);
                runtime.Start();

                Assert.Contains("test.practice-kano", runtime.LoadedPluginIds);
                Assert.True(registry.TryGetClassBinding("test.practice.kano", out var binding), string.Join(Environment.NewLine, logs));
                Assert.Equal(PlayerClass.Custom, binding.PlayerClass);
                Assert.NotNull(world.DamageDecisionInterceptor);
                runtime.Tick();
            }

            Assert.False(registry.TryGetClassBinding("test.practice.kano", out _));
            Assert.Null(world.DamageDecisionInterceptor);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void RegisterKanoContent(GameplayRuntimeRegistry registry)
    {
        Assert.True(registry.TryRegisterPrimaryWeaponBehavior(
            new GameplayPrimaryWeaponRuntimeBinding(BehaviorId, PrimaryWeaponKind.Custom, "KnifeSnd"),
            out var error), error);
        Assert.True(registry.TryRegisterGameplayWeaponItem(
            new GameplayWeaponItemRegistration(
                KnifeItemId,
                "Kano Knife",
                GameplayEquipmentSlot.Primary,
                BehaviorId,
                new GameplayItemAmmoDefinition(MaxAmmo: 3, AmmoPerUse: 1, ProjectilesPerUse: 1, UseDelaySourceTicks: 10, ReloadSourceTicks: 20),
                ModPackId: "plugin.test"),
            out error), error);
        Assert.True(registry.TryRegisterGameplayClass(CreateKanoClass(KanoClassId) with
        {
            DefaultLoadout = new GameplayLoadoutRegistration(KanoClassId, $"{KanoClassId}.stock", "Stock", KnifeItemId),
        }, out error), error);
        Assert.True(registry.TryRegisterGameplayLoadout(
            new GameplayLoadoutRegistration("scout", ScoutLoadoutId, "Plugin Knife", KnifeItemId),
            out error), error);
    }

    private static GameplayClassRegistration CreateKanoClass(string classId)
    {
        return new GameplayClassRegistration(
            classId,
            "Kano",
            new GameplayClassMovementDefinition(
                MaxHealth: 150,
                CollisionLeft: -7f,
                CollisionTop: -12f,
                CollisionRight: 8f,
                CollisionBottom: 12f,
                RunPower: 1.1f,
                JumpStrength: 8.3f,
                MaxAirJumps: 0,
                TauntLengthFrames: 8),
            new GameplayLoadoutRegistration(classId, $"{classId}.stock", "Stock", "weapon.scattergun"),
            new GameplayClassPresentationDefinition("Scout"),
            ModPackId: "plugin.test");
    }
}
