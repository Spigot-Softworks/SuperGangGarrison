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
        /// <summary>Small per-column front offsets (pixels) for a gently uneven melt line.</summary>
        public required float[] ColumnOffsets { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public Vector2 Origin { get; init; }
        public float Scale { get; init; }
        public bool OwnsTexture { get; init; }
        public float LastAppliedProgress { get; set; } = -1f;
        public float WavePhase { get; init; }
        public float WaveAmplitude { get; init; }
        public float WaveFrequency { get; init; }
        public float SoftBand { get; init; }
        public float EdgePad { get; init; }
        public int NoiseSeed { get; init; }
        /// <summary>
        /// When set, the dissolve texture already bakes pose/facing/rotation and is drawn at this
        /// world top-left with identity transform (dynamic ragdoll freeze).
        /// </summary>
        public bool UseFrozenWorldSnapshot { get; init; }
        public float FrozenWorldX { get; init; }
        public float FrozenWorldY { get; init; }
        /// <summary>Burn-charred dissolve owns its own progress (not the normal end-of-life fade window).</summary>
        public bool IsBurnCharred { get; set; }
        public Texture2D? OutlineTexture { get; set; }
        public Color[]? OutlinePixels { get; set; }
        public float LastOutlinePhase { get; set; } = float.MinValue;
    }

    private void AdvanceCorpseAcidDissolves()
    {
        AdvanceCorpseAcidDissolveStates();
    }

    private void AdvanceCorpseAcidDissolveStates()
    {
        if (_corpseDurationMode == ClientSettings.CorpseDurationInfinite)
        {
            // Keep burn-charred dissolve textures; only clear normal end-of-life fades.
            _staleCorpseAcidDissolveIds.Clear();
            foreach (var pair in _corpseAcidDissolveStates)
            {
                if (!pair.Value.IsBurnCharred)
                {
                    _staleCorpseAcidDissolveIds.Add(pair.Key);
                }
            }

            for (var index = 0; index < _staleCorpseAcidDissolveIds.Count; index += 1)
            {
                DisposeCorpseAcidDissolve(_staleCorpseAcidDissolveIds[index]);
            }

            _staleCorpseAcidDissolveIds.Clear();
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

            TransferCorpseAcidDissolveIdIfNeeded(deadBody.Id, deadBody.SourcePlayerId);
            if (_corpseAcidDissolveStates.TryGetValue(deadBody.Id, out var existing)
                && existing.IsBurnCharred)
            {
                continue;
            }

            if (!TryEnsureCorpseAcidDissolveState(
                    deadBody.Id,
                    deadBody.SourcePlayerId,
                    deadBody.GameplayClassId,
                    deadBody.ClassId,
                    deadBody.Team,
                    deadBody.AnimationKind,
                    deadBody.Height,
                    out var state))
            {
                continue;
            }

            if (state.IsBurnCharred)
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
            if (_corpseAcidDissolveStates.TryGetValue(syntheticId, out var existing)
                && existing.IsBurnCharred)
            {
                continue;
            }

            if (!TryEnsureCorpseAcidDissolveState(
                    syntheticId,
                    deadBody.SourcePlayerId,
                    deadBody.GameplayClassId,
                    deadBody.ClassId,
                    deadBody.Team,
                    deadBody.AnimationKind,
                    deadBody.Height,
                    out var state))
            {
                continue;
            }

            if (state.IsBurnCharred)
            {
                continue;
            }

            ApplyCorpseAcidDissolveProgress(state, GetCorpseFadeProgress(deadBody.TicksRemaining));
        }
    }

    private bool IsActiveFadingCorpseId(int corpseId)
    {
        // Burn-charred dissolves start well before the normal end-of-life fade window.
        // Keep those textures alive while burn dissolve is still playing.
        if (_corpseAcidDissolveStates.TryGetValue(corpseId, out var dissolveState)
            && dissolveState.IsBurnCharred
            && IsActiveBurnCharredDissolveCorpseId(corpseId))
        {
            return true;
        }

        foreach (var deadBody in _world.DeadBodies)
        {
            if (deadBody.Id == corpseId && IsCorpseAcidFading(deadBody.TicksRemaining))
            {
                return true;
            }

            // Accept synthetic keys until Sync re-keys them onto the authoritative corpse id.
            var syntheticId = -Math.Abs(deadBody.SourcePlayerId);
            if (syntheticId == corpseId && IsCorpseAcidFading(deadBody.TicksRemaining))
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

    private bool IsActiveBurnCharredDissolveCorpseId(int corpseId)
    {
        if (_burnCharredCorpseStates.TryGetValue(corpseId, out var burnState)
            && burnState.Dissolving
            && !burnState.Consumed)
        {
            return true;
        }

        foreach (var entry in _burnCharredCorpseStates)
        {
            if (entry.Value.Consumed || !entry.Value.Dissolving)
            {
                continue;
            }

            if (entry.Key == corpseId || entry.Value.DissolveCorpseId == corpseId)
            {
                return true;
            }
        }

        return false;
    }

    private void TransferCorpseAcidDissolveIdIfNeeded(int deadBodyId, int sourcePlayerId)
    {
        if (deadBodyId <= 0 || _corpseAcidDissolveStates.ContainsKey(deadBodyId))
        {
            return;
        }

        var syntheticId = -Math.Abs(sourcePlayerId);
        if (_corpseAcidDissolveStates.Remove(syntheticId, out var transferred))
        {
            _corpseAcidDissolveStates[deadBodyId] = transferred;
        }
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

        state.OutlineTexture?.Dispose();
    }

    private bool TryEnsureCorpseAcidDissolveState(
        int corpseId,
        int sourcePlayerId,
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

        // Dynamic corpses freeze their posed ragdoll â€” never swap to the static DeadS sprite.
        if (TryGetDynamicRagdollForAcidCapture(corpseId, sourcePlayerId, out var ragdoll))
        {
            if (TryCaptureDynamicRagdollAcidDissolve(corpseId, ragdoll, out state))
            {
                return true;
            }

            // Keep the live ragdoll until a snapshot succeeds; do not fall back to the wrong sprite.
            state = null!;
            return false;
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

        if (!TryCreateCorpseAcidDissolveState(
                pixels,
                width,
                height,
                origin,
                scale,
                useFrozenWorldSnapshot: false,
                frozenWorldX: 0f,
                frozenWorldY: 0f,
                out state))
        {
            return false;
        }

        _corpseAcidDissolveStates[corpseId] = state;
        return true;
    }

    private bool TryGetDynamicRagdollForAcidCapture(
        int corpseId,
        int sourcePlayerId,
        out DynamicRagdollState ragdoll)
    {
        if (!_dynamicRagdollEnabled)
        {
            ragdoll = null!;
            return false;
        }

        if (_dynamicRagdolls.TryGetValue(corpseId, out ragdoll!))
        {
            return true;
        }

        var syntheticId = -Math.Abs(sourcePlayerId);
        if (syntheticId != corpseId && _dynamicRagdolls.TryGetValue(syntheticId, out ragdoll!))
        {
            return true;
        }

        // After re-keying, callers may still hold a synthetic id while the ragdoll lives under the real id.
        foreach (var pair in _dynamicRagdolls)
        {
            if (pair.Value.SourcePlayerId == sourcePlayerId)
            {
                ragdoll = pair.Value;
                return true;
            }
        }

        ragdoll = null!;
        return false;
    }

    private bool TryCaptureDynamicRagdollAcidDissolve(
        int corpseId,
        DynamicRagdollState ragdoll,
        out CorpseAcidDissolveState state)
    {
        state = null!;
        if (!TryEstimateDynamicRagdollCaptureBounds(ragdoll, out var worldLeft, out var worldTop, out var captureWidth, out var captureHeight))
        {
            return false;
        }

        RenderTarget2D? capture = null;
        var previousTargets = GraphicsDevice.GetRenderTargets();
        try
        {
            capture = new RenderTarget2D(
                GraphicsDevice,
                captureWidth,
                captureHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents);

            GraphicsDevice.SetRenderTarget(capture);
            GraphicsDevice.Clear(Color.Transparent);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
            // Force full opacity so the freeze snapshot is solid even mid-fade bookkeeping.
            var drawn = DrawDynamicRagdollVisual(
                ragdoll,
                ticksRemaining: int.MaxValue,
                cameraPosition: new Vector2(worldLeft, worldTop));
            _spriteBatch.End();

            if (!drawn)
            {
                return false;
            }

            var rawPixels = new Color[captureWidth * captureHeight];
            capture.GetData(rawPixels);
            if (!TryComputeOpaqueBounds(rawPixels, captureWidth, captureHeight, out var opaque)
                || opaque.Width <= 0
                || opaque.Height <= 0)
            {
                return false;
            }

            var cropped = new Color[opaque.Width * opaque.Height];
            for (var y = 0; y < opaque.Height; y += 1)
            {
                var srcRow = ((opaque.Y + y) * captureWidth) + opaque.X;
                var dstRow = y * opaque.Width;
                Array.Copy(rawPixels, srcRow, cropped, dstRow, opaque.Width);
            }

            if (!TryCreateCorpseAcidDissolveState(
                    cropped,
                    opaque.Width,
                    opaque.Height,
                    origin: Vector2.Zero,
                    scale: 1f,
                    useFrozenWorldSnapshot: true,
                    frozenWorldX: worldLeft + opaque.X,
                    frozenWorldY: worldTop + opaque.Y,
                    out state))
            {
                return false;
            }

            ragdoll.AcidFrozen = true;
            ragdoll.VelocityX = 0f;
            ragdoll.VelocityY = 0f;
            ragdoll.AngularVelocityDegrees = 0f;
            for (var pivotIndex = 0; pivotIndex < DynamicRagdollPivotCount; pivotIndex += 1)
            {
                ragdoll.PivotVelocities[pivotIndex] = 0f;
            }

            _corpseAcidDissolveStates[corpseId] = state;
            return true;
        }
        catch
        {
            state = null!;
            return false;
        }
        finally
        {
            try
            {
                GraphicsDevice.SetRenderTargets(previousTargets);
            }
            catch
            {
                GraphicsDevice.SetRenderTarget(null);
            }

            capture?.Dispose();
        }
    }

    private static bool TryEstimateDynamicRagdollCaptureBounds(
        DynamicRagdollState ragdoll,
        out float worldLeft,
        out float worldTop,
        out int captureWidth,
        out int captureHeight)
    {
        worldLeft = 0f;
        worldTop = 0f;
        captureWidth = 0;
        captureHeight = 0;

        var opaque = ragdoll.OpaqueBounds;
        var halfSpan = MathF.Max(24f, MathF.Max(opaque.Width, opaque.Height) * 0.85f + 20f);

        Span<Vector2> nodes = stackalloc Vector2[DynamicRagdollCollisionNodeCount];
        var nodeCount = BuildRagdollCollisionNodes(ragdoll, nodes);
        var minX = ragdoll.X - halfSpan;
        var maxX = ragdoll.X + halfSpan;
        var minY = ragdoll.Y - halfSpan;
        var maxY = ragdoll.Y + halfSpan;
        for (var index = 0; index < nodeCount; index += 1)
        {
            minX = MathF.Min(minX, nodes[index].X - halfSpan * 0.55f);
            maxX = MathF.Max(maxX, nodes[index].X + halfSpan * 0.55f);
            minY = MathF.Min(minY, nodes[index].Y - halfSpan * 0.55f);
            maxY = MathF.Max(maxY, nodes[index].Y + halfSpan * 0.55f);
        }

        // Weapon flap can stick out a bit on Elkondo ragdolls.
        if (ragdoll.UseElkondoVerticalVisual && !string.IsNullOrEmpty(ragdoll.WeaponSpriteName))
        {
            minX -= 18f;
            maxX += 18f;
            minY -= 18f;
            maxY += 18f;
        }

        worldLeft = MathF.Floor(minX);
        worldTop = MathF.Floor(minY);
        captureWidth = Math.Clamp((int)MathF.Ceiling(maxX - worldLeft) + 2, 8, 512);
        captureHeight = Math.Clamp((int)MathF.Ceiling(maxY - worldTop) + 2, 8, 512);
        return true;
    }

    private bool TryCreateCorpseAcidDissolveState(
        Color[] pixels,
        int width,
        int height,
        Vector2 origin,
        float scale,
        bool useFrozenWorldSnapshot,
        float frozenWorldX,
        float frozenWorldY,
        out CorpseAcidDissolveState state)
    {
        // Gentle random-walk offsets: a soft uneven front, not independent vertical streaks.
        var columnOffsets = new float[width];
        var offset = 0f;
        for (var x = 0; x < width; x += 1)
        {
            offset = Math.Clamp(offset + ((_visualRandom.NextSingle() * 2f) - 1f) * 0.85f, -3.25f, 3.25f);
            columnOffsets[x] = offset;
        }

        var softBand = Math.Clamp(height * 0.08f, 2.5f, 5.5f);
        var waveAmplitude = Math.Clamp(height * 0.035f, 1.25f, 3.25f);
        var edgePad = softBand + 3.25f + waveAmplitude + 1f;

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
            ColumnOffsets = columnOffsets,
            Width = width,
            Height = height,
            Origin = origin,
            Scale = scale,
            OwnsTexture = true,
            WavePhase = _visualRandom.NextSingle() * MathF.Tau,
            WaveAmplitude = waveAmplitude,
            WaveFrequency = MathF.Tau / MathF.Max(10f, width * 0.55f),
            SoftBand = softBand,
            EdgePad = edgePad,
            NoiseSeed = _visualRandom.Next(),
            UseFrozenWorldSnapshot = useFrozenWorldSnapshot,
            FrozenWorldX = frozenWorldX,
            FrozenWorldY = frozenWorldY,
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
        if (!TryGetSpriteFramePixels(frame, out var framePixels, out var frameWidth, out var frameHeight))
        {
            return false;
        }

        // Crop transparent padding so acid starts eating at the visible top of the corpse.
        if (TryComputeOpaqueBounds(framePixels, frameWidth, frameHeight, out var opaque)
            && opaque.Width > 0
            && opaque.Height > 0
            && (opaque.Width != frameWidth || opaque.Height != frameHeight))
        {
            pixels = new Color[opaque.Width * opaque.Height];
            for (var y = 0; y < opaque.Height; y += 1)
            {
                var srcRow = ((opaque.Y + y) * frameWidth) + opaque.X;
                var dstRow = y * opaque.Width;
                Array.Copy(framePixels, srcRow, pixels, dstRow, opaque.Width);
            }

            width = opaque.Width;
            height = opaque.Height;
            var fullOrigin = new Vector2(frameWidth * 0.5f, frameHeight - (corpseHeight * 0.5f));
            origin = fullOrigin - new Vector2(opaque.X, opaque.Y);
        }
        else
        {
            pixels = framePixels;
            width = frameWidth;
            height = frameHeight;
            origin = new Vector2(width * 0.5f, height - (corpseHeight * 0.5f));
        }

        scale = 1f;
        return true;
    }

    private static void ApplyCorpseAcidDissolveProgress(CorpseAcidDissolveState state, float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        if (MathF.Abs(progress - state.LastAppliedProgress) < 0.0005f)
        {
            return;
        }

        state.LastAppliedProgress = progress;
        Array.Copy(state.SourcePixels, state.WorkingPixels, state.SourcePixels.Length);

        // Front travels past the bottom (plus pad) so jitter/wave still fully clears by progress=1.
        var baseFront = progress * (state.Height + state.EdgePad);

        for (var x = 0; x < state.Width; x += 1)
        {
            var front = baseFront
                + state.ColumnOffsets[x]
                + (MathF.Sin((x * state.WaveFrequency) + state.WavePhase) * state.WaveAmplitude);

            for (var y = 0; y < state.Height; y += 1)
            {
                var index = (y * state.Width) + x;
                if (state.SourcePixels[index].A == 0)
                {
                    continue;
                }

                var intoAcid = front - y;
                if (intoAcid <= 0f)
                {
                    continue;
                }

                if (intoAcid >= state.SoftBand)
                {
                    state.WorkingPixels[index] = Color.Transparent;
                    continue;
                }

                // Soft mottled nibble along a roughly horizontal front â€” not vertical melt columns.
                var bandT = intoAcid / state.SoftBand;
                var nibble = Hash01(x, y, state.NoiseSeed);
                var threshold = 0.12f + (bandT * bandT * 0.88f);
                if (nibble < threshold)
                {
                    state.WorkingPixels[index] = Color.Transparent;
                }
            }
        }

        state.Texture.SetData(state.WorkingPixels);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFFu) / 65535f;
        }
    }

    /// <summary>
    /// Draws an already-captured acid dissolve (used by dynamic ragdolls â€” never creates a static sprite).
    /// </summary>
    private bool TryDrawExistingCorpseAcidDissolve(
        int corpseId,
        int sourcePlayerId,
        int ticksRemaining,
        Vector2 cameraPosition)
    {
        if (!IsCorpseAcidFading(ticksRemaining))
        {
            return false;
        }

        TransferCorpseAcidDissolveIdIfNeeded(corpseId, sourcePlayerId);
        if (!_corpseAcidDissolveStates.TryGetValue(corpseId, out var state))
        {
            var syntheticId = -Math.Abs(sourcePlayerId);
            if (syntheticId == corpseId || !_corpseAcidDissolveStates.TryGetValue(syntheticId, out state))
            {
                return false;
            }
        }

        if (state.IsBurnCharred)
        {
            return false;
        }

        ApplyCorpseAcidDissolveProgress(state, GetCorpseFadeProgress(ticksRemaining));
        DrawCorpseAcidDissolveState(
            state,
            worldX: state.FrozenWorldX,
            worldY: state.FrozenWorldY,
            facingLeft: false,
            rotationDegrees: 0f,
            cameraPosition);
        return true;
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

        if (!TryEnsureCorpseAcidDissolveState(
                corpseId,
                sourcePlayerId: 0,
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
        if (state.UseFrozenWorldSnapshot)
        {
            DrawCorpseAcidDissolveState(
                state,
                state.FrozenWorldX,
                state.FrozenWorldY,
                facingLeft: false,
                rotationDegrees: 0f,
                cameraPosition);
            return true;
        }

        DrawCorpseAcidDissolveState(state, worldX, worldY, facingLeft, rotationDegrees, cameraPosition);
        return true;
    }

    private void DrawCorpseAcidDissolveState(
        CorpseAcidDissolveState state,
        float worldX,
        float worldY,
        bool facingLeft,
        float rotationDegrees,
        Vector2 cameraPosition)
    {
        var roundedOrigin = state.UseFrozenWorldSnapshot
            ? new Vector2(MathF.Round(worldX), MathF.Round(worldY))
            : GetRoundedPlayerSpriteOrigin(new Vector2(worldX, worldY));
        var facingScaleX = facingLeft ? -1f : 1f;
        var drawPosition = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y);
        var rotation = rotationDegrees * (MathF.PI / 180f);
        var scale = new Vector2(state.Scale * facingScaleX, state.Scale);

        if (state.IsBurnCharred && state.OutlineTexture is not null)
        {
            // Outline texture has BurnOutlinePadTop rows above the meat; bump origin so meat aligns.
            _spriteBatch.Draw(
                state.OutlineTexture,
                drawPosition,
                null,
                Color.White,
                rotation,
                state.Origin + new Vector2(0f, BurnOutlinePadTop),
                scale,
                SpriteEffects.None,
                0f);
        }

        _spriteBatch.Draw(
            state.Texture,
            drawPosition,
            null,
            Color.White,
            rotation,
            state.Origin,
            scale,
            SpriteEffects.None,
            0f);
    }

    private bool TryGetCorpseAcidDissolveState(int corpseId, out CorpseAcidDissolveState state)
        => _corpseAcidDissolveStates.TryGetValue(corpseId, out state!);

    private bool TryCaptureBurnCharredRagdollDissolve(int corpseId, DynamicRagdollState ragdoll)
    {
        if (_corpseAcidDissolveStates.TryGetValue(corpseId, out var existing))
        {
            if (!existing.IsBurnCharred)
            {
                ConvertDissolveStateToBurnCharred(existing);
            }

            ragdoll.AcidFrozen = true;
            return true;
        }

        if (!TryCaptureDynamicRagdollAcidDissolve(corpseId, ragdoll, out var state))
        {
            return false;
        }

        ConvertDissolveStateToBurnCharred(state);
        return true;
    }

    private void ConvertDissolveStateToBurnCharred(CorpseAcidDissolveState state)
    {
        BakeBurnCharredSoot(state.SourcePixels, state.Width, state.Height, state.NoiseSeed);
        Array.Copy(state.SourcePixels, state.WorkingPixels, state.SourcePixels.Length);
        state.Texture.SetData(state.SourcePixels);
        state.IsBurnCharred = true;
        state.LastAppliedProgress = -1f;
        EnsureBurnCharredOutlineBuffers(state);
    }

    private bool TryCaptureBurnCharredStaticDissolve(
        int corpseId,
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float corpseHeight,
        float worldX,
        float worldY)
    {
        if (_corpseAcidDissolveStates.TryGetValue(corpseId, out var existing) && existing.IsBurnCharred)
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
            return false;
        }

        BakeBurnCharredSoot(pixels, width, height, _visualRandom.Next());
        if (!TryCreateCorpseAcidDissolveState(
                pixels,
                width,
                height,
                origin,
                scale,
                useFrozenWorldSnapshot: false,
                frozenWorldX: worldX,
                frozenWorldY: worldY,
                out var state))
        {
            return false;
        }

        state.IsBurnCharred = true;
        EnsureBurnCharredOutlineBuffers(state);
        _corpseAcidDissolveStates[corpseId] = state;
        return true;
    }

    private static void BakeBurnCharredSoot(Color[] pixels, int width, int height, int seed)
    {
        for (var y = 0; y < height; y += 1)
        {
            for (var x = 0; x < width; x += 1)
            {
                var index = (y * width) + x;
                var src = pixels[index];
                if (src.A < 8)
                {
                    continue;
                }

                // Soft neighborhood noise; keep most of the original color visible (~35â€“55% soot).
                var center = Hash01(x, y, seed);
                var blur = (
                    center
                    + Hash01(x - 1, y, seed)
                    + Hash01(x + 1, y, seed)
                    + Hash01(x, y - 1, seed)
                    + Hash01(x, y + 1, seed)
                    + Hash01(x - 1, y - 1, seed)
                    + Hash01(x + 1, y + 1, seed)) * (1f / 7f);
                var sootAlpha = 0.58f + (blur * 0.22f);
                var keep = 1f - sootAlpha;
                pixels[index] = new Color(
                    (byte)Math.Clamp((int)(src.R * keep), 0, 255),
                    (byte)Math.Clamp((int)(src.G * keep * 0.94f), 0, 255),
                    (byte)Math.Clamp((int)(src.B * keep * 0.90f), 0, 255),
                    src.A);
            }
        }
    }

    private void EnsureBurnCharredOutlineBuffers(CorpseAcidDissolveState state)
    {
        var outlineHeight = state.Height + BurnOutlinePadTop;
        var outlineLength = state.Width * outlineHeight;
        if (state.OutlinePixels is null || state.OutlinePixels.Length != outlineLength)
        {
            state.OutlinePixels = new Color[outlineLength];
        }

        if (state.OutlineTexture is null
            || state.OutlineTexture.Width != state.Width
            || state.OutlineTexture.Height != outlineHeight)
        {
            state.OutlineTexture?.Dispose();
            state.OutlineTexture = new Texture2D(GraphicsDevice, state.Width, outlineHeight, false, SurfaceFormat.Color);
        }
    }

    private void ApplyBurnCharredDissolveProgress(CorpseAcidDissolveState state, float progress, float outlinePhase)
    {
        ApplyCorpseAcidDissolveProgress(state, progress);
        UpdateBurnCharredOutline(state, outlinePhase);
    }

    private void UpdateBurnCharredOutline(CorpseAcidDissolveState state, float outlinePhase)
    {
        if (!state.IsBurnCharred || state.OutlinePixels is null || state.OutlineTexture is null)
        {
            return;
        }

        state.LastOutlinePhase = outlinePhase;
        Array.Clear(state.OutlinePixels);

        // Per-column top of remaining opaque meat.
        Span<int> columnTopY = stackalloc int[state.Width];
        for (var x = 0; x < state.Width; x += 1)
        {
            columnTopY[x] = -1;
            for (var y = 0; y < state.Height; y += 1)
            {
                if (state.WorkingPixels[(y * state.Width) + x].A >= 20)
                {
                    columnTopY[x] = y;
                    break;
                }
            }
        }

        // Worst case: every 2px column emits up to 3 tongue cells (height 6).
        Span<BurnOutlineCell> outlineCells = stackalloc BurnOutlineCell[((state.Width + 1) / 2) * 3];
        var cellCount = EmitBurnOutlineTongueCells(
            state.Width,
            columnTopY,
            outlinePhase,
            state.NoiseSeed,
            outlineCells);
        for (var i = 0; i < cellCount; i += 1)
        {
            ref readonly var cell = ref outlineCells[i];
            StampBurnOutlineCell(state, cell.X, cell.Y, cell.Color);
        }

        state.OutlineTexture.SetData(state.OutlinePixels);
    }

    private readonly struct BurnOutlineCell
    {
        public BurnOutlineCell(int x, int y, Color color)
        {
            X = x;
            Y = y;
            Color = color;
        }

        public int X { get; }
        public int Y { get; }
        public Color Color { get; }
    }

    /// <summary>
    /// Shared 2Ã—2 tongue emitter for live + dissolve. <paramref name="columnTopY"/> is meat-top Y per pixel column (-1 = empty).
    /// Writes cell top-lefts in the same pixel space (may be negative above the meat). Returns written count.
    /// </summary>
    private static int EmitBurnOutlineTongueCells(
        int width,
        ReadOnlySpan<int> columnTopY,
        float outlinePhase,
        int seed,
        Span<BurnOutlineCell> output)
    {
        const int cellSize = 2;
        var cellColumns = (width + cellSize - 1) / cellSize;
        Span<int> cellThicknessPx = stackalloc int[cellColumns];
        for (var cx = 0; cx < cellColumns; cx += 1)
        {
            cellThicknessPx[cx] = SnapBurnOutlineThicknessPx(
                SampleBurnOutlineThickness(cx, outlinePhase, seed));
        }

        var written = 0;
        for (var cx = 0; cx < cellColumns; cx += 1)
        {
            var left = columnTopY[cx * cellSize];
            var right = (cx * cellSize + 1 < width) ? columnTopY[cx * cellSize + 1] : -1;
            var topY = left < 0 ? right : (right < 0 ? left : Math.Min(left, right));
            if (topY < 0)
            {
                continue;
            }

            // Snap the burn front to the 2px grid (ceil toward body so we sit just above meat).
            var frontY = ((topY + cellSize - 1) / cellSize) * cellSize;
            var thickness = cellThicknessPx[cx];
            var tongueCells = thickness / cellSize;

            for (var cell = 0; cell < tongueCells; cell += 1)
            {
                var cellTopY = frontY - ((cell + 1) * cellSize);

                // Height within tongue: 0 = near corpse (yellow), 1 = tip (red).
                var heightT = tongueCells <= 1 ? 0.5f : cell / (float)(tongueCells - 1);
                var visibility = SampleBurnOutlineVisibility(cx, cell, outlinePhase, seed);
                // Hard on/off flicker â€” never soft-alpha the outline (looks translucent).
                if (visibility < 0.35f)
                {
                    continue;
                }

                if (written >= output.Length)
                {
                    return written;
                }

                output[written] = new BurnOutlineCell(
                    cx * cellSize,
                    cellTopY,
                    SampleBurnFlameColorByHeight(heightT));
                written += 1;
            }
        }

        return written;
    }

    private static void StampBurnOutlineCell(CorpseAcidDissolveState state, int cellLeft, int cellTop, Color flame)
    {
        if (flame.A < 8 || state.OutlinePixels is null)
        {
            return;
        }

        var outlineHeight = state.Height + BurnOutlinePadTop;
        for (var oy = 0; oy < 2; oy += 1)
        {
            for (var ox = 0; ox < 2; ox += 1)
            {
                var nx = cellLeft + ox;
                var meatY = cellTop + oy;
                var ny = meatY + BurnOutlinePadTop;
                if (nx < 0 || ny < 0 || nx >= state.Width || ny >= outlineHeight)
                {
                    continue;
                }

                // Skip stamping over remaining meat (in unpadded working space).
                if (meatY >= 0
                    && meatY < state.Height
                    && state.WorkingPixels[(meatY * state.Width) + nx].A >= 20)
                {
                    continue;
                }

                var nIndex = (ny * state.Width) + nx;
                var previous = state.OutlinePixels[nIndex];
                if (previous.A < flame.A)
                {
                    state.OutlinePixels[nIndex] = flame;
                }
            }
        }
    }

    /// <summary>Moving blurred noise â†’ outline tongue height in pixels for a column (1â€“6).</summary>
    private static int SampleBurnOutlineThickness(int x, float phase, int seed)
    {
        var drift = (int)MathF.Floor(phase * 2.3f);
        var blur = (
            Hash01(x + drift, 0, seed ^ 0xC01D)
            + Hash01(x + drift - 1, 0, seed ^ 0xC01D)
            + Hash01(x + drift + 1, 0, seed ^ 0xC01D)
            + Hash01(x + drift - 2, 1, seed ^ 0xC01D)
            + Hash01(x + drift + 2, 1, seed ^ 0xC01D)) * 0.2f;
        var wave = 0.5f + (0.5f * MathF.Sin(phase * 2.6f + (x * 0.31f)));
        var t = Math.Clamp((blur * 0.7f) + (wave * 0.3f), 0f, 1f);
        return 1 + (int)MathF.Floor(t * 5.999f);
    }

    /// <summary>Snap tongue height onto the 2Ã—2 flame-particle lattice (2/4/6 px).</summary>
    private static int SnapBurnOutlineThicknessPx(int rawThicknessPx)
        => Math.Clamp(((rawThicknessPx + 1) / 2) * 2, 2, 6);

    private static float SampleBurnOutlineVisibility(int cellX, int cellDepth, float phase, int seed)
    {
        var drift = (int)MathF.Floor(phase * 1.9f);
        var blur = (
            Hash01(cellX + drift, cellDepth, seed ^ 0xA5A5)
            + Hash01(cellX + drift - 1, cellDepth, seed ^ 0xA5A5)
            + Hash01(cellX + drift + 1, cellDepth, seed ^ 0xA5A5)
            + Hash01(cellX + drift, cellDepth - 1, seed ^ 0xA5A5)
            + Hash01(cellX + drift, cellDepth + 1, seed ^ 0xA5A5)) * 0.2f;
        var pulse = 0.55f + (0.45f * MathF.Sin(phase * 4.4f + (cellX * 0.37f) - (cellDepth * 0.55f)));
        return Math.Clamp(0.25f + (blur * 0.75f * pulse), 0.08f, 1f);
    }

    /// <summary>0 = near corpse (yellow), 1 = tongue tip (red).</summary>
    private static Color SampleBurnFlameColorByHeight(float heightT)
    {
        heightT = Math.Clamp(heightT, 0f, 1f);
        Color a;
        Color b;
        float local;
        if (heightT < 0.45f)
        {
            // yellow â†’ orange
            a = new Color(255, 245, 140, 255);
            b = new Color(255, 180, 40, 255);
            local = heightT / 0.45f;
        }
        else if (heightT < 0.75f)
        {
            // orange â†’ hot red-orange
            a = new Color(255, 180, 40, 255);
            b = new Color(255, 90, 20, 255);
            local = (heightT - 0.45f) / 0.30f;
        }
        else
        {
            // hot red tip
            a = new Color(255, 90, 20, 255);
            b = new Color(220, 35, 10, 255);
            local = (heightT - 0.75f) / 0.25f;
        }

        return Color.Lerp(a, b, local);
    }

    private bool TryDrawBurnCharredDissolveState(
        int corpseId,
        float worldX,
        float worldY,
        bool facingLeft,
        Vector2 cameraPosition)
    {
        if (!TryGetCorpseAcidDissolveState(corpseId, out var state) || !state.IsBurnCharred)
        {
            return false;
        }

        if (state.UseFrozenWorldSnapshot)
        {
            DrawCorpseAcidDissolveState(
                state,
                state.FrozenWorldX,
                state.FrozenWorldY,
                facingLeft: false,
                rotationDegrees: 0f,
                cameraPosition);
            return true;
        }

        DrawCorpseAcidDissolveState(state, worldX, worldY, facingLeft, rotationDegrees: 0f, cameraPosition);
        return true;
    }
}
