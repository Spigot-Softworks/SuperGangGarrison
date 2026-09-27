using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class MovementSystemTests
{
    [Fact]
    public void JumpInputBufferExpiresAfterFourHeldFailedAttempts()
    {
        var movement = new MovementSystem();
        var player = CreatePlayer(1);

        movement.StartJumpInputBuffer(player);
        Assert.True(movement.HasBufferedJumpInput(player));

        for (var attempt = 0; attempt < 4; attempt += 1)
        {
            movement.AdvanceJumpInputBufferAfterAttempt(player, jumpHeld: true, jumped: false);
        }

        Assert.False(movement.HasBufferedJumpInput(player));
    }

    [Fact]
    public void ReleasingJumpClearsBufferedInputImmediately()
    {
        var movement = new MovementSystem();
        var player = CreatePlayer(2);

        movement.StartJumpInputBuffer(player);
        movement.AdvanceJumpInputBufferAfterAttempt(player, jumpHeld: false, jumped: false);

        Assert.False(movement.HasBufferedJumpInput(player));
    }

    [Fact]
    public void SuccessfulJumpConsumesBufferedInput()
    {
        var movement = new MovementSystem();
        var player = CreatePlayer(3);

        movement.StartJumpInputBuffer(player);
        movement.AdvanceJumpInputBufferAfterAttempt(player, jumpHeld: true, jumped: true);

        Assert.False(movement.HasBufferedJumpInput(player));
    }

    private static PlayerEntity CreatePlayer(int id)
    {
        var player = new PlayerEntity(id, CharacterClassCatalog.Scout, $"Player {id}");
        player.Spawn(PlayerTeam.Red, 0f, 0f);
        return player;
    }
}
