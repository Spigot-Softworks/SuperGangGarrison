#nullable enable
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed partial class GameplayGoreEffectsController
{
    private readonly Dictionary<(int X, int Y, PlayerTeam Team), float> _bloodGlowBuckets = new();
    private static readonly PlayerTeam[] BloodTeams = [PlayerTeam.Red, PlayerTeam.Blue, PlayerTeam.Neutral];

    private Color GetBloodColor(Color original, PlayerTeam team) => BloodLighting.ResolveColor(_context._world.Level.Lighting, team, original);

    private void DrawFlyingTeamBlood(Vector2 cameraPosition)
    {
        foreach (var team in BloodTeams)
        {
            _bloodDrawCellsScratch.Clear();
            _bloodCryoDrawCellsScratch.Clear();
            foreach (var squib in _bloodSquibParticles)
            {
                var sourceTeam = squib.Team is PlayerTeam.Red or PlayerTeam.Blue ? squib.Team : PlayerTeam.Neutral;
                if (sourceTeam != team) continue;
                AccumulateBloodSquibParticle(squib.ExperimentalCryoTinted ? _bloodCryoDrawCellsScratch : _bloodDrawCellsScratch,
                    squib.Seed, squib.X, squib.Y, squib.Scale, squib.VelocityX, squib.VelocityY);
            }
            DrawProceduralBloodCells(_bloodDrawCellsScratch, cameraPosition, false, true, team);
            DrawProceduralBloodCells(_bloodCryoDrawCellsScratch, cameraPosition, true, true, team);
        }
    }

    private void AssignSettledBloodTeams(List<SettledBloodRenderCell> cells)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            var team = PlayerTeam.Neutral;
            var mass = 0f;
            // Smoothing adds fringe cells. Use the strongest nearby source for their team.
            for (var y = -2; y <= 2; y++)
                for (var x = -2; x <= 2; x++)
                    if (_settledBloodCells.TryGetValue((cell.X + x, cell.Y + y), out var source))
                    {
                        var weight = source.Amount / (1f + x * x + y * y);
                        if (weight > mass) { mass = weight; team = source.Team; }
                    }
            cells[index] = cell with { Team = team };
        }
    }

    internal void AppendBloodLights(LightmapRenderer lightmap)
    {
        var lighting = _context._world.Level.Lighting;
        if (!_context.AreBloodVisualsEnabled || !lighting.IsActive || lighting.BloodGlow <= 0) return;
        _bloodGlowBuckets.Clear();
        if (_context.GameplayRuntimeSettings.BloodRenderMode == 0)
        {
            foreach (var squib in _bloodSquibParticles) AddBloodGlowSample(squib.X, squib.Y, squib.Team, 1f);
            foreach (var entry in _settledBloodCells)
                if (entry.Value.Amount >= 0.08f)
                    AddBloodGlowSample((entry.Key.X + 0.5f) * BloodCellSize, (entry.Key.Y + 0.5f) * BloodCellSize,
                        entry.Value.Team, Math.Clamp(entry.Value.Amount, 0f, 1f));
        }
        else
        {
            foreach (var blood in _bloodVisuals) AddBloodGlowSample(blood.X, blood.Y, blood.Team, blood.TicksRemaining / (float)Game1.BloodVisual.LifetimeTicks);
            foreach (var blood in _bloodSprayVisuals) AddBloodGlowSample(blood.X, blood.Y, blood.Team, blood.TicksRemaining / (float)blood.InitialTicks);
            foreach (var blood in _context._world.BloodDrops) AddBloodGlowSample(blood.X, blood.Y, blood.Team, blood.Alpha);
        }
        foreach (var blood in _gibBloodExplosionVisuals) AddBloodGlowSample(blood.X, blood.Y, blood.Team, 1f);
        var strength = lighting.BloodGlow / 100f;
        foreach (var entry in _bloodGlowBuckets)
        {
            var color = GetBloodColor(new Color(218, 22, 28), entry.Key.Team);
            lightmap.AddLight((entry.Key.X + 0.5f) * 16, (entry.Key.Y + 0.5f) * 16, 20,
                color, strength * entry.Value * 0.35f, glowBoost: strength * entry.Value);
        }
    }

    private void AddBloodGlowSample(float x, float y, PlayerTeam team, float amount)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || amount <= 0) return;
        var key = ((int)MathF.Floor(x / 16), (int)MathF.Floor(y / 16), team);
        _bloodGlowBuckets[key] = Math.Max(_bloodGlowBuckets.GetValueOrDefault(key), Math.Clamp(amount, 0f, 1f));
    }
}
