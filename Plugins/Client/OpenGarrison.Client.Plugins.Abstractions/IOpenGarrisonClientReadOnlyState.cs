using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientReadOnlyState
{
    bool IsConnected { get; }

    bool IsMainMenuOpen { get; }

    bool IsGameplayActive { get; }

    bool IsSpectator { get; }

    bool IsDeathCamActive { get; }

    ulong WorldFrame { get; }

    int TickRate { get; }

    string LevelName { get; }

    int ViewportWidth { get; }

    int ViewportHeight { get; }

    int? LocalPlayerId { get; }

    Vector2 CameraTopLeft { get; }

    bool TryGetPlayerWorldPosition(int playerId, out Vector2 position);
}
