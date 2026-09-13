#nullable enable

using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed record VoteParticipant(string Identity, byte Slot, string DisplayName);

internal sealed record VoteDefinition(
    ServerVoteKind Kind,
    string Subject,
    string InitiatorIdentity,
    string InitiatorName,
    Func<bool> Apply,
    string OwnerPluginId = "");

internal sealed class VoteCoordinator
{
    private sealed class ActiveVote(VoteDefinition definition, ulong voteId, long startFrame, long expiresFrame)
    {
        public VoteDefinition Definition { get; } = definition;
        public ulong VoteId { get; } = voteId;
        public long StartFrame { get; } = startFrame;
        public long ExpiresFrame { get; } = expiresFrame;
        public Dictionary<string, bool> Ballots { get; } = new(StringComparer.Ordinal);
        public int LastPublishedEligibleCount { get; set; }
    }

    private readonly Func<long> _frameGetter;
    private readonly Func<IReadOnlyList<VoteParticipant>> _participantsGetter;
    private readonly Action<VoteStateMessage> _publish;
    private readonly int _durationTicks;
    private readonly int _cooldownTicks;
    private readonly int _minimumEligiblePlayers;
    private readonly int _ticksPerSecond;
    private ActiveVote? _active;
    private long _cooldownEndsFrame;
    private ulong _nextVoteId;
    private uint _revision;
    private bool _isApplyingAction;

    public VoteCoordinator(
        Func<long> frameGetter,
        Func<IReadOnlyList<VoteParticipant>> participantsGetter,
        Action<VoteStateMessage> publish,
        int ticksPerSecond,
        int durationTicks,
        int cooldownTicks,
        int minimumEligiblePlayers = 1)
    {
        _frameGetter = frameGetter;
        _participantsGetter = participantsGetter;
        _publish = publish;
        _ticksPerSecond = Math.Max(1, ticksPerSecond);
        _durationTicks = Math.Max(1, durationTicks);
        _cooldownTicks = Math.Max(0, cooldownTicks);
        _minimumEligiblePlayers = Math.Max(1, minimumEligiblePlayers);
    }

    public bool HasActiveVote => _active is not null;

    public ulong ActiveVoteId => _active?.VoteId ?? 0;

    public int CooldownTicksRemaining => (int)Math.Clamp(
        _cooldownEndsFrame - _frameGetter(),
        0,
        int.MaxValue);

