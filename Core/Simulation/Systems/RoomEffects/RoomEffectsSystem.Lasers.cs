namespace OpenGarrison.Core;

/// <summary>
/// Damaging lasers: placed lights of type laser with damage on. Their beams never move, so
/// their wall-clipped geometry is computed once per level (shared with clients, which draw
/// exactly these beams). Each tick a powered beam damages and ignites the players it
/// touches: everyone along it, or only the nearest when it stops at players.
/// </summary>
internal sealed partial class RoomEffectsSystem
{
    private readonly Dictionary<int, float> _laserDamageCarry = new();
    private float[] _laserStopDistances = [];
    private long _laserStopFrame = long.MinValue;

    private void ApplyLaserHazards(PlayerEntity player)
    {
        // Damage is the server's call; a predicting client would only double it up.
        if (_host.ClientPredictionMode || !player.IsAlive)
        {
            return;
        }

        var level = _host.Level;
        var lights = level.MapLights;
        if (lights.Count == 0)
        {
            return;
        }

        var beams = level.MapLightState.GetFixedBeams(lights, level.Solids);
        if (beams.Count == 0)
        {
            return;
        }

        UpdateLaserStopDistances(beams, lights);
        player.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
        var damage = 0f;
        var burn = 0;
        for (var index = 0; index < beams.Count; index += 1)
        {
            var beam = beams[index];
            var light = lights[beam.SourceIndex];
            if (light.DestructibleZoneIndex >= 0 && light.DestructibleZoneIndex < level.DamageableZoneStates.Length
                && level.DamageableZoneStates[light.DestructibleZoneIndex].IsDestroyed) continue;
            var style = light.Style;
            if (!level.MapLightState.IsPowered(beam.SourceIndex, style) || !DamagesTeam(style.DamageTeam, player.Team))
            {
                continue;
            }

            var length = MathF.Min(beam.Length, _laserStopDistances[index]);
            if (!MapLightBeamGeometry.TryHitBox(beam.X, beam.Y, beam.DirectionX, beam.DirectionY, length, beam.Width * 0.5f, left, top, right, bottom, out _))
            {
                continue;
            }

            damage += style.DamagePerSecond * (float)_host.Config.FixedDeltaSeconds;
            burn = Math.Max(burn, style.Burn);
        }

        if (damage <= 0f && burn <= 0)
        {
            _laserDamageCarry.Remove(player.Id);
            return;
        }

        _laserDamageCarry.TryGetValue(player.Id, out var carry);
        carry += damage;
        var whole = (int)carry;
        _laserDamageCarry[player.Id] = carry - whole;
        if (whole > 0)
        {
            _host.Combat.ApplyPlayerDamageWithContext(player, whole, attacker: null);
        }

        if (burn > 0 && player.IsAlive)
        {
            // Same unattributed burn as a fire zone, scaled by the laser's burn strength.
            var scale = burn / 100f;
            player.IgniteAfterburn(
                ownerPlayerId: 0,
                durationIncreaseSourceTicks: PlayerEntity.BurnDefaultMaxDurationSourceTicks * scale,
                intensityIncrease: PlayerEntity.BurnMaxIntensity * scale,
                afterburnFalloff: false,
                burnFalloffAmount: 0f);
        }
    }

    /// <summary>
    /// Once per tick: for beams that stop at players, how far each reaches before the
    /// nearest living player blocks it (otherwise its full wall-clipped length).
    /// </summary>
    private void UpdateLaserStopDistances(IReadOnlyList<MapLightFixedBeam> beams, IReadOnlyList<MapLightMarker> lights)
    {
        if (_laserStopFrame == _host.Frame && _laserStopDistances.Length == beams.Count)
        {
            return;
        }

        _laserStopFrame = _host.Frame;
        if (_laserStopDistances.Length != beams.Count)
        {
            _laserStopDistances = new float[beams.Count];
        }

        for (var index = 0; index < beams.Count; index += 1)
        {
            var beam = beams[index];
            var nearest = beam.Length;
            if (lights[beam.SourceIndex].Style.StopsAtPlayers)
            {
                foreach (var other in _host.EnumerateSimulatedPlayers())
                {
                    if (!other.IsAlive)
                    {
                        continue;
                    }

                    other.GetCollisionBounds(out var left, out var top, out var right, out var bottom);
                    if (MapLightBeamGeometry.TryHitBox(beam.X, beam.Y, beam.DirectionX, beam.DirectionY, nearest, beam.Width * 0.5f, left, top, right, bottom, out var distance))
                    {
                        // Reach just into the blocking player so they are still hit.
                        nearest = MathF.Min(nearest, distance + 1f);
                    }
                }
            }

            _laserStopDistances[index] = nearest;
        }
    }

    private static bool DamagesTeam(MapLightDamageTeam damageTeam, PlayerTeam team) => damageTeam switch
    {
        MapLightDamageTeam.Red => team == PlayerTeam.Red,
        MapLightDamageTeam.Blue => team == PlayerTeam.Blue,
        _ => true,
    };
}
