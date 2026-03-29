using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

public readonly record struct LocalDamageEvent(
    int Amount,
    DamageTargetKind TargetKind,
    int TargetEntityId,
    Vector2 TargetWorldPosition,
    bool TargetWasKilled,
    bool DealtByLocalPlayer,
    bool AssistedByLocalPlayer,
    bool ReceivedByLocalPlayer,
    int AttackerPlayerId,
    int AssistedByPlayerId);
