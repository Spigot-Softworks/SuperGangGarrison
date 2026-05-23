using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class PluginHost
{
    private readonly Func<SimulationWorld> _worldGetter;
    private readonly PluginCommandRegistry _commandRegistry;
    private readonly IOpenGarrisonServerReadOnlyState _serverState;
    private readonly IOpenGarrisonServerAdminOperations _adminOperations;
    private readonly IOpenGarrisonServerCvarRegistry _cvarRegistry;
    private readonly IOpenGarrisonServerScheduler _scheduler;
    private readonly Func<byte, OpenGarrisonServerAdminIdentity> _adminIdentityResolver;
    private readonly Action<byte, string, string, string, string, PluginMessagePayloadFormat, ushort> _sendMessageToClient;
    private readonly Action<string, string, string, string, PluginMessagePayloadFormat, ushort> _broadcastMessageToClients;
    private readonly Action<string> _log;
    private readonly string _pluginsRootDirectory;
    private readonly string _pluginConfigRoot;
    private readonly string _mapsDirectory;
    private readonly OpenGarrisonPluginHostApi _hostApi = OpenGarrisonPluginHostApi.CreateServerDefault();
    private readonly List<PluginLoader.LoadedPlugin> _loadedPlugins = new();
    private readonly HashSet<string> _registeredManifestGameplayPackIds = new(StringComparer.Ordinal);

    public PluginHost(
        Func<SimulationWorld> worldGetter,
        PluginCommandRegistry commandRegistry,
        IOpenGarrisonServerReadOnlyState serverState,
        IOpenGarrisonServerAdminOperations adminOperations,
        IOpenGarrisonServerCvarRegistry cvarRegistry,
        IOpenGarrisonServerScheduler scheduler,
        Func<byte, OpenGarrisonServerAdminIdentity> adminIdentityResolver,
        Action<byte, string, string, string, string, PluginMessagePayloadFormat, ushort> sendMessageToClient,
        Action<string, string, string, string, PluginMessagePayloadFormat, ushort> broadcastMessageToClients,
        string pluginsDirectory,
        string pluginConfigRoot,
        string mapsDirectory,
        Action<string> log)
    {
        _worldGetter = worldGetter;
        _commandRegistry = commandRegistry;
        _serverState = serverState;
        _adminOperations = adminOperations;
        _cvarRegistry = cvarRegistry;
        _scheduler = scheduler;
        _adminIdentityResolver = adminIdentityResolver;
        _sendMessageToClient = sendMessageToClient;
        _broadcastMessageToClients = broadcastMessageToClients;
        _pluginsRootDirectory = pluginsDirectory;
        _pluginConfigRoot = pluginConfigRoot;
        _mapsDirectory = mapsDirectory;
        _log = log;
    }

    public IReadOnlyList<string> LoadedPluginIds => _loadedPlugins
        .Select(entry => entry.Plugin.Id)
        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void LoadPlugins()
    {
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(PluginLoader.LoadFromSearchDirectories(BuildPluginSearchDirectories(), CreateContext, _log));
        foreach (var loadedPlugin in _loadedPlugins)
        {
            _log($"[plugin] loaded {loadedPlugin.Plugin.DisplayName} ({loadedPlugin.Plugin.Id} {loadedPlugin.Plugin.Version})");
        }
    }

    public void LoadPlugins(IEnumerable<System.Reflection.Assembly> assemblies)
    {
        _loadedPlugins.Clear();
        _loadedPlugins.AddRange(PluginLoader.LoadFromAssemblies(assemblies, CreateContext, _log));
        foreach (var loadedPlugin in _loadedPlugins)
        {
            _log($"[plugin] loaded {loadedPlugin.Plugin.DisplayName} ({loadedPlugin.Plugin.Id} {loadedPlugin.Plugin.Version})");
        }
    }

    public void NotifyServerStarting() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStarting());

    public void NotifyServerStarted() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStarted());

    public void NotifyServerStopping() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStopping());

    public void NotifyServerStopped() => Dispatch<IOpenGarrisonServerLifecycleHooks>(hook => hook.OnServerStopped());

    public void NotifyServerHeartbeat(TimeSpan uptime) => Dispatch<IOpenGarrisonServerUpdateHooks>(hook => hook.OnServerHeartbeat(uptime));

    public void NotifyHelloReceived(HelloReceivedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnHelloReceived(e));

    public void NotifyClientConnected(ClientConnectedEvent e)
    {
        Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnClientConnected(e));
        Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnPlayerJoined(new OpenGarrisonServerPlayerJoinedEvent(
            _worldGetter().Frame,
            e.Slot,
            e.PlayerName,
            e.EndPoint,
            e.IsAuthorized,
            e.IsSpectator)));
    }

    public void NotifyClientDisconnected(ClientDisconnectedEvent e)
    {
        Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnClientDisconnected(e));
        Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnPlayerLeft(new OpenGarrisonServerPlayerLeftEvent(
            _worldGetter().Frame,
            e.Slot,
            e.PlayerName,
            e.EndPoint,
            e.Reason,
            e.WasAuthorized)));
    }

    public void NotifyPasswordAccepted(PasswordAcceptedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnPasswordAccepted(e));

    public void NotifyPlayerTeamChanged(PlayerTeamChangedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnPlayerTeamChanged(e));

    public void NotifyPlayerClassChanged(PlayerClassChangedEvent e) => Dispatch<IOpenGarrisonServerClientHooks>(hook => hook.OnPlayerClassChanged(e));

    public void NotifyChatReceived(ChatReceivedEvent e) => Dispatch<IOpenGarrisonServerChatHooks>(hook => hook.OnChatReceived(e));

    public bool TryHandleChatMessage(ChatReceivedEvent e)
    {
        var decision = DispatchDecision(
            hook => hook.BeforeChatMessage(e),
            "before_chat_message");
        if (decision.IsCancelled)
        {
            if (!string.IsNullOrWhiteSpace(decision.Reason))
            {
                _log($"[plugin] chat message from slot {e.Slot} cancelled: {decision.Reason}");
            }

            return true;
        }

        var context = new OpenGarrisonServerChatMessageContext(
            _serverState,
            _adminOperations,
            _cvarRegistry,
            _scheduler,
            _adminIdentityResolver(e.Slot));
        if (IsPluginChatCommandText(e.Text)
            && _commandRegistry.TryExecute(
                e.Text,
                new OpenGarrisonServerCommandContext(
                    _serverState,
                    _adminOperations,
                    _cvarRegistry,
                    _scheduler,
                    context.Identity,
                    OpenGarrisonServerCommandSource.PrivateChat),
                System.Threading.CancellationToken.None,
                out var commandResponseLines))
        {
            foreach (var line in commandResponseLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _adminOperations.SendSystemMessage(e.Slot, line);
                }
            }

            return true;
        }

        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.Plugin is not IOpenGarrisonServerChatCommandHooks hook)
            {
                continue;
            }

            try
            {
                if (hook.TryHandleChatMessage(context, e))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log($"[plugin] chat hook failed for {loadedPlugin.Plugin.Id}: {ex.Message}");
            }
        }

        return false;
    }

    private static bool IsPluginChatCommandText(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.Length > 1 && trimmed[0] is '!' or '/';
    }

    public OpenGarrisonServerDecisionResult BeforeTeamChange(byte slot, PlayerTeam team)
    {
        return DispatchDecision(
            hook => hook.BeforeTeamChange(new OpenGarrisonServerTeamChangeRequest(slot, team)),
            "before_team_change");
    }

    public OpenGarrisonServerDecisionResult BeforeClassChange(byte slot, PlayerClass playerClass)
    {
        return DispatchDecision(
            hook => hook.BeforeClassChange(new OpenGarrisonServerClassChangeRequest(slot, playerClass)),
            "before_class_change");
    }

    public OpenGarrisonServerDecisionResult BeforeLoadoutChange(byte slot, string loadoutId)
    {
        return DispatchDecision(
            hook => hook.BeforeLoadoutChange(new OpenGarrisonServerLoadoutChangeRequest(slot, loadoutId)),
            "before_loadout_change");
    }

    public OpenGarrisonServerDecisionResult BeforeMapChange(string levelName, int mapAreaIndex, bool preservePlayerStats)
    {
        return DispatchDecision(
            hook => hook.BeforeMapChange(new OpenGarrisonServerMapChangeRequest(levelName, mapAreaIndex, preservePlayerStats)),
            "before_map_change");
    }

    public OpenGarrisonServerDecisionResult BeforeSpawn(WorldSpawnDecisionRequest e)
    {
        return DispatchDecision(
            hook => hook.BeforeSpawn(new OpenGarrisonServerSpawnRequest(
                e.Frame,
                e.Slot,
                e.PlayerId,
                e.PlayerName,
                e.Team,
                e.PlayerClass,
                e.WorldX,
                e.WorldY,
                e.IsRespawn)),
            "before_spawn");
    }

    public OpenGarrisonServerDecisionResult BeforeDamage(WorldDamageDecisionRequest e)
    {
        return DispatchDecision(
            hook => hook.BeforeDamage(new OpenGarrisonServerDamageRequest(
                e.Frame,
                e.TargetKind,
                e.TargetEntityId,
                e.TargetPlayerId,
                e.TargetTeam,
                e.AttackerPlayerId,
                e.AttackerTeam,
                e.Amount,
                e.WouldBeFatal,
                e.WorldX,
                e.WorldY)),
            "before_damage");
    }

    public OpenGarrisonServerDecisionResult BeforeDeath(WorldDeathDecisionRequest e)
    {
        return DispatchDecision(
            hook => hook.BeforeDeath(new OpenGarrisonServerDeathRequest(
                e.Frame,
                e.Slot,
                e.VictimPlayerId,
                e.VictimName,
                e.VictimTeam,
                e.VictimClass,
                e.KillerPlayerId,
                e.KillerName,
                e.KillerTeam,
                e.WeaponSpriteName,
                e.Gibbed)),
            "before_death");
    }

    public OpenGarrisonServerDecisionResult BeforePickup(WorldPickupDecisionRequest e)
    {
        return DispatchDecision(
            hook => hook.BeforePickup(new OpenGarrisonServerPickupRequest(
                e.Frame,
                e.Kind.ToString(),
                e.Slot,
                e.PlayerId,
                e.PlayerName,
                e.Team,
                e.PickupEntityId,
                e.PickupValue,
                e.WorldX,
                e.WorldY)),
            "before_pickup");
    }

    public OpenGarrisonServerDecisionResult BeforeScore(WorldScoreDecisionRequest e)
    {
        return DispatchDecision(
            hook => hook.BeforeScore(new OpenGarrisonServerScoreRequest(
                e.Frame,
                e.Team,
                e.Delta,
                e.RedCaps,
                e.BlueCaps,
                e.ActorPlayerId,
                e.Reason)),
            "before_score");
    }

    public OpenGarrisonServerDecisionResult BeforeRoundEnd(WorldRoundEndDecisionRequest e)
    {
        return DispatchDecision(
            hook => hook.BeforeRoundEnd(new OpenGarrisonServerRoundEndRequest(
                e.Frame,
                e.GameMode,
                e.WinnerTeam,
                e.RedCaps,
                e.BlueCaps,
                e.Reason)),
            "before_round_end");
    }

    public void NotifyMapChanging(MapChangingEvent e) => Dispatch<IOpenGarrisonServerMapHooks>(hook => hook.OnMapChanging(e));

    public void NotifyMapChanged(MapChangedEvent e) => Dispatch<IOpenGarrisonServerMapHooks>(hook => hook.OnMapChanged(e));

    public void NotifyScoreChanged(ScoreChangedEvent e) => Dispatch<IOpenGarrisonServerGameplayHooks>(hook => hook.OnScoreChanged(e));

    public void NotifyRoundEnded(RoundEndedEvent e) => Dispatch<IOpenGarrisonServerGameplayHooks>(hook => hook.OnRoundEnded(e));

    public void NotifyKillFeedEntry(KillFeedEvent e) => Dispatch<IOpenGarrisonServerGameplayHooks>(hook => hook.OnKillFeedEntry(e));

    public void NotifyDamage(OpenGarrisonServerDamageEvent e) => Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnDamage(e));

    public void NotifyDeath(OpenGarrisonServerDeathEvent e) => Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnDeath(e));

    public void NotifyAssist(OpenGarrisonServerAssistEvent e) => Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnAssist(e));

    public void NotifyBuild(OpenGarrisonServerBuildableEvent e) => Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnBuild(e));

    public void NotifyDestroy(OpenGarrisonServerBuildableEvent e) => Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnDestroy(e));

    public void NotifyIntelEvent(OpenGarrisonServerIntelEvent e) => Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnIntelEvent(e));

    public void NotifyControlPointStateChanged(OpenGarrisonServerControlPointStateEvent e) => Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnControlPointStateChanged(e));

    public void NotifyPlayerSpawned(OpenGarrisonServerPlayerSpawnEvent e)
    {
        Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnPlayerSpawned(e));
        if (e.IsRespawn)
        {
            Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnPlayerRespawned(new OpenGarrisonServerPlayerRespawnEvent(
                e.Frame,
                e.Slot,
                e.PlayerId,
                e.PlayerName,
                e.Team,
                e.PlayerClass,
                e.WorldX,
                e.WorldY)));
        }
    }

    public bool TryNotifyGameplayAbilityInput(WorldGameplayAbilityEvent e)
    {
        var inputEvent = ToGameplayAbilityInputEvent(e);
        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.Plugin is not IOpenGarrisonServerSemanticGameplayHooks hook)
            {
                continue;
            }

            try
            {
                hook.OnGameplayAbilityInput(inputEvent);
            }
            catch (Exception ex)
            {
                _log($"[plugin] ability input hook failed for {loadedPlugin.Plugin.Id}: {ex.Message}");
            }
        }

        return inputEvent.IsCancelled;
    }

    public void NotifyGameplayAbilityUsed(WorldGameplayAbilityEvent e)
    {
        Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnGameplayAbilityUsed(ToGameplayAbilityUsedEvent(e)));
    }

    public void NotifyGameplayAbilityStateChanged(OpenGarrisonServerGameplayAbilityStateChangedEvent e)
    {
        Dispatch<IOpenGarrisonServerSemanticGameplayHooks>(hook => hook.OnGameplayAbilityStateChanged(e));
    }

    public void NotifyClientPluginMessage(OpenGarrisonServerPluginMessageEnvelope e)
    {
        if (!PluginMessageContract.TryNormalizeIncoming(
                e.SourcePluginId,
                e.TargetPluginId,
                e.MessageType,
                e.Payload,
                e.PayloadFormat,
                e.SchemaVersion,
                out var normalizedSourcePluginId,
                out var normalizedTargetPluginId,
                out var normalizedMessageType,
                out var normalizedPayload,
                out var error))
        {
            _log($"[plugin] rejected inbound client plugin message from slot {e.SourceSlot}: {error}");
            return;
        }

        var targetPlugin = FindLoadedPlugin(normalizedTargetPluginId);
        if (targetPlugin?.Plugin is not IOpenGarrisonServerPluginMessageHooks hook)
        {
            return;
        }

        try
        {
            hook.OnClientPluginMessage(new OpenGarrisonServerPluginMessageEnvelope(
                e.SourceSlot,
                e.SourcePlayerName,
                normalizedSourcePluginId,
                normalizedTargetPluginId,
                normalizedMessageType,
                normalizedPayload,
                e.PayloadFormat,
                e.SchemaVersion));
        }
        catch (Exception ex)
        {
            _log($"[plugin] hook failed for {targetPlugin.Plugin.Id}: {ex.Message}");
        }
    }

    public void ShutdownPlugins()
    {
        for (var index = _loadedPlugins.Count - 1; index >= 0; index -= 1)
        {
            try
            {
                _loadedPlugins[index].Plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _log($"[plugin] shutdown failed for {_loadedPlugins[index].Plugin.Id}: {ex.Message}");
            }
        }
    }

    private IEnumerable<PluginLoader.PluginSearchDirectory> BuildPluginSearchDirectories()
    {
        var scopedServerPluginsDirectory = Path.Combine(_pluginsRootDirectory, "Server");
        yield return new PluginLoader.PluginSearchDirectory(scopedServerPluginsDirectory, SearchOption.AllDirectories);

        if (LegacyServerPluginsExist())
        {
            _log("[plugin] discovered legacy server plugins under Plugins root; prefer Plugins/Server/<PluginFolder>/ for new installs.");
            yield return new PluginLoader.PluginSearchDirectory(_pluginsRootDirectory, SearchOption.TopDirectoryOnly);
        }
    }

    private bool LegacyServerPluginsExist()
    {
        if (!Directory.Exists(_pluginsRootDirectory))
        {
            return false;
        }

        return Directory.EnumerateFiles(_pluginsRootDirectory, "*.dll", SearchOption.TopDirectoryOnly).Any();
    }

    private IOpenGarrisonServerPluginContext CreateContext(IOpenGarrisonServerPlugin plugin, OpenGarrisonPluginManifest manifest, string pluginDirectory)
    {
        pluginDirectory = ResolvePluginDirectory(plugin, pluginDirectory);
        var configDirectory = ResolveConfigDirectory(plugin.Id);
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(_mapsDirectory);
        RegisterManifestGameplayPacks(manifest, pluginDirectory);
        return new ServerPluginContext(
            plugin.Id,
            pluginDirectory,
            configDirectory,
            manifest,
            _hostApi,
            _mapsDirectory,
            _serverState,
            _adminOperations,
            _cvarRegistry,
            _scheduler,
            (slot, targetPluginId, messageType, payload, payloadFormat, schemaVersion) => TrySendMessageToClient(plugin.Id, manifest, slot, targetPluginId, messageType, payload, payloadFormat, schemaVersion),
            (targetPluginId, messageType, payload, payloadFormat, schemaVersion) => TryBroadcastMessageToClients(plugin.Id, manifest, targetPluginId, messageType, payload, payloadFormat, schemaVersion),
            (ownerId, slot, stateKey, value) => TrySetPlayerReplicatedState(slot, static (player, pluginId, key, stateValue) => player.SetReplicatedStateInt(pluginId, key, stateValue), ownerId, stateKey, value),
            (ownerId, slot, stateKey, value) => TrySetPlayerReplicatedState(slot, static (player, pluginId, key, stateValue) => player.SetReplicatedStateFloat(pluginId, key, stateValue), ownerId, stateKey, value),
            (ownerId, slot, stateKey, value) => TrySetPlayerReplicatedState(slot, static (player, pluginId, key, stateValue) => player.SetReplicatedStateBool(pluginId, key, stateValue), ownerId, stateKey, value),
            (ownerId, slot, stateKey) => TryClearPlayerReplicatedState(slot, ownerId, stateKey),
            TryRegisterGameplayAbility,
            TryOverrideGameplayAbility,
            TryRegisterGameplayAbilityExecutor,
            TryRegisterGameplayPrimaryWeaponBehavior,
            TryRegisterGameplayWeaponItem,
            TryRegisterGameplayLoadout,
            TryRegisterGameplaySlotItem,
            (playerId, velocityX, velocityY) => _worldGetter().TryApplyGameplayImpulse(playerId, velocityX, velocityY),
            (ownerId, playerId, cooldownKey, ticks) => TrySetGameplayAbilityCooldown(ownerId, playerId, cooldownKey, ticks),
            (targetPlayerId, amount, attackerPlayerId, weaponSpriteName) => _worldGetter().TryApplyGameplayDamage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName),
            (playerId, amount) => _worldGetter().TryApplyGameplayHealing(playerId, amount),
            (playerId, statusEffectId, ticks, value) => _worldGetter().TryApplyGameplayStatusEffect(playerId, statusEffectId, ticks, value),
            (GameplayProjectileSpawnRequest request, out int projectileId) => _worldGetter().TrySpawnGameplayProjectile(request, out projectileId),
            _commandRegistry,
            _log);
    }

    private void RegisterManifestGameplayPacks(OpenGarrisonPluginManifest manifest, string pluginDirectory)
    {
        foreach (var gameplayPack in manifest.GameplayPacks)
        {
            var packDirectory = OpenGarrisonPluginPathContainment.ResolveContainedPath(
                pluginDirectory,
                gameplayPack.Path,
                "manifest gameplayPacks path escapes plugin directory.");
            GameplayModPackDefinition modPack;
            try
            {
                modPack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Manifest gameplay pack \"{gameplayPack.Path}\" could not be loaded: {ex.Message}", ex);
            }

            if (_registeredManifestGameplayPackIds.Contains(modPack.Id))
            {
                continue;
            }

            if (!CharacterClassCatalog.RuntimeRegistry.TryRegisterModPack(
                    modPack,
                    gameplayPack.AllowRuntimeClassBindingOverride,
                    out var errorMessage))
            {
                throw new InvalidOperationException($"Manifest gameplay pack \"{modPack.Id}\" could not be registered: {errorMessage}");
            }

            _registeredManifestGameplayPackIds.Add(modPack.Id);
            _log($"[plugin] registered gameplay pack {modPack.DisplayName} ({modPack.Id} {modPack.Version})");
        }
    }

    private void TrySendMessageToClient(
        string sourcePluginId,
        OpenGarrisonPluginManifest? sourceManifest,
        byte slot,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        if (!PluginMessageContract.TryNormalizeOutgoing(
                targetPluginId,
                messageType,
                payload,
                payloadFormat,
                schemaVersion,
                out var normalizedTargetPluginId,
                out var normalizedMessageType,
                out var normalizedPayload,
                out var error))
        {
            _log($"[plugin] rejected outbound plugin message for {sourcePluginId}: {error}");
            return;
        }

        var manifest = sourceManifest
            ?? FindLoadedPlugin(sourcePluginId)?.Context.Manifest;
        if (manifest is not null
            && !OpenGarrisonPluginManifestMessageContractPolicy.TryValidateOutgoing(
                manifest,
                normalizedTargetPluginId,
                normalizedMessageType,
                payloadFormat.ToString(),
                schemaVersion,
                OpenGarrisonPluginManifestMessageContractPolicy.DirectionServerToClient,
                out error))
        {
            _log($"[plugin] rejected outbound plugin message for {sourcePluginId}: {error}");
            return;
        }

        _sendMessageToClient(slot, sourcePluginId, normalizedTargetPluginId, normalizedMessageType, normalizedPayload, payloadFormat, schemaVersion);
    }

    private void TryBroadcastMessageToClients(
        string sourcePluginId,
        OpenGarrisonPluginManifest? sourceManifest,
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion)
    {
        if (!PluginMessageContract.TryNormalizeOutgoing(
                targetPluginId,
                messageType,
                payload,
                payloadFormat,
                schemaVersion,
                out var normalizedTargetPluginId,
                out var normalizedMessageType,
                out var normalizedPayload,
                out var error))
        {
            _log($"[plugin] rejected outbound plugin message for {sourcePluginId}: {error}");
            return;
        }

        var manifest = sourceManifest
            ?? FindLoadedPlugin(sourcePluginId)?.Context.Manifest;
        if (manifest is not null
            && !OpenGarrisonPluginManifestMessageContractPolicy.TryValidateOutgoing(
                manifest,
                normalizedTargetPluginId,
                normalizedMessageType,
                payloadFormat.ToString(),
                schemaVersion,
                OpenGarrisonPluginManifestMessageContractPolicy.DirectionServerToClient,
                out error))
        {
            _log($"[plugin] rejected outbound plugin message for {sourcePluginId}: {error}");
            return;
        }

        _broadcastMessageToClients(sourcePluginId, normalizedTargetPluginId, normalizedMessageType, normalizedPayload, payloadFormat, schemaVersion);
    }

    private bool TrySetPlayerReplicatedState<TValue>(
        byte slot,
        Func<PlayerEntity, string, string, TValue, bool> setter,
        string ownerId,
        string stateKey,
        TValue value)
    {
        if (!GameplayReplicatedStateContract.TryNormalizeIdentifier(stateKey, out var normalizedStateKey))
        {
            _log($"[plugin] rejected replicated state write for {ownerId}: state key must be a non-empty ASCII identifier up to {GameplayReplicatedStateContract.MaxIdentifierLength} characters.");
            return false;
        }

        return _worldGetter().TryGetNetworkPlayer(slot, out var player)
            && setter(player, ownerId, normalizedStateKey, value);
    }

    private bool TryClearPlayerReplicatedState(byte slot, string ownerId, string stateKey)
    {
        if (!GameplayReplicatedStateContract.TryNormalizeIdentifier(stateKey, out var normalizedStateKey))
        {
            _log($"[plugin] rejected replicated state clear for {ownerId}: state key must be a non-empty ASCII identifier up to {GameplayReplicatedStateContract.MaxIdentifierLength} characters.");
            return false;
        }

        return _worldGetter().TryGetNetworkPlayer(slot, out var player)
            && player.ClearReplicatedState(ownerId, normalizedStateKey);
    }

    private bool TrySetGameplayAbilityCooldown(string ownerId, int playerId, string cooldownKey, int ticks)
    {
        if (!GameplayReplicatedStateContract.TryNormalizeIdentifier(cooldownKey, out var normalizedCooldownKey))
        {
            _log($"[plugin] rejected ability cooldown write for {ownerId}: cooldown key must be a non-empty ASCII identifier up to {GameplayReplicatedStateContract.MaxIdentifierLength} characters.");
            return false;
        }

        return _worldGetter().TrySetGameplayAbilityCooldown(playerId, ownerId, normalizedCooldownKey, ticks);
    }

    private bool TryRegisterGameplayAbility(string pluginId, GameplayAbilityRegistration registration, out string errorMessage)
    {
        var modPackId = string.IsNullOrWhiteSpace(registration.ModPackId)
            ? $"plugin.{pluginId}"
            : registration.ModPackId;
        if (CharacterClassCatalog.RuntimeRegistry.TryRegisterGameplayAbility(registration with { ModPackId = modPackId }, out errorMessage))
        {
            return true;
        }

        _log($"[plugin] gameplay ability registration rejected for {pluginId}: {errorMessage}");
        return false;
    }

    private bool TryOverrideGameplayAbility(string pluginId, string itemId, GameplayAbilityPatch patch, out string errorMessage)
    {
        if (CharacterClassCatalog.RuntimeRegistry.TryOverrideGameplayAbility(itemId, patch, out errorMessage))
        {
            return true;
        }

        _log($"[plugin] gameplay ability override rejected for {pluginId}: {errorMessage}");
        return false;
    }

    private bool TryRegisterGameplayAbilityExecutor(string pluginId, string executorId, IGameplayAbilityExecutor executor, out string errorMessage)
    {
        var normalizedExecutorId = string.IsNullOrWhiteSpace(executorId)
            ? string.Empty
            : executorId.Trim();
        if (!normalizedExecutorId.StartsWith($"plugin.{pluginId}.", StringComparison.Ordinal)
            && !normalizedExecutorId.StartsWith($"{pluginId}.", StringComparison.Ordinal))
        {
            errorMessage = $"Plugin ability executor ids must start with \"plugin.{pluginId}.\" or \"{pluginId}.\".";
            return false;
        }

        if (CharacterClassCatalog.RuntimeRegistry.TryRegisterGameplayAbilityExecutor(normalizedExecutorId, executor, out errorMessage))
        {
            return true;
        }

        _log($"[plugin] gameplay ability executor registration rejected for {pluginId}: {errorMessage}");
        return false;
    }

    private bool TryRegisterGameplayPrimaryWeaponBehavior(
        string pluginId,
        string behaviorId,
        IGameplayPrimaryWeaponExecutor executor,
        string? fireSoundName,
        out string errorMessage)
    {
        var normalizedBehaviorId = string.IsNullOrWhiteSpace(behaviorId)
            ? string.Empty
            : behaviorId.Trim();
        if (!normalizedBehaviorId.StartsWith($"plugin.{pluginId}.", StringComparison.Ordinal)
            && !normalizedBehaviorId.StartsWith($"{pluginId}.", StringComparison.Ordinal))
        {
            errorMessage = $"Plugin primary weapon behavior ids must start with \"plugin.{pluginId}.\" or \"{pluginId}.\".";
            return false;
        }

        var binding = new GameplayPrimaryWeaponRuntimeBinding(
            normalizedBehaviorId,
            PrimaryWeaponKind.Custom,
            string.IsNullOrWhiteSpace(fireSoundName) ? null : fireSoundName.Trim(),
            executor);
        if (CharacterClassCatalog.RuntimeRegistry.TryRegisterPrimaryWeaponBehavior(binding, out errorMessage))
        {
            return true;
        }

        _log($"[plugin] gameplay primary weapon behavior registration rejected for {pluginId}: {errorMessage}");
        return false;
    }

    private bool TryRegisterGameplayWeaponItem(string pluginId, GameplayWeaponItemRegistration registration, out string errorMessage)
    {
        var modPackId = string.IsNullOrWhiteSpace(registration.ModPackId)
            ? $"plugin.{pluginId}"
            : registration.ModPackId;
        if (CharacterClassCatalog.RuntimeRegistry.TryRegisterGameplayWeaponItem(registration with { ModPackId = modPackId }, out errorMessage))
        {
            return true;
        }

        _log($"[plugin] gameplay weapon item registration rejected for {pluginId}: {errorMessage}");
        return false;
    }

    private bool TryRegisterGameplayLoadout(string pluginId, GameplayLoadoutRegistration registration, out string errorMessage)
    {
        var modPackId = string.IsNullOrWhiteSpace(registration.ModPackId)
            ? $"plugin.{pluginId}"
            : registration.ModPackId;
        if (CharacterClassCatalog.RuntimeRegistry.TryRegisterGameplayLoadout(registration with { ModPackId = modPackId }, out errorMessage))
        {
            return true;
        }

        _log($"[plugin] gameplay loadout registration rejected for {pluginId}: {errorMessage}");
        return false;
    }

    private bool TryRegisterGameplaySlotItem(string pluginId, GameplaySlotItemRegistration registration, out string errorMessage)
    {
        var modPackId = string.IsNullOrWhiteSpace(registration.ModPackId)
            ? $"plugin.{pluginId}"
            : registration.ModPackId;
        if (CharacterClassCatalog.RuntimeRegistry.TryRegisterGameplaySlotItem(registration with { ModPackId = modPackId }, out errorMessage))
        {
            return true;
        }

        _log($"[plugin] gameplay slot item registration rejected for {pluginId}: {errorMessage}");
        return false;
    }

    private string ResolvePluginDirectory(IOpenGarrisonServerPlugin plugin, string pluginDirectory)
    {
        if (!string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return pluginDirectory;
        }

        return Path.Combine(_pluginsRootDirectory, "Server", plugin.Id);
    }

    private string ResolveConfigDirectory(string pluginId)
    {
        var scopedConfigDirectory = Path.Combine(_pluginConfigRoot, "server", pluginId);
        var legacyConfigDirectory = Path.Combine(_pluginConfigRoot, pluginId);
        return Directory.Exists(legacyConfigDirectory) && !Directory.Exists(scopedConfigDirectory)
            ? legacyConfigDirectory
            : scopedConfigDirectory;
    }

    private PluginLoader.LoadedPlugin? FindLoadedPlugin(string pluginId)
    {
        for (var index = 0; index < _loadedPlugins.Count; index += 1)
        {
            var loadedPlugin = _loadedPlugins[index];
            if (string.Equals(loadedPlugin.Plugin.Id, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return loadedPlugin;
            }
        }

        return null;
    }

    private static OpenGarrisonServerGameplayAbilityInputEvent ToGameplayAbilityInputEvent(WorldGameplayAbilityEvent e)
    {
        return new OpenGarrisonServerGameplayAbilityInputEvent(
            e.Frame,
            e.PlayerId,
            e.ClassId,
            e.Team,
            e.ItemId,
            e.BehaviorId,
            e.AbilityCategory,
            e.Activation,
            e.ExecutorId,
            e.Phase.ToString(),
            e.Tags.ToArray());
    }

    private static OpenGarrisonServerGameplayAbilityUsedEvent ToGameplayAbilityUsedEvent(WorldGameplayAbilityEvent e)
    {
        return new OpenGarrisonServerGameplayAbilityUsedEvent(
            e.Frame,
            e.PlayerId,
            e.ClassId,
            e.Team,
            e.ItemId,
            e.BehaviorId,
            e.AbilityCategory,
            e.Activation,
            e.ExecutorId,
            e.Phase.ToString(),
            e.Tags.ToArray(),
            e.Handled,
            e.ConsumedInput);
    }

    private void Dispatch<THook>(Action<THook> callback) where THook : class
    {
        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.Plugin is not THook hook)
            {
                continue;
            }

            try
            {
                callback(hook);
            }
            catch (Exception ex)
            {
                _log($"[plugin] hook failed for {loadedPlugin.Plugin.Id}: {ex.Message}");
            }
        }
    }

    private OpenGarrisonServerDecisionResult DispatchDecision(
        Func<IOpenGarrisonServerDecisionHooks, OpenGarrisonServerDecisionResult> callback,
        string hookName)
    {
        foreach (var loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.Plugin is not IOpenGarrisonServerDecisionHooks hook)
            {
                continue;
            }

            try
            {
                var decision = callback(hook);
                if (decision.IsCancelled)
                {
                    return decision;
                }
            }
            catch (Exception ex)
            {
                _log($"[plugin] decision hook {hookName} failed for {loadedPlugin.Plugin.Id}: {ex.Message}");
            }
        }

        return OpenGarrisonServerDecisionResult.Continue;
    }
}
