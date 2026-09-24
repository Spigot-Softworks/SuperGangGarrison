#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<int, DynamicGibVisual> _dynamicGibVisuals = new();
    private readonly Dictionary<int, CachedDynamicGibBodyFrame> _cachedDynamicGibBodyFrames = new();
    private readonly Dictionary<int, CachedDynamicGibWeaponFrame> _cachedDynamicGibWeaponFrames = new();
    private readonly List<int> _staleDynamicGibVisualIds = new();
    private int _nextDynamicGibVisualId = 1;
    private readonly Random _dynamicGibRandom = new();

    private sealed class DynamicGibVisual
    {
        public DynamicGibVisual(Texture2D texture, bool ownsTexture)
        {
            Texture = texture;
            OwnsTexture = ownsTexture;
        }

        public Texture2D Texture { get; }
        public bool OwnsTexture { get; }
    }

    private readonly record struct CachedDynamicGibBodyFrame(
        string SpriteName,
        int FrameIndex,
        Vector2 RenderPosition,
        Vector2 Scale,
        Vector2 Origin,
        float BodyYOffset);

    private readonly record struct CachedDynamicGibWeaponFrame(
        string SpriteName,
        int FrameIndex,
        float WorldDrawX,
        float WorldDrawY,
        float RotationRadians,
        float FacingScale,
        float PlayerScale,
        Vector2 Origin);

    private bool TryHandleDynamicPlayerGibSpawn(PlayerEntity player, float spawnX, float spawnY)
    {
        if (_gibRenderMode != 1 || !AreGibVisualsEnabled)
        {
            return false;
        }

        return TrySpawnDynamicPlayerGibs(player, spawnX, spawnY);
    }

    private void RecordDynamicGibBodyFrame(
        PlayerEntity player,
        string spriteName,
        int frameIndex,
        Vector2 renderPosition,
        Vector2 origin,
        Vector2 scale,
        float bodyYOffset)
    {
        if (_gibRenderMode != 1)
        {
            return;
        }

        _cachedDynamicGibBodyFrames[player.Id] = new CachedDynamicGibBodyFrame(
            spriteName,
            frameIndex,
            renderPosition,
            scale,
            origin,
            bodyYOffset);
    }

    private void RecordDynamicGibWeaponFrame(
        PlayerEntity player,
        string spriteName,
        int frameIndex,
        float worldDrawX,
        float worldDrawY,
        float rotationRadians,
        float facingScale,
        float playerScale,
        Vector2 origin)
    {
        if (_gibRenderMode != 1)
        {
            return;
        }

        _cachedDynamicGibWeaponFrames[player.Id] = new CachedDynamicGibWeaponFrame(
            spriteName,
            frameIndex,
            worldDrawX,
            worldDrawY,
            rotationRadians,
            facingScale,
            playerScale,
            origin);
    }

    private bool TrySpawnDynamicPlayerGibs(PlayerEntity player, float spawnX, float spawnY)
    {
        if (!TryResolveDynamicBodyFrame(player, spawnX, spawnY, out var bodyFrame, out var bodySpriteFrame))
        {
            return false;
        }

        if (!TryGetSpriteFramePixels(bodySpriteFrame, out var bodyPixels, out var bodyWidth, out var bodyHeight))
        {
            return false;
        }

        var scaleX = bodyFrame.Scale.X;
        var bodySpriteOrigin = bodyFrame.Origin;
        if (scaleX < 0f)
        {
            FlipPixelsHorizontally(bodyPixels, bodyWidth, bodyHeight);
            bodySpriteOrigin = new Vector2(bodyWidth - 1f - bodySpriteOrigin.X, bodySpriteOrigin.Y);
            scaleX = MathF.Abs(scaleX);
        }

        var scaleY = MathF.Abs(bodyFrame.Scale.Y);
        if (scaleY <= 0.001f)
        {
            scaleY = 1f;
        }

        if (scaleX <= 0.001f)
        {
            scaleX = scaleY;
        }

        var chunkCount = ResolveDynamicBodyChunkCount();
        if (!TryBuildVoronoiChunks(
                bodyPixels,
                bodyWidth,
                bodyHeight,
                chunkCount,
                experimentalCryoTinted: player.IsExperimentalCryoFrozen,
                out var chunks))
        {
            return false;
        }

        var inheritedVelocityX = player.HorizontalSpeed * (float)_world.Config.FixedDeltaSeconds;
        var inheritedVelocityY = player.VerticalSpeed * (float)_world.Config.FixedDeltaSeconds;
        var experimentalCryoTinted = player.IsExperimentalCryoFrozen;
        // Body draw uses rounded player origin + body Y offset. Spawn offsets are relative to that.
        var bodyWorldOriginX = spawnX;
        var bodyWorldOriginY = spawnY + bodyFrame.BodyYOffset;
        var isTopDown = _world.Level.IsTopDown;

        for (var index = 0; index < chunks.Count; index += 1)
        {
            var chunk = chunks[index];
            var texture = CreateDynamicGibTexture(chunk.Pixels, chunk.Width, chunk.Height);
            if (texture is null)
            {
                continue;
            }

            var visualId = AllocateDynamicGibVisualId();
            _dynamicGibVisuals[visualId] = new DynamicGibVisual(texture, ownsTexture: true);

            var localOffsetX = (chunk.CentroidX - bodySpriteOrigin.X) * scaleX;
            var localOffsetY = (chunk.CentroidY - bodySpriteOrigin.Y) * scaleY;
            var gibX = bodyWorldOriginX + localOffsetX;
            var gibY = bodyWorldOriginY + localOffsetY;

            BuildDynamicGibLaunchVelocity(
                localOffsetX,
                localOffsetY,
                inheritedVelocityX,
                inheritedVelocityY,
                isTopDown,
                out var velocityX,
                out var velocityY);

            var rotationSpeed = (_dynamicGibRandom.NextSingle() * 6f) - 3f;
            var deferredSpin = 48f + (_dynamicGibRandom.NextSingle() * 36f);
            _world.SpawnCustomPlayerGib(
                gibX,
                gibY,
                velocityX,
                velocityY,
                rotationSpeed,
                horizontalFriction: 0.4f,
                rotationFriction: 0.55f,
                lifetimeTicks: 220,
                bloodChance: 2.1f,
                experimentalCryoTinted,
                visualId,
                visualOriginX: chunk.CentroidX - chunk.BoundsX,
                visualOriginY: chunk.CentroidY - chunk.BoundsY,
                visualScale: scaleY,
                enableDeferredSpin: true,
                deferredSpinSpeedDegrees: deferredSpin,
                deferredSpinAirborneTicks: 16);
        }

        TrySpawnDynamicWeaponGib(
            player,
            spawnX,
            spawnY,
            bodyWorldOriginX,
            bodyWorldOriginY,
            inheritedVelocityX,
            inheritedVelocityY,
            experimentalCryoTinted);
        return true;
    }

    private void BuildDynamicGibLaunchVelocity(
        float localOffsetX,
        float localOffsetY,
        float inheritedVelocityX,
        float inheritedVelocityY,
        bool isTopDown,
        out float velocityX,
        out float velocityY)
    {
        const float minUpwardBurst = 1.15f;
        var dirX = localOffsetX;
        var dirY = localOffsetY;
        var wouldLaunchDown = !isTopDown && dirY > 0.25f;
        float explodeSpeed;
        if (wouldLaunchDown)
        {
            // Pieces that would fall straight down get a random up/side toss instead, slower.
            var angle = (_dynamicGibRandom.NextSingle() * MathF.PI) - (MathF.PI * 0.5f); // -90°..+90° from up
            dirX = MathF.Sin(angle);
            dirY = -MathF.Abs(MathF.Cos(angle));
            explodeSpeed = 0.55f + (_dynamicGibRandom.NextSingle() * 0.95f);
        }
        else
        {
            var dirLength = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
            if (dirLength < 0.5f)
            {
                dirX = _dynamicGibRandom.NextSingle() < 0.5f ? -1f : 1f;
                dirY = isTopDown ? ((_dynamicGibRandom.NextSingle() * 2f) - 1f) : -0.45f;
                dirLength = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
            }

            dirX /= dirLength;
            dirY /= dirLength;
            if (!isTopDown && dirY > -0.2f)
            {
                // Always bias slightly upward so near-ground explosions don't pin gibs in the floor.
                dirY = -0.35f - (_dynamicGibRandom.NextSingle() * 0.35f);
                var horizontal = MathF.Abs(dirX);
                if (horizontal < 0.05f)
                {
                    dirX = _dynamicGibRandom.NextSingle() < 0.5f ? -0.85f : 0.85f;
                }

                var renormalize = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
                dirX /= renormalize;
                dirY /= renormalize;
            }

            explodeSpeed = 1.4f + (_dynamicGibRandom.NextSingle() * 2.2f);
        }

        var explodeX = dirX * explodeSpeed;
        var explodeY = dirY * explodeSpeed;
        if (!isTopDown)
        {
            explodeY = MathF.Min(explodeY, -minUpwardBurst);
        }

        velocityX = inheritedVelocityX + explodeX;
        velocityY = inheritedVelocityY + explodeY;
        if (!isTopDown)
        {
            // Keep the final launch from starting downward into the ground.
            velocityY = MathF.Min(velocityY, -0.35f);
        }
    }

    private void TrySpawnDynamicWeaponGib(
        PlayerEntity player,
        float spawnX,
        float spawnY,
        float bodyWorldOriginX,
        float bodyWorldOriginY,
        float inheritedVelocityX,
        float inheritedVelocityY,
        bool experimentalCryoTinted)
    {
        if (!TryResolveDynamicWeaponFrame(player, spawnX, spawnY, out var weaponFrame, out var weaponSpriteFrame))
        {
            return;
        }

        if (!TryGetSpriteFramePixels(weaponSpriteFrame, out var weaponPixels, out var weaponWidth, out var weaponHeight))
        {
            return;
        }

        // Bake horizontal facing into the texture so draw can use a positive scale.
        if (weaponFrame.FacingScale < 0f)
        {
            FlipPixelsHorizontally(weaponPixels, weaponWidth, weaponHeight);
        }

        var texture = CreateDynamicGibTexture(weaponPixels, weaponWidth, weaponHeight);
        if (texture is null)
        {
            return;
        }

        var visualId = AllocateDynamicGibVisualId();
        _dynamicGibVisuals[visualId] = new DynamicGibVisual(texture, ownsTexture: true);

        var playerScale = MathF.Abs(weaponFrame.PlayerScale);
        if (playerScale <= 0.001f)
        {
            playerScale = 1f;
        }

        var origin = weaponFrame.Origin;
        if (weaponFrame.FacingScale < 0f)
        {
            origin = new Vector2(weaponWidth - 1f - origin.X, origin.Y);
        }

        var gibX = weaponFrame.WorldDrawX;
        var gibY = weaponFrame.WorldDrawY;
        BuildDynamicGibLaunchVelocity(
            gibX - bodyWorldOriginX,
            gibY - bodyWorldOriginY,
            inheritedVelocityX,
            inheritedVelocityY,
            _world.Level.IsTopDown,
            out var velocityX,
            out var velocityY);

        var rotationDegrees = weaponFrame.RotationRadians * (180f / MathF.PI);
        if (weaponFrame.FacingScale < 0f)
        {
            // Facing was baked into pixels; undo the mirrored-draw half-turn.
            rotationDegrees -= 180f;
        }

        var rotationSpeed = (_dynamicGibRandom.NextSingle() * 6f) - 3f;
        var deferredSpin = 55f + (_dynamicGibRandom.NextSingle() * 40f);
        _world.SpawnCustomPlayerGib(
            gibX,
            gibY,
            velocityX,
            velocityY,
            rotationSpeed,
            horizontalFriction: 0.35f,
            rotationFriction: 0.45f,
            lifetimeTicks: 240,
            bloodChance: 0.45f,
            experimentalCryoTinted,
            visualId,
            visualOriginX: origin.X,
            visualOriginY: origin.Y,
            visualScale: playerScale,
            initialRotationDegrees: rotationDegrees,
            enableDeferredSpin: true,
            deferredSpinSpeedDegrees: deferredSpin,
            deferredSpinAirborneTicks: 16);
    }

    private bool TryResolveDynamicBodyFrame(
        PlayerEntity player,
        float spawnX,
        float spawnY,
        out CachedDynamicGibBodyFrame bodyFrame,
        out LoadedSpriteFrame spriteFrame)
    {
        spriteFrame = null!;
        if (_cachedDynamicGibBodyFrames.TryGetValue(player.Id, out bodyFrame))
        {
            bodyFrame = bodyFrame with
            {
                RenderPosition = new Vector2(spawnX, spawnY),
            };
        }
        else if (!TryCaptureLiveDynamicBodyFrame(player, spawnX, spawnY, out bodyFrame))
        {
            bodyFrame = default;
            return false;
        }

        var sprite = GetResolvedSprite(bodyFrame.SpriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frameIndex = Math.Clamp(bodyFrame.FrameIndex, 0, sprite.Frames.Count - 1);
        spriteFrame = sprite.Frames[frameIndex];
        bodyFrame = bodyFrame with
        {
            FrameIndex = frameIndex,
            Origin = sprite.Origin.ToVector2(),
        };
        return true;
    }

    private bool TryCaptureLiveDynamicBodyFrame(
        PlayerEntity player,
        float spawnX,
        float spawnY,
        out CachedDynamicGibBodyFrame bodyFrame)
    {
        bodyFrame = default;
        var bodySelection = GetPlayerBodySpriteSelection(player);
        var spriteName = bodySelection.SpriteName;
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return false;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var frameIndex = GetPlayerBodySpriteFrameIndex(bodySelection.AnimationImage, sprite.Frames.Count);
        var facingScale = GetPlayerFacingScale(player) * player.PlayerScale;
        bodyFrame = new CachedDynamicGibBodyFrame(
            spriteName,
            frameIndex,
            new Vector2(spawnX, spawnY),
            new Vector2(facingScale, player.PlayerScale),
            sprite.Origin.ToVector2(),
            bodySelection.BodyYOffset * player.PlayerScale);
        return true;
    }

    private bool TryResolveDynamicWeaponFrame(
        PlayerEntity player,
        float spawnX,
        float spawnY,
        out CachedDynamicGibWeaponFrame weaponFrame,
        out LoadedSpriteFrame spriteFrame)
    {
        spriteFrame = null!;
        if (!_cachedDynamicGibWeaponFrames.TryGetValue(player.Id, out weaponFrame)
            && !_gameplayWeaponRenderController.TryCaptureWeaponGibFrame(player, out weaponFrame))
        {
            return false;
        }

        // Always resolve the default idle weapon sprite, even if cache held a pose-only transform.
        var idleSpriteName = weaponFrame.SpriteName;
        var weaponDefinition = _gameplayWeaponRenderController.GetWeaponRenderDefinitionProxy(player);
        if (!string.IsNullOrWhiteSpace(weaponDefinition.NormalSpriteName))
        {
            idleSpriteName = weaponDefinition.NormalSpriteName;
        }

        var sprite = GetResolvedSprite(idleSpriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        spriteFrame = sprite.Frames[0];
        weaponFrame = weaponFrame with
        {
            SpriteName = idleSpriteName,
            FrameIndex = 0,
            Origin = sprite.Origin.ToVector2(),
        };
        return true;
    }

    private int ResolveDynamicBodyChunkCount()
    {
        var baseCount = 5 + _dynamicGibRandom.Next(4);
        return Math.Clamp(
            (int)MathF.Round(baseCount * GetGibAmountScale()),
            3,
            8);
    }

    private int AllocateDynamicGibVisualId()
    {
        var id = _nextDynamicGibVisualId++;
        if (_nextDynamicGibVisualId == int.MaxValue)
        {
            _nextDynamicGibVisualId = 1;
        }

        return id;
    }

    private Texture2D? CreateDynamicGibTexture(Color[] pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height)
        {
            return null;
        }

        try
        {
            var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
            texture.SetData(pixels);
            return texture;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetSpriteFramePixels(
        LoadedSpriteFrame frame,
        out Color[] pixels,
        out int width,
        out int height)
    {
        width = frame.Width;
        height = frame.Height;
        pixels = new Color[width * height];
        if (frame.TryCopyPixelData(pixels))
        {
            return true;
        }

        try
        {
            if (frame.SourceRectangle is { } sourceRectangle)
            {
                frame.Texture.GetData(0, sourceRectangle, pixels, 0, pixels.Length);
            }
            else
            {
                frame.Texture.GetData(pixels);
            }

            return true;
        }
        catch
        {
            pixels = Array.Empty<Color>();
            width = 0;
            height = 0;
            return false;
        }
    }

    private static void FlipPixelsHorizontally(Color[] pixels, int width, int height)
    {
        for (var y = 0; y < height; y += 1)
        {
            var row = y * width;
            for (var x = 0; x < width / 2; x += 1)
            {
                var left = row + x;
                var right = row + (width - 1 - x);
                (pixels[left], pixels[right]) = (pixels[right], pixels[left]);
            }
        }
    }

    private bool TryBuildVoronoiChunks(
        Color[] sourcePixels,
        int width,
        int height,
        int chunkCount,
        bool experimentalCryoTinted,
        out List<DynamicGibChunk> chunks)
    {
        chunks = new List<DynamicGibChunk>(chunkCount);
        var opaqueIndices = new List<int>(width * height / 2);
        for (var index = 0; index < sourcePixels.Length; index += 1)
        {
            if (sourcePixels[index].A >= 24)
            {
                opaqueIndices.Add(index);
            }
        }

        if (opaqueIndices.Count < chunkCount)
        {
            return false;
        }

        var seeds = new Vector2[chunkCount];
        var usedSeedPixels = new HashSet<int>();
        for (var seedIndex = 0; seedIndex < chunkCount; seedIndex += 1)
        {
            int pixelIndex;
            do
            {
                pixelIndex = opaqueIndices[_dynamicGibRandom.Next(opaqueIndices.Count)];
            }
            while (!usedSeedPixels.Add(pixelIndex) && usedSeedPixels.Count < opaqueIndices.Count);

            seeds[seedIndex] = new Vector2(pixelIndex % width, pixelIndex / width);
        }

        var assignments = new int[sourcePixels.Length];
        Array.Fill(assignments, -1);
        var regionMinX = new int[chunkCount];
        var regionMinY = new int[chunkCount];
        var regionMaxX = new int[chunkCount];
        var regionMaxY = new int[chunkCount];
        var regionCounts = new int[chunkCount];
        var regionSumX = new float[chunkCount];
        var regionSumY = new float[chunkCount];
        for (var seedIndex = 0; seedIndex < chunkCount; seedIndex += 1)
        {
            regionMinX[seedIndex] = width;
            regionMinY[seedIndex] = height;
            regionMaxX[seedIndex] = -1;
            regionMaxY[seedIndex] = -1;
        }

        for (var opaqueIndex = 0; opaqueIndex < opaqueIndices.Count; opaqueIndex += 1)
        {
            var pixelIndex = opaqueIndices[opaqueIndex];
            var x = pixelIndex % width;
            var y = pixelIndex / width;
            var bestSeed = 0;
            var bestDistance = float.MaxValue;
            for (var seedIndex = 0; seedIndex < chunkCount; seedIndex += 1)
            {
                var dx = x - seeds[seedIndex].X;
                var dy = y - seeds[seedIndex].Y;
                // Slight jitter keeps chunk borders from looking too grid-like.
                var distance = (dx * dx) + (dy * dy) + (_dynamicGibRandom.NextSingle() * 0.35f);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSeed = seedIndex;
                }
            }

            assignments[pixelIndex] = bestSeed;
            regionCounts[bestSeed] += 1;
            regionSumX[bestSeed] += x;
            regionSumY[bestSeed] += y;
            if (x < regionMinX[bestSeed]) regionMinX[bestSeed] = x;
            if (y < regionMinY[bestSeed]) regionMinY[bestSeed] = y;
            if (x > regionMaxX[bestSeed]) regionMaxX[bestSeed] = x;
            if (y > regionMaxY[bestSeed]) regionMaxY[bestSeed] = y;
        }

        // Mark voronoi cut seams (opaque pixels next to a different region).
        var cutSeam = new bool[sourcePixels.Length];
        for (var opaqueIndex = 0; opaqueIndex < opaqueIndices.Count; opaqueIndex += 1)
        {
            var pixelIndex = opaqueIndices[opaqueIndex];
            var x = pixelIndex % width;
            var y = pixelIndex / width;
            var region = assignments[pixelIndex];
            if (IsVoronoiCutPixel(assignments, width, height, x, y, region))
            {
                cutSeam[pixelIndex] = true;
            }
        }

        const int bloodCellSize = 2;

        for (var seedIndex = 0; seedIndex < chunkCount; seedIndex += 1)
        {
            if (regionCounts[seedIndex] <= 0 || regionMaxX[seedIndex] < regionMinX[seedIndex])
            {
                continue;
            }

            // Pad so exterior seam blood has room outside the flesh silhouette.
            var boundsX = regionMinX[seedIndex] - bloodCellSize;
            var boundsY = regionMinY[seedIndex] - bloodCellSize;
            var chunkWidth = (regionMaxX[seedIndex] - regionMinX[seedIndex] + 1) + (bloodCellSize * 2);
            var chunkHeight = (regionMaxY[seedIndex] - regionMinY[seedIndex] + 1) + (bloodCellSize * 2);
            var chunkPixels = new Color[chunkWidth * chunkHeight];
            var chunkCutSeam = new bool[chunkWidth * chunkHeight];
            for (var y = 0; y < chunkHeight; y += 1)
            {
                var sourceY = boundsY + y;
                if (sourceY < 0 || sourceY >= height)
                {
                    continue;
                }

                for (var x = 0; x < chunkWidth; x += 1)
                {
                    var sourceX = boundsX + x;
                    if (sourceX < 0 || sourceX >= width)
                    {
                        continue;
                    }

                    var sourceIndex = (sourceY * width) + sourceX;
                    if (assignments[sourceIndex] != seedIndex)
                    {
                        continue;
                    }

                    var localIndex = (y * chunkWidth) + x;
                    chunkPixels[localIndex] = sourcePixels[sourceIndex];
                    chunkCutSeam[localIndex] = cutSeam[sourceIndex];
                }
            }

            RoundDynamicGibCutEdges(chunkPixels, chunkCutSeam, chunkWidth, chunkHeight);
            if (!TryRecomputeChunkMetrics(chunkPixels, chunkWidth, chunkHeight, boundsX, boundsY, out var centroidX, out var centroidY))
            {
                continue;
            }

            PaintDynamicGibCutBlood(chunkPixels, chunkCutSeam, chunkWidth, chunkHeight, experimentalCryoTinted);

            chunks.Add(new DynamicGibChunk(
                chunkPixels,
                chunkWidth,
                chunkHeight,
                boundsX,
                boundsY,
                centroidX,
                centroidY));
        }

        return chunks.Count > 0;
    }

    private static bool IsVoronoiCutPixel(int[] assignments, int width, int height, int x, int y, int region)
    {
        // 4-neighborhood is enough for a 1px cut seam at sprite resolution.
        if (x > 0)
        {
            var neighbor = assignments[y * width + (x - 1)];
            if (neighbor >= 0 && neighbor != region)
            {
                return true;
            }
        }

        if (x + 1 < width)
        {
            var neighbor = assignments[y * width + (x + 1)];
            if (neighbor >= 0 && neighbor != region)
            {
                return true;
            }
        }

        if (y > 0)
        {
            var neighbor = assignments[(y - 1) * width + x];
            if (neighbor >= 0 && neighbor != region)
            {
                return true;
            }
        }

        if (y + 1 < height)
        {
            var neighbor = assignments[(y + 1) * width + x];
            if (neighbor >= 0 && neighbor != region)
            {
                return true;
            }
        }

        return false;
    }

    private void RoundDynamicGibCutEdges(Color[] pixels, bool[] cutSeam, int width, int height)
    {
        // One light erode pass on cut-edge tips only — softens jagged voronoi nubs
        // without shrinking chunks as much as multi-pass rounding.
        var originalCutSeam = (bool[])cutSeam.Clone();
        var remove = new bool[pixels.Length];
        for (var index = 0; index < pixels.Length; index += 1)
        {
            if (!cutSeam[index] || pixels[index].A < 24)
            {
                continue;
            }

            var x = index % width;
            var y = index / width;
            if (CountOpaqueOrthoNeighbors(pixels, width, height, x, y) <= 1)
            {
                remove[index] = true;
            }
        }

        for (var index = 0; index < pixels.Length; index += 1)
        {
            if (!remove[index])
            {
                continue;
            }

            pixels[index] = Color.Transparent;
            cutSeam[index] = false;
        }

        // Mark remaining cut-face flesh pixels (blood paints outside these, not over them).
        for (var index = 0; index < pixels.Length; index += 1)
        {
            if (pixels[index].A < 24)
            {
                cutSeam[index] = false;
                continue;
            }

            var x = index % width;
            var y = index / width;
            cutSeam[index] = HasTransparentOrthoNeighbor(pixels, width, height, x, y)
                && IsNearOriginalCutSeam(originalCutSeam, width, height, x, y);
        }
    }

    private static bool IsNearOriginalCutSeam(bool[] originalCutSeam, int width, int height, int x, int y)
    {
        var index = (y * width) + x;
        if (originalCutSeam[index])
        {
            return true;
        }

        for (var oy = -1; oy <= 1; oy += 1)
        {
            for (var ox = -1; ox <= 1; ox += 1)
            {
                if (ox == 0 && oy == 0)
                {
                    continue;
                }

                var nx = x + ox;
                var ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    continue;
                }

                if (originalCutSeam[(ny * width) + nx])
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Flat 2x2 gib gore palette — red blood, beige skin, purple gut.
    private static readonly Color[] DynamicGibGoreColors =
    [
        new(218, 22, 28),
        new(165, 10, 16),
        new(198, 16, 24),
        new(145, 8, 14),
        new(210, 170, 130),
        new(186, 142, 108),
        new(168, 120, 92),
        new(140, 72, 110),
        new(112, 48, 88),
        new(158, 58, 96),
    ];

    private static readonly Color[] DynamicGibCryoGoreColors =
    [
        new(185, 230, 245),
        new(140, 195, 220),
        new(168, 210, 230),
        new(120, 170, 200),
        new(190, 210, 220),
        new(150, 180, 200),
        new(130, 120, 180),
        new(110, 100, 160),
    ];

    private void PaintDynamicGibCutBlood(
        Color[] pixels,
        bool[] cutSeam,
        int width,
        int height,
        bool experimentalCryoTinted)
    {
        // Match blood-particle cells: 2x2 blocks on a snapped grid, drawn only in
        // transparent pixels outside the flesh so the gib silhouette stays fully visible.
        const int bloodCellSize = 2;
        var goreColors = experimentalCryoTinted ? DynamicGibCryoGoreColors : DynamicGibGoreColors;
        var bloodCells = new HashSet<(int Gx, int Gy)>();
        for (var index = 0; index < pixels.Length; index += 1)
        {
            if (!cutSeam[index] || pixels[index].A < 24)
            {
                continue;
            }

            var x = index % width;
            var y = index / width;
            TryAddOutwardBloodCells(pixels, width, height, x, y, bloodCellSize, bloodCells);
        }

        if (bloodCells.Count == 0)
        {
            return;
        }

        foreach (var (gx, gy) in bloodCells)
        {
            // Mostly red, with occasional skin / gut cells for a messier cut.
            var roll = _dynamicGibRandom.Next(10);
            var colorIndex = roll switch
            {
                < 5 => _dynamicGibRandom.Next(0, 4),           // reds
                < 8 => _dynamicGibRandom.Next(4, Math.Min(7, goreColors.Length)), // beige skin
                _ => _dynamicGibRandom.Next(Math.Min(7, goreColors.Length - 1), goreColors.Length), // purple gut
            };
            if (experimentalCryoTinted)
            {
                colorIndex = _dynamicGibRandom.Next(goreColors.Length);
            }

            var color = goreColors[Math.Clamp(colorIndex, 0, goreColors.Length - 1)];
            var cellMinX = gx * bloodCellSize;
            var cellMinY = gy * bloodCellSize;
            for (var py = 0; py < bloodCellSize; py += 1)
            {
                var y = cellMinY + py;
                if (y < 0 || y >= height)
                {
                    continue;
                }

                for (var px = 0; px < bloodCellSize; px += 1)
                {
                    var x = cellMinX + px;
                    if (x < 0 || x >= width)
                    {
                        continue;
                    }

                    var index = (y * width) + x;
                    // Never overwrite flesh — gore sits outside / behind the sprite.
                    if (pixels[index].A >= 24)
                    {
                        continue;
                    }

                    pixels[index] = color;
                }
            }
        }
    }

    private static void TryAddOutwardBloodCells(
        Color[] pixels,
        int width,
        int height,
        int x,
        int y,
        int bloodCellSize,
        HashSet<(int Gx, int Gy)> bloodCells)
    {
        TryStep(x - 1, y);
        TryStep(x + 1, y);
        TryStep(x, y - 1);
        TryStep(x, y + 1);

        void TryStep(int firstX, int firstY)
        {
            if (!IsTransparentInBounds(firstX, firstY))
            {
                return;
            }

            bloodCells.Add((firstX / bloodCellSize, firstY / bloodCellSize));

            // Second step out for a full 2px exterior rim.
            var secondX = firstX + (firstX - x);
            var secondY = firstY + (firstY - y);
            if (IsTransparentInBounds(secondX, secondY))
            {
                bloodCells.Add((secondX / bloodCellSize, secondY / bloodCellSize));
            }
        }

        bool IsTransparentInBounds(int px, int py)
        {
            if (px < 0 || py < 0 || px >= width || py >= height)
            {
                return false;
            }

            return pixels[(py * width) + px].A < 24;
        }
    }

    private static bool TryRecomputeChunkMetrics(
        Color[] pixels,
        int width,
        int height,
        int boundsX,
        int boundsY,
        out float centroidX,
        out float centroidY)
    {
        centroidX = 0f;
        centroidY = 0f;
        var count = 0;
        var sumX = 0f;
        var sumY = 0f;
        for (var index = 0; index < pixels.Length; index += 1)
        {
            if (pixels[index].A < 24)
            {
                continue;
            }

            var x = index % width;
            var y = index / width;
            sumX += boundsX + x;
            sumY += boundsY + y;
            count += 1;
        }

        if (count <= 0)
        {
            return false;
        }

        centroidX = sumX / count;
        centroidY = sumY / count;
        return true;
    }

    private static int CountOpaqueOrthoNeighbors(Color[] pixels, int width, int height, int x, int y)
    {
        var count = 0;
        if (x > 0 && pixels[y * width + (x - 1)].A >= 24) count += 1;
        if (x + 1 < width && pixels[y * width + (x + 1)].A >= 24) count += 1;
        if (y > 0 && pixels[(y - 1) * width + x].A >= 24) count += 1;
        if (y + 1 < height && pixels[(y + 1) * width + x].A >= 24) count += 1;
        return count;
    }

    private static bool HasTransparentOrthoNeighbor(Color[] pixels, int width, int height, int x, int y)
    {
        if (x <= 0 || pixels[y * width + (x - 1)].A < 24) return true;
        if (x + 1 >= width || pixels[y * width + (x + 1)].A < 24) return true;
        if (y <= 0 || pixels[(y - 1) * width + x].A < 24) return true;
        if (y + 1 >= height || pixels[(y + 1) * width + x].A < 24) return true;
        return false;
    }

    private readonly record struct DynamicGibChunk(
        Color[] Pixels,
        int Width,
        int Height,
        int BoundsX,
        int BoundsY,
        float CentroidX,
        float CentroidY);

    private bool TryGetDynamicGibVisual(int visualId, out DynamicGibVisual visual)
        => _dynamicGibVisuals.TryGetValue(visualId, out visual!);

    private void AdvanceDynamicGibVisualCleanup()
    {
        if (_dynamicGibVisuals.Count == 0)
        {
            return;
        }

        _staleDynamicGibVisualIds.Clear();
        foreach (var visualId in _dynamicGibVisuals.Keys)
        {
            var stillAlive = false;
            for (var index = 0; index < _world.PlayerGibs.Count; index += 1)
            {
                var gib = _world.PlayerGibs[index];
                if (gib.CustomVisualId == visualId && !gib.IsExpired)
                {
                    stillAlive = true;
                    break;
                }
            }

            if (!stillAlive)
            {
                _staleDynamicGibVisualIds.Add(visualId);
            }
        }

        for (var index = 0; index < _staleDynamicGibVisualIds.Count; index += 1)
        {
            DisposeDynamicGibVisual(_staleDynamicGibVisualIds[index]);
        }

        _staleDynamicGibVisualIds.Clear();
    }

    private void ResetDynamicGibEffects()
    {
        foreach (var visualId in _dynamicGibVisuals.Keys)
        {
            _staleDynamicGibVisualIds.Add(visualId);
        }

        for (var index = 0; index < _staleDynamicGibVisualIds.Count; index += 1)
        {
            DisposeDynamicGibVisual(_staleDynamicGibVisualIds[index]);
        }

        _staleDynamicGibVisualIds.Clear();
        _cachedDynamicGibBodyFrames.Clear();
        _cachedDynamicGibWeaponFrames.Clear();
    }

    private void DisposeDynamicGibVisual(int visualId)
    {
        if (!_dynamicGibVisuals.Remove(visualId, out var visual))
        {
            return;
        }

        if (visual.OwnsTexture)
        {
            visual.Texture.Dispose();
        }
    }
}
