using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

internal static class MLBotWaypointResolver
{
    public static MLBotWaypointSnapshot Resolve(
        SimulationWorld world,
        PlayerEntity player,
        MLBotTaskPhase taskPhase,
        in MLBotObjectiveSnapshot objective,
        MLBotObservationRuntimeState runtimeState,
        out MLBotTraversalSnapshot traversal)
    {
        _ = world;
        traversal = default;
        MLBotObservationRuntimeStateTracker.NoteTraversalSegment(runtimeState, null);

        if (!objective.HasObjective || taskPhase == MLBotTaskPhase.None)
        {
            return default;
        }

        return CreateWaypoint(player, objective.WorldX, objective.WorldY);
    }

    private static MLBotWaypointSnapshot CreateWaypoint(PlayerEntity player, float worldX, float worldY)
    {
        var relativeX = worldX - player.X;
        var relativeY = worldY - player.Y;
        var distance = MathF.Sqrt((relativeX * relativeX) + (relativeY * relativeY));
        return new MLBotWaypointSnapshot(
            HasWaypoint: true,
            WorldX: worldX,
            WorldY: worldY,
            RelativeX: relativeX,
            RelativeY: relativeY,
            Distance: distance,
            IsFinalWaypoint: true);
    }
}
