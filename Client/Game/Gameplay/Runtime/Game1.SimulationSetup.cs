#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ReinitializeSimulationForTickRate(int tickRate)
    {
        var normalizedTickRate = SimulationConfig.NormalizeTicksPerSecond(tickRate);
        if (_world is not null && _config.TicksPerSecond == normalizedTickRate)
        {
            return;
        }

        var localPlayerName = _world is null
            ? _clientSettings.PlayerName
            : _world.LocalPlayer.DisplayName;
        _config = new SimulationConfig
        {
            TicksPerSecond = normalizedTickRate,
        };
        _world = new SimulationWorld(_config);
        _simulator = new FixedStepSimulator(_world);
        _world.SetLocalPlayerName(localPlayerName);
        _observedGameplayLevelName = string.Empty;
        _observedGameplayMapAreaIndex = -1;
    }
}
