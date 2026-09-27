namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float JumpPadBuildCost = 50f;
    private const float JumpPadBuildProximityRadius = 50f;

    public bool TryBuildLocalJumpPad()
    {
        return TryBuildJumpPad(LocalPlayer);
    }

    public bool TryDestroyLocalJumpPad()
    {
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            var pad = _jumpPads[index];
            if (pad.OwnerPlayerId != LocalPlayer.Id)
            {
                continue;
            }

            DestroyJumpPad(pad);
            return true;
        }

        return false;
    }

    private void DestroyJumpPad(JumpPadEntity pad)
    {
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            if (!ReferenceEquals(_jumpPads[index], pad))
            {
                continue;
            }

            EntityStore.Remove(pad.Id);
            _jumpPads.RemoveAt(index);
            RegisterWorldSoundEvent("ExplosionSnd", pad.X, pad.Y);
            RegisterVisualEffect("Explosion", pad.X, pad.Y);
            SpawnJumpPadGibs(pad.Team, pad.X, pad.Y);
            break;
        }
    }

    private void SpawnJumpPadGibs(PlayerTeam team, float x, float y)
    {
        var gib = new JumpPadGibEntity(AllocateEntityId(), team, x, y);
        _jumpPadGibs.Add(gib);
        EntityStore.Add(gib);
    }

    private void AdvanceJumpPadGibs()
    {
        for (var gibIndex = _jumpPadGibs.Count - 1; gibIndex >= 0; gibIndex -= 1)
        {
            var gib = _jumpPadGibs[gibIndex];
            gib.AdvanceOneTick();
            var pickedUp = false;
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.ClassId != PlayerClass.Engineer || player.Metal >= player.MaxMetal)
                {
                    continue;
                }

                if (!player.IntersectsMarker(gib.X, gib.Y, JumpPadGibEntity.PickupRadius, JumpPadGibEntity.PickupRadius))
                {
                    continue;
                }

                player.AddMetal(JumpPadGibEntity.MetalValue);
                pickedUp = true;
                break;
            }

            if (!pickedUp && !gib.IsExpired)
            {
                continue;
            }

            EntityStore.Remove(gib.Id);
            _jumpPadGibs.RemoveAt(gibIndex);
        }
    }

    private bool TryBuildJumpPad(PlayerEntity player, bool ignoreMetalCost = false)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Engineer
            || player.IsInSpawnRoom)
        {
            return false;
        }

        if (!TryResolveStructurePlacement(player.X, player.Y, JumpPadEntity.Width, JumpPadEntity.Height, out var placementX, out var placementY))
        {
            return false;
        }

        foreach (var pad in _jumpPads)
        {
            if (pad.OwnerPlayerId == player.Id)
            {
                return false;
            }

            if (pad.IsNear(placementX, placementY, JumpPadBuildProximityRadius))
            {
                return false;
            }
        }

        if (!ignoreMetalCost && !player.SpendMetal(JumpPadBuildCost))
        {
            return false;
        }

        var entity = new JumpPadEntity(AllocateEntityId(), player.Id, player.Team, placementX, placementY);
        _jumpPads.Add(entity);
        EntityStore.Add(entity);
        return true;
    }

    private void ResetJumpPadSpawnsForLevel()
    {
        RemoveEntities(_jumpPads);
        if (Level.JumpPadSpawns.Count == 0)
        {
            return;
        }

        for (var index = 0; index < Level.JumpPadSpawns.Count; index += 1)
        {
            var marker = Level.JumpPadSpawns[index];
            var pad = new JumpPadEntity(
                AllocateEntityId(),
                0,
                PlayerTeam.Neutral,
                marker.X,
                marker.Y);
            _jumpPads.Add(pad);
            EntityStore.Add(pad);
        }
    }

    private bool TryDestroyJumpPad(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        var hadPad = false;
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            if (_jumpPads[index].OwnerPlayerId != player.Id)
            {
                continue;
            }

            hadPad = true;
            DestroyJumpPad(_jumpPads[index]);
        }

        return hadPad;
    }
}
