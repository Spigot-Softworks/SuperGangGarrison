using System;
using System.IO;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplaySoundCueTests
{
    [Theory]
    [InlineData("ScattergunSnd")]
    [InlineData(Game1.MinigunShotSoundName)]
    [InlineData("SMGSnd")]
    [InlineData("TommygunSnd")]
    [InlineData("NailgunSnd")]
    [InlineData("NailReloadSnd")]
    [InlineData("FlameJumpSnd")]
    [InlineData("HeavyDashSnd")]
    [InlineData(Game1.LandingSoundName)]
    [InlineData(Game1.ShotgunShellReloadSoundName)]
    [InlineData(Game1.ShotgunReadySoundName)]
    [InlineData(Game1.SmgReadySoundName)]
    [InlineData(Game1.FlareReadySoundName)]
    [InlineData(Game1.PistolReadySoundName)]
    [InlineData(Game1.PistolHalfReloadSoundName)]
    [InlineData(Game1.HeavyPainSoundName)]
    [InlineData(Game1.VeryHeavyPainSoundName)]
    [InlineData(Game1.FunnyDeathSoundName)]
    public void NewSoundsAreInTheAssetManifest(string soundName)
    {
        var manifest = GameMakerAssetManifestImporter.ImportProjectAssets();
        Assert.True(manifest.Sounds.TryGetValue(soundName, out var sound), $"missing sound {soundName}");
        Assert.True(File.Exists(sound!.AudioPath), $"missing audio file for {soundName}");
    }

    [Theory]
    [InlineData("weapon.scattergun", "ScattergunSnd", Game1.LocalWeaponCueKind.Shotgun)]
    [InlineData("weapon.shotgun", "ShotgunSnd", Game1.LocalWeaponCueKind.Shotgun)]
    [InlineData("weapon.heavy-shotgun", "ShotgunSnd", Game1.LocalWeaponCueKind.Shotgun)]
    [InlineData("weapon.sniper-smg", "SMGSnd", Game1.LocalWeaponCueKind.Smg)]
    [InlineData("weapon.tommy-gun", "TommygunSnd", Game1.LocalWeaponCueKind.Smg)]
    [InlineData("weapon.pyro-flaregun", "FlaregunSnd", Game1.LocalWeaponCueKind.Flare)]
    [InlineData("weapon.scout-pistol", "PistolSnd", Game1.LocalWeaponCueKind.Pistol)]
    [InlineData("weapon.minigun", "ChaingunSnd", Game1.LocalWeaponCueKind.None)]
    [InlineData("weapon.rocketlauncher", "RocketSnd", Game1.LocalWeaponCueKind.None)]
    public void StockWeaponsUseTheirSoundsAndCues(string itemId, string fireSound, Game1.LocalWeaponCueKind cueKind)
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var weapon = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem(itemId));

        Assert.Equal(fireSound, weapon.FireSoundName);
        Assert.Equal(cueKind, Game1.ResolveLocalWeaponCueKind(weapon));
        Assert.True(Game1.IsWeaponFireSoundName(fireSound));
    }

    [Fact]
    public void MinigunShotBurstIsShortAndCountsAsWeaponFire()
    {
        var manifest = GameMakerAssetManifestImporter.ImportProjectAssets();
        Assert.True(manifest.Sounds.TryGetValue(Game1.MinigunShotSoundName, out var shot));
        Assert.True(new FileInfo(shot!.AudioPath).Length < 16 * 1024, "the per-shot minigun burst must stay short");
        Assert.True(Game1.IsWeaponFireSoundName(Game1.MinigunShotSoundName));
    }

    [Fact]
    public void NailgunUsesItsOwnReloadCue()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var nailgun = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.scout-nailgun"));

        var kind = Game1.ResolveLocalWeaponCueKind(nailgun);
        Assert.Equal(Game1.LocalWeaponCueKind.Nailgun, kind);
        Assert.Equal(Game1.NailgunReloadSoundName, Game1.GetLocalWeaponReloadReadySound(kind));
        Assert.Null(Game1.GetLocalWeaponSwitchReadySound(kind));
    }

    [Theory]
    [InlineData(20, false, DamageEventFlags.None, null)]
    [InlineData(21, false, DamageEventFlags.None, Game1.HeavyPainSoundName)]
    [InlineData(50, false, DamageEventFlags.None, Game1.HeavyPainSoundName)]
    [InlineData(51, false, DamageEventFlags.None, Game1.VeryHeavyPainSoundName)]
    [InlineData(80, true, DamageEventFlags.None, null)]
    [InlineData(40, false, DamageEventFlags.AfterburnTick, null)]
    [InlineData(40, false, DamageEventFlags.Airshot, Game1.HeavyPainSoundName)]
    public void PainNeedsOneHeavyNonFatalHit(int amount, bool fatal, DamageEventFlags flags, string? expected)
    {
        Assert.Equal(expected, Game1.ResolveLocalPainSoundName(amount, fatal, flags));
    }

    [Fact]
    public void OnlyHardLaunchedBodiesCountAsFlying()
    {
        Assert.False(Game1.IsFlyingCorpseSpeed(CorpseKnockbackRules.SniperSpeed, -2f));
        Assert.True(Game1.IsFlyingCorpseSpeed(9f, -6f));
        Assert.True(Game1.IsPlayerDeathVoiceSoundName("DeathSnd2"));
        Assert.False(Game1.IsPlayerDeathVoiceSoundName("Gibbing"));
    }
}
