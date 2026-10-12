#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Character brightmaps (experimental): parts of a sprite marked in its brightmap keep some
/// of their own colour when the character stands in the dark, for a soft sheen on metal and
/// a muzzle flash that lights up even where the map is black.
/// </summary>
/// <remarks>
/// <para>The look is the sprite multiplied by its brightmap, faded by how dark it is where
/// the character stands: in full darkness the brightmap shows completely, in full light not
/// at all. It is added over the lit character after the light map, so it never makes a
/// character darker than lighting alone would.</para>
/// <para>Anything drawn over a character after it (another player, the map's foreground,
/// weather) must hide its brightmap, yet the brightmaps are drawn later, after the whole
/// world has been lit. The world canvas's alpha channel carries that: as each brightmapped
/// sprite is drawn its pixels are marked (alpha cleared), anything opaque drawn on top
/// unmarks them again (ordinary blending writes alpha back), and the glow only lands where
/// the mark survived. Glows are drawn front to back, each re-claiming its pixels as it goes,
/// so overlapping parts of one character (the gun over the torso) never add up, and the
/// canvas's alpha is whole again afterwards.</para>
/// </remarks>
public partial class Game1
{
    /// <summary>Clears destination alpha where the sprite is (marks its pixels as brightmapped).</summary>
    private static readonly BlendState BrightmapMarkBlend = new()
    {
        Name = "OpenGarrison.Brightmap.Mark",
        ColorWriteChannels = ColorWriteChannels.Alpha,
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.InverseSourceAlpha,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>Adds the glow only where pixels are still marked (destination alpha cleared).</summary>
    private static readonly BlendState BrightmapGlowBlend = new()
    {
        Name = "OpenGarrison.Brightmap.Glow",
        ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue,
        ColorSourceBlend = Blend.InverseDestinationAlpha,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>Writes destination alpha back where the sprite is (claims its pixels).</summary>
    private static readonly BlendState BrightmapClaimBlend = new()
    {
        Name = "OpenGarrison.Brightmap.Claim",
        ColorWriteChannels = ColorWriteChannels.Alpha,
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.InverseSourceAlpha,
        AlphaBlendFunction = BlendFunction.Add,
    };

    /// <summary>Below this, a character is lit enough that its brightmap would not show.</summary>
    private const float MinBrightmapStrength = 0.02f;

    private readonly record struct BrightmapSpriteDraw(
        Texture2D Glow,
        Vector2 Position,
        Color Tint,
        float Rotation,
        Vector2 Origin,
        Vector2 Scale,
        SpriteEffects Effects,
        float Strength);

    private SpriteBrightmapLibrary? _spriteBrightmaps;
    private readonly List<BrightmapSpriteDraw> _brightmapDraws = new();
    private bool _characterBrightmapsActive;
    private float _characterBrightmapStrength;

    private SpriteBrightmapLibrary SpriteBrightmaps =>
        _spriteBrightmaps ??= new SpriteBrightmapLibrary(AddConsoleLine);

    /// <summary>Called whenever a sprite is resolved by name, so its brightmaps are ready before it draws.</summary>
    private void ObserveResolvedSpriteForBrightmaps(string spriteName, LoadedGameMakerSprite? sprite)
    {
        if (sprite is not null && !OperatingSystem.IsBrowser())
        {
            SpriteBrightmaps.Observe(GraphicsDevice, spriteName, sprite);
        }
    }

    /// <summary>
    /// Opens brightmaps for one character (from <see cref="BeginCharacterLighting"/>): how
    /// strongly they show follows how dark it is where the character stands.
    /// </summary>
    private void BeginCharacterBrightmaps(Vector2 renderPosition, float visibilityAlpha)
    {
        _characterBrightmapsActive = false;
        if (!_gameplayManager.RuntimeSettings.CharacterBrightmapsEnabled
            || !_gameplayManager.RuntimeSettings.MapLightingEnabled
            || !IsGameplayLightingPassActive
            || !_gameplayLightmap.HasFrame
            || UseReducedBrowserEffects
            || visibilityAlpha <= 0.05f
            || !SpriteBrightmaps.HasAny)
        {
            return;
        }

        var brightness = _gameplayLightmap.EstimateBrightness(renderPosition.X, renderPosition.Y);
        _characterBrightmapStrength = Math.Clamp(1f - brightness, 0f, 1f);
        _characterBrightmapsActive = _characterBrightmapStrength > MinBrightmapStrength;
    }

    private void EndCharacterBrightmaps() => _characterBrightmapsActive = false;

    /// <summary>
    /// After a character sprite is drawn (and rim-lit): if its frame has a brightmap, marks
    /// its pixels and queues the glow for after the light map.
    /// </summary>
    private void ObserveBrightmapSpriteDraw(
        Texture2D texture,
        Rectangle? source,
        Vector2 position,
        Color tint,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects)
    {
        if (!_characterBrightmapsActive
            || tint.A == 0
            || _spriteBrightmaps is null
            || !_spriteBrightmaps.TryGetGlow(texture, source, out var glow))
        {
            return;
        }

        _brightmapDraws.Add(new BrightmapSpriteDraw(glow, position, tint, rotation, origin, scale, effects, _characterBrightmapStrength));

        var transform = GetActiveGameplayWorldSpriteBatchTransform();
        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BrightmapMarkBlend, SamplerState.PointClamp, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
        _spriteBatch.Draw(glow, position, null, tint, rotation, origin, scale, effects, 0f);
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _gameplayWorldRasterizerState, transformMatrix: transform);
    }

    /// <summary>
    /// Adds this pass's queued glows over the lit world (no sprite batch open), front to back,
    /// and leaves the canvas's alpha whole again.
    /// </summary>
    private void DrawCharacterBrightmaps(Matrix? transform)
    {
        if (_brightmapDraws.Count == 0)
        {
            return;
        }

        for (var index = _brightmapDraws.Count - 1; index >= 0; index -= 1)
        {
            var draw = _brightmapDraws[index];
            var glowTint = new Color(
                (byte)(draw.Tint.R * draw.Strength),
                (byte)(draw.Tint.G * draw.Strength),
                (byte)(draw.Tint.B * draw.Strength),
                draw.Tint.A);
            _spriteBatch.Begin(SpriteSortMode.Deferred, BrightmapGlowBlend, SamplerState.PointClamp, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
            _spriteBatch.Draw(draw.Glow, draw.Position, null, glowTint, draw.Rotation, draw.Origin, draw.Scale, draw.Effects, 0f);
            _spriteBatch.End();

            _spriteBatch.Begin(SpriteSortMode.Deferred, BrightmapClaimBlend, SamplerState.PointClamp, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
            _spriteBatch.Draw(draw.Glow, draw.Position, null, draw.Tint, draw.Rotation, draw.Origin, draw.Scale, draw.Effects, 0f);
            _spriteBatch.End();
        }

        _brightmapDraws.Clear();
    }

    /// <summary>
    /// Drops queued glows when a pass ends without lighting. Their marks are claimed back
    /// so the canvas alpha stays whole. Needs the world batch open.
    /// </summary>
    private void DiscardCharacterBrightmaps()
    {
        if (_brightmapDraws.Count == 0)
        {
            return;
        }

        var transform = GetActiveGameplayWorldSpriteBatchTransform();
        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BrightmapClaimBlend, SamplerState.PointClamp, DepthStencilState.None, _gameplayWorldRasterizerState, null, transform);
        foreach (var draw in _brightmapDraws)
        {
            _spriteBatch.Draw(draw.Glow, draw.Position, null, draw.Tint, draw.Rotation, draw.Origin, draw.Scale, draw.Effects, 0f);
        }

        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: _gameplayWorldRasterizerState, transformMatrix: transform);
        _brightmapDraws.Clear();
    }
}
