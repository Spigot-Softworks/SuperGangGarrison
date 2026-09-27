using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// A damage event observed by the local client.
/// </summary>
/// <param name="Amount">The damage amount.</param>
/// <param name="TargetKind">The kind of entity that was damaged.</param>
/// <param name="TargetEntityId">The damaged entity id.</param>
/// <param name="TargetWorldPosition">The target's world position.</param>
/// <param name="TargetWasKilled">Whether the damage killed the target.</param>
/// <param name="DealtByLocalPlayer">Whether the local player dealt the damage.</param>
/// <param name="AssistedByLocalPlayer">Whether the local player assisted the damage.</param>
/// <param name="ReceivedByLocalPlayer">Whether the local player received the damage.</param>
/// <param name="AttackerPlayerId">The attacking player id.</param>
/// <param name="AssistedByPlayerId">The assisting player id.</param>
/// <param name="Flags">Additional damage flags.</param>
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
    int AssistedByPlayerId,
    LocalDamageFlags Flags = LocalDamageFlags.None);
