using System.Collections.Generic;

namespace OpenGarrison.Core.BotBrain;

public sealed class BotBrainChatBubbleController
{
    private const int FrameAlert = ChatBubbleFrameCatalog.Alert;
    private const int FrameQuestion = ChatBubbleFrameCatalog.Question;
    private const int FrameHappy = ChatBubbleFrameCatalog.Happy;
    private const int FrameObjectiveAlert = ChatBubbleFrameCatalog.ObjectiveAlert;
    private const int FrameThumbsUp = ChatBubbleFrameCatalog.ThumbsUp;
    private const int FrameAttack = ChatBubbleFrameCatalog.Attack;
    private const int FrameShield = ChatBubbleFrameCatalog.Shield;
    private const int FrameConfirm = ChatBubbleFrameCatalog.Confirm;
    private const int FrameDeny = ChatBubbleFrameCatalog.Deny;
    private const int FrameMedic = ChatBubbleFrameCatalog.Medic;
    private const int FrameBurning = ChatBubbleFrameCatalog.Burning;
    private const float ObjectiveAwarenessDistance = 900f;
    private const float CarrierAwarenessDistance = 1200f;
    private const float ControlPointAwarenessDistance = 220f;
    private const float LowHealthFraction = 0.35f;
    private const float LowHealthRecoveryFraction = 0.55f;
    private const int GlobalCooldownTicks = 120;
    private const int ObjectiveCooldownTicks = 90;
    private const int CombatCooldownTicks = 105;
    private const int RecoveryCooldownTicks = 150;
    private const int AmbientCooldownTicks = 420;
    private const int SignalCooldownTicks = 240;
    private const int ResponseCooldownTicks = 180;
    private const int DirectSeekRedXDelayTicks = 45;
    private const int MedicPocketResponseDelayTicks = 35;
    private const float AllyResponseDistance = 560f;
    private const int SignalChancePercent = 24;
    private const int AllyResponseChancePercent = 28;
    private const int MaxAllyResponsesPerSignal = 2;

    private readonly Dictionary<byte, BotBrainChatBubbleState> _statesBySlot = [];

    public void Reset()
    {
        _statesBySlot.Clear();
    }

    public void RemoveSlot(byte slot)
    {
        _statesBySlot.Remove(slot);
    }

    public PlayerInputSnapshot Update(
        SimulationWorld world,
        byte slot,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainController controller,
        PlayerInputSnapshot input,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        var state = GetOrCreateState(slot, self);
        if (!self.IsAlive)
        {
            state.WasAlive = false;
            state.LowHealthCallIssued = false;
            state.WasCarryingIntel = false;
            state.LastSeenCombatTargetId = null;
            state.LastKnownHealth = self.Health;
            state.LastKills = self.Kills;
            state.LastCaps = self.Caps;
            return input;
        }

        ProcessPendingBubbles(world, slot, self, state, controlledTeamsBySlot);

        var killCelebration = self.Kills > state.LastKills;
        var captureCelebration = self.Caps > state.LastCaps;
        if (killCelebration || captureCelebration)
        {
            var ctfCaptureCelebration = captureCelebration && world.MatchRules.Mode == GameModeKind.CaptureTheFlag;
            if (killCelebration || ctfCaptureCelebration)
            {
                input = ApplyTauntCelebrationInput(input);
            }

            if (ctfCaptureCelebration)
            {
                TryTriggerTeamZ5CelebrationEmotes(world, team, self.Kills, self.Caps, controlledTeamsBySlot);
            }
            else
            {
                TryTriggerZ5CelebrationEmote(world, slot, self.Kills, self.Caps);
            }
        }

        if (!state.WasAlive)
        {
            state.LastGlobalReactionFrame = world.Frame - GlobalCooldownTicks;
            state.LastAmbientReactionFrame = world.Frame;
            state.LowHealthCallIssued = false;
        }

        if (TryTriggerCommunicationSignal(world, slot, self, team, controller, state, controlledTeamsBySlot))
        {
            UpdateLiveState(self, state);
            return input;
        }

        if (TrySelectReaction(world, slot, self, team, controller, state, out var reaction)
            && TryTriggerReaction(world, slot, self, reaction, state))
        {
            MarkReaction(state, world.Frame, reaction);
        }

        if (GetHealthFraction(self) >= LowHealthRecoveryFraction)
        {
            state.LowHealthCallIssued = false;
        }

        UpdateLiveState(self, state);
        return input;
    }

