using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace OpenGarrison.Core.LastToDie;

/// <summary>
/// Transport-agnostic authoritative Last to Die state machine. The server owns
/// the instance; clients consume immutable snapshots and submit validated commands.
/// </summary>
public sealed class LastToDieDirector
{
    private sealed class PlayerState(Guid playerId)
    {
        public Guid PlayerId { get; } = playerId;

        public LastToDieSurvivorId? SurvivorId { get; set; }

        public List<LastToDiePerkId> OwnedPerks { get; } = [];

        public HashSet<LastToDiePerkId> OwnedPerkSet { get; } = [];

        public LastToDieRewardOffer? ActiveOffer { get; set; }

        public int DraftOrdinal { get; set; }

        public bool IsReady { get; set; }

        public bool IsAlive { get; set; } = true;

        public int Kills { get; set; }

        public int ConquistadorStacks { get; set; }
    }

    private readonly LastToDieRuleset _ruleset;
    private readonly LastToDieSurvivorCatalog _survivors;
    private readonly LastToDiePerkCatalog _perks;
    private readonly IReadOnlyList<string> _mapRotation;
    private readonly LastToDieRandom _mapRandom;
    private readonly Dictionary<Guid, PlayerState> _players = [];
    private ulong _nextOfferId = 1;
    private ulong _structuralRevision = 1;
    private LastToDiePhase _phase = LastToDiePhase.Lobby;
    private int _stageNumber;
    private ulong _stageInstanceId;
    private string _currentMap = string.Empty;
    private int _enemyCount;
    private long _stageEndServerTick;
    private long _runEndServerTick;
    private string _terminalReason = string.Empty;

