using System.Diagnostics;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

/// <summary>
/// Runs server Lua plugins against an offline practice world, which has no server process.
/// Plugins register their gameplay content, executors and decision hooks as they would on
/// a server; anything that needs real client sessions (messaging, votes, bans, map changes,
/// bots) is unavailable. Content is removed again when the runtime is disposed.
/// </summary>
internal sealed class PracticePluginRuntime : IDisposable
{
    private readonly SimulationWorld _world;
    private readonly PluginHost _pluginHost;
    private readonly ServerScheduler _scheduler;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly TimeSpan _heartbeatInterval;
    private TimeSpan _nextHeartbeatAt;
    private bool _started;
    private bool _disposed;

    public PracticePluginRuntime(
        SimulationWorld world,
        string pluginsDirectory,
        string pluginConfigRoot,
        string mapsDirectory,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(log);
        _world = world;
        IReadOnlyDictionary<byte, ClientSession> noClients = new Dictionary<byte, ClientSession>();
        _scheduler = new ServerScheduler(() => _clock.Elapsed, log);
        var serverState = new ServerReadOnlyStateView(static () => "Practice", () => world, () => noClients);
        // Only the world exists here. Admin operations that need sessions, rotation, snapshots
        // or bots throw, and the plugin host logs the failed hook.
        var adminOperations = new ServerAdminOperations(
            log,
            static (_, _) => { },
            () => noClients,
            static () => null!,
            () => world,
            static () => null,
            static () => null!,
            static () => null!,
            static () => null!);
        _pluginHost = new PluginHost(
            () => world,
            new PluginCommandRegistry(),
            serverState,
            adminOperations,
            new ServerCvarRegistry(),
            _scheduler,
            static slot => OpenGarrisonServerAdminIdentity.CreateUnauthenticated(slot),
            static (_, _, _, _, _, _, _) => { },
            static (_, _, _, _, _, _) => { },
            RejectVoteKindRegistration,
            RejectVoteStart,
            static _ => { },
            pluginsDirectory,
            pluginConfigRoot,
            mapsDirectory,
            log);
        // Same cadence as GameServer's plugin heartbeat.
        _heartbeatInterval = TimeSpan.FromSeconds(Math.Clamp(world.Config.FixedDeltaSeconds, 1d / 60d, 0.1d));
    }

    public IReadOnlyList<string> LoadedPluginIds => _pluginHost.LoadedPluginIds;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _started = true;
        _pluginHost.LoadPlugins();
        _pluginHost.NotifyServerStarting();
        _world.Abilities.GameplayAbilityInputInterceptor = abilityEvent => _pluginHost.TryNotifyGameplayAbilityInput(abilityEvent);
        _world.SpawnDecisionInterceptor = request => ToWorldDecision(_pluginHost.BeforeSpawn(request));
        _world.DamageDecisionInterceptor = request => ToWorldDecision(_pluginHost.BeforeDamage(request));
        _world.DeathDecisionInterceptor = request => ToWorldDecision(_pluginHost.BeforeDeath(request));
        _world.PickupDecisionInterceptor = request => ToWorldDecision(_pluginHost.BeforePickup(request));
        _world.ScoreDecisionInterceptor = request => ToWorldDecision(_pluginHost.BeforeScore(request));
        _world.RoundEndDecisionInterceptor = request => ToWorldDecision(_pluginHost.BeforeRoundEnd(request));
        _pluginHost.NotifyServerStarted();
    }

    /// <summary>Runs scheduled plugin tasks and the plugin heartbeat; call once per simulation tick.</summary>
    public void Tick()
    {
        if (!_started || _disposed)
        {
            return;
        }

        _scheduler.RunDueTasks();
        var now = _clock.Elapsed;
        if (now >= _nextHeartbeatAt)
        {
            _pluginHost.NotifyServerHeartbeat(now);
            _nextHeartbeatAt = now + _heartbeatInterval;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_started)
        {
            return;
        }

        // The practice world outlives this runtime, so its hooks must not keep calling disposed plugins.
        _world.Abilities.GameplayAbilityInputInterceptor = null;
        _world.SpawnDecisionInterceptor = null;
        _world.DamageDecisionInterceptor = null;
        _world.DeathDecisionInterceptor = null;
        _world.PickupDecisionInterceptor = null;
        _world.ScoreDecisionInterceptor = null;
        _world.RoundEndDecisionInterceptor = null;
        _pluginHost.NotifyServerStopping();
        _pluginHost.NotifyServerStopped();
        _pluginHost.ShutdownPlugins();
        // Their classes went with them: anyone still playing one (DIO, say) goes back to a stock
        // class, or the next look-up of it (returning to the menu resets loadouts) throws.
        _world.NetworkPlayers.ResetUnregisteredClassesToStock();
    }

    private static WorldDecisionResult ToWorldDecision(OpenGarrisonServerDecisionResult? decision)
    {
        return decision is { IsCancelled: true } cancelled
            ? WorldDecisionResult.Cancel(cancelled.Reason)
            : WorldDecisionResult.Continue;
    }

    private static bool RejectVoteKindRegistration(string pluginId, OpenGarrisonServerVoteRegistration registration, out string errorMessage)
    {
        errorMessage = "Votes are not available in practice.";
        return false;
    }

    private static bool RejectVoteStart(string pluginId, string voteKindId, byte initiatorSlot, string argument, out string errorMessage)
    {
        errorMessage = "Votes are not available in practice.";
        return false;
    }
}
