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

        Assert.Equal(127, catalog.Definitions.Count);
        Assert.Equal(25, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.SoldierId));
        Assert.Equal(13, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.DemoknightId));
        Assert.Equal(26, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.EngineerId));
        Assert.Equal(25, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.SpyId));
        Assert.Equal(20, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.MedicId));
        Assert.Equal(18, catalog.Definitions.Count(perk => perk.SurvivorId == LastToDieSurvivorCatalog.SniperId));
        Assert.Equal(127, catalog.Definitions.Select(perk => perk.Id).Distinct().Count());

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

        Assert.True(world.ClearLastToDiePlayerPredictionProfile(2));
        Assert.Equal(baseRunPower, player.RunPower, precision: 3);
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
    public void DefaultRulesetMatchesExistingNineStageCurve()
    {
        var ruleset = LastToDieRuleset.CreateDefault(ticksPerSecond: 30);

        Assert.Equal(new LastToDieStageDefinition(1, 2, 5_400), ruleset.GetStage(1));
        Assert.Equal(new LastToDieStageDefinition(9, 10, 19_800), ruleset.GetStage(9));
        Assert.Equal(54_000, ruleset.RunTimeLimitTicks);
        Assert.Equal(90, ruleset.KillTimerReductionTicks);
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
        Assert.Equal(5_500, playing.StageEndServerTick);
        Assert.Equal(54_100, playing.RunEndServerTick);

        var structuralRevision = playing.StructuralRevision;
        Assert.True(director.TryRecordKills(SoloPlayerId, killCount: 2, serverTick: 200, out var killError), killError);
        var afterKills = director.CreateSnapshot();
        Assert.Equal(5_320, afterKills.StageEndServerTick);
        Assert.Equal(structuralRevision, afterKills.StructuralRevision);
        Assert.Equal(2, GetSolo(director).Kills);

        Assert.True(director.TryAdvancePlayingState(
            serverTick: 5_320,
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
    public void LobbyStartRequiresEveryConfiguredSeatReady()
    {
        var director = CreateDirector(seed: 101);
        Assert.True(director.TryAddPlayer(SoloPlayerId, out var firstAddError), firstAddError);
        Assert.True(director.TrySetLobbyReady(SoloPlayerId, true, out var firstReadyError), firstReadyError);
        Assert.False(director.TryStart(out var missingSeatError, requireReadyRoster: true));
        Assert.Contains("seat", missingSeatError, StringComparison.OrdinalIgnoreCase);

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

    [Fact]
    public void ThreeOrMoreEnemiesAlwaysUseBothSpawnDirectionsWithSeededAssignments()
    {
        Assert.All(
            GameServer.BuildLastToDieEnemySpawnSides(
                2,
                new LastToDieRandom(seed: 300, sequence: 7)),
            side => Assert.Equal(PlayerTeam.Blue, side));

        var first = GameServer.BuildLastToDieEnemySpawnSides(
            7,
            new LastToDieRandom(seed: 301, sequence: 7));
        var replay = GameServer.BuildLastToDieEnemySpawnSides(
            7,
            new LastToDieRandom(seed: 301, sequence: 7));

        Assert.Equal(first, replay);
        Assert.Contains(PlayerTeam.Red, first);
        Assert.Contains(PlayerTeam.Blue, first);
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
