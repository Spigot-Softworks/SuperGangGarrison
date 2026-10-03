namespace OpenGarrison.Core;

internal sealed partial class SpawnSystem
{
    internal IReadOnlyList<SpawnPoint> TestGetTeamSpawnSelectionPool(PlayerTeam team)
    {
        var spawns = team == PlayerTeam.Blue ? _host.Level.BlueSpawns : _host.Level.RedSpawns;
        return BuildTeamSpawnSelectionPool(spawns, team);
    }

    internal SpawnPoint TestReserveTeamSpawn(PlayerEntity player, PlayerTeam team)
    {
        return ReserveSpawn(player, team);
    }

    internal void TestSetControlPointOwner(int controlPointIndex, PlayerTeam? team)
    {
        for (var index = 0; index < _host.Objectives.ControlPoints.Points.Count; index += 1)
        {
            if (_host.Objectives.ControlPoints.Points[index].Index == controlPointIndex)
            {
                _host.Objectives.ControlPoints.Points[index].Team = team;
                return;
            }
        }
    }
}