    public bool CanStart(string initiatorIdentity, out string error)
    {
        Tick();
        if (_active is not null)
        {
            error = $"A vote is already active: {BuildSummary(_active)}";
            return false;
        }

        var cooldown = CooldownTicksRemaining;
        if (cooldown > 0)
        {
            error = $"Votes are on cooldown for {FormatSeconds(cooldown)}s.";
            return false;
        }

        var participants = GetDistinctParticipants();
        if (participants.Length < _minimumEligiblePlayers)
        {
            error = $"Need at least {_minimumEligiblePlayers} eligible players to start a vote.";
            return false;
        }

        if (!participants.Any(participant => participant.Identity == initiatorIdentity))
        {
            error = "You must be an active player to start a vote.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryStart(VoteDefinition definition, out string error)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!CanStart(definition.InitiatorIdentity, out error))
        {
            return false;
        }

        var participants = GetDistinctParticipants();
        var frame = _frameGetter();
        var active = new ActiveVote(
            definition,
            ++_nextVoteId,
            frame,
            checked(frame + _durationTicks));
        active.Ballots[definition.InitiatorIdentity] = true;
        active.LastPublishedEligibleCount = participants.Length;
        _active = active;
        Publish(active, ServerVoteEventKind.Started, definition.InitiatorName,
            $"{definition.InitiatorName} started a vote for {definition.Subject}.");
        ResolveIfPossible();
        error = string.Empty;
        return true;
    }

    public bool TryCast(string identity, string actorName, bool yes, out string error, ulong expectedVoteId = 0)
    {
        Tick();
        var active = _active;
        if (active is null)
        {
            error = "There is no active vote.";
            return false;
        }

        if (expectedVoteId != 0 && active.VoteId != expectedVoteId)
        {
            error = "That vote is no longer active.";
            return false;
        }

        var participants = GetDistinctParticipants();
        if (!participants.Any(participant => participant.Identity == identity))
        {
            error = "You must be an active player to vote.";
            return false;
        }

        if (active.Ballots.TryGetValue(identity, out var previous) && previous == yes)
        {
            error = $"You already voted {(yes ? "yes" : "no")}.";
            return false;
        }

        active.Ballots[identity] = yes;
        Publish(active, yes ? ServerVoteEventKind.Yes : ServerVoteEventKind.No, actorName,
            $"{actorName} voted {(yes ? "yes" : "no")}.");
        ResolveIfPossible();
        error = string.Empty;
        return true;
    }

    public bool TryCancel(string identity, string actorName, bool force, out string error, ulong expectedVoteId = 0)
    {
        Tick();
        var active = _active;
        if (active is null)
        {
            error = "There is no active vote.";
            return false;
        }

        if (expectedVoteId != 0 && active.VoteId != expectedVoteId)
        {
            error = "That vote is no longer active.";
            return false;
        }

        if (!force && active.Definition.InitiatorIdentity != identity)
        {
            error = "Only the player who started the vote can cancel it.";
            return false;
        }

        Complete(active, ServerVoteEventKind.Canceled, actorName, $"{actorName} canceled the vote.");
        error = string.Empty;
        return true;
    }

    public VoteStateMessage? CreateSnapshot()
    {
        Tick();
        return _active is { } active
            ? CreateMessage(active, ServerVoteEventKind.Snapshot, string.Empty, BuildSummary(active))
            : null;
    }

    public void Tick()
    {
        var active = _active;
        if (active is null)
        {
            return;
        }

        var participants = GetDistinctParticipants();
        var eligible = participants.Select(static participant => participant.Identity).ToHashSet(StringComparer.Ordinal);
        var ballotCountBeforePrune = active.Ballots.Count;
        foreach (var staleIdentity in active.Ballots.Keys.Where(identity => !eligible.Contains(identity)).ToArray())
        {
            active.Ballots.Remove(staleIdentity);
        }

        if (participants.Length < _minimumEligiblePlayers)
        {
            Complete(active, ServerVoteEventKind.Canceled, string.Empty, "Vote canceled: not enough eligible players remain.");
            return;
        }

        if (_frameGetter() >= active.ExpiresFrame)
        {
            Complete(active, ServerVoteEventKind.Expired, string.Empty, "Vote expired.");
            return;
        }

        var eligibilityChanged = active.LastPublishedEligibleCount != participants.Length
            || ballotCountBeforePrune != active.Ballots.Count;
        active.LastPublishedEligibleCount = participants.Length;
        ResolveIfPossible();
        if (eligibilityChanged && ReferenceEquals(_active, active))
        {
            Publish(active, ServerVoteEventKind.Snapshot, string.Empty, BuildSummary(active));
        }
    }

    public void CancelForMapTransition()
    {
        if (_isApplyingAction)
        {
            return;
        }

        if (_active is { } active)
        {
            Complete(active, ServerVoteEventKind.Canceled, string.Empty, "Vote canceled by map change.", beginCooldown: false);
        }

        _cooldownEndsFrame = 0;
    }

    public void CancelOwnedVote(string ownerPluginId)
    {
        if (_isApplyingAction
            || _active is not { } active
            || string.IsNullOrWhiteSpace(ownerPluginId)
            || !string.Equals(
                active.Definition.OwnerPluginId,
                ownerPluginId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Complete(
            active,
            ServerVoteEventKind.Canceled,
            string.Empty,
            $"Vote canceled because plugin {ownerPluginId} was unloaded.",
            beginCooldown: false);
    }

    private void ResolveIfPossible()
    {
        var active = _active;
        if (active is null)
        {
            return;
        }

        var eligibleCount = GetDistinctParticipants().Length;
        var required = RequiredVotes(eligibleCount);
        var yesCount = active.Ballots.Count(static ballot => ballot.Value);
        var noCount = active.Ballots.Count(static ballot => !ballot.Value);
        if (yesCount >= required)
        {
            bool applied;
            try
            {
                _isApplyingAction = true;
                applied = active.Definition.Apply();
            }
            catch
            {
                applied = false;
            }
            finally
            {
                _isApplyingAction = false;
            }

            Complete(
                active,
                applied ? ServerVoteEventKind.Passed : ServerVoteEventKind.ActionFailed,
                string.Empty,
                applied ? $"Vote passed for {active.Definition.Subject}." : "Vote passed, but the action could not be applied.");
            return;
        }

        if (noCount >= required || yesCount + Math.Max(0, eligibleCount - yesCount - noCount) < required)
        {
            Complete(active, ServerVoteEventKind.Failed, string.Empty, "Vote failed.");
        }
    }

    private void Complete(
        ActiveVote active,
        ServerVoteEventKind eventKind,
        string actorName,
        string message,
        bool beginCooldown = true)
    {
        if (!ReferenceEquals(_active, active))
        {
            return;
        }

        Publish(active, eventKind, actorName, message);
        _active = null;
        _cooldownEndsFrame = beginCooldown ? checked(_frameGetter() + _cooldownTicks) : 0;
    }

    private void Publish(ActiveVote active, ServerVoteEventKind eventKind, string actorName, string message)
    {
        _publish(CreateMessage(active, eventKind, actorName, message));
    }

    private VoteStateMessage CreateMessage(
        ActiveVote active,
        ServerVoteEventKind eventKind,
        string actorName,
        string message)
    {
        var eligibleCount = GetDistinctParticipants().Length;
        return new VoteStateMessage(
            active.VoteId,
            ++_revision,
            eventKind,
            active.Definition.Kind,
            active.Definition.Subject,
            active.Definition.InitiatorName,
            actorName,
            active.Ballots.Count(static ballot => ballot.Value),
            active.Ballots.Count(static ballot => !ballot.Value),
            RequiredVotes(eligibleCount),
            eligibleCount,
            (int)Math.Clamp(active.ExpiresFrame - _frameGetter(), 0, int.MaxValue),
            message);
    }

    private VoteParticipant[] GetDistinctParticipants()
    {
        return _participantsGetter()
            .Where(static participant => !string.IsNullOrWhiteSpace(participant.Identity))
            .GroupBy(static participant => participant.Identity, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private string BuildSummary(ActiveVote active)
    {
        var eligible = GetDistinctParticipants().Length;
        var required = RequiredVotes(eligible);
        var yes = active.Ballots.Count(static ballot => ballot.Value);
        var no = active.Ballots.Count(static ballot => !ballot.Value);
        var remaining = (int)Math.Clamp(active.ExpiresFrame - _frameGetter(), 0, int.MaxValue);
        return $"{active.Definition.Subject}: yes {yes}/{required}, no {no}/{required}, {FormatSeconds(remaining)}s remaining.";
    }

    private int FormatSeconds(int ticks)
        => Math.Max(1, (int)Math.Ceiling(ticks / (double)_ticksPerSecond));

    private static int RequiredVotes(int eligibleCount) => Math.Max(1, (eligibleCount / 2) + 1);
}