    private static void UpdateLiveState(PlayerEntity self, BotBrainChatBubbleState state)
    {
        state.WasAlive = true;
        state.WasCarryingIntel = self.IsCarryingIntel;
        state.LastKnownHealth = self.Health;
        state.LastKills = self.Kills;
        state.LastCaps = self.Caps;
    }

    private static bool TrySelectReaction(
        SimulationWorld world,
        byte slot,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainController controller,
        BotBrainChatBubbleState state,
        out BotBrainChatBubbleReaction reaction)
    {
        if (self.IsBurning)
        {
            reaction = default;
            return false;
        }

        if (!state.WasCarryingIntel && self.IsCarryingIntel)
        {
            reaction = new BotBrainChatBubbleReaction(FrameConfirm, BotBrainChatBubbleCategory.Objective, Priority: 100);
            return true;
        }

        if (ShouldCallMedic(self, state))
        {
            reaction = new BotBrainChatBubbleReaction(FrameMedic, BotBrainChatBubbleCategory.Combat, Priority: 95);
            return true;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && TrySelectCaptureTheFlagReaction(world, self, team, out reaction))
        {
            return true;
        }

        if (TrySelectControlPointReaction(world, self, team, out reaction))
        {
            return true;
        }

        var combatTarget = TargetSelector.SelectCombatTarget(self, world, team);
        if (combatTarget is { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            && state.LastSeenCombatTargetId != target.Id)
        {
            state.LastSeenCombatTargetId = target.Id;
            reaction = new BotBrainChatBubbleReaction(ResolveClassPortraitFrame(target), BotBrainChatBubbleCategory.Combat, Priority: 70);
            return true;
        }

        if (!string.IsNullOrEmpty(controller.LastSemanticRecoveryTrace)
            || controller.LastDirectDriveTrace.Contains("recovery", StringComparison.OrdinalIgnoreCase))
        {
            reaction = new BotBrainChatBubbleReaction(FrameQuestion, BotBrainChatBubbleCategory.Recovery, Priority: 55);
            return true;
        }

        if (ShouldTriggerAmbientReaction(world, slot, state))
        {
            reaction = new BotBrainChatBubbleReaction(FrameHappy, BotBrainChatBubbleCategory.Ambient, Priority: 10);
            return true;
        }

        reaction = default;
        return false;
    }

    private static bool TrySelectCaptureTheFlagReaction(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        out BotBrainChatBubbleReaction reaction)
    {
        var enemyCarrier = FindCarrier(world, team);
        if (enemyCarrier is not null
            && DistanceSquared(self.X, self.Y, enemyCarrier.X, enemyCarrier.Y) <= CarrierAwarenessDistance * CarrierAwarenessDistance)
        {
            reaction = new BotBrainChatBubbleReaction(ResolveOwnIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 90);
            return true;
        }

        var enemyIntel = GetEnemyIntelState(world, team);
        if (enemyIntel.IsDropped
            && DistanceSquared(self.X, self.Y, enemyIntel.X, enemyIntel.Y) <= ObjectiveAwarenessDistance * ObjectiveAwarenessDistance)
        {
            reaction = new BotBrainChatBubbleReaction(ResolveEnemyIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 85);
            return true;
        }

        var ownIntel = GetOwnIntelState(world, team);
        if (ownIntel.IsDropped
            && DistanceSquared(self.X, self.Y, ownIntel.X, ownIntel.Y) <= ObjectiveAwarenessDistance * ObjectiveAwarenessDistance)
        {
            reaction = new BotBrainChatBubbleReaction(ResolveOwnIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 82);
            return true;
        }

        var friendlyCarrier = FindCarrier(world, GetOpposingTeam(team));
        if (friendlyCarrier is not null
            && friendlyCarrier.Id != self.Id
            && DistanceSquared(self.X, self.Y, friendlyCarrier.X, friendlyCarrier.Y) <= ObjectiveAwarenessDistance * ObjectiveAwarenessDistance)
        {
            reaction = new BotBrainChatBubbleReaction(ResolveEnemyIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 65);
            return true;
        }

        reaction = default;
        return false;
    }

    private static bool TrySelectControlPointReaction(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        out BotBrainChatBubbleReaction reaction)
    {
        if (world.MatchRules.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill or GameModeKind.Arena))
        {
            reaction = default;
            return false;
        }

        foreach (var point in world.ControlPoints)
        {
            if (DistanceSquared(self.X, self.Y, point.HealingAuraCenterX, point.HealingAuraCenterY) > ControlPointAwarenessDistance * ControlPointAwarenessDistance)
            {
                continue;
            }

            if (point.CappingTeam == team)
            {
                reaction = new BotBrainChatBubbleReaction(FrameConfirm, BotBrainChatBubbleCategory.Objective, Priority: 75);
                return true;
            }

            if (point.CappingTeam == GetOpposingTeam(team))
            {
                reaction = new BotBrainChatBubbleReaction(FrameObjectiveAlert, BotBrainChatBubbleCategory.Objective, Priority: 88);
                return true;
            }

            if (world.MatchRules.Mode == GameModeKind.KingOfTheHill && point.Team == team)
            {
                reaction = new BotBrainChatBubbleReaction(FrameShield, BotBrainChatBubbleCategory.Objective, Priority: 45);
                return true;
            }
        }

        reaction = default;
        return false;
    }

