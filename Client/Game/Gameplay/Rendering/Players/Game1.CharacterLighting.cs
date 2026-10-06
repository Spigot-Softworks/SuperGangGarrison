#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Map-lighting effects on characters: a rim light on the edges facing the strongest light
/// (RimLight.fx, one flat colour), optional darkening / re-saturation of the body, and a
/// dark silhouette cast away from it.
/// </summary>
/// <remarks>
/// <para>The player renderers bracket a character's body and weapon draws with
/// <see cref="BeginCharacterLighting"/> / <see cref="EndCharacterLighting"/>. While that is
/// open, the sprite primitives (<see cref="DrawSpriteFrame"/>,
/// <see cref="DrawSpriteFrameWithOptionalShadow"/>) report each sprite here: it gets a rim
/// pass, and is recorded for the shadow. Ghosts, overlays and HUD bits drawn outside the
/// bracket are left alone.</para>
/// <para>The cast shadow is drawn from the sprites recorded in the character's previous
/// frame, before anything of the character is drawn, so it falls behind the whole
/// character (weapon included) instead of darkening parts of it. One frame of lag on a
/// silhouette offset by a few pixels is not visible.</para>
/// </remarks>
public partial class Game1
{
    /// <summary>Sky light comes from up and to the right.</summary>
    private static readonly Vector2 SkyCharacterLightDirection = Vector2.Normalize(new Vector2(0.45f, -1f));
    private static readonly Color RimTeamRed = new(232, 72, 56);
    private static readonly Color RimTeamBlue = new(84, 146, 236);

    /// <summary>Uber rim light shines up from below.</summary>
    private static readonly Vector2 UberCharacterLightDirection = new(0f, 1f);

    /// <summary>How dark an übercharged body gets (at least), 0-100.</summary>
    private const int UberBodyShade = 88;

    /// <summary>Uber rim reaches at least round the sides.</summary>
    private const float UberRimWrap = 0.4f;

    /// <summary>Charge fraction below which the uber rim starts flashing, and its rate.</summary>
    private const float UberFlashBelowCharge = 0.25f;
    private const float UberFlashHz = 3f;

    /// <summary>Shadow buffers per drawn character: (group, id), so corpses never share a player's buffer.</summary>
    private readonly Dictionary<(int Group, int Id), CharacterShadowBuffers> _characterShadowBuffers = new();

    /// <summary>Lighting groups: living players, the three kinds of drawn corpse, and gibs.</summary>
    internal const int CharacterLightingPlayerGroup = 0;
    internal const int CharacterLightingRetainedCorpseGroup = 1;
    internal const int CharacterLightingNetworkCorpseGroup = 2;
    internal const int CharacterLightingWorldCorpseGroup = 3;
    internal const int CharacterLightingGibGroup = 4;
    private readonly Dictionary<(Texture2D Texture, Rectangle Rect), int> _spriteArtPixelScales = new();
    private Effect? _rimLightEffect;
    private bool _rimLightEffectLoadAttempted;
    private bool _characterLightingActive;
    private Vector2 _characterLightToLight;
    private Color _characterLightColor;
    private float _characterLightStrength;
    private PlayerTeam _characterLightTeam;
    private bool _characterUberRim;
    private float _characterUberFlash = 1f;
    private float _characterBodyShade;
    private float _characterBodySaturation = 1f;
    private bool _characterBodyAdjust;
    private Vector2 _characterSpriteAnchor;
    private CharacterShadowBuffers? _characterShadowRecording;
    private long _characterLightingFrame;

    private readonly record struct CharacterShadowSprite(
        Texture2D Texture,
        Rectangle? Source,
        Vector2 Offset,
        float Rotation,
        Vector2 Origin,
        Vector2 Scale,
        SpriteEffects Effects,
        float Alpha);

    private sealed class CharacterShadowBuffers
    {
        public List<CharacterShadowSprite> Previous = new();
        public List<CharacterShadowSprite> Current = new();
        public long CurrentFrame = -10;
    }

