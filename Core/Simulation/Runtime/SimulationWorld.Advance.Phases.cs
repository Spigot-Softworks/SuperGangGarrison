namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvancePrePlayerSimulationPhase()
    {
        ApplyExperimentalRageEffects();
        AdvanceMedicUberEffects();
        AdvanceProjectileAndTransientEntityPhase();
        AdvancePresentationAndChatPhase();
    }

    private void AdvanceProjectileAndTransientEntityPhase()
    {
        AdvanceCombatTraces();
        AdvanceShots();
        AdvanceBubbles();
        AdvanceBlades();
        AdvanceNeedles();
        AdvanceRevolverShots();
        AdvanceStabAnimations();
        AdvanceStabMasks();
        AdvanceFlames();
        AdvanceFlares();
        AdvanceRockets();
        AdvanceMines();
        AdvancePlayerGibs();
        AdvanceBloodDrops();
        AdvanceDeadBodies();
        AdvanceSentryGibs();
    }

    private void AdvancePresentationAndChatPhase()
    {
        AdvanceKillFeed();
        AdvanceLocalDeathCam();

        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            AdvanceNetworkPlayerChatBubbleState(NetworkPlayerSlots[index]);
        }

        if (EnemyPlayerEnabled)
        {
            EnemyPlayer.AdvanceChatBubbleState();
        }

        FriendlyDummy.AdvanceChatBubbleState();
    }

    private void AdvancePlayerSimulationPhase()
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            AdvancePlayableNetworkPlayer(NetworkPlayerSlots[index]);
        }

        AdvanceEnemyDummy();

        if (FriendlyDummyEnabled && FriendlyDummy.IsAlive)
        {
            ApplyRoomForces(FriendlyDummy);
            FriendlyDummy.Advance(default, false, Level, FriendlyDummy.Team, Config.FixedDeltaSeconds);
            UpdateSpawnRoomState(FriendlyDummy);
            TryActivatePendingSpyBackstab(FriendlyDummy);
            ApplyHealingCabinets(FriendlyDummy);
            ApplyRoomHazards(FriendlyDummy);
        }
    }

    private void AdvancePostPlayerSimulationPhase()
    {
        AdvanceHealthPacks();
        AdvanceDroppedWeapons();
        AdvanceAfterburnAlertBubbles();
        AdvanceExperimentalRageState();
        AdvanceActiveMatchObjectives();
        AdvanceActiveMatchResolution();
        AdvanceSentries();
    }
}
