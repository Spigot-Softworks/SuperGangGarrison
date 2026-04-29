#nullable enable

using OpenGarrison.BotAI;
using OpenGarrison.Core;
using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int BrowserPracticeBotThinkIntervalTicksSmallRoster = 2;
    private const int BrowserPracticeBotThinkIntervalTicksMediumRoster = 3;
    private const int BrowserPracticeBotThinkIntervalTicksLargeRoster = 4;
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
    private readonly PracticeBotDisplayNamePool _practiceBotDisplayNamePool = new();
    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Practice bots intentionally sit behind a controller seam so the practice session can select MotionProof, Modern, or ML controllers without leaking implementation details into the client plumbing.")]
    private readonly IPracticeBotController _practiceBotController = CreatePracticeBotController();
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
        _browserPracticeBotInputCache.Clear();
        _browserPracticeBotThinkTick = 0;
        _browserPracticeBotPerfSamples = 0;
        _browserPracticeBotPerfBuildInputTotalMilliseconds = 0d;
        _browserPracticeBotPerfBuildInputMaxMilliseconds = 0d;
        _browserPracticeBotPerfSetInputTotalMilliseconds = 0d;
        _browserPracticeBotPerfSetInputMaxMilliseconds = 0d;
        _practiceBotController.Reset();
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
            _world.TryReleaseNetworkPlayerSlot(slot);
            _practiceBotSlots.Remove(slot);
            _practiceBotDisplayNamePool.ReleaseSlot(slot);
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

        var classCycle = GetEligiblePracticeBotClassCycle();
        nextSlot = AppendDesiredPracticeBotSlots(
            desiredSlots,
            nextSlot,
            localTeam,
            GetOfflineFriendlyBotCount(),
            classCycle,
            classOffset: 0);
        _ = AppendDesiredPracticeBotSlots(
            desiredSlots,
            nextSlot,
            GetOpposingTeam(localTeam),
            GetOfflineEnemyBotCount(),
            classCycle,
            classOffset: 3);
        return desiredSlots;
    }

    private byte AppendDesiredPracticeBotSlots(
        Dictionary<byte, PracticeBotSlotState> desiredSlots,
        byte startSlot,
        PlayerTeam team,
        int count,
        PlayerClass[] classCycle,
        int classOffset)
    {
        var nextSlot = startSlot;
        if (count <= 0 || classCycle.Length == 0)
        {
            return nextSlot;
        }

        for (var index = 0; index < count && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
        {
            var classId = classCycle[(index + classOffset) % classCycle.Length];
            desiredSlots[nextSlot] = new PracticeBotSlotState(
                nextSlot,
                team,
                classId,
                _practiceBotDisplayNamePool.GetOrAssign(nextSlot, team, index + 1));
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
        int count)
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
        for (var index = 0; index < count && nextSlot <= SimulationWorld.MaxPlayableNetworkPlayers; index += 1)
        {
            var classId = _practiceBotSlots.TryGetValue(nextSlot, out var existing)
                          && eligibleSet.Contains(existing.ClassId)
                ? existing.ClassId
                : randomizedPool[randomizedIndex++ % randomizedPool.Count];
            desiredSlots[nextSlot] = new PracticeBotSlotState(
                nextSlot,
                team,
                classId,
                _practiceBotDisplayNamePool.GetOrAssign(nextSlot, team, index + 1));
            nextSlot += 1;
        }

        return nextSlot;
    }

    private List<PlayerClass> BuildRandomizedPracticeBotClassPool(PlayerClass[] eligibleClasses)
    {
        var pool = eligibleClasses.ToList();
        for (var index = pool.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = _visualRandom.Next(index + 1);
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

    private void UpdatePracticeBots()
    {
        if (!IsOfflineBotSessionActive)
        {
            return;
        }

        var collectDiagnostics = _botDiagnosticsEnabled || (_navEditorEnabled && _navEditorShowBotTags);
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
        var buildInputsStartTimestamp = OperatingSystem.IsBrowser() ? Stopwatch.GetTimestamp() : 0L;
        var inputsBySlot = GetPracticeBotInputs(controlledSlots);
        var buildInputsMilliseconds = GetDiagnosticsElapsedMilliseconds(buildInputsStartTimestamp);
        var setInputsStartTimestamp = OperatingSystem.IsBrowser() ? Stopwatch.GetTimestamp() : 0L;
        foreach (var entry in controlledSlots)
        {
            _world.TrySetNetworkPlayerInput(entry.Key, inputsBySlot.GetValueOrDefault(entry.Key));
        }
        var setInputsMilliseconds = GetDiagnosticsElapsedMilliseconds(setInputsStartTimestamp);
        LogBrowserPracticeBotPerformance(controlledSlots.Count, buildInputsMilliseconds, setInputsMilliseconds);

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
        var thinkIntervalTicks = GetBrowserPracticeBotThinkIntervalTicks(controlledSlots.Count);
        var shouldRefreshInputs = _browserPracticeBotInputCache.Count == 0
            || (_browserPracticeBotThinkTick % thinkIntervalTicks) == 0
            || !DoBrowserPracticeBotInputsCoverRoster(controlledSlots);
        if (!shouldRefreshInputs)
        {
            return _browserPracticeBotInputCache;
        }

        var refreshedInputs = _practiceBotController.BuildInputs(_world, controlledSlots);
        SyncBrowserPracticeBotInputCache(controlledSlots, refreshedInputs);
        return _browserPracticeBotInputCache;
    }

    private void LogBrowserPracticeBotPerformance(int controlledBotCount, double buildInputsMilliseconds, double setInputsMilliseconds)
    {
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
            $"Browser practice bots slots={controlledBotCount} thinkInterval={GetBrowserPracticeBotThinkIntervalTicks(controlledBotCount)} " +
            $"build(last/avg/max)={buildInputsMilliseconds:0.0}/{averageBuildInputsMilliseconds:0.0}/{_browserPracticeBotPerfBuildInputMaxMilliseconds:0.0}ms " +
            $"apply(last/avg/max)={setInputsMilliseconds:0.0}/{averageSetInputsMilliseconds:0.0}/{_browserPracticeBotPerfSetInputMaxMilliseconds:0.0}ms");
    }

    private static int GetBrowserPracticeBotThinkIntervalTicks(int controlledBotCount)
    {
        return controlledBotCount switch
        {
            >= 8 => BrowserPracticeBotThinkIntervalTicksLargeRoster,
            >= 4 => BrowserPracticeBotThinkIntervalTicksMediumRoster,
            _ => BrowserPracticeBotThinkIntervalTicksSmallRoster,
        };
    }

    private bool DoBrowserPracticeBotInputsCoverRoster(IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots)
    {
        if (_browserPracticeBotInputCache.Count != controlledSlots.Count)
        {
            return false;
        }

        foreach (var slot in controlledSlots.Keys)
        {
            if (!_browserPracticeBotInputCache.ContainsKey(slot))
            {
                return false;
            }
        }

        return true;
    }

    private void SyncBrowserPracticeBotInputCache(
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledSlots,
        IReadOnlyDictionary<byte, PlayerInputSnapshot> refreshedInputs)
    {
        var staleSlots = new List<byte>();
        foreach (var entry in _browserPracticeBotInputCache)
        {
            if (!controlledSlots.ContainsKey(entry.Key))
            {
                staleSlots.Add(entry.Key);
            }
        }

        for (var index = 0; index < staleSlots.Count; index += 1)
        {
            _browserPracticeBotInputCache.Remove(staleSlots[index]);
        }

        foreach (var slot in controlledSlots.Keys)
        {
            _browserPracticeBotInputCache[slot] = refreshedInputs.GetValueOrDefault(slot);
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
                entry.Value.ClassId);
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
        return string.Join("/", NavEditorScoreTrioClasses.Select(BotNavigationClasses.GetShortLabel));
    }

    private static IPracticeBotController CreatePracticeBotController()
    {
        var botController = Environment.GetEnvironmentVariable("OG_BOT_CONTROLLER");
        if (string.Equals(botController, "motion_proof", StringComparison.OrdinalIgnoreCase)
            || string.Equals(botController, "MotionProof", StringComparison.OrdinalIgnoreCase))
        {
            return new MotionProofPracticeBotController();
        }

        if (string.Equals(botController, "modern", StringComparison.OrdinalIgnoreCase)
            || string.Equals(botController, "ModernGraphRoute", StringComparison.OrdinalIgnoreCase))
        {
            return new ModernPracticeBotController();
        }

        if (string.Equals(botController, "objective_traversal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(botController, "ObjectiveTraversal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(botController, "lattice", StringComparison.OrdinalIgnoreCase))
        {
            return new MotionProofPracticeBotController();
        }

        return MLBotModeResolver.Resolve() is MLBotControllerMode.ML or MLBotControllerMode.Capture
            ? new MLPracticeBotController()
            : new MotionProofPracticeBotController();
    }
}
