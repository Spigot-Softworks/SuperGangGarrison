#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string LoadingOverlayTitle = "Smoke - Loading";
    private const int LoadingOverlayWidth = 340;
    private const int LoadingOverlayHeight = 84;
    private const int LoadingOverlayMargin = 12;
    private const int LoadingOverlayProgressSegments = 20;
    private const float LoadingOverlayTextScale = 1f;

    private bool _loadingOverlayVisible;
    private string _loadingOverlayMessage = string.Empty;
    private string _joiningServerLoadingLabel = string.Empty;
    private double? _loadingOverlayProgress;

    private void ShowLoadingOverlay(string message, double? progress = null)
    {
        _loadingOverlayVisible = true;
        _loadingOverlayMessage = string.IsNullOrWhiteSpace(message) ? "Loading..." : message.Trim();
        _loadingOverlayProgress = NormalizeLoadingOverlayProgress(progress);
    }

    private void HideLoadingOverlay()
    {
        _loadingOverlayVisible = false;
        _loadingOverlayMessage = string.Empty;
        _loadingOverlayProgress = null;
    }

    private void DrawLoadingOverlay()
    {
        if (!_loadingOverlayVisible)
        {
            return;
        }

        var width = Math.Min(LoadingOverlayWidth, Math.Max(180, ViewportWidth - (LoadingOverlayMargin * 2)));
        var height = LoadingOverlayHeight;
        var x = Math.Max(LoadingOverlayMargin, ViewportWidth - width - LoadingOverlayMargin);
        var y = Math.Max(LoadingOverlayMargin, ViewportHeight - height - 28);
        var bounds = new Rectangle(x, y, width, height);

        DrawRoundedRectangleOutline(bounds, new Color(54, 51, 50) * 0.96f, new Color(119, 119, 119), outlineThickness: 1, radius: 4);
        DrawBitmapFontText(LoadingOverlayTitle, new Vector2(bounds.X + 10f, bounds.Y + 8f), Color.White, LoadingOverlayTextScale);

        var message = TrimBitmapMenuText(_loadingOverlayMessage, bounds.Width - 20f, LoadingOverlayTextScale);
        DrawBitmapFontText(message, new Vector2(bounds.X + 10f, bounds.Y + 32f), Color.White, LoadingOverlayTextScale);
        DrawLoadingOverlayProgress(bounds);
    }

    private void ShowJoiningServerLoadingOverlay(string? serverLabel = null)
    {
        ShowLoadingOverlay(CreateJoiningServerLoadingMessage(serverLabel), progress: null);
    }

    private void SetJoiningServerLoadingLabel(string? serverLabel)
    {
        _joiningServerLoadingLabel = NormalizeLoadingOverlayServerLabel(serverLabel);
    }

    private string CreateJoiningServerLoadingMessage(string? serverLabel = null)
    {
        var resolvedServerLabel = NormalizeLoadingOverlayServerLabel(serverLabel);
        if (string.IsNullOrWhiteSpace(resolvedServerLabel))
        {
            resolvedServerLabel = _joiningServerLoadingLabel;
        }

        if (string.IsNullOrWhiteSpace(resolvedServerLabel))
        {
            resolvedServerLabel = NormalizeLoadingOverlayServerLabel(_networkClient.ServerDescription);
        }

        if (string.IsNullOrWhiteSpace(resolvedServerLabel))
        {
            resolvedServerLabel = "server";
        }

        return $"Joining {resolvedServerLabel}...";
    }

    private static string NormalizeLoadingOverlayServerLabel(string? serverLabel)
    {
        return string.IsNullOrWhiteSpace(serverLabel)
            ? string.Empty
            : serverLabel.Trim();
    }

    private void DrawLoadingOverlayProgress(Rectangle bounds)
    {
        var progressBounds = new Rectangle(bounds.X + 10, bounds.Bottom - 23, bounds.Width - 20, 14);
        _spriteBatch.Draw(_pixel, new Rectangle(progressBounds.X, progressBounds.Y, progressBounds.Width, 1), new Color(119, 119, 119));
        _spriteBatch.Draw(_pixel, new Rectangle(progressBounds.X, progressBounds.Bottom - 1, progressBounds.Width, 1), new Color(119, 119, 119));
        _spriteBatch.Draw(_pixel, new Rectangle(progressBounds.X, progressBounds.Y, 1, progressBounds.Height), new Color(119, 119, 119));
        _spriteBatch.Draw(_pixel, new Rectangle(progressBounds.Right - 1, progressBounds.Y, 1, progressBounds.Height), new Color(119, 119, 119));

        var inner = new Rectangle(progressBounds.X + 1, progressBounds.Y + 1, progressBounds.Width - 2, progressBounds.Height - 2);
        _spriteBatch.Draw(_pixel, inner, new Color(54, 51, 50));

        var availableWidth = inner.Width - 4;
        var segmentStride = Math.Max(6, availableWidth / LoadingOverlayProgressSegments);
        var segmentWidth = Math.Max(3, Math.Min(8, segmentStride - 3));
        var segmentCount = _loadingOverlayProgress.HasValue
            ? Math.Clamp((int)Math.Floor(_loadingOverlayProgress.Value * LoadingOverlayProgressSegments), 0, LoadingOverlayProgressSegments)
            : 3;
        var indeterminateOffset = _loadingOverlayProgress.HasValue
            ? 0
            : (int)((Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency) * 8.0) % LoadingOverlayProgressSegments;

        for (var index = 0; index < segmentCount; index += 1)
        {
            var resolvedIndex = _loadingOverlayProgress.HasValue
                ? index
                : (indeterminateOffset + index) % LoadingOverlayProgressSegments;
            var left = inner.X + 2 + (resolvedIndex * segmentStride);
            if (left + segmentWidth > inner.Right - 2)
            {
                continue;
            }

            _spriteBatch.Draw(_pixel, new Rectangle(left, inner.Y + 3, segmentWidth, Math.Max(2, inner.Height - 6)), new Color(69, 108, 140));
        }
    }

    private static double? NormalizeLoadingOverlayProgress(double? progress)
    {
        return progress.HasValue
            ? Math.Clamp(progress.Value, 0d, 1d)
            : null;
    }
}