    private static bool TryTriggerReaction(
        SimulationWorld world,
        byte slot,
        PlayerEntity self,
        BotBrainChatBubbleReaction reaction,
        BotBrainChatBubbleState state,
        bool bypassCooldown = false)
    {
        if (self.IsChatBubbleVisible && self.ChatBubbleFrameIndex == FrameBurning)
        {
            return false;
        }

        if (reaction.Frame == state.LastFrame && reaction.Priority < 90)
        {
            return false;
        }

        if (!bypassCooldown && world.Frame - state.LastGlobalReactionFrame < GetGlobalCooldownTicks(slot))
        {
            return false;
        }

        if (!bypassCooldown && !IsCategoryCooldownReady(world.Frame, reaction.Category, state))
        {
            return false;
        }

        return world.TryTriggerNetworkPlayerChatBubble(slot, reaction.Frame);
    }

    private static void MarkReaction(BotBrainChatBubbleState state, long frame, BotBrainChatBubbleReaction reaction)
    {
        state.LastGlobalReactionFrame = frame;
        state.LastFrame = reaction.Frame;
        switch (reaction.Category)
        {
            case BotBrainChatBubbleCategory.Objective:
                state.LastObjectiveReactionFrame = frame;
                break;
            case BotBrainChatBubbleCategory.Combat:
                state.LastCombatReactionFrame = frame;
                if (reaction.Frame == FrameMedic)
                {
                    state.LowHealthCallIssued = true;
                }

                break;
            case BotBrainChatBubbleCategory.Recovery:
                state.LastRecoveryReactionFrame = frame;
                break;
            case BotBrainChatBubbleCategory.Ambient:
                state.LastAmbientReactionFrame = frame;
                break;
        }
    }

    private void ProcessPendingBubbles(
        SimulationWorld world,
        byte slot,
        PlayerEntity self,
        BotBrainChatBubbleState state,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        if (state.PendingBubbles.Count == 0)
        {
            return;
        }

        for (var index = 0; index < state.PendingBubbles.Count; index += 1)
        {
            var pending = state.PendingBubbles[index];
            if (world.Frame < pending.TriggerFrame)
            {
                continue;
            }

            state.PendingBubbles.RemoveAt(index);
            if (TryTriggerReaction(world, slot, self, pending.Reaction, state, bypassCooldown: true))
            {
                MarkReaction(state, world.Frame, pending.Reaction);
                if (pending.Signal.HasValue)
                {
                    TryTriggerNearbyAllyResponses(world, pending.Signal.Value, controlledTeamsBySlot);
                }
            }

            return;
        }
    }

