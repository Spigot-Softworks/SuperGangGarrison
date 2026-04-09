using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal readonly record struct SnapshotBroadcastMetrics(
    bool HasMeasurements,
    ulong Frame,
    int ClientCount,
    double SharedCaptureMilliseconds,
    double PerClientMilliseconds,
    double TotalMilliseconds,
    long SharedCaptureAllocatedBytes,
    long PerClientAllocatedBytes,
    long TotalAllocatedBytes,
    int AverageFullPayloadBytes,
    int AverageSentPayloadBytes,
    double AverageSerializePasses,
    int BudgetedClientCount,
    int BaselineHitCount,
    int BaselineMissCount,
    double AverageSnapshotHistoryCount,
    int MaxSnapshotHistoryCount);

internal readonly record struct SnapshotBudgetBuildResult(
    SnapshotMessage Message,
    byte[] Payload,
    int SerializePassCount);
