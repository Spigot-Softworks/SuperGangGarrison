#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    // Temporary Elkondo corpse art: 5th run-cycle pose (run.Frames[4] → usually pose 5).
    // TODO(dynamic-ragdoll): replace this temporary run-frame corpse with unique Elkondo Dead sprites
    // once those are authored. Keep the vertical legs/body joint topology, torso-over-legs draw order,
    // per-class waist pivots, and flapping attached weapon.
    private const int DynamicRagdollElkondoRunCorpseClipIndex = 4;
    private const int ElkondoPivotChest = 0;
    private const int ElkondoPivotWaist = 1;
    private const int ElkondoPivotKnee = 2;
    private const float ElkondoChestMaxPivotDegrees = 40f;
    private const float ElkondoWaistMaxPivotDegrees = 75f;
    private const float ElkondoKneeMaxPivotDegrees = 120f;

    private bool UsesElkondoRagdollVisual()
        => _spriteStyle == PlayerSpriteStyle.Elkondo;

    private static void GetElkondoPivotLimits(DynamicRagdollState ragdoll, int pivotIndex, out float minDegrees, out float maxDegrees)
    {
        switch (pivotIndex)
        {
            case ElkondoPivotChest:
                minDegrees = -ElkondoChestMaxPivotDegrees;
                maxDegrees = ElkondoChestMaxPivotDegrees;
                return;
            case ElkondoPivotWaist:
                minDegrees = -ElkondoWaistMaxPivotDegrees;
                maxDegrees = ElkondoWaistMaxPivotDegrees;
                return;
            case ElkondoPivotKnee:
                // Knee only bends backward (respects the joint). Screen-clockwise is +degrees;
                // for a left-facing sprite backward is the opposite screen direction.
                if (ragdoll.FacingLeft)
                {
                    minDegrees = -ElkondoKneeMaxPivotDegrees;
                    maxDegrees = 0f;
                }
                else
                {
                    minDegrees = 0f;
                    maxDegrees = ElkondoKneeMaxPivotDegrees;
                }

                return;
            default:
                minDegrees = -DynamicRagdollMaxPivotDegrees;
                maxDegrees = DynamicRagdollMaxPivotDegrees;
                return;
        }
    }

    private static float ClampElkondoPivotDegrees(DynamicRagdollState ragdoll, int pivotIndex, float degrees)
    {
        GetElkondoPivotLimits(ragdoll, pivotIndex, out var minDegrees, out var maxDegrees);
        return Math.Clamp(degrees, minDegrees, maxDegrees);
    }

    private bool TryResolveElkondoCorpseSprite(
        string gameplayClassId,
        PlayerClass classId,
        PlayerTeam team,
        out string spriteName,
        out int frameIndex,
        out PlayerSkinDefinition? skin)
    {
        spriteName = string.Empty;
        frameIndex = 0;
        skin = _playerSkins.Value.Find(
            string.IsNullOrWhiteSpace(gameplayClassId) ? classId.ToString().ToLowerInvariant() : gameplayClassId,
            team,
            PlayerSpriteStyle.Elkondo.ToString());
        if (skin is null
            && !string.IsNullOrWhiteSpace(gameplayClassId))
        {
            skin = _playerSkins.Value.Find(classId.ToString().ToLowerInvariant(), team, PlayerSpriteStyle.Elkondo.ToString());
        }

        if (skin is null || !skin.Clips.TryGetValue("run", out var runClip) || runClip.Frames.Length == 0)
        {
            return false;
        }

        var clipIndex = Math.Clamp(DynamicRagdollElkondoRunCorpseClipIndex, 0, runClip.Frames.Length - 1);
        frameIndex = runClip.Frames[clipIndex];
        spriteName = skin.SpriteForTeam(skin.BodySprite, team);
        return !string.IsNullOrWhiteSpace(spriteName);
    }

    /// <summary>
    /// Waist cut as a fraction down the opaque body (0 = head, 1 = feet).
    /// Tuned so the joint sits where legs tuck under / overlap the torso.
    /// </summary>
    private static float GetElkondoWaistFraction(PlayerClass classId)
        => classId switch
        {
            PlayerClass.Scout => 0.58f,
            PlayerClass.Soldier => 0.54f,
            PlayerClass.Pyro => 0.56f,
            PlayerClass.Demoman => 0.55f,
            PlayerClass.Heavy => 0.64f,
            PlayerClass.Medic => 0.57f,
            PlayerClass.Engineer => 0.56f,
            PlayerClass.Sniper => 0.54f,
            PlayerClass.Spy => 0.55f,
            _ => 0.56f,
        };

    private bool DrawElkondoRagdollVisual(DynamicRagdollState ragdoll, int ticksRemaining, Vector2 cameraPosition)
    {
        var fadeAlpha = GetCorpseFadeAlpha(ticksRemaining);
        if (fadeAlpha <= 0.001f)
        {
            return ticksRemaining <= 0;
        }

        if (!TryResolveElkondoCorpseSprite(
                ragdoll.GameplayClassId,
                ragdoll.ClassId,
                ragdoll.Team,
                out var spriteName,
                out var frameIndex,
                out _))
        {
            return false;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        frameIndex = Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        var frame = sprite.Frames[frameIndex];
        var opaque = ragdoll.OpaqueBounds;
        if (opaque.Width <= 1 || opaque.Height <= 1)
        {
            opaque = frame.OpaqueBounds ?? new Rectangle(0, 0, frame.Width, frame.Height);
        }

        var scaleX = ragdoll.FacingLeft ? -1f : 1f;
        var tint = Color.White * fadeAlpha;
        var roundedOrigin = GetRoundedPlayerSpriteOrigin(new Vector2(ragdoll.X, ragdoll.Y));
        var rootPosition = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y);
        var baseSource = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Width, frame.Height);
        var bodyRotationRadians = ragdoll.RotationDegrees * (MathF.PI / 180f);

        // Vertical spine: head → chest mid → waist (legs/body overlap) → knee mid → feet.
        // Pivot[0]=chest (±30°), Pivot[1]=waist (±75°), Pivot[2]=knee (0..120° backward only).
        var waistFraction = GetElkondoWaistFraction(ragdoll.ClassId);
        var chestFraction = Math.Clamp(waistFraction * 0.48f, 0.16f, waistFraction - 0.10f);
        var kneeFraction = Math.Clamp(waistFraction + ((1f - waistFraction) * 0.50f), waistFraction + 0.10f, 0.90f);
        Span<float> cutFractions = stackalloc float[]
        {
            0f,
            chestFraction,
            waistFraction,
            kneeFraction,
            1f,
        };

        Span<int> cutYs = stackalloc int[cutFractions.Length];
        for (var index = 0; index < cutFractions.Length; index += 1)
        {
            cutYs[index] = opaque.Top + Math.Clamp(
                (int)MathF.Round(opaque.Height * cutFractions[index]),
                0,
                opaque.Height);
        }

        for (var index = 1; index < cutYs.Length; index += 1)
        {
            if (cutYs[index] <= cutYs[index - 1])
            {
                cutYs[index] = Math.Min(opaque.Bottom, cutYs[index - 1] + 1);
            }
        }

        // Walk head→feet to resolve joint world positions, then draw legs first so torso stays on top.
        Span<Vector2> jointPositions = stackalloc Vector2[cutYs.Length];
        Span<float> segmentRotations = stackalloc float[cutYs.Length - 1];
        // Place head tip relative to entity center along the unbent spine.
        var cursor = rootPosition + TransformRagdollLocal(
            new Vector2(0f, -opaque.Height * 0.5f),
            scaleX,
            bodyRotationRadians);
        jointPositions[0] = cursor;
        var cumulativePivot = 0f;
        for (var segmentIndex = 0; segmentIndex < cutYs.Length - 1; segmentIndex += 1)
        {
            var segmentHeight = cutYs[segmentIndex + 1] - cutYs[segmentIndex];
            var rotationDegrees = ragdoll.RotationDegrees + cumulativePivot;
            segmentRotations[segmentIndex] = rotationDegrees;
            var rotationRadians = rotationDegrees * (MathF.PI / 180f);
            cursor += TransformRagdollLocal(new Vector2(0f, segmentHeight), scaleX, rotationRadians);
            jointPositions[segmentIndex + 1] = cursor;
            if (segmentIndex < DynamicRagdollPivotCount)
            {
                cumulativePivot += ragdoll.PivotDegrees[segmentIndex];
            }
        }

        const int waistPivotIndex = ElkondoPivotWaist;
        // Legs first so torso/head stay on top at the waist overlap.
        for (var segmentIndex = waistPivotIndex + 1; segmentIndex < cutYs.Length - 1; segmentIndex += 1)
        {
            DrawElkondoRagdollSegment(
                frame,
                baseSource,
                opaque,
                cutYs[segmentIndex],
                cutYs[segmentIndex + 1],
                jointPositions[segmentIndex],
                segmentRotations[segmentIndex],
                scaleX,
                tint);
        }

        // Knee + waist seam fills (rigid segments; only the seam pixels stretch into the wedge).
        DrawElkondoPivotSeamFill(
            frame,
            baseSource,
            opaque,
            cutYs[ElkondoPivotKnee + 1],
            jointPositions[ElkondoPivotKnee + 1],
            segmentRotations[ElkondoPivotKnee],
            segmentRotations[Math.Min(ElkondoPivotKnee + 1, segmentRotations.Length - 1)],
            ragdoll.PivotDegrees[ElkondoPivotKnee],
            scaleX,
            tint);
        DrawElkondoPivotSeamFill(
            frame,
            baseSource,
            opaque,
            cutYs[waistPivotIndex + 1],
            jointPositions[waistPivotIndex + 1],
            segmentRotations[waistPivotIndex],
            segmentRotations[Math.Min(waistPivotIndex + 1, segmentRotations.Length - 1)],
            ragdoll.PivotDegrees[waistPivotIndex],
            scaleX,
            tint);

        // Lower torso (chest→waist), chest seam, then head chunk on top.
        DrawElkondoRagdollSegment(
            frame,
            baseSource,
            opaque,
            cutYs[1],
            cutYs[2],
            jointPositions[1],
            segmentRotations[1],
            scaleX,
            tint);
        DrawElkondoPivotSeamFill(
            frame,
            baseSource,
            opaque,
            cutYs[ElkondoPivotChest + 1],
            jointPositions[ElkondoPivotChest + 1],
            segmentRotations[ElkondoPivotChest],
            segmentRotations[Math.Min(ElkondoPivotChest + 1, segmentRotations.Length - 1)],
            ragdoll.PivotDegrees[ElkondoPivotChest],
            scaleX,
            tint);
        DrawElkondoRagdollSegment(
            frame,
            baseSource,
            opaque,
            cutYs[0],
            cutYs[1],
            jointPositions[0],
            segmentRotations[0],
            scaleX,
            tint);

        DrawElkondoRagdollWeapon(
            ragdoll,
            rootPosition,
            // Follow the torso strip (after chest pivot) so the grip stays on the body meat.
            segmentRotations[Math.Min(1, segmentRotations.Length - 1)],
            scaleX,
            tint);
        return true;
    }

    private void DrawElkondoRagdollSegment(
        LoadedSpriteFrame frame,
        Rectangle baseSource,
        Rectangle opaque,
        int segmentTop,
        int segmentBottom,
        Vector2 jointPosition,
        float rotationDegrees,
        float scaleX,
        Color tint)
    {
        var segmentHeight = segmentBottom - segmentTop;
        if (segmentHeight <= 0)
        {
            return;
        }

        var segmentSource = new Rectangle(
            baseSource.X + opaque.Left,
            baseSource.Y + segmentTop,
            opaque.Width,
            segmentHeight);
        var segmentFrame = new LoadedSpriteFrame(
            frame.Texture,
            SourceRectangle: segmentSource,
            OwnsTexture: false,
            OpaqueBounds: null,
            PixelSource: null);
        // Origin at the top-center of the strip (head-ward joint).
        var origin = new Vector2(opaque.Width * 0.5f, 0f);
        DrawSpriteFrameWithOptionalShadow(
            segmentFrame,
            jointPosition,
            tint,
            rotationDegrees * (MathF.PI / 180f),
            origin,
            new Vector2(scaleX, 1f));
    }

    /// <summary>
    /// Stretch a 1–2px seam row into the open wedge at a pivot. Segments stay rigid.
    /// </summary>
    private void DrawElkondoPivotSeamFill(
        LoadedSpriteFrame frame,
        Rectangle baseSource,
        Rectangle opaque,
        int pivotTextureY,
        Vector2 jointPosition,
        float previousRotationDegrees,
        float nextRotationDegrees,
        float pivotDegrees,
        float scaleX,
        Color tint)
    {
        var absPivot = MathF.Abs(pivotDegrees);
        if (absPivot < 0.6f)
        {
            return;
        }

        var seamTop = Math.Clamp(pivotTextureY - 1, opaque.Top, opaque.Bottom - 1);
        var seamHeight = Math.Min(2, opaque.Bottom - seamTop);
        if (seamHeight <= 0)
        {
            return;
        }

        var openRadians = absPivot * (MathF.PI / 180f);
        // Stronger stretch on big bends so chest/knee gaps don't hollow out.
        var stretchAlongSpine = 1f + (openRadians * 1.15f);
        var bisectorDegrees = previousRotationDegrees + (pivotDegrees * 0.5f);
        var seamSource = new Rectangle(
            baseSource.X + opaque.Left,
            baseSource.Y + seamTop,
            opaque.Width,
            seamHeight);
        var seamFrame = new LoadedSpriteFrame(
            frame.Texture,
            SourceRectangle: seamSource,
            OwnsTexture: false,
            OpaqueBounds: null,
            PixelSource: null);
        DrawSpriteFrameWithOptionalShadow(
            seamFrame,
            jointPosition,
            tint,
            bisectorDegrees * (MathF.PI / 180f),
            new Vector2(opaque.Width * 0.5f, seamHeight * 0.5f),
            new Vector2(scaleX, stretchAlongSpine));
        _ = nextRotationDegrees;
    }

    private void DrawElkondoRagdollWeapon(
        DynamicRagdollState ragdoll,
        Vector2 bodyRootScreen,
        float torsoRotationDegrees,
        float scaleX,
        Color tint)
    {
        if (string.IsNullOrWhiteSpace(ragdoll.WeaponSpriteName))
        {
            return;
        }

        var sprite = GetResolvedSprite(ragdoll.WeaponSpriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var frameIndex = Math.Clamp(ragdoll.WeaponFrameIndex, 0, sprite.Frames.Count - 1);
        var frame = sprite.Frames[frameIndex];
        var torsoRadians = torsoRotationDegrees * (MathF.PI / 180f);
        // Companion-torso overlays (e.g. Whipping Cord) pin at body+weaponOffset with
        // the sprite origin as draw origin. Stock weapons pin at body+(offset+origin).
        var attachLocal = new Vector2(ragdoll.WeaponAttachLocalX, ragdoll.WeaponAttachLocalY);
        var attachWorld = bodyRootScreen + TransformRagdollLocal(attachLocal, scaleX, torsoRadians);
        DrawSpriteFrameWithOptionalShadow(
            frame,
            attachWorld,
            tint,
            torsoRadians,
            ragdoll.WeaponOrigin,
            new Vector2(scaleX, 1f));
    }

    private void AdvanceElkondoRagdollWeapon(DynamicRagdollState ragdoll)
    {
        // Weapon is rigidly stuck to the torso pivot — no free flap.
        ragdoll.WeaponFlapDegrees = 0f;
        ragdoll.WeaponFlapVelocityDegrees = 0f;
    }

    // Visual-only; uses the same on-player sprite pivot. Never feeds collision.
    private void TryCaptureElkondoRagdollWeapon(DynamicRagdollState ragdoll, PlayerEntity? player)
    {
        if (player is null)
        {
            return;
        }

        var weaponDefinition = GetWeaponRenderDefinition(player);
        if (weaponDefinition.NormalSpriteName is null)
        {
            return;
        }

        // Full torso replacements are the body upper half, not a detachable overlay.
        if (weaponDefinition.IsFullTorsoReplacement)
        {
            return;
        }

        var sprite = GetResolvedSprite(weaponDefinition.NormalSpriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        ragdoll.WeaponSpriteName = weaponDefinition.NormalSpriteName;
        // Idle team frame (Blue = offset half) — PoseFrameIndex alone is always the red/skin pose.
        ragdoll.WeaponFrameIndex = GetWeaponSpriteFrameIndex(
            player,
            WeaponAnimationMode.Idle,
            weaponDefinition,
            sprite.Frames.Count);
        var anchorOrigin = GetWeaponAnchorOrigin(weaponDefinition, sprite);
        ragdoll.WeaponOrigin = anchorOrigin;
        if (weaponDefinition.HasCompanionTorso)
        {
            // Companion whip/torso layers draw at body origin + weaponOffset with the
            // sprite origin as the MonoGame draw origin (same as live companion draw).
            ragdoll.WeaponAttachLocalX = weaponDefinition.XOffset;
            ragdoll.WeaponAttachLocalY = weaponDefinition.YOffset;
        }
        else
        {
            // Match TryGetWeaponDrawTransform: world pivot = bodyOrigin + (XOffset+origin.X, YOffset+origin.Y).
            ragdoll.WeaponAttachLocalX = weaponDefinition.XOffset + anchorOrigin.X;
            ragdoll.WeaponAttachLocalY = weaponDefinition.YOffset + anchorOrigin.Y;
        }

        ragdoll.WeaponFlapDegrees = 0f;
        ragdoll.WeaponFlapVelocityDegrees = 0f;
    }
}
