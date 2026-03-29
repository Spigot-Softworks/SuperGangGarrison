namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int BurnAlertChatBubbleFrame = 49;
    private const int BurnAlertRollModulus = 80;
    private const int BurnAlertSourceFrameMultiplier = 37;
    private const int BurnAlertPlayerIdMultiplier = 11;

    private int _lastAfterburnAlertSourceFrame = -1;

    private void AdvanceAfterburnAlertBubbles()
    {
        var currentSourceFrame = GetCurrentSourceFrame();
        if (currentSourceFrame == _lastAfterburnAlertSourceFrame)
        {
            return;
        }

        _lastAfterburnAlertSourceFrame = currentSourceFrame;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.ClassId == PlayerClass.Pyro || player.BurnDurationSourceTicks <= 0f)
            {
                continue;
            }

            if (ShouldTriggerAfterburnAlertBubble(player.Id, currentSourceFrame))
            {
                player.TriggerChatBubble(BurnAlertChatBubbleFrame);
            }
        }
    }

    private static bool ShouldTriggerAfterburnAlertBubble(int playerId, int sourceFrame)
    {
        var roll = (sourceFrame * BurnAlertSourceFrameMultiplier) + (playerId * BurnAlertPlayerIdMultiplier);
        return roll % BurnAlertRollModulus <= 1;
    }

    private void SpawnAirblastExtinguishFlames(PlayerEntity attacker, PlayerEntity target, float aimRadians)
    {
        var flameCount = target.BurnVisualCount;
        if (flameCount <= 0)
        {
            return;
        }

        var currentSourceFrame = GetCurrentSourceFrame();
        var directionX = MathF.Cos(aimRadians);
        var directionY = MathF.Sin(aimRadians);
        for (var flameIndex = 0; flameIndex < flameCount; flameIndex += 1)
        {
            target.GetBurnVisualOffset(flameIndex, currentSourceFrame, out var offsetX, out var offsetY);
            var spawnX = target.X + offsetX;
            var spawnY = target.Y + offsetY;
            var flameSpeed = 6.5f + (_random.NextSingle() * 2.5f);
            SpawnFlame(
                attacker,
                spawnX,
                spawnY,
                directionX * flameSpeed,
                directionY * flameSpeed);
        }
    }

    private int GetCurrentSourceFrame()
    {
        return (int)((Frame * LegacyMovementModel.SourceTicksPerSecond) / Config.TicksPerSecond);
    }
}
