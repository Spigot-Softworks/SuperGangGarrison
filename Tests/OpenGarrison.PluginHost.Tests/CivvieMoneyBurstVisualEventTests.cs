using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivvieMoneyBurstVisualEventTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void BurstEventUsesItsCountAndOriginForTheMoneyParticleSpawns()
    {
        var (_, world, gameplay) = CreateGameWithGameplayManager();
        const float centerX = 312f;
        const float centerY = 148f;
        var count = CivvieMoneyTrailRules.PogoTrickBurstParticleCount;
        var expected = Enumerable.Range(0, count)
            .Select(index => CivvieMoneyTrailRules.CreatePogoTrickBurstSpawn(
                (ulong)world.Frame,
                0,
                index,
                centerX,
                centerY))
            .ToArray();

        gameplay.VisualEvents.PlayVisualEvent("CivvieMoneyBurst", centerX, centerY, 45f, count);

        var moneySheets = GetLooseSheetVisuals(gameplay.MaterialEffects)
            .Where(static visual => visual.IsCivvieMoney)
            .ToArray();
        Assert.Equal(count, gameplay.MaterialEffects.CivvieMoneySheetVisualCount);
        Assert.Equal(count, moneySheets.Length);
        for (var index = 0; index < count; index += 1)
        {
            Assert.Equal(expected[index].X, moneySheets[index].X);
            Assert.Equal(expected[index].Y, moneySheets[index].Y);
            Assert.Equal(expected[index].VelocityX, moneySheets[index].VelocityX);
            Assert.Equal(expected[index].VelocityY, moneySheets[index].VelocityY);
        }
    }

    [Fact]
    public void BurstEventHonorsTheParticleSettingAndExistingMoneySheetBudget()
    {
        var (_, _, gameplay) = CreateGameWithGameplayManager();
        gameplay.RuntimeSettings.ParticleMode = 1;

        gameplay.VisualEvents.PlayVisualEvent(
            "CivvieMoneyBurst",
            100f,
            200f,
            0f,
            CivvieMoneyTrailRules.PogoTrickBurstParticleCount);

        Assert.Equal(0, gameplay.MaterialEffects.CivvieMoneySheetVisualCount);

        gameplay.RuntimeSettings.ParticleMode = 0;
        gameplay.VisualEvents.PlayVisualEvent("CivvieMoneyBurst", 100f, 200f, 0f, count: 45);

        Assert.Equal(40, gameplay.MaterialEffects.CivvieMoneySheetVisualCount);
    }

    [Fact]
    public void ExistingCivvieMoneyTrailEventStillSpawnsOneTrailSheet()
    {
        var (_, _, gameplay) = CreateGameWithGameplayManager();

        gameplay.VisualEvents.PlayVisualEvent("CivvieMoney", 75f, 125f, 24f, count: 1);

        var moneySheets = GetLooseSheetVisuals(gameplay.MaterialEffects)
            .Where(static visual => visual.IsCivvieMoney)
            .ToArray();
        var sheet = Assert.Single(moneySheets);
        Assert.Equal(75f, sheet.X);
        Assert.Equal(125f, sheet.Y);
    }

    private static (Game1 Game, SimulationWorld World, GameplayManager Gameplay) CreateGameWithGameplayManager()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var services = new ClientServiceContainer();
        typeof(Game1).GetField("_world", InstanceMembers)!.SetValue(game, world);
        typeof(Game1).GetField("_services", InstanceMembers)!.SetValue(game, services);

        var gameplay = new GameplayManager((IGameplayContext)game);
        services.Register(gameplay);
        return (game, world, gameplay);
    }

    private static List<Game1.LooseSheetVisual> GetLooseSheetVisuals(GameplayMaterialEffectsController controller)
    {
        var field = typeof(GameplayMaterialEffectsController).GetField("_looseSheetVisuals", InstanceMembers)!;
        return Assert.IsType<List<Game1.LooseSheetVisual>>(field.GetValue(controller));
    }
}
