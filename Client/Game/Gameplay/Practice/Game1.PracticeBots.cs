#nullable enable

using OpenGarrison.BotAI;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string PracticeBotNamesRelativePath = "OpenGarrison.Client/practice-bot-names.txt";
    private static readonly PlayerClass[] PracticeBotClassCycle =
    [
        PlayerClass.Scout,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Medic,
    ];

    private readonly Dictionary<byte, PracticeBotSlotState> _practiceBotSlots = new();
    private readonly Dictionary<byte, string> _practiceBotDisplayNamesBySlot = new();
    private readonly HashSet<string> _practiceUsedBotDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _practiceAvailableBotDisplayNames = new();
    private readonly BotController _practiceBotController = new();

    private sealed class PracticeBotSlotState
    {
        public PracticeBotSlotState(byte slot, PlayerTeam team, PlayerClass classId, string displayName)
        {
            Slot = slot;
            Team = team;
            ClassId = classId;
            DisplayName = displayName;
        }

        public byte Slot { get; }

        public PlayerTeam Team { get; }

        public PlayerClass ClassId { get; }

        public string DisplayName { get; }
    }

    private void ResetPracticeBotManagerState(bool releaseWorldSlots)
    {
        if (releaseWorldSlots)
        {
            var slotsToRelease = new List<byte>(_practiceBotSlots.Keys);
            for (var index = 0; index < slotsToRelease.Count; index += 1)
            {
                _world.TryReleaseNetworkPlayerSlot(slotsToRelease[index]);
            }
        }

        _practiceBotSlots.Clear();
        _practiceBotDisplayNamesBySlot.Clear();
        _practiceUsedBotDisplayNames.Clear();
        _practiceAvailableBotDisplayNames.Clear();
        _practiceBotController.Reset();
    }

    private void SyncPracticeBotRoster(PlayerTeam localTeam)
    {
        if (!IsPracticeSessionActive)
        {
            ResetPracticeBotManagerState(releaseWorldSlots: true);
            return;
        }

        var desiredSlots = BuildDesiredPracticeBotSlots(localTeam);
        var staleSlots = new List<byte>();
        foreach (var slot in _practiceBotSlots.Keys)
        {
            if (!desiredSlots.ContainsKey(slot))
            {
                staleSlots.Add(slot);
            }
        }

        for (var index = 0; index < staleSlots.Count; index += 1)
        {
            var slot = staleSlots[index];
            _world.TryReleaseNetworkPlayerSlot(slot);
            _practiceBotSlots.Remove(slot);
            _practiceBotDisplayNamesBySlot.Remove(slot);
        }

        foreach (var desired in desiredSlots.Values)
        {
            var isNewSlot = !_practiceBotSlots.TryGetValue(desired.Slot, out var existing);
            if (isNewSlot)
            {
                _world.TryPrepareNetworkPlayerJoin(desired.Slot);
            }

            _world.TrySetNetworkPlayerName(desired.Slot, desired.DisplayName);
            if (isNewSlot || existing!.Team != desired.Team)
            {
                _world.TrySetNetworkPlayerTeam(desired.Slot, desired.Team);
            }

            if (isNewSlot || existing!.ClassId != desired.ClassId)
            {
                _world.TryApplyNetworkPlayerClassSelection(desired.Slot, desired.ClassId);
            }

            _practiceBotSlots[desired.Slot] = desired;
        }
    }

    private Dictionary<byte, PracticeBotSlotState> BuildDesiredPracticeBotSlots(PlayerTeam localTeam)
    {
        var desiredSlots = new Dictionary<byte, PracticeBotSlotState>();
        var nextSlot = (byte)(SimulationWorld.LocalPlayerSlot + 1);
        nextSlot = AppendDesiredPracticeBotSlots(
            desiredSlots,
            nextSlot,
            localTeam,
            _practiceFriendlyBotCount,
            classOffset: 0);
        _ = AppendDesiredPracticeBotSlots(
            desiredSlots,
            nextSlot,
            GetOpposingTeam(localTeam),
            _practiceEnemyBotCount,
            classOffset: 3);
        return desiredSlots;
    }

    private byte AppendDesiredPracticeBotSlots(
        Dictionary<byte, PracticeBotSlotState> desiredSlots,
        byte startSlot,
        PlayerTeam team,
        int count,
        int classOffset)
    {
        var nextSlot = startSlot;
        var teamLabel = team == PlayerTeam.Blue ? "BLU" : "RED";
        for (var index = 0; index < count && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
        {
            var classId = PracticeBotClassCycle[(index + classOffset) % PracticeBotClassCycle.Length];
            desiredSlots[nextSlot] = new PracticeBotSlotState(
                nextSlot,
                team,
                classId,
                GetOrAssignPracticeBotDisplayName(nextSlot, teamLabel, index + 1));
            nextSlot += 1;
        }

        return nextSlot;
    }

    private void InitializePracticeBotNamePoolForMatch()
    {
        _practiceBotDisplayNamesBySlot.Clear();
        _practiceUsedBotDisplayNames.Clear();
        _practiceAvailableBotDisplayNames.Clear();

        var availableNames = LoadPracticeBotDisplayNames();
        ShufflePracticeBotNames(availableNames);
        for (var index = 0; index < availableNames.Count; index += 1)
        {
            _practiceAvailableBotDisplayNames.Enqueue(availableNames[index]);
        }
    }

    private List<string> LoadPracticeBotDisplayNames()
    {
        var path = ResolvePracticeBotNamesPath();
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return names;
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!seenNames.Add(trimmed))
            {
                continue;
            }

            names.Add(trimmed);
        }

        return names;
    }

    private static string? ResolvePracticeBotNamesPath()
    {
        var projectFilePath = ProjectSourceLocator.FindFile(PracticeBotNamesRelativePath);
        if (!string.IsNullOrWhiteSpace(projectFilePath))
        {
            return projectFilePath;
        }

        var configPath = RuntimePaths.GetConfigPath("practice-bot-names.txt");
        return File.Exists(configPath) ? configPath : null;
    }

    private void ShufflePracticeBotNames(List<string> names)
    {
        for (var index = names.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = _visualRandom.Next(index + 1);
            (names[index], names[swapIndex]) = (names[swapIndex], names[index]);
        }
    }

    private string GetOrAssignPracticeBotDisplayName(byte slot, string teamLabel, int teamBotNumber)
    {
        if (_practiceBotDisplayNamesBySlot.TryGetValue(slot, out var existing))
        {
            return existing;
        }

        while (_practiceAvailableBotDisplayNames.Count > 0)
        {
            var candidate = _practiceAvailableBotDisplayNames.Dequeue();
            if (!_practiceUsedBotDisplayNames.Add(candidate))
            {
                continue;
            }

            _practiceBotDisplayNamesBySlot[slot] = candidate;
            return candidate;
        }

        var fallbackNumber = Math.Max(1, teamBotNumber);
        while (true)
        {
            var fallback = $"{teamLabel} Bot {fallbackNumber}";
            fallbackNumber += 1;
            if (!_practiceUsedBotDisplayNames.Add(fallback))
            {
                continue;
            }

            _practiceBotDisplayNamesBySlot[slot] = fallback;
            return fallback;
        }
    }

    private void UpdatePracticeBots()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        _practiceBotController.CollectDiagnostics = _botDiagnosticsEnabled;
        if (_practiceBotSlots.Count == 0)
        {
            if (_botDiagnosticsEnabled)
            {
                RecordPracticeBotDiagnosticsUpdate(0d, BotControllerDiagnosticsSnapshot.Empty);
            }

            return;
        }

        var diagnosticsStartTimestamp = _botDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        var controlledSlots = BuildControlledPracticeBotSlots();
        var inputsBySlot = _practiceBotController.BuildInputs(_world, controlledSlots, _practiceNavigationAssets.Assets);
        for (var slotValue = SimulationWorld.LocalPlayerSlot + 1; slotValue <= SimulationWorld.MaxPlayableNetworkPlayers; slotValue += 1)
        {
            var slot = (byte)slotValue;
            if (!controlledSlots.ContainsKey(slot))
            {
                continue;
            }

            _world.TrySetNetworkPlayerInput(slot, inputsBySlot.GetValueOrDefault(slot));
        }

        if (_botDiagnosticsEnabled)
        {
            RecordPracticeBotDiagnosticsUpdate(
                GetDiagnosticsElapsedMilliseconds(diagnosticsStartTimestamp),
                _practiceBotController.LastDiagnostics);
        }
    }

    private Dictionary<byte, ControlledBotSlot> BuildControlledPracticeBotSlots()
    {
        var controlledSlots = new Dictionary<byte, ControlledBotSlot>();
        foreach (var entry in _practiceBotSlots)
        {
            controlledSlots[entry.Key] = new ControlledBotSlot(
                entry.Key,
                entry.Value.Team,
                entry.Value.ClassId);
        }

        return controlledSlots;
    }

    private IEnumerable<PlayerEntity> EnumeratePracticeBotPlayersForView()
    {
        for (var slotValue = SimulationWorld.LocalPlayerSlot + 1; slotValue <= SimulationWorld.MaxPlayableNetworkPlayers; slotValue += 1)
        {
            var slot = (byte)slotValue;
            if (!_practiceBotSlots.ContainsKey(slot)
                || _world.IsNetworkPlayerAwaitingJoin(slot)
                || !_world.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            yield return player;
        }
    }
}
