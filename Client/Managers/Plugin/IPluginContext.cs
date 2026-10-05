#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IPluginContext
{
    Game1.GameplayHudCanvas CreateGameplayHudCanvas(Microsoft.Xna.Framework.Vector2 cameraTopLeft);
    Game1.ClientPluginStateView CreateClientPluginStateView();
    GameplayRuntimeSettings GameplayRuntimeSettings { get; }
    OpenGarrison.Client.ClientPluginHost _clientPluginHost { get; set; }
    Microsoft.Xna.Framework.Vector2 _gameplayCameraTopLeft { get; set; }
    bool _hasGameplayCameraTopLeft { get; set; }
    Nullable<int> _localPlayerSnapshotEntityId { get; set; }
    bool _mainMenuOpen { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
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
    void ObserveLocalPainDamage(int damageAmount, bool wasFatal, OpenGarrison.Core.DamageEventFlags flags);
    void TriggerLocalHudPortraitDamageFeedback(int damageAmount);
}