    /// <summary>True while a character's body and weapons are being drawn with map lighting.</summary>
    internal bool IsCharacterLightingActive => _characterLightingActive;

    /// <summary>True when the map's cast shadow replaces the small built-in sprite drop shadow.</summary>
    internal bool ShouldReplaceSpriteDropShadow =>
        _characterLightingActive && _world.Level.Lighting.CastShadow > 0 && _characterLightStrength > 0.01f;

    /// <summary>Called once per frame when the gameplay light map is built.</summary>
    private void AdvanceCharacterLightingFrame()
    {
        _characterLightingFrame += 1;
        if (_characterShadowBuffers.Count > 64)
        {
            // Forget players who have not been drawn for a while.
            var stale = new List<(int Group, int Id)>();
            foreach (var pair in _characterShadowBuffers)
            {
                if (pair.Value.CurrentFrame < _characterLightingFrame - 120)
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var key in stale)
            {
                _characterShadowBuffers.Remove(key);
            }
        }
    }

    /// <summary>
    /// Opens the lighting bracket for one character and draws its cast shadow. Call right
    /// before the character's weapon backdrop / body / weapon draws.
    /// </summary>
    internal void BeginCharacterLighting(PlayerEntity player, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha) =>
        BeginCharacterLighting((CharacterLightingPlayerGroup, player.Id), player.Team, renderPosition, cameraPosition, visibilityAlpha, player);

    /// <summary>
    /// Opens the lighting bracket for a corpse or ragdoll, so remains get the same rim, body
    /// shade and cast shadow as the living. Close it with <see cref="EndCharacterLighting"/>.
    /// </summary>
    internal void BeginCorpseLighting(int group, int id, PlayerTeam team, Vector2 worldPosition, Vector2 cameraPosition) =>
        BeginCharacterLighting((group, id), team, worldPosition, cameraPosition, 1f, null);

    private void BeginCharacterLighting(
        (int Group, int Id) key,
        PlayerTeam team,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerEntity? player)
    {
        _characterLightingActive = false;
        _characterShadowRecording = null;
        _characterUberRim = false;
        var lighting = _world.Level.Lighting;
        if (!lighting.HasCharacterLighting
            || !IsGameplayLightingPassActive
            || UseReducedBrowserEffects
            || _world.Level.IsTopDown
            || visibilityAlpha <= 0.05f)
        {
            return;
        }

        _characterBodyShade = MapLightingMetadata.Clamp(lighting.BodyShade) / 100f;
        _characterBodySaturation = MapLightingMetadata.ClampScale(lighting.BodySaturation) / 100f;
        _characterBodyAdjust = lighting.HasBodyAdjustment;
        if (player is not null && lighting.UberRim && player.IsUbered && !IsKritzUberWeaponOnlyVisual(player))
        {
            // Uber: a dark silhouette lit hard from below in the team colour.
            _characterUberRim = true;
            _characterUberFlash = GetUberRimFlash(player);
            _characterLightToLight = UberCharacterLightDirection;
            _characterLightColor = player.Team == PlayerTeam.Blue ? RimTeamBlue : RimTeamRed;
            _characterLightStrength = 1f;
            _characterBodyShade = MathF.Max(_characterBodyShade, UberBodyShade / 100f);
            _characterBodyAdjust = true;
        }
        else if (!TryResolveCharacterLight(lighting, renderPosition))
        {
            if (!lighting.HasBodyAdjustment)
            {
                return;
            }

            // No light nearby: no rim or cast shadow, but the body shade still applies so
            // characters look the same in the dark as next to a lamp.
            _characterLightToLight = SkyCharacterLightDirection;
            _characterLightColor = Color.White;
            _characterLightStrength = 0f;
        }

        _characterLightingActive = true;
        _characterLightTeam = team;
        _characterSpriteAnchor = renderPosition - cameraPosition;
        if (!_characterShadowBuffers.TryGetValue(key, out var buffers))
        {
            buffers = new CharacterShadowBuffers();
            _characterShadowBuffers[key] = buffers;
        }

        // Last frame's sprites become the shadow source; record this frame's afresh.
        var previousFrame = buffers.CurrentFrame;
        (buffers.Previous, buffers.Current) = (buffers.Current, buffers.Previous);
        buffers.Current.Clear();
        buffers.CurrentFrame = _characterLightingFrame;
        _characterShadowRecording = buffers;

        // The uber light comes from below, so it casts no shadow on the ground.
        if (lighting.CastShadow > 0 && !_characterUberRim && previousFrame >= _characterLightingFrame - 2)
        {
            DrawCharacterCastShadow(buffers.Previous, lighting, visibilityAlpha);
        }
    }

