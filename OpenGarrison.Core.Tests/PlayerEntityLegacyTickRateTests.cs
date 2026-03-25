using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class PlayerEntityLegacyTickRateTests
{
    [Fact]
    public void SixtyTickRate_PyroAmmoRegenMatchesSourceCadence()
    {
        var player = CreatePlayer(CharacterClassCatalog.Pyro);
        player.ForceSetAmmo(100);

        AdvanceTicks(player, 30, 1d / 60d);

        Assert.Equal(115, player.CurrentShells);
    }

    [Fact]
    public void SixtyTickRate_TauntAdvancesAtSourceRate()
    {
        var player = CreatePlayer(CharacterClassCatalog.Scout);

        Assert.True(player.TryStartTaunt());
        AdvanceTicks(player, 60, 1d / 60d);

        Assert.False(player.IsTaunting);
        Assert.Equal(8.1f, player.TauntFrameIndex, 3);
    }

    [Fact]
    public void MedicNeedleReload_RefillsWholeClipAfterIdleDelay()
    {
        var player = CreatePlayer(CharacterClassCatalog.Medic);
        player.ForceSetAmmo(30);

        AdvanceTicks(player, 100, 1d / 60d);
        Assert.Equal(30, player.CurrentShells);

        AdvanceTicks(player, 20, 1d / 60d);
        Assert.Equal(40, player.CurrentShells);
    }

    [Fact]
    public void SixtyTickRate_ShotgunReloadWaitsForPrimaryCooldown()
    {
        var player = CreatePlayer(CharacterClassCatalog.Engineer);

        Assert.True(player.TryFirePrimaryWeapon());
        var shellsAfterShot = player.CurrentShells;
        var reloadTicksAfterShot = player.ReloadTicksUntilNextShell;

        AdvanceTicks(player, (player.PrimaryWeapon.ReloadDelayTicks * 2) - 2, 1d / 60d);

        Assert.Equal(shellsAfterShot, player.CurrentShells);
        Assert.Equal(reloadTicksAfterShot, player.ReloadTicksUntilNextShell);

        AdvanceTicks(player, 2, 1d / 60d);

        Assert.Equal(shellsAfterShot, player.CurrentShells);
        Assert.True(player.ReloadTicksUntilNextShell < reloadTicksAfterShot);
    }

    [Fact]
    public void RevolverReload_RestartsFromFullDelayWhenFiringMidReload()
    {
        var player = CreatePlayer(CharacterClassCatalog.Spy);
        player.ForceSetAmmo(4);

        AdvanceTicks(player, 10, 1d / 60d);
        Assert.True(player.ReloadTicksUntilNextShell < player.PrimaryWeapon.AmmoReloadTicks);

        Assert.True(player.TryFirePrimaryWeapon());

        Assert.Equal(3, player.CurrentShells);
        Assert.Equal(player.PrimaryWeapon.AmmoReloadTicks, player.ReloadTicksUntilNextShell);
    }

    private static PlayerEntity CreatePlayer(CharacterClassDefinition definition)
    {
        var player = new PlayerEntity(42, definition, "TimerTest");
        player.Spawn(PlayerTeam.Red, 120f, 220f);
        return player;
    }

    private static void AdvanceTicks(PlayerEntity player, int ticks, double deltaSeconds)
    {
        var level = CreateLevel();
        for (var tick = 0; tick < ticks; tick += 1)
        {
            player.Advance(default, jumpPressed: false, level, player.Team, deltaSeconds);
        }
    }

    private static SimpleLevel CreateLevel()
    {
        var floor = new LevelSolid(0f, 260f, 600f, 24f);
        var spawn = new SpawnPoint(120f, floor.Top - 18f);
        return new SimpleLevel(
            name: "PlayerEntityLegacyTickRateTests",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(1000f, 600f),
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [spawn],
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: Array.Empty<RoomObjectMarker>(),
            floorY: floor.Top,
            solids: [floor],
            importedFromSource: false);
    }
}
