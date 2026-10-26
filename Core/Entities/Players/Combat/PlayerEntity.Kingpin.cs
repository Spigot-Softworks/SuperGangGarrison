namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    internal HashSet<int> KingpinBashHitPlayers { get; } = [];
    internal HashSet<int> KingpinBashHitZones { get; } = [];
}
