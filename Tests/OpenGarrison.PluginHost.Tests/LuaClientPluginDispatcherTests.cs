using System.Reflection;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;
using OpenGarrison.Client;
using OpenGarrison.Client.Plugins;
using OpenGarrison.PluginHost;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LuaClientPluginDispatcherTests
{
    [Fact]
    public void DispatcherReusesCallbackAcrossArgumentsResultsAndExplicitYield()
    {
        using var fixture = CreateFixture("""
            local plugin = {}
            local cameraCalls = 0

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_client_frame(event)
                plugin.host.log("frame:" .. event.clientTicks)
            end

            function plugin.get_camera_offset()
                cameraCalls = cameraCalls + 1
                if cameraCalls == 1 then
                    coroutine.yield("pause")
                end
                return { x = cameraCalls, y = -cameraCalls }
            end

            return plugin
            """);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(fixture.Plugin);
        var cameraHooks = Assert.IsAssignableFrom<IOpenGarrisonClientCameraHooks>(fixture.Plugin);

        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 41, false, true, true, false));
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 42, false, true, true, false));

        Assert.Equal(new Vector2(1, -1), cameraHooks.GetCameraOffset());
        Assert.Equal(new Vector2(2, -2), cameraHooks.GetCameraOffset());
        Assert.Equal(1, fixture.Logs.Count(log => log == "frame:41"));
        Assert.Equal(1, fixture.Logs.Count(log => log == "frame:42"));
        Assert.DoesNotContain(fixture.Logs, log => log.Contains("callback failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DispatcherAbandonsTimedOutCallbackAndDoesNotResumeIt()
    {
        using var fixture = CreateFixture("""
            local plugin = {}

            function plugin.initialize(host)
                plugin.host = host
            end

            function plugin.on_client_frame(event)
                plugin.host.log("entered")
                local sum = 0
                for i = 1, 100000000 do
                    sum = sum + i
                end
            end

            return plugin
            """);

        var updateHooks = Assert.IsAssignableFrom<IOpenGarrisonClientUpdateHooks>(fixture.Plugin);
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 1, false, true, true, false));
        updateHooks.OnClientFrame(new ClientFrameEvent(0.016f, 2, false, true, true, false));

        Assert.Equal(1, fixture.Logs.Count(log => log == "entered"));
        Assert.Equal(1, fixture.Logs.Count(log => log.Contains("disabled", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(fixture.Logs, log => log.Contains("budget", StringComparison.OrdinalIgnoreCase));
        Assert.Null(GetPrivateField("_callbackDispatcher", fixture.Plugin));
    }

    [Fact]
    public void DispatcherDisablesPluginAfterCallbackError()
    {
        using var fixture = CreateFixture("""
            local plugin = {}

            function plugin.get_camera_offset()
                error("dispatcher failure")
            end

            return plugin
            """);

        var cameraHooks = Assert.IsAssignableFrom<IOpenGarrisonClientCameraHooks>(fixture.Plugin);
        Assert.Equal(Vector2.Zero, cameraHooks.GetCameraOffset());
        Assert.Equal(Vector2.Zero, cameraHooks.GetCameraOffset());

        Assert.Equal(1, fixture.Logs.Count(log => log.Contains("disabled", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(fixture.Logs, log => log.Contains("dispatcher failure", StringComparison.Ordinal));
    }

    [Fact]
    public void DispatcherFallsBackToFreshCoroutineWhenReentered()
    {
        using var fixture = CreateFixture("""
            local plugin = {}

            function plugin.get_camera_offset()
                return { x = 7, y = -4 }
            end

            return plugin
            """);

        var activeField = typeof(LuaClientPlugin).GetField(
            "_callbackDispatcherActive",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(activeField);
        activeField!.SetValue(fixture.Plugin, true);
        try
        {
            var cameraHooks = Assert.IsAssignableFrom<IOpenGarrisonClientCameraHooks>(fixture.Plugin);
            Assert.Equal(new Vector2(7, -4), cameraHooks.GetCameraOffset());
        }
        finally
        {
            activeField.SetValue(fixture.Plugin, false);
        }
    }

    [Fact]
    public void DispatcherPreservesMultipleCallbackResults()
    {
        using var fixture = CreateFixture("return {} ");
        var script = (Script)GetPrivateField("_script", fixture.Plugin)!;
        var callback = script.DoString("return function() return 11, 22 end");
        var invoke = typeof(LuaClientPlugin).GetMethod(
            "InvokeCallbackWithLimits",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(invoke);

        var result = (DynValue)invoke!.Invoke(fixture.Plugin, [callback, Array.Empty<DynValue>()])!;

        Assert.Equal(DataType.Tuple, result.Type);
        Assert.Equal(2, result.Tuple.Length);
        Assert.Equal(11d, result.Tuple[0].Number);
        Assert.Equal(22d, result.Tuple[1].Number);
    }

    [Fact]
    public void DispatcherHandlesRepeatedCallsWithFrequentAutomaticYields()
    {
        using var fixture = CreateFixture("""
            local plugin = {}
            local calls = 0

            function plugin.get_camera_offset()
                calls = calls + 1
                return { x = calls, y = -calls }
            end

            return plugin
            """);
        var dispatcher = (Coroutine)GetPrivateField("_callbackDispatcher", fixture.Plugin)!;
        dispatcher.AutoYieldCounter = 1;
        var cameraHooks = Assert.IsAssignableFrom<IOpenGarrisonClientCameraHooks>(fixture.Plugin);

        for (var call = 1; call <= 8; call++)
        {
            Assert.Equal(new Vector2(call, -call), cameraHooks.GetCameraOffset());
        }
    }

    [Fact]
    public void DispatcherReferencesAreClearedOnShutdown()
    {
        using var fixture = CreateFixture("return {} ");

        fixture.Plugin.Shutdown();

        Assert.Null(GetPrivateField("_callbackDispatcher", fixture.Plugin));
        Assert.Null(GetPrivateField("_callbackDispatcherRequestTable", fixture.Plugin));
        Assert.Null(GetPrivateField("_callbackDispatcherCompletionMarkerTable", fixture.Plugin));
    }

    private static DispatcherFixture CreateFixture(string lua)
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenGarrison.LuaDispatcherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var logs = new List<string>();
        File.WriteAllText(Path.Combine(root, "main.lua"), lua);
        var manifest = new OpenGarrisonPluginManifest
        {
            Id = "tests.client.lua-dispatcher",
            DisplayName = "Lua Dispatcher Test",
            Type = OpenGarrisonPluginType.Client,
            Runtime = OpenGarrisonPluginRuntimeKind.Lua,
            EntryPoint = "main.lua",
            Compatibility = new OpenGarrisonPluginManifestCompatibility("1.0"),
        };
        var configDirectory = Path.Combine(root, "config");
        Directory.CreateDirectory(configDirectory);
        var context = DispatchProxy.Create<IOpenGarrisonClientPluginContext, TestClientPluginContext>();
        ((TestClientPluginContext)(object)context).Configure(manifest, root, configDirectory, logs);

        var plugin = new LuaClientPlugin(manifest, root);
        plugin.Initialize(context);
        typeof(LuaClientPlugin)
            .GetMethod("InitializeCallbackDispatcher", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, null);
        return new DispatcherFixture(root, plugin, logs);
    }

    private static object? GetPrivateField(string name, LuaClientPlugin plugin)
    {
        return typeof(LuaClientPlugin)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(plugin);
    }

    private sealed class DispatcherFixture(string root, LuaClientPlugin plugin, List<string> logs) : IDisposable
    {
        public LuaClientPlugin Plugin { get; } = plugin;

        public List<string> Logs { get; } = logs;

        public void Dispose()
        {
            try
            {
                Plugin.Shutdown();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    public class TestClientPluginContext : DispatchProxy
    {
        private OpenGarrisonPluginManifest _manifest = null!;
        private string _pluginDirectory = string.Empty;
        private string _configDirectory = string.Empty;
        private List<string> _logs = null!;

        public void Configure(
            OpenGarrisonPluginManifest manifest,
            string pluginDirectory,
            string configDirectory,
            List<string> logs)
        {
            _manifest = manifest;
            _pluginDirectory = pluginDirectory;
            _configDirectory = configDirectory;
            _logs = logs;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_" + nameof(IOpenGarrisonPluginHostContext.PluginId) => _manifest.Id,
                "get_" + nameof(IOpenGarrisonPluginHostContext.PluginDirectory) => _pluginDirectory,
                "get_" + nameof(IOpenGarrisonPluginHostContext.ConfigDirectory) => _configDirectory,
                "get_" + nameof(IOpenGarrisonPluginHostContext.Manifest) => _manifest,
                "get_" + nameof(IOpenGarrisonPluginHostContext.HostApi) => OpenGarrisonPluginHostApi.CreateClientDefault(),
                nameof(IOpenGarrisonPluginHostContext.Log) => Log(args),
                _ => null,
            };
        }

        private object? Log(object?[]? args)
        {
            _logs.Add(args is { Length: > 0 } ? args[0]?.ToString() ?? string.Empty : string.Empty);
            return null;
        }
    }
}
