using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server.Plugins.ChatVoting;

public sealed class ChatVotingPlugin :
    IOpenGarrisonServerPlugin,
    IOpenGarrisonServerUpdateHooks,
    IOpenGarrisonServerClientHooks,
    IOpenGarrisonServerChatCommandHooks,
    IOpenGarrisonServerMapHooks
{
    private const string ConfigFileName = "chat-voting.json";
    private readonly Func<DateTimeOffset> _utcNowProvider;
    private IOpenGarrisonServerPluginContext? _context;
    private ChatVotingPluginConfig _config = new();
    private ActiveVote? _activeVote;
    private DateTimeOffset _cooldownEndsAt;

    public ChatVotingPlugin()
        : this(static () => DateTimeOffset.UtcNow)
    {
    }

    public ChatVotingPlugin(Func<DateTimeOffset> utcNowProvider)
    {
        _utcNowProvider = utcNowProvider;
    }

    public string Id => "chat.voting";

    public string DisplayName => "Chat Voting";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonServerPluginContext context)
    {
        _context = context;
        LoadConfig();
        context.Log(
            $"loaded config voteDuration={_config.VoteDurationSeconds}s cooldown={_config.CooldownSeconds}s " +
            $"minimumEligiblePlayers={_config.MinimumEligiblePlayers}");
    }

    public void Shutdown()
    {
    }

    public void OnServerHeartbeat(TimeSpan uptime)
    {
        if (_context is null)
        {
            return;
        }

        ExpireVoteIfNeeded(CreateChatContext());
    }

    public void OnHelloReceived(HelloReceivedEvent e)
    {
    }

    public void OnClientConnected(ClientConnectedEvent e)
    {
    }

    public void OnClientDisconnected(ClientDisconnectedEvent e)
    {
        if (_context is null || _activeVote is null)
        {
            return;
        }

        _activeVote.YesVotes.Remove(e.Slot);
        _activeVote.NoVotes.Remove(e.Slot);
        ResolveVoteIfPossible(CreateChatContext());
    }

    public void OnPasswordAccepted(PasswordAcceptedEvent e)
    {
    }

    public void OnPlayerTeamChanged(PlayerTeamChangedEvent e)
    {
    }

    public void OnPlayerClassChanged(PlayerClassChangedEvent e)
    {
    }

    public void OnMapChanging(MapChangingEvent e)
    {
        ClearActiveVote();
    }

    public void OnMapChanged(MapChangedEvent e)
    {
        ClearActiveVote();
    }

    public bool TryHandleChatMessage(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e)
    {
        ExpireVoteIfNeeded(context);
        if (!TryParseCommand(e.Text, out var commandName, out var arguments))
        {
            return false;
        }

        return commandName switch
        {
            "votemap" => TryStartVote(context, e, arguments, VoteKind.ChangeMapNow),
            "votenextround" => TryStartVote(context, e, arguments, VoteKind.ChangeMapNextRound),
            "vote" => TryRegisterVote(context, e, arguments),
            "yes" => TryRegisterVote(context, e, "yes"),
            "no" => TryRegisterVote(context, e, "no"),
            "votes" or "votestatus" => HandleVoteStatus(context, e.Slot),
            "cancelvote" => HandleCancelVote(context, e),
            _ => false,
        };
    }

    private void LoadConfig()
    {
        if (_context is null)
        {
            return;
        }

        var path = Path.Combine(_context.ConfigDirectory, ConfigFileName);
        _config = ChatVotingPluginConfig.Normalize(JsonConfigurationFile.LoadOrCreate(path, static () => new ChatVotingPluginConfig()));
        JsonConfigurationFile.Save(path, _config);
    }

    private OpenGarrisonServerChatMessageContext CreateChatContext()
    {
        return new OpenGarrisonServerChatMessageContext(
            _context!.ServerState,
            _context.AdminOperations);
    }

    private bool TryStartVote(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e, string arguments, VoteKind kind)
    {
        if (!TryGetEligiblePlayer(context.ServerState, e.Slot, out var initiator))
        {
            context.AdminOperations.SendSystemMessage(
                e.Slot,
                _config.AllowSpectatorsToVote
                    ? "Only authorized players can start votes."
                    : "Only authorized non-spectators can start votes.");
            return true;
        }

        if (_activeVote is not null)
        {
            context.AdminOperations.SendSystemMessage(e.Slot, $"A vote is already active: {BuildVoteSummary(context.ServerState, _activeVote)}");
            return true;
        }

        var now = _utcNowProvider();
        if (now < _cooldownEndsAt)
        {
            var secondsRemaining = Math.Max(1, (int)Math.Ceiling((_cooldownEndsAt - now).TotalSeconds));
            context.AdminOperations.SendSystemMessage(e.Slot, $"Votes are on cooldown for {secondsRemaining}s.");
            return true;
        }

        if (!TryResolveLevel(arguments, out var level, out var isUsageError))
        {
            context.AdminOperations.SendSystemMessage(
                e.Slot,
                isUsageError
                    ? kind == VoteKind.ChangeMapNow
                        ? "Usage: !votemap <mapName> [area]"
                        : "Usage: !votenextround <mapName> [area]"
                    : $"Unknown map \"{arguments.Trim()}\".");
            return true;
        }

        var eligibleCount = GetEligibleSlots(context.ServerState).Count;
        if (eligibleCount < _config.MinimumEligiblePlayers)
        {
            context.AdminOperations.SendSystemMessage(
                e.Slot,
                $"Need at least {_config.MinimumEligiblePlayers} eligible players to start a vote.");
            return true;
        }

        _activeVote = new ActiveVote(
            kind,
            level.Name,
            level.MapAreaIndex,
            level.MapAreaCount,
            initiator.Slot,
            initiator.Name,
            now.AddSeconds(_config.VoteDurationSeconds));
        _activeVote.YesVotes.Add(initiator.Slot);

        var label = FormatMapLabel(level.Name, level.MapAreaIndex, level.MapAreaCount);
        var requiredYesVotes = GetRequiredYesVotes(eligibleCount);
        var actionLabel = kind == VoteKind.ChangeMapNow ? "votemap" : "votenextround";
        ResolveVoteIfPossible(context);
        if (_activeVote is null)
        {
            return true;
        }

        context.AdminOperations.BroadcastSystemMessage(
            $"{initiator.Name} started {actionLabel} for {label}. " +
            $"Type !vote yes or !vote no. ({requiredYesVotes} yes needed, {_config.VoteDurationSeconds}s)");
        return true;
    }

    private bool TryRegisterVote(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e, string arguments)
    {
        if (_activeVote is null)
        {
            context.AdminOperations.SendSystemMessage(e.Slot, "There is no active vote.");
            return true;
        }

        if (!TryGetEligiblePlayer(context.ServerState, e.Slot, out var player))
        {
            context.AdminOperations.SendSystemMessage(
                e.Slot,
                _config.AllowSpectatorsToVote
                    ? "Only authorized players can vote."
                    : "Only authorized non-spectators can vote.");
            return true;
        }

        if (!TryParseVoteChoice(arguments, out var isYesVote))
        {
            context.AdminOperations.SendSystemMessage(e.Slot, "Usage: !vote <yes|no>");
            return true;
        }

        var targetVotes = isYesVote ? _activeVote.YesVotes : _activeVote.NoVotes;
        var oppositeVotes = isYesVote ? _activeVote.NoVotes : _activeVote.YesVotes;
        if (targetVotes.Contains(player.Slot) && !oppositeVotes.Contains(player.Slot))
        {
            context.AdminOperations.SendSystemMessage(e.Slot, $"You already voted {(isYesVote ? "yes" : "no")}.");
            return true;
        }

        oppositeVotes.Remove(player.Slot);
        targetVotes.Add(player.Slot);
        context.AdminOperations.BroadcastSystemMessage(
            $"{player.Name} voted {(isYesVote ? "yes" : "no")} " +
            $"({BuildVoteCountsLabel(context.ServerState, _activeVote)})");
        ResolveVoteIfPossible(context);
        return true;
    }

    private bool HandleVoteStatus(OpenGarrisonServerChatMessageContext context, byte slot)
    {
        if (_activeVote is null)
        {
            context.AdminOperations.SendSystemMessage(slot, "There is no active vote.");
            return true;
        }

        context.AdminOperations.SendSystemMessage(slot, BuildVoteSummary(context.ServerState, _activeVote));
        return true;
    }

    private bool HandleCancelVote(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e)
    {
        if (_activeVote is null)
        {
            context.AdminOperations.SendSystemMessage(e.Slot, "There is no active vote.");
            return true;
        }

        if (_activeVote.InitiatorSlot != e.Slot)
        {
            context.AdminOperations.SendSystemMessage(e.Slot, "Only the player who started the vote can cancel it.");
            return true;
        }

        context.AdminOperations.BroadcastSystemMessage(
            $"{_activeVote.InitiatorName} canceled the vote for " +
            $"{FormatMapLabel(_activeVote.LevelName, _activeVote.AreaIndex, _activeVote.AreaCount)}.");
        ClearActiveVote(startCooldown: true);
        return true;
    }

    private void ExpireVoteIfNeeded(OpenGarrisonServerChatMessageContext context)
    {
        if (_activeVote is null || _utcNowProvider() < _activeVote.ExpiresAtUtc)
        {
            return;
        }

        FailVote(context, "Vote expired");
    }

    private void ResolveVoteIfPossible(OpenGarrisonServerChatMessageContext context)
    {
        if (_activeVote is null)
        {
            return;
        }

        var eligibleSlots = GetEligibleSlots(context.ServerState);
        _activeVote.YesVotes.RemoveWhere(slot => !eligibleSlots.Contains(slot));
        _activeVote.NoVotes.RemoveWhere(slot => !eligibleSlots.Contains(slot));

        if (eligibleSlots.Count < _config.MinimumEligiblePlayers)
        {
            FailVote(context, "Vote canceled: not enough eligible players remain");
            return;
        }

        var requiredYesVotes = GetRequiredYesVotes(eligibleSlots.Count);
        if (_activeVote.YesVotes.Count >= requiredYesVotes)
        {
            PassVote(context);
            return;
        }

        var maxPossibleYesVotes = eligibleSlots.Count - _activeVote.NoVotes.Count;
        if (maxPossibleYesVotes < requiredYesVotes)
        {
            FailVote(context, "Vote failed");
        }
    }

    private void PassVote(OpenGarrisonServerChatMessageContext context)
    {
        if (_activeVote is null)
        {
            return;
        }

        var voteKind = _activeVote.Kind;
        var levelName = _activeVote.LevelName;
        var areaIndex = _activeVote.AreaIndex;
        var label = FormatMapLabel(levelName, areaIndex, _activeVote.AreaCount);
        var success = voteKind == VoteKind.ChangeMapNow
            ? context.AdminOperations.TryChangeMap(levelName, areaIndex, preservePlayerStats: false)
            : context.AdminOperations.TrySetNextRoundMap(levelName, areaIndex);

        if (success)
        {
            context.AdminOperations.BroadcastSystemMessage(
                voteKind == VoteKind.ChangeMapNow
                    ? $"Vote passed for {label}. Changing map now."
                    : $"Vote passed for {label}. It will be played next round.");
        }
        else
        {
            context.AdminOperations.BroadcastSystemMessage($"Vote passed for {label}, but the action could not be applied.");
        }

        ClearActiveVote(startCooldown: true);
    }

    private void FailVote(OpenGarrisonServerChatMessageContext context, string reason)
    {
        if (_activeVote is null)
        {
            return;
        }

        context.AdminOperations.BroadcastSystemMessage(
            $"{reason} for {FormatMapLabel(_activeVote.LevelName, _activeVote.AreaIndex, _activeVote.AreaCount)} " +
            $"({BuildVoteCountsLabel(context.ServerState, _activeVote)}).");
        ClearActiveVote(startCooldown: true);
    }

    private void ClearActiveVote(bool startCooldown = false)
    {
        _activeVote = null;
        if (startCooldown)
        {
            _cooldownEndsAt = _utcNowProvider().AddSeconds(_config.CooldownSeconds);
        }
    }

    private HashSet<byte> GetEligibleSlots(IOpenGarrisonServerReadOnlyState serverState)
    {
        return serverState.GetPlayers()
            .Where(player =>
                player.IsAuthorized
                && (_config.AllowSpectatorsToVote || !player.IsSpectator))
            .Select(player => player.Slot)
            .ToHashSet();
    }

    private bool TryGetEligiblePlayer(IOpenGarrisonServerReadOnlyState serverState, byte slot, out OpenGarrisonServerPlayerInfo player)
    {
        foreach (var entry in serverState.GetPlayers())
        {
            if (entry.Slot != slot
                || !entry.IsAuthorized
                || (!_config.AllowSpectatorsToVote && entry.IsSpectator))
            {
                continue;
            }

            player = entry;
            return true;
        }

        player = default;
        return false;
    }

    private int GetRequiredYesVotes(int eligibleCount)
    {
        return Math.Max(1, (eligibleCount / 2) + 1);
    }

    private static bool TryParseCommand(string text, out string commandName, out string arguments)
    {
        commandName = string.Empty;
        arguments = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (!trimmed.StartsWith('!'))
        {
            return false;
        }

        var commandLine = trimmed[1..].Trim();
        if (commandLine.Length == 0)
        {
            return false;
        }

        var separatorIndex = commandLine.IndexOf(' ');
        commandName = (separatorIndex < 0 ? commandLine : commandLine[..separatorIndex]).Trim().ToLowerInvariant();
        arguments = separatorIndex < 0 ? string.Empty : commandLine[(separatorIndex + 1)..].Trim();
        return commandName.Length > 0;
    }

    private static bool TryResolveLevel(string arguments, [NotNullWhen(true)] out SimpleLevel? level, out bool isUsageError)
    {
        level = null;
        var trimmed = arguments.Trim();
        if (trimmed.Length == 0)
        {
            isUsageError = true;
            return false;
        }

        level = SimpleLevelFactory.CreateImportedLevel(trimmed, mapAreaIndex: 1);
        if (level is not null)
        {
            isUsageError = false;
            return true;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1
            && int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedArea)
            && parsedArea > 0)
        {
            var levelName = string.Join(' ', parts[..^1]);
            if (levelName.Length > 0)
            {
                level = SimpleLevelFactory.CreateImportedLevel(levelName, parsedArea);
            }
        }

        isUsageError = false;
        return level is not null;
    }

    private static bool TryParseVoteChoice(string arguments, out bool isYesVote)
    {
        var normalized = arguments.Trim();
        if (normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) || normalized.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            isYesVote = true;
            return true;
        }

        if (normalized.Equals("no", StringComparison.OrdinalIgnoreCase) || normalized.Equals("n", StringComparison.OrdinalIgnoreCase))
        {
            isYesVote = false;
            return true;
        }

        isYesVote = false;
        return false;
    }

    private string BuildVoteCountsLabel(IOpenGarrisonServerReadOnlyState serverState, ActiveVote vote)
    {
        var eligibleCount = GetEligibleSlots(serverState).Count;
        return $"{vote.YesVotes.Count}/{eligibleCount} yes, {vote.NoVotes.Count} no";
    }

    private string BuildVoteSummary(IOpenGarrisonServerReadOnlyState serverState, ActiveVote vote)
    {
        var now = _utcNowProvider();
        var secondsRemaining = Math.Max(0, (int)Math.Ceiling((vote.ExpiresAtUtc - now).TotalSeconds));
        var actionLabel = vote.Kind == VoteKind.ChangeMapNow ? "votemap" : "votenextround";
        return $"{actionLabel} for {FormatMapLabel(vote.LevelName, vote.AreaIndex, vote.AreaCount)} " +
               $"by {vote.InitiatorName} ({BuildVoteCountsLabel(serverState, vote)}, {secondsRemaining}s left)";
    }

    private static string FormatMapLabel(string levelName, int areaIndex, int areaCount)
    {
        return areaCount > 1
            ? $"{levelName} area {areaIndex}/{areaCount}"
            : levelName;
    }

    private enum VoteKind : byte
    {
        ChangeMapNow = 1,
        ChangeMapNextRound = 2,
    }

    private sealed class ActiveVote
    {
        public ActiveVote(
            VoteKind kind,
            string levelName,
            int areaIndex,
            int areaCount,
            byte initiatorSlot,
            string initiatorName,
            DateTimeOffset expiresAtUtc)
        {
            Kind = kind;
            LevelName = levelName;
            AreaIndex = areaIndex;
            AreaCount = areaCount;
            InitiatorSlot = initiatorSlot;
            InitiatorName = initiatorName;
            ExpiresAtUtc = expiresAtUtc;
        }

        public VoteKind Kind { get; }

        public string LevelName { get; }

        public int AreaIndex { get; }

        public int AreaCount { get; }

        public byte InitiatorSlot { get; }

        public string InitiatorName { get; }

        public DateTimeOffset ExpiresAtUtc { get; }

        public HashSet<byte> YesVotes { get; } = [];

        public HashSet<byte> NoVotes { get; } = [];
    }
}

public sealed class ChatVotingPluginConfig
{
    public int VoteDurationSeconds { get; set; } = 30;

    public int CooldownSeconds { get; set; } = 20;

    public int MinimumEligiblePlayers { get; set; } = 1;

    public bool AllowSpectatorsToVote { get; set; }

    public static ChatVotingPluginConfig Normalize(ChatVotingPluginConfig config)
    {
        return new ChatVotingPluginConfig
        {
            VoteDurationSeconds = Math.Clamp(config.VoteDurationSeconds, 10, 300),
            CooldownSeconds = Math.Clamp(config.CooldownSeconds, 0, 600),
            MinimumEligiblePlayers = Math.Clamp(config.MinimumEligiblePlayers, 1, SimulationWorld.MaxPlayableNetworkPlayers),
            AllowSpectatorsToVote = config.AllowSpectatorsToVote,
        };
    }
}