    internal void EndCharacterLighting()
    {
        _characterLightingActive = false;
        _characterShadowRecording = null;
        _characterUberRim = false;
    }

    /// <summary>
    /// Picks the rim / shadow light for a character from the map's light source setting.
    /// Returns false when there is no light to use (nearest-light mode, nothing nearby).
    /// </summary>
    private bool TryResolveCharacterLight(MapLighting lighting, Vector2 renderPosition)
    {
        var skyColor = new Color(lighting.SkyTint.R, lighting.SkyTint.G, lighting.SkyTint.B);
        if (lighting.RimSource == MapRimLightSource.Sky)
        {
            _characterLightToLight = SkyCharacterLightDirection;
            _characterLightColor = skyColor;
            _characterLightStrength = 1f;
            return true;
        }

        var found = _gameplayLightmap.TryGetDominantLight(
            renderPosition.X,
            renderPosition.Y,
            out var toLight,
            out var lightColor,
            out var lightStrength);
        if (lighting.RimSource != MapRimLightSource.Both)
        {
            _characterLightToLight = toLight;
            _characterLightColor = lightColor;
            _characterLightStrength = lightStrength;
            return found;
        }

        // Lights + sky: a dim rim from the sky everywhere. The closer (stronger) a light is,
        // the more its direction, colour and brightness take over, so walking away from a
        // lamp swings the rim back toward the top.
        var sky = MapLightingMetadata.Clamp(lighting.SkyRim) / 100f;
        var weight = found ? Math.Clamp(lightStrength, 0f, 1f) : 0f;
        var blended = Vector2.Lerp(SkyCharacterLightDirection, found ? toLight : SkyCharacterLightDirection, weight);
        _characterLightToLight = blended.LengthSquared() > 1e-4f
            ? Vector2.Normalize(blended)
            : (weight >= 0.5f ? toLight : SkyCharacterLightDirection);
        _characterLightColor = Color.Lerp(skyColor, found ? lightColor : skyColor, weight);
        _characterLightStrength = MathF.Max(sky, (sky * (1f - weight)) + (Math.Clamp(lightStrength, 0f, 1f) * weight));
        return _characterLightStrength > 0.01f;
    }

    /// <summary>
    /// 1 while the uber has charge to spare; as it runs low the rim pulses between dark and
    /// bright, like TF2's flashing. The charge is the ubering medic's (the player's own, or
    /// the medic healing them).
    /// </summary>
    private float GetUberRimFlash(PlayerEntity player)
    {
        PlayerEntity? medic = player.IsMedicUbering ? player : null;
        if (medic is null)
        {
            foreach (var candidate in EnumerateRenderablePlayers())
            {
                if (candidate.IsMedicUbering && candidate.MedicHealTargetId == player.Id)
                {
                    medic = candidate;
                    break;
                }
            }
        }

        if (medic is null)
        {
            return 1f;
        }

        var charge = GetPlayerMedicUberCharge(medic) / PlayerEntity.MedicUberMaxCharge;
        if (charge >= UberFlashBelowCharge)
        {
            return 1f;
        }

        var wave = MathF.Sin((float)(_weatherClock.Elapsed.TotalSeconds * MathF.Tau * UberFlashHz));
        return 0.6f + (0.4f * wave);
    }

