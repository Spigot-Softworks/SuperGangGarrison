#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IPluginContext
{
    Game1.GameplayHudCanvas IPluginContext.CreateGameplayHudCanvas(Microsoft.Xna.Framework.Vector2 cameraTopLeft)
        => new(this, cameraTopLeft);

    Game1.ClientPluginStateView IPluginContext.CreateClientPluginStateView()
        => new(this);

    int IPluginContext._bloodRenderMode { get => _bloodRenderMode; set => _bloodRenderMode = value; }

    OpenGarrison.Client.ClientPluginHost IPluginContext._clientPluginHost { get => _clientPluginHost; set => _clientPluginHost = value; }

    ref ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> IPluginContext._clientPluginPreviousBlueIntelState => ref _clientPluginPreviousBlueIntelState;

    Dictionary<OpenGarrison.Core.PlayerTeam, ValueTuple<int, int, bool>> IPluginContext._clientPluginPreviousGeneratorStates { get => _clientPluginPreviousGeneratorStates; }

    int IPluginContext._clientPluginPreviousKillFeedCount { get => _clientPluginPreviousKillFeedCount; set => _clientPluginPreviousKillFeedCount = value; }

    bool IPluginContext._clientPluginPreviousLocalAlive { get => _clientPluginPreviousLocalAlive; set => _clientPluginPreviousLocalAlive = value; }

    int IPluginContext._clientPluginPreviousLocalAmmo { get => _clientPluginPreviousLocalAmmo; set => _clientPluginPreviousLocalAmmo = value; }

    bool IPluginContext._clientPluginPreviousLocalBurning { get => _clientPluginPreviousLocalBurning; set => _clientPluginPreviousLocalBurning = value; }

    bool IPluginContext._clientPluginPreviousLocalCarryingIntel { get => _clientPluginPreviousLocalCarryingIntel; set => _clientPluginPreviousLocalCarryingIntel = value; }

    int IPluginContext._clientPluginPreviousLocalPrimaryCooldownTicks { get => _clientPluginPreviousLocalPrimaryCooldownTicks; set => _clientPluginPreviousLocalPrimaryCooldownTicks = value; }

    OpenGarrison.Client.Plugins.ClientRoundPhase IPluginContext._clientPluginPreviousMatchPhase { get => _clientPluginPreviousMatchPhase; set => _clientPluginPreviousMatchPhase = value; }

    Dictionary<int, ValueTuple<OpenGarrison.Client.Plugins.ClientPluginTeam, OpenGarrison.Client.Plugins.ClientPluginTeam, float, bool>> IPluginContext._clientPluginPreviousObjectiveStates { get => _clientPluginPreviousObjectiveStates; }

    ref ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> IPluginContext._clientPluginPreviousRedIntelState => ref _clientPluginPreviousRedIntelState;

    OpenGarrison.Client.Game1.ClientPluginStateView IPluginContext._clientPluginStateView { get => _clientPluginStateView; set => _clientPluginStateView = value; }

    Microsoft.Xna.Framework.Vector2 IPluginContext._gameplayCameraTopLeft { get => _gameplayCameraTopLeft; set => _gameplayCameraTopLeft = value; }

    bool IPluginContext._hasGameplayCameraTopLeft { get => _hasGameplayCameraTopLeft; set => _hasGameplayCameraTopLeft = value; }

    Nullable<int> IPluginContext._localPlayerSnapshotEntityId { get => _localPlayerSnapshotEntityId; set => _localPlayerSnapshotEntityId = value; }

    bool IPluginContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }

    OpenGarrison.Client.NetworkGameClient IPluginContext._networkClient { get => _networkClient; }

    List<OpenGarrison.Protocol.SnapshotDamageEvent> IPluginContext._pendingNetworkDamageEvents { get => _pendingNetworkDamageEvents; }

    HashSet<ulong> IPluginContext._processedNetworkDamageEventIds { get => _processedNetworkDamageEventIds; }

    Queue<ulong> IPluginContext._processedNetworkDamageEventOrder { get => _processedNetworkDamageEventOrder; }

    OpenGarrison.Client.GameMakerRuntimeAssetCache IPluginContext._runtimeAssets { get => _runtimeAssets; set => _runtimeAssets = value; }

    bool IPluginContext._startupSplashOpen { get => _startupSplashOpen; set => _startupSplashOpen = value; }

    OpenGarrison.Core.SimulationWorld IPluginContext._world { get => _world; set => _world = value; }

    bool IPluginContext.AreBloodVisualsEnabled { get => AreBloodVisualsEnabled; }

    int IPluginContext.ViewportHeight { get => ViewportHeight; }

    int IPluginContext.ViewportWidth { get => ViewportWidth; }

    void IPluginContext.AddConsoleLine(string line) { AddConsoleLine(line); }

    OpenGarrison.Client.ClientPluginHost IPluginContext.CreateClientPluginHost(string pluginsDirectory, string pluginConfigRoot, string pluginStatePath) => CreateClientPluginHost(pluginsDirectory, pluginConfigRoot, pluginStatePath);

    IEnumerable<OpenGarrison.Core.PlayerEntity> IPluginContext.EnumerateRemotePlayersForView() => EnumerateRemotePlayersForView();

    Nullable<int> IPluginContext.GetClientPluginLocalPlayerId() => GetClientPluginLocalPlayerId();

    Microsoft.Xna.Framework.Input.MouseState IPluginContext.GetFrameMouseState() => GetFrameMouseState();

    Microsoft.Xna.Framework.Vector2 IPluginContext.GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true) => GetRenderPosition(entityId, x, y, allowInterpolation);

    Microsoft.Xna.Framework.Vector2 IPluginContext.GetRenderPosition(OpenGarrison.Core.PlayerEntity player, bool allowInterpolation = true) => GetRenderPosition(player, allowInterpolation);

    Microsoft.Xna.Framework.Vector2 IPluginContext.GetUntrackedCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY) => GetUntrackedCameraTopLeft(viewportWidth, viewportHeight, mouseX, mouseY);

    bool IPluginContext.IsClientPerformanceDiagnosticsEnabled() => IsClientPerformanceDiagnosticsEnabled();

    bool IPluginContext.IsLocalSpectatorPresentationActive() => IsLocalSpectatorPresentationActive();

    void IPluginContext.ObserveCivvieUmbrellaShieldBlockDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveCivvieUmbrellaShieldBlockDamageEvent(damageEvent); }

    void IPluginContext.ObserveCivvieUmbrellaShieldBlockDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveCivvieUmbrellaShieldBlockDamageEvent(damageEvent); }

    void IPluginContext.ObserveDynamicMusicDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveDynamicMusicDamageEvent(damageEvent); }

    void IPluginContext.ObserveDynamicMusicDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveDynamicMusicDamageEvent(damageEvent); }

    void IPluginContext.ObserveEvasionMissDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveEvasionMissDamageEvent(damageEvent); }

    void IPluginContext.ObserveEvasionMissDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveEvasionMissDamageEvent(damageEvent); }

    void IPluginContext.ObserveHeavyDashDodgeDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent) { ObserveHeavyDashDodgeDamageEvent(damageEvent); }

    void IPluginContext.ObserveHeavyDashDodgeDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { ObserveHeavyDashDodgeDamageEvent(damageEvent); }

    void IPluginContext.QueueImmediateNetworkDeathPresentation(OpenGarrison.Protocol.SnapshotMessage resolvedSnapshot, OpenGarrison.Protocol.SnapshotDamageEvent damageEvent) { QueueImmediateNetworkDeathPresentation(resolvedSnapshot, damageEvent); }

    void IPluginContext.RecordClientPerformanceMetric(OpenGarrison.Client.Game1.ClientPerformanceMetric metric, double milliseconds) { RecordClientPerformanceMetric(metric, milliseconds); }

    void IPluginContext.RegisterLastToDieLocalDamageDealt(int amount) { RegisterLastToDieLocalDamageDealt(amount); }

    void IPluginContext.ResetClientPluginGameplayEventState() { ResetClientPluginGameplayEventState(); }

    void IPluginContext.TriggerLocalHudDamageVignette(int damageAmount) { TriggerLocalHudDamageVignette(damageAmount); }

    void IPluginContext.TriggerLocalHudPortraitDamageFeedback(int damageAmount) { TriggerLocalHudPortraitDamageFeedback(damageAmount); }

}
