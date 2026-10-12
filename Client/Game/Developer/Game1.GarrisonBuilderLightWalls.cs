#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Walls in the builder's lighting preview: the walkmask is read once into the same wall
/// field the game uses, and each light's reach is cached until it moves or resizes, so
/// lights, searchlights and lasers stop at walls in the editor just as they do in game.
/// </summary>
public partial class Game1
{
    private LightOcclusionField? _builderLightOcclusionField;
    private Texture2D? _builderLightOcclusionTexture;
    private Vector4 _builderLightOcclusionKey;
    private readonly Dictionary<int, (float X, float Y, float Radius, float[] Reach)> _builderLightReachCache = new();
    private float[]?[] _builderLightReach = Array.Empty<float[]?>();

    /// <summary>The wall field of the map being edited, or null when it has no walkmask yet.</summary>
    private LightOcclusionField? GetGarrisonBuilderLightOcclusionField()
    {
        var texture = ResolveGarrisonBuilderWalkmaskTexture();
        if (texture is null || texture.IsDisposed || !HasGarrisonBuilderWalkmaskData())
        {
            _builderLightOcclusionField = null;
            _builderLightOcclusionTexture = null;
            _builderLightReachCache.Clear();
            return null;
        }

        var scaleX = CustomMapBuilderDocument.ResolveWalkmaskHorizontalScale(_builderDocument.Metadata, _builderDocument.Scale);
        var scaleY = _builderDocument.Scale;
        var offset = GetGarrisonBuilderWalkmaskWorldOffset();
        var key = new Vector4(scaleX, scaleY, offset.X, offset.Y);
        if (_builderLightOcclusionField is not null
            && ReferenceEquals(texture, _builderLightOcclusionTexture)
            && key == _builderLightOcclusionKey)
        {
            return _builderLightOcclusionField;
        }

        _builderLightOcclusionTexture = texture;
        _builderLightOcclusionKey = key;
        _builderLightReachCache.Clear();
        _builderLightOcclusionField = BuildGarrisonBuilderLightOcclusionField(texture, scaleX, scaleY, offset);
        return _builderLightOcclusionField;
    }

    /// <summary>Solid walkmask pixels (opaque) as rows of wall rectangles in world space.</summary>
    private static LightOcclusionField? BuildGarrisonBuilderLightOcclusionField(Texture2D walkmask, float scaleX, float scaleY, Vector2 offset)
    {
        if (walkmask.Width <= 0 || walkmask.Height <= 0 || scaleX <= 0f || scaleY <= 0f)
        {
            return null;
        }

        var pixels = new Color[walkmask.Width * walkmask.Height];
        try
        {
            walkmask.GetData(pixels);
        }
        catch (Exception)
        {
            return null;
        }

        var solids = new List<LevelSolid>();
        for (var row = 0; row < walkmask.Height; row += 1)
        {
            var column = 0;
            while (column < walkmask.Width)
            {
                if (pixels[(row * walkmask.Width) + column].A < 128)
                {
                    column += 1;
                    continue;
                }

                var start = column;
                while (column < walkmask.Width && pixels[(row * walkmask.Width) + column].A >= 128)
                {
                    column += 1;
                }

                solids.Add(new LevelSolid(
                    offset.X + (start * scaleX),
                    offset.Y + (row * scaleY),
                    (column - start) * scaleX,
                    scaleY));
            }
        }

        var bounds = new WorldBounds(
            MathF.Max(1f, offset.X + (walkmask.Width * scaleX)),
            MathF.Max(1f, offset.Y + (walkmask.Height * scaleY)));
        return LightOcclusionField.Build(solids, bounds);
    }

    /// <summary>Wall reach for every light in the document (by entity index), cached per light.</summary>
    private float[]?[] GetGarrisonBuilderLightReach(LightOcclusionField? field)
    {
        if (_builderLightReach.Length != _builderEntities.Count)
        {
            _builderLightReach = new float[]?[_builderEntities.Count];
        }
        else
        {
            Array.Clear(_builderLightReach);
        }

        if (field is null)
        {
            return _builderLightReach;
        }

        for (var index = 0; index < _builderEntities.Count; index += 1)
        {
            var entity = _builderEntities[index];
            if (!MapLightMetadata.IsLightEntityType(entity.Type))
            {
                continue;
            }

            var light = MapLightMetadata.FromProperties(entity.X, entity.Y, entity.Properties);
            if (_builderLightReachCache.TryGetValue(index, out var cached)
                && cached.X == light.X
                && cached.Y == light.Y
                && cached.Radius == light.Radius)
            {
                _builderLightReach[index] = cached.Reach;
                continue;
            }

            var reach = field.ComputeVisibility(light.X, light.Y, light.Radius);
            _builderLightReachCache[index] = (light.X, light.Y, light.Radius, reach);
            _builderLightReach[index] = reach;
        }

        return _builderLightReach;
    }
}
