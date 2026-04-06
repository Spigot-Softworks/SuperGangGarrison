using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientReadOnlyState
{
    bool IsConnected { get; }

    bool IsMainMenuOpen { get; }

    bool IsGameplayActive { get; }

    bool IsGameplayInputBlocked { get; }

    bool IsSpectator { get; }

    bool IsDeathCamActive { get; }

    ulong WorldFrame { get; }

    int TickRate { get; }

    int LocalPingMilliseconds { get; }

    string LevelName { get; }

    float LevelWidth { get; }

    float LevelHeight { get; }

    int ViewportWidth { get; }

    int ViewportHeight { get; }

    int? LocalPlayerId { get; }

    ClientPluginTeam LocalPlayerTeam { get; }

    ClientPluginClass LocalPlayerClass { get; }

    bool IsLocalPlayerAlive { get; }

    bool IsLocalPlayerScoped { get; }

    bool IsLocalPlayerHealing { get; }

    Vector2 CameraTopLeft { get; }

    bool TryGetLocalPlayerHealth(out int health, out int maxHealth);

    bool TryGetLocalPlayerWorldPosition(out Vector2 position);

    bool TryGetPlayerWorldPosition(int playerId, out Vector2 position);

    bool IsPlayerVisibleToLocalViewer(int playerId);

    bool IsPlayerCloaked(int playerId);

    bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value);

    bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value);

    bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value);

    bool WasKeyPressedThisFrame(Keys key);

    IReadOnlyList<ClientPlayerMarker> GetPlayerMarkers();

    IReadOnlyList<ClientSentryMarker> GetSentryMarkers();

    IReadOnlyList<ClientObjectiveMarker> GetObjectiveMarkers();
}