    private void DrawCharacterCastShadow(List<CharacterShadowSprite> sprites, MapLighting lighting, float visibilityAlpha)
    {
        var distance = Math.Clamp(lighting.ShadowDistance, MapLightingMetadata.MinShadowDistance, MapLightingMetadata.MaxShadowDistance);
        // Whole pixels keep the silhouette crisp.
        var offset = new Vector2(
            MathF.Round(-_characterLightToLight.X * distance),
            MathF.Round(-_characterLightToLight.Y * distance));
        if (offset == Vector2.Zero)
        {
            offset = new Vector2(0f, 1f);
        }

        var opacity = lighting.CastShadow / 100f * _characterLightStrength * visibilityAlpha;
        if (opacity <= 0.01f)
        {
            return;
        }

        foreach (var sprite in sprites)
        {
            _spriteBatch.Draw(
                sprite.Texture,
                _characterSpriteAnchor + sprite.Offset + offset,
                sprite.Source,
                Color.Black * (opacity * sprite.Alpha),
                sprite.Rotation,
                sprite.Origin,
                sprite.Scale,
                sprite.Effects,
                0f);
        }
    }

    /// <summary>Sprite primitives call this after drawing a sprite normally.</summary>
    private void ObserveCharacterSpriteDraw(
        Texture2D texture,
        Rectangle? source,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects)
    {
        if (!_characterLightingActive || tint.A == 0)
        {
            return;
        }

        _characterShadowRecording?.Current.Add(new CharacterShadowSprite(
            texture,
            source,
            position - _characterSpriteAnchor,
            rotation,
            origin,
            scale,
            effects,
            tint.A / 255f));

        var lighting = _world.Level.Lighting;
        if (_characterUberRim || (lighting.RimLight > 0 && _characterLightStrength > 0.01f) || _characterBodyAdjust)
        {
            DrawCharacterRim(texture, source, position, tint, rotation, origin, scale, effects);
        }
    }

