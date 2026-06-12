#nullable enable

using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ObserveCivvieUmbrellaShieldBlockDamageEvent(WorldDamageEvent damageEvent)
    {
        if (!IsCivvieUmbrellaShieldBlockEvent(damageEvent.Flags, damageEvent.TargetKind))
        {
            return;
        }

        TryTriggerCivvieUmbrellaShieldBlockVisual(damageEvent.TargetEntityId, damageEvent.X, damageEvent.Y);
    }

    private void ObserveCivvieUmbrellaShieldBlockDamageEvent(SnapshotDamageEvent damageEvent)
    {
        if (!IsCivvieUmbrellaShieldBlockEvent((DamageEventFlags)damageEvent.Flags, (DamageTargetKind)damageEvent.TargetKind))
        {
            return;
        }

        TryTriggerCivvieUmbrellaShieldBlockVisual(damageEvent.TargetEntityId, damageEvent.X, damageEvent.Y);
    }

    private static bool IsCivvieUmbrellaShieldBlockEvent(DamageEventFlags flags, DamageTargetKind targetKind)
    {
        return targetKind == DamageTargetKind.Player
            && flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock);
    }

    private void TryTriggerCivvieUmbrellaShieldBlockVisual(int targetPlayerId, float x, float y)
    {
        if (FindPlayerById(targetPlayerId) is not { } targetPlayer
            || !targetPlayer.IsAlive)
        {
            return;
        }

        const int maxBlockVisualsPerPlayer = 6;
        var count = 0;
        for (var index = _civvieUmbrellaShieldBlockVisuals.Count - 1; index >= 0; index -= 1)
        {
            if (_civvieUmbrellaShieldBlockVisuals[index].PlayerId != targetPlayerId)
            {
                continue;
            }

            count += 1;
            if (count >= maxBlockVisualsPerPlayer)
            {
                _civvieUmbrellaShieldBlockVisuals.RemoveAt(index);
            }
        }

        _civvieUmbrellaShieldBlockVisuals.Add(new CivvieUmbrellaShieldBlockVisual(targetPlayerId, x, y));
    }
}
