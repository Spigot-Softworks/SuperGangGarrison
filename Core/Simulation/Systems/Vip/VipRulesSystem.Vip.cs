using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class VipRulesSystem
{
    private const int VipDeathTimePenaltySeconds = 15;
    private const int VipWarmupSeconds = 10;

    private bool IsPracticeVipRulesActive => _host.VipState.PracticeRulesEnabled && _host.MatchRules.Mode == GameModeKind.ControlPoint;

    internal bool IsVipModeActive => _host.MatchRules.Mode == GameModeKind.Vip || IsPracticeVipRulesActive;

    internal bool PracticeVipRulesEnabled => _host.VipState.PracticeRulesEnabled;

    internal bool VipWarmupActive => IsVipModeActive && _host.VipState.WarmupTicksRemaining > 0;

    internal int VipWarmupTicksRemaining => _host.VipState.WarmupTicksRemaining;

    internal int VipAssignmentVersion => _host.VipState.AssignmentVersion;

    internal int VipRoundStartVersion => _host.VipState.RoundStartVersion;

    internal IReadOnlyDictionary<PlayerTeam, byte> VipSlotsByTeam => _host.VipState.SlotsByTeam;

    internal bool VipRequiresDualVip => RequiresDualVip();

    internal void ConfigurePracticeVipRules(bool enabled)
    {
        var normalizedEnabled = enabled && _host.MatchRules.Mode == GameModeKind.ControlPoint;
        if (_host.VipState.PracticeRulesEnabled == normalizedEnabled)
        {
            return;
        }

        _host.VipState.PracticeRulesEnabled = normalizedEnabled;
        if (IsVipModeActive)
        {
            ResetVipStateForNewRound();
        }
        else
        {
            ClearVipState();
        }
    }

    internal bool IsVipSlot(byte slot)
    {
        return IsVipModeActive && _host.VipState.SlotsByTeam.ContainsValue(slot);
    }

    internal bool TryGetVipSlot(PlayerTeam team, out byte slot)
    {
        return _host.VipState.SlotsByTeam.TryGetValue(team, out slot);
    }

    internal bool CanNetworkPlayerChangeTeamInCurrentMode(byte slot)
    {
        if (!_host.CanNetworkPlayerChangeTeamByMapBehavior(slot))
        {
            return false;
        }

        if (IsPracticeVipRulesActive && _host.ControlPointSetupActive)
        {
            return true;
        }

        return !IsVipModeActive || _host.MatchState.IsEnded || !IsVipSlot(slot);
    }

    internal bool CanNetworkPlayerSelectClassInCurrentMode(byte slot, CharacterClassDefinition definition)
    {
        if (!_host.CanNetworkPlayerSelectClassByMapBehavior(slot, definition))
        {
            return false;
        }

        if (IsPracticeVipRulesActive && _host.ControlPointSetupActive)
        {
            return true;
        }

        if (!IsVipModeActive || _host.MatchState.IsEnded)
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

    internal bool TrySetPreferredVipSlot(PlayerTeam team, byte slot)
    {
        if (!IsVipModeActive
            || !IsVipTeamRequired(team)
            || !_host.TryGetNetworkPlayer(slot, out var player)
            || _host.IsNetworkPlayerAwaitingJoin(slot)
            || !NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot)
            || !player.IsAlive)
        {
            return false;
        }

        _host.VipState.PreferredSlotsByTeam[team] = slot;
        if (_host.VipState.SlotsByTeam.TryGetValue(team, out var currentSlot) && currentSlot != slot)
        {
            _host.VipState.SlotsByTeam.Remove(team);
        }

        return true;
    }


    internal void ResetVipStateForNewRound()
    {
        _host.VipState.SlotsByTeam.Clear();
        _host.VipState.PreferredSlotsByTeam.Clear();
        _host.VipState.WarmupTicksRemaining = ShouldStartVipWarmup()
            ? Math.Max(1, VipWarmupSeconds * _host.Config.TicksPerSecond)
            : 0;
        _host.VipState.AssignmentVersion += 1;
    }

    internal void ClearVipState()
    {
        if (_host.VipState.SlotsByTeam.Count == 0 && _host.VipState.PreferredSlotsByTeam.Count == 0 && _host.VipState.WarmupTicksRemaining == 0)
        {
            return;
        }

        _host.VipState.SlotsByTeam.Clear();
        _host.VipState.PreferredSlotsByTeam.Clear();
        _host.VipState.WarmupTicksRemaining = 0;
        _host.VipState.AssignmentVersion += 1;
    }

    internal void AdvanceVipState()
    {
        if (!IsVipModeActive)
        {
            ClearVipState();
            return;
        }

        if (_host.MatchState.IsEnded)
        {
            return;
        }

        if (IsPracticeVipRulesActive && _host.ControlPointSetupActive)
        {
            return;
        }

        EnsureVipAssignments();
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        ForceVipRulesOnCurrentPlayers();
        AdvanceVipWarmup();
        ResolveVipDeathWinCondition();
    }

    private bool IsVipPlayer(PlayerEntity player)
    {
        return _host.TryGetNetworkPlayerSlot(player, out var slot) && IsVipSlot(slot);
    }

    internal void ApplyVipDeathTimerPenalty(PlayerEntity victim, PlayerEntity? killer)
    {
        if (!IsVipModeActive
            || VipWarmupActive
            || _host.MatchState.IsEnded
            || !IsVipPlayer(victim)
            || killer is null
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team)
        {
            return;
        }

        var penaltyTicks = VipDeathTimePenaltySeconds * _host.Config.TicksPerSecond;
        _host.MatchState = _host.MatchState with
        {
            TimeRemainingTicks = Math.Max(0, _host.MatchState.TimeRemainingTicks - penaltyTicks),
        };
    }

    internal bool CanPlayerCaptureInVipMode(PlayerEntity player)
    {
        return !IsVipModeActive
            || (!VipWarmupActive && player.IsCivilian && IsVipPlayer(player));
    }

    internal bool CanPlayerAffectControlPointInVipMode()
    {
        return !IsVipModeActive || !VipWarmupActive;
    }

    internal bool CanPlayerPauseVipCaptureDecay(PlayerEntity player)
    {
        return IsVipModeActive
            && !VipWarmupActive
            && !IsVipPlayer(player)
            && IsVipDead(player.Team);
    }

    private bool IsVipDead(PlayerTeam team)
    {
        return _host.VipState.SlotsByTeam.TryGetValue(team, out var slot)
            && _host.TryGetNetworkPlayer(slot, out var vip)
            && !vip.IsAlive;
    }

    private bool RequiresDualVip()
    {
        return IsVipModeActive && !_host.Objectives.ControlPoints.SetupMode;
    }

    private bool ShouldStartVipWarmup()
    {
        return _host.MatchRules.Mode == GameModeKind.Vip;
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

        _host.VipState.SlotsByTeam.Remove(PlayerTeam.Blue);
        EnsureVipAssignment(PlayerTeam.Red);
    }

    private void EnsureVipAssignment(PlayerTeam team)
    {
        if (!IsVipTeamRequired(team))
        {
            _host.VipState.SlotsByTeam.Remove(team);
            return;
        }

        if (_host.VipState.SlotsByTeam.TryGetValue(team, out var currentSlot)
            && IsValidVipSlot(currentSlot, team))
        {
            return;
        }

        _host.VipState.SlotsByTeam.Remove(team);
        if (!TrySelectVipSlot(team, out var selectedSlot))
        {
            return;
        }

        _host.VipState.SlotsByTeam[team] = selectedSlot;
        _host.VipState.AssignmentVersion += 1;
        ForceVipSlot(selectedSlot, team);
    }

    private bool TrySelectVipSlot(PlayerTeam team, out byte slot)
    {
        if (IsPracticeVipRulesActive && TrySelectPracticeVipSlot(team, out slot))
        {
            return true;
        }

        if (_host.VipState.PreferredSlotsByTeam.TryGetValue(team, out var preferredSlot)
            && IsVipCandidateSlot(preferredSlot, team, allowTeamMove: true))
        {
            slot = preferredSlot;
            return true;
        }

        var candidates = new List<byte>();
        foreach (var entry in _host.EnumerateActiveNetworkPlayers())
        {
            if (IsVipCandidateSlot(entry.Slot, team, allowTeamMove: RequiresDualVip() ? entry.Player.Team == team : true))
            {
                candidates.Add(entry.Slot);
            }
        }

        if (candidates.Count == 0 && !RequiresDualVip())
        {
            foreach (var entry in _host.EnumerateActiveNetworkPlayers())
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

        slot = candidates[_host.Randoms.Gameplay.Next(candidates.Count)];
        return true;
    }

    private bool IsVipCandidateSlot(byte slot, PlayerTeam team, bool allowTeamMove)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player)
            || _host.IsNetworkPlayerAwaitingJoin(slot)
            || !player.IsAlive
            || _host.VipState.SlotsByTeam.Any(entry => entry.Value == slot && entry.Key != team))
        {
            return false;
        }

        return allowTeamMove || player.Team == team;
    }

    private bool TrySelectPracticeVipSlot(PlayerTeam team, out byte slot)
    {
        if (_host.VipState.PreferredSlotsByTeam.TryGetValue(team, out var preferredSlot)
            && IsVipCandidateSlot(preferredSlot, team, allowTeamMove: false)
            && _host.TryGetNetworkPlayer(preferredSlot, out var preferredPlayer)
            && IsCivilianClass(preferredPlayer.ClassDefinition))
        {
            slot = preferredSlot;
            return true;
        }

        if (IsVipCandidateSlot(SimulationConstants.LocalPlayerSlot, team, allowTeamMove: false)
            && IsCivilianClass(_host.LocalPlayer.ClassDefinition))
        {
            slot = SimulationConstants.LocalPlayerSlot;
            return true;
        }

        byte? civilianCandidate = null;
        foreach (var entry in _host.EnumerateActiveNetworkPlayers())
        {
            if (entry.Slot == SimulationConstants.LocalPlayerSlot)
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
        foreach (var entry in _host.EnumerateActiveNetworkPlayers())
        {
            if (entry.Slot == SimulationConstants.LocalPlayerSlot)
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

        slot = botCandidates[_host.Randoms.Gameplay.Next(botCandidates.Count)];
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
        return _host.TryGetNetworkPlayer(slot, out var player)
            && !_host.IsNetworkPlayerAwaitingJoin(slot)
            && player.Team == team;
    }

    private void ForceVipRulesOnCurrentPlayers()
    {
        foreach (var entry in _host.VipState.SlotsByTeam.ToArray())
        {
            ForceVipSlot(entry.Value, entry.Key);
        }

        foreach (var entry in _host.EnumerateActiveNetworkPlayers())
        {
            if (IsVipSlot(entry.Slot) || !entry.Player.IsCivilian)
            {
                continue;
            }

            _host.TryApplyNetworkPlayerClassChange(entry.Slot, CharacterClassCatalog.Scout, enforceClassLimit: false);
        }
    }

    private void ForceVipSlot(byte slot, PlayerTeam team)
    {
        if (!_host.TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        var civilianDefinition = CharacterClassCatalog.Civilian;
        _host.TrySetNetworkPlayerClassDefinition(slot, civilianDefinition);
        if (player.Team != team)
        {
            _host.TrySetNetworkPlayerTeam(slot, team, respawnLivePlayerImmediately: true);
        }

        if (!player.IsCivilian)
        {
            player.SetClassDefinition(civilianDefinition);
            _host.SyncExperimentalGameplayLoadout(slot, player);
        }
    }

    private void AdvanceVipWarmup()
    {
        if (_host.VipState.WarmupTicksRemaining <= 0)
        {
            return;
        }

        _host.VipState.WarmupTicksRemaining -= 1;
        if (_host.VipState.WarmupTicksRemaining <= 0)
        {
            _host.VipState.WarmupTicksRemaining = 0;
            _host.VipState.RoundStartVersion += 1;
        }
    }

    private void ResolveVipDeathWinCondition()
    {
        if (VipWarmupActive)
        {
            return;
        }

        foreach (var entry in _host.VipState.SlotsByTeam.ToArray())
        {
            if (!_host.TryGetNetworkPlayer(entry.Value, out _) || _host.IsNetworkPlayerAwaitingJoin(entry.Value))
            {
                _host.VipState.SlotsByTeam.Remove(entry.Key);
                _host.VipState.AssignmentVersion += 1;
                return;
            }

            // VIP death is not a loss condition. VIP mode plays as a normal attack/defense
            // control-point match where only the VIP can capture; a dead VIP simply respawns
            // (and stays VIP), and the round resolves on the usual capture/time conditions.
        }
    }

    internal bool ShouldDeferVipObjectiveResolution()
    {
        if (!IsVipModeActive)
        {
            return false;
        }

        if (IsPracticeVipRulesActive && _host.ControlPointSetupActive)
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
        return _host.VipState.SlotsByTeam.TryGetValue(team, out var slot)
            && IsValidVipSlot(slot, team);
    }
}
