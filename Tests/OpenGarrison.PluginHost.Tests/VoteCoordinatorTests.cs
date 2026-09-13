#nullable enable

using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class VoteCoordinatorTests
{
    [Fact]
    public void StrictMajorityPassesAndAppliesActionExactlyOnce()
    {
        long frame = 10;
        var participants = CreateParticipants(3);
        var publications = new List<VoteStateMessage>();
        var applyCount = 0;
        var coordinator = CreateCoordinator(() => frame, participants, publications);

        Assert.True(coordinator.TryStart(
            Definition(participants[0], () => { applyCount += 1; return true; }),
            out var startError), startError);
        Assert.True(coordinator.HasActiveVote);
        Assert.True(coordinator.TryCast(participants[1].Identity, participants[1].DisplayName, true, out var castError), castError);

        Assert.False(coordinator.HasActiveVote);
        Assert.Equal(1, applyCount);
        Assert.Equal(ServerVoteEventKind.Passed, publications[^1].Event);
        coordinator.Tick();
        Assert.Equal(1, applyCount);
    }

    [Fact]
    public void BallotCanChangeButIdenticalDuplicateIsRejected()
    {
        long frame = 0;
        var participants = CreateParticipants(4);
        var publications = new List<VoteStateMessage>();
        var coordinator = CreateCoordinator(() => frame, participants, publications);
        Assert.True(coordinator.TryStart(Definition(participants[0], () => true), out _));

        Assert.True(coordinator.TryCast(participants[1].Identity, participants[1].DisplayName, false, out _));
        Assert.False(coordinator.TryCast(participants[1].Identity, participants[1].DisplayName, false, out var duplicateError));
        Assert.Contains("already voted", duplicateError, StringComparison.OrdinalIgnoreCase);
        Assert.True(coordinator.TryCast(participants[1].Identity, participants[1].DisplayName, true, out _));

        var current = Assert.IsType<VoteStateMessage>(coordinator.CreateSnapshot());
        Assert.Equal(2, current.YesVotes);
        Assert.Equal(0, current.NoVotes);
        Assert.Equal(3, current.RequiredYesVotes);
    }

    [Fact]
    public void DisconnectPrunesBallotsAndRecalculatesMajority()
    {
        long frame = 0;
        var participants = CreateParticipants(4);
        var publications = new List<VoteStateMessage>();
        var applyCount = 0;
        var coordinator = CreateCoordinator(() => frame, participants, publications);
        Assert.True(coordinator.TryStart(
            Definition(participants[0], () => { applyCount += 1; return true; }),
            out _));
        Assert.True(coordinator.TryCast(participants[1].Identity, participants[1].DisplayName, true, out _));
        Assert.True(coordinator.HasActiveVote);

        participants.RemoveAt(3);
        coordinator.Tick();

        Assert.False(coordinator.HasActiveVote);
        Assert.Equal(1, applyCount);
        Assert.Equal(2, publications[^1].RequiredYesVotes);
    }

    [Fact]
    public void TimeoutFailsExplicitlyAndCooldownBlocksImmediateRestart()
    {
        long frame = 0;
        var participants = CreateParticipants(2);
        var publications = new List<VoteStateMessage>();
        var coordinator = CreateCoordinator(() => frame, participants, publications, durationTicks: 30, cooldownTicks: 60);
        Assert.True(coordinator.TryStart(Definition(participants[0], () => true), out _));

        frame = 30;
        coordinator.Tick();
        Assert.False(coordinator.HasActiveVote);
        Assert.Equal(ServerVoteEventKind.Expired, publications[^1].Event);
        Assert.False(coordinator.TryStart(Definition(participants[0], () => true), out var cooldownError));
        Assert.Contains("cooldown", cooldownError, StringComparison.OrdinalIgnoreCase);

        frame = 90;
        Assert.True(coordinator.TryStart(Definition(participants[0], () => true), out var restartError), restartError);
    }

    [Fact]
    public void FailedActionPublishesActionFailedAndEntersCooldown()
    {
        long frame = 0;
        var participants = CreateParticipants(1);
        var publications = new List<VoteStateMessage>();
        var coordinator = CreateCoordinator(() => frame, participants, publications);

        Assert.True(coordinator.TryStart(Definition(participants[0], () => false), out var error), error);

        Assert.False(coordinator.HasActiveVote);
        Assert.Equal(ServerVoteEventKind.ActionFailed, publications[^1].Event);
        Assert.True(coordinator.CooldownTicksRemaining > 0);
    }

    [Fact]
    public void EligibilityChangesPublishAReplacementSnapshot()
    {
        long frame = 0;
        var participants = CreateParticipants(4);
        var publications = new List<VoteStateMessage>();
        var coordinator = CreateCoordinator(() => frame, participants, publications);
        Assert.True(coordinator.TryStart(Definition(participants[0], () => true), out _));
        var previousRevision = publications[^1].Revision;

        participants.Add(new VoteParticipant("client-5", 5, "Player 5"));
        frame += 1;
        coordinator.Tick();

        Assert.Equal(ServerVoteEventKind.Snapshot, publications[^1].Event);
        Assert.True(publications[^1].Revision > previousRevision);
        Assert.Equal(5, publications[^1].EligibleVoters);
        Assert.Equal(3, publications[^1].RequiredYesVotes);
    }

    [Fact]
    public void StaleTypedBallotCannotApplyToANewerVote()
    {
        long frame = 0;
        var participants = CreateParticipants(3);
        var publications = new List<VoteStateMessage>();
        var coordinator = CreateCoordinator(() => frame, participants, publications, cooldownTicks: 0);
        Assert.True(coordinator.TryStart(Definition(participants[0], () => true), out _));
        var firstVoteId = coordinator.ActiveVoteId;
        Assert.True(coordinator.TryCancel(participants[0].Identity, participants[0].DisplayName, force: false, out _));
        Assert.True(coordinator.TryStart(Definition(participants[0], () => true), out _));

        Assert.False(coordinator.TryCast(
            participants[1].Identity,
            participants[1].DisplayName,
            true,
            out var staleError,
            firstVoteId));

        Assert.Contains("no longer active", staleError, StringComparison.OrdinalIgnoreCase);
        Assert.True(coordinator.HasActiveVote);
        Assert.Equal(1, Assert.IsType<VoteStateMessage>(coordinator.CreateSnapshot()).YesVotes);
    }

    [Fact]
    public void PluginOwnerUnloadCancelsWithoutCooldownOrApplyingAction()
    {
        long frame = 0;
        var participants = CreateParticipants(3);
        var publications = new List<VoteStateMessage>();
        var applyCount = 0;
        var coordinator = CreateCoordinator(() => frame, participants, publications);
        Assert.True(coordinator.TryStart(
            Definition(participants[0], () => { applyCount += 1; return true; }) with
            {
                Kind = ServerVoteKind.PluginDefined,
                OwnerPluginId = "plugin.owner",
            },
            out _));

        coordinator.CancelOwnedVote("different.owner");
        Assert.True(coordinator.HasActiveVote);
        coordinator.CancelOwnedVote("plugin.owner");

        Assert.False(coordinator.HasActiveVote);
        Assert.Equal(0, applyCount);
        Assert.Equal(ServerVoteEventKind.Canceled, publications[^1].Event);
        Assert.Equal(0, coordinator.CooldownTicksRemaining);
    }

    [Fact]
    public void OwnerCleanupDuringPassedActionCannotReplacePassedResult()
    {
        long frame = 0;
        var participants = CreateParticipants(1);
        var publications = new List<VoteStateMessage>();
        VoteCoordinator? coordinator = null;
        coordinator = CreateCoordinator(() => frame, participants, publications);

        Assert.True(coordinator.TryStart(
            Definition(participants[0], () =>
            {
                coordinator!.CancelOwnedVote("plugin.owner");
                return true;
            }) with
            {
                Kind = ServerVoteKind.PluginDefined,
                OwnerPluginId = "plugin.owner",
            },
            out var error), error);

        Assert.False(coordinator.HasActiveVote);
        Assert.Equal(ServerVoteEventKind.Passed, publications[^1].Event);
    }

    private static VoteCoordinator CreateCoordinator(
        Func<long> frameGetter,
        List<VoteParticipant> participants,
        List<VoteStateMessage> publications,
        int durationTicks = 300,
        int cooldownTicks = 60)
        => new(
            frameGetter,
            () => participants,
            publications.Add,
            ticksPerSecond: 30,
            durationTicks,
            cooldownTicks);

    private static VoteDefinition Definition(VoteParticipant initiator, Func<bool> apply)
        => new(
            ServerVoteKind.ChangeMapNow,
            "change map now: ctf_truefort",
            initiator.Identity,
            initiator.DisplayName,
            apply);

    private static List<VoteParticipant> CreateParticipants(int count)
        => Enumerable.Range(1, count)
            .Select(index => new VoteParticipant($"client-{index}", (byte)index, $"Player {index}"))
            .ToList();
}
