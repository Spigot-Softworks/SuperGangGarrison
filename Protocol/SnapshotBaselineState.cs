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
    IReadOnlyList<SnapshotPlayerState> Players { get; }
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
    IReadOnlyList<SnapshotGrenadeState> Grenades { get; }
    IReadOnlyList<SnapshotDeadBodyState> DeadBodies { get; }
    IReadOnlyList<SnapshotSentryGibState> SentryGibs { get; }
    IReadOnlyList<SnapshotPlayerGibState> PlayerGibs { get; }
    IReadOnlyList<SnapshotJumpPadState> JumpPads { get; }
    IReadOnlyList<SnapshotJumpPadGibState> JumpPadGibs { get; }
}

public sealed record SnapshotBaselineState(
    ulong Frame,
    string LevelName,
    byte MapAreaIndex,
    byte MapAreaCount,
    float MapScale,
    IReadOnlyList<SnapshotPlayerState> Players,
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
    IReadOnlyList<SnapshotGrenadeState> Grenades,
    IReadOnlyList<SnapshotDeadBodyState> DeadBodies,
    IReadOnlyList<SnapshotSentryGibState> SentryGibs,
    IReadOnlyList<SnapshotPlayerGibState> PlayerGibs,
    IReadOnlyList<SnapshotJumpPadState> JumpPads,
    IReadOnlyList<SnapshotJumpPadGibState> JumpPadGibs) : ISnapshotBaselineState
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
            snapshot.Players,
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
            snapshot.Grenades,
            snapshot.DeadBodies,
            snapshot.SentryGibs,
            snapshot.PlayerGibs,
            snapshot.JumpPads,
            snapshot.JumpPadGibs);
    }
}
