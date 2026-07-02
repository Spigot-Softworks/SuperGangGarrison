using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed class MapBotSpawnController(SimulationWorld world, ServerBotManager botManager)
{
    private SimpleLevel? _level;
    private bool[] _previousTriggerStates = [];

    public void Tick()
    {
        var botSpawns = world.Level.BotSpawns;
        if (botSpawns.Count == 0)
        {
            ResetIfLevelChanged();
            return;
        }

        EnsureState(botSpawns.Count);
        var graph = world.Level.LogicGraph;
        for (var index = 0; index < botSpawns.Count; index += 1)
        {
            var marker = botSpawns[index];
            var current = marker.UsesTrigger && graph.GetOutput(marker.TriggerNodeIndex);
            if (current && !_previousTriggerStates[index])
            {
                botManager.TrySpawnMapBot(
                    marker.Team,
                    marker.ClassId,
                    marker.Kind,
                    marker.Respawn,
                    marker.RespawnMode,
                    marker.NameMode,
                    marker.Name,
                    marker.ForceNameplate,
                    marker.ForceHealthBar,
                    marker.X,
                    marker.Y,
                    out _,
                    marker.DeathTriggerNodeIndex);
            }

            _previousTriggerStates[index] = current;
        }
    }

    public void Reset()
    {
        _level = world.Level;
        var botSpawns = world.Level.BotSpawns;
        _previousTriggerStates = botSpawns.Count == 0
            ? []
            : new bool[botSpawns.Count];
    }

    private void ResetIfLevelChanged()
    {
        if (!ReferenceEquals(_level, world.Level))
        {
            Reset();
        }
    }

    private void EnsureState(int count)
    {
        if (ReferenceEquals(_level, world.Level) && _previousTriggerStates.Length == count)
        {
            return;
        }

        Reset();
    }
}
