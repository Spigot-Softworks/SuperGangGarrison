namespace OpenGarrison.Core;

internal sealed partial class DamageRulesSystem
{
    private bool IsRoundEndFriendlyFireActive()
    {
        return _host.MatchState.IsEnded && _host.MatchSettings.RoundEndFriendlyFireEnabled;
    }

    internal bool CanPlayerDamagePlayer(PlayerEntity attacker, PlayerEntity target)
    {
        return CanTeamDamagePlayer(attacker.Team, attacker.Id, target);
    }

    internal bool CanTeamDamagePlayer(PlayerTeam attackerTeam, int attackerId, PlayerEntity target)
    {
        if (!target.IsAlive)
        {
            return false;
        }

        if (attackerId == target.Id)
        {
            var selfAttacker = _host.FindPlayerById(attackerId);
            return selfAttacker is null
                || !_host.GetLastToDieGameplaySettings(selfAttacker).DisableSelfDamage;
        }

        var attacker = _host.FindPlayerById(attackerId);
        return attackerTeam != target.Team
            || IsRoundEndFriendlyFireActive()
            || (attacker is not null && IsExperimentalConfusionFriendlyFireAllowed(attacker, target));
    }

    private bool IsExperimentalConfusionFriendlyFireAllowed(PlayerEntity attacker, PlayerEntity target)
    {
        return _host.GetLastToDieGameplaySettings(attacker).EnableEngineerConfusionField
            && attacker.Team == target.Team
            && attacker.Id != target.Id
            && (attacker.ExperimentalConfusedAttackTargetPlayerId == target.Id
                || target.IsExperimentalConfusionRetaliationMarked);
    }

    internal int ScaleConfiguredDamage(int damage)
    {
        if (damage <= 0)
        {
            return 0;
        }

        var scaledDamage = damage * _host.MatchSettings.DamageScale;
        return scaledDamage <= 0f
            ? 0
            : Math.Max(1, (int)MathF.Ceiling(scaledDamage));
    }

    internal float ScaleConfiguredDamage(float damage)
    {
        if (damage <= 0f)
        {
            return 0f;
        }

        return damage * _host.MatchSettings.DamageScale;
    }
}
