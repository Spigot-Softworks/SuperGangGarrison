using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private SimpleLevel? _roundEndCountdownLevel;
    private int _roundEndCountdownIndex = -1;
    private int _roundEndCountdownNumber;
    private float _roundEndCountdownPulseAge;

    private void AdvanceMapRoundEndCountdownPresentation()
    {
        _roundEndCountdownPulseAge += (float)ClientUpdateStepSeconds;
        var countdown = _world.MapLogic.GetActiveRoundEndCountdown();
        if (!ReferenceEquals(_roundEndCountdownLevel, _world.Level) || countdown is null)
        {
            _roundEndCountdownLevel = _world.Level;
            _roundEndCountdownIndex = -1;
            _roundEndCountdownNumber = 0;
        }
        if (countdown is not { } active) return;
        var number = (int)MathF.Ceiling(active.SecondsRemaining);
        if (_roundEndCountdownIndex == active.Index && number >= _roundEndCountdownNumber) return;
        _roundEndCountdownIndex = active.Index;
        _roundEndCountdownNumber = number;
        _roundEndCountdownPulseAge = 0f;
        var soundName = active.Definition.TickSound;
        if (string.IsNullOrWhiteSpace(soundName)) return;
        var sound = TryGetGameplayMessageCustomSound(soundName, out var customSound)
            ? customSound : _runtimeAssets.GetSound(soundName);
        if (sound is not null) TryPlaySound(sound, 0.88f, 0f, 0f);
    }

    private void DrawMapRoundEndCountdownHud()
    {
        if (_world.MapLogic.GetActiveRoundEndCountdown() is not { } active || !active.Definition.ShowCountdown) return;
        var position = new Vector2(ViewportWidth * 0.5f, 100f);
        var tint = active.Definition.Winner == PlayerTeam.Red ? new Color(255, 110, 100)
            : active.Definition.Winner == PlayerTeam.Blue ? new Color(120, 185, 255) : Color.White;
        var pulse = 1f - Math.Clamp(_roundEndCountdownPulseAge / 0.22f, 0f, 1f);
        var scale = 3f + pulse * 0.8f;
        var text = Math.Max(1, (int)MathF.Ceiling(active.SecondsRemaining)).ToString(CultureInfo.InvariantCulture);
        DrawHudTextCentered(text, position + new Vector2(2f, 2f), Color.Black * 0.85f, scale);
        DrawHudTextCentered(text, position, tint, scale);
    }
}
