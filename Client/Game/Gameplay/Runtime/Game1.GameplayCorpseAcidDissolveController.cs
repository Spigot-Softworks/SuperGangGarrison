#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<int, CorpseAcidDissolveState> _corpseAcidDissolveStates = new();
    private readonly List<int> _staleCorpseAcidDissolveIds = new();

    private sealed class CorpseAcidDissolveState
    {
        public required Texture2D Texture { get; init; }
        public required Color[] SourcePixels { get; init; }
        public required Color[] WorkingPixels { get; init; }
        public required float[] ColumnSpeeds { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public Vector2 Origin { get; init; }
        public float Scale { get; init; }
        public bool OwnsTexture { get; init; }
        public float LastAppliedProgress { get; set; } = -1f;
    }

    private void AdvanceCorpseAcidDissolves()
    {
        AdvanceCorpseAcidDissolveStates();
    }

    private void AdvanceCorpseAcidDissolveStates()
    {
        if (_corpseDurationMode == ClientSettings.CorpseDurationInfinite)
        {
            if (_corpseAcidDissolveStates.Count > 0)
            {
                ResetCorpseAcidDissolves();
            }

            return;
        }

        _staleCorpseAcidDissolveIds.Clear();
        foreach (var corpseId in _corpseAcidDissolveStates.Keys)
        {
            if (!IsActiveFadingCorpseId(corpseId))
            {
                _staleCorpseAcidDissolveIds.Add(corpseId);
            }
        }

        for (var index = 0; index < _staleCorpseAcidDissolveIds.Count; index += 1)
        {
            DisposeCorpseAcidDissolve(_staleCorpseAcidDissolveIds[index]);
        }

        _staleCorpseAcidDissolveIds.Clear();

        foreach (var deadBody in _world.DeadBodies)
        {
            if (!IsCorpseAcidFading(deadBody.TicksRemaining))
            {
                continue;
            }

            if (!TryGetOrCreateCorpseAcidDissolveState(
                    deadBody.Id,
                    deadBody.GameplayClassId,
                    deadBody.ClassId,
                    deadBody.Team,
                    deadBody.AnimationKind,
                    deadBody.Height,
                    out var state))
            {
                continue;
            }

            ApplyCorpseAcidDissolveProgress(state, GetCorpseFadeProgress(deadBody.TicksRemaining));
        }

        foreach (var entry in _immediateNetworkDeadBodies)
        {
            var deadBody = entry.Value;
            if (!IsCorpseAcidFading(deadBody.TicksRemaining))
            {
                continue;
            }

            var syntheticId = -Math.Abs(deadBody.SourcePlayerId);
            if (!TryGetOrCreateCorpseAcidDissolveState(
                    syntheticId,
                    deadBody.GameplayClassId,
                    deadBody.ClassId,
                    deadBody.Team,
                    deadBody.AnimationKind,
                    deadBody.Height,
                    out var state))
            {
                continue;
            }

            ApplyCorpseAcidDissolveProgress(state, GetCorpseFadeProgress(deadBody.TicksRemaining));
        }
    }

    private bool IsActiveFadingCorpseId(int corpseId)
    {
        foreach (var deadBody in _world.DeadBodies)
        {
            if (deadBody.Id == corpseId && IsCorpseAcidFading(deadBody.TicksRemaining))
            {
                return true;
            }
        }

        foreach (var entry in _immediateNetworkDeadBodies)
        {
            var syntheticId = -Math.Abs(entry.Value.SourcePlayerId);
            if (syntheticId == corpseId && IsCorpseAcidFading(entry.Value.TicksRemaining))
            {
                return true;
            }
        }

        return false;
    }

    private void ResetCorpseAcidDissolves()
    {
        foreach (var corpseId in _corpseAcidDissolveStates.Keys)
        {
            _staleCorpseAcidDissolveIds.Add(corpseId);
        }

        for (var index = 0; index < _staleCorpseAcidDissolveIds.Count; index += 1)
        {
            DisposeCorpseAcidDissolve(_staleCorpseAcidDissolveIds[index]);
        }

        _staleCorpseAcidDissolveIds.Clear();
    }

    private void DisposeCorpseAcidDissolve(int corpseId)
    {
        if (!_corpseAcidDissolveStates.Remove(corpseId, out var state))
        {
            return;
        }

        if (state.OwnsTexture)
        {
            state.Texture.Dispose();
        }
    }

    private bool TryGetOrCreateCorpseAcidDissolveState(
        int corpseId,
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float corpseHeight,
        out CorpseAcidDissolveState state)
    {
        if (_corpseAcidDissolveStates.TryGetValue(corpseId, out state!))
        {
            return true;
        }

        if (!TryCaptureCorpseAcidSourcePixels(
                gameplayClassId,
                classId,
                team,
                animationKind,
                corpseHeight,
                out var pixels,
                out var width,
                out var height,
                out var origin,
                out var scale))
        {
            state = null!;
            return false;
        }

        if (!TryCreateCorpseAcidDissolveState(pixels, width, height, origin, scale, out state))
        {
            return false;
        }

        _corpseAcidDissolveStates[corpseId] = state;
        return true;
    }

    private bool TryCreateCorpseAcidDissolveState(
        Color[] pixels,
        int width,
        int height,
        Vector2 origin,
        float scale,
        out CorpseAcidDissolveState state)
    {
        var columnSpeeds = new float[width];
        for (var x = 0; x < width; x += 1)
        {
            // Per-column melt rate makes the corpse fade front uneven and organic.
            columnSpeeds[x] = 0.55f + (_visualRandom.NextSingle() * 0.9f);
        }

        Texture2D? texture = null;
        try
        {
            texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
            texture.SetData(pixels);
        }
        catch
        {
            texture?.Dispose();
            state = null!;
            return false;
        }

        state = new CorpseAcidDissolveState
        {
            Texture = texture!,
            SourcePixels = pixels,
            WorkingPixels = (Color[])pixels.Clone(),
            ColumnSpeeds = columnSpeeds,
            Width = width,
            Height = height,
            Origin = origin,
            Scale = scale,
            OwnsTexture = true,
        };
        return true;
    }

    private bool TryCaptureCorpseAcidSourcePixels(
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float corpseHeight,
        out Color[] pixels,
        out int width,
        out int height,
        out Vector2 origin,
        out float scale)
    {
        pixels = Array.Empty<Color>();
        width = 0;
        height = 0;
        origin = Vector2.Zero;
        scale = 1f;

        var spriteName = GetDeadBodySpriteName(gameplayClassId, classId, team, animationKind);
        if (spriteName is null)
        {
            return false;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frame = sprite.Frames[0];
        if (!TryGetSpriteFramePixels(frame, out pixels, out width, out height))
        {
            return false;
        }

        origin = new Vector2(width * 0.5f, height - (corpseHeight * 0.5f));
        scale = 1f;
        return true;
    }

    private static void ApplyCorpseAcidDissolveProgress(CorpseAcidDissolveState state, float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        if (MathF.Abs(progress - state.LastAppliedProgress) < 0.001f)
        {
            return;
        }

        state.LastAppliedProgress = progress;
        Array.Copy(state.SourcePixels, state.WorkingPixels, state.SourcePixels.Length);

        // Melt from texture-top (y = 0) downward; each column advances at its own speed.
        for (var x = 0; x < state.Width; x += 1)
        {
            var dissolveDepth = progress * state.Height * state.ColumnSpeeds[x];
            var maxY = Math.Min(state.Height, (int)MathF.Floor(dissolveDepth + 0.0001f));
            for (var y = 0; y < maxY; y += 1)
            {
                state.WorkingPixels[(y * state.Width) + x] = Color.Transparent;
            }
        }

        state.Texture.SetData(state.WorkingPixels);
    }

    private bool TryDrawCorpseAcidDissolve(
        int corpseId,
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float worldX,
        float worldY,
        float corpseHeight,
        bool facingLeft,
        float rotationDegrees,
        int ticksRemaining,
        Vector2 cameraPosition)
    {
        if (!IsCorpseAcidFading(ticksRemaining))
        {
            return false;
        }

        if (!TryGetOrCreateCorpseAcidDissolveState(
                corpseId,
                gameplayClassId,
                classId,
                team,
                animationKind,
                corpseHeight,
                out var state))
        {
            return false;
        }

        ApplyCorpseAcidDissolveProgress(state, GetCorpseFadeProgress(ticksRemaining));
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(new Vector2(worldX, worldY));
        var facingScaleX = facingLeft ? -1f : 1f;
        _spriteBatch.Draw(
            state.Texture,
            new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y),
            null,
            Color.White,
            rotationDegrees * (MathF.PI / 180f),
            state.Origin,
            new Vector2(state.Scale * facingScaleX, state.Scale),
            SpriteEffects.None,
            0f);
        return true;
    }
}
