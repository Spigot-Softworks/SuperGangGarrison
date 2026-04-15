using System;
using System.Collections.Generic;

namespace OpenGarrison.Protocol;

public interface ISnapshotBaselineState
{
    ulong Frame { get; }
    string LevelName { get; }
    byte MapAreaIndex { get; }
    byte MapAreaCount { get; }
    float MapScale { get; }
    IReadOnlyList<SnapshotSentryState> Sentries { get; }
    IReadOnlyList<SnapshotShotState> Shots { get; }
    IReadOnlyList<SnapshotShotState> Bubbles { get; }
    IReadOnlyList<SnapshotShotState> Blades { get; }
    IReadOnlyList<SnapshotShotState> Needles { get; }
    IReadOnlyList<SnapshotShotState> RevolverShots { get; }
    IReadOnlyList<SnapshotRocketState> Rockets { get; }
    IReadOnlyList<SnapshotFlameState> Flames { get; }
    IReadOnlyList<SnapshotShotState> Flares { get; }
    IReadOnlyList<SnapshotMineState> Mines { get; }
    IReadOnlyList<SnapshotPlayerGibState> PlayerGibs { get; }
    IReadOnlyList<SnapshotBloodDropState> BloodDrops { get; }
    IReadOnlyList<SnapshotDeadBodyState> DeadBodies { get; }
    IReadOnlyList<SnapshotSentryGibState> SentryGibs { get; }
    IReadOnlyList<SnapshotJumpPadState> JumpPads { get; }
}

public sealed record SnapshotBaselineState(
    ulong Frame,
    string LevelName,
    byte MapAreaIndex,
    byte MapAreaCount,
    float MapScale,
    IReadOnlyList<SnapshotSentryState> Sentries,
    IReadOnlyList<SnapshotShotState> Shots,
    IReadOnlyList<SnapshotShotState> Bubbles,
    IReadOnlyList<SnapshotShotState> Blades,
    IReadOnlyList<SnapshotShotState> Needles,
    IReadOnlyList<SnapshotShotState> RevolverShots,
    IReadOnlyList<SnapshotRocketState> Rockets,
    IReadOnlyList<SnapshotFlameState> Flames,
    IReadOnlyList<SnapshotShotState> Flares,
    IReadOnlyList<SnapshotMineState> Mines,
    IReadOnlyList<SnapshotPlayerGibState> PlayerGibs,
    IReadOnlyList<SnapshotBloodDropState> BloodDrops,
    IReadOnlyList<SnapshotDeadBodyState> DeadBodies,
    IReadOnlyList<SnapshotSentryGibState> SentryGibs,
    IReadOnlyList<SnapshotJumpPadState> JumpPads) : ISnapshotBaselineState
{
    public static SnapshotBaselineState FromSnapshot(SnapshotMessage snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new SnapshotBaselineState(
            snapshot.Frame,
            snapshot.LevelName,
            snapshot.MapAreaIndex,
            snapshot.MapAreaCount,
            snapshot.MapScale,
            snapshot.Sentries,
            snapshot.Shots,
            snapshot.Bubbles,
            snapshot.Blades,
            snapshot.Needles,
            snapshot.RevolverShots,
            snapshot.Rockets,
            snapshot.Flames,
            snapshot.Flares,
            snapshot.Mines,
            snapshot.PlayerGibs,
            snapshot.BloodDrops,
            snapshot.DeadBodies,
            snapshot.SentryGibs,
            snapshot.JumpPads);
    }
}
