#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int BurnCharredDissolveTicks = 96;
    /// <summary>How long the corpse must rest after landing before burn-dissolve starts.</summary>
    private const int BurnCharredSettleDwellTicks = 22;
    private const int BurnCharredMinGroundedTicks = 10;
    /// <summary>Extra outline texture rows above the meat so 2×2 tongues can extend past a tight opaque crop.</summary>
    private const int BurnOutlinePadTop = 6;

    private readonly Dictionary<int, BurnCharredCorpseState> _burnCharredCorpseStates = new();
    private readonly List<int> _staleBurnCharredCorpseIds = new();

    private sealed class BurnCharredCorpseState
    {
        public bool Dissolving;
        public bool Consumed;
        public int DissolveTicksRemaining;
        public int SettledDwellTicks;
        public float OutlinePhase;
        public float WorldX;
        public float WorldY;
        public bool FacingLeft;
        public string GameplayClassId = string.Empty;
        public PlayerClass ClassId;
        public PlayerTeam Team;
        public DeadBodyAnimationKind AnimationKind;
        public float CorpseHeight;
        /// <summary>Corpse id used for the dissolve texture lookup (may differ after synthetic→real rekey).</summary>
        public int DissolveCorpseId;
    }

    private void ResetBurnCharredCorpses()
    {
        _burnCharredCorpseStates.Clear();
        _staleBurnCharredCorpseIds.Clear();
    }

    private void AdvanceBurnCharredCorpses()
    {
        if (!_gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled)
        {
            if (_burnCharredCorpseStates.Count > 0)
            {
                ResetBurnCharredCorpses();
            }

            return;
        }

        _staleBurnCharredCorpseIds.Clear();
        foreach (var corpseId in _burnCharredCorpseStates.Keys)
        {
            if (!IsActiveBurnCharredCorpseId(corpseId))
            {
                _staleBurnCharredCorpseIds.Add(corpseId);
            }
        }

        for (var index = 0; index < _staleBurnCharredCorpseIds.Count; index += 1)
        {
            _burnCharredCorpseStates.Remove(_staleBurnCharredCorpseIds[index]);
        }

        _staleBurnCharredCorpseIds.Clear();

        foreach (var deadBody in _world.DeadBodies)
        {
            if (!deadBody.DiedToFire)
            {
                continue;
            }

            TransferBurnCharredCorpseIdIfNeeded(deadBody.Id, deadBody.SourcePlayerId);
            AdvanceBurnCharredCorpse(
                deadBody.Id,
                deadBody.SourcePlayerId,
                deadBody.X,
                deadBody.Y,
                deadBody.FacingLeft,
                deadBody.GameplayClassId,
                deadBody.ClassId,
                deadBody.Team,
                deadBody.AnimationKind,
                deadBody.Height,
                deadBody.HorizontalSpeed,
                deadBody.VerticalSpeed);
        }

        foreach (var entry in _immediateNetworkDeadBodies)
        {
            var deadBody = entry.Value;
            if (!deadBody.DiedToFire)
            {
                continue;
            }

            // Authoritative corpse already owns this death — don't dual-advance under the synthetic id
            // (that path was capturing the DeadS sprite when the ragdoll had already been re-keyed).
            if (HasAuthoritativeDeadBodyForPlayer(deadBody.SourcePlayerId))
            {
                continue;
            }

            var syntheticId = -Math.Abs(deadBody.SourcePlayerId);
            AdvanceBurnCharredCorpse(
                syntheticId,
                deadBody.SourcePlayerId,
                deadBody.X,
                deadBody.Y,
                deadBody.FacingLeft,
                deadBody.GameplayClassId,
                deadBody.ClassId,
                deadBody.Team,
                deadBody.AnimationKind,
                deadBody.Height,
                horizontalSpeed: 0f,
                verticalSpeed: 0f);
        }
    }

    private bool HasAuthoritativeDeadBodyForPlayer(int sourcePlayerId)
    {
        foreach (var deadBody in _world.DeadBodies)
        {
            if (deadBody.SourcePlayerId == sourcePlayerId)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsActiveBurnCharredCorpseId(int corpseId)
    {
        foreach (var deadBody in _world.DeadBodies)
        {
            if (deadBody.Id == corpseId && deadBody.DiedToFire)
            {
                return true;
            }

            if (-Math.Abs(deadBody.SourcePlayerId) == corpseId && deadBody.DiedToFire)
            {
                return true;
            }
        }

        foreach (var entry in _immediateNetworkDeadBodies)
        {
            if (HasAuthoritativeDeadBodyForPlayer(entry.Value.SourcePlayerId))
            {
                continue;
            }

            if (-Math.Abs(entry.Value.SourcePlayerId) == corpseId && entry.Value.DiedToFire)
            {
                return true;
            }
        }

        return false;
    }

    private void TransferBurnCharredCorpseIdIfNeeded(int deadBodyId, int sourcePlayerId)
    {
        if (deadBodyId <= 0 || _burnCharredCorpseStates.ContainsKey(deadBodyId))
        {
            return;
        }

        var syntheticId = -Math.Abs(sourcePlayerId);
        if (_burnCharredCorpseStates.Remove(syntheticId, out var transferred))
        {
            if (transferred.DissolveCorpseId == 0 || transferred.DissolveCorpseId == syntheticId)
            {
                transferred.DissolveCorpseId = deadBodyId;
            }

            _burnCharredCorpseStates[deadBodyId] = transferred;
        }

        TransferCorpseAcidDissolveIdIfNeeded(deadBodyId, sourcePlayerId);
    }

    private void AdvanceBurnCharredCorpse(
        int corpseId,
        int sourcePlayerId,
        float worldX,
        float worldY,
        bool facingLeft,
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float corpseHeight,
        float horizontalSpeed,
        float verticalSpeed)
    {
        if (!_burnCharredCorpseStates.TryGetValue(corpseId, out var state))
        {
            state = new BurnCharredCorpseState { DissolveCorpseId = corpseId };
            _burnCharredCorpseStates[corpseId] = state;
        }

        if (state.Consumed)
        {
            return;
        }

        state.WorldX = worldX;
        state.WorldY = worldY;
        state.FacingLeft = facingLeft;
        state.GameplayClassId = gameplayClassId;
        state.ClassId = classId;
        state.Team = team;
        state.AnimationKind = animationKind;
        state.CorpseHeight = corpseHeight;
        state.OutlinePhase += 0.18f;
        if (state.DissolveCorpseId == 0)
        {
            state.DissolveCorpseId = corpseId;
        }

        if (state.Dissolving)
        {
            state.DissolveTicksRemaining = Math.Max(0, state.DissolveTicksRemaining - 1);
            var progress = 1f - (state.DissolveTicksRemaining / (float)BurnCharredDissolveTicks);
            if (TryGetBurnCharredDissolveState(state, corpseId, sourcePlayerId, out var dissolve))
            {
                ApplyBurnCharredDissolveProgress(dissolve, progress, state.OutlinePhase);
            }

            if (state.DissolveTicksRemaining <= 0)
            {
                state.Consumed = true;
            }

            return;
        }

        if (!UpdateBurnCharredSettleDwell(corpseId, sourcePlayerId, state, horizontalSpeed, verticalSpeed))
        {
            return;
        }

        // Prefer the live ragdoll pose — same capture path as regular dissolve (which works).
        if (!TryGetDynamicRagdollForAcidCapture(corpseId, sourcePlayerId, out var ragdoll))
        {
            // Keep waiting for the ragdoll; never fall back to DeadS for dynamic corpses.
            if (_gameplayManager.RuntimeSettings.DynamicRagdollEnabled)
            {
                state.SettledDwellTicks = Math.Max(0, state.SettledDwellTicks - 1);
                return;
            }

            if (!TryCaptureBurnCharredStaticDissolve(
                    corpseId,
                    gameplayClassId,
                    classId,
                    team,
                    animationKind,
                    corpseHeight,
                    worldX,
                    worldY))
            {
                return;
            }

            state.DissolveCorpseId = corpseId;
        }
        else if (!TryCaptureBurnCharredRagdollDissolve(corpseId, ragdoll))
        {
            state.SettledDwellTicks = Math.Max(0, state.SettledDwellTicks - 1);
            return;
        }
        else
        {
            state.DissolveCorpseId = corpseId;
        }

        state.Dissolving = true;
        state.DissolveTicksRemaining = BurnCharredDissolveTicks;
        if (TryGetBurnCharredDissolveState(state, corpseId, sourcePlayerId, out var dissolveState))
        {
            ApplyBurnCharredDissolveProgress(dissolveState, progress: 0f, state.OutlinePhase);
        }
    }

    /// <summary>
    /// Returns true once the corpse has rested on the ground long enough to start dissolving.
    /// </summary>
    private bool UpdateBurnCharredSettleDwell(
        int corpseId,
        int sourcePlayerId,
        BurnCharredCorpseState state,
        float horizontalSpeed,
        float verticalSpeed)
    {
        var resting = false;
        if (TryGetDynamicRagdollForAcidCapture(corpseId, sourcePlayerId, out var ragdoll))
        {
            // Settled is ideal, but also accept a planted low-motion pose — Settled can be rare.
            resting = ragdoll.Settled
                || (ragdoll.Grounded
                    && ragdoll.GroundedTicks >= BurnCharredMinGroundedTicks
                    && MathF.Abs(ragdoll.VelocityX) < DynamicRagdollSettledSpeed * 1.5f
                    && MathF.Abs(ragdoll.VelocityY) < DynamicRagdollSettledSpeed * 1.5f
                    && MathF.Abs(ragdoll.AngularVelocityDegrees) < DynamicRagdollSettledAngularSpeed * 1.5f);
        }
        else if (!_gameplayManager.RuntimeSettings.DynamicRagdollEnabled)
        {
            resting = MathF.Abs(horizontalSpeed) <= 0.35f && MathF.Abs(verticalSpeed) <= 0.45f;
        }

        if (!resting)
        {
            state.SettledDwellTicks = 0;
            return false;
        }

        state.SettledDwellTicks = Math.Min(state.SettledDwellTicks + 1, BurnCharredSettleDwellTicks);
        return state.SettledDwellTicks >= BurnCharredSettleDwellTicks;
    }

    /// <summary>
    /// Only freeze physics once burn-dissolve owns the visual — never during normal fall/settle.
    /// </summary>
    private bool IsBurnCharredRagdollHeld(DynamicRagdollState ragdoll)
    {
        if (!_gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled || !ragdoll.DiedToFire)
        {
            return false;
        }

        if (_burnCharredCorpseStates.TryGetValue(ragdoll.DeadBodyId, out var state)
            && (state.Dissolving || state.Consumed))
        {
            return true;
        }

        var syntheticId = -Math.Abs(ragdoll.SourcePlayerId);
        return syntheticId != ragdoll.DeadBodyId
            && _burnCharredCorpseStates.TryGetValue(syntheticId, out state)
            && (state.Dissolving || state.Consumed);
    }

    private bool TryGetBurnCharredDissolveState(
        BurnCharredCorpseState burnState,
        int corpseId,
        int sourcePlayerId,
        out CorpseAcidDissolveState state)
    {
        if (burnState.DissolveCorpseId != 0
            && TryGetCorpseAcidDissolveState(burnState.DissolveCorpseId, out state)
            && state.IsBurnCharred)
        {
            return true;
        }

        if (TryGetCorpseAcidDissolveState(corpseId, out state) && state.IsBurnCharred)
        {
            burnState.DissolveCorpseId = corpseId;
            return true;
        }

        var syntheticId = -Math.Abs(sourcePlayerId);
        if (syntheticId != corpseId
            && TryGetCorpseAcidDissolveState(syntheticId, out state)
            && state.IsBurnCharred)
        {
            burnState.DissolveCorpseId = syntheticId;
            return true;
        }

        state = null!;
        return false;
    }

    private bool TryDrawBurnCharredCorpse(
        int corpseId,
        int sourcePlayerId,
        bool diedToFire,
        float worldX,
        float worldY,
        float corpseHeight,
        bool facingLeft,
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        int ticksRemaining,
        Vector2 cameraPosition)
    {
        if (!_gameplayManager.RuntimeSettings.BurnCharredCorpsesEnabled || !diedToFire)
        {
            return false;
        }

        TransferBurnCharredCorpseIdIfNeeded(corpseId, sourcePlayerId);
        if (_burnCharredCorpseStates.TryGetValue(corpseId, out var state) && state.Consumed)
        {
            return true;
        }

        if (state is { Dissolving: true })
        {
            if (TryGetBurnCharredDissolveState(state, corpseId, sourcePlayerId, out _)
                && TryDrawBurnCharredDissolveState(state.DissolveCorpseId, worldX, worldY, facingLeft, cameraPosition))
            {
                return true;
            }

            // Snapshot missing — keep the live charred ragdoll (never DeadS).
            if (TryGetDynamicRagdollForAcidCapture(corpseId, sourcePlayerId, out var dissolvingRagdoll))
            {
                return DrawBurnCharredLiveRagdoll(dissolvingRagdoll, ticksRemaining, cameraPosition);
            }

            return true;
        }

        if (TryGetDynamicRagdollForAcidCapture(corpseId, sourcePlayerId, out var ragdoll))
        {
            // If regular end-of-life dissolve already froze this ragdoll, step aside so that path draws.
            if (ragdoll.AcidFrozen
                && TryGetCorpseAcidDissolveState(corpseId, out var existing)
                && !existing.IsBurnCharred)
            {
                return false;
            }

            return DrawBurnCharredLiveRagdoll(ragdoll, ticksRemaining, cameraPosition);
        }

        // Dynamic ragdolls on: wait for the ragdoll instead of flashing DeadS.
        if (_gameplayManager.RuntimeSettings.DynamicRagdollEnabled)
        {
            return false;
        }

        return DrawBurnCharredLiveStaticCorpse(
            gameplayClassId,
            classId,
            team,
            animationKind,
            worldX,
            worldY,
            corpseHeight,
            facingLeft,
            ticksRemaining,
            cameraPosition,
            state?.OutlinePhase ?? (corpseId * 0.13f));
    }

    private bool DrawBurnCharredLiveRagdoll(
        DynamicRagdollState ragdoll,
        int ticksRemaining,
        Vector2 cameraPosition)
    {
        var phase = ragdoll.AgeTicks * 0.18f;
        DrawBurnCharredLiveRagdollOutlineCells(ragdoll, cameraPosition, phase);

        // Base corpse, then a translucent soot wash (not a full black replace).
        DrawDynamicRagdollVisual(ragdoll, ticksRemaining, cameraPosition, Color.White);
        var sootOverlay = new Color(0, 0, 0) * 0.68f;
        return DrawDynamicRagdollVisual(ragdoll, ticksRemaining, cameraPosition, sootOverlay);
    }

    /// <summary>
    /// Same 2×2 yellow→red tongue cells as dissolve, projected along the live ragdoll opaque top.
    /// </summary>
    private void DrawBurnCharredLiveRagdollOutlineCells(
        DynamicRagdollState ragdoll,
        Vector2 cameraPosition,
        float phase)
    {
        var opaque = ragdoll.OpaqueBounds;
        if (opaque.Width <= 1 || opaque.Height <= 1)
        {
            return;
        }

        // Opaque-local column tops: meat starts at y=0 after crop.
        Span<int> columnTopY = stackalloc int[opaque.Width];
        columnTopY.Fill(0);

        var seed = ragdoll.SourcePlayerId;
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(new Vector2(ragdoll.X, ragdoll.Y));
        var rootPosition = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y);
        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        var spineMidY = opaque.Top + (opaque.Height * 0.5f);

        Span<BurnOutlineCell> outlineCells = stackalloc BurnOutlineCell[((opaque.Width + 1) / 2) * 3];
        var cellCount = EmitBurnOutlineTongueCells(
            opaque.Width,
            columnTopY,
            phase,
            seed,
            outlineCells);
        for (var i = 0; i < cellCount; i += 1)
        {
            ref readonly var cell = ref outlineCells[i];
            // Texture-space cell center → screen via rigid body approximation.
            var texX = opaque.Left + cell.X + 1f;
            var texY = opaque.Top + cell.Y + 1f;
            var local = new Vector2(texX - (opaque.Left + opaque.Width * 0.5f), texY - spineMidY);
            var screen = rootPosition + TransformRagdollLocal(
                local,
                scaleX,
                ragdoll.RotationDegrees * (MathF.PI / 180f));
            var rect = new Rectangle(
                (int)MathF.Round(screen.X) - 1,
                (int)MathF.Round(screen.Y) - 1,
                2,
                2);
            _spriteBatch.Draw(_pixel, rect, cell.Color);
        }
    }

    private bool DrawBurnCharredLiveStaticCorpse(
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        DeadBodyAnimationKind animationKind,
        float worldX,
        float worldY,
        float corpseHeight,
        bool facingLeft,
        int ticksRemaining,
        Vector2 cameraPosition,
        float outlinePhase)
    {
        var fadeAlpha = GetCorpseFadeAlpha(ticksRemaining);
        if (fadeAlpha <= 0.001f)
        {
            return ticksRemaining <= 0;
        }

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
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(new Vector2(worldX, worldY));
        var drawPosition = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y);
        var corpseOrigin = new Vector2(frame.Width * 0.5f, frame.Height - (corpseHeight * 0.5f));
        var scale = new Vector2(facingLeft ? -1f : 1f, 1f);

        var opaque = frame.OpaqueBounds ?? new Rectangle(0, 0, frame.Width, frame.Height);
        if (opaque.Width > 1 && opaque.Height > 1)
        {
            Span<int> columnTopY = stackalloc int[opaque.Width];
            columnTopY.Fill(0);
            var seed = HashCode.Combine(gameplayClassId, classId);
            Span<BurnOutlineCell> outlineCells = stackalloc BurnOutlineCell[((opaque.Width + 1) / 2) * 3];
            var cellCount = EmitBurnOutlineTongueCells(
                opaque.Width,
                columnTopY,
                outlinePhase,
                seed,
                outlineCells);
            for (var i = 0; i < cellCount; i += 1)
            {
                ref readonly var cell = ref outlineCells[i];
                var local = new Vector2(
                    opaque.Left + cell.X + 1f,
                    opaque.Top + cell.Y + 1f);
                var screen = drawPosition + ((local - corpseOrigin) * scale);
                // Facing flip is in scale.X; keep 2×2 axis-aligned like flame particles.
                var rect = new Rectangle(
                    (int)MathF.Round(screen.X) - 1,
                    (int)MathF.Round(screen.Y) - 1,
                    2,
                    2);
                _spriteBatch.Draw(_pixel, rect, cell.Color * fadeAlpha);
            }
        }

        DrawSpriteFrameWithOptionalShadow(frame, drawPosition, Color.White * fadeAlpha, 0f, corpseOrigin, scale);
        DrawSpriteFrameWithOptionalShadow(frame, drawPosition, new Color(0, 0, 0) * (0.68f * fadeAlpha), 0f, corpseOrigin, scale);
        return true;
    }
}
