#nullable enable

using OpenGarrison.Core;
using OpenGarrison.Protocol;
using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class NetworkPresentationState
{
    public float NetworkSnapshotInterpolationDurationSeconds = 1f / SimulationConfig.DefaultTicksPerSecond;
    public float SmoothedSnapshotIntervalSeconds = 1f / SimulationConfig.DefaultTicksPerSecond;
    public float SmoothedSnapshotJitterSeconds;
    public float LocalPlayerInterpolationBackTimeSeconds = LocalPlayerMinimumInterpolationBackTimeSeconds;
    public float RemotePlayerInterpolationBackTimeSeconds = RemotePlayerMinimumInterpolationBackTimeSeconds;
    public float ProjectileInterpolationBackTimeSeconds = ProjectileMinimumInterpolationBackTimeSeconds;
    public double LocalPlayerRenderTimeSeconds;
    public double RemotePlayerRenderTimeSeconds;
    public double LastLocalPlayerRenderTimeClockSeconds = -1d;
    public double LastRemotePlayerRenderTimeClockSeconds = -1d;
    public double LastSnapshotReceivedTimeSeconds = -1d;
    public double LatestSnapshotServerTimeSeconds = -1d;
    public double LatestSnapshotReceivedClockSeconds = -1d;
    public bool HasReceivedSnapshot;
    public bool HasLocalPlayerRenderTime;
    public bool HasRemotePlayerRenderTime;
    public ulong LastAppliedSnapshotFrame;
    public ulong LastBufferedSnapshotFrame;
    public int? LastAppliedSnapshotLocalPlayerId;
    public int NetworkInterpolationWarmupSnapshotsRemaining;
    public double NetworkInterpolationWarmupUntilClockSeconds = -1d;
    public bool NetworkWorldWarmupActive;
    public bool NetworkWorldWarmupFullSnapshotApplied;
    public int NetworkWorldWarmupAppliedSnapshotsAfterFull;
    public bool NetworkWorldWarmupAcceptNextAppliedSnapshotAsBaseline;
    public LastToDieWirePhase? NetworkPresentationObservedLastToDiePhase;
}
