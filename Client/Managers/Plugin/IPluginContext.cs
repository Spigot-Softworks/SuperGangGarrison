#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IPluginContext
{
    Game1.GameplayHudCanvas CreateGameplayHudCanvas(Microsoft.Xna.Framework.Vector2 cameraTopLeft);
    Game1.ClientPluginStateView CreateClientPluginStateView();
    int _bloodRenderMode { get; set; }
    OpenGarrison.Client.ClientPluginHost _clientPluginHost { get; set; }
    ref ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> _clientPluginPreviousBlueIntelState { get; }
    Dictionary<OpenGarrison.Core.PlayerTeam, ValueTuple<int, int, bool>> _clientPluginPreviousGeneratorStates { get; }
    int _clientPluginPreviousKillFeedCount { get; set; }
    bool _clientPluginPreviousLocalAlive { get; set; }
    int _clientPluginPreviousLocalAmmo { get; set; }
    bool _clientPluginPreviousLocalBurning { get; set; }
    bool _clientPluginPreviousLocalCarryingIntel { get; set; }
    int _clientPluginPreviousLocalPrimaryCooldownTicks { get; set; }
    OpenGarrison.Client.Plugins.ClientRoundPhase _clientPluginPreviousMatchPhase { get; set; }
    Dictionary<int, ValueTuple<OpenGarrison.Client.Plugins.ClientPluginTeam, OpenGarrison.Client.Plugins.ClientPluginTeam, float, bool>> _clientPluginPreviousObjectiveStates { get; }
    ref ValueTuple<bool, bool, OpenGarrison.Client.Plugins.ClientPluginTeam, float, float, float> _clientPluginPreviousRedIntelState { get; }
    OpenGarrison.Client.Game1.ClientPluginStateView _clientPluginStateView { get; set; }
    Microsoft.Xna.Framework.Vector2 _gameplayCameraTopLeft { get; set; }
    bool _hasGameplayCameraTopLeft { get; set; }
    Nullable<int> _localPlayerSnapshotEntityId { get; set; }
    bool _mainMenuOpen { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    List<OpenGarrison.Protocol.SnapshotDamageEvent> _pendingNetworkDamageEvents { get; }
    HashSet<ulong> _processedNetworkDamageEventIds { get; }
    Queue<ulong> _processedNetworkDamageEventOrder { get; }
    OpenGarrison.Client.GameMakerRuntimeAssetCache _runtimeAssets { get; set; }
    bool _startupSplashOpen { get; set; }
    OpenGarrison.Core.SimulationWorld _world { get; set; }
    bool AreBloodVisualsEnabled { get; }
    int ViewportHeight { get; }
    int ViewportWidth { get; }
    void AddConsoleLine(string line);
    OpenGarrison.Client.ClientPluginHost CreateClientPluginHost(string pluginsDirectory, string pluginConfigRoot, string pluginStatePath);
    IEnumerable<OpenGarrison.Core.PlayerEntity> EnumerateRemotePlayersForView();
    Nullable<int> GetClientPluginLocalPlayerId();
    Microsoft.Xna.Framework.Input.MouseState GetFrameMouseState();
    Microsoft.Xna.Framework.Vector2 GetRenderPosition(int entityId, float x, float y, bool allowInterpolation = true);
    Microsoft.Xna.Framework.Vector2 GetRenderPosition(OpenGarrison.Core.PlayerEntity player, bool allowInterpolation = true);
    Microsoft.Xna.Framework.Vector2 GetUntrackedCameraTopLeft(int viewportWidth, int viewportHeight, int mouseX, int mouseY);
    bool IsClientPerformanceDiagnosticsEnabled();
    bool IsLocalSpectatorPresentationActive();
    void ObserveCivvieUmbrellaShieldBlockDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent);
    void ObserveCivvieUmbrellaShieldBlockDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent);
    void ObserveDynamicMusicDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent);
    void ObserveDynamicMusicDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent);
    void ObserveEvasionMissDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent);
    void ObserveEvasionMissDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent);
    void ObserveHeavyDashDodgeDamageEvent(OpenGarrison.Core.WorldDamageEvent damageEvent);
    void ObserveHeavyDashDodgeDamageEvent(OpenGarrison.Protocol.SnapshotDamageEvent damageEvent);
    void QueueImmediateNetworkDeathPresentation(OpenGarrison.Protocol.SnapshotMessage resolvedSnapshot, OpenGarrison.Protocol.SnapshotDamageEvent damageEvent);
    void RecordClientPerformanceMetric(OpenGarrison.Client.Game1.ClientPerformanceMetric metric, double milliseconds);
    void RegisterLastToDieLocalDamageDealt(int amount);
    void ResetClientPluginGameplayEventState();
    void TriggerLocalHudDamageVignette(int damageAmount);
    void TriggerLocalHudPortraitDamageFeedback(int damageAmount);
}
