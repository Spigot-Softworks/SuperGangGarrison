#nullable enable

using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int BrowserPracticeBotThinkBatchSizeSmallRoster = 1;
    private const int BrowserPracticeBotThinkBatchSizeMediumRoster = 1;
    private const int BrowserPracticeBotThinkBatchSizeLargeRoster = 2;
    private static readonly PlayerClass[] PracticeBotClassCycle =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Demoman,
        PlayerClass.Medic,
        PlayerClass.Engineer,
        PlayerClass.Spy,
        PlayerClass.Sniper,
        PlayerClass.Quote,
    ];
    private static readonly PlayerClass[] NavEditorScoreTrioClasses =
    [
        PlayerClass.Scout,
        PlayerClass.Heavy,
        PlayerClass.Pyro,
    ];

    private readonly Dictionary<byte, PracticeBotSlotState> _practiceBotSlots = new();
    private readonly Dictionary<byte, PlayerInputSnapshot> _browserPracticeBotInputCache = new();
    private readonly Dictionary<byte, ControlledBotSlot> _controlledPracticeBotSlotsBuffer = new();
    private readonly List<byte> _browserPracticeBotThinkSlotsBuffer = new();
    private readonly List<byte> _browserPracticeBotRosterSlotsBuffer = new();
    private readonly List<byte> _browserPracticeBotStaleSlotsBuffer = new();
    private readonly List<ManualPracticeBotRequest> _manualPracticeBotRequests = new();
    private readonly PracticeBotDisplayNamePool _practiceBotDisplayNamePool = new();
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Practice bots intentionally sit behind a controller seam so client and server bot plumbing can select different controller implementations.")]
    private IPracticeBotController _practiceBotController = new BotBrainPracticeBotController();
    private string _practiceBotControllerSelectionKey = nameof(OfflineBotControllerMode.BotBrain);
    private static readonly (PlayerClass Medic, PlayerClass Partner)[] LastToDieOpeningEnemyCombos =
    [
        (PlayerClass.Medic, PlayerClass.Heavy),
        (PlayerClass.Medic, PlayerClass.Scout),
        (PlayerClass.Medic, PlayerClass.Soldier),
        (PlayerClass.Medic, PlayerClass.Pyro),
        (PlayerClass.Medic, PlayerClass.Demoman),
    ];
    private int _browserPracticeBotThinkTick;
    private int _browserPracticeBotPerfSamples;
    private double _browserPracticeBotPerfBuildInputTotalMilliseconds;
    private double _browserPracticeBotPerfBuildInputMaxMilliseconds;
    private double _browserPracticeBotPerfSetInputTotalMilliseconds;
    private double _browserPracticeBotPerfSetInputMaxMilliseconds;
    private bool _navEditorScoreTrioActive;

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

    private sealed class ManualPracticeBotRequest
    {
        public ManualPracticeBotRequest(PlayerTeam team, PlayerClass classId, string displayName)
        {
            Team = team;
            ClassId = classId;
            DisplayName = displayName.Trim();
        }

        public PlayerTeam Team { get; }

        public PlayerClass ClassId { get; }

        public string DisplayName { get; }
    }

    private void ResetPracticeBotManagerState(bool releaseWorldSlots)
    {
        ClearPracticeBotSpawnOverrides(_practiceBotSlots.Keys);
        if (releaseWorldSlots)
        {
            var slotsToRelease = new List<byte>(_practiceBotSlots.Keys);
            for (var index = 0; index < slotsToRelease.Count; index += 1)
            {
                _world.TryReleaseNetworkPlayerSlot(slotsToRelease[index]);
            }
        }

        _practiceBotSlots.Clear();
        ResetPracticeBotControllerState();
    }

    private void SyncPracticeBotRoster(PlayerTeam localTeam)
    {
        if (!IsOfflineBotSessionActive)
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
            _world.TryClearNetworkPlayerSpawnOverride(slot);
            _world.TryReleaseNetworkPlayerSlot(slot);
            _practiceBotSlots.Remove(slot);
            _practiceBotDisplayNamePool.ReleaseSlot(slot);
        }

        var desiredControlledSlots = desiredSlots.ToDictionary(
            static entry => entry.Key,
            static entry => new ControlledBotSlot(entry.Key, entry.Value.Team, entry.Value.ClassId));
        ClearPracticeBotSpawnOverrides(desiredControlledSlots.Keys);
        _practiceBotController.ConfigureSpawnOverrides(_world, desiredControlledSlots);

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

        if (_navEditorScoreTrioActive && !IsLastToDieSessionActive)
        {
            AppendExplicitDesiredPracticeBotSlots(
                desiredSlots,
                nextSlot,
                localTeam,
                NavEditorScoreTrioClasses);
            return desiredSlots;
        }

        if (IsLastToDieSessionActive)
        {
            nextSlot = AppendLastToDieDesiredPracticeBotSlots(
                desiredSlots,
                nextSlot,
                localTeam);
            return desiredSlots;
        }

        nextSlot = AppendRandomizedDesiredPracticeBotSlots(
            desiredSlots,
            nextSlot,
            localTeam,
            GetOfflineFriendlyBotCount(),
            guaranteeSecondMedic: true);
        nextSlot = AppendRandomizedDesiredPracticeBotSlots(
            desiredSlots,
            nextSlot,
            GetOpposingTeam(localTeam),
            GetOfflineEnemyBotCount(),
            guaranteeSecondMedic: true);
        nextSlot = AppendManualDesiredPracticeBotSlots(desiredSlots, nextSlot);
        return desiredSlots;
    }

    private byte AppendManualDesiredPracticeBotSlots(
        Dictionary<byte, PracticeBotSlotState> desiredSlots,
        byte startSlot)
    {
        var nextSlot = startSlot;
        for (var index = 0; index < _manualPracticeBotRequests.Count && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
        {
            var request = _manualPracticeBotRequests[index];
            var teamBotNumber = desiredSlots.Values.Count(slot => slot.Team == request.Team) + 1;
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? _practiceBotDisplayNamePool.GetOrAssign(nextSlot, request.Team, teamBotNumber)
                : request.DisplayName;
            _practiceBotDisplayNamePool.Reserve(displayName);
            desiredSlots[nextSlot] = new PracticeBotSlotState(
                nextSlot,
                request.Team,
                request.ClassId,
                displayName);
            nextSlot += 1;
        }

        return nextSlot;
    }

    private byte AppendExplicitDesiredPracticeBotSlots(
        Dictionary<byte, PracticeBotSlotState> desiredSlots,
        byte startSlot,
        PlayerTeam team,
        PlayerClass[] classes)
    {
        var nextSlot = startSlot;
        for (var index = 0; index < classes.Length && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
        {
            desiredSlots[nextSlot] = new PracticeBotSlotState(
                nextSlot,
                team,
                classes[index],
                _practiceBotDisplayNamePool.GetOrAssign(nextSlot, team, index + 1));
            nextSlot += 1;
        }

        return nextSlot;
    }

    private byte AppendLastToDieDesiredPracticeBotSlots(
        Dictionary<byte, PracticeBotSlotState> desiredSlots,
        byte startSlot,
        PlayerTeam localTeam)
    {
        var nextSlot = startSlot;
        nextSlot = AppendRandomizedDesiredPracticeBotSlots(
            desiredSlots,
            nextSlot,
            localTeam,
            GetOfflineFriendlyBotCount());

        var enemyClasses = BuildLastToDieEnemyBotClassList(GetOfflineEnemyBotCount());
        var enemyTeam = GetOpposingTeam(localTeam);
        for (var index = 0; index < enemyClasses.Count && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
        {
            desiredSlots[nextSlot] = new PracticeBotSlotState(
                nextSlot,
                enemyTeam,
                enemyClasses[index],
                _practiceBotDisplayNamePool.GetOrAssign(nextSlot, enemyTeam, index + 1));
            nextSlot += 1;
        }

        return nextSlot;
    }

    private byte AppendRandomizedDesiredPracticeBotSlots(
        Dictionary<byte, PracticeBotSlotState> desiredSlots,
        byte startSlot,
        PlayerTeam team,
        int count,
        bool guaranteeSecondMedic = false)
    {
        var nextSlot = startSlot;
        if (count <= 0)
        {
            return nextSlot;
        }

        var eligibleClasses = GetEligiblePracticeBotClassCycle();
        if (eligibleClasses.Length == 0)
        {
            return nextSlot;
        }

        var eligibleSet = new HashSet<PlayerClass>(eligibleClasses);
        var randomizedPool = BuildRandomizedPracticeBotClassPool(eligibleClasses);
        var randomizedIndex = 0;
        var hasMedic = eligibleSet.Contains(PlayerClass.Medic);
        for (var index = 0; index < count && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
        {
            var forceMedic = guaranteeSecondMedic && index == 1 && hasMedic;
            var avoidMedic = guaranteeSecondMedic && count > 1 && index == 0 && eligibleSet.Count > 1;
            PlayerClass classId;
            if (forceMedic)
            {
                classId = PlayerClass.Medic;
            }
            else if (_practiceBotSlots.TryGetValue(nextSlot, out var existing)
                     && eligibleSet.Contains(existing.ClassId)
                     && (!avoidMedic || existing.ClassId != PlayerClass.Medic))
            {
                classId = existing.ClassId;
            }
            else
            {
                classId = TakeRandomPracticeBotClass(randomizedPool, ref randomizedIndex, avoidMedic);
            }

            desiredSlots[nextSlot] = new PracticeBotSlotState(
                nextSlot,
                team,
                classId,
                _practiceBotDisplayNamePool.GetOrAssign(nextSlot, team, index + 1));
            nextSlot += 1;
        }

        return nextSlot;
    }

    private static PlayerClass TakeRandomPracticeBotClass(
        List<PlayerClass> randomizedPool,
        ref int randomizedIndex,
        bool avoidMedic)
    {
        for (var attempts = 0; attempts < randomizedPool.Count; attempts += 1)
        {
            var classId = randomizedPool[randomizedIndex++ % randomizedPool.Count];
            if (!avoidMedic || classId != PlayerClass.Medic)
            {
                return classId;
            }
        }

        return randomizedPool[0];
    }

    private static List<PlayerClass> BuildRandomizedPracticeBotClassPool(PlayerClass[] eligibleClasses)
    {
        var pool = eligibleClasses.ToList();
        for (var index = pool.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (pool[index], pool[swapIndex]) = (pool[swapIndex], pool[index]);
        }

        return pool;
    }

    private static PlayerClass[] GetEligiblePracticeBotClassCycle()
    {
        return PracticeBotClassCycle;
    }

    private List<PlayerClass> BuildLastToDieEnemyBotClassList(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        if (_lastToDieRun?.CurrentSpecialRound is { IsSpecial: true } specialRound)
        {
            switch (specialRound.Kind)
            {
                case LastToDieSpecialRoundKind.AllOneClass:
                    return Enumerable
                        .Repeat(specialRound.SelectedClass ?? PickRandomLastToDieEligibleBotClass(excludeMedic: false), count)
                        .ToList();
                case LastToDieSpecialRoundKind.Giga:
                    var gigaClasses = specialRound.GigaClasses.Length == 0
                        ? PickRandomLastToDieGigaClasses(GetLastToDieGigaBossCount(_lastToDieRun.StageNumber))
                        : specialRound.GigaClasses;
                    var gigaList = gigaClasses.ToList();
                    gigaList.Add(PlayerClass.Medic);
                    return gigaList;
                case LastToDieSpecialRoundKind.DroneSwarm:
                    return [];
                case LastToDieSpecialRoundKind.Haxton:
                    return [PlayerClass.Demoman];
            }
        }

        if (_lastToDieRun is not null
            && _lastToDieRun.StageNumber == 1
            && count == 2)
        {
            var openingCombo = TryBuildLastToDieOpeningEnemyPair();
            if (openingCombo is not null)
            {
                return [openingCombo.Value.Medic, openingCombo.Value.Partner];
            }
        }

        var eligibleClasses = GetEligibleLastToDieBotClassCycle();
        if (eligibleClasses.Length == 0)
        {
            return [];
        }

        var randomizedPool = BuildRandomizedPracticeBotClassPool(eligibleClasses);
        var classList = new List<PlayerClass>(count);
        for (var index = 0; index < count; index += 1)
        {
            classList.Add(randomizedPool[index % randomizedPool.Count]);
        }

        return classList;
    }

    private (PlayerClass Medic, PlayerClass Partner)? TryBuildLastToDieOpeningEnemyPair()
    {
        var eligibleClasses = new HashSet<PlayerClass>(GetEligibleLastToDieBotClassCycle());
        var validCombos = LastToDieOpeningEnemyCombos
            .Where(combo => eligibleClasses.Contains(combo.Medic) && eligibleClasses.Contains(combo.Partner))
            .ToList();
        if (validCombos.Count == 0)
        {
            return null;
        }

        return validCombos[_visualRandom.Next(validCombos.Count)];
    }

    private PlayerClass[] GetEligibleLastToDieBotClassCycle()
    {
        return GetEligiblePracticeBotClassCycle()
            .Where(IsEligibleLastToDieBotClass)
            .ToArray();
    }

    private bool IsEligibleLastToDieBotClass(PlayerClass classId)
    {
        if (classId == PlayerClass.Quote)
        {
            return false;
        }

        if (_lastToDieRun is not null
            && _lastToDieRun.StageNumber < 3
            && classId == PlayerClass.Sniper)
        {
            return false;
        }

        var levelName = _lastToDieRun?.CurrentLevelName ?? _world.Level.Name;
        if (MatchesLastToDieRestrictedMap(levelName, "Harvest")
            && classId == PlayerClass.Pyro)
        {
            return false;
        }

        if (MatchesLastToDieRestrictedMap(levelName, "Valley")
            && classId == PlayerClass.Heavy)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesLastToDieRestrictedMap(string? levelName, string restrictedMapName)
    {
        return !string.IsNullOrWhiteSpace(levelName)
            && (string.Equals(levelName, restrictedMapName, StringComparison.OrdinalIgnoreCase)
                || levelName.Contains(restrictedMapName, StringComparison.OrdinalIgnoreCase));
    }

    private void InitializePracticeBotNamePoolForMatch()
    {
        _practiceBotDisplayNamePool.Reset();
    }

    private void ClearManualPracticeBotRequests()
    {
        _manualPracticeBotRequests.Clear();
    }

    private void TryHandlePracticeBotConsoleCommand(string[] parts)
    {
        if (!IsPracticeSessionActive)
        {
            AddConsoleLine("practice_bot commands require an active Practice session.");
            return;
        }

        if (parts.Length < 2)
        {
            PrintPracticeBotConsoleRoster();
            AddConsoleLine("usage: practice_bot <add|list|clear> ...");
            return;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "add":
                TryAddManualPracticeBotFromConsole(parts);
                break;
            case "list":
                PrintPracticeBotConsoleRoster();
                break;
            case "clear":
                ClearManualPracticeBotsFromConsole();
                break;
            default:
                AddConsoleLine("usage: practice_bot add <friendly|enemy|red|blue> <class> [name]");
                AddConsoleLine("usage: practice_bot <list|clear>");
                break;
        }
    }

    private void TryAddManualPracticeBotFromConsole(string[] parts)
    {
        if (_navEditorScoreTrioActive)
        {
            AddConsoleLine("practice_bot add is unavailable while nav editor score trio is active.");
            return;
        }

        if (parts.Length < 4
            || !TryParsePracticeBotTeam(parts[2], out var team)
            || !TryParsePlayerClass(parts[3], out var playerClass))
        {
            AddConsoleLine("usage: practice_bot add <friendly|enemy|red|blue> <scout|engineer|pyro|soldier|demoman|heavy|sniper|medic|spy|quote> [name]");
            return;
        }

        var maxPracticeBotCount = SimulationWorld.MaxPlayableNetworkPlayers - SimulationWorld.LocalPlayerSlot;
        if (_practiceBotSlots.Count >= maxPracticeBotCount)
        {
            AddConsoleLine("practice bot roster is full.");
            return;
        }

        var displayName = parts.Length > 4
            ? string.Join(' ', parts.Skip(4)).Trim()
            : string.Empty;
        var existingSlots = _practiceBotSlots.Keys.ToHashSet();
        _manualPracticeBotRequests.Add(new ManualPracticeBotRequest(team, playerClass, displayName));
        SyncPracticeBotRoster(_world.LocalPlayerTeam);

        var addedSlot = _practiceBotSlots.Keys
            .Where(slot => !existingSlots.Contains(slot))
            .OrderBy(static slot => slot)
            .FirstOrDefault();
        if (addedSlot == 0 || !_practiceBotSlots.TryGetValue(addedSlot, out var addedState))
        {
            _manualPracticeBotRequests.RemoveAt(_manualPracticeBotRequests.Count - 1);
            SyncPracticeBotRoster(_world.LocalPlayerTeam);
            AddConsoleLine("unable to add practice bot.");
            return;
        }

        AddConsoleLine($"practice bot added slot={addedState.Slot} team={addedState.Team} class={addedState.ClassId} name={addedState.DisplayName}");
    }

    private void ClearManualPracticeBotsFromConsole()
    {
        var clearedCount = _manualPracticeBotRequests.Count;
        if (clearedCount == 0)
        {
            AddConsoleLine("no manual practice bots to clear.");
            return;
        }

        _manualPracticeBotRequests.Clear();
        SyncPracticeBotRoster(_world.LocalPlayerTeam);
        AddConsoleLine($"cleared {clearedCount} manual practice bot{(clearedCount == 1 ? string.Empty : "s")}.");
    }

    private void PrintPracticeBotConsoleRoster()
    {
        if (_practiceBotSlots.Count == 0)
        {
            AddConsoleLine("practice bots: none");
            return;
        }

        AddConsoleLine($"practice bots: active={_practiceBotSlots.Count} manual={_manualPracticeBotRequests.Count}");
        foreach (var entry in _practiceBotSlots.OrderBy(static entry => entry.Key))
        {
            var bot = entry.Value;
            AddConsoleLine($"practice bot slot={bot.Slot} team={bot.Team} class={bot.ClassId} name={bot.DisplayName}");
        }
    }

    private bool TryParsePracticeBotTeam(string value, out PlayerTeam team)
    {
        team = default;
        var normalized = value.Trim();
        if (normalized.Equals("friendly", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("friend", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ally", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("allied", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("local", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("team", StringComparison.OrdinalIgnoreCase))
        {
            team = _world.LocalPlayerTeam;
            return true;
        }

        if (normalized.Equals("enemy", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("opponent", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("opposing", StringComparison.OrdinalIgnoreCase))
        {
            team = GetOpposingTeam(_world.LocalPlayerTeam);
            return true;
        }

        if (normalized.Equals("red", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Red;
            return true;
        }

        if (normalized.Equals("blue", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("blu", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            team = PlayerTeam.Blue;
            return true;
        }

        return false;
    }

    private void UpdatePracticeBots()
    {
        if (!IsOfflineBotSessionActive)
        {
            return;
        }

        var collectDiagnostics = ClientPerformanceTestEnabled || _botDiagnosticsEnabled || (_navEditorEnabled && _navEditorShowBotTags);
        _practiceBotController.CollectDiagnostics = collectDiagnostics;
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
        var buildInputsStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        var inputsBySlot = GetPracticeBotInputs(controlledSlots);
        var buildInputsMilliseconds = GetDiagnosticsElapsedMilliseconds(buildInputsStartTimestamp);
        var setInputsStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        foreach (var entry in controlledSlots)
        {
            _world.TrySetNetworkPlayerInput(entry.Key, inputsBySlot.GetValueOrDefault(entry.Key));
        }
        var setInputsMilliseconds = GetDiagnosticsElapsedMilliseconds(setInputsStartTimestamp);
        LogBrowserPracticeBotPerformance(controlledSlots.Count, buildInputsMilliseconds, setInputsMilliseconds);
        RecordClientPerformanceBotBehavior(controlledSlots, _practiceBotController.LastDiagnostics);

        if (_botDiagnosticsEnabled)
        {
            RecordPracticeBotDiagnosticsUpdate(
                GetDiagnosticsElapsedMilliseconds(diagnosticsStartTimestamp),
                _practiceBotController.LastDiagnostics);
        }
    }

    private IReadOnlyDictionary<byte, PlayerInputSnapshot> GetPracticeBotInputs(
        Dictionary<byte, ControlledBotSlot> controlledSlots)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return _practiceBotController.BuildInputs(_world, controlledSlots);
        }

        _browserPracticeBotThinkTick += 1;
        SyncBrowserPracticeBotInputCacheRoster(controlledSlots);
        var slotsToThink = SelectBrowserPracticeBotThinkSlots(controlledSlots);
        if (slotsToThink.Count == 0)
        {
            return _browserPracticeBotInputCache;
        }

        var refreshedInputs = _practiceBotController.BuildInputsForSlots(_world, controlledSlots, slotsToThink);
        ApplyBrowserPracticeBotInputRefresh(refreshedInputs);
        return _browserPracticeBotInputCache;
    }

    private void LogBrowserPracticeBotPerformance(int controlledBotCount, double buildInputsMilliseconds, double setInputsMilliseconds)
    {
        RecordClientPerformanceMetric(ClientPerformanceMetric.BotBuild, buildInputsMilliseconds);
        RecordClientPerformanceMetric(ClientPerformanceMetric.BotApply, setInputsMilliseconds);
        if (!OperatingSystem.IsBrowser() || controlledBotCount <= 0)
        {
            return;
        }

        _browserPracticeBotPerfSamples += 1;
        _browserPracticeBotPerfBuildInputTotalMilliseconds += buildInputsMilliseconds;
        _browserPracticeBotPerfBuildInputMaxMilliseconds = Math.Max(_browserPracticeBotPerfBuildInputMaxMilliseconds, buildInputsMilliseconds);
        _browserPracticeBotPerfSetInputTotalMilliseconds += setInputsMilliseconds;
        _browserPracticeBotPerfSetInputMaxMilliseconds = Math.Max(_browserPracticeBotPerfSetInputMaxMilliseconds, setInputsMilliseconds);
        if ((_browserPracticeBotPerfSamples % 30) != 0)
        {
            return;
        }

        var averageBuildInputsMilliseconds = _browserPracticeBotPerfBuildInputTotalMilliseconds / _browserPracticeBotPerfSamples;
        var averageSetInputsMilliseconds = _browserPracticeBotPerfSetInputTotalMilliseconds / _browserPracticeBotPerfSamples;
        Console.WriteLine(
            $"Browser practice bots slots={controlledBotCount} thinkBatch={GetBrowserPracticeBotThinkBatchSize(controlledBotCount)} " +
            $"build(last/avg/max)={buildInputsMilliseconds:0.0}/{averageBuildInputsMilliseconds:0.0}/{_browserPracticeBotPerfBuildInputMaxMilliseconds:0.0}ms " +
            $"apply(last/avg/max)={setInputsMilliseconds:0.0}/{averageSetInputsMilliseconds:0.0}/{_browserPracticeBotPerfSetInputMaxMilliseconds:0.0}ms");
    }

    private static int GetBrowserPracticeBotThinkBatchSize(int controlledBotCount)
    {
        return controlledBotCount switch
        {
            >= 8 => BrowserPracticeBotThinkBatchSizeLargeRoster,
            >= 4 => BrowserPracticeBotThinkBatchSizeMediumRoster,
            _ => BrowserPracticeBotThinkBatchSizeSmallRoster,
        };
    }

    private List<byte> SelectBrowserPracticeBotThinkSlots(IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _browserPracticeBotThinkSlotsBuffer.Clear();
        if (controlledSlots.Count == 0)
        {
            return _browserPracticeBotThinkSlotsBuffer;
        }

        _browserPracticeBotRosterSlotsBuffer.Clear();
        foreach (var slot in controlledSlots.Keys)
        {
            _browserPracticeBotRosterSlotsBuffer.Add(slot);
        }

        _browserPracticeBotRosterSlotsBuffer.Sort();
        var batchSize = Math.Min(
            _browserPracticeBotRosterSlotsBuffer.Count,
            GetBrowserPracticeBotThinkBatchSize(_browserPracticeBotRosterSlotsBuffer.Count));
        var startIndex = (int)(((long)Math.Max(0, _browserPracticeBotThinkTick - 1) * batchSize) % _browserPracticeBotRosterSlotsBuffer.Count);
        for (var batchIndex = 0; batchIndex < batchSize; batchIndex += 1)
        {
            var slotIndex = (startIndex + batchIndex) % _browserPracticeBotRosterSlotsBuffer.Count;
            _browserPracticeBotThinkSlotsBuffer.Add(_browserPracticeBotRosterSlotsBuffer[slotIndex]);
        }

        return _browserPracticeBotThinkSlotsBuffer;
    }

    private void SyncBrowserPracticeBotInputCacheRoster(IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        _browserPracticeBotStaleSlotsBuffer.Clear();
        foreach (var entry in _browserPracticeBotInputCache)
        {
            if (!controlledSlots.ContainsKey(entry.Key))
            {
                _browserPracticeBotStaleSlotsBuffer.Add(entry.Key);
            }
        }

        for (var index = 0; index < _browserPracticeBotStaleSlotsBuffer.Count; index += 1)
        {
            _browserPracticeBotInputCache.Remove(_browserPracticeBotStaleSlotsBuffer[index]);
        }

        foreach (var slot in controlledSlots.Keys)
        {
            _browserPracticeBotInputCache.TryAdd(slot, default);
        }
    }

    private void ApplyBrowserPracticeBotInputRefresh(IReadOnlyDictionary<byte, PlayerInputSnapshot> refreshedInputs)
    {
        foreach (var (slot, input) in refreshedInputs)
        {
            _browserPracticeBotInputCache[slot] = input;
        }
    }

    private Dictionary<byte, ControlledBotSlot> BuildControlledPracticeBotSlots()
    {
        _controlledPracticeBotSlotsBuffer.Clear();
        foreach (var entry in _practiceBotSlots)
        {
            _controlledPracticeBotSlotsBuffer[entry.Key] = new ControlledBotSlot(
                entry.Key,
                entry.Value.Team,
                entry.Value.ClassId,
                IsLastToDieHaxtonSlot(entry.Key));
        }

        return _controlledPracticeBotSlotsBuffer;
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

    private void RunNavEditorScoreTrioPracticeBots()
    {
        if (!IsPracticeSessionActive)
        {
            SetNavEditorStatus("score trio is practice-only");
            AddConsoleLine("nav editor score trio requires an active practice session.");
            return;
        }

        _navEditorScoreTrioActive = true;
        ResetPracticeBotManagerState(releaseWorldSlots: true);
        SyncPracticeBotRoster(_world.LocalPlayerTeam);
        var teamLabel = _world.LocalPlayerTeam.ToString();
        SetNavEditorStatus($"score trio spawned from {teamLabel} spawn ({DescribeNavEditorScoreTrioClasses()}, current state)");
        AddConsoleLine($"nav editor score trio respawned {DescribeNavEditorScoreTrioClasses()} on {teamLabel} from spawn in the current match state.");
    }

    private void StopNavEditorScoreTrioPracticeBots(bool silent = false)
    {
        if (!_navEditorScoreTrioActive)
        {
            if (!silent)
            {
                SetNavEditorStatus("score trio is not active");
            }

            return;
        }

        _navEditorScoreTrioActive = false;
        ResetPracticeBotManagerState(releaseWorldSlots: true);
        if (IsOfflineBotSessionActive)
        {
            SyncPracticeBotRoster(_world.LocalPlayerTeam);
        }

        if (!silent)
        {
            SetNavEditorStatus("score trio cleared; restored practice bot roster");
            AddConsoleLine("nav editor score trio cleared; restored the normal practice bot roster.");
        }
    }

    private static string DescribeNavEditorScoreTrioClasses()
    {
        return string.Join("/", NavEditorScoreTrioClasses.Select(static classId => classId.ToString()));
    }

    private void ResetPracticeBotControllerState()
    {
        ClearPracticeBotSpawnOverrides(_practiceBotSlots.Keys);
        _browserPracticeBotInputCache.Clear();
        _browserPracticeBotThinkSlotsBuffer.Clear();
        _browserPracticeBotRosterSlotsBuffer.Clear();
        _browserPracticeBotStaleSlotsBuffer.Clear();
        _browserPracticeBotThinkTick = 0;
        _browserPracticeBotPerfSamples = 0;
        _browserPracticeBotPerfBuildInputTotalMilliseconds = 0d;
        _browserPracticeBotPerfBuildInputMaxMilliseconds = 0d;
        _browserPracticeBotPerfSetInputTotalMilliseconds = 0d;
        _browserPracticeBotPerfSetInputMaxMilliseconds = 0d;
        _practiceBotController.Reset();
    }

    private void ClearPracticeBotSpawnOverrides(IEnumerable<byte> slots)
    {
        foreach (var slot in slots)
        {
            _world.TryClearNetworkPlayerSpawnOverride(slot);
        }
    }

    private void ApplyConfiguredPracticeBotController(bool respawnActiveBots)
    {
        var (selectionKey, controller) = CreatePracticeBotController();
        if (string.Equals(_practiceBotControllerSelectionKey, selectionKey, StringComparison.Ordinal)
            && _practiceBotController.GetType() == controller.GetType())
        {
            return;
        }

        _practiceBotController = controller;
        _practiceBotControllerSelectionKey = selectionKey;
        _botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
        ResetBotDiagnosticSample();

        if (respawnActiveBots && IsOfflineBotSessionActive)
        {
            ResetPracticeBotManagerState(releaseWorldSlots: true);
            SyncPracticeBotRoster(_world.LocalPlayerTeam);
            return;
        }

        ResetPracticeBotControllerState();
    }

    private (string SelectionKey, IPracticeBotController Controller) CreatePracticeBotController()
    {
        var mode = PracticeBotControllerFactory.NormalizeMode(_clientSettings.BotMode);
        return (mode.ToString(), PracticeBotControllerFactory.Create(mode));
    }
}
