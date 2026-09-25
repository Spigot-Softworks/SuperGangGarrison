namespace OpenGarrison.Core;

using System;

public static class ExperimentalDemoknightCatalog
{
    public const string EyelanderItemId = "weapon.experimental-demoknight-eyelander";
    public const string PaintrainItemId = "weapon.experimental-demoknight-paintrain";

    public const string EyelanderWorldSpriteName = "EyeLanderTorsoS";
    public const string EyelanderRecoilSpriteName = "EyeLanderTorsoFS";
    public const string EyelanderBlueWorldSpriteName = "EyeLanderTorsoBlueS";
    public const string EyelanderBlueRecoilSpriteName = "EyeLanderTorsoBlueFS";
    public const string EyelanderMeleeHitboxSpriteName = "EyeLanderMeleeHitboxS";
    public const string EyelanderKillFeedSpriteName = "EyelanderKL";
    public const string EyelanderSwingSoundName = "EyelanderSnd";

    /// <summary>
    /// Share of recoil duration spent on the first attack frame (and the active damage window).
    /// </summary>
    public const float EyelanderFirstFrameProgress = 0.15f;

    public const string PaintrainWorldSpriteName = "PaintrainS";
    public const string PaintrainRecoilSpriteName = "PaintrainFS";
    public const string PaintrainKillFeedSpriteName = "PaintrainKL";

    public const string HudSpriteName = "BladeAmmoS";
    public const string FullChargeHudSpriteName = "FullChargeS";
    public const string ChargeStartSoundName = "ChargeSnd";
    public const string ChargeReadySoundName = "rechargeSnd";

    public static string ResolveTorsoReplacementTeamSpriteName(string? spriteName, PlayerTeam team)
    {
        if (string.IsNullOrWhiteSpace(spriteName) || team != PlayerTeam.Blue)
        {
            return spriteName ?? string.Empty;
        }

        // Convention: FooS → FooBlueS, FooFS → FooBlueFS.
        if (spriteName.EndsWith("BlueS", StringComparison.Ordinal)
            || spriteName.EndsWith("BlueFS", StringComparison.Ordinal))
        {
            return spriteName;
        }

        if (spriteName.EndsWith("FS", StringComparison.Ordinal))
        {
            return spriteName[..^2] + "BlueFS";
        }

        if (spriteName.EndsWith("S", StringComparison.Ordinal))
        {
            return spriteName[..^1] + "BlueS";
        }

        return spriteName;
    }
    public static string? GetDecapitatedDeadBodySpriteName(PlayerClass classId, PlayerTeam team)
    {
        var teamName = GetTeamName(team);
        if (teamName is null)
        {
            return null;
        }

        return classId switch
        {
            PlayerClass.Scout => $"Scout{teamName}DecapDeadS",
            PlayerClass.Engineer => $"Engineer{teamName}DecapDeadS",
            PlayerClass.Pyro => $"Pyro{teamName}DecapDeadS",
            PlayerClass.Soldier => $"Soldier{teamName}DecapDeadS",
            PlayerClass.Demoman => $"Demoman{teamName}DecapDeadS",
            PlayerClass.Heavy => $"Heavy{teamName}DecapDeadS",
            PlayerClass.Sniper => $"Sniper{teamName}DecapDeadS",
            PlayerClass.Medic => $"Medic{teamName}DecapDeadS",
            PlayerClass.Spy => $"Spy{teamName}DecapDeadS",
            _ => null,
        };
    }

    public static string? GetDecapitatedHeadSpriteName(PlayerClass classId, PlayerTeam team)
    {
        var teamName = GetTeamName(team);
        if (teamName is null)
        {
            return null;
        }

        return classId switch
        {
            PlayerClass.Scout => $"Scout{teamName}HeadS",
            PlayerClass.Engineer => $"Engineer{teamName}HeadS",
            PlayerClass.Pyro => $"Pyro{teamName}HeadS",
            PlayerClass.Soldier => $"Soldier{teamName}HeadS",
            PlayerClass.Demoman => $"Demoman{teamName}HeadS",
            PlayerClass.Heavy => $"Heavy{teamName}HeadS",
            PlayerClass.Sniper => $"Sniper{teamName}HeadS",
            PlayerClass.Medic => $"Medic{teamName}HeadS",
            PlayerClass.Spy => $"Spy{teamName}HeadS",
            _ => null,
        };
    }

    private static string? GetTeamName(PlayerTeam team)
    {
        return team switch
        {
            PlayerTeam.Red => "Red",
            PlayerTeam.Blue => "Blue",
            _ => null,
        };
    }
}
