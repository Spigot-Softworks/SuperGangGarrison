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
    IOpenGarrisonServerDecisionHooks,
    IOpenGarrisonServerPluginMessageHooks,
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
    private readonly Dictionary<string, DynValue> _callbackCache = new(StringComparer.Ordinal);
    private ServerLuaCallbackPhase _currentCallbackPhase = ServerLuaCallbackPhase.None;
    private OpenGarrisonServerAdminIdentity? _activeCommandIdentity;
    private bool _callbacksDisabled;
    private readonly Dictionary<Guid, DynValue> _scheduledCallbacks = [];
    private readonly Queue<LuaDeferredServerAction> _deferredServerActions = [];
    private const long CallbackAutoYieldCounter = 1000;
    private const int MaxCallbackResumeCount = 4096;
    private const int MaxInitializeResumeCount = 65536;
    private const int MaxDeferredServerActions = 256;

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
            var entryPointPath = OpenGarrisonPluginPathContainment.ResolveContainedPath(
                pluginDirectory,
                manifest.EntryPoint,
                "Lua entry point escapes plugin directory.");
            if (!File.Exists(entryPointPath))
            {
                throw new FileNotFoundException($"Lua entry point was not found: {entryPointPath}", entryPointPath);
            }

            _script = new Script(CoreModules.Preset_SoftSandbox);
            _script.Options.DebugPrint = message => context.Log($"[lua] {message}");

            var hostTable = CreateHostTable(_script, context);
            var result = _script.DoFile(entryPointPath);
            _pluginTable = ResolvePluginTable(_script, result);
            _callbackCache.Clear();
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
        _deferredServerActions.Clear();
        ExecuteInPhase(ServerLuaCallbackPhase.Shutdown, () => CallIfPresent("shutdown"));
        _callbackCache.Clear();
        _pluginTable = null;
        _script = null;
        _context = null;
        _callbacksDisabled = false;
    }

    public void OnServerStarting() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_starting"));

    public void OnServerStarted() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_started"));

    public void OnServerStopping() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_stopping"));

    public void OnServerStopped() => ExecuteInPhase(ServerLuaCallbackPhase.Lifecycle, () => CallIfPresent("on_server_stopped"));

    public void OnServerHeartbeat(TimeSpan uptime) => ExecuteInPhase(ServerLuaCallbackPhase.Update, () =>
    {
        CallIfPresent("on_server_heartbeat", DynValue.NewNumber(uptime.TotalSeconds));
        DrainDeferredServerActions();
    });

    public void OnHelloReceived(HelloReceivedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_hello_received", ToDynValue(e)));

    public void OnClientConnected(ClientConnectedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_client_connected", ToDynValue(e)));

    public void OnClientDisconnected(ClientDisconnectedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_client_disconnected", ToDynValue(e)));

    public void OnPasswordAccepted(PasswordAcceptedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_password_accepted", ToDynValue(e)));

    public void OnPlayerTeamChanged(PlayerTeamChangedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_team_changed", ToDynValue(e)));

    public void OnPlayerClassChanged(PlayerClassChangedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_player_class_changed", ToDynValue(e)));

    public void OnChatReceived(ChatReceivedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_chat_received", ToDynValue(e)));

    public OpenGarrisonServerDecisionResult BeforeChatMessage(ChatReceivedEvent e)
    {
        return ExecuteInPhase(ServerLuaCallbackPhase.Event, () =>
            TryInvokeCallback("before_chat_message", out var result, ToDynValue(e))
                ? ReadDecisionResult(result)
                : OpenGarrisonServerDecisionResult.Continue);
    }

    public OpenGarrisonServerDecisionResult BeforeTeamChange(OpenGarrisonServerTeamChangeRequest e)
    {
        return ExecuteServerDecisionCallback("before_team_change", e);
    }

    public OpenGarrisonServerDecisionResult BeforeClassChange(OpenGarrisonServerClassChangeRequest e)
    {
        return ExecuteServerDecisionCallback("before_class_change", e);
    }

    public OpenGarrisonServerDecisionResult BeforeLoadoutChange(OpenGarrisonServerLoadoutChangeRequest e)
    {
        return ExecuteServerDecisionCallback("before_loadout_change", e);
    }

    public OpenGarrisonServerDecisionResult BeforeMapChange(OpenGarrisonServerMapChangeRequest e)
    {
        return ExecuteServerDecisionCallback("before_map_change", e);
    }

    public OpenGarrisonServerDecisionResult BeforeSpawn(OpenGarrisonServerSpawnRequest e)
    {
        return ExecuteServerDecisionCallback("before_spawn", e);
    }

    public OpenGarrisonServerDecisionResult BeforeDamage(OpenGarrisonServerDamageRequest e)
    {
        return ExecuteServerDecisionCallback("before_damage", e);
    }

    public OpenGarrisonServerDecisionResult BeforeDeath(OpenGarrisonServerDeathRequest e)
    {
        return ExecuteServerDecisionCallback("before_death", e);
    }

    public OpenGarrisonServerDecisionResult BeforePickup(OpenGarrisonServerPickupRequest e)
    {
        return ExecuteServerDecisionCallback("before_pickup", e);
    }

    public OpenGarrisonServerDecisionResult BeforeScore(OpenGarrisonServerScoreRequest e)
    {
        return ExecuteServerDecisionCallback("before_score", e);
    }

    public OpenGarrisonServerDecisionResult BeforeRoundEnd(OpenGarrisonServerRoundEndRequest e)
    {
        return ExecuteServerDecisionCallback("before_round_end", e);
    }

    private OpenGarrisonServerDecisionResult ExecuteServerDecisionCallback(string callbackName, object e)
    {
        return ExecuteInPhase(ServerLuaCallbackPhase.Event, () =>
            TryInvokeCallback(callbackName, out var result, ToDynValue(e))
                ? ReadDecisionResult(result)
                : OpenGarrisonServerDecisionResult.Continue);
    }

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
                if (!TryInvokeCallback("try_handle_chat_message", out var result, CreateCommandInteractionContext(context), ToDynValue(e)))
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

    public void OnClientPluginMessage(OpenGarrisonServerPluginMessageEnvelope e)
        // Client-originated plugin messages are typically explicit user interactions
        // such as admin UI selections, so they need the same bounded headroom as
        // chat/admin commands instead of the tighter generic event budget.
        => ExecuteInPhase(ServerLuaCallbackPhase.CommandInteraction, () => CallIfPresent("on_client_plugin_message", ToDynValue(e)));

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

    public void OnGameplayAbilityInput(OpenGarrisonServerGameplayAbilityInputEvent e)
    {
        ExecuteInPhase(ServerLuaCallbackPhase.Event, () =>
        {
            if (TryInvokeCallback("on_gameplay_ability_input", out var result, ToDynValue(e))
                && result.Type == DataType.Boolean
                && !result.Boolean)
            {
                e.Cancel();
            }
        });
    }

    public void OnGameplayAbilityUsed(OpenGarrisonServerGameplayAbilityUsedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_gameplay_ability_used", ToDynValue(e)));

    public void OnGameplayAbilityStateChanged(OpenGarrisonServerGameplayAbilityStateChangedEvent e) => ExecuteInPhase(ServerLuaCallbackPhase.Event, () => CallIfPresent("on_gameplay_ability_state_changed", ToDynValue(e)));

    private static OpenGarrisonServerDecisionResult ReadDecisionResult(DynValue result)
    {
        if (result.IsNil() || result.Type == DataType.Void)
        {
            return OpenGarrisonServerDecisionResult.Continue;
        }

        if (result.Type == DataType.Boolean)
        {
            return result.Boolean
                ? OpenGarrisonServerDecisionResult.Cancel()
                : OpenGarrisonServerDecisionResult.Continue;
        }

        if (result.Type == DataType.String)
        {
            return string.IsNullOrWhiteSpace(result.String)
                ? OpenGarrisonServerDecisionResult.Continue
                : OpenGarrisonServerDecisionResult.Cancel(result.String);
        }

        if (result.Type == DataType.Table)
        {
            var table = result.Table;
            var cancelValue = ReadField(table, "cancel", "Cancel", "cancelled", "Cancelled", "isCancelled", "IsCancelled");
            var isCancelled = cancelValue.Type switch
            {
                DataType.Boolean => cancelValue.Boolean,
                DataType.Number => Math.Abs(cancelValue.Number) > double.Epsilon,
                DataType.String => bool.Parse(cancelValue.String),
                _ => false,
            };
            var reason = ReadOptionalStringField(table, "reason", "Reason") ?? string.Empty;
            return isCancelled
                ? OpenGarrisonServerDecisionResult.Cancel(reason)
                : OpenGarrisonServerDecisionResult.Continue;
        }

        return OpenGarrisonServerDecisionResult.Continue;
    }

    private GameplayAbilityResult ExecuteGameplayAbility(GameplayAbilityContext context)
    {
        return ExecuteInPhase(ServerLuaCallbackPhase.AbilityExecution, () =>
        {
            if (!TryInvokeCallback("on_gameplay_ability_execute", out var result, ToDynValue(CreateLuaGameplayAbilityExecutionEvent(context))))
            {
                return GameplayAbilityResult.Ignored;
            }

            return ReadGameplayAbilityExecutionResult(result);
        });
    }

    private GameplayPrimaryWeaponResult ExecuteGameplayPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        return ExecuteInPhase(ServerLuaCallbackPhase.PrimaryWeaponExecution, () =>
        {
            if (!TryInvokeCallback("on_gameplay_primary_weapon_execute", out var result, ToDynValue(CreateLuaGameplayPrimaryWeaponExecutionEvent(context))))
            {
                return GameplayPrimaryWeaponResult.Ignored;
            }

            return ReadGameplayPrimaryWeaponExecutionResult(result);
        });
    }

    private object CreateLuaGameplayAbilityExecutionEvent(GameplayAbilityContext context)
    {
        return new
        {
            Frame = context.World.Frame,
            PlayerId = context.Player.Id,
            context.Player.ClassId,
            context.Player.Team,
            ItemId = context.Item.Id,
            context.Item.BehaviorId,
            AbilityCategory = context.Ability.Category,
            context.Ability.Activation,
            context.Ability.ExecutorId,
            Phase = context.Phase.ToString(),
            Tags = context.Ability.Tags.ToArray(),
            Parameters = context.Ability.Parameters.ToDictionary(
                static pair => pair.Key,
                static pair => JsonElementToObject(pair.Value),
                StringComparer.Ordinal),
            context.SourceX,
            context.SourceY,
            context.Input.AimWorldX,
            context.Input.AimWorldY,
        };
    }

    private static object CreateLuaGameplayPrimaryWeaponExecutionEvent(GameplayPrimaryWeaponContext context)
    {
        return new
        {
            Frame = context.World.Frame,
            PlayerId = context.Player.Id,
            context.Player.ClassId,
            context.Player.Team,
            context.ItemId,
            context.BehaviorId,
            WeaponClassId = context.WeaponClassId,
            Weapon = new
            {
                context.Weapon.DisplayName,
                Kind = context.Weapon.Kind.ToString(),
                context.Weapon.MaxAmmo,
                context.Weapon.AmmoPerShot,
                context.Weapon.ProjectilesPerShot,
                context.Weapon.ReloadDelayTicks,
                context.Weapon.AmmoReloadTicks,
                context.Weapon.SpreadDegrees,
                context.Weapon.MinShotSpeed,
                context.Weapon.AdditionalRandomShotSpeed,
                context.Weapon.DirectHitDamage,
                context.Weapon.DamagePerTick,
                context.Weapon.DirectHitHealAmount,
                context.Weapon.ActiveProjectileLimit,
            },
            context.SourceX,
            context.SourceY,
            context.AimWorldX,
            context.AimWorldY,
            context.DirectionX,
            context.DirectionY,
            context.DirectionRadians,
            context.KillFeedWeaponSpriteName,
        };
    }

    private static GameplayAbilityResult ReadGameplayAbilityExecutionResult(DynValue result)
    {
        if (result.Type == DataType.Boolean)
        {
            return result.Boolean ? GameplayAbilityResult.HandledAndConsumed : GameplayAbilityResult.Ignored;
        }

        if (result.Type != DataType.Table)
        {
            return GameplayAbilityResult.Ignored;
        }

        var handled = ReadOptionalBoolField(result.Table, false, "handled", "Handled");
        var consumedInput = ReadOptionalBoolField(result.Table, handled, "consumedInput", "ConsumedInput", "consumed_input");
        var suppressPrimary = ReadOptionalBoolField(result.Table, false, "suppressPrimary", "SuppressPrimary", "suppress_primary");
        return new GameplayAbilityResult(handled, consumedInput, suppressPrimary);
    }

    private static GameplayPrimaryWeaponResult ReadGameplayPrimaryWeaponExecutionResult(DynValue result)
    {
        if (result.Type == DataType.Boolean)
        {
            return result.Boolean ? GameplayPrimaryWeaponResult.HandledResult : GameplayPrimaryWeaponResult.Ignored;
        }

        if (result.Type != DataType.Table)
        {
            return GameplayPrimaryWeaponResult.Ignored;
        }

        return new GameplayPrimaryWeaponResult(ReadOptionalBoolField(result.Table, false, "handled", "Handled"));
    }

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

        if (!TryGetCachedCallbackFunction(callbackName, out var function))
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
        host["register_command"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterLuaCommand("register_command", "Lua command registration"))
            {
                return DynValue.False;
            }

            var registration = ReadLuaCommandRegistration(ReadArgument(args, 0));
            context.RegisterCommand(registration.Command, registration.RequiredPermissions, registration.Aliases);
            return DynValue.True;
        });
        host["get_server_state"] = DynValue.NewCallback((_, _) => DynValue.NewTable(CreateServerStateTable(script, context.ServerState)));
        host["get_match_state"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetMatchState()));
        host["get_objectives"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetObjectives()));
        host["get_buildables"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetBuildables()));
        host["get_projectiles"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetProjectiles()));
        host["get_recent_events"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetRecentEvents()));
        host["get_map_region"] = DynValue.NewCallback((_, args) =>
        {
            var query = ReadOptionalTableArgument(args, 0);
            if (query is not null)
            {
                return ToDynValue(context.ServerState.GetMapRegion(
                    ReadOptionalFloatField(query, 0f, "x", "X", "centerX", "CenterX", "center_x"),
                    ReadOptionalFloatField(query, 0f, "y", "Y", "centerY", "CenterY", "center_y"),
                    ReadOptionalFloatField(query, 256f, "radius", "Radius"),
                    ReadOptionalIntField(query, 128, "limit", "Limit")));
            }

            return ToDynValue(context.ServerState.GetMapRegion(
                (float)ReadDoubleArgument(args, 0),
                (float)ReadDoubleArgument(args, 1),
                (float)ReadOptionalDoubleArgument(args, 2, 256d),
                ReadOptionalIntArgument(args, 3, 128)));
        });
        host["has_line_of_sight"] = DynValue.NewCallback((_, args) =>
        {
            var query = ReadOptionalTableArgument(args, 0);
            if (query is not null)
            {
                return ToDynValue(context.ServerState.HasLineOfSight(
                    ReadOptionalFloatField(query, 0f, "originX", "OriginX", "origin_x", "x1"),
                    ReadOptionalFloatField(query, 0f, "originY", "OriginY", "origin_y", "y1"),
                    ReadOptionalFloatField(query, 0f, "targetX", "TargetX", "target_x", "x2"),
                    ReadOptionalFloatField(query, 0f, "targetY", "TargetY", "target_y", "y2"),
                    ReadOptionalEnumField<PlayerTeam>(query, "team", "Team", "targetTeam", "TargetTeam", "target_team")));
            }

            return ToDynValue(context.ServerState.HasLineOfSight(
                (float)ReadDoubleArgument(args, 0),
                (float)ReadDoubleArgument(args, 1),
                (float)ReadDoubleArgument(args, 2),
                (float)ReadDoubleArgument(args, 3),
                ReadOptionalEnumArgument<PlayerTeam>(args, 4)));
        });
        host["get_player_state"] = DynValue.NewCallback((_, args) =>
            TryResolvePlayerStateQuery(context.ServerState, ReadArgument(args, 0), out var player)
                ? DynValue.NewTable(CreatePlayerInfoTable(script, player))
                : DynValue.Nil);
        host["get_admin_summary"] = DynValue.NewCallback((_, _) => DynValue.NewTable(CreateAdminSummaryTable(script, context)));
        host["get_players"] = DynValue.NewCallback((_, _) => DynValue.NewTable(CreatePlayerListTable(script, context.ServerState.GetPlayers())));
        host["resolve_targets"] = DynValue.NewCallback((_, args) =>
        {
            var options = ReadOptionalTableArgument(args, 1);
            var resolution = targetResolver.Resolve(
                ReadStringArgument(args, 0),
                new ServerAdminTargetQueryOptions(
                    SourceSlot: ReadOptionalByteField(options, "sourceSlot"),
                    AllowMultiple: ReadOptionalBoolField(options, "allowMultiple", true),
                    RequireAlive: ReadOptionalBoolField(options, "requireAlive", false),
                    IncludeSpectators: ReadOptionalBoolField(options, "includeSpectators", true)));
            return ToDynValue(CreateLuaTargetResolution(resolution));
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
        host["get_gameplay_abilities"] = DynValue.NewCallback((_, _) =>
            ToDynValue(context.ServerState.GetGameplayAbilities()));
        host["get_player_gameplay_abilities"] = DynValue.NewCallback((_, args) =>
            ToDynValue(context.ServerState.GetPlayerGameplayAbilities(ReadIntArgument(args, 0))));
        host["get_player_gameplay_ability"] = DynValue.NewCallback((_, args) =>
            context.ServerState.TryGetPlayerGameplayAbility(
                ReadIntArgument(args, 0),
                ReadStringArgument(args, 1),
                out var ability)
                ? ToDynValue(ability)
                : DynValue.Nil);
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
        host["enqueue_action"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanEnqueueServerAction("enqueue_action", "deferred server action"))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(EnqueueDeferredServerAction(ReadDeferredServerAction(args)));
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
        host["try_set_player_movement_speed_scale"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_player_movement_speed_scale", $"player {slot}")
                && context.AdminOperations.TrySetPlayerMovementSpeedScale(slot, (float)ReadDoubleArgument(args, 1)));
        });
        host["try_clear_player_movement_speed_scale"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_clear_player_movement_speed_scale", $"player {slot}")
                && context.AdminOperations.TryClearPlayerMovementSpeedScale(slot));
        });
        host["try_set_player_gravity_scale"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_set_player_gravity_scale", $"player {slot}")
                && context.AdminOperations.TrySetPlayerGravityScale(slot, (float)ReadDoubleArgument(args, 1)));
        });
        host["try_clear_player_gravity_scale"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_clear_player_gravity_scale", $"player {slot}")
                && context.AdminOperations.TryClearPlayerGravityScale(slot));
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
        host["get_bot_slots"] = DynValue.NewCallback((_, _) =>
            ToDynValue(context.AdminOperations.GetBotSlots()));
        host["try_add_bot"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_add_bot", $"bot slot {slot}")
                && TryParseEnumArgument<PlayerTeam>(args, 1, out var team)
                && TryParseEnumArgument<PlayerClass>(args, 2, out var playerClass)
                && context.AdminOperations.TryAddBot(slot, team, playerClass, ReadOptionalStringArgument(args, 3) ?? string.Empty));
        });
        host["try_remove_bot"] = DynValue.NewCallback((_, args) =>
        {
            var slot = ReadByteArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_remove_bot", $"bot slot {slot}")
                && context.AdminOperations.TryRemoveBot(slot));
        });
        host["try_fill_bots"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_fill_bots", "bot roster"))
            {
                return DynValue.NewNumber(0);
            }

            PlayerClass? requestedClass = TryParseOptionalEnumArgument(args, 1, out PlayerClass playerClass)
                ? playerClass
                : null;
            return DynValue.NewNumber(context.AdminOperations.TryFillBots(ReadIntArgument(args, 0), requestedClass));
        });
        host["try_fill_bot_team"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_fill_bot_team", "bot team roster"))
            {
                return DynValue.NewNumber(0);
            }

            PlayerClass? requestedClass = TryParseOptionalEnumArgument(args, 2, out PlayerClass playerClass)
                ? playerClass
                : null;
            return DynValue.NewNumber(
                TryParseEnumArgument<PlayerTeam>(args, 0, out var team)
                    ? context.AdminOperations.TryFillBotTeam(team, ReadIntArgument(args, 1), requestedClass)
                    : 0);
        });
        host["try_clear_all_bots"] = DynValue.NewCallback((_, _) =>
        {
            if (!CanIssueServerMutation("try_clear_all_bots", "bot roster"))
            {
                return DynValue.NewNumber(0);
            }

            return DynValue.NewNumber(context.AdminOperations.TryClearAllBots());
        });
        host["get_demo_recording_status"] = DynValue.NewCallback((_, _) =>
            DynValue.NewString(context.AdminOperations.GetDemoRecordingStatus()));
        host["try_start_demo_recording"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_start_demo_recording", "demo recording"))
            {
                return ToDynValue(new OpenGarrisonServerDemoRecordingResult(false, string.Empty, "Admin access required."));
            }

            return ToDynValue(context.AdminOperations.TryStartDemoRecording(ReadOptionalStringArgument(args, 0)));
        });
        host["try_stop_demo_recording"] = DynValue.NewCallback((_, _) =>
        {
            if (!CanIssueServerMutation("try_stop_demo_recording", "demo recording"))
            {
                return ToDynValue(new OpenGarrisonServerDemoRecordingResult(false, string.Empty, "Admin access required."));
            }

            return ToDynValue(context.AdminOperations.TryStopDemoRecording());
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
        host["get_player_replicated_state_int"] = DynValue.NewCallback((_, args) =>
            context.ServerState.TryGetPlayerReplicatedStateInt(
                ReadIntArgument(args, 0),
                ReadStringArgument(args, 1),
                ReadStringArgument(args, 2),
                out var value)
                ? DynValue.NewNumber(value)
                : DynValue.Nil);
        host["get_player_replicated_state_float"] = DynValue.NewCallback((_, args) =>
            context.ServerState.TryGetPlayerReplicatedStateFloat(
                ReadIntArgument(args, 0),
                ReadStringArgument(args, 1),
                ReadStringArgument(args, 2),
                out var value)
                ? DynValue.NewNumber(value)
                : DynValue.Nil);
        host["get_player_replicated_state_bool"] = DynValue.NewCallback((_, args) =>
            context.ServerState.TryGetPlayerReplicatedStateBool(
                ReadIntArgument(args, 0),
                ReadStringArgument(args, 1),
                ReadStringArgument(args, 2),
                out var value)
                ? DynValue.NewBoolean(value)
                : DynValue.Nil);
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
        host["try_apply_gameplay_impulse"] = DynValue.NewCallback((_, args) =>
        {
            var playerId = ReadIntArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_apply_gameplay_impulse", $"player {playerId}")
                && context.TryApplyGameplayImpulse(
                    playerId,
                    (float)ReadDoubleArgument(args, 1),
                    (float)ReadDoubleArgument(args, 2)));
        });
        host["try_set_gameplay_ability_cooldown"] = DynValue.NewCallback((_, args) =>
        {
            var playerId = ReadIntArgument(args, 0);
            if (!CanIssueServerMutation("try_set_gameplay_ability_cooldown", $"player {playerId}"))
            {
                return DynValue.False;
            }

            if (!TryNormalizeReplicatedStateKey("try_set_gameplay_ability_cooldown", ReadStringArgument(args, 1), out var normalizedCooldownKey))
            {
                return DynValue.False;
            }

            return DynValue.NewBoolean(context.TrySetGameplayAbilityCooldown(playerId, normalizedCooldownKey, ReadIntArgument(args, 2)));
        });
        host["try_apply_gameplay_damage"] = DynValue.NewCallback((_, args) =>
        {
            var targetPlayerId = ReadIntArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_apply_gameplay_damage", $"player {targetPlayerId}")
                && context.TryApplyGameplayDamage(
                    targetPlayerId,
                    (float)ReadDoubleArgument(args, 1),
                    ReadOptionalNullableIntArgument(args, 2),
                    ReadOptionalStringArgument(args, 3)));
        });
        host["try_apply_gameplay_healing"] = DynValue.NewCallback((_, args) =>
        {
            var playerId = ReadIntArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_apply_gameplay_healing", $"player {playerId}")
                && context.TryApplyGameplayHealing(playerId, (float)ReadDoubleArgument(args, 1)));
        });
        host["try_apply_gameplay_status_effect"] = DynValue.NewCallback((_, args) =>
        {
            var playerId = ReadIntArgument(args, 0);
            return DynValue.NewBoolean(
                CanIssueServerMutation("try_apply_gameplay_status_effect", $"player {playerId}")
                && context.TryApplyGameplayStatusEffect(
                    playerId,
                    ReadStringArgument(args, 1),
                    ReadIntArgument(args, 2),
                    (float)ReadOptionalDoubleArgument(args, 3, 0d)));
        });
        host["try_spawn_gameplay_projectile"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanIssueServerMutation("try_spawn_gameplay_projectile", "gameplay projectile"))
            {
                return DynValue.False;
            }

            var request = ReadGameplayProjectileSpawnRequest(ReadArgument(args, 0));
            return context.TrySpawnGameplayProjectile(request, out var projectileId)
                ? DynValue.NewNumber(projectileId)
                : DynValue.False;
        });
        host["register_gameplay_ability"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterGameplayAbility("register_gameplay_ability", "gameplay ability registration"))
            {
                return DynValue.False;
            }

            var registration = ReadGameplayAbilityRegistration(ReadArgument(args, 0));
            if (context.TryRegisterGameplayAbility(registration, out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] register_gameplay_ability rejected for {manifest.Id}: {errorMessage}");
            return DynValue.False;
        });
        host["override_gameplay_ability"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterGameplayAbility("override_gameplay_ability", "gameplay ability override"))
            {
                return DynValue.False;
            }

            var itemId = ReadStringArgument(args, 0);
            var patch = ReadGameplayAbilityPatch(ReadArgument(args, 1));
            if (context.TryOverrideGameplayAbility(itemId, patch, out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] override_gameplay_ability rejected for {manifest.Id}: {errorMessage}");
            return DynValue.False;
        });
        host["register_gameplay_ability_executor"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterGameplayAbility("register_gameplay_ability_executor", "gameplay ability executor registration"))
            {
                return DynValue.False;
            }

            var executorId = ReadStringArgument(args, 0);
            if (context.TryRegisterGameplayAbilityExecutor(executorId, new LuaGameplayAbilityExecutor(this), out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] register_gameplay_ability_executor rejected for {manifest.Id}: {errorMessage}");
            return DynValue.False;
        });
        host["register_gameplay_primary_weapon_behavior"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterGameplayAbility("register_gameplay_primary_weapon_behavior", "gameplay primary weapon behavior registration"))
            {
                return DynValue.False;
            }

            var (behaviorId, fireSoundName) = ReadGameplayPrimaryWeaponBehaviorRegistration(args);
            if (context.TryRegisterGameplayPrimaryWeaponBehavior(behaviorId, new LuaGameplayPrimaryWeaponExecutor(this), fireSoundName, out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] register_gameplay_primary_weapon_behavior rejected for {manifest.Id}: {errorMessage}");
            return DynValue.False;
        });
        host["register_gameplay_weapon_item"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterGameplayAbility("register_gameplay_weapon_item", "gameplay weapon item registration"))
            {
                return DynValue.False;
            }

            var registration = ReadGameplayWeaponItemRegistration(ReadArgument(args, 0));
            if (context.TryRegisterGameplayWeaponItem(registration, out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] register_gameplay_weapon_item rejected for {manifest.Id}: {errorMessage}");
            return DynValue.False;
        });
        host["register_gameplay_loadout"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterGameplayAbility("register_gameplay_loadout", "gameplay loadout registration"))
            {
                return DynValue.False;
            }

            var registration = ReadGameplayLoadoutRegistration(ReadArgument(args, 0));
            if (context.TryRegisterGameplayLoadout(registration, out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] register_gameplay_loadout rejected for {manifest.Id}: {errorMessage}");
            return DynValue.False;
        });
        host["register_gameplay_slot_item"] = DynValue.NewCallback((_, args) =>
        {
            if (!CanRegisterGameplayAbility("register_gameplay_slot_item", "gameplay slot item registration"))
            {
                return DynValue.False;
            }

            var registration = ReadGameplaySlotItemRegistration(ReadArgument(args, 0));
            if (context.TryRegisterGameplaySlotItem(registration, out var errorMessage))
            {
                return DynValue.True;
            }

            context.Log($"[lua-plugin] register_gameplay_slot_item rejected for {manifest.Id}: {errorMessage}");
            return DynValue.False;
        });

        ValidateLuaHostApiSurface(OpenGarrisonPluginType.Server, context.HostApi, host);
        return host;
    }

    private static void ValidateLuaHostApiSurface(OpenGarrisonPluginType hostType, OpenGarrisonPluginHostApi hostApi, Table host)
    {
        if (hostApi.HostType != hostType)
        {
            throw new InvalidOperationException($"Lua host API type mismatch. Expected {hostType}, got {hostApi.HostType}.");
        }

        var surface = hostApi.RuntimeSurfaces.FirstOrDefault(static surface => surface.Runtime == OpenGarrisonPluginRuntimeKind.Lua);
        if (surface is null)
        {
            throw new InvalidOperationException("Lua host API surface is missing.");
        }

        var advertisedFunctions = surface.Functions.ToHashSet(StringComparer.Ordinal);
        var actualFunctions = host.Pairs
            .Where(static pair => pair.Key.Type == DataType.String && !IsHostMetadataKey(pair.Key.String))
            .Select(static pair => pair.Key.String)
            .ToHashSet(StringComparer.Ordinal);

        var missingBindings = advertisedFunctions
            .Except(actualFunctions, StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var undocumentedBindings = actualFunctions
            .Except(advertisedFunctions, StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        if (missingBindings.Length > 0 || undocumentedBindings.Length > 0)
        {
            throw new InvalidOperationException(
                $"Lua host API surface mismatch for {hostType}. Missing bindings: {FormatFunctionList(missingBindings)}. Undocumented bindings: {FormatFunctionList(undocumentedBindings)}.");
        }
    }

    private static string FormatFunctionList(string[] functions)
    {
        return functions.Length == 0 ? "none" : string.Join(", ", functions);
    }

    private static bool IsHostMetadataKey(string key)
    {
        return key is "plugin_id" or "plugin_directory" or "config_directory" or "maps_directory";
    }

    private LuaCommandRegistration ReadLuaCommandRegistration(DynValue value)
    {
        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Lua command registration must be a table.");
        }

        var table = value.Table;
        var name = ReadRequiredStringField(table, "name", "Name");
        var description = ReadOptionalStringField(table, "description", "Description", "displayName", "DisplayName") ?? string.Empty;
        var usage = ReadOptionalStringField(table, "usage", "Usage") ?? name;
        var aliases = ReadStringListField(table, "aliases", "Aliases") ?? [];
        var requiredPermissions = ReadAdminPermissions(ReadField(
            table,
            "permission",
            "permissions",
            "Permission",
            "Permissions",
            "requiredPermission",
            "requiredPermissions"));
        var handler = ReadRequiredFunctionField(table, "handler", "Handler");

        return new LuaCommandRegistration(
            new LuaServerCommand(this, name, description, usage, handler),
            requiredPermissions,
            aliases);
    }

    private LuaDeferredServerAction ReadDeferredServerAction(CallbackArguments args)
    {
        var actionValue = ReadArgument(args, 0);
        string actionType;
        Table? actionTable;
        if (actionValue.Type == DataType.String)
        {
            actionType = actionValue.CastToString();
            actionTable = ReadOptionalTableArgument(args, 1);
        }
        else if (actionValue.Type == DataType.Table)
        {
            actionTable = actionValue.Table;
            actionType = ReadRequiredStringField(actionTable, "type", "Type", "action", "Action", "name", "Name");
        }
        else
        {
            throw new InvalidOperationException("Deferred server action must be a table or an action name plus a table.");
        }

        if (actionTable is null)
        {
            throw new InvalidOperationException($"Deferred server action \"{actionType}\" requires an argument table.");
        }

        return CreateDeferredServerAction(actionType, actionTable);
    }

    private LuaDeferredServerAction CreateDeferredServerAction(string actionType, Table table)
    {
        var normalizedActionType = NormalizeDeferredActionType(actionType);
        switch (normalizedActionType)
        {
            case "broadcastsystemmessage":
            {
                var text = ReadRequiredStringField(table, "text", "Text", "message", "Message");
                return new LuaDeferredServerAction(actionType, context =>
                {
                    context.AdminOperations.BroadcastSystemMessage(text);
                    return true;
                });
            }

            case "sendsystemmessage":
            {
                var slot = ReadRequiredByteField(table, "slot", "Slot");
                var text = ReadRequiredStringField(table, "text", "Text", "message", "Message");
                return new LuaDeferredServerAction(actionType, context =>
                {
                    context.AdminOperations.SendSystemMessage(slot, text);
                    return true;
                });
            }

            case "setplayername":
            case "trysetplayername":
            {
                var slot = ReadRequiredByteField(table, "slot", "Slot");
                var name = ReadRequiredStringField(table, "name", "Name", "newName", "NewName", "new_name");
                return new LuaDeferredServerAction(actionType, context => context.AdminOperations.TryRenamePlayer(slot, name));
            }

            case "setteam":
            case "trysetteam":
            {
                var slot = ReadRequiredByteField(table, "slot", "Slot");
                var team = ReadRequiredEnumField<PlayerTeam>(table, "team", "Team");
                return new LuaDeferredServerAction(actionType, context => context.AdminOperations.TrySetTeam(slot, team));
            }

            case "setclass":
            case "trysetclass":
            {
                var slot = ReadRequiredByteField(table, "slot", "Slot");
                var playerClass = ReadRequiredEnumField<PlayerClass>(table, "class", "Class", "playerClass", "PlayerClass", "player_class");
                return new LuaDeferredServerAction(actionType, context => context.AdminOperations.TrySetClass(slot, playerClass));
            }

            case "forcekill":
            case "tryforcekill":
            {
                var slot = ReadRequiredByteField(table, "slot", "Slot");
                return new LuaDeferredServerAction(actionType, context => context.AdminOperations.TryForceKill(slot));
            }

            case "changemap":
            case "trychangemap":
            {
                var levelName = ReadRequiredStringField(table, "levelName", "LevelName", "level", "Level", "map", "Map");
                var mapAreaIndex = ReadOptionalIntField(table, 1, "mapAreaIndex", "MapAreaIndex", "areaIndex", "AreaIndex", "area", "Area");
                var preservePlayerStats = ReadOptionalBoolField(table, false, "preservePlayerStats", "PreservePlayerStats", "preserve_stats");
                return new LuaDeferredServerAction(actionType, context => context.AdminOperations.TryChangeMap(levelName, mapAreaIndex, preservePlayerStats));
            }

            case "setcvar":
            {
                var name = ReadRequiredStringField(table, "name", "Name", "cvar", "Cvar");
                var value = ReadRequiredStringField(table, "value", "Value");
                var allowProtectedMutation = CanAccessProtectedCvars();
                return new LuaDeferredServerAction(actionType, context =>
                {
                    if (context.Cvars.TrySet(name, value, allowProtectedMutation, out var _, out var errorMessage))
                    {
                        return true;
                    }

                    context.Log($"[lua-plugin] deferred action \"{actionType}\" rejected for {manifest.Id} cvar {name}: {errorMessage}");
                    return false;
                });
            }

            default:
                throw new InvalidOperationException($"Unsupported deferred server action \"{actionType}\".");
        }
    }

    private static string NormalizeDeferredActionType(string value)
    {
        return new string(value
            .Where(static ch => ch != '_' && ch != '-' && !char.IsWhiteSpace(ch))
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private IReadOnlyList<string> ExecuteLuaCommandHandler(
        DynValue handler,
        OpenGarrisonServerCommandContext commandContext,
        string arguments)
    {
        if (_script is null)
        {
            return [$"[server] command failed: Lua plugin {manifest.Id} is not initialized."];
        }

        var previousIdentity = _activeCommandIdentity;
        _activeCommandIdentity = commandContext.Identity;
        try
        {
            return ExecuteInPhase(ServerLuaCallbackPhase.CommandInteraction, () =>
            {
                var commandContextTable = CreateCommandExecutionContext(commandContext, arguments);
                var result = InvokeCallbackWithLimits(handler, [commandContextTable, DynValue.NewString(arguments)]);
                return ReadLuaCommandResponseLines(result);
            });
        }
        finally
        {
            _activeCommandIdentity = previousIdentity;
        }
    }

    private DynValue CreateCommandExecutionContext(OpenGarrisonServerCommandContext context, string arguments)
    {
        if (_script is null)
        {
            return DynValue.Nil;
        }

        var table = new Table(_script)
        {
            ["identity"] = ToDynValue(context.Identity),
            ["is_authenticated_admin"] = context.Identity.IsAuthenticated,
            ["source"] = context.Source.ToString(),
            ["arguments"] = arguments,
            ["has_permission"] = DynValue.NewCallback((_, args) =>
                DynValue.NewBoolean(context.HasPermission(ReadAdminPermissions(ReadArgument(args, 0))))),
            ["require_permission"] = DynValue.NewCallback((_, args) =>
            {
                var requiredPermissions = ReadAdminPermissions(ReadArgument(args, 0));
                if (!context.HasPermission(requiredPermissions))
                {
                    throw new InvalidOperationException($"Command requires {requiredPermissions}.");
                }

                return DynValue.True;
            }),
        };

        return DynValue.NewTable(table);
    }

    private static IReadOnlyList<string> ReadLuaCommandResponseLines(DynValue result)
    {
        if (result.IsNil() || result.Type == DataType.Void)
        {
            return [];
        }

        if (result.Type == DataType.Table)
        {
            return result.Table.Pairs
                .Where(static pair => pair.Key.Type == DataType.Number)
                .OrderBy(static pair => pair.Key.Number)
                .Select(static pair => pair.Value.CastToString())
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
        }

        var text = result.CastToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static DynValue ReadRequiredFunctionField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        if (value.Type != DataType.Function)
        {
            throw new InvalidOperationException($"Field \"{names[0]}\" must be a function.");
        }

        return value;
    }

    private static OpenGarrisonServerAdminPermissions ReadAdminPermissions(DynValue value)
    {
        if (value.IsNil())
        {
            return OpenGarrisonServerAdminPermissions.None;
        }

        if (value.Type == DataType.Number)
        {
            return (OpenGarrisonServerAdminPermissions)(int)value.Number;
        }

        if (value.Type == DataType.String)
        {
            return ParseAdminPermissions(value.String);
        }

        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Permission must be a string, number, or table.");
        }

        var permissions = OpenGarrisonServerAdminPermissions.None;
        foreach (var pair in value.Table.Pairs)
        {
            if (pair.Key.Type == DataType.Number)
            {
                permissions |= ReadAdminPermissions(pair.Value);
                continue;
            }

            if (pair.Key.Type == DataType.String && ReadOptionalBoolValue(pair.Value, defaultValue: true))
            {
                permissions |= ParseAdminPermissions(pair.Key.String);
            }
        }

        return permissions;
    }

    private static OpenGarrisonServerAdminPermissions ParseAdminPermissions(string permissionText)
    {
        if (string.IsNullOrWhiteSpace(permissionText))
        {
            return OpenGarrisonServerAdminPermissions.None;
        }

        var permissions = OpenGarrisonServerAdminPermissions.None;
        foreach (var permissionPart in permissionText.Split([',', '|', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseAdminPermission(permissionPart, out var parsedPermission))
            {
                throw new InvalidOperationException($"Unknown server permission \"{permissionPart}\".");
            }

            permissions |= parsedPermission;
        }

        return permissions;
    }

    private static bool TryParseAdminPermission(string permissionText, out OpenGarrisonServerAdminPermissions permission)
    {
        var normalizedPermissionText = NormalizePermissionName(permissionText);
        foreach (var permissionName in Enum.GetNames<OpenGarrisonServerAdminPermissions>())
        {
            if (string.Equals(NormalizePermissionName(permissionName), normalizedPermissionText, StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse(permissionName, out permission))
            {
                return true;
            }
        }

        permission = OpenGarrisonServerAdminPermissions.None;
        return false;
    }

    private static string NormalizePermissionName(string permissionText)
    {
        return new string(permissionText
            .Where(static ch => ch != '_' && ch != '-' && !char.IsWhiteSpace(ch))
            .ToArray());
    }

    private static bool ReadOptionalBoolValue(DynValue value, bool defaultValue)
    {
        if (value.IsNil())
        {
            return defaultValue;
        }

        return value.Type switch
        {
            DataType.Boolean => value.Boolean,
            DataType.Number => Math.Abs(value.Number) > double.Epsilon,
            _ => bool.Parse(value.CastToString()),
        };
    }

    private static (string BehaviorId, string? FireSoundName) ReadGameplayPrimaryWeaponBehaviorRegistration(CallbackArguments args)
    {
        var value = ReadArgument(args, 0);
        if (value.Type == DataType.Table)
        {
            var table = value.Table;
            return (
                ReadRequiredStringField(table, "behaviorId", "BehaviorId", "behavior_id"),
                ReadOptionalStringField(table, "fireSoundName", "FireSoundName", "fire_sound_name"));
        }

        return (ReadStringArgument(args, 0), ReadOptionalStringArgument(args, 1));
    }

    private static GameplayWeaponItemRegistration ReadGameplayWeaponItemRegistration(DynValue value)
    {
        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Gameplay weapon item registration must be a Lua table.");
        }

        var table = value.Table;
        return new GameplayWeaponItemRegistration(
            ReadRequiredStringField(table, "itemId", "ItemId", "item_id"),
            ReadRequiredStringField(table, "displayName", "DisplayName", "display_name"),
            ReadGameplayEquipmentSlotField(table),
            ReadRequiredStringField(table, "behaviorId", "BehaviorId", "behavior_id"),
            ReadOptionalGameplayItemAmmoDefinition(table) ?? new GameplayItemAmmoDefinition(),
            ReadOptionalStringField(table, "modPackId", "ModPackId", "mod_pack_id"),
            ReadOptionalGameplayItemPresentationDefinition(table),
            ReadOptionalGameplayItemCombatDefinition(table),
            ReadOptionalGameplayItemOwnershipDefinition(table),
            ReadOptionalGameplayItemDescriptionDefinition(table));
    }

    private static GameplayAbilityRegistration ReadGameplayAbilityRegistration(DynValue value)
    {
        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Gameplay ability registration must be a Lua table.");
        }

        var table = value.Table;
        var abilityTable = ReadOptionalTableField(table, "ability", "Ability") ?? table;
        var behaviorId = ReadRequiredStringField(table, "behaviorId", "BehaviorId", "behavior_id");
        return new GameplayAbilityRegistration(
            ReadRequiredStringField(table, "itemId", "ItemId", "item_id"),
            ReadRequiredStringField(table, "displayName", "DisplayName", "display_name"),
            ReadGameplayEquipmentSlotField(table),
            behaviorId,
            new GameplayAbilityDefinition(
                Category: ReadRequiredStringField(abilityTable, "category", "Category"),
                Activation: ReadRequiredStringField(abilityTable, "activation", "Activation"),
                ExecutorId: ReadOptionalStringField(abilityTable, "executorId", "ExecutorId", "executor_id") ?? behaviorId,
                Tags: ReadStringListField(abilityTable, "tags", "Tags") ?? [],
                Parameters: ReadJsonElementDictionaryField(abilityTable, "parameters", "Parameters") ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            ReadOptionalStringField(table, "modPackId", "ModPackId", "mod_pack_id"),
            ReadOptionalGameplayItemPresentationDefinition(table));
    }

    private static GameplayAbilityPatch ReadGameplayAbilityPatch(DynValue value)
    {
        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Gameplay ability patch must be a Lua table.");
        }

        var table = value.Table;
        return new GameplayAbilityPatch(
            ReadOptionalStringField(table, "category", "Category"),
            ReadOptionalStringField(table, "activation", "Activation"),
            ReadOptionalStringField(table, "executorId", "ExecutorId", "executor_id"),
            ReadStringListField(table, "tags", "Tags"),
            ReadJsonElementDictionaryField(table, "parameters", "Parameters"));
    }

    private static GameplayLoadoutRegistration ReadGameplayLoadoutRegistration(DynValue value)
    {
        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Gameplay loadout registration must be a Lua table.");
        }

        var table = value.Table;
        return new GameplayLoadoutRegistration(
            ReadRequiredStringField(table, "classId", "ClassId", "class_id"),
            ReadRequiredStringField(table, "loadoutId", "LoadoutId", "loadout_id"),
            ReadRequiredStringField(table, "displayName", "DisplayName", "display_name"),
            ReadRequiredStringField(table, "primaryItemId", "PrimaryItemId", "primary_item_id"),
            ReadOptionalStringField(table, "secondaryItemId", "SecondaryItemId", "secondary_item_id"),
            ReadOptionalStringField(table, "utilityItemId", "UtilityItemId", "utility_item_id"),
            ReadStringListField(table, "abilityItemIds", "AbilityItemIds", "ability_item_ids"),
            ReadOptionalStringField(table, "modPackId", "ModPackId", "mod_pack_id"));
    }

    private static GameplaySlotItemRegistration ReadGameplaySlotItemRegistration(DynValue value)
    {
        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Gameplay slot item registration must be a Lua table.");
        }

        var table = value.Table;
        return new GameplaySlotItemRegistration(
            ReadRequiredStringField(table, "classId", "ClassId", "class_id"),
            ReadGameplayEquipmentSlotField(table),
            ReadRequiredStringField(table, "itemId", "ItemId", "item_id"),
            ReadOptionalStringField(table, "loadoutId", "LoadoutId", "loadout_id"),
            ReadOptionalStringField(table, "displayName", "DisplayName", "display_name"),
            ReadOptionalStringField(table, "baseLoadoutId", "BaseLoadoutId", "base_loadout_id"),
            ReadOptionalStringField(table, "modPackId", "ModPackId", "mod_pack_id"));
    }

    private static GameplayProjectileSpawnRequest ReadGameplayProjectileSpawnRequest(DynValue value)
    {
        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException("Gameplay projectile spawn request must be a Lua table.");
        }

        var table = value.Table;
        return new GameplayProjectileSpawnRequest(
            ReadRequiredIntField(table, "ownerPlayerId", "OwnerPlayerId", "owner_player_id"),
            ReadRequiredStringField(table, "kind", "Kind"),
            ReadOptionalFloatField(table, 0f, "x", "X"),
            ReadOptionalFloatField(table, 0f, "y", "Y"),
            ReadOptionalFloatField(table, 0f, "velocityX", "VelocityX", "velocity_x"),
            ReadOptionalFloatField(table, 0f, "velocityY", "VelocityY", "velocity_y"),
            ReadOptionalFloatField(table, 0f, "speed", "Speed"),
            ReadOptionalFloatField(table, 0f, "directionRadians", "DirectionRadians", "direction_radians"),
            ReadOptionalFloatField(table, 0f, "damage", "Damage"),
            ReadOptionalStringField(table, "killFeedWeaponSpriteName", "KillFeedWeaponSpriteName", "kill_feed_weapon_sprite_name"));
    }

    private static GameplayItemPresentationDefinition? ReadOptionalGameplayItemPresentationDefinition(Table table)
    {
        var presentationTable = ReadOptionalTableField(table, "presentation", "Presentation");
        if (presentationTable is null)
        {
            return null;
        }

        return new GameplayItemPresentationDefinition(
            WorldSpriteName: ReadOptionalStringField(presentationTable, "worldSpriteName", "WorldSpriteName", "world_sprite_name"),
            RecoilSpriteName: ReadOptionalStringField(presentationTable, "recoilSpriteName", "RecoilSpriteName", "recoil_sprite_name"),
            ReloadSpriteName: ReadOptionalStringField(presentationTable, "reloadSpriteName", "ReloadSpriteName", "reload_sprite_name"),
            RecoilCarrierSpriteName: ReadOptionalStringField(presentationTable, "recoilCarrierSpriteName", "RecoilCarrierSpriteName", "recoil_carrier_sprite_name"),
            RecoilOverlaySpriteName: ReadOptionalStringField(presentationTable, "recoilOverlaySpriteName", "RecoilOverlaySpriteName", "recoil_overlay_sprite_name"),
            RecoilOverlayOffsetX: ReadOptionalFloatField(presentationTable, 0f, "recoilOverlayOffsetX", "RecoilOverlayOffsetX", "recoil_overlay_offset_x"),
            RecoilOverlayOffsetY: ReadOptionalFloatField(presentationTable, 0f, "recoilOverlayOffsetY", "RecoilOverlayOffsetY", "recoil_overlay_offset_y"),
            RecoilOverlayRotationDegrees: ReadOptionalFloatField(presentationTable, 0f, "recoilOverlayRotationDegrees", "RecoilOverlayRotationDegrees", "recoil_overlay_rotation_degrees"),
            ReloadCarrierSpriteName: ReadOptionalStringField(presentationTable, "reloadCarrierSpriteName", "ReloadCarrierSpriteName", "reload_carrier_sprite_name"),
            ReloadOverlaySpriteName: ReadOptionalStringField(presentationTable, "reloadOverlaySpriteName", "ReloadOverlaySpriteName", "reload_overlay_sprite_name"),
            ReloadOverlayOffsetX: ReadOptionalFloatField(presentationTable, 0f, "reloadOverlayOffsetX", "ReloadOverlayOffsetX", "reload_overlay_offset_x"),
            ReloadOverlayOffsetY: ReadOptionalFloatField(presentationTable, 0f, "reloadOverlayOffsetY", "ReloadOverlayOffsetY", "reload_overlay_offset_y"),
            ReloadOverlayRotationDegrees: ReadOptionalFloatField(presentationTable, 0f, "reloadOverlayRotationDegrees", "ReloadOverlayRotationDegrees", "reload_overlay_rotation_degrees"),
            HudSpriteName: ReadOptionalStringField(presentationTable, "hudSpriteName", "HudSpriteName", "hud_sprite_name"),
            WeaponOffsetX: ReadOptionalFloatField(presentationTable, 0f, "weaponOffsetX", "WeaponOffsetX", "weapon_offset_x"),
            WeaponOffsetY: ReadOptionalFloatField(presentationTable, 0f, "weaponOffsetY", "WeaponOffsetY", "weapon_offset_y"),
            RecoilDurationSourceTicks: ReadOptionalIntField(presentationTable, 0, "recoilDurationSourceTicks", "RecoilDurationSourceTicks", "recoil_duration_source_ticks"),
            ReloadDurationSourceTicks: ReadOptionalIntField(presentationTable, 0, "reloadDurationSourceTicks", "ReloadDurationSourceTicks", "reload_duration_source_ticks"),
            ScopedRecoilDurationSourceTicks: ReadOptionalIntField(presentationTable, 0, "scopedRecoilDurationSourceTicks", "ScopedRecoilDurationSourceTicks", "scoped_recoil_duration_source_ticks"),
            LoopRecoilWhileActive: ReadOptionalBoolField(presentationTable, false, "loopRecoilWhileActive", "LoopRecoilWhileActive", "loop_recoil_while_active"),
            LoopReloadAnimation: ReadOptionalBoolField(presentationTable, true, "loopReloadAnimation", "LoopReloadAnimation", "loop_reload_animation"),
            BlueTeamHudFrameOffset: ReadOptionalIntField(presentationTable, 1, "blueTeamHudFrameOffset", "BlueTeamHudFrameOffset", "blue_team_hud_frame_offset"),
            UseAmmoCountForHudFrame: ReadOptionalBoolField(presentationTable, false, "useAmmoCountForHudFrame", "UseAmmoCountForHudFrame", "use_ammo_count_for_hud_frame"),
            BlueTeamAmmoHudFrameOffset: ReadOptionalIntField(presentationTable, 0, "blueTeamAmmoHudFrameOffset", "BlueTeamAmmoHudFrameOffset", "blue_team_ammo_hud_frame_offset"),
            Hud: ReadOptionalGameplayItemHudPresentationDefinition(presentationTable));
    }

    private static GameplayItemAmmoDefinition? ReadOptionalGameplayItemAmmoDefinition(Table table)
    {
        var ammoTable = ReadOptionalTableField(table, "ammo", "Ammo");
        if (ammoTable is null)
        {
            return null;
        }

        return new GameplayItemAmmoDefinition(
            MaxAmmo: ReadOptionalIntField(ammoTable, 0, "maxAmmo", "MaxAmmo", "max_ammo"),
            AmmoPerUse: ReadOptionalIntField(ammoTable, 0, "ammoPerUse", "AmmoPerUse", "ammo_per_use", "ammoPerShot", "AmmoPerShot", "ammo_per_shot"),
            ProjectilesPerUse: ReadOptionalIntField(ammoTable, 0, "projectilesPerUse", "ProjectilesPerUse", "projectiles_per_use", "projectilesPerShot", "ProjectilesPerShot", "projectiles_per_shot"),
            UseDelaySourceTicks: ReadOptionalIntField(ammoTable, 0, "useDelaySourceTicks", "UseDelaySourceTicks", "use_delay_source_ticks", "reloadDelayTicks", "ReloadDelayTicks", "reload_delay_ticks"),
            ReloadSourceTicks: ReadOptionalIntField(ammoTable, 0, "reloadSourceTicks", "ReloadSourceTicks", "reload_source_ticks", "ammoReloadTicks", "AmmoReloadTicks", "ammo_reload_ticks"),
            SpreadDegrees: ReadOptionalFloatField(ammoTable, 0f, "spreadDegrees", "SpreadDegrees", "spread_degrees"),
            MinProjectileSpeed: ReadOptionalFloatField(ammoTable, 0f, "minProjectileSpeed", "MinProjectileSpeed", "min_projectile_speed", "minShotSpeed", "MinShotSpeed", "min_shot_speed"),
            AdditionalProjectileSpeed: ReadOptionalFloatField(ammoTable, 0f, "additionalProjectileSpeed", "AdditionalProjectileSpeed", "additional_projectile_speed", "additionalRandomShotSpeed", "AdditionalRandomShotSpeed", "additional_random_shot_speed"),
            AutoReloads: ReadOptionalBoolField(ammoTable, true, "autoReloads", "AutoReloads", "auto_reloads"),
            AmmoRegenPerTick: ReadOptionalIntField(ammoTable, 0, "ammoRegenPerTick", "AmmoRegenPerTick", "ammo_regen_per_tick"),
            RefillsAllAtOnce: ReadOptionalBoolField(ammoTable, false, "refillsAllAtOnce", "RefillsAllAtOnce", "refills_all_at_once"));
    }

    private static GameplayItemCombatDefinition? ReadOptionalGameplayItemCombatDefinition(Table table)
    {
        var combatTable = ReadOptionalTableField(table, "combat", "Combat");
        if (combatTable is null)
        {
            return null;
        }

        return new GameplayItemCombatDefinition(
            FireSoundName: ReadOptionalStringField(combatTable, "fireSoundName", "FireSoundName", "fire_sound_name"),
            DirectHitDamage: ReadOptionalFloatField(combatTable, null, "directHitDamage", "DirectHitDamage", "direct_hit_damage"),
            DamagePerTick: ReadOptionalFloatField(combatTable, null, "damagePerTick", "DamagePerTick", "damage_per_tick"),
            DirectHitHealAmount: ReadOptionalFloatField(combatTable, null, "directHitHealAmount", "DirectHitHealAmount", "direct_hit_heal_amount"),
            ActiveProjectileLimit: ReadOptionalIntField(combatTable, null, "activeProjectileLimit", "ActiveProjectileLimit", "active_projectile_limit"),
            Rocket: ReadOptionalGameplayRocketCombatDefinition(combatTable));
    }

    private static GameplayRocketCombatDefinition? ReadOptionalGameplayRocketCombatDefinition(Table combatTable)
    {
        var rocketTable = ReadOptionalTableField(combatTable, "rocket", "Rocket");
        if (rocketTable is null)
        {
            return null;
        }

        return new GameplayRocketCombatDefinition(
            DirectHitDamage: ReadOptionalIntField(rocketTable, 25, "directHitDamage", "DirectHitDamage", "direct_hit_damage"),
            ExplosionDamage: ReadOptionalFloatField(rocketTable, 30f, "explosionDamage", "ExplosionDamage", "explosion_damage"),
            BlastRadius: ReadOptionalFloatField(rocketTable, 65f, "blastRadius", "BlastRadius", "blast_radius"),
            SplashThresholdFactor: ReadOptionalFloatField(rocketTable, 0.25f, "splashThresholdFactor", "SplashThresholdFactor", "splash_threshold_factor"));
    }

    private static GameplayItemOwnershipDefinition? ReadOptionalGameplayItemOwnershipDefinition(Table table)
    {
        var ownershipTable = ReadOptionalTableField(table, "ownership", "Ownership");
        if (ownershipTable is null)
        {
            return null;
        }

        return new GameplayItemOwnershipDefinition(
            TrackOwnership: ReadOptionalBoolField(ownershipTable, false, "trackOwnership", "TrackOwnership", "track_ownership"),
            DefaultGranted: ReadOptionalBoolField(ownershipTable, true, "defaultGranted", "DefaultGranted", "default_granted"),
            GrantOnAcquire: ReadOptionalBoolField(ownershipTable, false, "grantOnAcquire", "GrantOnAcquire", "grant_on_acquire"),
            GrantKey: ReadOptionalStringField(ownershipTable, "grantKey", "GrantKey", "grant_key"));
    }

    private static GameplayItemDescriptionDefinition? ReadOptionalGameplayItemDescriptionDefinition(Table table)
    {
        var descriptionTable = ReadOptionalTableField(table, "description", "Description");
        if (descriptionTable is null)
        {
            return null;
        }

        return new GameplayItemDescriptionDefinition(
            Summary: ReadOptionalStringField(descriptionTable, "summary", "Summary"),
            PositiveAttributes: ReadStringListField(descriptionTable, "positiveAttributes", "PositiveAttributes", "positive_attributes"),
            NegativeAttributes: ReadStringListField(descriptionTable, "negativeAttributes", "NegativeAttributes", "negative_attributes"),
            Notes: ReadStringListField(descriptionTable, "notes", "Notes"));
    }

    private static GameplayItemHudPresentationDefinition? ReadOptionalGameplayItemHudPresentationDefinition(Table presentationTable)
    {
        var hudTable = ReadOptionalTableField(presentationTable, "hud", "Hud");
        if (hudTable is null)
        {
            return null;
        }

        return new GameplayItemHudPresentationDefinition(
            DisplayKind: ReadOptionalStringField(hudTable, "displayKind", "DisplayKind", "display_kind") ?? string.Empty,
            StackGroup: ReadOptionalStringField(hudTable, "stackGroup", "StackGroup", "stack_group") ?? string.Empty,
            Order: ReadOptionalIntField(hudTable, 0, "order", "Order"),
            StateProvider: ReadOptionalStringField(hudTable, "stateProvider", "StateProvider", "state_provider") ?? string.Empty,
            HideWhenUnavailable: ReadOptionalBoolField(hudTable, false, "hideWhenUnavailable", "HideWhenUnavailable", "hide_when_unavailable"),
            ShowWhenEquippedOnly: ReadOptionalBoolField(hudTable, false, "showWhenEquippedOnly", "ShowWhenEquippedOnly", "show_when_equipped_only"),
            StateOwner: ReadOptionalStringField(hudTable, "stateOwner", "StateOwner", "state_owner") ?? string.Empty,
            CooldownKey: ReadOptionalStringField(hudTable, "cooldownKey", "CooldownKey", "cooldown_key") ?? string.Empty,
            MaxCooldown: ReadOptionalIntField(hudTable, 0, "maxCooldown", "MaxCooldown", "max_cooldown"),
            ActiveKey: ReadOptionalStringField(hudTable, "activeKey", "ActiveKey", "active_key") ?? string.Empty,
            DisabledKey: ReadOptionalStringField(hudTable, "disabledKey", "DisabledKey", "disabled_key") ?? string.Empty,
            WidgetId: ReadOptionalStringField(hudTable, "widgetId", "WidgetId", "widget_id") ?? string.Empty,
            WidgetOwner: ReadOptionalStringField(hudTable, "widgetOwner", "WidgetOwner", "widget_owner", "widgetPluginId", "WidgetPluginId", "widget_plugin_id") ?? string.Empty,
            WidgetCallback: ReadOptionalStringField(hudTable, "widgetCallback", "WidgetCallback", "widget_callback", "drawCallback", "DrawCallback", "draw_callback") ?? string.Empty,
            Anchor: ReadOptionalStringField(hudTable, "anchor", "Anchor") ?? string.Empty);
    }

    private static GameplayEquipmentSlot ReadGameplayEquipmentSlotField(Table table)
    {
        var value = ReadField(table, "slot", "Slot");
        if (value.Type == DataType.Number)
        {
            var slot = (GameplayEquipmentSlot)(int)value.Number;
            if (Enum.IsDefined(slot))
            {
                return slot;
            }
        }

        if (Enum.TryParse<GameplayEquipmentSlot>(value.CastToString(), ignoreCase: true, out var parsedSlot))
        {
            return parsedSlot;
        }

        throw new InvalidOperationException($"Unknown gameplay equipment slot \"{value}\".");
    }

    private static string ReadRequiredStringField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        if (value.IsNil())
        {
            throw new InvalidOperationException($"Missing required field \"{names[0]}\".");
        }

        return value.CastToString();
    }

    private static int ReadRequiredIntField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        if (value.IsNil())
        {
            throw new InvalidOperationException($"Missing required field \"{names[0]}\".");
        }

        return (int)value.Number;
    }

    private static byte ReadRequiredByteField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        if (value.IsNil())
        {
            throw new InvalidOperationException($"Missing required field \"{names[0]}\".");
        }

        return ReadByteValue(value);
    }

    private static TEnum ReadRequiredEnumField<TEnum>(Table table, params string[] names) where TEnum : struct, Enum
    {
        var value = ReadField(table, names);
        if (value.IsNil())
        {
            throw new InvalidOperationException($"Missing required field \"{names[0]}\".");
        }

        if (TryParseEnumValue(value, out TEnum enumValue))
        {
            return enumValue;
        }

        throw new InvalidOperationException($"Invalid {typeof(TEnum).Name} value \"{value.CastToString()}\".");
    }

    private static int ReadOptionalIntField(Table table, int defaultValue, params string[] names)
    {
        var value = ReadField(table, names);
        return value.IsNil() ? defaultValue : (int)value.Number;
    }

    private static int? ReadOptionalIntField(Table table, int? defaultValue, params string[] names)
    {
        var value = ReadField(table, names);
        return value.IsNil() ? defaultValue : (int)value.Number;
    }

    private static float ReadOptionalFloatField(Table table, float defaultValue, params string[] names)
    {
        var value = ReadField(table, names);
        return value.IsNil() ? defaultValue : (float)value.Number;
    }

    private static float? ReadOptionalFloatField(Table table, float? defaultValue, params string[] names)
    {
        var value = ReadField(table, names);
        return value.IsNil() ? defaultValue : (float)value.Number;
    }

    private static string? ReadOptionalStringField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        if (value.IsNil())
        {
            return null;
        }

        var text = value.CastToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static Table? ReadOptionalTableField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        return value.Type == DataType.Table ? value.Table : null;
    }

    private static TEnum? ReadOptionalEnumField<TEnum>(Table table, params string[] names) where TEnum : struct, Enum
    {
        var value = ReadField(table, names);
        return TryParseEnumValue(value, out TEnum parsed) ? parsed : null;
    }

    private static DynValue ReadField(Table table, params string[] names)
    {
        for (var index = 0; index < names.Length; index += 1)
        {
            var value = table.Get(names[index]);
            if (!value.IsNil())
            {
                return value;
            }
        }

        return DynValue.Nil;
    }

    private static IReadOnlyList<string>? ReadStringListField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        if (value.IsNil())
        {
            return null;
        }

        if (value.Type == DataType.String)
        {
            return value.String
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static tag => tag.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException($"Field \"{names[0]}\" must be a string or table.");
        }

        return value.Table.Pairs
            .Where(static pair => pair.Key.Type == DataType.Number)
            .OrderBy(static pair => pair.Key.Number)
            .Select(static pair => pair.Value.CastToString())
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, JsonElement>? ReadJsonElementDictionaryField(Table table, params string[] names)
    {
        var value = ReadField(table, names);
        if (value.IsNil())
        {
            return null;
        }

        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException($"Field \"{names[0]}\" must be a table.");
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in value.Table.Pairs)
        {
            if (pair.Key.Type != DataType.String)
            {
                continue;
            }

            result[pair.Key.String] = JsonSerializer.SerializeToElement(DynValueToPlainObject(pair.Value), JsonOptions);
        }

        return result;
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

    private bool TryGetCachedCallbackFunction(string callbackName, out DynValue function)
    {
        function = DynValue.Nil;
        if (_pluginTable is null)
        {
            return false;
        }

        if (_callbackCache.TryGetValue(callbackName, out var cachedFunction)
            && cachedFunction is not null)
        {
            function = cachedFunction;
        }
        else
        {
            function = _pluginTable.Get(callbackName);
            _callbackCache[callbackName] = function;
        }

        return function.Type is DataType.Function or DataType.ClrFunction;
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
        return OpenGarrisonPluginPathContainment.ResolveContainedPath(
            configDirectory,
            normalizedRelativePath,
            "Plugin config path escapes config directory.");
    }

    private DynValue CreateCommandInteractionContext(OpenGarrisonServerChatMessageContext context)
    {
        return ToDynValue(new
        {
            context.Identity,
            context.IsAuthenticatedAdmin,
        });
    }

    private static object CreateLuaTargetResolution(ServerAdminTargetResolution resolution)
    {
        return new
        {
            resolution.Success,
            resolution.Query,
            resolution.MatchKind,
            resolution.ErrorCode,
            resolution.ErrorMessage,
            Targets = resolution.Targets.Select(CreateLuaTargetInfo).ToArray(),
        };
    }

    private static object CreateLuaTargetInfo(OpenGarrisonServerPlayerInfo player)
    {
        return new
        {
            player.Slot,
            player.UserId,
            player.Name,
            player.IsSpectator,
            player.IsAuthorized,
            player.IsGagged,
            player.IsAlive,
            player.PlayerId,
            player.Team,
            player.PlayerClass,
            player.PlayerScale,
            player.EndPoint,
            player.MovementSpeedScale,
            player.HasMovementSpeedScaleOverride,
            player.GravityScale,
            player.HasGravityScaleOverride,
        };
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

    private bool CanEnqueueServerAction(string functionName, string target)
    {
        if (IsDeferredServerActionPhaseAllowed(_currentCallbackPhase))
        {
            return true;
        }

        return RejectHostOperation(
            functionName,
            target,
            "Enqueue deferred server actions during initialize, lifecycle, update, event, or command callbacks.");
    }

    private bool EnqueueDeferredServerAction(LuaDeferredServerAction action)
    {
        if (_deferredServerActions.Count >= MaxDeferredServerActions)
        {
            _context?.Log($"[lua-plugin] enqueue_action rejected for {manifest.Id}: deferred server action queue is full.");
            return false;
        }

        _deferredServerActions.Enqueue(action);
        return true;
    }

    private void DrainDeferredServerActions()
    {
        if (_context is null)
        {
            _deferredServerActions.Clear();
            return;
        }

        while (_deferredServerActions.TryDequeue(out var action))
        {
            try
            {
                if (!action.Execute(_context))
                {
                    _context.Log($"[lua-plugin] deferred action \"{action.ActionType}\" returned false for {manifest.Id}.");
                }
            }
            catch (Exception ex)
            {
                _context.Log($"[lua-plugin] deferred action \"{action.ActionType}\" failed for {manifest.Id}: {ex.Message}");
            }
        }
    }

    private bool CanRegisterGameplayAbility(string functionName, string target)
    {
        if (_currentCallbackPhase == ServerLuaCallbackPhase.Initialize)
        {
            return true;
        }

        return RejectHostOperation(
            functionName,
            target,
            "Register or override gameplay abilities during initialize before the ability registry is sealed.");
    }

    private bool CanRegisterLuaCommand(string functionName, string target)
    {
        if (_currentCallbackPhase == ServerLuaCallbackPhase.Initialize)
        {
            return true;
        }

        return RejectHostOperation(
            functionName,
            target,
            "Register Lua commands during initialize before the command registry is exposed to players.");
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
            // Admin/chat command callbacks are human-driven and may need to emit
            // multiple status lines or perform bounded catalog/search work.
            ServerLuaCallbackPhase.CommandInteraction => TimeSpan.FromSeconds(5),
            ServerLuaCallbackPhase.AbilityExecution => TimeSpan.FromMilliseconds(5),
            ServerLuaCallbackPhase.PrimaryWeaponExecution => TimeSpan.FromMilliseconds(5),
            ServerLuaCallbackPhase.Event => TimeSpan.FromMilliseconds(10),
            _ => TimeSpan.FromMilliseconds(10),
        };
    }

    private static int GetMaxCallbackResumeCount(ServerLuaCallbackPhase phase)
    {
        return phase switch
        {
            ServerLuaCallbackPhase.Initialize => MaxInitializeResumeCount,
            ServerLuaCallbackPhase.CommandInteraction => MaxInitializeResumeCount,
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
            ServerLuaCallbackPhase.AbilityExecution => "an ability execution callback",
            ServerLuaCallbackPhase.PrimaryWeaponExecution => "a primary weapon execution callback",
            ServerLuaCallbackPhase.Event => "a server event callback",
            _ => "an unknown callback",
        };
    }

    private static bool IsPluginStoragePhaseAllowed(ServerLuaCallbackPhase phase)
    {
        return phase is ServerLuaCallbackPhase.Initialize
            or ServerLuaCallbackPhase.Lifecycle
            or ServerLuaCallbackPhase.Update
            or ServerLuaCallbackPhase.AbilityExecution
            or ServerLuaCallbackPhase.PrimaryWeaponExecution
            or ServerLuaCallbackPhase.Event
            or ServerLuaCallbackPhase.CommandInteraction;
    }

    private static bool IsServerMutationPhaseAllowed(ServerLuaCallbackPhase phase)
    {
        return phase is ServerLuaCallbackPhase.Lifecycle
            or ServerLuaCallbackPhase.Update
            or ServerLuaCallbackPhase.AbilityExecution
            or ServerLuaCallbackPhase.PrimaryWeaponExecution
            or ServerLuaCallbackPhase.Event
            or ServerLuaCallbackPhase.CommandInteraction;
    }

    private static bool IsSchedulerPhaseAllowed(ServerLuaCallbackPhase phase)
    {
        return phase is ServerLuaCallbackPhase.Initialize
            or ServerLuaCallbackPhase.Lifecycle
            or ServerLuaCallbackPhase.Update
            or ServerLuaCallbackPhase.AbilityExecution
            or ServerLuaCallbackPhase.PrimaryWeaponExecution
            or ServerLuaCallbackPhase.Event
            or ServerLuaCallbackPhase.CommandInteraction;
    }

    private static bool IsDeferredServerActionPhaseAllowed(ServerLuaCallbackPhase phase)
    {
        return phase is ServerLuaCallbackPhase.Initialize
            or ServerLuaCallbackPhase.Lifecycle
            or ServerLuaCallbackPhase.Update
            or ServerLuaCallbackPhase.AbilityExecution
            or ServerLuaCallbackPhase.PrimaryWeaponExecution
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

    private static bool TryResolvePlayerStateQuery(
        IOpenGarrisonServerReadOnlyState state,
        DynValue query,
        out OpenGarrisonServerPlayerInfo player)
    {
        player = default;
        if (query.IsNil())
        {
            return false;
        }

        if (query.Type != DataType.Table)
        {
            return state.TryGetPlayerStateBySlot(ReadByteValue(query), out player);
        }

        var table = query.Table;
        var slot = ReadField(table, "slot", "Slot");
        if (!slot.IsNil())
        {
            return state.TryGetPlayerStateBySlot(ReadByteValue(slot), out player);
        }

        var playerId = ReadField(table, "playerId", "PlayerId", "player_id");
        if (!playerId.IsNil())
        {
            return state.TryGetPlayerStateByPlayerId(ReadIntValue(playerId), out player);
        }

        var queryKind = ReadOptionalStringField(table, "by", "kind");
        var queryValue = ReadField(table, "value", "id");
        if (queryKind is null || queryValue.IsNil())
        {
            return false;
        }

        return NormalizePlayerStateQueryKind(queryKind) switch
        {
            "slot" => state.TryGetPlayerStateBySlot(ReadByteValue(queryValue), out player),
            "playerid" => state.TryGetPlayerStateByPlayerId(ReadIntValue(queryValue), out player),
            _ => false,
        };
    }

    private static string NormalizePlayerStateQueryKind(string value)
    {
        return new string(value
            .Where(static ch => ch != '_' && ch != '-' && !char.IsWhiteSpace(ch))
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static Table CreateServerStateTable(Script script, IOpenGarrisonServerReadOnlyState state)
    {
        var table = new Table(script);
        SetNamedValue(table, nameof(state.ServerName), DynValue.NewString(state.ServerName));
        SetNamedValue(table, nameof(state.LevelName), DynValue.NewString(state.LevelName));
        SetNamedValue(table, nameof(state.MapAreaIndex), DynValue.NewNumber(state.MapAreaIndex));
        SetNamedValue(table, nameof(state.MapAreaCount), DynValue.NewNumber(state.MapAreaCount));
        SetNamedValue(table, nameof(state.MapScale), DynValue.NewNumber(state.MapScale));
        SetNamedValue(table, "GameMode", DynValue.NewString(state.GameMode.ToString()));
        SetNamedValue(table, "MatchPhase", DynValue.NewString(state.MatchPhase.ToString()));
        SetNamedValue(table, nameof(state.RedCaps), DynValue.NewNumber(state.RedCaps));
        SetNamedValue(table, nameof(state.BlueCaps), DynValue.NewNumber(state.BlueCaps));
        return table;
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

    private static Table CreatePlayerListTable(Script script, IReadOnlyList<OpenGarrisonServerPlayerInfo> players)
    {
        var table = new Table(script);
        for (var index = 0; index < players.Count; index += 1)
        {
            table[index + 1] = DynValue.NewTable(CreatePlayerInfoTable(script, players[index]));
        }

        return table;
    }

    private static Table CreatePlayerInfoTable(Script script, OpenGarrisonServerPlayerInfo player)
    {
        var table = new Table(script);
        SetNamedValue(table, nameof(player.Slot), DynValue.NewNumber(player.Slot));
        SetNamedValue(table, nameof(player.UserId), DynValue.NewNumber(player.UserId));
        SetNamedValue(table, nameof(player.Name), DynValue.NewString(player.Name));
        SetNamedValue(table, nameof(player.IsSpectator), DynValue.NewBoolean(player.IsSpectator));
        SetNamedValue(table, nameof(player.IsAuthorized), DynValue.NewBoolean(player.IsAuthorized));
        SetNamedValue(table, nameof(player.IsGagged), DynValue.NewBoolean(player.IsGagged));
        SetNamedValue(table, nameof(player.IsAlive), DynValue.NewBoolean(player.IsAlive));
        SetNamedValue(table, nameof(player.PlayerId), player.PlayerId.HasValue ? DynValue.NewNumber(player.PlayerId.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.Team), player.Team.HasValue ? DynValue.NewString(player.Team.Value.ToString()) : DynValue.Nil);
        SetNamedValue(table, nameof(player.PlayerClass), player.PlayerClass.HasValue ? DynValue.NewString(player.PlayerClass.Value.ToString()) : DynValue.Nil);
        SetNamedValue(table, nameof(player.PlayerScale), DynValue.NewNumber(player.PlayerScale));
        SetNamedValue(table, nameof(player.MovementSpeedScale), DynValue.NewNumber(player.MovementSpeedScale));
        SetNamedValue(table, nameof(player.HasMovementSpeedScaleOverride), DynValue.NewBoolean(player.HasMovementSpeedScaleOverride));
        SetNamedValue(table, nameof(player.GravityScale), DynValue.NewNumber(player.GravityScale));
        SetNamedValue(table, nameof(player.HasGravityScaleOverride), DynValue.NewBoolean(player.HasGravityScaleOverride));
        SetNamedValue(table, nameof(player.EndPoint), DynValue.NewString(player.EndPoint));
        SetNamedValue(table, nameof(player.GameplayLoadoutId), DynValue.NewString(player.GameplayLoadoutId));
        SetNamedValue(table, nameof(player.GameplaySecondaryItemId), DynValue.NewString(player.GameplaySecondaryItemId));
        SetNamedValue(table, nameof(player.GameplayAcquiredItemId), DynValue.NewString(player.GameplayAcquiredItemId));
        SetNamedValue(table, nameof(player.GameplayEquippedSlot), DynValue.NewString(player.GameplayEquippedSlot.ToString()));
        SetNamedValue(table, nameof(player.GameplayEquippedItemId), DynValue.NewString(player.GameplayEquippedItemId));
        SetNamedValue(table, nameof(player.WorldX), player.WorldX.HasValue ? DynValue.NewNumber(player.WorldX.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.WorldY), player.WorldY.HasValue ? DynValue.NewNumber(player.WorldY.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.HorizontalSpeed), player.HorizontalSpeed.HasValue ? DynValue.NewNumber(player.HorizontalSpeed.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.VerticalSpeed), player.VerticalSpeed.HasValue ? DynValue.NewNumber(player.VerticalSpeed.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.Health), player.Health.HasValue ? DynValue.NewNumber(player.Health.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.MaxHealth), player.MaxHealth.HasValue ? DynValue.NewNumber(player.MaxHealth.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.CurrentAmmo), player.CurrentAmmo.HasValue ? DynValue.NewNumber(player.CurrentAmmo.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.MaxAmmo), player.MaxAmmo.HasValue ? DynValue.NewNumber(player.MaxAmmo.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.Kills), player.Kills.HasValue ? DynValue.NewNumber(player.Kills.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.Deaths), player.Deaths.HasValue ? DynValue.NewNumber(player.Deaths.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.Assists), player.Assists.HasValue ? DynValue.NewNumber(player.Assists.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.Caps), player.Caps.HasValue ? DynValue.NewNumber(player.Caps.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.Points), player.Points.HasValue ? DynValue.NewNumber(player.Points.Value) : DynValue.Nil);
        SetNamedValue(table, nameof(player.IsCarryingIntel), DynValue.NewBoolean(player.IsCarryingIntel));
        SetNamedValue(table, nameof(player.IsInSpawnRoom), DynValue.NewBoolean(player.IsInSpawnRoom));
        return table;
    }

    private static void SetNamedValue(Table table, string name, DynValue value)
    {
        table[name] = value;
        var camelCaseName = ToCamelCase(name);
        if (!string.Equals(camelCaseName, name, StringComparison.Ordinal))
        {
            table[camelCaseName] = value;
        }
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
        return ReadByteValue(ReadArgument(args, index));
    }

    private static byte ReadByteValue(DynValue dynValue)
    {
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
        return ReadIntValue(ReadArgument(args, index));
    }

    private static int ReadIntValue(DynValue dynValue)
    {
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

    private static int? ReadOptionalNullableIntArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            return null;
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

    private static double ReadOptionalDoubleArgument(CallbackArguments args, int index, double defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            return defaultValue;
        }

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

    private static bool ReadOptionalBoolField(Table? table, bool defaultValue, params string[] fieldNames)
    {
        if (table is null)
        {
            return defaultValue;
        }

        for (var index = 0; index < fieldNames.Length; index += 1)
        {
            var dynValue = table.Get(fieldNames[index]);
            if (dynValue.IsNil())
            {
                continue;
            }

            return dynValue.Type switch
            {
                DataType.Boolean => dynValue.Boolean,
                DataType.Number => Math.Abs(dynValue.Number) > double.Epsilon,
                _ => bool.Parse(dynValue.CastToString()),
            };
        }

        return defaultValue;
    }

    private static PluginMessagePayloadFormat ReadOptionalEnumArgument(CallbackArguments args, int index, PluginMessagePayloadFormat defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.IsNil() || !Enum.TryParse<PluginMessagePayloadFormat>(dynValue.CastToString(), ignoreCase: true, out var value)
            ? defaultValue
            : value;
    }

    private static TEnum? ReadOptionalEnumArgument<TEnum>(CallbackArguments args, int index) where TEnum : struct, Enum
    {
        return TryParseEnumValue(ReadArgument(args, index), out TEnum value) ? value : null;
    }

    private static bool TryParseEnumArgument<TEnum>(CallbackArguments args, int index, out TEnum value) where TEnum : struct, Enum
    {
        return TryParseEnumValue(ReadArgument(args, index), out value);
    }

    private static bool TryParseEnumValue<TEnum>(DynValue dynValue, out TEnum value) where TEnum : struct, Enum
    {
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

    private static bool TryParseOptionalEnumArgument<TEnum>(CallbackArguments args, int index, out TEnum value) where TEnum : struct, Enum
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            value = default;
            return false;
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

    private sealed class LuaGameplayAbilityExecutor(LuaServerPlugin plugin) : IGameplayAbilityExecutor
    {
        public GameplayAbilityResult Handle(GameplayAbilityContext context)
        {
            return plugin.ExecuteGameplayAbility(context);
        }
    }

    private sealed class LuaGameplayPrimaryWeaponExecutor(LuaServerPlugin plugin) : IGameplayPrimaryWeaponExecutor
    {
        public GameplayPrimaryWeaponResult Handle(GameplayPrimaryWeaponContext context)
        {
            return plugin.ExecuteGameplayPrimaryWeapon(context);
        }
    }

    private sealed class LuaServerCommand(
        LuaServerPlugin plugin,
        string name,
        string description,
        string usage,
        DynValue handler) : IOpenGarrisonServerCommand
    {
        public string Name { get; } = name;

        public string Description { get; } = description;

        public string Usage { get; } = usage;

        public Task<IReadOnlyList<string>> ExecuteAsync(
            OpenGarrisonServerCommandContext context,
            string arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(plugin.ExecuteLuaCommandHandler(handler, context, arguments));
        }
    }

    private sealed record LuaCommandRegistration(
        IOpenGarrisonServerCommand Command,
        OpenGarrisonServerAdminPermissions RequiredPermissions,
        IReadOnlyList<string> Aliases);

    private sealed record LuaDeferredServerAction(
        string ActionType,
        Func<IOpenGarrisonServerPluginContext, bool> Execute);

    private enum ServerLuaCallbackPhase
    {
        None,
        Initialize,
        Shutdown,
        Lifecycle,
        Update,
        Query,
        CommandInteraction,
        AbilityExecution,
        PrimaryWeaponExecution,
        Event,
    }
}
