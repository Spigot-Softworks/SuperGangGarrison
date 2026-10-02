using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int VipDeathTimePenaltySeconds = 15;
    private const int VipWarmupSeconds = 10;

    private bool IsPracticeVipRulesActive => VipState.PracticeRulesEnabled && MatchRules.Mode == GameModeKind.ControlPoint;

    public bool IsVipModeActive => MatchRules.Mode == GameModeKind.Vip || IsPracticeVipRulesActive;

    public bool PracticeVipRulesEnabled => VipState.PracticeRulesEnabled;

    public bool VipWarmupActive => IsVipModeActive && VipState.WarmupTicksRemaining > 0;

    public int VipWarmupTicksRemaining => VipState.WarmupTicksRemaining;

    public int VipAssignmentVersion => VipState.AssignmentVersion;

    public int VipRoundStartVersion => VipState.RoundStartVersion;

    public IReadOnlyDictionary<PlayerTeam, byte> VipSlotsByTeam => VipState.SlotsByTeam;

    public bool VipRequiresDualVip => RequiresDualVip();

    public void ConfigurePracticeVipRules(bool enabled)
    {
        var normalizedEnabled = enabled && MatchRules.Mode == GameModeKind.ControlPoint;
        if (VipState.PracticeRulesEnabled == normalizedEnabled)
        {
            return;
        }

        VipState.PracticeRulesEnabled = normalizedEnabled;
        if (IsVipModeActive)
        {
            ResetVipStateForNewRound();
        }
        else
        {
            ClearVipState();
        }
    }

    public bool IsVipSlot(byte slot)
    {
        return IsVipModeActive && VipState.SlotsByTeam.ContainsValue(slot);
    }

    public bool TryGetVipSlot(PlayerTeam team, out byte slot)
    {
        return VipState.SlotsByTeam.TryGetValue(team, out slot);
    }

    public bool CanNetworkPlayerChangeTeamInCurrentMode(byte slot)
    {
        if (!CanNetworkPlayerChangeTeamByMapBehavior(slot))
        {
            return false;
        }

        if (IsPracticeVipRulesActive && ControlPointSetupActive)
        {
            return true;
        }

        return !IsVipModeActive || MatchState.IsEnded || !IsVipSlot(slot);
    }

    public bool CanNetworkPlayerSelectClassInCurrentMode(byte slot, CharacterClassDefinition definition)
    {
        if (!CanNetworkPlayerSelectClassByMapBehavior(slot, definition))
        {
            return false;
        }

        if (IsPracticeVipRulesActive && ControlPointSetupActive)
        {
            return true;
        }

        if (!IsVipModeActive || MatchState.IsEnded)
        {
            return true;
        }

        var isCivilian = string.Equals(
            definition.GameplayClassId,
            CharacterClassCatalog.CivilianGameplayClassId,
            StringComparison.Ordinal);
        return IsVipSlot(slot)
            ? isCivilian
            : !isCivilian;
    }

    public bool TrySetPreferredVipSlot(PlayerTeam team, byte slot)
    {
        if (!IsVipModeActive
            || !IsVipTeamRequired(team)
            || !TryGetNetworkPlayer(slot, out var player)
            || IsNetworkPlayerAwaitingJoin(slot)
            || !IsPlayableNetworkPlayerSlot(slot)
            || !player.IsAlive)
        {
            return false;
        }

        VipState.PreferredSlotsByTeam[team] = slot;
        if (VipState.SlotsByTeam.TryGetValue(team, out var currentSlot) && currentSlot != slot)
        {
            VipState.SlotsByTeam.Remove(team);
        }

        return true;
    }


    private void ResetVipStateForNewRound()
    {
        VipState.SlotsByTeam.Clear();
        VipState.PreferredSlotsByTeam.Clear();
        VipState.WarmupTicksRemaining = ShouldStartVipWarmup()
            ? Math.Max(1, VipWarmupSeconds * Config.TicksPerSecond)
            : 0;
        VipState.AssignmentVersion += 1;
    }

    private void ClearVipState()
    {
        if (VipState.SlotsByTeam.Count == 0 && VipState.PreferredSlotsByTeam.Count == 0 && VipState.WarmupTicksRemaining == 0)
        {
            return;
        }

        VipState.SlotsByTeam.Clear();
        VipState.PreferredSlotsByTeam.Clear();
        VipState.WarmupTicksRemaining = 0;
        VipState.AssignmentVersion += 1;
    }

    private void AdvanceVipState()
    {
        if (!IsVipModeActive)
        {
            ClearVipState();
            return;
        }

        if (MatchState.IsEnded)
        {
            return;
        }

        if (IsPracticeVipRulesActive && ControlPointSetupActive)
        {
            return;
        }

        EnsureVipAssignments();
        if (MatchState.IsEnded)
        {
            return;
        }

        ForceVipRulesOnCurrentPlayers();
        AdvanceVipWarmup();
        ResolveVipDeathWinCondition();
    }

    private bool IsVipPlayer(PlayerEntity player)
    {
        return TryGetNetworkPlayerSlot(player, out var slot) && IsVipSlot(slot);
    }

    private void ApplyVipDeathTimerPenalty(PlayerEntity victim, PlayerEntity? killer)
    {
        if (!IsVipModeActive
            || VipWarmupActive
            || MatchState.IsEnded
            || !IsVipPlayer(victim)
            || killer is null
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team)
        {
            return;
        }

        var penaltyTicks = VipDeathTimePenaltySeconds * Config.TicksPerSecond;
        MatchState = MatchState with
        {
            TimeRemainingTicks = Math.Max(0, MatchState.TimeRemainingTicks - penaltyTicks),
        };
    }

    private bool CanPlayerCaptureInVipMode(PlayerEntity player)
    {
        return !IsVipModeActive
            || (!VipWarmupActive && player.IsCivilian && IsVipPlayer(player));
    }

    private bool CanPlayerAffectControlPointInVipMode()
    {
        return !IsVipModeActive || !VipWarmupActive;
    }

    private bool CanPlayerPauseVipCaptureDecay(PlayerEntity player)
    {
        return IsVipModeActive
            && !VipWarmupActive
            && !IsVipPlayer(player)
            && IsVipDead(player.Team);
    }

    private bool IsVipDead(PlayerTeam team)
    {
        return VipState.SlotsByTeam.TryGetValue(team, out var slot)
            && TryGetNetworkPlayer(slot, out var vip)
            && !vip.IsAlive;
    }

    private bool RequiresDualVip()
    {
        return IsVipModeActive && !Objectives.ControlPoints.SetupMode;
    }

    private bool ShouldStartVipWarmup()
    {
        return MatchRules.Mode == GameModeKind.Vip;
    }

    private bool IsVipTeamRequired(PlayerTeam team)
    {
        if (!IsVipModeActive)
        {
            return false;
        }

        return RequiresDualVip()
            ? team is PlayerTeam.Red or PlayerTeam.Blue
            : team == PlayerTeam.Red;
    }

    private void EnsureVipAssignments()
    {
        if (RequiresDualVip())
        {
            EnsureVipAssignment(PlayerTeam.Red);
            EnsureVipAssignment(PlayerTeam.Blue);
            return;
        }

        VipState.SlotsByTeam.Remove(PlayerTeam.Blue);
        EnsureVipAssignment(PlayerTeam.Red);
    }

    private void EnsureVipAssignment(PlayerTeam team)
    {
        if (!IsVipTeamRequired(team))
        {
            VipState.SlotsByTeam.Remove(team);
            return;
        }

        if (VipState.SlotsByTeam.TryGetValue(team, out var currentSlot)
            && IsValidVipSlot(currentSlot, team))
        {
            return;
        }

        VipState.SlotsByTeam.Remove(team);
        if (!TrySelectVipSlot(team, out var selectedSlot))
        {
            return;
        }

        VipState.SlotsByTeam[team] = selectedSlot;
        VipState.AssignmentVersion += 1;
        ForceVipSlot(selectedSlot, team);
    }

    private bool TrySelectVipSlot(PlayerTeam team, out byte slot)
    {
        if (IsPracticeVipRulesActive && TrySelectPracticeVipSlot(team, out slot))
        {
            return true;
        }

        if (VipState.PreferredSlotsByTeam.TryGetValue(team, out var preferredSlot)
            && IsVipCandidateSlot(preferredSlot, team, allowTeamMove: true))
        {
            slot = preferredSlot;
            return true;
        }

        var candidates = new List<byte>();
        foreach (var entry in EnumerateActiveNetworkPlayers())
        {
            if (IsVipCandidateSlot(entry.Slot, team, allowTeamMove: RequiresDualVip() ? entry.Player.Team == team : true))
            {
                candidates.Add(entry.Slot);
            }
        }

        if (candidates.Count == 0 && !RequiresDualVip())
        {
            foreach (var entry in EnumerateActiveNetworkPlayers())
            {
                if (IsVipCandidateSlot(entry.Slot, team, allowTeamMove: true))
                {
                    candidates.Add(entry.Slot);
                }
            }
        }

        if (candidates.Count == 0)
        {
            slot = 0;
            return false;
        }

        slot = candidates[Randoms.Gameplay.Next(candidates.Count)];
        return true;
    }

    private bool IsVipCandidateSlot(byte slot, PlayerTeam team, bool allowTeamMove)
    {
        if (!TryGetNetworkPlayer(slot, out var player)
            || IsNetworkPlayerAwaitingJoin(slot)
            || !player.IsAlive
            || VipState.SlotsByTeam.Any(entry => entry.Value == slot && entry.Key != team))
        {
            return false;
        }

        return allowTeamMove || player.Team == team;
    }

    private bool TrySelectPracticeVipSlot(PlayerTeam team, out byte slot)
    {
        if (VipState.PreferredSlotsByTeam.TryGetValue(team, out var preferredSlot)
            && IsVipCandidateSlot(preferredSlot, team, allowTeamMove: false)
            && TryGetNetworkPlayer(preferredSlot, out var preferredPlayer)
            && IsCivilianClass(preferredPlayer.ClassDefinition))
        {
            slot = preferredSlot;
            return true;
        }

        if (IsVipCandidateSlot(LocalPlayerSlot, team, allowTeamMove: false)
            && IsCivilianClass(LocalPlayer.ClassDefinition))
        {
            slot = LocalPlayerSlot;
            return true;
        }

        byte? civilianCandidate = null;
        foreach (var entry in EnumerateActiveNetworkPlayers())
        {
            if (entry.Slot == LocalPlayerSlot)
            {
                continue;
            }

            if (IsVipCandidateSlot(entry.Slot, team, allowTeamMove: false)
                && IsCivilianClass(entry.Player.ClassDefinition))
            {
                civilianCandidate = !civilianCandidate.HasValue || entry.Slot < civilianCandidate.Value
                    ? entry.Slot
                    : civilianCandidate;
            }
        }

        if (civilianCandidate.HasValue)
        {
            slot = civilianCandidate.Value;
            return true;
        }

        var botCandidates = new List<byte>();
        foreach (var entry in EnumerateActiveNetworkPlayers())
        {
            if (entry.Slot == LocalPlayerSlot)
            {
                continue;
            }

            if (IsVipCandidateSlot(entry.Slot, team, allowTeamMove: false))
            {
                botCandidates.Add(entry.Slot);
            }
        }

        if (botCandidates.Count == 0)
        {
            slot = 0;
            return false;
        }

        slot = botCandidates[Randoms.Gameplay.Next(botCandidates.Count)];
        return true;
    }

    private static bool IsCivilianClass(CharacterClassDefinition definition)
    {
        return string.Equals(
            definition.GameplayClassId,
            CharacterClassCatalog.CivilianGameplayClassId,
            StringComparison.Ordinal);
    }

    private bool IsValidVipSlot(byte slot, PlayerTeam team)
    {
        return TryGetNetworkPlayer(slot, out var player)
            && !IsNetworkPlayerAwaitingJoin(slot)
            && player.Team == team;
    }

    private void ForceVipRulesOnCurrentPlayers()
    {
        foreach (var entry in VipState.SlotsByTeam.ToArray())
        {
            ForceVipSlot(entry.Value, entry.Key);
        }

        foreach (var entry in EnumerateActiveNetworkPlayers())
        {
            if (IsVipSlot(entry.Slot) || !entry.Player.IsCivilian)
            {
                continue;
            }

            TryApplyNetworkPlayerClassChange(entry.Slot, CharacterClassCatalog.Scout, enforceClassLimit: false);
        }
    }

    private void ForceVipSlot(byte slot, PlayerTeam team)
    {
        if (!TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        var civilianDefinition = CharacterClassCatalog.Civilian;
        TrySetNetworkPlayerClassDefinition(slot, civilianDefinition);
        if (player.Team != team)
        {
            TrySetNetworkPlayerTeam(slot, team, respawnLivePlayerImmediately: true);
        }

        if (!player.IsCivilian)
        {
            player.SetClassDefinition(civilianDefinition);
            SyncExperimentalGameplayLoadout(slot, player);
        }
    }

    private void AdvanceVipWarmup()
    {
        if (VipState.WarmupTicksRemaining <= 0)
        {
            return;
        }

        VipState.WarmupTicksRemaining -= 1;
        if (VipState.WarmupTicksRemaining <= 0)
        {
            VipState.WarmupTicksRemaining = 0;
            VipState.RoundStartVersion += 1;
        }
    }

    private void ResolveVipDeathWinCondition()
    {
        if (VipWarmupActive)
        {
            return;
        }

        foreach (var entry in VipState.SlotsByTeam.ToArray())
        {
            if (!TryGetNetworkPlayer(entry.Value, out _) || IsNetworkPlayerAwaitingJoin(entry.Value))
            {
                VipState.SlotsByTeam.Remove(entry.Key);
                VipState.AssignmentVersion += 1;
                return;
            }

            // VIP death is not a loss condition. VIP mode plays as a normal attack/defense
            // control-point match where only the VIP can capture; a dead VIP simply respawns
            // (and stays VIP), and the round resolves on the usual capture/time conditions.
        }
    }

    private bool ShouldDeferVipObjectiveResolution()
    {
        if (!IsVipModeActive)
        {
            return false;
        }

        if (IsPracticeVipRulesActive && ControlPointSetupActive)
        {
            return false;
        }

        if (VipWarmupActive)
        {
            return true;
        }

        return (IsVipTeamRequired(PlayerTeam.Red) && !HasValidVipAssignment(PlayerTeam.Red))
            || (IsVipTeamRequired(PlayerTeam.Blue) && !HasValidVipAssignment(PlayerTeam.Blue));
    }

    private bool HasValidVipAssignment(PlayerTeam team)
    {
        return VipState.SlotsByTeam.TryGetValue(team, out var slot)
            && IsValidVipSlot(slot, team);
    }
}
