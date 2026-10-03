namespace OpenGarrison.Core;

internal sealed partial class StructureSystem
{
    internal const float JumpPadBuildCost = 50f;
    internal const float JumpPadBuildProximityRadius = 50f;

    public bool TryBuildLocalJumpPad()
    {
        return TryBuildJumpPad(_host.LocalPlayer);
    }


    internal void DestroyJumpPad(JumpPadEntity pad)
    {
        for (var index = _host.WorldObjects.JumpPads.Count - 1; index >= 0; index -= 1)
        {
            if (!ReferenceEquals(_host.WorldObjects.JumpPads[index], pad))
            {
                continue;
            }

            _host.EntityStore.Remove(pad.Id);
            _host.WorldObjects.JumpPads.RemoveAt(index);
            _host.WorldEffects.RegisterWorldSoundEvent("ExplosionSnd", pad.X, pad.Y);
            _host.WorldEffects.RegisterVisualEffect("Explosion", pad.X, pad.Y);
            SpawnJumpPadGibs(pad.Team, pad.X, pad.Y);
            break;
        }
    }

    internal void SpawnJumpPadGibs(PlayerTeam team, float x, float y)
    {
        var gib = new JumpPadGibEntity(_host.AllocateEntityId(), team, x, y);
        _host.WorldObjects.JumpPadGibs.Add(gib);
        _host.EntityStore.Add(gib);
    }

    internal void AdvanceJumpPadGibs()
    {
        for (var gibIndex = _host.WorldObjects.JumpPadGibs.Count - 1; gibIndex >= 0; gibIndex -= 1)
        {
            var gib = _host.WorldObjects.JumpPadGibs[gibIndex];
            gib.AdvanceOneTick();
            var pickedUp = false;
            foreach (var player in _host.EnumerateSimulatedPlayers())
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

            _host.EntityStore.Remove(gib.Id);
            _host.WorldObjects.JumpPadGibs.RemoveAt(gibIndex);
        }
    }

    internal bool TryBuildJumpPad(PlayerEntity player, bool ignoreMetalCost = false)
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

        foreach (var pad in _host.WorldObjects.JumpPads)
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

        var entity = new JumpPadEntity(_host.AllocateEntityId(), player.Id, player.Team, placementX, placementY);
        _host.WorldObjects.JumpPads.Add(entity);
        _host.EntityStore.Add(entity);
        return true;
    }

    internal void ResetJumpPadSpawnsForLevel()
    {
        _host.WorldObjects.RemoveAll(_host.WorldObjects.JumpPads);
        if (_host.Level.JumpPadSpawns.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _host.Level.JumpPadSpawns.Count; index += 1)
        {
            var marker = _host.Level.JumpPadSpawns[index];
            var pad = new JumpPadEntity(
                _host.AllocateEntityId(),
                0,
                PlayerTeam.Neutral,
                marker.X,
                marker.Y);
            _host.WorldObjects.JumpPads.Add(pad);
            _host.EntityStore.Add(pad);
        }
    }

    internal bool TryDestroyJumpPad(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        var hadPad = false;
        for (var index = _host.WorldObjects.JumpPads.Count - 1; index >= 0; index -= 1)
        {
            if (_host.WorldObjects.JumpPads[index].OwnerPlayerId != player.Id)
            {
                continue;
            }

            hadPad = true;
            DestroyJumpPad(_host.WorldObjects.JumpPads[index]);
        }

        return hadPad;
    }
}
