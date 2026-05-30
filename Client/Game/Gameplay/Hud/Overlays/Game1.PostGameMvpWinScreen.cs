#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int PostGameMvpThirdStartTick = 12;
    private const int PostGameMvpSecondStartTick = 38;
    private const int PostGameMvpFirstStartTick = 64;
    private const int PostGameMvpSideEnterTicks = 56;
    private const int PostGameMvpFirstEnterTicks = 64;
    private const float PostGameMvpBoardSourceWidth = 282f;
    private const float PostGameMvpArtToggleHintCenterY = 24f;
    private const float PostGameMvpNameLabelTopPadding = 8f;
    private const float PostGameMvpNameLabelTopGap = 22f;
    private const float PostGameMvpWinnerNameHintGap = 8f;
    private const float PostGameMvpWinnerOpaqueTopFallbackPixels = 8f;
    private const float PostGameMvpSideAnchorStartRatio = 0.7f;

    private readonly record struct PostGameMvpEntry(PlayerEntity Player, int Score);
    private readonly record struct PostGameMvpLayout(Vector2 BoardCenter, Vector2 ArtScale);
    private readonly record struct PostGameMvpArtFrameKey(int PlayerId, int Rank, string SpriteName);

    private PlayerTeam? _postGameMvpPresentationWinnerTeam;
    private int _postGameMvpPresentationTicks;
    private bool _postGameMvpArtHidden;
    private readonly Dictionary<PostGameMvpArtFrameKey, int> _postGameMvpArtFrameSelections = new();
    private readonly Dictionary<LoadedSpriteFrame, Rectangle> _postGameMvpSideAnchorBounds = new();

    private void UpdatePostGameMvpWinScreenState(KeyboardState keyboard, int clientTicks)
    {
        if (!ShouldDrawPostGameMvpWinScreen())
        {
            _postGameMvpPresentationWinnerTeam = null;
            _postGameMvpPresentationTicks = 0;
            _postGameMvpArtHidden = false;
            _postGameMvpArtFrameSelections.Clear();
            _postGameMvpSideAnchorBounds.Clear();
            return;
        }

        var winnerTeam = _world.MatchState.WinnerTeam!.Value;
        if (_postGameMvpPresentationWinnerTeam != winnerTeam)
        {
            _postGameMvpPresentationWinnerTeam = winnerTeam;
            _postGameMvpPresentationTicks = 0;
            _postGameMvpArtHidden = false;
            _postGameMvpArtFrameSelections.Clear();
            _postGameMvpSideAnchorBounds.Clear();
        }

        if (clientTicks > 0)
        {
            _postGameMvpPresentationTicks = Math.Min(3600, _postGameMvpPresentationTicks + clientTicks);
        }

        if (_postGameMvpArtEnabled && IsPostGameMvpArtTogglePressed(keyboard))
        {
            _postGameMvpArtHidden = !_postGameMvpArtHidden;
        }
    }

    private bool IsPostGameMvpArtTogglePressed(KeyboardState keyboard)
    {
        return (keyboard.IsKeyDown(Keys.LeftShift) && !_previousKeyboard.IsKeyDown(Keys.LeftShift))
            || (keyboard.IsKeyDown(Keys.RightShift) && !_previousKeyboard.IsKeyDown(Keys.RightShift));
    }

    private bool ShouldDrawPostGameMvpWinScreen()
    {
        return _world.MatchState.IsEnded && _world.MatchState.WinnerTeam.HasValue;
    }

    private void DrawPostGameMvpWinScreenHud()
    {
        if (!ShouldDrawPostGameMvpWinScreen())
        {
            return;
        }

        var winnerTeam = _world.MatchState.WinnerTeam!.Value;
        var entries = BuildPostGameMvpEntries(winnerTeam);
        var layout = GetPostGameMvpLayout();
        var drawArt = _postGameMvpArtEnabled && !_postGameMvpArtHidden;

        if (drawArt)
        {
            DrawPostGameMvpArt(entries, layout);
        }

        DrawPostGameMvpBoard(winnerTeam, entries, layout);
        if (drawArt)
        {
            DrawPostGameMvpArtToggleHint();
        }
    }

    private List<PostGameMvpEntry> BuildPostGameMvpEntries(PlayerTeam team)
    {
        var players = GetScoreboardPlayers(team);
        var entries = new List<PostGameMvpEntry>(players.Count);
        for (var index = 0; index < players.Count; index += 1)
        {
            var player = players[index];
            entries.Add(new PostGameMvpEntry(player, GetPostGameMvpScore(player)));
        }

        entries.Sort(static (left, right) =>
        {
            var scoreCompare = right.Score.CompareTo(left.Score);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            var pointsCompare = right.Player.Points.CompareTo(left.Player.Points);
            if (pointsCompare != 0)
            {
                return pointsCompare;
            }

            var killsCompare = right.Player.Kills.CompareTo(left.Player.Kills);
            if (killsCompare != 0)
            {
                return killsCompare;
            }

            var deathsCompare = left.Player.Deaths.CompareTo(right.Player.Deaths);
            if (deathsCompare != 0)
            {
                return deathsCompare;
            }

            return string.Compare(left.Player.DisplayName, right.Player.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        if (entries.Count > 3)
        {
            entries.RemoveRange(3, entries.Count - 3);
        }

        return entries;
    }

    private static int GetPostGameMvpScore(PlayerEntity player)
    {
        return (int)MathF.Floor(player.Points) + (int)MathF.Floor(Math.Max(0, player.HealPoints) / 200f);
    }

    private PostGameMvpLayout GetPostGameMvpLayout()
    {
        var boardCenterY = ViewportHeight - 100f;
        var maximumBoardCenterY = ViewportHeight - 82f;
        if (maximumBoardCenterY > 180f)
        {
            boardCenterY = MathF.Min(boardCenterY, maximumBoardCenterY);
        }

        boardCenterY = MathF.Max(220f, boardCenterY);
        var artScale = Math.Clamp(ViewportHeight / 72f, 6.5625f, 8.75f);
        return new PostGameMvpLayout(
            new Vector2(ViewportWidth / 2f, boardCenterY),
            new Vector2(artScale, artScale));
    }

    private void DrawPostGameMvpArt(IReadOnlyList<PostGameMvpEntry> entries, PostGameMvpLayout layout)
    {
        if (entries.Count >= 1)
        {
            DrawPostGameMvpArtEntry(entries[0], rank: 1, layout);
        }

        if (entries.Count >= 3)
        {
            DrawPostGameMvpArtEntry(entries[2], rank: 3, layout);
        }

        if (entries.Count >= 2)
        {
            DrawPostGameMvpArtEntry(entries[1], rank: 2, layout);
        }

        DrawPostGameMvpNameLabels(entries, layout);
    }

    private void DrawPostGameMvpArtEntry(PostGameMvpEntry entry, int rank, PostGameMvpLayout layout)
    {
        if (!TryGetPostGameMvpArtDrawState(
                entry,
                rank,
                layout,
                out var frame,
                out var position,
                out _,
                out var progress,
                out var origin,
                out var effects))
        {
            return;
        }

        var roundedPosition = RoundToSourcePixels(position);
        DrawLoadedSpriteFrame(
            frame!,
            roundedPosition + new Vector2(4f, 4f),
            null,
            Color.Black * (0.35f * progress),
            0f,
            origin,
            layout.ArtScale,
            effects,
            0f);
        DrawLoadedSpriteFrame(
            frame!,
            roundedPosition,
            null,
            Color.White * progress,
            0f,
            origin,
            layout.ArtScale,
            effects,
            0f);
    }

    private bool TryGetPostGameMvpArtDrawState(
        PostGameMvpEntry entry,
        int rank,
        PostGameMvpLayout layout,
        out LoadedSpriteFrame? frame,
        out Vector2 position,
        out float topExtent,
        out float progress,
        out Vector2 origin,
        out SpriteEffects effects)
    {
        frame = null;
        position = Vector2.Zero;
        topExtent = 0f;
        progress = 0f;
        origin = Vector2.Zero;
        effects = SpriteEffects.None;

        var spriteName = GetPostGameMvpArtSpriteName(entry.Player.Team, entry.Player.ClassId, winner: rank == 1);
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return false;
        }

        var startTick = rank switch
        {
            1 => PostGameMvpFirstStartTick,
            2 => PostGameMvpSecondStartTick,
            _ => PostGameMvpThirdStartTick,
        };
        var durationTicks = rank == 1 ? PostGameMvpFirstEnterTicks : PostGameMvpSideEnterTicks;
        progress = GetPostGameMvpAnimationProgress(startTick, durationTicks);
        if (progress <= 0f)
        {
            return false;
        }

        var frameIndex = GetPostGameMvpArtFrameIndex(entry, rank, spriteName, sprite);
        frame = sprite.Frames[frameIndex];
        var target = GetPostGameMvpArtTarget(rank, layout, sprite, frame);
        var leftExtent = sprite.Origin.X * layout.ArtScale.X;
        var rightExtent = MathF.Max(0f, frame.Width - sprite.Origin.X) * layout.ArtScale.X;
        topExtent = GetPostGameMvpArtTopExtent(sprite, frame, layout, rank);
        var start = rank switch
        {
            1 => new Vector2(target.X, ViewportHeight + topExtent + 40f),
            2 => new Vector2(-rightExtent - 40f, target.Y),
            _ => new Vector2(ViewportWidth + leftExtent + 40f, target.Y),
        };

        var eased = EaseOutCubic(progress);
        position = Vector2.Lerp(start, target, eased);
        origin = sprite.Origin.ToVector2();
        effects = rank == 3 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        return true;
    }

    private static float GetPostGameMvpArtTopExtent(
        LoadedGameMakerSprite sprite,
        LoadedSpriteFrame frame,
        PostGameMvpLayout layout,
        int rank)
    {
        var transparentTopPixels = frame.OpaqueBounds?.Y ?? (rank == 1 ? PostGameMvpWinnerOpaqueTopFallbackPixels : 0f);
        transparentTopPixels = Math.Clamp(transparentTopPixels, 0f, MathF.Max(0f, frame.Height));
        return MathF.Max(0f, sprite.Origin.Y - transparentTopPixels) * layout.ArtScale.Y;
    }

    private void DrawPostGameMvpNameLabels(IReadOnlyList<PostGameMvpEntry> entries, PostGameMvpLayout layout)
    {
        if (entries.Count >= 1)
        {
            DrawPostGameMvpNameLabel(entries[0], rank: 1, layout);
        }

        if (entries.Count >= 3)
        {
            DrawPostGameMvpNameLabel(entries[2], rank: 3, layout);
        }

        if (entries.Count >= 2)
        {
            DrawPostGameMvpNameLabel(entries[1], rank: 2, layout);
        }
    }

    private void DrawPostGameMvpArtToggleHint()
    {
        const string hint = "Press SHIFT to hide";
        const float hintScale = 1f;
        var position = new Vector2(ViewportWidth * 0.5f, PostGameMvpArtToggleHintCenterY);
        DrawPostGameMvpTextCentered(hint, position + new Vector2(2f, 2f), Color.Black * 0.8f, hintScale);
        DrawPostGameMvpTextCentered(hint, position, Color.White, hintScale);
    }

    private void DrawPostGameMvpNameLabel(PostGameMvpEntry entry, int rank, PostGameMvpLayout layout)
    {
        if (!TryGetPostGameMvpArtDrawState(
                entry,
                rank,
                layout,
                out _,
                out var position,
                out var topExtent,
                out var progress,
                out _,
                out _))
        {
            return;
        }

        const float labelScale = 1f;
        var label = TrimPostGameMvpText(SanitizeScoreboardText(entry.Player.DisplayName), 126f, labelScale);
        var labelWidth = MeasureBitmapFontWidth(label, labelScale);
        var labelY = position.Y - topExtent - PostGameMvpNameLabelTopGap;
        if (rank == 1)
        {
            var hintBottomY = PostGameMvpArtToggleHintCenterY + (MeasureBitmapFontHeight(labelScale) * 0.5f);
            labelY = MathF.Max(labelY, hintBottomY + PostGameMvpWinnerNameHintGap);
        }
        else
        {
            labelY = MathF.Max(PostGameMvpNameLabelTopPadding, labelY);
        }

        var labelPosition = new Vector2(
            MathF.Round(position.X - (labelWidth * 0.5f)),
            MathF.Round(labelY));
        DrawPostGameMvpText(label, labelPosition + new Vector2(2f, 2f), Color.Black * (0.8f * progress), labelScale);
        DrawPostGameMvpText(label, labelPosition, Color.White * progress, labelScale);
    }

    private Vector2 GetPostGameMvpArtTarget(
        int rank,
        PostGameMvpLayout layout,
        LoadedGameMakerSprite sprite,
        LoadedSpriteFrame frame)
    {
        var boardHalfWidth = PostGameMvpBoardSourceWidth * 0.5f;
        var sideTargetY = layout.BoardCenter.Y + 44f;
        if (rank == 1)
        {
            return new Vector2(layout.BoardCenter.X, layout.BoardCenter.Y - 52f);
        }

        var anchorBounds = GetPostGameMvpSideAnchorBounds(frame);
        var originX = sprite.Origin.X;
        if (rank == 2)
        {
            var boardRight = layout.BoardCenter.X + boardHalfWidth;
            var visibleRightFromOrigin = (anchorBounds.Right - originX) * layout.ArtScale.X;
            return new Vector2(boardRight - visibleRightFromOrigin, sideTargetY);
        }

        var boardLeft = layout.BoardCenter.X - boardHalfWidth;
        var mirroredVisibleLeftFromOrigin = ((frame.Width - anchorBounds.Right) - originX) * layout.ArtScale.X;
        return new Vector2(boardLeft - mirroredVisibleLeftFromOrigin, sideTargetY);
    }

    private Rectangle GetPostGameMvpSideAnchorBounds(LoadedSpriteFrame frame)
    {
        if (_postGameMvpSideAnchorBounds.TryGetValue(frame, out var cachedBounds))
        {
            return cachedBounds;
        }

        var minimumAnchorY = Math.Clamp(
            (int)MathF.Floor(frame.Height * PostGameMvpSideAnchorStartRatio),
            0,
            Math.Max(0, frame.Height - 1));
        var bounds = TryCalculatePostGameMvpOpaqueBounds(frame, minimumAnchorY, out var lowerBounds)
            ? lowerBounds
            : frame.OpaqueBounds ?? new Rectangle(0, 0, frame.Width, frame.Height);
        _postGameMvpSideAnchorBounds[frame] = bounds;
        return bounds;
    }

    private static bool TryCalculatePostGameMvpOpaqueBounds(
        LoadedSpriteFrame frame,
        int minimumY,
        out Rectangle bounds)
    {
        bounds = default;
        if (frame.Width <= 0 || frame.Height <= 0)
        {
            return false;
        }

        var pixels = new Color[frame.Width * frame.Height];
        if (!TryCopyPostGameMvpFramePixels(frame, pixels))
        {
            return false;
        }

        if (TryCalculatePostGameMvpOpaqueBounds(pixels, frame.Width, frame.Height, minimumY, out bounds))
        {
            return true;
        }

        return minimumY > 0 && TryCalculatePostGameMvpOpaqueBounds(pixels, frame.Width, frame.Height, 0, out bounds);
    }

    private static bool TryCopyPostGameMvpFramePixels(LoadedSpriteFrame frame, Color[] pixels)
    {
        try
        {
            if (frame.TryCopyPixelData(pixels))
            {
                return true;
            }

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
            return false;
        }
    }

    private static bool TryCalculatePostGameMvpOpaqueBounds(
        Color[] pixels,
        int width,
        int height,
        int minimumY,
        out Rectangle bounds)
    {
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var y = minimumY; y < height; y += 1)
        {
            var rowIndex = y * width;
            for (var x = 0; x < width; x += 1)
            {
                if (pixels[rowIndex + x].A == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = default;
            return false;
        }

        bounds = new Rectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        return true;
    }

    private float GetPostGameMvpAnimationProgress(int startTick, int durationTicks)
    {
        if (_postGameMvpPresentationTicks <= startTick)
        {
            return 0f;
        }

        return Math.Clamp((_postGameMvpPresentationTicks - startTick) / (float)Math.Max(1, durationTicks), 0f, 1f);
    }

    private int GetPostGameMvpArtFrameIndex(PostGameMvpEntry entry, int rank, string spriteName, LoadedGameMakerSprite sprite)
    {
        if (sprite.Frames.Count <= 1)
        {
            return 0;
        }

        var key = new PostGameMvpArtFrameKey(entry.Player.Id, rank, spriteName);
        if (_postGameMvpArtFrameSelections.TryGetValue(key, out var frameIndex))
        {
            return Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1);
        }

        frameIndex = _visualRandom.Next(sprite.Frames.Count);
        _postGameMvpArtFrameSelections[key] = frameIndex;
        return frameIndex;
    }

    private static float EaseOutCubic(float value)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        var inverse = 1f - clamped;
        return 1f - (inverse * inverse * inverse);
    }

    private static string GetPostGameMvpArtSpriteName(PlayerTeam team, PlayerClass playerClass, bool winner)
    {
        var teamName = team == PlayerTeam.Blue ? "Blue" : "Red";
        var className = playerClass switch
        {
            PlayerClass.Engineer => "Engineer",
            PlayerClass.Pyro => "Pyro",
            PlayerClass.Soldier => "Soldier",
            PlayerClass.Demoman => "Demoman",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Sniper => "Sniper",
            PlayerClass.Medic => "Medic",
            PlayerClass.Spy => "Spy",
            PlayerClass.Quote => "Quote",
            _ => "Scout",
        };

        return winner
            ? $"Mvp{teamName}{className}WinnerS"
            : $"Mvp{teamName}{className}S";
    }

    private void DrawPostGameMvpBoard(PlayerTeam winnerTeam, IReadOnlyList<PostGameMvpEntry> entries, PostGameMvpLayout layout)
    {
        var boardFrame = winnerTeam == PlayerTeam.Blue ? 2 : 0;
        if (!TryDrawScreenSprite("MVPBannerS", boardFrame, layout.BoardCenter, Color.White * 0.92f, Vector2.One))
        {
            DrawPostGameMvpFallbackBoard(winnerTeam, layout.BoardCenter);
        }

        const float rowY = 31f;
        const float rowStepY = 15f;
        for (var index = 0; index < entries.Count; index += 1)
        {
            DrawPostGameMvpBoardRow(entries[index], layout.BoardCenter, rowY + (rowStepY * index));
        }
    }

    private void DrawPostGameMvpBoardRow(PostGameMvpEntry entry, Vector2 boardCenter, float rowOffsetY)
    {
        const float rowTextScale = 1f;
        var rowY = boardCenter.Y + rowOffsetY;
        var name = TrimPostGameMvpText(SanitizeScoreboardText(entry.Player.DisplayName), 94f, rowTextScale);
        DrawPostGameMvpText(name, new Vector2(boardCenter.X - 130f, rowY), Color.White, rowTextScale);
        DrawPostGameMvpTextRightAligned(entry.Player.Kills.ToString(CultureInfo.InvariantCulture), new Vector2(boardCenter.X - 2f, rowY), Color.White, rowTextScale);
        DrawPostGameMvpTextRightAligned(entry.Player.HealPoints.ToString(CultureInfo.InvariantCulture), new Vector2(boardCenter.X + 82f, rowY), Color.White, rowTextScale);
        DrawPostGameMvpTextRightAligned(entry.Score.ToString(CultureInfo.InvariantCulture), new Vector2(boardCenter.X + 136f, rowY), Color.White, rowTextScale);
    }

    private void DrawPostGameMvpFallbackBoard(PlayerTeam winnerTeam, Vector2 boardCenter)
    {
        var bounds = new Rectangle(
            (int)MathF.Round(boardCenter.X - 141f),
            (int)MathF.Round(boardCenter.Y - 82f),
            282,
            164);
        var teamColor = winnerTeam == PlayerTeam.Blue
            ? new Color(80, 104, 124)
            : new Color(171, 78, 70);
        var innerColor = winnerTeam == PlayerTeam.Blue
            ? new Color(91, 119, 142)
            : new Color(177, 93, 88);
        DrawInsetHudPanel(bounds, teamColor, innerColor);
        DrawPostGameMvpTextCentered(
            winnerTeam == PlayerTeam.Blue ? "BLUE TEAM WON!" : "RED TEAM WON!",
            new Vector2(boardCenter.X, bounds.Y + 46f),
            Color.White,
            1f);
        DrawPostGameMvpText("MVPs", new Vector2(bounds.X + 12f, bounds.Y + 72f), Color.White, 1f);
        DrawPostGameMvpText("Kills", new Vector2(bounds.X + 90f, bounds.Y + 72f), Color.White, 1f);
        DrawPostGameMvpText("Healing", new Vector2(bounds.X + 154f, bounds.Y + 72f), Color.White, 1f);
        DrawPostGameMvpText("Score", new Vector2(bounds.X + 232f, bounds.Y + 72f), Color.White, 1f);
    }

    private void DrawPostGameMvpText(string text, Vector2 position, Color color, float scale)
    {
        DrawBitmapFontText(text, position, color, scale);
    }

    private void DrawPostGameMvpTextCentered(string text, Vector2 position, Color color, float scale)
    {
        var width = MeasureBitmapFontWidth(text, scale);
        var height = MeasureBitmapFontHeight(scale);
        DrawPostGameMvpText(text, new Vector2(position.X - (width * 0.5f), position.Y - (height * 0.5f)), color, scale);
    }

    private void DrawPostGameMvpTextRightAligned(string text, Vector2 position, Color color, float scale)
    {
        DrawPostGameMvpText(text, new Vector2(position.X - MeasureBitmapFontWidth(text, scale), position.Y), color, scale);
    }

    private string TrimPostGameMvpText(string text, float maxWidth, float scale)
    {
        if (string.IsNullOrEmpty(text) || MeasureBitmapFontWidth(text, scale) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        var trimmed = text;
        while (trimmed.Length > 0 && MeasureBitmapFontWidth(trimmed + ellipsis, scale) > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }
}
