namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool IsRoundEndFriendlyFireActive()
    {
        return MatchState.IsEnded && _roundEndFriendlyFireEnabled;
    }

    private bool CanPlayerDamagePlayer(PlayerEntity attacker, PlayerEntity target)
    {
        return CanTeamDamagePlayer(attacker.Team, attacker.Id, target);
    }

    private bool CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target)
    {
        if (!target.IsAlive)
        {
            return false;
        }

        return attackerId == target.Id
            || attackerTeam != target.Team
            || IsRoundEndFriendlyFireActive();
    }

    private int ScaleConfiguredDamage(int damage)
    {
        if (damage <= 0)
        {
            return 0;
        }

        var scaledDamage = damage * _configuredDamageScale;
        return scaledDamage <= 0f
            ? 0
            : Math.Max(1, (int)MathF.Ceiling(scaledDamage));
    }

    private float ScaleConfiguredDamage(float damage)
    {
        if (damage <= 0f)
        {
            return 0f;
        }

        return damage * _configuredDamageScale;
    }
}
