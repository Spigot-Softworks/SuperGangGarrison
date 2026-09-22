using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Server.LastToDie;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieFoundationTests
{
    private static readonly Guid SoloPlayerId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid SecondPlayerId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
    private static readonly Guid RunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void ExpansionCatalogContainsAllRequestedPerksAndValidRelationships()
    {
        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var catalog = LastToDieExpansionPerkCatalog.Create(survivors);

        Assert.Equal(158, catalog.Definitions.Count);
        Assert.Equal(25, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.SoldierId));
        Assert.Equal(13, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.DemoknightId));
        Assert.Equal(26, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.EngineerId));
        Assert.Equal(25, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.SpyId));
        Assert.Equal(20, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.MedicId));
        Assert.Equal(21, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.SniperId));
        Assert.Equal(158, catalog.Definitions.Select(perk => perk.Id).Distinct().Count());
        Assert.Equal(28, catalog.Definitions.Count(perk => perk.Scope == LastToDiePerkScope.AllClass));
        Assert.Equal(15, catalog.Definitions.Count(perk => perk.Tier == LastToDiePerkTier.Rare));
        Assert.Equal(13, catalog.Definitions.Count(perk => perk.Tier == LastToDiePerkTier.Ultra));
        Assert.All(
            catalog.Definitions.Where(perk => perk.Scope == LastToDiePerkScope.AllClass),
            perk => Assert.Null(perk.SurvivorId));

        var essenceExtractor = catalog.GetRequired(LastToDiePerkIds.Engineer.EssenceExtractor);
        var freezeRay = catalog.GetRequired(LastToDiePerkIds.Engineer.FreezeRay);
        Assert.Contains(LastToDieExpansionPerkCatalog.InteractWeaponBindingToken, essenceExtractor.Description);
        Assert.Contains(LastToDieExpansionPerkCatalog.InteractWeaponBindingToken, freezeRay.Description);
        Assert.DoesNotContain("Press Q", essenceExtractor.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("Press Q", freezeRay.Description, StringComparison.Ordinal);

        var levelThree = catalog.GetRequired(LastToDiePerkIds.Spy.Blunderbuss3);
        Assert.Equal(3, levelThree.Rank);
        Assert.Equal(
            [LastToDiePerkIds.Spy.Blunderbuss1, LastToDiePerkIds.Spy.Blunderbuss2],
            levelThree.Requires);

        var agent = catalog.GetRequired(LastToDiePerkIds.Spy.Agent);
        Assert.Contains(LastToDiePerkIds.Spy.Blunderbuss1, agent.Excludes);
        Assert.Contains(LastToDiePerkIds.Spy.Agent, catalog.GetRequired(LastToDiePerkIds.Spy.Blunderbuss1).Excludes);
    }

    [Fact]
    public void EveryStockSurvivorReceivesAnOpeningHostedRewardOffer()
    {
        var survivorIds = new[]
        {
            LastToDieSurvivorCatalog.SoldierId,
            LastToDieSurvivorCatalog.DemoknightId,
            LastToDieSurvivorCatalog.EngineerId,
            LastToDieSurvivorCatalog.SpyId,
            LastToDieSurvivorCatalog.MedicId,
            LastToDieSurvivorCatalog.SniperId,
        };

        for (var index = 0; index < survivorIds.Length; index += 1)
        {
            var director = CreateDirector(seed: (ulong)(700 + index));
            var survivorId = survivorIds[index];
            Assert.True(director.TryAddPlayer(SoloPlayerId, out var addError), addError);
            Assert.True(director.TryStart(out var startError), startError);
            Assert.True(director.TrySelectSurvivor(SoloPlayerId, survivorId, out var survivorError), survivorError);
            var offer = Assert.IsType<LastToDieRewardOffer>(GetSolo(director).ActiveOffer);
            var catalog = LastToDieExpansionPerkCatalog.Create(LastToDieSurvivorCatalog.CreateStock());
            Assert.All(
                offer.Choices,
                choice => Assert.Equal(survivorId, catalog.GetRequired(choice).SurvivorId));
            Assert.True(director.TrySelectReward(SoloPlayerId, offer.OfferId, offer.Choices[0], out var rewardError), rewardError);
            var snapshot = director.CreateSnapshot();
            Assert.Equal(LastToDiePhase.LoadingStage, snapshot.Phase);
        }
    }

    [Fact]
    public void RewardOffersKeepTierGuaranteesAnchoredToTheirTargetStage()
    {
        var director = CreateDirector(seed: 0x5A6E);
        var stageOneOffers = AdvanceDirectorToRewardStage(director, targetStage: 1);
        Assert.Single(stageOneOffers);
        Assert.Equal(1, stageOneOffers[0].TargetStage);
        Assert.All(stageOneOffers[0].Slots, slot => Assert.Equal(LastToDiePerkTier.Standard, slot.Tier));

        var stageThreeOffers = AdvanceDirectorToRewardStage(director, targetStage: 3);
        Assert.All(stageThreeOffers, offer => Assert.Equal(3, offer.TargetStage));
        Assert.All(stageThreeOffers, offer => Assert.Equal(3, offer.Slots.Count(slot => slot.Tier == LastToDiePerkTier.Rare)));
        Assert.DoesNotContain(stageThreeOffers.SelectMany(offer => offer.Slots), slot => slot.Tier == LastToDiePerkTier.Ultra);

        var stageFourOffers = AdvanceDirectorToRewardStage(director, targetStage: 4);
        Assert.DoesNotContain(stageFourOffers.SelectMany(offer => offer.Slots), slot => slot.Tier == LastToDiePerkTier.Ultra);

        var stageFiveOffers = AdvanceDirectorToRewardStage(director, targetStage: 5);
        Assert.All(stageFiveOffers, offer => Assert.Equal(5, offer.TargetStage));
        Assert.DoesNotContain(stageFiveOffers.SelectMany(offer => offer.Slots), slot => slot.Tier == LastToDiePerkTier.Ultra);

        var stageTenOffers = AdvanceDirectorToRewardStage(director, targetStage: 10);
        Assert.All(stageTenOffers, offer => Assert.Equal(10, offer.TargetStage));
        Assert.Equal(3, stageTenOffers.SelectMany(offer => offer.Slots).Count(slot => slot.Tier == LastToDiePerkTier.Ultra));

        var stageTwentyOffers = AdvanceDirectorToRewardStage(director, targetStage: 20);
        Assert.All(stageTwentyOffers, offer => Assert.Equal(20, offer.TargetStage));
        Assert.Equal(3, stageTwentyOffers.SelectMany(offer => offer.Slots).Count(slot => slot.Tier == LastToDiePerkTier.Ultra));
    }

    [Theory]
    [InlineData(2, false, false, false, LastToDiePerkTier.Standard)]
    [InlineData(3, false, false, false, LastToDiePerkTier.Rare)]
    [InlineData(4, false, false, false, LastToDiePerkTier.Standard)]
    [InlineData(4, false, false, true, LastToDiePerkTier.Rare)]
    [InlineData(4, true, false, true, LastToDiePerkTier.Rare)]
    [InlineData(10, false, true, true, LastToDiePerkTier.Ultra)]
    public void RewardTierSelectionAppliesStageThreeRareAndLaterPerSlotOdds(
        int targetStage,
        bool luckyDrawActive,
        bool guaranteedUltra,
        bool rareSlotRoll,
        LastToDiePerkTier expected)
    {
        Assert.Equal(
            expected,
            LastToDieDirector.GetDesiredRewardTierForSlot(
                targetStage,
                luckyDrawActive,
                guaranteedUltra,
                rareSlotRoll));
    }

    [Theory]
    [InlineData(3, false, false, true, 0, false)]
    [InlineData(4, false, false, true, 4, true)]
    [InlineData(4, false, false, true, 5, false)]
    [InlineData(4, false, false, true, 99, false)]
    [InlineData(10, false, true, true, 0, false)]
    [InlineData(4, true, false, true, 0, false)]
    [InlineData(4, false, false, false, 0, false)]
    public void RareRewardSlotChanceIsFivePercentAfterRoundThree(
        int targetStage,
        bool luckyDrawActive,
        bool guaranteedUltra,
        bool hasEligibleRarePerk,
        int percentileRoll,
        bool expected)
    {
        Assert.Equal(
            expected,
            LastToDieDirector.ShouldRollRareRewardSlot(
                targetStage,
                luckyDrawActive,
                guaranteedUltra,
                hasEligibleRarePerk,
                percentileRoll));
    }

    [Fact]
    public void RewardRerollKeepsItsTierAndCanOnlyBeUsedOnce()
    {
        var director = CreateDirector(seed: 4711);
        AdvanceToOpeningOffer(director);
        var offer = GetSolo(director).ActiveOffer!;
        var slot = offer.Slots.First(candidate => candidate.RerollsRemaining > 0);

        Assert.True(director.TryRerollReward(SoloPlayerId, offer.OfferId, slot.PerkId, out var rerollError), rerollError);
        var rerolledOffer = GetSolo(director).ActiveOffer!;
        var replacement = Assert.Single(rerolledOffer.Slots.Where(candidate => candidate.Tier == slot.Tier && candidate.RerollsRemaining == 0));
        Assert.NotEqual(slot.PerkId, replacement.PerkId);
        Assert.False(director.TrySelectReward(SoloPlayerId, offer.OfferId, replacement.PerkId, out var staleError));
        Assert.Contains("stale", staleError, StringComparison.OrdinalIgnoreCase);
        Assert.False(director.TryRerollReward(SoloPlayerId, rerolledOffer.OfferId, replacement.PerkId, out var secondRerollError));
        Assert.Contains("already", secondRerollError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LuckyDrawForcesRareOffersForExactlyTheNextTwoRounds()
    {
        var director = CreateDirectorWithUniversalPool(LastToDiePerkIds.Ultra.LuckyDraw);
        var milestoneOffers = AdvanceDirectorToRewardStage(
            director,
            targetStage: 10,
            selectTargetOffers: false);
        var milestoneOffer = Assert.Single(milestoneOffers);
        var luckyDrawSlot = Assert.Single(milestoneOffer.Slots.Where(slot => slot.Tier == LastToDiePerkTier.Ultra));
        Assert.Equal(LastToDiePerkIds.Ultra.LuckyDraw, luckyDrawSlot.PerkId);
        Assert.True(director.TrySelectReward(SoloPlayerId, milestoneOffer.OfferId, luckyDrawSlot.PerkId, out var luckyDrawError), luckyDrawError);
        Assert.Equal(2, GetSolo(director).LuckyDrawRoundsRemaining);

        var stageElevenOffer = CompleteCurrentStageAndGetNextOffer(director);
        Assert.Equal(11, stageElevenOffer.TargetStage);
        Assert.All(stageElevenOffer.Slots, slot => Assert.Equal(LastToDiePerkTier.Rare, slot.Tier));
        Assert.Equal(1, GetSolo(director).LuckyDrawRoundsRemaining);
        Assert.True(director.TrySelectReward(SoloPlayerId, stageElevenOffer.OfferId, stageElevenOffer.Choices[0], out var stageElevenError), stageElevenError);

        var stageTwelveOffer = CompleteCurrentStageAndGetNextOffer(director);
        Assert.Equal(12, stageTwelveOffer.TargetStage);
        Assert.All(stageTwelveOffer.Slots, slot => Assert.Equal(LastToDiePerkTier.Rare, slot.Tier));
        Assert.Equal(0, GetSolo(director).LuckyDrawRoundsRemaining);
    }

    [Fact]
    public void SecondChanceCanBeConsumedOnlyOnceAfterItIsSelected()
    {
        var director = CreateDirectorWithUniversalPool(LastToDiePerkIds.Ultra.SecondChance);
        var milestoneOffers = AdvanceDirectorToRewardStage(
            director,
            targetStage: 10,
            selectTargetOffers: false);
        var milestoneOffer = Assert.Single(milestoneOffers);
        var secondChanceSlot = Assert.Single(milestoneOffer.Slots.Where(slot => slot.Tier == LastToDiePerkTier.Ultra));
        Assert.Equal(LastToDiePerkIds.Ultra.SecondChance, secondChanceSlot.PerkId);
        Assert.True(director.TrySelectReward(SoloPlayerId, milestoneOffer.OfferId, secondChanceSlot.PerkId, out var selectionError), selectionError);

        Assert.True(director.TryConsumeSecondChance(SoloPlayerId, out var consumeError), consumeError);
        Assert.True(GetSolo(director).SecondChanceConsumed);
        Assert.False(director.TryConsumeSecondChance(SoloPlayerId, out var secondConsumeError));
        Assert.Contains("already been consumed", secondConsumeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UniversalPerkModifiersFollowTheDocumentedStackingRules()
    {
        var modifiers = LastToDieUniversalModifiers.FromPerks(
        [
            LastToDiePerkIds.Rare.Colossus,
            LastToDiePerkIds.Rare.HealthBooster,
            LastToDiePerkIds.Rare.TroopersBlessing,
            LastToDiePerkIds.Rare.SpeedBooster,
            LastToDiePerkIds.Rare.SleightOfHand,
            LastToDiePerkIds.Rare.PowerBooster,
            LastToDiePerkIds.Rare.SpikedArmor,
            LastToDiePerkIds.Ultra.GutsAndGlory,
            LastToDiePerkIds.Ultra.ZergRush,
            LastToDiePerkIds.Ultra.FatalBravado,
            LastToDiePerkIds.Ultra.FightOrFlight,
            LastToDiePerkIds.Ultra.ImmovableObject,
            LastToDiePerkIds.Ultra.HeartOfBravery,
            LastToDiePerkIds.Ultra.InfiniteSlayWorks,
            LastToDiePerkIds.Ultra.SecondChance,
        ]);

        Assert.Equal(210, modifiers.MaximumHealthBonus);
        Assert.Equal(50, modifiers.BaseMaximumHealthOverride);
        Assert.Equal(0.39f, modifiers.PlayerScale, precision: 3);
        Assert.Equal(1.3f, modifiers.MeleeScale);
        Assert.Equal(1.3f, modifiers.ProjectileScale);
        Assert.Equal(1.3f, modifiers.ExplosionScale);
        Assert.Equal(1.15f * 1.15f * 1.25f * 2f, modifiers.FireSpeedMultiplier, precision: 3);
        Assert.Equal(modifiers.FireSpeedMultiplier, modifiers.ReloadSpeedMultiplier);
        Assert.Equal(1.15f * 1.15f, modifiers.MovementSpeedMultiplier, precision: 3);
        Assert.Equal(1.15f, modifiers.OutgoingDamageMultiplier, precision: 3);
        Assert.Equal(0.8f, modifiers.BulletDamageTakenMultiplier);
        Assert.Equal(0.8f, modifiers.ExplosionDamageTakenMultiplier);
        Assert.Equal(0.4f, modifiers.KnockbackReceivedMultiplier);
        Assert.Equal(5f, modifiers.RegenerationPerSecond);
        Assert.Equal(0.2f, modifiers.ReflectionFraction);
        Assert.Equal(3, modifiers.MaximumHealthPerRunKill);
        Assert.Equal(10, modifiers.HealPerKill);
        Assert.True(modifiers.RageDisabled);
        Assert.True(modifiers.FatalBravado);
        Assert.True(modifiers.SecondChance);
    }

    [Fact]
    public void LegacyPerkTranslatorRetainsOriginalRepresentativeSemantics()
    {
        var soldier = LastToDieLegacyPerkSettings.FromPerks(
            PlayerClass.Soldier,
            [LastToDiePerkIds.Soldier.HealOnDamage, LastToDiePerkIds.Soldier.InstantReload]);
        Assert.True(soldier.EnableHealOnDamage);
        Assert.Equal(0.35f, soldier.HealOnDamageFraction);
        Assert.True(soldier.EnableSoldierInstantReload);

        var demoknight = LastToDieLegacyPerkSettings.FromPerks(
            PlayerClass.Demoman,
            [LastToDiePerkIds.Demoknight.Lifesteal, LastToDiePerkIds.Demoknight.ChargeResistance]);
        Assert.Equal(0.6f, demoknight.HealOnDamageFraction);
        Assert.Equal(0.2f, demoknight.DemoknightChargeDamageTakenMultiplier);

        var engineer = LastToDieLegacyPerkSettings.FromPerks(
            PlayerClass.Engineer,
            [LastToDiePerkIds.Engineer.DestinyPunctuator, LastToDiePerkIds.Engineer.MateriaRecycler]);
        Assert.True(engineer.EnableEngineerDestinyPunctuator);
        Assert.Equal(1.3f, engineer.PassiveMovementSpeedMultiplier);
        Assert.Equal(1.3f, engineer.PassiveJumpHeightMultiplier);
        Assert.True(engineer.EnableEngineerMateriaRecycler);
    }

    [Fact]
    public void LegacyPerkTranslatorDeduplicatesRepeatedChoices()
    {
        var single = LastToDieLegacyPerkSettings.FromPerks(
            PlayerClass.Engineer,
            [LastToDiePerkIds.Engineer.DestinyPunctuator]);
        var repeated = LastToDieLegacyPerkSettings.FromPerks(
            PlayerClass.Engineer,
            [LastToDiePerkIds.Engineer.DestinyPunctuator, LastToDiePerkIds.Engineer.DestinyPunctuator]);

        Assert.Equal(single, repeated);
        Assert.Equal(1.3f, repeated.PassiveMovementSpeedMultiplier);
        Assert.Equal(1.3f, repeated.PassiveJumpHeightMultiplier);
    }

    [Fact]
    public void EveryOriginalClassPerkChangesItsLegacySettingsProfile()
    {
        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var catalog = LastToDieExpansionPerkCatalog.Create(survivors);
        var originalClasses = new Dictionary<LastToDieSurvivorId, PlayerClass>
        {
            [LastToDieSurvivorCatalog.SoldierId] = PlayerClass.Soldier,
            [LastToDieSurvivorCatalog.DemoknightId] = PlayerClass.Demoman,
            [LastToDieSurvivorCatalog.EngineerId] = PlayerClass.Engineer,
        };

        foreach (var (survivorId, playerClass) in originalClasses)
        {
            var baseline = LastToDieLegacyPerkSettings.FromPerks(playerClass, []);
            foreach (var definition in catalog.Definitions.Where(definition => definition.SurvivorId == survivorId))
            {
                var withPerk = LastToDieLegacyPerkSettings.FromPerks(playerClass, [definition.Id]);
                Assert.False(
                    Equals(baseline, withPerk),
                    $"Legacy translator left {definition.Id.Value} inert for {playerClass}.");
            }
        }
    }

    [Fact]
    public void HostedLegacyBuildProfilesRemainIsolatedPerNetworkSlot()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Demoman));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Demoman));
        Assert.True(world.TryGetNetworkPlayer(2, out var boosted));
        Assert.True(world.TryGetNetworkPlayer(3, out var stock));

        var baseRunPower = stock.ClassDefinition.RunPower;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Demoknight.MoveSpeed],
            refillHealth: true));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            3,
            [],
            refillHealth: true));

        Assert.Equal(baseRunPower * 1.3f, boosted.RunPower, precision: 3);
        Assert.Equal(baseRunPower, stock.RunPower, precision: 3);
    }

    [Fact]
    public void HostedServerTracksCombosForActualSurvivors()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Heavy));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red, respawnLivePlayerImmediately: true));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Blue, respawnLivePlayerImmediately: true));
        Assert.True(world.TryGetNetworkPlayer(2, out var attacker));
        Assert.True(world.TryGetNetworkPlayer(3, out var target));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, [], refillHealth: true));

        Assert.True(world.TryApplyGameplayDamage(target.Id, 20f, attacker.Id, null));

        Assert.Equal(1, attacker.CurrentCombo);
        Assert.True(attacker.ComboTicksRemaining > 0);
        Assert.Equal(0, target.CurrentCombo);
    }

    [Fact]
    public void HostedClientPredictionInstallsAndClearsLegacyProfileWithoutCreatingServerRuntime()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Demoman));
        Assert.True(world.TryGetNetworkPlayer(2, out var player));
        var baseRunPower = player.ClassDefinition.RunPower;

        Assert.True(world.TryApplyLastToDiePlayerPredictionProfile(
            2,
            [LastToDiePerkIds.Demoknight.MoveSpeed.Value]));
        Assert.Equal(baseRunPower * 1.3f, player.RunPower, precision: 3);
        Assert.True(player.IsExperimentalDemoknightEnabled);
        Assert.Equal(100, player.GetExperimentalDemoknightSwordDamage());

        var predictionShadow = new PlayerEntity(
            5000,
            CharacterClassCatalog.Demoman,
            "PredictionShadow");
        predictionShadow.RestorePredictionState(player.CapturePredictionState());
        Assert.True(predictionShadow.IsExperimentalDemoknightEnabled);
        Assert.Equal(player.RunPower, predictionShadow.RunPower, precision: 3);
        Assert.Equal(100, predictionShadow.GetExperimentalDemoknightSwordDamage());
        Assert.True(predictionShadow.TryFireExperimentalDemoknightSword());

        Assert.True(world.ClearLastToDiePlayerPredictionProfile(2));
        Assert.Equal(baseRunPower, player.RunPower, precision: 3);
        Assert.False(player.IsExperimentalDemoknightEnabled);
    }

    [Fact]
    public void HostedEngineerLegacyBuildSynchronizesMetalCapacityImmediately()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Engineer));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Engineer));
        Assert.True(world.TryGetNetworkPlayer(2, out var recycler));
        Assert.True(world.TryGetNetworkPlayer(3, out var stock));

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Engineer.MateriaRecycler],
            refillHealth: true));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(3, [], refillHealth: true));

        Assert.Equal(200f, recycler.MaxMetal);
        Assert.Equal(100f, stock.MaxMetal);
    }

    [Fact]
    public void HostedSoldierLegacyDamageRewardAppliesImmediatelyAndPerSlot()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TryPrepareNetworkPlayerJoin(4));
        Assert.True(world.TryPrepareNetworkPlayerJoin(5));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Soldier));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(4, PlayerClass.Heavy));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(5, PlayerClass.Heavy));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Red, respawnLivePlayerImmediately: true));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Red, respawnLivePlayerImmediately: true));
        Assert.True(world.TrySetNetworkPlayerTeam(4, PlayerTeam.Blue, respawnLivePlayerImmediately: true));
        Assert.True(world.TrySetNetworkPlayerTeam(5, PlayerTeam.Blue, respawnLivePlayerImmediately: true));
        Assert.True(world.TryGetNetworkPlayer(2, out var sadist));
        Assert.True(world.TryGetNetworkPlayer(3, out var stock));
        Assert.True(world.TryGetNetworkPlayer(4, out var firstTarget));
        Assert.True(world.TryGetNetworkPlayer(5, out var secondTarget));

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            2,
            [LastToDiePerkIds.Soldier.HealOnDamage],
            refillHealth: true));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(3, [], refillHealth: true));
        sadist.ForceSetHealth(sadist.MaxHealth - 50);
        stock.ForceSetHealth(stock.MaxHealth - 50);

        Assert.True(world.TryApplyGameplayDamage(firstTarget.Id, 40f, sadist.Id, null));
        Assert.True(world.TryApplyGameplayDamage(secondTarget.Id, 40f, stock.Id, null));

        Assert.Equal(sadist.MaxHealth - 36, sadist.Health);
        Assert.Equal(stock.MaxHealth - 50, stock.Health);
    }

    [Fact]
    public void EndlessEnemyScalingChangesMovementAndOutgoingDamagePerSlot()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TryPrepareNetworkPlayerJoin(3));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Heavy));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue, respawnLivePlayerImmediately: true));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Red, respawnLivePlayerImmediately: true));
        Assert.True(world.TryGetNetworkPlayer(2, out var enemy));
        Assert.True(world.TryGetNetworkPlayer(3, out var survivor));
        var healthBefore = survivor.Health;

        Assert.True(world.TrySetNetworkPlayerLastToDieEnemyScaling(2, 1.15f, 1.15f));
        Assert.Equal(1.15f, enemy.ServerMovementSpeedScale, precision: 3);
        Assert.True(world.TryApplyGameplayDamage(survivor.Id, 20f, enemy.Id, null));

        Assert.Equal(healthBefore - 23, survivor.Health);
    }

    [Fact]
    public void CatalogRejectsAsymmetricExclusion()
    {
        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var first = new LastToDiePerkId("ltd.perk.spy.first");
        var second = new LastToDiePerkId("ltd.perk.spy.second");

        var exception = Assert.Throws<InvalidOperationException>(() => new LastToDiePerkCatalog(
            survivors,
            [
                new(first, LastToDieSurvivorCatalog.SpyId, "First", "", excludes: [second]),
                new(second, LastToDieSurvivorCatalog.SpyId, "Second", ""),
            ]));

        Assert.Contains("symmetric", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlunderbussPrerequisitesAndExclusionsControlEligibility()
    {
        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var catalog = LastToDieExpansionPerkCatalog.Create(survivors);
        var owned = new HashSet<LastToDiePerkId>();

        var initial = catalog.GetEligible(LastToDieSurvivorCatalog.SpyId, owned).Select(perk => perk.Id).ToArray();
        Assert.Contains(LastToDiePerkIds.Spy.Blunderbuss1, initial);
        Assert.DoesNotContain(LastToDiePerkIds.Spy.Blunderbuss2, initial);
        Assert.DoesNotContain(LastToDiePerkIds.Spy.Blunderbuss3, initial);
        Assert.Contains(LastToDiePerkIds.Spy.Agent, initial);

        owned.Add(LastToDiePerkIds.Spy.Blunderbuss1);
        var afterLevelOne = catalog.GetEligible(LastToDieSurvivorCatalog.SpyId, owned).Select(perk => perk.Id).ToArray();
        Assert.Contains(LastToDiePerkIds.Spy.Blunderbuss2, afterLevelOne);
        Assert.DoesNotContain(LastToDiePerkIds.Spy.Blunderbuss3, afterLevelOne);
        Assert.DoesNotContain(LastToDiePerkIds.Spy.Agent, afterLevelOne);
        Assert.DoesNotContain(LastToDiePerkIds.Spy.RubberBullets, afterLevelOne);

        owned.Add(LastToDiePerkIds.Spy.Blunderbuss2);
        Assert.Contains(
            LastToDiePerkIds.Spy.Blunderbuss3,
            catalog.GetEligible(LastToDieSurvivorCatalog.SpyId, owned).Select(perk => perk.Id));
    }

    [Fact]
    public void PcgStreamMatchesVersionedReferenceVectorAndRestoresExactly()
    {
        var random = new LastToDieRandom(seed: 42, sequence: 54);

        Assert.Equal(0xA15C02B7U, random.NextUInt32());
        Assert.Equal(0x7B47F409U, random.NextUInt32());
        var state = random.CaptureState();
        var expected = random.NextUInt32();

        Assert.Equal(0xBA1D3330U, expected);
        Assert.Equal(expected, LastToDieRandom.Restore(state).NextUInt32());
    }

    [Fact]
    public void DefaultRulesetBecomesEndlessAfterTheExistingNineStageCurve()
    {
        var ruleset = LastToDieRuleset.CreateDefault(ticksPerSecond: 30);

        Assert.Equal(new LastToDieStageDefinition(1, 2, 1_800), ruleset.GetStage(1));
        Assert.Equal(new LastToDieStageDefinition(9, 10, 16_200), ruleset.GetStage(9));
        Assert.Equal(new LastToDieStageDefinition(10, 11, 16_200), ruleset.GetStage(10));
        Assert.Equal(new LastToDieStageDefinition(15, 16, 16_200), ruleset.GetStage(15));
        Assert.Equal(new LastToDieStageDefinition(16, 16, 16_200), ruleset.GetStage(16));
        Assert.True(ruleset.Endless);
        Assert.False(LastToDieRuleset.CanSpawnSniper(8));
        Assert.True(LastToDieRuleset.CanSpawnSniper(9));
        Assert.Equal(1f, LastToDieRuleset.GetEnemyStatMultiplier(9));
        Assert.Equal(1.05f, LastToDieRuleset.GetEnemyStatMultiplier(10));
        Assert.Equal(1.2f, LastToDieRuleset.GetEnemyStatMultiplier(13));
        Assert.Equal(1f, LastToDieRuleset.GetEnemyDamageMultiplier(10));
        Assert.Equal(1.05f, LastToDieRuleset.GetEnemyDamageMultiplier(11));
        Assert.Equal(1.1f, LastToDieRuleset.GetEnemyDamageMultiplier(12));
        Assert.Equal(54_000, ruleset.RunTimeLimitTicks);
        Assert.Equal(90, ruleset.KillTimerReductionTicks);
    }

    [Fact]
    public void EndlessDirectorAdvancesPastItsFormerFinalStage()
    {
        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var director = new LastToDieDirector(
            LastToDieRuleset.CreateDefault() with { StageCount = 1, Endless = true },
            survivors,
            LastToDieExpansionPerkCatalog.Create(survivors),
            ["Truefort"],
            LastToDieDifficulty.Standard,
            seed: 17,
            RunId);
        AdvanceToOpeningOffer(director);
        var openingOffer = GetSolo(director).ActiveOffer!;
        Assert.True(director.TrySelectReward(
            SoloPlayerId,
            openingOffer.OfferId,
            openingOffer.Choices[0],
            out var rewardError), rewardError);
        Assert.True(director.TrySetStageReady(SoloPlayerId, out var readyError), readyError);
        Assert.True(director.TryBeginStage(100, out var beginError), beginError);
        Assert.True(director.TryAdvancePlayingState(101, true, false, false, out var clearError), clearError);

        Assert.Equal(LastToDiePhase.RewardChoice, director.Phase);
        var secondOffer = GetSolo(director).ActiveOffer!;
        Assert.True(director.TrySelectReward(
            SoloPlayerId,
            secondOffer.OfferId,
            secondOffer.Choices[0],
            out var secondRewardError), secondRewardError);
        Assert.Equal(2, director.CreateSnapshot().StageNumber);
        Assert.Equal(LastToDiePhase.LoadingStage, director.Phase);
    }

    [Fact]
    public void SameSeedCommandsAndPlayerIdentityProduceSameOfferAndMap()
    {
        var first = CreateDirector(seed: 12345);
        var second = CreateDirector(seed: 12345);

        AdvanceToOpeningOffer(first);
        AdvanceToOpeningOffer(second);

        var firstOffer = GetSolo(first).ActiveOffer!;
        var secondOffer = GetSolo(second).ActiveOffer!;
        Assert.Equal(firstOffer.Choices, secondOffer.Choices);

        Assert.True(first.TrySelectReward(SoloPlayerId, firstOffer.OfferId, firstOffer.Choices[0], out var firstError), firstError);
        Assert.True(second.TrySelectReward(SoloPlayerId, secondOffer.OfferId, secondOffer.Choices[0], out var secondError), secondError);
        Assert.Equal(first.CreateSnapshot().CurrentMap, second.CreateSnapshot().CurrentMap);
    }

    [Fact]
    public void TwoPlayerDraftsAreIndependentAndStageStartUsesAReadinessBarrier()
    {
        var director = CreateDirector(seed: 54321);
        Assert.True(director.TryAddPlayer(SoloPlayerId, out var firstAddError), firstAddError);
        Assert.True(director.TryAddPlayer(SecondPlayerId, out var secondAddError), secondAddError);
        Assert.True(director.TryStart(out var startError), startError);

        Assert.True(director.TrySelectSurvivor(
            SoloPlayerId,
            LastToDieSurvivorCatalog.SpyId,
            out var firstSurvivorError), firstSurvivorError);
        Assert.Equal(LastToDiePhase.SurvivorChoice, director.Phase);
        Assert.True(director.TrySelectSurvivor(
            SecondPlayerId,
            LastToDieSurvivorCatalog.MedicId,
            out var secondSurvivorError), secondSurvivorError);

        var players = director.CreateSnapshot().Players.ToDictionary(player => player.PlayerId);
        var firstOffer = players[SoloPlayerId].ActiveOffer!;
        var secondOffer = players[SecondPlayerId].ActiveOffer!;
        var revisionBeforeForgery = director.StructuralRevision;
        Assert.False(director.TrySelectReward(
            SoloPlayerId,
            firstOffer.OfferId + 100,
            firstOffer.Choices[0],
            out var forgedError));
        Assert.Contains("stale", forgedError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(revisionBeforeForgery, director.StructuralRevision);

        Assert.True(director.TrySelectReward(
            SoloPlayerId,
            firstOffer.OfferId,
            firstOffer.Choices[0],
            out var firstRewardError), firstRewardError);
        Assert.Equal(LastToDiePhase.RewardChoice, director.Phase);
        players = director.CreateSnapshot().Players.ToDictionary(player => player.PlayerId);
        Assert.Null(players[SoloPlayerId].ActiveOffer);
        Assert.NotNull(players[SecondPlayerId].ActiveOffer);

        Assert.True(director.TrySelectReward(
            SecondPlayerId,
            secondOffer.OfferId,
            secondOffer.Choices[0],
            out var secondRewardError), secondRewardError);
        Assert.Equal(LastToDiePhase.LoadingStage, director.Phase);

        Assert.True(director.TrySetStageReady(SoloPlayerId, out var firstReadyError), firstReadyError);
        Assert.False(director.TryBeginStage(serverTick: 10, out var earlyStartError));
        Assert.Contains("Every connected player", earlyStartError, StringComparison.Ordinal);
        Assert.True(director.TrySetStageReady(SecondPlayerId, out var secondReadyError), secondReadyError);
        Assert.True(director.TryBeginStage(serverTick: 10, out var beginError), beginError);
        Assert.Equal(LastToDiePhase.Playing, director.Phase);
    }

    [Fact]
    public void SoloDirectorOwnsOpeningDraftStageAnchorsAndKillReduction()
    {
        var director = CreateDirector(seed: 7);
        AdvanceToOpeningOffer(director);
        var offer = GetSolo(director).ActiveOffer!;

        Assert.True(director.TrySelectReward(SoloPlayerId, offer.OfferId, offer.Choices[0], out var rewardError), rewardError);
        var loading = director.CreateSnapshot();
        Assert.Equal(LastToDiePhase.LoadingStage, loading.Phase);
        Assert.Equal(1, loading.StageNumber);
        Assert.Equal(2, loading.EnemyCount);
        Assert.False(string.IsNullOrWhiteSpace(loading.CurrentMap));

        Assert.True(director.TrySetStageReady(SoloPlayerId, out var readyError), readyError);
        Assert.True(director.TryBeginStage(serverTick: 100, out var beginError), beginError);
        var playing = director.CreateSnapshot();
        Assert.Equal(LastToDiePhase.Playing, playing.Phase);
        Assert.Equal(1_900, playing.StageEndServerTick);
        Assert.Equal(0, playing.RunEndServerTick);

        var structuralRevision = playing.StructuralRevision;
        Assert.True(director.TryRecordKills(SoloPlayerId, killCount: 2, serverTick: 200, out var killError), killError);
        var afterKills = director.CreateSnapshot();
        Assert.Equal(1_720, afterKills.StageEndServerTick);
        Assert.Equal(structuralRevision + 1, afterKills.StructuralRevision);
        Assert.Equal(2, GetSolo(director).Kills);

        Assert.True(director.TryAdvancePlayingState(
            serverTick: afterKills.StageEndServerTick,
            redObjectiveWon: false,
            blueObjectiveWon: false,
            anyAfterlifeWindowActive: false,
            out var advanceError), advanceError);
        Assert.Equal(LastToDiePhase.RewardChoice, director.Phase);
    }

    [Fact]
    public void TeamWipeWaitsForAfterlifeWindowThenLoses()
    {
        var director = CreatePlayingDirector();
        Assert.True(director.TrySetPlayerAlive(SoloPlayerId, false, out var deathError), deathError);
        Assert.False(GetSolo(director).IsAlive);

        Assert.True(director.TryAdvancePlayingState(101, false, false, true, out var pendingError), pendingError);
        Assert.Equal(LastToDiePhase.Playing, director.Phase);

        Assert.True(director.TryAdvancePlayingState(102, false, false, false, out var wipeError), wipeError);
        var lost = director.CreateSnapshot();
        Assert.Equal(LastToDiePhase.Lost, lost.Phase);
        Assert.Contains("died", lost.TerminalReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LobbyStartRequiresEveryPresentPlayerReady()
    {
        var director = CreateDirector(seed: 101);
        Assert.True(director.TryAddPlayer(SoloPlayerId, out var firstAddError), firstAddError);
        Assert.True(director.TrySetLobbyReady(SoloPlayerId, true, out var firstReadyError), firstReadyError);
        Assert.True(director.TryAddPlayer(SecondPlayerId, out var secondAddError), secondAddError);
        Assert.False(director.TryStart(out var unreadyError, requireReadyRoster: true));
        Assert.Contains("ready", unreadyError, StringComparison.OrdinalIgnoreCase);
        Assert.True(director.TrySetLobbyReady(SecondPlayerId, true, out var secondReadyError), secondReadyError);
        Assert.True(director.TryStart(out var startError, requireReadyRoster: true), startError);
        Assert.Equal(LastToDiePhase.SurvivorChoice, director.Phase);
    }

    [Fact]
    public void SurvivorCanUnlockWhilePartnerIsStillChoosing()
    {
        var director = CreateDirector(seed: 102);
        Assert.True(director.TryAddPlayer(SoloPlayerId, out var firstAddError), firstAddError);
        Assert.True(director.TryAddPlayer(SecondPlayerId, out var secondAddError), secondAddError);
        Assert.True(director.TryStart(out var startError), startError);
        Assert.True(director.TrySelectSurvivor(
            SoloPlayerId,
            LastToDieSurvivorCatalog.SpyId,
            out var selectError), selectError);
        Assert.True(director.TryClearSurvivor(SoloPlayerId, out var clearError), clearError);
        Assert.Null(director.CreateSnapshot().Players.Single(player => player.PlayerId == SoloPlayerId).SurvivorId);
        Assert.Equal(LastToDiePhase.SurvivorChoice, director.Phase);
    }

    [Fact]
    public void CompletedRunReturnsToCleanSharedLobby()
    {
        var director = CreatePlayingDirector();
        Assert.True(director.TrySetPlayerAlive(SoloPlayerId, false, out var deathError), deathError);
        Assert.True(director.TryAdvancePlayingState(101, false, false, false, out var loseError), loseError);
        Assert.Equal(LastToDiePhase.Lost, director.Phase);

        Assert.True(director.TryReturnToLobby(out var returnError), returnError);
        var lobby = director.CreateSnapshot();
        var player = Assert.Single(lobby.Players);
        Assert.Equal(LastToDiePhase.Lobby, lobby.Phase);
        Assert.Null(player.SurvivorId);
        Assert.Empty(player.OwnedPerks);
        Assert.False(player.IsReady);
        Assert.True(player.IsAlive);
        Assert.Equal(0, player.Kills);
        Assert.Equal(0, lobby.StageNumber);
        Assert.Equal(string.Empty, lobby.TerminalReason);
    }

    [Fact]
    public void CoopRetryWaitsForBothPlayersAndPreservesOnlyTheirSurvivors()
    {
        var director = CreateDirector(seed: 103);
        var loading = AdvanceHostedDirectorToOpeningStage(
            director,
            (SoloPlayerId, LastToDieSurvivorCatalog.SpyId),
            (SecondPlayerId, LastToDieSurvivorCatalog.MedicId));
        Assert.True(director.TrySetStageReady(SoloPlayerId, out var firstReadyError), firstReadyError);
        Assert.True(director.TrySetStageReady(SecondPlayerId, out var secondReadyError), secondReadyError);
        Assert.True(director.TryBeginStage(100, out var beginError), beginError);
        Assert.True(director.TryRecordKills(SoloPlayerId, 3, 101, out var killError), killError);
        Assert.True(director.TrySetPlayerConquistadorStacks(SoloPlayerId, 12, out var stackError), stackError);
        Assert.True(director.TrySetPlayerAlive(SoloPlayerId, false, out var firstDeathError), firstDeathError);
        Assert.True(director.TrySetPlayerAlive(SecondPlayerId, false, out var secondDeathError), secondDeathError);
        Assert.True(director.TryAdvancePlayingState(102, false, false, false, out var loseError), loseError);
        Assert.Equal(LastToDiePhase.Lost, director.Phase);

        Assert.True(director.TrySetRetryReady(SoloPlayerId, out var firstVoteError), firstVoteError);
        var waiting = director.CreateSnapshot();
        Assert.Equal(LastToDiePhase.Lost, waiting.Phase);
        Assert.True(waiting.Players.Single(player => player.PlayerId == SoloPlayerId).IsReady);
        Assert.False(waiting.Players.Single(player => player.PlayerId == SecondPlayerId).IsReady);

        Assert.True(director.TrySetRetryReady(SecondPlayerId, out var secondVoteError), secondVoteError);
        var retry = director.CreateSnapshot();
        Assert.Equal(LastToDiePhase.RewardChoice, retry.Phase);
        Assert.Equal(0, retry.StageNumber);
        Assert.Equal(string.Empty, retry.TerminalReason);
        Assert.Equal(loading.StageInstanceId, retry.StageInstanceId);

        var players = retry.Players.ToDictionary(player => player.PlayerId);
        Assert.Equal(LastToDieSurvivorCatalog.SpyId, players[SoloPlayerId].SurvivorId);
        Assert.Equal(LastToDieSurvivorCatalog.MedicId, players[SecondPlayerId].SurvivorId);
        Assert.All(players.Values, player =>
        {
            Assert.Empty(player.OwnedPerks);
            Assert.NotNull(player.ActiveOffer);
            Assert.False(player.IsReady);
            Assert.True(player.IsAlive);
            Assert.Equal(0, player.Kills);
            Assert.Equal(0, player.ConquistadorStacks);
        });
    }

    [Fact]
    public void ServerAdapterRejectsCustomMapsAndOwnsLastToDieVariant()
    {
        var serverDirector = LastToDieServerDirector.CreateFirstSlice(
            ["Truefort", "Conflict"],
            LastToDieDifficulty.Standard,
            seed: 1,
            runId: RunId);

        Assert.Equal(GameplayVariantKind.LastToDie, serverDirector.Variant);
        Assert.Equal(LastToDiePhase.Lobby, serverDirector.Director.Phase);
        Assert.Throws<InvalidOperationException>(() => LastToDieServerDirector.CreateFirstSlice(
            ["definitely_not_a_stock_map"],
            LastToDieDifficulty.Standard,
            seed: 1));
    }

    [Fact]
    public void ServerAdapterFiltersStockRotationToKothAndCtfOnly()
    {
        var director = LastToDieServerDirector.CreateFirstSlice(
            ["Dirtbowl", "Lumberyard", "Mantic", "Harvest"],
            LastToDieDifficulty.Standard,
            seed: 104,
            maximumPlayers: 1).Director;

        var loading = AdvanceHostedDirectorToOpeningStage(
            director,
            (SoloPlayerId, LastToDieSurvivorCatalog.SniperId));

        Assert.Equal("Harvest", loading.CurrentMap);
        Assert.Throws<InvalidOperationException>(() => LastToDieServerDirector.CreateFirstSlice(
            ["Dirtbowl", "Lumberyard", "Mantic"],
            LastToDieDifficulty.Standard,
            seed: 105));
    }

    [Fact]
    public void HostedCoopStartsWithThreeEnemiesWhileSoloStartsWithTwo()
    {
        var solo = LastToDieServerDirector.CreateFirstSlice(
            ["Harvest"],
            LastToDieDifficulty.Standard,
            seed: 201,
            maximumPlayers: 1).Director;
        var coop = LastToDieServerDirector.CreateFirstSlice(
            ["Harvest"],
            LastToDieDifficulty.Standard,
            seed: 202,
            maximumPlayers: 2).Director;

        var soloLoading = AdvanceHostedDirectorToOpeningStage(
            solo,
            (SoloPlayerId, LastToDieSurvivorCatalog.SpyId));
        var coopLoading = AdvanceHostedDirectorToOpeningStage(
            coop,
            (SoloPlayerId, LastToDieSurvivorCatalog.SpyId),
            (SecondPlayerId, LastToDieSurvivorCatalog.MedicId));

        Assert.Equal(LastToDiePhase.LoadingStage, soloLoading.Phase);
        Assert.Equal(LastToDieRuleset.SoloStartingEnemyCount, soloLoading.EnemyCount);
        Assert.Equal(LastToDiePhase.LoadingStage, coopLoading.Phase);
        Assert.Equal(LastToDieRuleset.CoopStartingEnemyCount, coopLoading.EnemyCount);
    }

    [Theory]
    [InlineData(GameModeKind.KingOfTheHill)]
    [InlineData(GameModeKind.DoubleKingOfTheHill)]
    public void KothWithThreeOrMoreEnemiesUsesBothSpawnDirectionsWithSeededAssignments(GameModeKind mapMode)
    {
        Assert.All(
            GameServer.BuildLastToDieEnemySpawnSides(
                2,
                mapMode,
                new LastToDieRandom(seed: 300, sequence: 7)),
            side => Assert.Equal(PlayerTeam.Blue, side));

        var first = GameServer.BuildLastToDieEnemySpawnSides(
            7,
            mapMode,
            new LastToDieRandom(seed: 301, sequence: 7));
        var replay = GameServer.BuildLastToDieEnemySpawnSides(
            7,
            mapMode,
            new LastToDieRandom(seed: 301, sequence: 7));

        Assert.Equal(first, replay);
        Assert.Contains(PlayerTeam.Red, first);
        Assert.Contains(PlayerTeam.Blue, first);
    }

    [Theory]
    [InlineData(GameModeKind.CaptureTheFlag)]
    [InlineData(GameModeKind.ControlPoint)]
    [InlineData(GameModeKind.Arena)]
    [InlineData(GameModeKind.Generator)]
    [InlineData(GameModeKind.TeamDeathmatch)]
    [InlineData(GameModeKind.Scr)]
    [InlineData(GameModeKind.Vip)]
    public void NonKothMapsAlwaysUseTheNormalBlueEnemySpawn(GameModeKind mapMode)
    {
        var sides = GameServer.BuildLastToDieEnemySpawnSides(
            12,
            mapMode,
            new LastToDieRandom(seed: 302, sequence: 7));

        Assert.All(sides, side => Assert.Equal(PlayerTeam.Blue, side));
    }

    [Fact]
    public void DirectorCheckpointsConquistadorStacksForStageAndReconnectRestoration()
    {
        var director = CreatePlayingDirector();

        Assert.True(director.TrySetPlayerConquistadorStacks(SoloPlayerId, 73, out var error), error);
        Assert.Equal(73, GetSolo(director).ConquistadorStacks);
        Assert.False(director.TrySetPlayerConquistadorStacks(SoloPlayerId, 101, out _));
        Assert.Equal(73, GetSolo(director).ConquistadorStacks);
    }

    [Fact]
    public void TimeoutWaitsForPointOwnershipButObjectiveWinRemainsImmediate()
    {
        var director = CreatePlayingDirector();
        var deadline = director.CreateSnapshot().StageEndServerTick;
        Assert.True(director.TryAdvancePlayingState(deadline, false, false, false, out _, false));
        Assert.Equal(LastToDiePhase.Playing, director.Phase);
        Assert.True(director.TryAdvancePlayingDeadline(deadline + 1, out _));
        Assert.Equal(LastToDiePhase.Playing, director.Phase);
        Assert.True(director.TryAdvancePlayingState(deadline + 2, false, false, false, out _, true));
        Assert.Equal(LastToDiePhase.RewardChoice, director.Phase);

        director = CreatePlayingDirector();
        Assert.True(director.TryAdvancePlayingState(101, true, false, false, out _, false));
        Assert.Equal(LastToDiePhase.RewardChoice, director.Phase);
        director = CreatePlayingDirector();
        Assert.True(director.TryAdvancePlayingState(101, false, true, false, out _, false));
        Assert.Equal(LastToDiePhase.Lost, director.Phase);
    }

    [Fact]
    public void OwnedControlPointConsumesLastToDieStageTimerTwiceAsFast()
    {
        var withoutOwnership = CreatePlayingDirector();
        var withOwnership = CreatePlayingDirector();
        var initialDeadline = withoutOwnership.CreateSnapshot().StageEndServerTick;
        Assert.Equal(initialDeadline, withOwnership.CreateSnapshot().StageEndServerTick);

        for (var serverTick = 101; serverTick <= 200; serverTick++)
        {
            Assert.True(withoutOwnership.TryAdvancePlayingState(
                serverTick,
                redObjectiveWon: false,
                blueObjectiveWon: false,
                anyAfterlifeWindowActive: false,
                out var unownedError,
                redControlPointOwned: false), unownedError);
            Assert.True(withOwnership.TryAdvancePlayingState(
                serverTick,
                redObjectiveWon: false,
                blueObjectiveWon: false,
                anyAfterlifeWindowActive: false,
                out var ownedError,
                redControlPointOwned: true), ownedError);
        }

        var unownedDeadline = withoutOwnership.CreateSnapshot().StageEndServerTick;
        var ownedDeadline = withOwnership.CreateSnapshot().StageEndServerTick;
        Assert.Equal(initialDeadline, unownedDeadline);
        Assert.Equal(initialDeadline - 100, ownedDeadline);
    }

    private static LastToDieDirector CreateDirector(ulong seed)
    {
        var survivors = LastToDieSurvivorCatalog.CreateStock();
        return new LastToDieDirector(
            LastToDieRuleset.CreateDefault(),
            survivors,
            LastToDieExpansionPerkCatalog.Create(survivors),
            ["Truefort", "Conflict", "Harvest"],
            LastToDieDifficulty.Standard,
            seed,
            RunId);
    }

    private static LastToDieDirector CreateDirectorWithUniversalPool(LastToDiePerkId universalPerk)
    {
        var survivors = LastToDieSurvivorCatalog.CreateStock();
        var definitions = Enumerable.Range(0, 12)
            .Select(index => new LastToDiePerkDefinition(
                new LastToDiePerkId($"ltd.perk.spy.test-standard-{index:D2}"),
                LastToDieSurvivorCatalog.SpyId,
                $"Test Standard {index}",
                string.Empty))
            .Concat(Enumerable.Range(0, 16).Select(index => new LastToDiePerkDefinition(
                new LastToDiePerkId($"ltd.perk.allclass.test-rare-{index:D2}"),
                null,
                $"Test Rare {index}",
                string.Empty,
                tier: LastToDiePerkTier.Rare,
                scope: LastToDiePerkScope.AllClass)))
            .Append(new LastToDiePerkDefinition(
                universalPerk,
                null,
                "Test Ultra",
                string.Empty,
                tier: LastToDiePerkTier.Ultra,
                scope: LastToDiePerkScope.AllClass));

        return new LastToDieDirector(
            LastToDieRuleset.CreateDefault(),
            survivors,
            new LastToDiePerkCatalog(survivors, definitions),
            ["Truefort", "Conflict", "Harvest"],
            LastToDieDifficulty.Standard,
            seed: 0x71D2,
            RunId);
    }

    private static LastToDieRewardOffer CompleteCurrentStageAndGetNextOffer(LastToDieDirector director)
    {
        Assert.Equal(LastToDiePhase.LoadingStage, director.Phase);
        Assert.True(director.TrySetStageReady(SoloPlayerId, out var readyError), readyError);
        Assert.True(director.TryBeginStage(serverTick: 100, out var beginError), beginError);
        Assert.True(director.TryAdvancePlayingState(
            serverTick: 101,
            redObjectiveWon: true,
            blueObjectiveWon: false,
            anyAfterlifeWindowActive: false,
            out var advanceError), advanceError);
        return Assert.IsType<LastToDieRewardOffer>(GetSolo(director).ActiveOffer);
    }

    private static LastToDieDirector CreatePlayingDirector()
    {
        var director = CreateDirector(seed: 99);
        AdvanceToOpeningOffer(director);
        var offer = GetSolo(director).ActiveOffer!;
        Assert.True(director.TrySelectReward(SoloPlayerId, offer.OfferId, offer.Choices[0], out var rewardError), rewardError);
        Assert.True(director.TrySetStageReady(SoloPlayerId, out var readyError), readyError);
        Assert.True(director.TryBeginStage(100, out var beginError), beginError);
        return director;
    }

    private static IReadOnlyList<LastToDieRewardOffer> AdvanceDirectorToRewardStage(
        LastToDieDirector director,
        int targetStage,
        bool selectTargetOffers = true)
    {
        if (director.Phase == LastToDiePhase.Lobby)
        {
            AdvanceToOpeningOffer(director);
        }

        while (true)
        {
            var snapshot = director.CreateSnapshot();
            if (snapshot.Phase == LastToDiePhase.RewardChoice)
            {
                var activeOffer = GetSolo(director).ActiveOffer;
                if (activeOffer is null)
                {
                    throw new InvalidOperationException("The director entered reward choice without an active offer.");
                }

                var captureTargetOffers = activeOffer.TargetStage == targetStage;
                var capturedOffers = new List<LastToDieRewardOffer>();
                while ((activeOffer = GetSolo(director).ActiveOffer) is not null)
                {
                    if (captureTargetOffers)
                    {
                        capturedOffers.Add(activeOffer);
                        if (!selectTargetOffers)
                        {
                            return capturedOffers;
                        }
                    }

                    Assert.True(
                        director.TrySelectReward(
                            SoloPlayerId,
                            activeOffer.OfferId,
                            activeOffer.Choices[0],
                            out var rewardError),
                        rewardError);
                }

                if (captureTargetOffers)
                {
                    return capturedOffers;
                }

                continue;
            }

            if (snapshot.Phase != LastToDiePhase.LoadingStage)
            {
                throw new InvalidOperationException($"Cannot advance to a reward from phase {snapshot.Phase}.");
            }

            Assert.True(director.TrySetStageReady(SoloPlayerId, out var readyError), readyError);
            Assert.True(director.TryBeginStage(serverTick: 100, out var beginError), beginError);
            Assert.True(
                director.TryAdvancePlayingState(
                    serverTick: 101,
                    redObjectiveWon: true,
                    blueObjectiveWon: false,
                    anyAfterlifeWindowActive: false,
                    out var stageError),
                stageError);
        }
    }

    private static LastToDieRunSnapshot AdvanceHostedDirectorToOpeningStage(
        LastToDieDirector director,
        params (Guid PlayerId, LastToDieSurvivorId SurvivorId)[] players)
    {
        foreach (var (playerId, _) in players)
        {
            Assert.True(director.TryAddPlayer(playerId, out var addError), addError);
        }

        Assert.True(director.TryStart(out var startError), startError);
        foreach (var (playerId, survivorId) in players)
        {
            Assert.True(director.TrySelectSurvivor(playerId, survivorId, out var survivorError), survivorError);
        }

        foreach (var player in director.CreateSnapshot().Players)
        {
            var offer = Assert.IsType<LastToDieRewardOffer>(player.ActiveOffer);
            Assert.True(
                director.TrySelectReward(player.PlayerId, offer.OfferId, offer.Choices[0], out var rewardError),
                rewardError);
        }

        return director.CreateSnapshot();
    }

    private static void AdvanceToOpeningOffer(LastToDieDirector director)
    {
        Assert.True(director.TryAddPlayer(SoloPlayerId, out var addError), addError);
        Assert.True(director.TryStart(out var startError), startError);
        Assert.True(director.TrySelectSurvivor(SoloPlayerId, LastToDieSurvivorCatalog.SpyId, out var survivorError), survivorError);
        Assert.Equal(LastToDiePhase.RewardChoice, director.Phase);
    }

    private static LastToDiePlayerSnapshot GetSolo(LastToDieDirector director)
        => Assert.Single(director.CreateSnapshot().Players);
}
