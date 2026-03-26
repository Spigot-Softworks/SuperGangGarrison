#nullable enable

using Microsoft.Xna.Framework;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawNoticeHud()
    {
        if (_notice is null)
        {
            return;
        }

        if (_killCamEnabled && _world.LocalDeathCam is not null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var alpha = Math.Clamp(_notice.Alpha, 0.01f, 0.99f);
        var tint = Color.White * alpha;
        var barRectangle = new Rectangle(0, viewportHeight - 110, viewportWidth, 18);
        var noticeTextY = viewportHeight - 106f;
        _spriteBatch.Draw(_pixel, barRectangle, Color.Black * alpha);
        TryDrawScreenSprite("GameNoticeS", 0, new Vector2(25f, noticeTextY), tint, new Vector2(2f, 2f));

        var text = GetNoticeText(_notice.Kind);
        if (!string.IsNullOrEmpty(text))
        {
            DrawHudTextLeftAligned(text, new Vector2(50f, noticeTextY), tint, 1f);
        }
    }

    private void UpdateLocalSentryNotice()
    {
        if (_networkClient.IsSpectator || _world.LocalPlayerAwaitingJoin)
        {
            _hadLocalSentry = false;
            return;
        }

        var hasSentry = GetLocalOwnedSentry() is not null;
        if (_hadLocalSentry && !hasSentry)
        {
            ShowNotice(NoticeKind.AutogunScrapped);
        }

        _hadLocalSentry = hasSentry;
    }

    private void UpdateIntelNotice()
    {
        if (_networkClient.IsSpectator || _world.LocalPlayerAwaitingJoin)
        {
            _wasCarryingIntel = false;
            return;
        }

        var isCarrying = _world.LocalPlayer.IsCarryingIntel;
        if (!_wasCarryingIntel && isCarrying)
        {
            ShowNotice(NoticeKind.HaveIntel);
        }

        _wasCarryingIntel = isCarrying;
    }

    private void UpdateNoticeState()
    {
        if (_notice is null)
        {
            return;
        }

        if (!_notice.Done)
        {
            if (_notice.Alpha < 0.8f)
            {
                _notice.Alpha = MathF.Min(0.8f, MathF.Pow(MathF.Max(_notice.Alpha, 0.01f), 0.7f));
            }

            _notice.TicksRemaining = Math.Max(0, _notice.TicksRemaining - 1);
            if (_notice.TicksRemaining <= 0)
            {
                _notice.Done = true;
            }
            return;
        }

        if (_notice.Alpha > 0.01f)
        {
            _notice.Alpha = MathF.Max(0.01f, MathF.Pow(_notice.Alpha, 1f / 0.7f));
            return;
        }

        _notice = null;
    }

    private void ShowNotice(NoticeKind kind)
    {
        _notice = new NoticeState(kind, 0.1f, false, 200);
        if (!_audioAvailable)
        {
            return;
        }

        var sound = _runtimeAssets.GetSound("NoticeSnd");
        TryPlaySound(sound, 0.9f, 0f, 0f);
    }

    private static string GetNoticeText(NoticeKind kind)
    {
        return kind switch
        {
            NoticeKind.NutsNBolts => "Not enough Nuts 'N' Bolts to build!",
            NoticeKind.TooClose => "Cannot build this close to another building!",
            NoticeKind.AutogunScrapped => "Autogun scrapped!",
            NoticeKind.AutogunExists => "You already have an autogun built!",
            NoticeKind.HaveIntel => "You have the intelligence!",
            NoticeKind.SetCheckpoint => "Checkpoint set at this location!",
            NoticeKind.DestroyCheckpoint => "Checkpoint destroyed at this location!",
            NoticeKind.PlayerTrackEnable => "Player tracking enabled!",
            NoticeKind.PlayerTrackDisable => "Player tracking disabled!!",
            _ => string.Empty,
        };
    }
}
