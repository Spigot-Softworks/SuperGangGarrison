using System.Reflection;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
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
    private bool _callbacksDisabled;
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

        return ExecuteInPhase(ServerLuaCallbackPhase.Query, () =>
        {
            if (!TryInvokeCallback("try_handle_chat_message", out var result, ToDynValue(context), ToDynValue(e)))
            {
                return false;
            }

            return result.CastToBool();
        });
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
            var path = ResolveConfigPath(context.ConfigDirectory, relativePath);
            SaveLuaTableJson(path, value);
            return DynValue.True;
        });

        host["get_manifest"] = DynValue.NewCallback((_, _) => ToDynValue(context.Manifest));
        host["get_host_api"] = DynValue.NewCallback((_, _) => ToDynValue(context.HostApi));
        host["get_server_state"] = DynValue.NewCallback((_, _) => ToDynValue(CreateServerStateSnapshot(context.ServerState)));
        host["get_players"] = DynValue.NewCallback((_, _) => ToDynValue(context.ServerState.GetPlayers()));
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
            context.AdminOperations.BroadcastSystemMessage(ReadStringArgument(args, 0));
            return DynValue.True;
        });
        host["send_system_message"] = DynValue.NewCallback((_, args) =>
        {
            context.AdminOperations.SendSystemMessage(ReadByteArgument(args, 0), ReadStringArgument(args, 1));
            return DynValue.True;
        });
        host["try_disconnect"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TryDisconnect(ReadByteArgument(args, 0), ReadStringArgument(args, 1))));
        host["try_move_to_spectator"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TryMoveToSpectator(ReadByteArgument(args, 0))));
        host["try_set_team"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(TryParseEnumArgument<PlayerTeam>(args, 1, out var team)
                && context.AdminOperations.TrySetTeam(ReadByteArgument(args, 0), team)));
        host["try_set_class"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(TryParseEnumArgument<PlayerClass>(args, 1, out var playerClass)
                && context.AdminOperations.TrySetClass(ReadByteArgument(args, 0), playerClass)));
        host["try_set_gameplay_loadout"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TrySetGameplayLoadout(ReadByteArgument(args, 0), ReadStringArgument(args, 1))));
        host["try_set_gameplay_secondary_item"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TrySetGameplaySecondaryItem(ReadByteArgument(args, 0), ReadOptionalStringArgument(args, 1))));
        host["try_set_gameplay_acquired_item"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TrySetGameplayAcquiredItem(ReadByteArgument(args, 0), ReadOptionalStringArgument(args, 1))));
        host["try_grant_gameplay_item"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TryGrantGameplayItem(ReadByteArgument(args, 0), ReadStringArgument(args, 1))));
        host["try_revoke_gameplay_item"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TryRevokeGameplayItem(ReadByteArgument(args, 0), ReadStringArgument(args, 1))));
        host["try_set_gameplay_equipped_slot"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(TryParseEnumArgument<GameplayEquipmentSlot>(args, 1, out var equippedSlot)
                && context.AdminOperations.TrySetGameplayEquippedSlot(ReadByteArgument(args, 0), equippedSlot)));
        host["try_force_kill"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TryForceKill(ReadByteArgument(args, 0))));
        host["try_set_cap_limit"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TrySetCapLimit(ReadIntArgument(args, 0))));
        host["try_change_map"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TryChangeMap(
                ReadStringArgument(args, 0),
                ReadOptionalIntArgument(args, 1, 1),
                ReadOptionalBoolArgument(args, 2, false))));
        host["try_set_next_round_map"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.AdminOperations.TrySetNextRoundMap(
                ReadStringArgument(args, 0),
                ReadOptionalIntArgument(args, 1, 1))));
        host["send_message_to_client"] = DynValue.NewCallback((_, args) =>
        {
            context.SendMessageToClient(
                ReadByteArgument(args, 0),
                ReadStringArgument(args, 1),
                ReadStringArgument(args, 2),
                ReadStringArgument(args, 3),
                ReadOptionalEnumArgument(args, 4, PluginMessagePayloadFormat.Text),
                ReadOptionalUShortArgument(args, 5, 1));
            return DynValue.True;
        });
        host["broadcast_message_to_clients"] = DynValue.NewCallback((_, args) =>
        {
            context.BroadcastMessageToClients(
                ReadStringArgument(args, 0),
                ReadStringArgument(args, 1),
                ReadStringArgument(args, 2),
                ReadOptionalEnumArgument(args, 3, PluginMessagePayloadFormat.Text),
                ReadOptionalUShortArgument(args, 4, 1));
            return DynValue.True;
        });
        host["set_player_replicated_state_int"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.SetPlayerReplicatedStateInt(ReadByteArgument(args, 0), ReadStringArgument(args, 1), ReadIntArgument(args, 2))));
        host["set_player_replicated_state_float"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.SetPlayerReplicatedStateFloat(ReadByteArgument(args, 0), ReadStringArgument(args, 1), (float)ReadDoubleArgument(args, 2))));
        host["set_player_replicated_state_bool"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.SetPlayerReplicatedStateBool(ReadByteArgument(args, 0), ReadStringArgument(args, 1), ReadBoolArgument(args, 2))));
        host["clear_player_replicated_state"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.ClearPlayerReplicatedState(ReadByteArgument(args, 0), ReadStringArgument(args, 1))));

        return host;
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
            ServerLuaCallbackPhase.Event => "a server event callback",
            _ => "an unknown callback",
        };
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
            GameMode = state.GameMode.ToString(),
            MatchPhase = state.MatchPhase.ToString(),
            state.RedCaps,
            state.BlueCaps,
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

    private static byte ReadByteArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
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
        Event,
    }
}
