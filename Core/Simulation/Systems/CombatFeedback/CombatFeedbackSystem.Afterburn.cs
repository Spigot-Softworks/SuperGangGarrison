namespace OpenGarrison.Core;

internal sealed partial class CombatFeedbackSystem
{
    private const int BurnAlertChatBubbleFrame = 49;
    private const int BurnAlertRollModulus = 80;
    private const int BurnAlertSourceFrameMultiplier = 37;
    private const int BurnAlertPlayerIdMultiplier = 11;


    internal void AdvanceAfterburnAlertBubbles()
    {
        var currentSourceFrame = GetCurrentSourceFrame();
        if (currentSourceFrame == _host.CombatRuntime.LastAfterburnAlertSourceFrame)
        {
            return;
        }

        _host.CombatRuntime.LastAfterburnAlertSourceFrame = currentSourceFrame;
        foreach (var player in _host.EnumerateSimulatedPlayers())
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

    internal void SpawnAirblastExtinguishFlames(PlayerEntity attacker, PlayerEntity target, float aimRadians)
    {
        var flameCount = target.BurnVisualCount;
        if (flameCount <= 0)
        {
            return;
        }

        var currentSourceFrame = GetCurrentSourceFrame();
        var directionX = DeterministicMath.Cos(aimRadians);
        var directionY = DeterministicMath.Sin(aimRadians);
        for (var flameIndex = 0; flameIndex < flameCount; flameIndex += 1)
        {
            target.GetBurnVisualOffset(flameIndex, currentSourceFrame, out var offsetX, out var offsetY);
            var spawnX = target.X + offsetX;
            var spawnY = target.Y + offsetY;
            var flameSpeed = 6.5f + (_host.Randoms.Gameplay.NextSingle() * 2.5f);
            _host.SpawnFlame(
                attacker,
                spawnX,
                spawnY,
                directionX * flameSpeed,
                directionY * flameSpeed);
        }
    }

    private int GetCurrentSourceFrame()
    {
        return (int)((_host.Frame * LegacyMovementModel.SourceTicksPerSecond) / _host.Config.TicksPerSecond);
    }
}