    public LastToDieDirector(
        LastToDieRuleset ruleset,
        LastToDieSurvivorCatalog survivors,
        LastToDiePerkCatalog perks,
        IEnumerable<string> mapRotation,
        LastToDieDifficulty difficulty,
        ulong seed,
        Guid? runId = null)
    {
        _ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
        _ruleset.Validate();
        _survivors = survivors ?? throw new ArgumentNullException(nameof(survivors));
        _perks = perks ?? throw new ArgumentNullException(nameof(perks));
        ArgumentNullException.ThrowIfNull(mapRotation);

        var maps = mapRotation
            .Where(map => !string.IsNullOrWhiteSpace(map))
            .Select(map => map.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (maps.Length == 0)
        {
            throw new InvalidOperationException("Last to Die requires at least one map.");
        }

        _mapRotation = Array.AsReadOnly(maps);
        _mapRandom = new LastToDieRandom(
            LastToDieRandom.DeriveSeed(seed, 0x4D4150UL),
            sequence: 0x4C54444D4150UL);
        Difficulty = difficulty;
        Seed = seed;
        RunId = runId ?? Guid.NewGuid();
    }

    public Guid RunId { get; }

    public ulong Seed { get; }

    public LastToDieDifficulty Difficulty { get; }

    public LastToDiePhase Phase => _phase;

    public int MaximumPlayers => _ruleset.MaximumPlayers;

    public ulong StructuralRevision => _structuralRevision;

    public bool TryAddPlayer(Guid playerId, out string error)
    {
        if (_phase != LastToDiePhase.Lobby)
        {
            return Fail("Players can only join the Last to Die run in the lobby.", out error);
        }

        if (playerId == Guid.Empty)
        {
            return Fail("Player ID must be non-empty.", out error);
        }

        if (_players.ContainsKey(playerId))
        {
            error = string.Empty;
            return true;
        }

        if (_players.Count >= _ruleset.MaximumPlayers)
        {
            return Fail("The Last to Die run is full.", out error);
        }

        _players.Add(playerId, new PlayerState(playerId));
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TryRemovePlayer(Guid playerId, out string error)
    {
        if (_phase is LastToDiePhase.Won or LastToDiePhase.Lost)
        {
            return Fail("Players cannot leave a completed Last to Die run.", out error);
        }

        if (!_players.Remove(playerId))
        {
            return Fail("Player does not belong to this Last to Die run.", out error);
        }

        if (_players.Count == 0)
        {
            Lose("All survivors left the run.");
            error = string.Empty;
            return true;
        }

        if (_phase == LastToDiePhase.SurvivorChoice
            && _players.Values.All(player => player.SurvivorId.HasValue))
        {
            BeginRewardChoice(nextStageNumber: 1);
        }
        else if (_phase == LastToDiePhase.RewardChoice
                 && _players.Values.All(player => player.ActiveOffer is null))
        {
            PrepareStage(_stageNumber == 0 ? 1 : _stageNumber + 1);
        }

        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TryFailRun(string reason, out string error)
    {
        if (_phase is LastToDiePhase.Won or LastToDiePhase.Lost)
        {
            return Fail("The Last to Die run is already complete.", out error);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Fail("A terminal reason is required.", out error);
        }

        Lose(reason.Trim());
        error = string.Empty;
        return true;
    }

    public bool TryReturnToLobby(out string error)
    {
        if (_phase is not (LastToDiePhase.Won or LastToDiePhase.Lost))
        {
            return Fail("Only a completed Last to Die run can return to its lobby.", out error);
        }

        _phase = LastToDiePhase.Lobby;
        _stageNumber = 0;
        _stageInstanceId = 0;
        _currentMap = string.Empty;
        _enemyCount = 0;
        _stageEndServerTick = 0;
        _runEndServerTick = 0;
        _terminalReason = string.Empty;
        _nextOfferId = 1;
        foreach (var player in _players.Values)
        {
            player.SurvivorId = null;
            player.OwnedPerks.Clear();
            player.OwnedPerkSet.Clear();
            player.ActiveOffer = null;
            player.DraftOrdinal = 0;
            player.IsReady = false;
            player.IsAlive = true;
            player.Kills = 0;
            player.ConquistadorStacks = 0;
        }

        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TrySetRetryReady(Guid playerId, out string error)
    {
        if (_phase != LastToDiePhase.Lost)
        {
            return Fail("Retry votes are only accepted after a failed Last to Die run.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        if (player.IsReady)
        {
            error = string.Empty;
            return true;
        }

        player.IsReady = true;
        if (_players.Values.Any(candidate => !candidate.IsReady))
        {
            TouchStructure();
            error = string.Empty;
            return true;
        }

        if (_players.Values.Any(candidate => !candidate.SurvivorId.HasValue))
        {
            player.IsReady = false;
            return Fail("Every player must have a survivor selected before retrying.", out error);
        }

        RestartWithCurrentSurvivors();
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TryStart(out string error, bool requireReadyRoster = false)
    {
        if (_phase != LastToDiePhase.Lobby)
        {
            return Fail("The Last to Die run has already started.", out error);
        }

        if (_players.Count == 0)
        {
            return Fail("At least one player is required to start Last to Die.", out error);
        }

        if (requireReadyRoster
            && (_players.Count != _ruleset.MaximumPlayers
                || _players.Values.Any(player => !player.IsReady)))
        {
            return Fail("Every lobby seat must be occupied and ready before the host can start.", out error);
        }

        _phase = LastToDiePhase.SurvivorChoice;
        foreach (var player in _players.Values)
        {
            player.IsReady = false;
        }

        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TrySetLobbyReady(Guid playerId, bool isReady, out string error)
    {
        if (_phase != LastToDiePhase.Lobby)
        {
            return Fail("Lobby readiness can only change before the run starts.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        if (player.IsReady == isReady)
        {
            error = string.Empty;
            return true;
        }

        player.IsReady = isReady;
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TrySelectSurvivor(
        Guid playerId,
        LastToDieSurvivorId survivorId,
        out string error)
    {
        if (_phase != LastToDiePhase.SurvivorChoice)
        {
            return Fail("Survivors can only be selected during survivor choice.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        if (!_survivors.Contains(survivorId))
        {
            return Fail($"Unknown Last to Die survivor {survivorId}.", out error);
        }

        if (player.SurvivorId == survivorId)
        {
            error = string.Empty;
            return true;
        }

        player.SurvivorId = survivorId;
        if (_players.Values.All(candidate => candidate.SurvivorId.HasValue))
        {
            BeginRewardChoice(nextStageNumber: 1);
        }

        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TryClearSurvivor(Guid playerId, out string error)
    {
        if (_phase != LastToDiePhase.SurvivorChoice)
        {
            return Fail("A survivor can only be unlocked during survivor choice.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        if (!player.SurvivorId.HasValue)
        {
            error = string.Empty;
            return true;
        }

        player.SurvivorId = null;
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TrySelectReward(
        Guid playerId,
        ulong offerId,
        LastToDiePerkId perkId,
        out string error)
    {
        if (_phase != LastToDiePhase.RewardChoice)
        {
            return Fail("Rewards can only be selected during reward choice.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        var offer = player.ActiveOffer;
        if (offer is null || offer.OfferId != offerId)
        {
            return Fail("Reward offer is stale or does not belong to this player.", out error);
        }

        if (!offer.Choices.Contains(perkId))
        {
            return Fail("Selected perk is not present in the active offer.", out error);
        }

        var survivorId = player.SurvivorId!.Value;
        var stillEligible = _perks.GetEligible(survivorId, player.OwnedPerkSet)
            .Any(definition => definition.Id == perkId);
        if (!stillEligible)
        {
            return Fail("Selected perk no longer satisfies its requirements and exclusions.", out error);
        }

        player.OwnedPerkSet.Add(perkId);
        player.OwnedPerks.Add(perkId);
        player.ActiveOffer = null;
        player.IsReady = true;

        if (_players.Values.All(candidate => candidate.ActiveOffer is null))
        {
            PrepareStage(_stageNumber == 0 ? 1 : _stageNumber + 1);
        }

        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TrySetStageReady(Guid playerId, out string error)
    {
        if (_phase != LastToDiePhase.LoadingStage)
        {
            return Fail("Stage readiness is only accepted while a stage is loading.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        if (player.IsReady)
        {
            error = string.Empty;
            return true;
        }

        player.IsReady = true;
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TrySetStageUnready(Guid playerId, out string error)
    {
        if (_phase != LastToDiePhase.LoadingStage)
        {
            return Fail("Stage readiness can only be cleared while a stage is loading.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        if (!player.IsReady)
        {
            error = string.Empty;
            return true;
        }

        player.IsReady = false;
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TryBeginStage(long serverTick, out string error)
    {
        if (_phase != LastToDiePhase.LoadingStage)
        {
            return Fail("No Last to Die stage is waiting to begin.", out error);
        }

        if (serverTick < 0)
        {
            return Fail("Server tick must be non-negative.", out error);
        }

        if (_players.Values.Any(player => !player.IsReady))
        {
            return Fail("Every connected player must finish the stage-load barrier.", out error);
        }

        var stage = _ruleset.GetStage(_stageNumber);
        _stageEndServerTick = checked(serverTick + stage.DurationTicks);
        if (_runEndServerTick == 0)
        {
            _runEndServerTick = checked(serverTick + _ruleset.RunTimeLimitTicks);
        }

        foreach (var player in _players.Values)
        {
            player.IsReady = false;
            player.IsAlive = true;
        }

        _phase = LastToDiePhase.Playing;
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TryRecordKills(Guid playerId, int killCount, long serverTick, out string error)
    {
        if (_phase != LastToDiePhase.Playing)
        {
            return Fail("Kills are only recorded while a Last to Die stage is playing.", out error);
        }

        if (killCount <= 0 || serverTick < 0)
        {
            return Fail("Kill count and server tick must be valid.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        player.Kills = checked(player.Kills + killCount);
        var reduction = checked((long)_ruleset.KillTimerReductionTicks * killCount);
        _stageEndServerTick = Math.Max(serverTick, _stageEndServerTick - reduction);
        error = string.Empty;
        return true;
    }

    public bool TrySetPlayerAlive(Guid playerId, bool isAlive, out string error)
    {
        if (_phase != LastToDiePhase.Playing)
        {
            return Fail("Player life state can only change while a Last to Die stage is playing.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        if (player.IsAlive == isAlive)
        {
            error = string.Empty;
            return true;
        }

        player.IsAlive = isAlive;
        TouchStructure();
        error = string.Empty;
        return true;
    }

    public bool TrySetPlayerConquistadorStacks(Guid playerId, int stacks, out string error)
    {
        if (stacks < 0 || stacks > LastToDieSniperProfile.ConquistadorMaximumStacks)
        {
            return Fail("Conquistador stacks are outside the supported range.", out error);
        }

        if (!TryGetPlayer(playerId, out var player, out error))
        {
            return false;
        }

        player.ConquistadorStacks = stacks;
        error = string.Empty;
        return true;
    }

    public bool TryAdvancePlayingState(
        long serverTick,
        bool redObjectiveWon,
        bool blueObjectiveWon,
        bool anyAfterlifeWindowActive,
        out string error)
    {
        if (_phase != LastToDiePhase.Playing)
        {
            return Fail("The Last to Die run is not in a playing stage.", out error);
        }

        if (serverTick < 0)
        {
            return Fail("Server tick must be non-negative.", out error);
        }

        if (serverTick >= _runEndServerTick)
        {
            Lose("Run time limit expired.");
        }
        else if (!_players.Values.Any(player => player.IsAlive) && !anyAfterlifeWindowActive)
        {
            Lose("All survivors died.");
        }
        else if (blueObjectiveWon)
        {
            Lose("Enemy team completed the objective.");
        }
        else if (redObjectiveWon || serverTick >= _stageEndServerTick)
        {
            CompleteStage();
        }

        error = string.Empty;
        return true;
    }

    public LastToDieRunSnapshot CreateSnapshot()
    {
        var players = _players.Values
            .OrderBy(player => player.PlayerId)
            .Select(player => new LastToDiePlayerSnapshot(
                player.PlayerId,
                player.SurvivorId,
                Array.AsReadOnly(player.OwnedPerks.ToArray()),
                player.ActiveOffer,
                player.IsReady,
                player.IsAlive,
                player.Kills,
                player.ConquistadorStacks))
            .ToArray();

        return new LastToDieRunSnapshot(
            RunId,
            _structuralRevision,
            Seed,
            _ruleset.Version,
            Difficulty,
            _phase,
            _stageNumber,
            _stageInstanceId,
            _currentMap,
            _enemyCount,
            _stageEndServerTick,
            _runEndServerTick,
            new ReadOnlyCollection<LastToDiePlayerSnapshot>(players),
            _terminalReason);
    }

    private void BeginRewardChoice(int nextStageNumber)
    {
        _phase = LastToDiePhase.RewardChoice;
        var createdOffer = false;
        foreach (var player in _players.Values.OrderBy(player => player.PlayerId))
        {
            player.IsReady = false;
            player.DraftOrdinal += 1;
            var choices = CreateOfferChoices(player);
            if (choices.Count == 0)
            {
                player.ActiveOffer = null;
                player.IsReady = true;
                continue;
            }

            player.ActiveOffer = new LastToDieRewardOffer(
                _nextOfferId++,
                player.DraftOrdinal,
                choices);
            createdOffer = true;
        }

        if (!createdOffer)
        {
            PrepareStage(nextStageNumber);
        }
    }

    private void RestartWithCurrentSurvivors()
    {
        _stageNumber = 0;
        _currentMap = string.Empty;
        _enemyCount = 0;
        _stageEndServerTick = 0;
        _runEndServerTick = 0;
        _terminalReason = string.Empty;
        foreach (var player in _players.Values)
        {
            // SurvivorId deliberately survives a retry. Offer IDs and draft
            // ordinals stay monotonic within the run so stale commands from the
            // failed attempt can never select a new reward by coincidence.
            player.OwnedPerks.Clear();
            player.OwnedPerkSet.Clear();
            player.ActiveOffer = null;
            player.IsReady = false;
            player.IsAlive = true;
            player.Kills = 0;
            player.ConquistadorStacks = 0;
        }

        BeginRewardChoice(nextStageNumber: 1);
    }

    private IReadOnlyList<LastToDiePerkId> CreateOfferChoices(PlayerState player)
    {
        var eligible = _perks.GetEligible(player.SurvivorId!.Value, player.OwnedPerkSet)
            .Select(definition => definition.Id)
            .ToList();
        if (eligible.Count == 0)
        {
            return Array.Empty<LastToDiePerkId>();
        }

        Span<byte> playerBytes = stackalloc byte[16];
        player.PlayerId.TryWriteBytes(playerBytes);
        var playerKey = BinaryPrimitives.ReadUInt64LittleEndian(playerBytes)
            ^ BinaryPrimitives.ReadUInt64LittleEndian(playerBytes[8..]);
        var streamKey = playerKey ^ ((ulong)player.DraftOrdinal << 32);
        var random = new LastToDieRandom(
            LastToDieRandom.DeriveSeed(Seed, streamKey),
            LastToDieRandom.DeriveSeed(streamKey, 0x4F46464552UL));
        random.Shuffle(eligible);

        var choiceCount = Math.Min(_ruleset.RewardChoiceCount, eligible.Count);
        return Array.AsReadOnly(eligible.Take(choiceCount).ToArray());
    }

    private void PrepareStage(int stageNumber)
    {
        var stage = _ruleset.GetStage(stageNumber);
        _stageNumber = stage.StageNumber;
        _stageInstanceId = checked(_stageInstanceId + 1);
        _enemyCount = stage.EnemyCount;
        _currentMap = SelectNextMap();
        _stageEndServerTick = 0;
        _phase = LastToDiePhase.LoadingStage;
        foreach (var player in _players.Values)
        {
            player.IsReady = false;
            player.IsAlive = true;
        }
    }

    private string SelectNextMap()
    {
        if (_mapRotation.Count == 1)
        {
            return _mapRotation[0];
        }

        var candidates = _mapRotation
            .Where(map => !string.Equals(map, _currentMap, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return candidates[_mapRandom.NextInt32(candidates.Length)];
    }

    private void CompleteStage()
    {
        if (_stageNumber >= _ruleset.StageCount)
        {
            _phase = LastToDiePhase.Won;
            _terminalReason = "All Last to Die stages completed.";
        }
        else
        {
            BeginRewardChoice(_stageNumber + 1);
        }

        TouchStructure();
    }

    private void Lose(string reason)
    {
        _phase = LastToDiePhase.Lost;
        _terminalReason = reason;
        TouchStructure();
    }

    private bool TryGetPlayer(Guid playerId, out PlayerState player, out string error)
    {
        if (_players.TryGetValue(playerId, out player!))
        {
            error = string.Empty;
            return true;
        }

        return Fail("Player does not belong to this Last to Die run.", out error);
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private void TouchStructure()
    {
        _structuralRevision = checked(_structuralRevision + 1);
    }
}
