namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float JumpPadBuildCost = 50f;
    private const float JumpPadBuildProximityRadius = 50f;
    private const float JumpPadJumpBoostMultiplier = 1.85f;
    private const float JumpPadLaunchEpsilon = 0.01f;

    private static long GetJumpPadTriggerContactKey(int playerId, int padId)
    {
        return ((long)playerId << 32) | (uint)padId;
    }

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

    internal void AdvanceJumpPads()
    {
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            var pad = _jumpPads[index];
            var owner = FindPlayerById(pad.OwnerPlayerId);
            if (owner is null || owner.ClassId != PlayerClass.Engineer || owner.Team != pad.Team)
            {
                DestroyJumpPad(pad);
                continue;
            }

            var wasLanded = pad.HasLanded;
            pad.Advance(Level, Bounds);
            if (!wasLanded && pad.HasLanded)
            {
                RegisterWorldSoundEvent("SentryFloorSnd", pad.X, pad.Y);
            }

            if (pad.IsDead)
            {
                DestroyJumpPad(pad);
            }
        }
    }

    private bool TryApplyJumpPadJumpBoostFromPlayerJump(PlayerEntity player, bool jumped)
    {
        if (!jumped || !player.IsAlive || player.VerticalSpeed >= 0f)
        {
            return false;
        }

        var pad = FindUsableJumpPadTouchingPlayer(player);
        if (pad is null)
        {
            return false;
        }

        return TryApplyJumpPadLaunchImpulse(player);
    }

    private bool TryApplyJumpPadLaunchImpulse(PlayerEntity player)
    {
        var boostedVerticalSpeed = -player.JumpSpeed * JumpPadJumpBoostMultiplier;
        if (player.VerticalSpeed <= boostedVerticalSpeed + JumpPadLaunchEpsilon)
        {
            return false;
        }

        var extraVerticalImpulse = boostedVerticalSpeed - player.VerticalSpeed;
        player.AddImpulse(0f, extraVerticalImpulse);
        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", player.X, player.Y - 8f, 270f);
        return true;
    }

    private JumpPadEntity? FindUsableJumpPadTouchingPlayer(PlayerEntity player)
    {
        for (var index = 0; index < _jumpPads.Count; index += 1)
        {
            var pad = _jumpPads[index];
            if (!IsJumpPadTriggerActive(pad)
                || !CanUseJumpPad(player, pad)
                || !IsPlayerInJumpPadTriggerArea(player, pad))
            {
                continue;
            }

            return pad;
        }

        return null;
    }

    private void HandleJumpPadTriggerTouch(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return;
        }

        for (var index = 0; index < _jumpPads.Count; index += 1)
        {
            var pad = _jumpPads[index];
            var key = GetJumpPadTriggerContactKey(player.Id, pad.Id);
            var inTrigger = IsJumpPadTriggerActive(pad)
                && CanUseJumpPad(player, pad)
                && IsPlayerInJumpPadTriggerArea(player, pad);

            if (!inTrigger)
            {
                _jumpPadTriggerContacts.Remove(key);
                continue;
            }

            _jumpPadTriggerContacts.Add(key);
        }
    }

    private static bool IsJumpPadTriggerActive(JumpPadEntity pad)
    {
        return pad.HasLanded && !pad.IsDead;
    }

    private static bool CanUseJumpPad(PlayerEntity player, JumpPadEntity pad)
    {
        if (player.Id == pad.OwnerPlayerId)
        {
            return true;
        }

        if (player.Team == pad.Team)
        {
            return true;
        }

        return player.ClassId == PlayerClass.Spy;
    }

    private static bool IsPlayerInJumpPadTriggerArea(PlayerEntity player, JumpPadEntity pad)
    {
        return player.IntersectsMarker(pad.X, pad.Y, JumpPadEntity.Width, JumpPadEntity.Height);
    }

    private void DestroyJumpPad(JumpPadEntity pad)
    {
        for (var index = _jumpPads.Count - 1; index >= 0; index -= 1)
        {
            if (!ReferenceEquals(_jumpPads[index], pad))
            {
                continue;
            }

            _entities.Remove(pad.Id);
            _jumpPads.RemoveAt(index);
            var keyMask = (uint)pad.Id;
            _jumpPadTriggerContacts.RemoveWhere(key => (key & 0xFFFFFFFF) == keyMask);
            RegisterWorldSoundEvent("ExplosionSnd", pad.X, pad.Y);
            RegisterVisualEffect("Explosion", pad.X, pad.Y);
            break;
        }
    }

    private bool TryBuildJumpPad(PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Engineer
            || player.IsInSpawnRoom)
        {
            return false;
        }

        foreach (var pad in _jumpPads)
        {
            if (pad.OwnerPlayerId == player.Id)
            {
                return false;
            }

            if (pad.IsNear(player.X, player.Y, JumpPadBuildProximityRadius))
            {
                return false;
            }
        }

        if (!player.SpendMetal(JumpPadBuildCost))
        {
            return false;
        }

        var entity = new JumpPadEntity(AllocateEntityId(), player.Id, player.Team, player.X, player.Y);
        _jumpPads.Add(entity);
        _entities.Add(entity.Id, entity);
        return true;
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
