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

    /// <summary>Shadow buffers per drawn character: (group, id), so corpses never share a player's buffer.</summary>
    private readonly Dictionary<(int Group, int Id), CharacterShadowBuffers> _characterShadowBuffers = new();

    /// <summary>Lighting groups: living players, the three kinds of drawn corpse, and gibs.</summary>
    internal const int CharacterLightingPlayerGroup = 0;
    internal const int CharacterLightingRetainedCorpseGroup = 1;
    internal const int CharacterLightingNetworkCorpseGroup = 2;
    internal const int CharacterLightingWorldCorpseGroup = 3;
    internal const int CharacterLightingGibGroup = 4;
    private Effect? _rimLightEffect;
    private bool _rimLightEffectLoadAttempted;
    private bool _characterLightingActive;
    private Vector2 _characterLightToLight;
    private Color _characterLightColor;
    private float _characterLightStrength;
    private PlayerTeam _characterLightTeam;
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
        BeginCharacterLighting((CharacterLightingPlayerGroup, player.Id), player.Team, renderPosition, cameraPosition, visibilityAlpha);

    /// <summary>
    /// Opens the lighting bracket for a corpse or ragdoll, so remains get the same rim, body
    /// shade and cast shadow as the living. Close it with <see cref="EndCharacterLighting"/>.
    /// </summary>
    internal void BeginCorpseLighting(int group, int id, PlayerTeam team, Vector2 worldPosition, Vector2 cameraPosition) =>
        BeginCharacterLighting((group, id), team, worldPosition, cameraPosition, 1f);

    private void BeginCharacterLighting((int Group, int Id) key, PlayerTeam team, Vector2 renderPosition, Vector2 cameraPosition, float visibilityAlpha)
    {
        _characterLightingActive = false;
        _characterShadowRecording = null;
        var lighting = _world.Level.Lighting;
        if (!lighting.HasCharacterLighting
            || !IsGameplayLightingPassActive
            || UseReducedBrowserEffects
            || _world.Level.IsTopDown
            || visibilityAlpha <= 0.05f)
        {
            return;
        }

        if (lighting.RimFromSky)
        {
            _characterLightToLight = SkyCharacterLightDirection;
            _characterLightColor = new Color(lighting.SkyTint.R, lighting.SkyTint.G, lighting.SkyTint.B);
            _characterLightStrength = 1f;
        }
        else if (!_gameplayLightmap.TryGetDominantLight(
                     renderPosition.X,
                     renderPosition.Y,
                     out _characterLightToLight,
                     out _characterLightColor,
                     out _characterLightStrength))
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

        if (lighting.CastShadow > 0 && previousFrame >= _characterLightingFrame - 2)
        {
            DrawCharacterCastShadow(buffers.Previous, lighting, visibilityAlpha);
        }
    }

    internal void EndCharacterLighting()
    {
        _characterLightingActive = false;
        _characterShadowRecording = null;
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
        if ((lighting.RimLight > 0 && _characterLightStrength > 0.01f) || lighting.HasBodyAdjustment)
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
        effect.Parameters["TexelSize"]?.SetValue(new Vector2(1f / width, 1f / height));
        effect.Parameters["RectMin"]?.SetValue(new Vector2(rect.X / width, rect.Y / height));
        effect.Parameters["RectMax"]?.SetValue(new Vector2(rect.Right / width, rect.Bottom / height));
        effect.Parameters["RimColor"]?.SetValue(GetCharacterRimColor(lighting));
        effect.Parameters["RimWidth"]?.SetValue((float)Math.Clamp(lighting.RimWidth, MapLightingMetadata.MinRimWidth, MapLightingMetadata.MaxRimWidth));
        effect.Parameters["RimWrap"]?.SetValue(lighting.RimWrap / 100f);
        effect.Parameters["BodyShade"]?.SetValue(MapLightingMetadata.Clamp(lighting.BodyShade) / 100f);
        effect.Parameters["BodySaturation"]?.SetValue(MapLightingMetadata.ClampScale(lighting.BodySaturation) / 100f);
        effect.Parameters["BodyAdjust"]?.SetValue(lighting.HasBodyAdjustment ? 1f : 0f);

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

        // Kept just above black so the shader still treats it as a rim.
        return Vector3.Max(color * MathF.Sqrt(MathF.Min(strength, 1f)), new Vector3(0.004f));
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
