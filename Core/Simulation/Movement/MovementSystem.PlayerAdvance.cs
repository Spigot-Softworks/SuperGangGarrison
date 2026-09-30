namespace OpenGarrison.Core;

public sealed partial class MovementSystem
{
    private const int JumpInputBufferTicks = 4;

    public bool TryApplyGameplayImpulse(int playerId, float velocityX, float velocityY)
    {
        var player = FindPlayerById(playerId);
        if (player is null || !player.IsAlive)
        {
            return false;
        }

        player.ApplyVelocityImpulse(velocityX, velocityY);
        return true;
    }

    public bool TryCancelSpySuperjumpChargeFromJumpInput(PlayerEntity player, bool jumpPressed, bool useAbilityHeld)
    {
        if (!jumpPressed
            || !useAbilityHeld
            || player.ClassId != PlayerClass.Spy
            || player.SpySuperjumpChargeTicks <= 0)
        {
            return false;
        }

        player.CancelSpySuperjumpCharge(blockRestartUntilAbilityRelease: true);
        return true;
    }

    public void StartJumpInputBuffer(PlayerEntity player)
    {
        _jumpInputBufferTicksByPlayerId[player.Id] = JumpInputBufferTicks;
    }

    public bool HasBufferedJumpInput(PlayerEntity player)
    {
        return _jumpInputBufferTicksByPlayerId.TryGetValue(player.Id, out var ticksRemaining)
            && ticksRemaining > 0;
    }

    public void AdvanceJumpInputBufferAfterAttempt(PlayerEntity player, bool jumpHeld, bool jumped)
    {
        if (jumped || !jumpHeld)
        {
            ClearJumpInputBuffer(player);
            return;
        }

        if (!_jumpInputBufferTicksByPlayerId.TryGetValue(player.Id, out var ticksRemaining))
        {
            return;
        }

        ticksRemaining -= 1;
        if (ticksRemaining <= 0)
        {
            ClearJumpInputBuffer(player);
            return;
        }

        _jumpInputBufferTicksByPlayerId[player.Id] = ticksRemaining;
    }

    public void ClearJumpInputBuffer(PlayerEntity player)
    {
        _jumpInputBufferTicksByPlayerId.Remove(player.Id);
    }

    public MovementPreparationResult PreparePlayerMovement(
        PlayerEntity player,
        PlayerInputSnapshot input,
        bool jumpPressed,
        PlayerTeam team,
        bool isHumiliated)
    {
        var startedGrounded = player.PrepareMovement(
            input,
            Level,
            team,
            Config.FixedDeltaSeconds,
            out var canMove,
            isHumiliated,
            HasLandedArrowGroundSupport(player, input.Down));
        var effectiveJumpPressed = jumpPressed || HasBufferedJumpInput(player);
        var jumped = player.TryJumpIfPossible(canMove, effectiveJumpPressed);
        AdvanceJumpInputBufferAfterAttempt(player, input.Up, jumped);
        var emitWallspinDust = player.IsAlive && player.IsPerformingSourceSpinjump(Level);
        if (jumped)
        {
            RegisterWorldSoundEvent("JumpSnd", player.X, player.Y, player.Id);
            TryApplyJumpPadJumpBoostFromPlayerJump(player, jumped);
        }

        return new MovementPreparationResult(
            input,
            jumpPressed,
            startedGrounded,
            jumped,
            emitWallspinDust);
    }

    public void CompletePlayerMovement(
        PlayerEntity player,
        PlayerTeam team,
        bool startedGrounded,
        bool jumped,
        bool allowDropdownFallThrough)
    {
        player.CompleteMovement(
            Level,
            team,
            Config.FixedDeltaSeconds,
            startedGrounded,
            jumped,
            allowDropdownFallThrough);
    }
}
