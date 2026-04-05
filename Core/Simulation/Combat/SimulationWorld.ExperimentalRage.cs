using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private int _experimentalRageEnemyHumiliationTicksRemaining;

    private int GetExperimentalRageDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.RageDurationSeconds));
    }

    private bool CanUseExperimentalRage(PlayerEntity? player)
    {
        return player is not null
            && ExperimentalGameplaySettings.EnableRage
            && IsExperimentalPracticePowerOwner(player)
            && (player.ClassId == PlayerClass.Soldier
                || player.IsExperimentalDemoknightEnabled);
    }

    private bool TryHandleExperimentalRageActivation(PlayerEntity player)
    {
        if (!CanUseExperimentalRage(player))
        {
            return false;
        }

        var durationTicks = GetExperimentalRageDurationTicks();
        if (!player.TryStartRage(durationTicks))
        {
            return false;
        }

        player.RefreshUber();
        _experimentalRageEnemyHumiliationTicksRemaining = Math.Max(
            _experimentalRageEnemyHumiliationTicksRemaining,
            durationTicks);
        return true;
    }

    private void ApplyExperimentalRageEffects()
    {
        if (!ExperimentalGameplaySettings.EnableRage)
        {
            _experimentalRageEnemyHumiliationTicksRemaining = 0;
            return;
        }

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (player.IsRaging)
            {
                player.RefreshUber();
            }
        }
    }

    private void AdvanceExperimentalRageState()
    {
        if (_experimentalRageEnemyHumiliationTicksRemaining > 0)
        {
            _experimentalRageEnemyHumiliationTicksRemaining -= 1;
            if (_experimentalRageEnemyHumiliationTicksRemaining < 0)
            {
                _experimentalRageEnemyHumiliationTicksRemaining = 0;
            }
        }

        foreach (var player in EnumerateSimulatedPlayers())
        {
            player.AdvanceRageState();
        }
    }

    private bool IsExperimentalRageHumiliationActiveForPlayer(PlayerEntity player)
    {
        return ExperimentalGameplaySettings.EnableRage
            && _experimentalRageEnemyHumiliationTicksRemaining > 0
            && !ReferenceEquals(player, LocalPlayer)
            && player.Team != LocalPlayer.Team;
    }
}
