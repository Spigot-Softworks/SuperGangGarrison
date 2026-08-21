#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    internal static bool AreCivvieMoneyParticlesEnabled(int particleMode) => particleMode != 1;

    private void PlayPendingCivvieMoneyTrailSpawns()
    {
        if (_networkClient.IsConnected)
        {
            return;
        }

        foreach (var spawn in _world.DrainPendingCivvieMoneyTrailSpawns())
        {
            SpawnCivvieMoneyVisual(spawn);
        }
    }

    private void SpawnCivviePogoTrickMoneyBurst(PlayerEntity player, ulong frame)
    {
        for (var particleIndex = 0; particleIndex < CivvieMoneyTrailRules.PogoTrickBurstParticleCount; particleIndex += 1)
        {
            var spawn = CivvieMoneyTrailRules.CreatePogoTrickBurstSpawn(
                frame,
                player.Id,
                particleIndex,
                player.X,
                player.Y);
            SpawnCivvieMoneyBurstVisual(spawn);
        }
    }
}
