using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpyBackstabDamageableZoneTests
{
    [Fact]
    public void StabMaskHitsStabbableDamageableZone()
    {
        var zone = CreateDamageableZone(stabbable: true);
        var world = CreateWorld([zone]);
        var mask = new StabMaskEntity(
            id: 1,
            ownerId: 2,
            team: PlayerTeam.Red,
            x: -20f,
            y: 21f,
            directionDegrees: 0f);

        var hit = world.CombatTestGetNearestStabHit(mask, directionX: 1f, directionY: 0f);

        Assert.NotNull(hit);
        Assert.Equal(0, hit.Value.HitDamageableZoneRoomObjectIndex);
        Assert.Null(hit.Value.HitPlayer);
        Assert.Null(hit.Value.HitSentry);
    }

    [Fact]
    public void StabMaskIgnoresNonStabbableDamageableZone()
    {
        var zone = CreateDamageableZone(stabbable: false);
        var world = CreateWorld([zone]);
        var mask = new StabMaskEntity(
            id: 1,
            ownerId: 2,
            team: PlayerTeam.Red,
            x: -20f,
            y: 21f,
            directionDegrees: 0f);

        var hit = world.CombatTestGetNearestStabHit(mask, directionX: 1f, directionY: 0f);

        Assert.Null(hit);
    }

    [Fact]
    public void StabMaskAppliesDamageToStabbableDamageableZone()
    {
        var zone = CreateDamageableZone(maxHealth: 250f, stabbable: true);
        var world = CreateWorld([zone]);
        var mask = new StabMaskEntity(
            id: 1,
            ownerId: 2,
            team: PlayerTeam.Red,
            x: -20f,
            y: 21f,
            directionDegrees: 0f);

        var hit = world.CombatTestGetNearestStabHit(mask, directionX: 1f, directionY: 0f);
        Assert.NotNull(hit);
        Assert.Equal(0, hit.Value.HitDamageableZoneRoomObjectIndex);

        Assert.True(world.TryApplyDamageableZoneDamage(0, StabMaskEntity.DamagePerHit));
        Assert.Equal(50f, world.GetDamageableZoneHealth(0));
    }

    private static RoomObjectMarker CreateDamageableZone(float maxHealth = 100f, bool stabbable = false)
    {
        return new RoomObjectMarker(
            RoomObjectType.DamageableZone,
            0f,
            0f,
            42f,
            42f,
            string.Empty,
            SourceName: "damageable-0",
            DamageableZone: new DamageableZoneConfiguration(
                maxHealth,
                -1,
                ShowHealthBar: false,
                BlockPlayers: false,
                DisableWhenDestroyed: true,
                SentryTarget: true,
                Stabbable: stabbable));
    }

    private static SimulationWorld CreateWorld(IReadOnlyList<RoomObjectMarker> roomObjects)
    {
        var world = new SimulationWorld();
        var setLevel = typeof(SimulationWorld).GetMethod(
            "CombatTestSetLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(setLevel);
        setLevel.Invoke(
            world,
            [
                new SimpleLevel(
                    "spy-backstab-damageable-test",
                    GameModeKind.TeamDeathmatch,
                    new WorldBounds(512f, 512f),
                    1f,
                    null,
                    0,
                    1,
                    new SpawnPoint(0f, 0f),
                    [],
                    [],
                    [],
                    roomObjects,
                    0f,
                    [],
                    importedFromSource: false),
            ]);
        return world;
    }
}