    private bool TryTriggerCommunicationSignal(
        SimulationWorld world,
        byte slot,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainController controller,
        BotBrainChatBubbleState state,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        if (!TryResolveCommunicationSignal(world, slot, self, team, controller, state, out var signal, out var firstReaction, out var followupReaction))
        {
            return false;
        }

        if (!ShouldTriggerSignal(world.Frame, slot, signal.Kind, signal.Key, state))
        {
            return false;
        }

        if (!TryTriggerReaction(world, slot, self, firstReaction, state))
        {
            return false;
        }

        MarkReaction(state, world.Frame, firstReaction);
        state.LastSignalFrame = world.Frame;
        state.LastSignalKind = signal.Kind;
        state.LastSignalKey = signal.Key;

        if (signal.Kind == BotBubbleSignalKind.MedicPocket)
        {
            state.LastMedicPocketTargetId = GetMedicPocketTargetId(signal);
            TryScheduleMedicPocketResponse(world, signal, controlledTeamsBySlot);
            return true;
        }

        if (followupReaction.HasValue)
        {
            state.PendingBubbles.Add(new PendingBubble(
                world.Frame + DirectSeekRedXDelayTicks,
                followupReaction.Value,
                signal));
        }
        else
        {
            TryTriggerNearbyAllyResponses(world, signal, controlledTeamsBySlot);
        }

        return true;
    }

