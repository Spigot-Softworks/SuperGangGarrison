using System.Reflection;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;
using MoonSharp.Interpreter;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;
using OpenGarrison.GameplayModding;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed class LuaServerPlugin(
    OpenGarrisonPluginManifest manifest,
    string pluginDirectory) : IOpenGarrisonServerPlugin,
    IOpenGarrisonServerLifecycleHooks,
    IOpenGarrisonServerUpdateHooks,
    IOpenGarrisonServerClientHooks,
    IOpenGarrisonServerChatHooks,
    IOpenGarrisonServerChatCommandHooks,
    IOpenGarrisonServerMapHooks,
    IOpenGarrisonServerGameplayHooks,
    IOpenGarrisonServerSemanticGameplayHooks
{
    private static readonly BindingFlags PublicInstanceProperties = BindingFlags.Instance | BindingFlags.Public;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private Script? _script;
    private Table? _pluginTable;
    private IOpenGarrisonServerPluginContext? _context;
    private ServerLuaCallbackPhase _currentCallbackPhase = ServerLuaCallbackPhase.None;
    private OpenGarrisonServerAdminIdentity? _activeCommandIdentity;
    private bool _callbacksDisabled;
    private readonly Dictionary<Guid, DynValue> _scheduledCallbacks = [];
    private const long CallbackAutoYieldCounter = 1000;
    private const int MaxCallbackResumeCount = 4096;
    private const int MaxInitializeResumeCount = 65536;

    public string Id => manifest.Id;

    public string DisplayName => manifest.DisplayName;

    public Version Version { get; } = Version.TryParse(manifest.Version, out var version)
        ? version
        : new Version(1, 0, 0, 0);

    public void Initialize(IOpenGarrisonServerPluginContext context)
    {
        try
        {
            _context = context;
            var entryPointPath = Path.GetFullPath(Path.Combine(pluginDirectory, manifest.EntryPoint));
            if (!File.Exists(entryPointPath))
            {
                throw new FileNotFoundException($"Lua entry point was not found: {entryPointPath}", entryPointPath);
            }

            _script = new Script(CoreModules.Preset_SoftSandbox);
            _script.Options.DebugPrint = message => context.Log($"[lua] {message}");

            var hostTable = CreateHostTable(_script, context);
            var result = _script.DoFile(entryPointPath);
            _pluginTable = ResolvePluginTable(_script, result);
            ExecuteInPhase(
                ServerLuaCallbackPhase.Initialize,
                () => CallIfPresent("initialize", rethrowOnFailure: true, DynValue.NewTable(hostTable)));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Lua plugin \"{manifest.Id}\" failed to initialize from \"{GetManifestPath()}\": {ex.Message}", ex);
        }
    }

    public void Shutdown()
    {
        CancelAllScheduledCallbacks();
        ExecuteInPhase(ServerLuaCallbackPhase.Shutdown, () => CallIfPresent("shutdown"));
        _pluginTable = null;
        _script = null;
        _context = null;
        _callbacksDisabled = false;
    }

    public void OnServerStarting() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_starting"));

    public void OnServerStarted() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_started"));

    public void OnServerStopping() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_stopping"));

    public void OnServerStopped() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_stopped"));

    public void OnServerHeartbeat(TimeSpan uptime) => ExecuteInPhase(ServerLuaCallbackPhase.Update, () => CallIfPresent("on_server_heartbeat", DynValue.NewNumber(uptime.TotalSeconds)));

    public void OnHelloReceived(HelloReceivedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_hello_received", ToDynValue(e)));

    public void OnClientConnected(ClientConnectedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_client_connected", ToDynValue(e)));

    public void OnClientDisconnected(ClientDisconnectedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_client_disconnected", ToDynValue(e)));

    public void OnPasswordAccepted(PasswordAcceptedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_password_accepted", ToDynValue(e)));

    public void OnPlayerTeamChanged(PlayerTeamChangedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_team_changed", ToDynValue(e)));

    public void OnPlayerClassChanged(PlayerClassChangedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_class_changed", ToDynValue(e)));

    public void OnChatReceived(ChatReceivedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_chat_received", ToDynValue(e)));

    public bool TryHandleChatMessage(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e)
    {
        if (_script is null || _pluginTable is null)
        {
            return false;
        }

        var previousIdentity = _activeCommandIdentity;
        _activeCommandIdentity = context.Identity;
        try
        {
            return ExecuteInPhase(ServerLuaCallbackPhase.CommandInteraction, () =>
            {
                if (!TryInvokeCallback("try_handle_chat_message", out var result, ToDynValue(context), ToDynValue(e)))
                {
                    return false;
                }

                return result.CastToBool();
            });
        }
        finally
        {
            _activeCommandIdentity = previousIdentity;
        }
    }

    public void OnMapChanging(MapChangingEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_map_changing", ToDynValue(e)));

    public void OnMapChanged(MapChangedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_map_changed", ToDynValue(e)));

    public void OnScoreChanged(ScoreChangedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_score_changed", ToDynValue(e)));

    public void OnRoundEnded(RoundEndedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_round_ended", ToDynValue(e)));

    public void OnKillFeedEntry(KillFeedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_kill_feed_entry", ToDynValue(e)));

    public void OnDamage(OpenGarrisonServerDamageEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_damage", ToDynValue(e)));

    public void OnDeath(OpenGarrisonServerDeathEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_death", ToDynValue(e)));

    public void OnAssist(OpenGarrisonServerAssistEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_assist", ToDynValue(e)));

    public void OnBuild(OpenGarrisonServerBuildableEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_build", ToDynValue(e)));

    public void OnDestroy(OpenGarrisonServerBuildableEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_destroy", ToDynValue(e)));

    public void OnIntelEvent(OpenGarrisonServerIntelEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_intel_event", ToDynValue(e)));

    public void OnControlPointStateChanged(OpenGarrisonServerControlPointStateEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_control_point_state_changed", ToDynValue(e)));

    public void OnPlayerJoined(OpenGarrisonServerPlayerJoinedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_joined", ToDynValue(e)));

    public void OnPlayerLeft(OpenGarrisonServerPlayerLeftEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_left", ToDynValue(e)));

    public void OnPlayerSpawned(OpenGarrisonServerPlayerSpawnEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_spawned", ToDynValue(e)));

    public void OnPlayerRespawned(OpenGarrisonServerPlayerRespawnEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_respawned", ToDynValue(e)));

    private void CallIfPresent(string functionName, params DynValue[] args)
    {
        CallIfPresent(functionName, rethrowOnFailure: false, args);
    }

    private void CallIfPresent(string functionName, bool rethrowOnFailure, params DynValue[] args)
    {
        if (!TryInvokeCallback(functionName, out _, rethrowOnFailure, args))
        {
            return;
        }
    }

    private bool TryInvokeCallback(string callbackName, out DynValue result, params DynValue[] args)
    {
        return TryInvokeCallback(callbackName, out result, rethrowOnFailure: false, args);
    }

    private bool TryInvokeCallback(string callbackName, out DynValue result, bool rethrowOnFailure, params DynValue[] args)
    {
        result = DynValue.Nil;
        if (_script is null || _pluginTable is null || _callbacksDisabled)
        {
            return false;
        }

        var function = _pluginTable.Get(callbackName);
        if (function.Type is not (DataType.Function or DataType.ClrFunction))
        {
            return false;
        }

        try
        {
            result = InvokeCallbackWithLimits(function, args);
            return true;
        }
        catch (Exception ex)
        {
            DisableCallbacks($"{callbackName} failed during {DescribePhase(_currentCallbackPhase)}: {ex.Message}");
            LogCallbackFailure(callbackName, ex);
            if (rethrowOnFailure)
            {
                throw;
            }

            return false;
        }
    }

    private DynValue InvokeCallbackWithLimits(DynValue function, DynValue[] args)
    {
        if (_script is null)
        {
            return DynValue.Nil;
        }

        var coroutine = _script.CreateCoroutine(function).Coroutine;
        coroutine.AutoYieldCounter = CallbackAutoYieldCounter;

        var stopwatch = Stopwatch.StartNew();
        var maxDuration = GetMaxCallbackDuration(_currentCallbackPhase);
        var maxResumeCount = GetMaxCallbackResumeCount(_currentCallbackPhase);
        var resumeCount = 0;
        var firstResume = true;
        while (true)
        {
            var result = firstResume ? coroutine.Resume(args) : coroutine.Resume();
            firstResume = false;
            resumeCount += 1;

            if (coroutine.State == CoroutineState.Dead)
            {
                return result;
            }

            if (resumeCount >= maxResumeCount)
            {
                throw new TimeoutException($"Lua callback exceeded the resume budget of {maxResumeCount} slices.");
            }

            if (stopwatch.Elapsed > maxDuration)
            {
                throw new TimeoutException($"Lua callback exceeded the {maxDuration.TotalMilliseconds:0.##}ms budget.");
            }
        }
    }

    private Table CreateHostTable(Script script, IOpenGarrisonServerPluginContext context)
    {
        var targetResolver = new ServerAdminTargetResolver(context.ServerState.GetPlayers);
        var host = new Table(script)
        {
            ["plugin_id"] = context.PluginId,
            ["plugin_directory"] = context.PluginDirectory,
            ["config_directory"] = context.ConfigDirectory,
            ["maps_directory"] = context.MapsDirectory,
        };

        host["log"] = DynValue.NewCallback((_, args) =>
        {
            context.Log(ReadStringArgument(args, 0));
            return DynValue.Nil;
        });

        host["get_utc_unix_time"] = DynValue.NewCallback((_, _) =>
            DynValue.NewNumber(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        host["load_json_config"] = DynValue.NewCallback((_, args) =>
        {
            var relativePath = ReadStringArgument(args, 0);
            var defaultValue = ReadArgument(args, 1);
            if (!CanAccessPluginStorage("load_json_config", $"config path \"{relativePath}\""))
            {
                return defaultValue;
            }

            var path = ResolveConfigPath(context.ConfigDirectory, relativePath);
            if (!File.Exists(path))
            {
                SaveLuaTableJson(path, defaultValue);
                return defaultValue;
            }

            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            return ToDynValue(value);
        });
        host["save_json_config"] = DynValue.NewCallback((_, args) =>
        {
            var relativePath = ReadStringArgument(args, 0);
            var value = ReadArgument(args, 1);
            if (!CanAccessPluginStorage("save_json_config", $"config path \"{relativePath}\""))
            {
                return DynValue.False;
            }

            var path = ResolveConfigPath(context.ConfigDirectory, relativePath);
            SaveLuaTableJson(path, value);
            return DynValue.True;
        });

        host["get_manifest"] = DynValue.NewCallback((_, _) => ToDynValue(context.Manifest));
        host["get_host_api"] = DynValue.NewCallback((_, _) => ToDynValue(context.HostApi));
        host["get_server_state"] = DynValue.NewCallback((_, _) => ToDynValue(CreateServerStateSnapshot(context.ServerState)));
        host["get_admin_summary"] = DynValue.NewCallback((_, _) => DynValue.NewTable(CreateAdminSummaryTable(script, context)));
        host["get_players"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetPlayers()));
        host["resolve_targets"] = DynValue.NewCallback((_, args) =>
        {
            var options = ReadOptionalTableArgument(args, 1);
            return ToDynValue(targetResolver.Resolve(
                ReadStringArgument(args, 0),
                new ServerAdminTargetQueryOptions(
                    SourceSlot: ReadOptionalByteField(options, "sourceSlot"),
                    AllowMultiple: ReadOptionalBoolField(options, "allowMultiple", true),
                    RequireAlive: ReadOptionalBoolField(options, "requireAlive", false),
                    IncludeSpectators: ReadOptionalBoolField(options, "includeSpectators", true))));
        });
        host["get_gameplay_mod_packs"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetGameplayModPacks()));
        host["get_gameplay_classes"] = DynValue.NewCallback((_, args) =>
        {
            var modPackId = ReadOptionalStringArgument(args, 0);
            return ToDynValue(context.ServerState.GetGameplayClasses(modPackId));
        });
        host["get_gameplay_items"] = DynValue.NewCallback((_, args) =>
        {
            var modPackId = ReadOptionalStringArgument(args, 0);
            return ToDynValue(context.ServerState.GetGameplayItems(modPackId));
        });
        host["get_owned_gameplay_items"] = DynValue.NewCallback((_, args) =>
            ToDynValue(context.ServerState.GetOwnedGameplayItems(ReadByteArgument(args, 0))));
        host["get_gameplay_loadouts_for_class"] = DynValue.NewCallback((_, args) =>
            ToDynValue(context.ServerState.GetGameplayLoadoutsForClass(ReadStringArgument(args, 0))));
        host["get_cvars"] = DynValue.NewCallback((_, _) => ToDynValue(context.Cvars.GetAll(CanAccessProtectedCvars())));
        host["find_cvars"] = DynValue.NewCallback((_, args) =>
            ToDynValue(CreateFilteredCvarResult(
                context.Cvars.GetAll(CanAccessProtectedCvars()),
                ReadOptionalStringArgument(args, 0),
                ReadOptionalIntArgument(args, 1, 16))));
        host["get_cvar"] = DynValue.NewCallback((_, args) =>
        {
            return context.Cvars.TryGet(ReadStringArgument(args, 0), CanAccessProtectedCvars(), out var cvar)
                ? ToDynValue(cvar)
                : DynValue.Nil;
        });
        host["set_cvar"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("set_cvar", "host cvar mutation"))
            {
                return DynValue.False;
            }

            var name = ReadStringArgument(args, 0);
            var value = ReadStringArgument(args, 1);
            if (context.Cvars.TrySet(name, value, CanAccessProtectedCvars(), out var _, out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] set_cvar rejected for {manifest.Id} {name}: {errorMessage}");
            return DynValue.False;
        });
        host["protect_cvar"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("protect_cvar", "host cvar protection"))
            {
                return DynValue.Nil;
            }

            if (!CanAccessProtectedCvars())
            {
                context.Log($"[lua-plugin] protect_cvar rejected for {manifest.Id}: protected cvar access requires rcon-level authority.");
                return DynValue.Nil;
            }

            return context.Cvars.TryProtect(ReadStringArgument(args, 0), out var cvar, out var errorMessage)
                ? ToDynValue(cvar)
                : ToDynValue(new { success = false, errorMessage });
        });
        host["get_scheduled_tasks"] = DynValue.NewCallback((_, _) => ToDynValue(context.Scheduler.GetScheduledTasks()));
        host["schedule_once"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanManageScheduler("schedule_once", "scheduled callback"))
            {
                return DynValue.Nil;
            }

            return ScheduleLuaCallback(
                context,
                ReadArgument(args, 1),
                TimeSpan.FromSeconds(Math.Max(0d, ReadDoubleArgument(args, 0))),
                isRepeating: false,
                ReadOptionalStringArgument(args, 2) ?? string.Empty,
                runImmediately: false);
        });
        host["schedule_repeating"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanManageScheduler("schedule_repeating", "scheduled callback"))
            {
                return DynValue.Nil;
            }

            var intervalSeconds = Math.Max(0d, ReadDoubleArgument(args, 0));
            if (intervalSeconds <= 0d)
            {
                context.Log($"[lua-plugin] schedule_repeating rejected for {manifest.Id}: interval must be greater than zero.");
                return DynValue.Nil;
            }

            return ScheduleLuaCallback(
                context,
                ReadArgument(args, 1),
                TimeSpan.FromSeconds(intervalSeconds),
                isRepeating: true,
                ReadOptionalStringArgument(args, 2) ?? string.Empty,
                ReadOptionalBoolArgument(args, 3, false));
        });
        host["cancel_scheduled_task"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanManageScheduler("cancel_scheduled_task", "scheduled callback"))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(CancelScheduledTask(context, ReadStringArgument(args, 0)));
        });
        host["get_available_gameplay_secondary_items"] = DynValue.NewCallback((_, args) =>
            ToDynValue(context.ServerState.GetAvailableGameplaySecondaryItems(ReadByteArgument(args, 0))));
        host["get_available_gameplay_acquired_items"] = DynValue.NewCallback((_, args) =>
            ToDynValue(context.ServerState.GetAvailableGameplayAcquiredItems(ReadByteArgument(args, 0))));
        host["try_resolve_level"] = DynValue.NewCallback((_, args) =>
        {
            var levelSpec = TryResolveLevel(ReadStringArgument(args, 0));
            return levelSpec is null
                ? DynValue.Nil
                : ToDynValue(levelSpec);
        });
        host["get_available_gameplay_loadouts"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return ToDynValue(context.ServerState.GetAvailableGameplayLoadouts(slot));
        });
        host["broadcast_system_message"] = DynValue.NewCallback((_, args) =>
        {
            var text = ReadStringArgument(args, 0);
            if (!CanIssueServerMutation("broadcast_system_message", "system message broadcast"))
            {
                return DynValue.False;
            }

            context.AdminOperations.BroadcastSystemMessage(text);
            return DynValue.True;
        });
        host["send_system_message"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            if (!CanIssueServerMutation("send_system_message", $"system message to player {slot}"))
            {
                return DynValue.False;
            }

            context.AdminOperations.SendSystemMessage(slot, ReadStringArgument(args, 1));
            return DynValue.True;
        });
        host["try_set_player_name"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_player_name", $"player {slot}")
                && context.AdminOperations.TryRenamePlayer(slot, ReadStringArgument(args, 1)));
        });
        host["try_disconnect"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_disconnect", $"player {slot}")
                && context.AdminOperations.TryDisconnect(slot, ReadStringArgument(args, 1)));
        });
        host["try_ban_player"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            if (!CanIssueServerMutation("try_ban_player", $"player {slot}"))
            {
                return ToDynValue(new OpenGarrisonServerBanActionResult(false, string.Empty, "Admin access required.", false, 0));
            }

            var minutes = ReadIntArgument(args, 1);
            var duration = minutes <= 0 ? (TimeSpan?)null : TimeSpan.FromMinutes(minutes);
            return ToDynValue(context.AdminOperations.TryBanPlayer(slot, duration, ReadOptionalStringArgument(args, 2) ?? string.Empty));
        });
        host["try_ban_ip_address"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_ban_ip_address", "ip address"))
            {
                return ToDynValue(new OpenGarrisonServerBanActionResult(false, string.Empty, "Admin access required.", false, 0));
            }

            var minutes = ReadIntArgument(args, 1);
            var duration = minutes <= 0 ? (TimeSpan?)null : TimeSpan.FromMinutes(minutes);
            return ToDynValue(context.AdminOperations.TryBanIpAddress(ReadStringArgument(args, 0), duration, ReadOptionalStringArgument(args, 2) ?? string.Empty));
        });
        host["try_unban_ip_address"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_unban_ip_address", "ip address"))
            {
                return ToDynValue(new OpenGarrisonServerAddressActionResult(false, string.Empty, "Admin access required."));
            }

            return ToDynValue(context.AdminOperations.TryUnbanIpAddress(ReadStringArgument(args, 0)));
        });
        host["try_set_player_gagged"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_player_gagged", $"player {slot}")
                && context.AdminOperations.TrySetPlayerGagged(slot, ReadBoolArgument(args, 1)));
        });
        host["try_move_to_spectator"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_move_to_spectator", $"player {slot}")
                && context.AdminOperations.TryMoveToSpectator(slot));
        });
        host["try_set_team"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_team", $"player {slot}")
                && TryParseEnumArgument<PlayerTeam>(args, 1, out var team)
                && context.AdminOperations.TrySetTeam(slot, team));
        });
        host["try_set_class"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_class", $"player {slot}")
                && TryParseEnumArgument<PlayerClass>(args, 1, out var playerClass)
                && context.AdminOperations.TrySetClass(slot, playerClass));
        });
        host["try_set_gameplay_loadout"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_gameplay_loadout", $"player {slot}")
                && context.AdminOperations.TrySetGameplayLoadout(slot, ReadStringArgument(args, 1)));
        });
        host["try_set_gameplay_secondary_item"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_gameplay_secondary_item", $"player {slot}")
                && context.AdminOperations.TrySetGameplaySecondaryItem(slot, ReadOptionalStringArgument(args, 1)));
        });
        host["try_set_gameplay_acquired_item"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_gameplay_acquired_item", $"player {slot}")
                && context.AdminOperations.TrySetGameplayAcquiredItem(slot, ReadOptionalStringArgument(args, 1)));
        });
        host["try_grant_gameplay_item"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_grant_gameplay_item", $"player {slot}")
                && context.AdminOperations.TryGrantGameplayItem(slot, ReadStringArgument(args, 1)));
        });
        host["try_revoke_gameplay_item"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_revoke_gameplay_item", $"player {slot}")
                && context.AdminOperations.TryRevokeGameplayItem(slot, ReadStringArgument(args, 1)));
        });
        host["try_set_gameplay_equipped_slot"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_gameplay_equipped_slot", $"player {slot}")
                && TryParseEnumArgument<GameplayEquipmentSlot>(args, 1, out var equippedSlot)
                && context.AdminOperations.TrySetGameplayEquippedSlot(slot, equippedSlot));
        });
        host["try_force_kill"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_force_kill", $"player {slot}")
                && context.AdminOperations.TryForceKill(slot));
        });
        host["try_ignite_player"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_ignite_player", $"player {slot}")
                && context.AdminOperations.TryIgnitePlayer(slot, (float)ReadDoubleArgument(args, 1)));
        });
        host["try_set_player_scale"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_player_scale", $"player {slot}")
                && context.AdminOperations.TrySetPlayerScale(slot, (float)ReadDoubleArgument(args, 1)));
        });
        host["try_set_cap_limit"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_set_cap_limit", "server cap limit"))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.AdminOperations.TrySetCapLimit(ReadIntArgument(args, 0)));
        });
        host["try_set_time_limit"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_set_time_limit", "server time limit"))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.AdminOperations.TrySetTimeLimit(ReadIntArgument(args, 0)));
        });
        host["try_set_respawn_seconds"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_set_respawn_seconds", "server respawn time"))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.AdminOperations.TrySetRespawnSeconds(ReadIntArgument(args, 0)));
        });
        host["try_change_map"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_change_map", "server map rotation"))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.AdminOperations.TryChangeMap(
                ReadStringArgument(args, 0),
                ReadOptionalIntArgument(args, 1, 1),
                ReadOptionalBoolArgument(args, 2, false)));
        });
        host["try_set_next_round_map"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_set_next_round_map", "server next-round map"))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.AdminOperations.TrySetNextRoundMap(
                ReadStringArgument(args, 0),
                ReadOptionalIntArgument(args, 1, 1)));
        });
        host["send_message_to_client"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            var payloadFormat = ReadOptionalEnumArgument(args, 4, PluginMessagePayloadFormat.Text);
            var schemaVersion = ReadOptionalUShortArgument(args, 5, 1);
            if (!CanIssueServerMutation("send_message_to_client", $"plugin message to player {slot}"))
            {
                return DynValue.False;
            }

            if (!TryNormalizePluginMessage(
                    "send_message_to_client",
                    ReadStringArgument(args, 1),
                    ReadStringArgument(args, 2),
                    payloadFormat,
                    schemaVersion,
                    ReadStringArgument(args, 3),
                    out var normalizedTargetPluginId,
                    out var normalizedMessageType,
                    out var normalizedPayload))
            {
                return DynValue.False;
            }

            context.SendMessageToClient(
                slot,
                normalizedTargetPluginId,
                normalizedMessageType,
                normalizedPayload,
                payloadFormat,
                schemaVersion);
            return DynValue.True;
        });
        host["broadcast_message_to_clients"] = DynValue.NewCallback((_, args) =>
        {
            var payloadFormat = ReadOptionalEnumArgument(args, 3, PluginMessagePayloadFormat.Text);
            var schemaVersion = ReadOptionalUShortArgument(args, 4, 1);
            if (!CanIssueServerMutation("broadcast_message_to_clients", "plugin message broadcast"))
            {
                return DynValue.False;
            }

            if (!TryNormalizePluginMessage(
                    "broadcast_message_to_clients",
                    ReadStringArgument(args, 0),
                    ReadStringArgument(args, 1),
                    payloadFormat,
                    schemaVersion,
                    ReadStringArgument(args, 2),
                    out var normalizedTargetPluginId,
                    out var normalizedMessageType,
                    out var normalizedPayload))
            {
                return DynValue.False;
            }

            context.BroadcastMessageToClients(
                normalizedTargetPluginId,
                normalizedMessageType,
                normalizedPayload,
                payloadFormat,
                schemaVersion);
            return DynValue.True;
        });
        host["set_player_replicated_state_int"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            if (!CanIssueServerMutation("set_player_replicated_state_int", $"replicated state for player {slot}"))
            {
                return DynValue.False;
            }

            if (!TryNormalizeReplicatedStateKey("set_player_replicated_state_int", ReadStringArgument(args, 1), out var normalizedStateKey))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.SetPlayerReplicatedStateInt(slot, normalizedStateKey, ReadIntArgument(args, 2)));
        });
        host["set_player_replicated_state_float"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            if (!CanIssueServerMutation("set_player_replicated_state_float", $"replicated state for player {slot}"))
            {
                return DynValue.False;
            }

            if (!TryNormalizeReplicatedStateKey("set_player_replicated_state_float", ReadStringArgument(args, 1), out var normalizedStateKey))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.SetPlayerReplicatedStateFloat(slot, normalizedStateKey, (float)ReadDoubleArgument(args, 2)));
        });
        host["set_player_replicated_state_bool"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            if (!CanIssueServerMutation("set_player_replicated_state_bool", $"replicated state for player {slot}"))
            {
                return DynValue.False;
            }

            if (!TryNormalizeReplicatedStateKey("set_player_replicated_state_bool", ReadStringArgument(args, 1), out var normalizedStateKey))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.SetPlayerReplicatedStateBool(slot, normalizedStateKey, ReadBoolArgument(args, 2)));
        });
        host["clear_player_replicated_state"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            if (!CanIssueServerMutation("clear_player_replicated_state", $"replicated state for player {slot}"))
            {
                return DynValue.False;
            }

            if (!TryNormalizeReplicatedStateKey("clear_player_replicated_state", ReadStringArgument(args, 1), out var normalizedStateKey))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.ClearPlayerReplicatedState(slot, normalizedStateKey));
        });

        return host;
    }

    private DynValue ScheduleLuaCallback(
        IOpenGarrisonServerPluginContext context,
        DynValue callback,
        TimeSpan interval,
        bool isRepeating,
        string description,
        bool runImmediately)
    {
        if (callback.Type is not (DataType.Function or DataType.ClrFunction))
        {
            context.Log($"[lua-plugin] scheduler rejected for {manifest.Id}: callback must be a Lua function.");
            return DynValue.Nil;
        }

        Guid timerId = Guid.Empty;
        Action invokeCallback = () =>
        {
            if (_callbacksDisabled || _script is null)
            {
                if (timerId != Guid.Empty)
                {
                    context.Scheduler.Cancel(timerId);
                    _scheduledCallbacks.Remove(timerId);
                }

                return;
            }

            ExecuteInPhase(ServerLuaCallbackPhase.Update, () =>
            {
                try
                {
                    InvokeCallbackWithLimits(callback, []);
                }
                catch (Exception ex)
                {
                    DisableCallbacks($"scheduled callback {timerId} failed during {DescribePhase(_currentCallbackPhase)}: {ex.Message}");
                    LogCallbackFailure("scheduled_callback", ex);
                }
                finally
                {
                    if (!isRepeating && timerId != Guid.Empty)
                    {
                        _scheduledCallbacks.Remove(timerId);
                    }
                }
            });
        };

        timerId = isRepeating
            ? context.Scheduler.ScheduleRepeating(interval, invokeCallback, description, runImmediately)
            : context.Scheduler.ScheduleOnce(interval, invokeCallback, description);
        _scheduledCallbacks[timerId] = callback;
        return DynValue.NewString(timerId.ToString("D"));
    }

    private bool CancelScheduledTask(IOpenGarrisonServerPluginContext context, string timerIdText)
    {
        if (!Guid.TryParse(timerIdText, out var timerId))
        {
            context.Log($"[lua-plugin] cancel_scheduled_task rejected for {manifest.Id}: invalid timer id \"{timerIdText}\".");
            return false;
        }

        _scheduledCallbacks.Remove(timerId);
        return context.Scheduler.Cancel(timerId);
    }

    private static Table ResolvePluginTable(Script script, DynValue result)
    {
        if (result.Type == DataType.Table)
        {
            return result.Table;
        }

        var globalPlugin = script.Globals.Get("plugin");
        if (globalPlugin.Type == DataType.Table)
        {
            return globalPlugin.Table;
        }

        throw new InvalidOperationException("Lua plugin entry point must return a plugin table or assign one to global 'plugin'.");
    }

    private DynValue ToDynValue(object? value)
    {
        if (_script is null)
        {
            return DynValue.Nil;
        }

        return ToDynValue(_script, value, depth: 0);
    }

    private static DynValue ToDynValue(Script script, object? value, int depth)
    {
        if (value is null)
        {
            return DynValue.Nil;
        }

        if (depth > 6)
        {
            return DynValue.NewString(value.ToString() ?? string.Empty);
        }

        switch (value)
        {
            case string text:
                return DynValue.NewString(text);
            case char character:
                return DynValue.NewString(character.ToString());
            case bool boolean:
                return DynValue.NewBoolean(boolean);
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                return DynValue.NewNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            case Enum enumValue:
                return DynValue.NewString(enumValue.ToString());
            case Version version:
                return DynValue.NewString(version.ToString());
            case JsonElement jsonElement:
                return ToDynValue(script, JsonElementToObject(jsonElement), depth + 1);
            case IDictionary<string, object?> dictionary:
                return ToDictionaryTable(script, dictionary, depth + 1);
            case System.Collections.IDictionary nonGenericDictionary:
                return ToDictionaryTable(
                    script,
                    nonGenericDictionary.Keys.Cast<object>()
                        .ToDictionary(key => key.ToString() ?? string.Empty, key => nonGenericDictionary[key]),
                    depth + 1);
            case IEnumerable<object?> sequence:
                return ToArrayTable(script, sequence, depth + 1);
            case System.Collections.IEnumerable nonGenericSequence when value is not string:
                return ToArrayTable(script, nonGenericSequence.Cast<object?>(), depth + 1);
        }

        var table = new Table(script);
        foreach (var property in value.GetType().GetProperties(PublicInstanceProperties))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var propertyValue = property.GetValue(value);
            var dynValue = ToDynValue(script, propertyValue, depth + 1);
            table[property.Name] = dynValue;

            var camelCaseName = ToCamelCase(property.Name);
            if (!string.Equals(camelCaseName, property.Name, StringComparison.Ordinal))
            {
                table[camelCaseName] = dynValue;
            }
        }

        return DynValue.NewTable(table);
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => JsonElementToObject(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static void SaveLuaTableJson(string path, DynValue value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        var serialized = DynValueToPlainObject(value);
        var json = JsonSerializer.Serialize(serialized, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static object? DynValueToPlainObject(DynValue value)
    {
        return value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean => value.Boolean,
            DataType.Number => value.Number,
            DataType.String => value.String,
            DataType.Table => LuaTableToPlainObject(value.Table),
            _ => value.ToString(),
        };
    }

    private static object LuaTableToPlainObject(Table table)
    {
        var numericEntries = table.Pairs
            .Where(pair => pair.Key.Type == DataType.Number)
            .OrderBy(pair => pair.Key.Number)
            .ToArray();
        var stringEntries = table.Pairs
            .Where(pair => pair.Key.Type == DataType.String)
            .ToArray();

        if (stringEntries.Length == 0 && numericEntries.Length > 0)
        {
            return numericEntries
                .Select(pair => DynValueToPlainObject(pair.Value))
                .ToArray();
        }

        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in stringEntries)
        {
            dictionary[pair.Key.String] = DynValueToPlainObject(pair.Value);
        }

        foreach (var pair in numericEntries)
        {
            dictionary[pair.Key.Number.ToString(CultureInfo.InvariantCulture)] = DynValueToPlainObject(pair.Value);
        }

        return dictionary;
    }

    private static string ResolveConfigPath(string configDirectory, string relativePath)
    {
        var normalizedRelativePath = string.IsNullOrWhiteSpace(relativePath) ? "config.json" : relativePath;
        var fullConfigDirectory = Path.GetFullPath(configDirectory);
        var combinedPath = Path.GetFullPath(Path.Combine(fullConfigDirectory, normalizedRelativePath));
        if (!combinedPath.StartsWith(fullConfigDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Plugin config path escapes config directory.");
        }

        return combinedPath;
    }

    private bool CanAccessPluginStorage(string functionName, string target)
    {
        if (IsPluginStoragePhaseAllowed(_currentCallbackPhase))
        {
            return true;
        }

        return RejectHostOperation(
            functionName,
            target,
            "Use plugin config access during initialize, lifecycle, update, event, or command callbacks.");
    }

    private bool CanIssueServerMutation(string functionName, string target)
    {
        if (IsServerMutationPhaseAllowed(_currentCallbackPhase))
        {
            return true;
        }

        return RejectHostOperation(
            functionName,
            target,
            "Use bounded server mutations during lifecycle, update, event, or command callbacks.");
    }

    private bool CanAccessProtectedCvars()
    {
        return _activeCommandIdentity?.Authority is OpenGarrisonServerAdminAuthority.RconSession
            or OpenGarrisonServerAdminAuthority.HostConsole
            or OpenGarrisonServerAdminAuthority.AdminPipe;
    }

    private bool CanManageScheduler(string functionName, string target)
    {
        if (IsSchedulerPhaseAllowed(_currentCallbackPhase))
        {
            return true;
        }

        return RejectHostOperation(
            functionName,
            target,
            "Use scheduler control during initialize, lifecycle, update, event, or command callbacks.");
    }

    private bool RejectHostOperation(string functionName, string target, string guidance)
    {
        _context?.Log(
            $"[lua-plugin] {functionName} rejected for {manifest.Id} {target} during {DescribePhase(_currentCallbackPhase)}. {guidance}");
        return false;
    }

    private void CancelAllScheduledCallbacks()
    {
        if (_context is null || _scheduledCallbacks.Count == 0)
        {
            _scheduledCallbacks.Clear();
            return;
        }

        foreach (var timerId in _scheduledCallbacks.Keys.ToArray())
        {
            _context.Scheduler.Cancel(timerId);
        }

        _scheduledCallbacks.Clear();
    }

    private bool TryNormalizePluginMessage(
        string functionName,
        string targetPluginId,
        string messageType,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion,
        string? payload,
        out string normalizedTargetPluginId,
        out string normalizedMessageType,
        out string normalizedPayload)
    {
        if (!PluginMessageContract.TryNormalizeOutgoing(
                targetPluginId,
                messageType,
                payload,
                payloadFormat,
                schemaVersion,
                out normalizedTargetPluginId,
                out normalizedMessageType,
                out normalizedPayload,
                out var error))
        {
            _context?.Log(
                $"[lua-plugin] {functionName} rejected for {manifest.Id} during {DescribePhase(_currentCallbackPhase)}. {error}");
            return false;
        }

        return true;
    }

    private bool TryNormalizeReplicatedStateKey(string functionName, string stateKey, out string normalizedStateKey)
    {
        if (GameplayReplicatedStateContract.TryNormalizeIdentifier(stateKey, out normalizedStateKey))
        {
            return true;
        }

        _context?.Log(
            $"[lua-plugin] {functionName} rejected for {manifest.Id} during {DescribePhase(_currentCallbackPhase)}. Replicated state keys must be non-empty ASCII identifiers up to {GameplayReplicatedStateContract.MaxIdentifierLength} characters.");
        return false;
    }

    private void ExecuteInPhase(ServerLuaCallbackPhase phase, Action action)
    {
        var previousPhase = _currentCallbackPhase;
        _currentCallbackPhase = phase;
        try
        {
            action();
        }
        finally
        {
            _currentCallbackPhase = previousPhase;
        }
    }

    private T ExecuteInPhase<T>(ServerLuaCallbackPhase phase, Func<T> action)
    {
        var previousPhase = _currentCallbackPhase;
        _currentCallbackPhase = phase;
        try
        {
            return action();
        }
        finally
        {
            _currentCallbackPhase = previousPhase;
        }
    }

    private static TimeSpan GetMaxCallbackDuration(ServerLuaCallbackPhase phase)
    {
        return phase switch
        {
            ServerLuaCallbackPhase.Initialize => TimeSpan.FromSeconds(2),
            ServerLuaCallbackPhase.Shutdown => TimeSpan.FromMilliseconds(50),
            ServerLuaCallbackPhase.Lifecycle => TimeSpan.FromMilliseconds(50),
            ServerLuaCallbackPhase.Update => TimeSpan.FromMilliseconds(10),
            ServerLuaCallbackPhase.Query => TimeSpan.FromMilliseconds(10),
            ServerLuaCallbackPhase.CommandInteraction => TimeSpan.FromMilliseconds(250),
            ServerLuaCallbackPhase.Event => TimeSpan.FromMilliseconds(10),
            _ => TimeSpan.FromMilliseconds(10),
        };
    }

    private static int GetMaxCallbackResumeCount(ServerLuaCallbackPhase phase)
    {
        return phase switch
        {
            ServerLuaCallbackPhase.Initialize => MaxInitializeResumeCount,
            _ => MaxCallbackResumeCount,
        };
    }

    private static string DescribePhase(ServerLuaCallbackPhase phase)
    {
        return phase switch
        {
            ServerLuaCallbackPhase.None => "an unmanaged host call",
            ServerLuaCallbackPhase.Initialize => "initialize",
            ServerLuaCallbackPhase.Shutdown => "shutdown",
            ServerLuaCallbackPhase.Lifecycle => "a lifecycle callback",
            ServerLuaCallbackPhase.Update => "an update callback",
            ServerLuaCallbackPhase.Query => "a query callback",
            ServerLuaCallbackPhase.CommandInteraction => "a command callback",
            ServerLuaCallbackPhase.Event => "a server event callback",
            _ => "an unknown callback",
        };
    }

    private static bool IsPluginStoragePhaseAllowed(ServerLuaCallbackPhase phase)
    {
        return phase is ServerLuaCallbackPhase.Initialize
            or ServerLuaCallbackPhase.Lifecycle
            or ServerLuaCallbackPhase.Update
            or ServerLuaCallbackPhase.Event
            or ServerLuaCallbackPhase.CommandInteraction;
    }

    private static bool IsServerMutationPhaseAllowed(ServerLuaCallbackPhase phase)
    {
        return phase is ServerLuaCallbackPhase.Lifecycle
            or ServerLuaCallbackPhase.Update
            or ServerLuaCallbackPhase.Event
            or ServerLuaCallbackPhase.CommandInteraction;
    }

    private static bool IsSchedulerPhaseAllowed(ServerLuaCallbackPhase phase)
    {
        return phase is ServerLuaCallbackPhase.Initialize
            or ServerLuaCallbackPhase.Lifecycle
            or ServerLuaCallbackPhase.Update
            or ServerLuaCallbackPhase.Event
            or ServerLuaCallbackPhase.CommandInteraction;
    }

    private static DynValue ToArrayTable(Script script, IEnumerable<object?> values, int depth)
    {
        var table = new Table(script);
        var index = 1;
        foreach (var item in values)
        {
            table[index] = ToDynValue(script, item, depth);
            index += 1;
        }

        return DynValue.NewTable(table);
    }

    private static DynValue ToDictionaryTable(Script script, IDictionary<string, object?> values, int depth)
    {
        var table = new Table(script);
        foreach (var pair in values)
        {
            table[pair.Key] = ToDynValue(script, pair.Value, depth);
        }

        return DynValue.NewTable(table);
    }

    private static object CreateServerStateSnapshot(IOpenGarrisonServerReadOnlyState state)
    {
        return new
        {
            state.ServerName,
            state.LevelName,
            state.MapAreaIndex,
            state.MapAreaCount,
            state.MapScale,
            GameMode = state.GameMode.ToString(),
            MatchPhase = state.MatchPhase.ToString(),
            state.RedCaps,
            state.BlueCaps,
        };
    }

    private static Table CreateAdminSummaryTable(Script script, IOpenGarrisonServerPluginContext context)
    {
        var players = context.ServerState.GetPlayers();
        var totalPlayers = players.Count;
        var spectatorPlayers = players.Count(player => player.IsSpectator);
        var authorizedPlayers = players.Count(player => player.IsAuthorized);

        var table = new Table(script);
        table["serverName"] = DynValue.NewString(context.ServerState.ServerName);
        table["levelName"] = DynValue.NewString(context.ServerState.LevelName);
        table["mapAreaIndex"] = DynValue.NewNumber(context.ServerState.MapAreaIndex);
        table["mapAreaCount"] = DynValue.NewNumber(context.ServerState.MapAreaCount);
        table["mapScale"] = DynValue.NewNumber(context.ServerState.MapScale);
        table["gameMode"] = DynValue.NewString(context.ServerState.GameMode.ToString());
        table["matchPhase"] = DynValue.NewString(context.ServerState.MatchPhase.ToString());
        table["redCaps"] = DynValue.NewNumber(context.ServerState.RedCaps);
        table["blueCaps"] = DynValue.NewNumber(context.ServerState.BlueCaps);
        table["playerCount"] = DynValue.NewNumber(totalPlayers);
        table["activePlayerCount"] = DynValue.NewNumber(totalPlayers - spectatorPlayers);
        table["spectatorCount"] = DynValue.NewNumber(spectatorPlayers);
        table["authorizedPlayerCount"] = DynValue.NewNumber(authorizedPlayers);
        table["scheduledTaskCount"] = DynValue.NewNumber(context.Scheduler.GetScheduledTasks().Count);
        table["uptimeSeconds"] = DynValue.NewNumber(context.Scheduler.Uptime.TotalSeconds);
        return table;
    }

    private static object CreateFilteredCvarResult(
        IReadOnlyList<OpenGarrisonServerCvarInfo> cvars,
        string? filter,
        int limit)
    {
        var normalizedFilter = filter?.Trim() ?? string.Empty;
        if (limit <= 0)
        {
            limit = 16;
        }

        var entries = cvars
            .Where(cvar => normalizedFilter.Length == 0
                || cvar.Name.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase)
                || cvar.Description.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(cvar => cvar.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Min(limit, 64))
            .Select(cvar => new
            {
                cvar.Name,
                cvar.Description,
                ValueType = cvar.ValueType.ToString(),
                cvar.DefaultValue,
                cvar.CurrentValue,
                cvar.IsProtected,
                cvar.IsReadOnly,
                cvar.MinimumNumericValue,
                cvar.MaximumNumericValue,
            })
            .ToArray();

        return new
        {
            Filter = normalizedFilter,
            Count = entries.Length,
            Items = entries,
        };
    }

    private static object? TryResolveLevel(string arguments)
    {
        var trimmed = arguments.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var level = SimpleLevelFactory.CreateImportedLevel(trimmed, mapAreaIndex: 1);
        if (level is null)
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1
                && int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedArea)
                && parsedArea > 0)
            {
                var levelName = string.Join(' ', parts[..^1]);
                if (levelName.Length > 0)
                {
                    level = SimpleLevelFactory.CreateImportedLevel(levelName, parsedArea);
                }
            }
        }

        if (level is null)
        {
            return null;
        }

        return new
        {
            level.Name,
            level.MapAreaIndex,
            level.MapAreaCount,
            Mode = level.Mode.ToString(),
        };
    }

    private static string ReadStringArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.IsNil() ? string.Empty : dynValue.CastToString();
    }

    private static string? ReadOptionalStringArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            return null;
        }

        var value = dynValue.CastToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Table? ReadOptionalTableArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.Type == DataType.Table ? dynValue.Table : null;
    }

    private static byte ReadByteArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.Type == DataType.Number
            ? (byte)dynValue.Number
            : byte.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture);
    }

    private static byte? ReadOptionalByteField(Table? table, string fieldName)
    {
        if (table is null)
        {
            return null;
        }

        var dynValue = table.Get(fieldName);
        if (dynValue.IsNil())
        {
            return null;
        }

        return dynValue.Type == DataType.Number
            ? (byte)dynValue.Number
            : byte.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture);
    }

    private static ushort ReadOptionalUShortArgument(CallbackArguments args, int index, ushort defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            return defaultValue;
        }

        return dynValue.Type == DataType.Number
            ? (ushort)dynValue.Number
            : ushort.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture);
    }

    private static int ReadIntArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.Type == DataType.Number
            ? (int)dynValue.Number
            : int.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture);
    }

    private static int ReadOptionalIntArgument(CallbackArguments args, int index, int defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            return defaultValue;
        }

        return dynValue.Type == DataType.Number
            ? (int)dynValue.Number
            : int.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture);
    }

    private static double ReadDoubleArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.Type == DataType.Number
            ? dynValue.Number
            : double.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture);
    }

    private static bool ReadBoolArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.Type switch
        {
            DataType.Boolean => dynValue.Boolean,
            DataType.Number => Math.Abs(dynValue.Number) > double.Epsilon,
            _ => bool.Parse(dynValue.CastToString()),
        };
    }

    private static bool ReadOptionalBoolArgument(CallbackArguments args, int index, bool defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.IsNil()
            ? defaultValue
            : ReadBoolArgument(args, index);
    }

    private static bool ReadOptionalBoolField(Table? table, string fieldName, bool defaultValue)
    {
        if (table is null)
        {
            return defaultValue;
        }

        var dynValue = table.Get(fieldName);
        if (dynValue.IsNil())
        {
            return defaultValue;
        }

        return dynValue.Type switch
        {
            DataType.Boolean => dynValue.Boolean,
            DataType.Number => Math.Abs(dynValue.Number) > double.Epsilon,
            _ => bool.Parse(dynValue.CastToString()),
        };
    }

    private static PluginMessagePayloadFormat ReadOptionalEnumArgument(CallbackArguments args, int index, PluginMessagePayloadFormat defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.IsNil() || !Enum.TryParse<PluginMessagePayloadFormat>(dynValue.CastToString(), ignoreCase: true, out var value)
            ? defaultValue
            : value;
    }

    private static bool TryParseEnumArgument<TEnum>(CallbackArguments args, int index, out TEnum value) where TEnum : struct, Enum
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            value = default;
            return false;
        }

        if (dynValue.Type == DataType.Number)
        {
            value = (TEnum)Enum.ToObject(typeof(TEnum), (int)dynValue.Number);
            return Enum.IsDefined(value);
        }

        return Enum.TryParse(dynValue.CastToString(), ignoreCase: true, out value);
    }

    private static DynValue ReadArgument(CallbackArguments args, int index)
    {
        if (args.Count <= index)
        {
            return DynValue.Nil;
        }

        if (args.Count > index + 1 && args[0].Type == DataType.Table)
        {
            return args[index + 1];
        }

        return args[index];
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private void LogCallbackFailure(string callbackName, Exception ex)
    {
        _context?.Log($"[lua-plugin] callback failed for {manifest.Id} callback \"{callbackName}\" manifest \"{GetManifestPath()}\": {ex.Message}");
    }

    private void DisableCallbacks(string reason)
    {
        if (_callbacksDisabled)
        {
            return;
        }

        _callbacksDisabled = true;
        _context?.Log($"[lua-plugin] disabled {manifest.Id}: {reason}");
    }

    private string GetManifestPath()
    {
        return OpenGarrisonPluginManifestLoader.GetManifestPath(pluginDirectory);
    }

    private enum ServerLuaCallbackPhase
    {
        None,
        Initialize,
        Shutdown,
        Lifecycle,
        Update,
        Query,
        CommandInteraction,
        Event,
    }
}
