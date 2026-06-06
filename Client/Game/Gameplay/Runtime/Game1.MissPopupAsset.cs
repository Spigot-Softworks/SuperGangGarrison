#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private LoadedSpriteFrame? _gameplayMissPopupFrame;
    private string? _gameplayMissPopupFramePath;

    private bool DrawGameplayMissPopupImage(Vector2 centerPosition, float alpha, float scale = 1f)
    {
        var frame = GetGameplayMissPopupFrame();
        if (frame is null)
        {
            return false;
        }

        alpha = Math.Clamp(alpha, 0f, 1f);
        var origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
        var drawScale = new Vector2(scale, scale);
        DrawSpriteFrame(frame, centerPosition + new Vector2(2f, 2f), Color.Black * (alpha * 0.45f), 0f, origin, drawScale);
        DrawSpriteFrame(frame, centerPosition, Color.White * alpha, 0f, origin, drawScale);
        return true;
    }

    private LoadedSpriteFrame? GetGameplayMissPopupFrame()
    {
        var path = ContentRoot.GetPath("Sprites", "HUDs", "Feedback", "miss.png");
        if (string.IsNullOrWhiteSpace(path) || !CanLoadSpriteFrameFromPath(path))
        {
            DisposeGameplayMissPopupFrame();
            return null;
        }

        if (_gameplayMissPopupFrame is not null
            && string.Equals(_gameplayMissPopupFramePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return _gameplayMissPopupFrame;
        }

        DisposeGameplayMissPopupFrame();
        _gameplayMissPopupFrame = LoadSpriteFrameFromPath(path);
        _gameplayMissPopupFramePath = path;
        return _gameplayMissPopupFrame;
    }

    private void DisposeGameplayMissPopupFrame()
    {
        _gameplayMissPopupFrame?.Dispose();
        _gameplayMissPopupFrame = null;
        _gameplayMissPopupFramePath = null;
    }
}
