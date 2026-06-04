namespace OpenGarrison.Core;

public readonly record struct SpawnPoint(
    float X,
    float Y,
    SpawnPointRole Role = SpawnPointRole.Standard,
    int LinkedControlPointIndex = 0,
    ForwardSpawnUseCondition UseCondition = ForwardSpawnUseCondition.ObjectiveOwnedByTeam,
    int Priority = ForwardSpawnPriorityMetadata.MinPriority,
    int LogicSignalNodeIndex = -1)
{
    public bool IsForwardSpawn => Role == SpawnPointRole.Forward;

    public bool IsStandardSpawn => Role == SpawnPointRole.Standard;

    public bool UsesLogicSignal => LogicSignalNodeIndex >= 0;
}
