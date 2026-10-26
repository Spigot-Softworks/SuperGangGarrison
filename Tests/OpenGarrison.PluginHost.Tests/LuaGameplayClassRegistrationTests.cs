using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.PluginHost;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LuaGameplayClassRegistrationTests
{
    private const string KanoLua = """
        local plugin = {}

        function plugin.initialize(host)
            plugin.registered = host.register_gameplay_class({
                classId = "plugin.test.kano",
                displayName = "Kano",
                botGraphPlayerClass = "Soldier",
                killFeedSprite = "KanoKL",
                movement = {
                    maxHealth = 150,
                    runPower = 1.2,
                    maxAirJumps = 1
                },
                presentation = {
                    spritePrefix = "Kano",
                    runSuffix = "RunS",
                    jumpSuffix = "JumpS"
                },
                loadout = {
                    loadoutId = "plugin.test.kano.stock",
                    displayName = "Stock",
                    primaryItemId = "weapon.scattergun"
                }
            })
            host.log("kano registered=" .. tostring(plugin.registered))
        end

        return plugin
        """;

    [Fact]
    public void LuaRegisterGameplayClassCreatesCustomSlotClassWithDefaultLoadout()
    {
        using var fixture = CreateFixture(KanoLua);

        Assert.Contains("kano registered=true", fixture.Logs);
        var registry = fixture.Registry;
        Assert.True(registry.TryGetClassBinding("plugin.test.kano", out var binding));
        Assert.Equal(PlayerClass.Custom, binding.PlayerClass);
        Assert.Equal(PlayerClass.Soldier, binding.BotGraphPlayerClass);
        Assert.Equal("plugin.tests.server.lua-class", binding.ModPackId);
        Assert.Equal("KanoKL", binding.PrimaryWeaponKillFeedSprite);

        var gameplayClass = registry.GetRequiredClass("plugin.test.kano");
        Assert.Equal("Kano", gameplayClass.DisplayName);
        Assert.Equal(150, gameplayClass.Movement.MaxHealth);
        Assert.Equal(1.2f, gameplayClass.Movement.RunPower, 3);
        Assert.Equal(1, gameplayClass.Movement.MaxAirJumps);
        Assert.Equal(-7f, gameplayClass.Movement.CollisionLeft);
        Assert.Equal("Kano", gameplayClass.Presentation?.SpritePrefix);
        Assert.Equal("RunS", gameplayClass.Presentation?.RunSuffix);
        Assert.Equal("plugin.test.kano.stock", gameplayClass.DefaultLoadoutId);

        var definition = registry.CreateCharacterClassDefinition("plugin.test.kano");
        Assert.True(definition.IsCustomSlot);
        Assert.Equal("Scattergun", definition.PrimaryWeapon.DisplayName);
        Assert.Contains(registry.CustomSlotClassBindings, candidate => candidate.ClassId == "plugin.test.kano");
    }

    [Fact]
    public void LuaRegisterGameplayClassRejectsUnknownPrimaryItem()
    {
        using var fixture = CreateFixture(KanoLua.Replace("weapon.scattergun", "weapon.does-not-exist", StringComparison.Ordinal));

        Assert.Contains("kano registered=false", fixture.Logs);
        Assert.Contains(fixture.Logs, log => log.Contains("register_gameplay_class rejected", StringComparison.Ordinal));
        Assert.False(fixture.Registry.TryGetClassBinding("plugin.test.kano", out _));
    }

    private static LuaClassFixture CreateFixture(string lua)
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenGarrison.LuaClassTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "main.lua"), lua);
        var manifest = new OpenGarrisonPluginManifest
        {
            Id = "tests.server.lua-class",
            DisplayName = "Lua Class Test",
            Type = OpenGarrisonPluginType.Server,
            Runtime = OpenGarrisonPluginRuntimeKind.Lua,
            EntryPoint = "main.lua",
            Compatibility = new OpenGarrisonPluginManifestCompatibility("1.0"),
        };
        var configDirectory = Path.Combine(root, "config");
        Directory.CreateDirectory(configDirectory);
        var logs = new List<string>();
        var registry = GameplayRuntimeRegistry.CreateStock();
        var context = DispatchProxy.Create<IOpenGarrisonServerPluginContext, TestServerPluginContext>();
        ((TestServerPluginContext)(object)context).Configure(manifest, root, configDirectory, logs, registry);

        var plugin = new LuaServerPlugin(manifest, root);
        plugin.Initialize(context);
        return new LuaClassFixture(root, plugin, logs, registry);
    }

    private sealed class LuaClassFixture(string root, LuaServerPlugin plugin, List<string> logs, GameplayRuntimeRegistry registry) : IDisposable
    {
        public List<string> Logs { get; } = logs;

        public GameplayRuntimeRegistry Registry { get; } = registry;

        public void Dispose()
        {
            try
            {
                plugin.Shutdown();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    public class TestServerPluginContext : DispatchProxy
    {
        private OpenGarrisonPluginManifest _manifest = null!;
        private string _pluginDirectory = string.Empty;
        private string _configDirectory = string.Empty;
        private List<string> _logs = null!;
        private GameplayRuntimeRegistry _registry = null!;

        public void Configure(
            OpenGarrisonPluginManifest manifest,
            string pluginDirectory,
            string configDirectory,
            List<string> logs,
            GameplayRuntimeRegistry registry)
        {
            _manifest = manifest;
            _pluginDirectory = pluginDirectory;
            _configDirectory = configDirectory;
            _logs = logs;
            _registry = registry;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_" + nameof(IOpenGarrisonPluginHostContext.PluginId) => _manifest.Id,
                "get_" + nameof(IOpenGarrisonPluginHostContext.PluginDirectory) => _pluginDirectory,
                "get_" + nameof(IOpenGarrisonPluginHostContext.ConfigDirectory) => _configDirectory,
                "get_" + nameof(IOpenGarrisonPluginHostContext.Manifest) => _manifest,
                "get_" + nameof(IOpenGarrisonPluginHostContext.HostApi) => OpenGarrisonPluginHostApi.CreateServerDefault(),
                nameof(IOpenGarrisonPluginHostContext.Log) => Log(args),
                nameof(IOpenGarrisonServerPluginContext.TryRegisterGameplayClass) => RegisterClass(args!),
                _ => GetDefault(targetMethod?.ReturnType),
            };
        }

        // Mirrors PluginHost.TryRegisterGameplayClass, which scopes classes to the plugin's mod pack.
        private object RegisterClass(object?[] args)
        {
            var registration = (GameplayClassRegistration)args[0]!;
            var registered = _registry.TryRegisterGameplayClass(
                registration with { ModPackId = $"plugin.{_manifest.Id}" },
                out var errorMessage);
            args[1] = errorMessage;
            return registered;
        }

        private object? Log(object?[]? args)
        {
            _logs.Add(args is { Length: > 0 } ? args[0]?.ToString() ?? string.Empty : string.Empty);
            return null;
        }

        internal static object? GetDefault(Type? type)
        {
            if (type is null || type == typeof(void))
            {
                return null;
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            // Host services the Lua host table reads but this test never exercises.
            return type.IsInterface
                ? DispatchProxy.Create(type, typeof(NullServiceProxy))
                : null;
        }
    }

    public class NullServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return TestServerPluginContext.GetDefault(targetMethod?.ReturnType);
        }
    }
}