    private static bool TryResolveCommunicationSignal(
        SimulationWorld world,
        byte slot,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainController controller,
        BotBrainChatBubbleState state,
        out BotBubbleSignal signal,
        out BotBrainChatBubbleReaction firstReaction,
        out BotBrainChatBubbleReaction? followupReaction)
    {
        followupReaction = null;
        var directTrace = controller.LastDirectDriveTrace;
        if (TryResolveMedicPocketSignal(world, slot, self, team, controller, state, out signal, out firstReaction))
        {
            return true;
        }

        if (directTrace.Contains("enemyCarrier", StringComparison.OrdinalIgnoreCase)
            && TryFindCarrier(world, team, self, out var enemyCarrier))
        {
            signal = CreateSignal(slot, self, team, BotBubbleSignalKind.EnemyCarrier, $"enemyCarrier:{enemyCarrier.Id}");
            firstReaction = new BotBrainChatBubbleReaction(ResolveOwnIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 91);
            return true;
        }

        if (directTrace.Contains("droppedEnemyIntel", StringComparison.OrdinalIgnoreCase))
        {
            signal = CreateSignal(slot, self, team, BotBubbleSignalKind.DroppedEnemyIntel, "droppedEnemyIntel");
            firstReaction = new BotBrainChatBubbleReaction(ResolveEnemyIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 86);
            return true;
        }

        if (directTrace.Contains("ownDroppedIntel", StringComparison.OrdinalIgnoreCase))
        {
            signal = CreateSignal(slot, self, team, BotBubbleSignalKind.OwnDroppedIntel, "ownDroppedIntel");
            firstReaction = new BotBrainChatBubbleReaction(ResolveOwnIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 87);
            return true;
        }

        if (directTrace.Contains("escortCarrier", StringComparison.OrdinalIgnoreCase)
            && TryFindCarrier(world, GetOpposingTeam(team), self, out var friendlyCarrier))
        {
            signal = CreateSignal(slot, self, team, BotBubbleSignalKind.EscortCarrier, $"escortCarrier:{friendlyCarrier.Id}");
            firstReaction = new BotBrainChatBubbleReaction(ResolveEnemyIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 72);
            return true;
        }

        var combatTarget = TargetSelector.SelectCombatTarget(self, world, team);
        if (combatTarget is { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            && IsDirectEnemySeekTrace(directTrace)
            && state.LastDirectSeekTargetId != target.Id)
        {
            state.LastDirectSeekTargetId = target.Id;
            signal = CreateSignal(slot, self, team, BotBubbleSignalKind.DirectEnemySeek, $"directEnemy:{target.Id}");
            firstReaction = new BotBrainChatBubbleReaction(ResolveClassPortraitFrame(target), BotBrainChatBubbleCategory.Combat, Priority: 80);
            followupReaction = new BotBrainChatBubbleReaction(FrameDeny, BotBrainChatBubbleCategory.Combat, Priority: 79);
            return true;
        }

        if (world.MatchRules.Mode == GameModeKind.CaptureTheFlag
            && !self.IsCarryingIntel
            && IsEnemyIntelAvailable(world, team))
        {
            signal = CreateSignal(slot, self, team, BotBubbleSignalKind.GoEnemyIntel, "goEnemyIntel");
            firstReaction = new BotBrainChatBubbleReaction(ResolveEnemyIntelFrame(team), BotBrainChatBubbleCategory.Objective, Priority: 52);
            return true;
        }

        signal = default;
        firstReaction = default;
        return false;
    }

    private static bool TryResolveMedicPocketSignal(
        SimulationWorld world,
        byte slot,
        PlayerEntity self,
        PlayerTeam team,
        BotBrainController controller,
        BotBrainChatBubbleState state,
        out BotBubbleSignal signal,
        out BotBrainChatBubbleReaction firstReaction)
    {
        if (self.ClassId != PlayerClass.Medic
            || !controller.LastMedicHealTargetIsPocket
            || !controller.LastMedicHealTargetId.HasValue
            || state.LastMedicPocketTargetId == controller.LastMedicHealTargetId.Value)
        {
            signal = default;
            firstReaction = default;
            return false;
        }

        var target = FindPlayerById(world, controller.LastMedicHealTargetId.Value);
        if (target is null || !target.IsAlive || target.Team != team)
        {
            signal = default;
            firstReaction = default;
            return false;
        }

        signal = CreateSignal(slot, self, team, BotBubbleSignalKind.MedicPocket, $"medicPocket:{target.Id}");
        firstReaction = new BotBrainChatBubbleReaction(ResolveClassPortraitFrame(target), BotBrainChatBubbleCategory.Combat, Priority: 83);
        return true;
    }

    private static bool ShouldTriggerSignal(
        long frame,
        byte slot,
        BotBubbleSignalKind kind,
        string key,
        BotBrainChatBubbleState state)
    {
        if (state.LastSignalAttemptKind == kind
            && string.Equals(state.LastSignalAttemptKey, key, StringComparison.Ordinal)
            && frame - state.LastSignalAttemptFrame < SignalCooldownTicks)
        {
            return false;
        }

        state.LastSignalAttemptFrame = frame;
        state.LastSignalAttemptKind = kind;
        state.LastSignalAttemptKey = key;
        if (kind == BotBubbleSignalKind.MedicPocket)
        {
            return true;
        }

        return ChanceRoll(frame, slot, (int)kind, key.GetHashCode(StringComparison.Ordinal), SignalChancePercent);
    }

    private void TryTriggerNearbyAllyResponses(
        SimulationWorld world,
        BotBubbleSignal signal,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        var responses = 0;
        foreach (var entry in controlledTeamsBySlot)
        {
            if (entry.Key == signal.SourceSlot || entry.Value != signal.Team)
            {
                continue;
            }

            if (responses >= MaxAllyResponsesPerSignal)
            {
                return;
            }

            if (!world.TryGetNetworkPlayer(entry.Key, out var ally) || !ally.IsAlive)
            {
                continue;
            }

            if (DistanceSquared(signal.X, signal.Y, ally.X, ally.Y) > AllyResponseDistance * AllyResponseDistance)
            {
                continue;
            }

            var allyState = GetOrCreateState(entry.Key, ally);
            if (world.Frame - allyState.LastResponseFrame < ResponseCooldownTicks)
            {
                continue;
            }

            if (!ChanceRoll(world.Frame, signal.SourceSlot, entry.Key, (int)signal.Kind, AllyResponseChancePercent))
            {
                continue;
            }

            var response = new BotBrainChatBubbleReaction(
                ResolveAllyResponseFrame(signal.Kind, entry.Key, world.Frame),
                BotBrainChatBubbleCategory.Objective,
                Priority: 68);
            if (!TryTriggerReaction(world, entry.Key, ally, response, allyState))
            {
                continue;
            }

            MarkReaction(allyState, world.Frame, response);
            allyState.LastResponseFrame = world.Frame;
            responses += 1;
        }
    }

    private void TryScheduleMedicPocketResponse(
        SimulationWorld world,
        BotBubbleSignal signal,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        var targetPlayerId = GetMedicPocketTargetId(signal);
        if (!targetPlayerId.HasValue
            || !TryFindControlledPlayerSlot(world, controlledTeamsBySlot, targetPlayerId.Value, signal.Team, out var targetSlot, out var target))
        {
            return;
        }

        var targetState = GetOrCreateState(targetSlot, target);
        if (world.Frame - targetState.LastResponseFrame < ResponseCooldownTicks)
        {
            return;
        }

        targetState.PendingBubbles.Add(new PendingBubble(
            world.Frame + MedicPocketResponseDelayTicks,
            new BotBrainChatBubbleReaction(ResolveMedicPocketResponseFrame(targetSlot, world.Frame), BotBrainChatBubbleCategory.Combat, Priority: 82),
            null));
        targetState.LastResponseFrame = world.Frame;
    }

    private static int? GetMedicPocketTargetId(BotBubbleSignal signal)
    {
        var separatorIndex = signal.Key.LastIndexOf(':');
        return separatorIndex >= 0
            && int.TryParse(signal.Key[(separatorIndex + 1)..], out var targetPlayerId)
            ? targetPlayerId
            : null;
    }

    private static int ResolveAllyResponseFrame(BotBubbleSignalKind kind, byte slot, long frame)
    {
        var variant = PositiveModulo(HashCode.Combine((int)(frame & 0x7fffffff), slot, (int)kind), 3);
        return kind switch
        {
            BotBubbleSignalKind.OwnDroppedIntel => variant == 0 ? FrameShield : variant == 1 ? FrameAttack : FrameConfirm,
            BotBubbleSignalKind.EnemyCarrier => variant == 0 ? FrameAttack : FrameConfirm,
            BotBubbleSignalKind.DirectEnemySeek => variant == 0 ? FrameAttack : variant == 1 ? FrameConfirm : FrameThumbsUp,
            BotBubbleSignalKind.EscortCarrier => variant == 0 ? FrameShield : variant == 1 ? FrameThumbsUp : FrameConfirm,
            BotBubbleSignalKind.MedicPocket => variant == 0 ? FrameThumbsUp : FrameConfirm,
            _ => variant == 0 ? FrameConfirm : FrameThumbsUp,
        };
    }

    private static int ResolveMedicPocketResponseFrame(byte slot, long frame)
    {
        return PositiveModulo(HashCode.Combine((int)(frame & 0x7fffffff), slot, 0x4D3D1C), 2) == 0
            ? FrameThumbsUp
            : FrameConfirm;
    }

    private static PlayerInputSnapshot ApplyTauntCelebrationInput(PlayerInputSnapshot input)
    {
        return input with
        {
            Left = false,
            Right = false,
            Up = false,
            Down = false,
            BuildSentry = false,
            DestroySentry = false,
            Taunt = true,
            FirePrimary = false,
            FireSecondary = false,
            DebugKill = false,
            DropIntel = false,
            UseAbility = false,
            InteractWeapon = false,
        };
    }

    private void TryTriggerTeamZ5CelebrationEmotes(
        SimulationWorld world,
        PlayerTeam team,
        int seedKills,
        int seedCaps,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot)
    {
        foreach (var entry in controlledTeamsBySlot)
        {
            if (entry.Value != team)
            {
                continue;
            }

            TryTriggerZ5CelebrationEmote(world, entry.Key, seedKills, seedCaps);
        }
    }

    private void TryTriggerZ5CelebrationEmote(
        SimulationWorld world,
        byte slot,
        int seedKills,
        int seedCaps)
    {
        if (!ShouldTriggerZ5CelebrationEmote(world.Frame, slot, seedKills, seedCaps))
        {
            return;
        }

        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        if (player.IsChatBubbleVisible && player.ChatBubbleFrameIndex == FrameBurning)
        {
            return;
        }

        if (!world.TryTriggerNetworkPlayerChatBubble(slot, FrameHappy))
        {
            return;
        }

        var state = GetOrCreateState(slot, player);
        MarkReaction(
            state,
            world.Frame,
            new BotBrainChatBubbleReaction(FrameHappy, BotBrainChatBubbleCategory.Objective, Priority: 95));
    }

    private static bool ShouldTriggerZ5CelebrationEmote(long frame, byte slot, int kills, int caps)
    {
        var hash = HashCode.Combine((int)(frame & 0x7fffffff), slot, kills, caps, 0x5A17);
        return PositiveModulo(hash, 100) < 20;
    }

    private static bool ChanceRoll(long frame, int a, int b, int c, int chancePercent)
    {
        var hash = HashCode.Combine((int)(frame & 0x7fffffff), a, b, c);
        return PositiveModulo(hash, 100) < chancePercent;
    }

    private static bool IsCategoryCooldownReady(long frame, BotBrainChatBubbleCategory category, BotBrainChatBubbleState state)
    {
        return category switch
        {
            BotBrainChatBubbleCategory.Objective => frame - state.LastObjectiveReactionFrame >= ObjectiveCooldownTicks,
            BotBrainChatBubbleCategory.Combat => frame - state.LastCombatReactionFrame >= CombatCooldownTicks,
            BotBrainChatBubbleCategory.Recovery => frame - state.LastRecoveryReactionFrame >= RecoveryCooldownTicks,
            BotBrainChatBubbleCategory.Ambient => frame - state.LastAmbientReactionFrame >= AmbientCooldownTicks,
            _ => false,
        };
    }

    private static bool ShouldCallMedic(PlayerEntity self, BotBrainChatBubbleState state)
    {
        return !state.LowHealthCallIssued
            && GetHealthFraction(self) <= LowHealthFraction;
    }

    private static bool ShouldTriggerAmbientReaction(SimulationWorld world, byte slot, BotBrainChatBubbleState state)
    {
        return world.Frame - state.LastAmbientReactionFrame >= AmbientCooldownTicks + ((slot % 5) * 30);
    }

    private BotBrainChatBubbleState GetOrCreateState(byte slot, PlayerEntity self)
    {
        if (_statesBySlot.TryGetValue(slot, out var state))
        {
            return state;
        }

        state = new BotBrainChatBubbleState
        {
            WasAlive = self.IsAlive,
            WasCarryingIntel = self.IsCarryingIntel,
            LastKnownHealth = self.Health,
            LastKills = self.Kills,
            LastCaps = self.Caps,
        };
        _statesBySlot[slot] = state;
        return state;
    }

    private static int GetGlobalCooldownTicks(byte slot)
    {
        return GlobalCooldownTicks + ((slot % 4) * 10);
    }

    private static PlayerEntity? FindCarrier(SimulationWorld world, PlayerTeam carriedIntelTeam)
    {
        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (candidate.IsAlive && candidate.Team != carriedIntelTeam && candidate.IsCarryingIntel)
            {
                return candidate;
            }
        }

        return null;
    }

