using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationPerformanceClassMaskTests
{
    [Fact]
    public void CachedCertifiedProfileCoverageMatchesCurrentMovementRulesForEveryMask()
    {
        var classes = Enum.GetValues<PlayerClass>();
        var registry = CharacterClassCatalog.RuntimeRegistry;
        var profiles = CreateCertifiedProfiles(registry);
        var maskCount = 1 << classes.Length;

        foreach (var playerClass in classes)
        {
            for (var classCombination = 0; classCombination < maskCount; classCombination += 1)
            {
                var mask = 0;
                for (var classIndex = 0; classIndex < classes.Length; classIndex += 1)
                {
                    if ((classCombination & (1 << classIndex)) != 0)
                    {
                        mask |= BotBrainClassMask.For(classes[classIndex]);
                    }
                }

                Assert.Equal(
                    ReferenceContains(mask, playerClass, profiles, registry),
                    BotBrainClassMask.Contains(mask, playerClass, registry));
            }

            Assert.True(BotBrainClassMask.Contains(BotBrainClassMask.All, playerClass, registry));
        }
    }

    [Fact]
    public void CertifiedProfileCacheRefreshesWhenAnotherClassChangesInAnIsolatedRegistry()
    {
        var registry = GameplayRuntimeRegistry.CreateStock([StockGameplayModCatalog.Definition]);
        var mask = BotBrainClassMask.For(PlayerClass.Heavy);
        var profiles = CreateCertifiedProfiles(registry);
        Assert.True(ReferenceContains(mask, PlayerClass.Scout, profiles, registry));
        Assert.True(BotBrainClassMask.Contains(mask, PlayerClass.Scout, registry));

        var candidateBeforeChange = registry.CreateCharacterClassDefinition(PlayerClass.Scout);
        var originalHeavy = registry.GetClassDefinition(registry.GetRequiredClassBinding(PlayerClass.Heavy).ClassId);
        var scout = registry.GetClassDefinition(registry.GetRequiredClassBinding(PlayerClass.Scout).ClassId);
        Assert.NotNull(originalHeavy.Runtime);
        var replacementHeavy = originalHeavy with
        {
            Id = "simulation-performance-heavy",
            Movement = originalHeavy.Movement with { RunPower = scout.Movement.RunPower + 10f },
        };
        var replacementPack = new GameplayModPackDefinition(
            "simulation-performance-profile-test",
            "Simulation Performance Profile Test",
            new Version(1, 0, 0),
            new Dictionary<string, GameplayItemDefinition>(),
            new Dictionary<string, GameplayClassDefinition> { [replacementHeavy.Id] = replacementHeavy },
            GameplayModPackAssetCatalog.Empty);

        Assert.True(
            registry.TryRegisterModPack(replacementPack, allowRuntimeClassBindingOverride: true, out var errorMessage),
            errorMessage);

        var candidateAfterChange = registry.CreateCharacterClassDefinition(PlayerClass.Scout);
        Assert.NotSame(candidateBeforeChange, candidateAfterChange);
        profiles = CreateCertifiedProfiles(registry);
        Assert.False(ReferenceContains(mask, PlayerClass.Scout, profiles, registry));
        Assert.False(BotBrainClassMask.Contains(mask, PlayerClass.Scout, registry));
    }

    private static List<(PlayerClass PlayerClass, CharacterClassDefinition Definition)> CreateCertifiedProfiles(
        GameplayRuntimeRegistry registry)
    {
        var profiles = new List<(PlayerClass PlayerClass, CharacterClassDefinition Definition)>
        {
            (PlayerClass.Heavy, registry.CreateCharacterClassDefinition(PlayerClass.Heavy)),
            (PlayerClass.Soldier, registry.CreateCharacterClassDefinition(PlayerClass.Soldier)),
            (PlayerClass.Scout, registry.CreateCharacterClassDefinition(PlayerClass.Scout)),
            (PlayerClass.Sniper, registry.CreateCharacterClassDefinition(PlayerClass.Sniper)),
            (PlayerClass.Engineer, registry.CreateCharacterClassDefinition(PlayerClass.Engineer)),
            (PlayerClass.Demoman, registry.CreateCharacterClassDefinition(PlayerClass.Demoman)),
        };
        if (registry.TryGetClassBinding(PlayerClass.Quote, out _))
        {
            profiles.Add((PlayerClass.Quote, registry.CreateCharacterClassDefinition(PlayerClass.Quote)));
        }

        profiles.Add((PlayerClass.Spy, registry.CreateCharacterClassDefinition(PlayerClass.Spy)));
        profiles.Add((PlayerClass.Medic, registry.CreateCharacterClassDefinition(PlayerClass.Medic)));
        profiles.Add((PlayerClass.Pyro, registry.CreateCharacterClassDefinition(PlayerClass.Pyro)));
        return profiles;
    }

    private static bool ReferenceContains(
        int mask,
        PlayerClass playerClass,
        IReadOnlyList<(PlayerClass PlayerClass, CharacterClassDefinition Definition)> profiles,
        GameplayRuntimeRegistry registry)
    {
        if (mask == BotBrainClassMask.All || (mask & BotBrainClassMask.For(playerClass)) != 0)
        {
            return true;
        }

        var candidate = registry.CreateCharacterClassDefinition(playerClass);
        foreach (var profile in profiles)
        {
            if ((mask & BotBrainClassMask.For(profile.PlayerClass)) != 0
                && candidate.RunPower >= profile.Definition.RunPower
                && candidate.JumpStrength >= profile.Definition.JumpStrength
                && candidate.MaxAirJumps >= profile.Definition.MaxAirJumps
                && candidate.CollisionLeft >= profile.Definition.CollisionLeft
                && candidate.CollisionRight <= profile.Definition.CollisionRight
                && candidate.CollisionTop >= profile.Definition.CollisionTop
                && candidate.CollisionBottom <= profile.Definition.CollisionBottom)
            {
                return true;
            }
        }

        return false;
    }
}
