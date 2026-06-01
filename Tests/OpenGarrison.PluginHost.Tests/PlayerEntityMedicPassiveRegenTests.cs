using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityMedicPassiveRegenTests
{
    [Fact]
    public void MedicPassiveRegenHealsThreeHealthAfterOneSecond()
    {
        var medic = CreateDamagedMedic(healthDeficit: 20);

        AdvanceSourceTicks(medic, PlayerEntity.MedicPassiveRegenIntervalSourceTicks - 1);
        Assert.Equal(medic.MaxHealth - 20, medic.Health);

        medic.AdvanceTickState(default, 1d / 30d);
        Assert.Equal(medic.MaxHealth - 17, medic.Health);
    }

    [Fact]
    public void MedicPassiveRegenUsesFourHealthAfterSevenSecondsUnscathed()
    {
        var medic = CreateDamagedMedic(healthDeficit: 50);

        AdvanceSourceTicks(medic, PlayerEntity.MedicPassiveRegenFirstThresholdSourceTicks);

        Assert.Equal(medic.MaxHealth - 28, medic.Health);
    }

    [Fact]
    public void MedicPassiveRegenResetsUnscathedTierWhenDamaged()
    {
        var medic = CreateDamagedMedic(healthDeficit: 50);
        AdvanceSourceTicks(medic, PlayerEntity.MedicPassiveRegenFirstThresholdSourceTicks);

        Assert.False(medic.ApplyDamage(10));
        AdvanceSourceTicks(medic, PlayerEntity.MedicPassiveRegenIntervalSourceTicks);

        Assert.Equal(medic.MaxHealth - 35, medic.Health);
    }

    private static PlayerEntity CreateDamagedMedic(int healthDeficit)
    {
        var medic = new PlayerEntity(1, CharacterClassCatalog.Medic, "Medic");
        medic.Spawn(PlayerTeam.Red, 100f, 100f);
        medic.ForceSetHealth(medic.MaxHealth - healthDeficit);
        return medic;
    }

    private static void AdvanceSourceTicks(PlayerEntity player, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            player.AdvanceTickState(default, 1d / 30d);
        }
    }
}