    private void DrawCharacterRim(
        Texture2D texture,
        Rectangle? source,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects)
    {
        var effect = GetRimLightEffect();
        if (effect is null)
        {
            return;
        }

        var lighting = _world.Level.Lighting;
        var rect = source ?? new Rectangle(0, 0, texture.Width, texture.Height);
        var width = (float)texture.Width;
        var height = (float)texture.Height;

        // The shader works in texture space: undo the sprite's rotation and mirroring.
        var cos = MathF.Cos(-rotation);
        var sin = MathF.Sin(-rotation);
        var toLight = new Vector2(
            (_characterLightToLight.X * cos) - (_characterLightToLight.Y * sin),
            (_characterLightToLight.X * sin) + (_characterLightToLight.Y * cos));
        if (scale.X < 0f)
        {
            toLight.X = -toLight.X;
        }

        if (scale.Y < 0f)
        {
            toLight.Y = -toLight.Y;
        }

        if ((effects & SpriteEffects.FlipHorizontally) != 0)
        {
            toLight.X = -toLight.X;
        }

        if ((effects & SpriteEffects.FlipVertically) != 0)
        {
            toLight.Y = -toLight.Y;
        }

        if (toLight.LengthSquared() < 1e-6f)
        {
            return;
        }

        toLight.Normalize();
        effect.Parameters["LightDirection"]?.SetValue(toLight);
        // Snap to the art's pixel grid; the half width uses a grid half that size (1x sprites
        // have nothing finer than a texel, so they stay at a whole pixel).
        var artScale = GetSpriteArtPixelScale(texture, rect);
        var rimWidth = Math.Clamp(lighting.RimWidth, MapLightingMetadata.MinRimWidth, MapLightingMetadata.MaxRimWidth);
        var grid = rimWidth == MapLightingMetadata.HalfRimWidth ? Math.Max(1, artScale / 2) : artScale;
        effect.Parameters["TexelSize"]?.SetValue(new Vector2(grid / width, grid / height));
        effect.Parameters["RectMin"]?.SetValue(new Vector2(rect.X / width, rect.Y / height));
        effect.Parameters["RectMax"]?.SetValue(new Vector2(rect.Right / width, rect.Bottom / height));
        effect.Parameters["RimColor"]?.SetValue(GetCharacterRimColor(lighting));
        effect.Parameters["RimWidth"]?.SetValue((float)Math.Max(1, rimWidth));
        var wrap = _characterUberRim ? MathF.Max(lighting.RimWrap / 100f, UberRimWrap) : lighting.RimWrap / 100f;
        var (cardinalLimit, diagonalLimit) = GetRimFacingLimits(wrap);
        effect.Parameters["CardinalLimit"]?.SetValue(cardinalLimit);
        effect.Parameters["DiagonalLimit"]?.SetValue(diagonalLimit);
        effect.Parameters["GrazeBand"]?.SetValue(RimGrazeBand);
        // The uber look is always a flat, full-strength rim.
        effect.Parameters["BlendMode"]?.SetValue(_characterUberRim ? 0f : (float)lighting.RimBlend);
        effect.Parameters["RimOpacity"]?.SetValue(_characterUberRim ? 1f : MapLightingMetadata.Clamp(lighting.RimOpacity) / 100f);
        effect.Parameters["BodyShade"]?.SetValue(_characterBodyShade);
        effect.Parameters["BodySaturation"]?.SetValue(_characterBodySaturation);
        effect.Parameters["BodyAdjust"]?.SetValue(_characterBodyAdjust ? 1f : 0f);

        var transform = GetActiveGameplayWorldSpriteBatchTransform();
        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, _gameplayWorldRasterizerState, effect, transform);
        _spriteBatch.Draw(texture, position, source, tint, rotation, origin, scale, effects, 0f);
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _gameplayWorldRasterizerState, transformMatrix: transform);
    }

    /// <summary>
    /// The rim's single flat colour for the character being drawn: the chosen colour dimmed
    /// by the rim amount and how strongly the light reaches the character (a faint light gives
    /// a dim rim rather than a half-transparent one that would mix with the sprite's pixels).
    /// The square root keeps a middling amount from looking muddy. Black means no rim.
    /// </summary>
    private Vector3 GetCharacterRimColor(MapLighting lighting)
    {
        if (_characterUberRim)
        {
            // Super bright team colour, whatever the rim settings; it pulses when the charge is low.
            var team = _characterLightColor.ToVector3();
            var hot = Vector3.Lerp(team, Vector3.One, 0.35f) * 1.15f;
            return Vector3.Clamp(hot * _characterUberFlash, new Vector3(0.004f), Vector3.One);
        }

        var strength = lighting.RimLight / 100f * _characterLightStrength;
        if (strength <= 0.01f)
        {
            return Vector3.Zero;
        }

        var light = _characterLightColor.ToVector3();
        var color = lighting.RimColorMode switch
        {
            // Anything without a team (gibs) takes the light's colour instead.
            MapRimColorMode.Team when _characterLightTeam == PlayerTeam.Blue => RimTeamBlue.ToVector3(),
            MapRimColorMode.Team when _characterLightTeam == PlayerTeam.Red => RimTeamRed.ToVector3(),
            MapRimColorMode.Team => light,
            MapRimColorMode.Custom => new Vector3(
                lighting.ResolvedRimCustomColor.R / 255f,
                lighting.ResolvedRimCustomColor.G / 255f,
                lighting.ResolvedRimCustomColor.B / 255f),
            // Glow: the light's colour pushed toward white so the edge reads as hot.
            MapRimColorMode.Additive => Vector3.Clamp((light * 1.35f) + new Vector3(0.3f), Vector3.Zero, Vector3.One),
            _ => light,
        };

        // A weak light fades the rim toward the blend's "no change" colour: black for
        // normal / add / screen, white for multiply, mid grey for overlay. Kept just above
        // black so the shader still treats it as a rim.
        var amount = MathF.Sqrt(MathF.Min(strength, 1f));
        var neutral = lighting.RimBlend switch
        {
            MapRimBlendMode.Multiply => Vector3.One,
            MapRimBlendMode.Overlay => new Vector3(0.5f),
            _ => Vector3.Zero,
        };
        return Vector3.Max(Vector3.Lerp(neutral, color, amount), new Vector3(0.004f));
    }

    /// <summary>
    /// How many texels make one pixel of the art in a sprite frame: 1 for native art, 2 when
    /// the image stores each art pixel as a 2x2 block (as the stock characters do), and so on
    /// up to 4. The rim snaps to this grid so it is exactly as chunky as the character.
    /// Measured once per frame image (one small texture read) and cached.
    /// </summary>
    private int GetSpriteArtPixelScale(Texture2D texture, Rectangle rect)
    {
        var key = (texture, rect);
        if (_spriteArtPixelScales.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var scale = 1;
        if (rect.Width > 0 && rect.Height > 0 && rect.Width * rect.Height <= 512 * 512)
        {
            try
            {
                var pixels = new Color[rect.Width * rect.Height];
                texture.GetData(0, rect, pixels, 0, pixels.Length);
                scale = MeasureArtPixelScale(pixels, rect.Width, rect.Height);
            }
            catch
            {
                scale = 1; // unreadable texture: fall back to the texture's own grid
            }
        }

        if (_spriteArtPixelScales.Count > 4096)
        {
            _spriteArtPixelScales.Clear(); // e.g. after many texture reloads
        }

        _spriteArtPixelScales[key] = scale;
        return scale;
    }

    /// <summary>Largest scale (4, 3, then 2) at which every aligned block holds one colour; else 1.</summary>
    internal static int MeasureArtPixelScale(Color[] pixels, int width, int height)
    {
        for (var scale = 4; scale >= 2; scale -= 1)
        {
            if (width < scale || height < scale || !IsUniformInBlocks(pixels, width, height, scale))
            {
                continue;
            }

            return scale;
        }

        return 1;
    }

    private static bool IsUniformInBlocks(Color[] pixels, int width, int height, int scale)
    {
        var anySolid = false;
        for (var y = 0; y < height; y += 1)
        {
            var blockTop = y - (y % scale);
            for (var x = 0; x < width; x += 1)
            {
                var pixel = pixels[(y * width) + x];
                if (pixel != pixels[(blockTop * width) + x - (x % scale)])
                {
                    return false;
                }

                anySolid |= pixel.A > 0;
            }
        }

        // A blank frame says nothing about the art.
        return anySolid;
    }

    /// <summary>How far the facing limits relax toward the light-facing end (see RimLight.fx).</summary>
    private const float RimGrazeBand = 0.3f;

    /// <summary>
    /// Cosine limits for which grid neighbours catch the light. A straight side counts within
    /// 67.5 degrees of the light, a diagonal within 22.5 (so a flat edge does not light up
    /// from the side through its corners). Wrap widens both by 45 degrees from 1/3, and by
    /// 90 from 2/3.
    /// </summary>
    internal static (float Cardinal, float Diagonal) GetRimFacingLimits(float wrap)
    {
        if (wrap >= 0.666f)
        {
            return (-0.924f, -0.383f);
        }

        return wrap >= 0.333f ? (-0.383f, 0.383f) : (0.383f, 0.924f);
    }

    private Effect? GetRimLightEffect()
    {
        if (_rimLightEffect is null && !_rimLightEffectLoadAttempted)
        {
            _rimLightEffectLoadAttempted = true;
            try
            {
                _rimLightEffect = Content.Load<Effect>("RimLight");
            }
            catch (Exception ex)
            {
                // Without the shader, characters just go without a rim; shadows still work.
                AddConsoleLine($"rim light unavailable ({ex.GetType().Name}: {ex.Message})");
            }
        }

        return _rimLightEffect;
    }
}