    private static PlayerEntity? FindPlayerById(SimulationWorld world, int playerId)
    {
        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (candidate.Id == playerId)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryFindControlledPlayerSlot(
        SimulationWorld world,
        IReadOnlyDictionary<byte, PlayerTeam> controlledTeamsBySlot,
        int playerId,
        PlayerTeam team,
        out byte slot,
        out PlayerEntity player)
    {
        foreach (var entry in controlledTeamsBySlot)
        {
            if (entry.Value != team
                || !world.TryGetNetworkPlayer(entry.Key, out var candidate)
                || !candidate.IsAlive
                || candidate.Id != playerId)
            {
                continue;
            }

            slot = entry.Key;
            player = candidate;
            return true;
        }

        slot = 0;
        player = null!;
        return false;
    }

    private static bool TryFindCarrier(
        SimulationWorld world,
        PlayerTeam carriedIntelTeam,
        PlayerEntity self,
        out PlayerEntity carrier)
    {
        carrier = null!;
        foreach (var candidate in CombatDecisionResolver.EnumeratePlayers(world))
        {
            if (candidate.IsAlive
                && candidate.Id != self.Id
                && candidate.Team != carriedIntelTeam
                && candidate.IsCarryingIntel)
            {
                carrier = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsDirectEnemySeekTrace(string directTrace)
    {
        return directTrace.Contains("ownedKothEnemy", StringComparison.OrdinalIgnoreCase)
            || directTrace.Contains("recoveryEnemy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnemyIntelAvailable(SimulationWorld world, PlayerTeam team)
    {
        var enemyIntel = GetEnemyIntelState(world, team);
        return enemyIntel.IsAtBase || enemyIntel.IsDropped;
    }

    private static BotBubbleSignal CreateSignal(
        byte sourceSlot,
        PlayerEntity self,
        PlayerTeam team,
        BotBubbleSignalKind kind,
        string key)
    {
        return new BotBubbleSignal(sourceSlot, team, self.X, self.Y, kind, key);
    }

    private static int ResolveClassPortraitFrame(PlayerEntity target)
    {
        return ChatBubbleFrameCatalog.GetClassPortraitFrame(target.ClassId, target.Team);
    }

    private static int ResolveOwnIntelFrame(PlayerTeam team)
    {
        return ChatBubbleFrameCatalog.GetIntelFrame(team);
    }

    private static int ResolveEnemyIntelFrame(PlayerTeam team)
    {
        return ChatBubbleFrameCatalog.GetIntelFrame(GetOpposingTeam(team));
    }

    private static float GetHealthFraction(PlayerEntity player)
    {
        return player.MaxHealth <= 0
            ? 0f
            : Math.Clamp(player.Health / (float)player.MaxHealth, 0f, 1f);
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    private static TeamIntelligenceState GetEnemyIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.RedIntel : world.BlueIntel;
    }

    private static TeamIntelligenceState GetOwnIntelState(SimulationWorld world, PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? world.BlueIntel : world.RedIntel;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private sealed class BotBrainChatBubbleState
    {
        public bool WasAlive { get; set; }

        public bool WasCarryingIntel { get; set; }

        public bool LowHealthCallIssued { get; set; }

        public int LastKnownHealth { get; set; }

        public int LastKills { get; set; }

        public int LastCaps { get; set; }

        public int? LastSeenCombatTargetId { get; set; }

        public int? LastDirectSeekTargetId { get; set; }

        public int? LastMedicPocketTargetId { get; set; }

        public int LastFrame { get; set; } = -1;

        public long LastGlobalReactionFrame { get; set; } = long.MinValue / 4;

        public long LastObjectiveReactionFrame { get; set; } = long.MinValue / 4;

        public long LastCombatReactionFrame { get; set; } = long.MinValue / 4;

        public long LastRecoveryReactionFrame { get; set; } = long.MinValue / 4;

        public long LastAmbientReactionFrame { get; set; } = long.MinValue / 4;

        public long LastSignalFrame { get; set; } = long.MinValue / 4;

        public long LastResponseFrame { get; set; } = long.MinValue / 4;

        public long LastSignalAttemptFrame { get; set; } = long.MinValue / 4;

        public string LastSignalKey { get; set; } = string.Empty;

        public BotBubbleSignalKind LastSignalKind { get; set; }

        public string LastSignalAttemptKey { get; set; } = string.Empty;

        public BotBubbleSignalKind LastSignalAttemptKind { get; set; }

        public List<PendingBubble> PendingBubbles { get; } = [];
    }

    private readonly record struct BotBrainChatBubbleReaction(
        int Frame,
        BotBrainChatBubbleCategory Category,
        int Priority);

    private enum BotBrainChatBubbleCategory
    {
        Objective,
        Combat,
        Recovery,
        Ambient,
    }

    private readonly record struct PendingBubble(
        long TriggerFrame,
        BotBrainChatBubbleReaction Reaction,
        BotBubbleSignal? Signal);

    private readonly record struct BotBubbleSignal(
        byte SourceSlot,
        PlayerTeam Team,
        float X,
        float Y,
        BotBubbleSignalKind Kind,
        string Key);

    private enum BotBubbleSignalKind
    {
        None,
        GoEnemyIntel,
        DroppedEnemyIntel,
        OwnDroppedIntel,
        EnemyCarrier,
        EscortCarrier,
        DirectEnemySeek,
        MedicPocket,
    }
}
