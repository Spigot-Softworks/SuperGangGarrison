using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private void ApplySnapshotTransientEntities(SnapshotMessage snapshot)
    {
        ApplySnapshotSentries(snapshot.Sentries);
        ApplySnapshotSentryUpdates(snapshot.SentryUpdateStates);
        ApplySnapshotJumpPads(snapshot.JumpPads);
        ApplySnapshotCivilDefenseTurrets(snapshot.CivilDefenseTurrets);
        ApplySnapshotShots(
            snapshot.Shots,
            snapshot.RemovedShotIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Shots),
            _host.Shots,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                var shot = new ShotProjectileEntity(
                    state.Id,
                    (PlayerTeam)state.Team,
                    state.OwnerId,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY,
                    damagePerHit: Math.Max(0f, state.DamageValue),
                    playerKnockbackImpulse: state.PlayerKnockbackImpulse,
                    playerKnockbackAirborneVerticalScale: state.PlayerKnockbackAirborneVerticalScale,
                    playerKnockbackGroundedVerticalScale: state.PlayerKnockbackGroundedVerticalScale,
                    isBoomstickPellet: state.IsBoomstickPellet);
                shot.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return shot;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY,
                    state.TicksRemaining,
                    Math.Max(0f, state.DamageValue),
                    state.PlayerKnockbackImpulse,
                    state.PlayerKnockbackAirborneVerticalScale,
                    state.PlayerKnockbackGroundedVerticalScale,
                    state.IsBoomstickPellet);
                entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
            },
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining)
                || entity.IsCritical != state.IsCritical
                || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier
                || entity.DamageValue != Math.Max(0f, state.DamageValue)
                || entity.PlayerKnockbackImpulse != state.PlayerKnockbackImpulse
                || entity.PlayerKnockbackAirborneVerticalScale != state.PlayerKnockbackAirborneVerticalScale
                || entity.PlayerKnockbackGroundedVerticalScale != state.PlayerKnockbackGroundedVerticalScale
                || entity.IsBoomstickPellet != state.IsBoomstickPellet);
        ApplySnapshotShots(
            snapshot.Bubbles,
            snapshot.RemovedBubbleIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Bubbles),
            _host.Bubbles,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            state =>
        {
                var bubble = new BubbleProjectileEntity(state.Id, (PlayerTeam)state.Team, state.OwnerId, state.X, state.Y, state.VelocityX, state.VelocityY);
                bubble.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return bubble;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining);
                entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
            },
            shouldApplyExistingState: static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining)
                || entity.IsCritical != state.IsCritical
                || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier);
        ApplySnapshotShots(
            snapshot.Blades,
            snapshot.RemovedBladeIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Blades),
            _host.Blades,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team && entity.OwnerId == state.OwnerId,
            CreateSnapshotBlade,
            ApplySnapshotBladeState,
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId, bloodCount: 6),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining)
                || entity.IsCritical != state.IsCritical
                || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier);
        ReapplyLocallyAheadQuoteBladeAmmoDrain(snapshot.Blades);
        HydrateSnapshotQuoteBladeCounts();
        ApplySnapshotShots(
            snapshot.Needles,
            snapshot.RemovedNeedleIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Needles),
            _host.Needles,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team
                && entity.OwnerId == state.OwnerId
                && entity is MedicHealNeedleProjectileEntity == state.IsMedicHealNeedle
                && (entity is not MedicHealNeedleProjectileEntity medicHealNeedle
                    || medicHealNeedle.LastToDiePayload.Encode()
                        == state.LastToDieMedicKritzM2Payload)
                && entity is ArrowProjectileEntity == state.IsArrow,
            state =>
        {
                NeedleProjectileEntity needle = state.IsArrow
                    ? new ArrowProjectileEntity(
                        state.Id,
                        (PlayerTeam)state.Team,
                        state.OwnerId,
                        state.X,
                        state.Y,
                        state.VelocityX,
                        state.VelocityY,
                        Math.Max(0, (int)MathF.Round(state.DamageValue)),
                        fakeSpeedMultiplier: state.ArrowFakeSpeedMultiplier,
                        appliesLastToDieGuardian: state.AppliesLastToDieGuardian,
                        piercesPlayers: state.PiercesPlayers,
                        appliesLastToDieTranqDarts: state.AppliesLastToDieTranqDarts,
                        lastToDiePoisonDamagePerSecond: state.LastToDiePoisonDamagePerSecond,
                        lastToDieGhostDamageMultiplier: state.LastToDieGhostDamageMultiplier,
                        appliesLastToDieDecapitator: state.AppliesLastToDieDecapitator,
                        isLastToDieDecapitatorFullyCharged: state.IsLastToDieDecapitatorFullyCharged,
                        appliesLastToDieExplosiveTip: state.AppliesLastToDieExplosiveTip,
                        lastToDieAttachedHeadClassId: state.LastToDieAttachedHeadClassId > 0
                            ? (PlayerClass?)state.LastToDieAttachedHeadClassId
                            : null,
                        lastToDieAttachedHeadTeam: state.LastToDieAttachedHeadTeam > 0
                            ? (PlayerTeam?)state.LastToDieAttachedHeadTeam
                            : null)
                    : state.IsMedicHealNeedle
                    ? new MedicHealNeedleProjectileEntity(
                        state.Id,
                        (PlayerTeam)state.Team,
                        state.OwnerId,
                        state.X,
                        state.Y,
                        state.VelocityX,
                        state.VelocityY,
                        lastToDiePayload: LastToDieMedicKritzM2Payload.Decode(
                            state.LastToDieMedicKritzM2Payload),
                        lastToDieJavelinFuseTicksRemaining:
                            state.LastToDieMedicJavelinFuseTicksRemaining,
                        isLastToDieJavelinAnchored:
                            state.IsLastToDieMedicJavelinAnchored,
                        hasLastToDieJavelinExploded:
                            state.HasLastToDieMedicJavelinExploded)
                    : new NeedleProjectileEntity(
                        state.Id,
                        (PlayerTeam)state.Team,
                        state.OwnerId,
                        state.X,
                        state.Y,
                        state.VelocityX,
                        state.VelocityY,
                        damagePerHit: state.DamageValue > 0f
                            ? Math.Max(0, (int)MathF.Round(state.DamageValue))
                            : NeedleProjectileEntity.DamagePerHit);
                needle.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                if (needle is ArrowProjectileEntity arrow)
                    arrow.SetLanded(state.IsLanded);
                return needle;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining);
                if (entity is ArrowProjectileEntity arrow)
                {
                    arrow.SetFakeSpeedMultiplier(state.ArrowFakeSpeedMultiplier);
                    arrow.SetLanded(state.IsLanded);
                    arrow.ConfigureLastToDiePayload(
                        state.AppliesLastToDieGuardian,
                        state.PiercesPlayers,
                        state.AppliesLastToDieTranqDarts,
                        state.LastToDiePoisonDamagePerSecond,
                        state.LastToDieGhostDamageMultiplier,
                        state.AppliesLastToDieDecapitator,
                        state.IsLastToDieDecapitatorFullyCharged,
                        state.AppliesLastToDieExplosiveTip,
                        state.LastToDieAttachedHeadClassId > 0
                            ? (PlayerClass?)state.LastToDieAttachedHeadClassId
                            : null,
                        state.LastToDieAttachedHeadTeam > 0
                            ? (PlayerTeam?)state.LastToDieAttachedHeadTeam
                            : null);
                }
                if (entity is MedicHealNeedleProjectileEntity medicHealNeedle)
                {
                    medicHealNeedle.HydrateLastToDieJavelinState(
                        state.IsLastToDieMedicJavelinAnchored,
                        state.LastToDieMedicJavelinFuseTicksRemaining,
                        state.HasLastToDieMedicJavelinExploded);
                }
                entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
            },
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining)
                || entity.IsCritical != state.IsCritical
                || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier);
        ApplySnapshotShots(
            snapshot.RevolverShots,
            snapshot.RemovedRevolverShotIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.RevolverShots),
            _host.RevolverShots,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team
                && entity.OwnerId == state.OwnerId
                && entity.IsCritical == state.IsCritical
                && entity.CriticalDamageMultiplier == state.CriticalDamageMultiplier
                && entity.DamageValue == Math.Max(0f, state.DamageValue)
                && entity.PlayerKnockbackImpulse == state.PlayerKnockbackImpulse
                && entity.PlayerKnockbackAirborneVerticalScale == state.PlayerKnockbackAirborneVerticalScale
                && entity.PlayerKnockbackGroundedVerticalScale == state.PlayerKnockbackGroundedVerticalScale
                && entity.LastToDieProfile.Encode() == state.LastToDieRevolverProfile
                && entity.AppliesLuckyStrikeStun == state.AppliesLuckyStrikeStun,
            state =>
        {
                var profile = global::OpenGarrison.Core.LastToDie.LastToDieSpyRevolverProfile.Decode(
                    state.LastToDieRevolverProfile);
                var damage = Math.Max(0f, state.DamageValue);
                var shot = new RevolverProjectileEntity(
                    state.Id,
                    (PlayerTeam)state.Team,
                    state.OwnerId,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY,
                    damage,
                    lastToDieProfile: profile,
                    appliesLuckyStrikeStun: state.AppliesLuckyStrikeStun,
                    playerKnockbackImpulse: state.PlayerKnockbackImpulse,
                    playerKnockbackAirborneVerticalScale: state.PlayerKnockbackAirborneVerticalScale,
                    playerKnockbackGroundedVerticalScale: state.PlayerKnockbackGroundedVerticalScale);
                shot.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return shot;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY,
                    state.TicksRemaining,
                    Math.Max(0f, state.DamageValue),
                    state.PlayerKnockbackImpulse,
                    state.PlayerKnockbackAirborneVerticalScale,
                    state.PlayerKnockbackGroundedVerticalScale);
                entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
            },
            entity => TryRegisterServerTerminatedProjectilePlayerHitEffect(
                entity.X, entity.Y, entity.PreviousX, entity.PreviousY, entity.Team, entity.OwnerId),
            static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining));
        ApplySnapshotRockets(
            snapshot.Rockets,
            snapshot.RemovedRocketIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Rockets));
        ApplySnapshotRocketSpawnEvents(snapshot.RocketSpawnEvents);
        ApplySnapshotFlames(
            snapshot.Flames,
            snapshot.RemovedFlameIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Flames));
        ApplySnapshotShots(
            snapshot.Flares,
            snapshot.RemovedFlareIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Flares),
            _host.Flares,
            static (entity, state) => entity.Team == (PlayerTeam)state.Team
                && entity.OwnerId == state.OwnerId
                && entity.Style == (FlareProjectileStyle)state.FlareStyle,
            state =>
        {
                var flare = new FlareProjectileEntity(
                    state.Id,
                    (PlayerTeam)state.Team,
                    state.OwnerId,
                    state.X,
                    state.Y,
                    state.VelocityX,
                    state.VelocityY,
                    ticksRemaining: state.TicksRemaining,
                    damagePerHit: state.DamageValue > 0f
                        ? state.DamageValue
                        : FlareProjectileEntity.DefaultDamagePerHit,
                    style: (FlareProjectileStyle)state.FlareStyle);
                flare.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
                return flare;
            },
            static (entity, state) =>
            {
                entity.ApplyNetworkState(state.X, state.Y, state.VelocityX, state.VelocityY, state.TicksRemaining);
                entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
            },
            shouldApplyExistingState: static (entity, state) => ShouldApplyLocallySimulatedProjectileState(entity.TicksRemaining, state.TicksRemaining)
                || entity.IsCritical != state.IsCritical
                || entity.CriticalDamageMultiplier != state.CriticalDamageMultiplier);
        ApplySnapshotMines(
            snapshot.Mines,
            snapshot.RemovedMineIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Mines));
        ApplySnapshotGrenades(
            snapshot.Grenades,
            snapshot.RemovedGrenadeIds,
            IsSnapshotEntityCollectionComplete(snapshot, SnapshotEntityCollectionCompletenessFlags.Grenades));
        ApplySnapshotHealthPacks(snapshot.HealthPacks, snapshot.RemovedHealthPackIds);
        ApplySnapshotGibSpawnEvents(snapshot.GibSpawnEvents);
        // Blood drops are now generated locally on the client - not synced from server
        ApplySnapshotDeadBodies(snapshot.DeadBodies);
        ApplySnapshotSentryGibs(snapshot.SentryGibs);
        ApplySnapshotJumpPadGibs(snapshot.JumpPadGibs);
    }
}
