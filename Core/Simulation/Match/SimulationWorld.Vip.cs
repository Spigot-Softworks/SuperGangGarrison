using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int VipDeathTimePenaltySeconds = 15;
    private const int VipWarmupSeconds = 10;

    private bool IsPracticeVipRulesActive => _practiceVipRulesEnabled && MatchRules.Mode == GameModeKind.ControlPoint;

    public bool IsVipModeActive => MatchRules.Mode == GameModeKind.Vip || IsPracticeVipRulesActive;

    public bool PracticeVipRulesEnabled => _practiceVipRulesEnabled;

    public bool VipWarmupActive => IsVipModeActive && _vipWarmupTicksRemaining > 0;

    public int VipWarmupTicksRemaining => _vipWarmupTicksRemaining;

    public int VipAssignmentVersion => _vipAssignmentVersion;

    public int VipRoundStartVersion => _vipRoundStartVersion;

    public IReadOnlyDictionary<PlayerTeam, byte> VipSlotsByTeam => _vipSlotsByTeam;

    public bool VipRequiresDualVip => RequiresDualVip();

    public void ConfigurePracticeVipRules(bool enabled)
    {
        var normalizedEnabled = enabled && MatchRules.Mode == GameModeKind.ControlPoint;
        if (_practiceVipRulesEnabled == normalizedEnabled)
        {
            return;
        }

        _practiceVipRulesEnabled = normalizedEnabled;
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
        return IsVipModeActive && _vipSlotsByTeam.ContainsValue(slot);
    }

    public bool TryGetVipSlot(PlayerTeam team, out byte slot)
    {
        return _vipSlotsByTeam.TryGetValue(team, out slot);
    }

    public bool CanNetworkPlayerChangeTeamInCurrentMode(byte slot)
    {
        if (IsPracticeVipRulesActive && ControlPointSetupActive)
        {
            return true;
        }

        return !IsVipModeActive || MatchState.IsEnded || !IsVipSlot(slot);
    }

    public bool CanNetworkPlayerSelectClassInCurrentMode(byte slot, CharacterClassDefinition definition)
    {
        if (IsPracticeVipRulesActive && ControlPointSetupActive)
        {
            return true;
        }

        if (!IsVipModeActive || MatchState.IsEnded)
        {
            return true;
        }

        var isCivilian = definition.Id == PlayerClass.Quote
            || string.Equals(definition.GameplayClassId, CharacterClassCatalog.Civilian.GameplayClassId, StringComparison.Ordinal);
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

        _preferredVipSlotsByTeam[team] = slot;
        if (_vipSlotsByTeam.TryGetValue(team, out var currentSlot) && currentSlot != slot)
        {
            _vipSlotsByTeam.Remove(team);
        }

        return true;
    }

    public void ClearPreferredVipSlots()
    {
        _preferredVipSlotsByTeam.Clear();
    }

    private void ResetVipStateForNewRound()
    {
        _vipSlotsByTeam.Clear();
        _preferredVipSlotsByTeam.Clear();
        _vipWarmupTicksRemaining = ShouldStartVipWarmup()
            ? Math.Max(1, VipWarmupSeconds * Config.TicksPerSecond)
            : 0;
        _vipAssignmentVersion += 1;
    }

    private void ClearVipState()
    {
        if (_vipSlotsByTeam.Count == 0 && _preferredVipSlotsByTeam.Count == 0 && _vipWarmupTicksRemaining == 0)
        {
            return;
        }

        _vipSlotsByTeam.Clear();
        _preferredVipSlotsByTeam.Clear();
        _vipWarmupTicksRemaining = 0;
        _vipAssignmentVersion += 1;
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
            || (!VipWarmupActive && player.ClassId == PlayerClass.Quote && IsVipPlayer(player));
    }

    private bool CanPlayerAffectControlPointInVipMode(PlayerEntity player)
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
        return _vipSlotsByTeam.TryGetValue(team, out var slot)
            && TryGetNetworkPlayer(slot, out var vip)
            && !vip.IsAlive;
    }

    private bool RequiresDualVip()
    {
        return IsVipModeActive && !_controlPointSetupMode;
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

        _vipSlotsByTeam.Remove(PlayerTeam.Blue);
        EnsureVipAssignment(PlayerTeam.Red);
    }

    private void EnsureVipAssignment(PlayerTeam team)
    {
        if (!IsVipTeamRequired(team))
        {
            _vipSlotsByTeam.Remove(team);
            return;
        }

        if (_vipSlotsByTeam.TryGetValue(team, out var currentSlot)
            && IsValidVipSlot(currentSlot, team))
        {
            return;
        }

        _vipSlotsByTeam.Remove(team);
        if (!TrySelectVipSlot(team, out var selectedSlot))
        {
            return;
        }

        _vipSlotsByTeam[team] = selectedSlot;
        _vipAssignmentVersion += 1;
        ForceVipSlot(selectedSlot, team);
    }

    private bool TrySelectVipSlot(PlayerTeam team, out byte slot)
    {
        if (IsPracticeVipRulesActive && TrySelectPracticeVipSlot(team, out slot))
        {
            return true;
        }

        if (_preferredVipSlotsByTeam.TryGetValue(team, out var preferredSlot)
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

        slot = candidates[_random.Next(candidates.Count)];
        return true;
    }

    private bool IsVipCandidateSlot(byte slot, PlayerTeam team, bool allowTeamMove)
    {
        if (!TryGetNetworkPlayer(slot, out var player)
            || IsNetworkPlayerAwaitingJoin(slot)
            || !player.IsAlive
            || _vipSlotsByTeam.Any(entry => entry.Value == slot && entry.Key != team))
        {
            return false;
        }

        return allowTeamMove || player.Team == team;
    }

    private bool TrySelectPracticeVipSlot(PlayerTeam team, out byte slot)
    {
        if (_preferredVipSlotsByTeam.TryGetValue(team, out var preferredSlot)
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

        slot = botCandidates[_random.Next(botCandidates.Count)];
        return true;
    }

    private static bool IsCivilianClass(CharacterClassDefinition definition)
    {
        return definition.Id == PlayerClass.Quote
            || string.Equals(definition.GameplayClassId, CharacterClassCatalog.Civilian.GameplayClassId, StringComparison.Ordinal);
    }

    private bool IsValidVipSlot(byte slot, PlayerTeam team)
    {
        return TryGetNetworkPlayer(slot, out var player)
            && !IsNetworkPlayerAwaitingJoin(slot)
            && player.Team == team;
    }

    private void ForceVipRulesOnCurrentPlayers()
    {
        foreach (var entry in _vipSlotsByTeam.ToArray())
        {
            ForceVipSlot(entry.Value, entry.Key);
        }

        foreach (var entry in EnumerateActiveNetworkPlayers())
        {
            if (IsVipSlot(entry.Slot) || entry.Player.ClassId != PlayerClass.Quote)
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

        if (player.ClassId != PlayerClass.Quote)
        {
            player.SetClassDefinition(civilianDefinition);
            SyncExperimentalGameplayLoadout(slot, player);
        }
    }

    private void AdvanceVipWarmup()
    {
        if (_vipWarmupTicksRemaining <= 0)
        {
            return;
        }

        _vipWarmupTicksRemaining -= 1;
        if (_vipWarmupTicksRemaining <= 0)
        {
            _vipWarmupTicksRemaining = 0;
            _vipRoundStartVersion += 1;
        }
    }

    private void ResolveVipDeathWinCondition()
    {
        if (VipWarmupActive)
        {
            return;
        }

        foreach (var entry in _vipSlotsByTeam.ToArray())
        {
            if (!TryGetNetworkPlayer(entry.Value, out _) || IsNetworkPlayerAwaitingJoin(entry.Value))
            {
                _vipSlotsByTeam.Remove(entry.Key);
                _vipAssignmentVersion += 1;
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
        return _vipSlotsByTeam.TryGetValue(team, out var slot)
            && IsValidVipSlot(slot, team);
    }
}
