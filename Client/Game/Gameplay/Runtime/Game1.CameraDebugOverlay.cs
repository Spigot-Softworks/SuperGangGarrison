#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int CameraDebugSampleWindow = 60;

    private bool _cameraDebugOverlayEnabled;
    private int _cameraDebugLastClientTicks;
    private bool _cameraDebugHasPreviousSample;
    private float _cameraDebugPreviousScreenX;
    private float _cameraDebugPreviousRenderX;
    private float _cameraDebugPreviousCameraX;
    private float _cameraDebugPreviousWorldX;
    private int _cameraDebugSampleIndex;
    private int _cameraDebugSampleCount;
    private readonly float[] _cameraDebugScreenDeltaSamples = new float[CameraDebugSampleWindow];
    private readonly List<string> _cameraDebugOverlayLines = new(capacity: 16);

    private void ObserveCameraDebugFrameTicks(int clientTicks)
    {
        _cameraDebugLastClientTicks = clientTicks;
    }

    private void HandleCameraDebugConsoleCommand(string[] parts)
    {
        if (parts.Length < 2 || parts[1].Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            _cameraDebugOverlayEnabled = !_cameraDebugOverlayEnabled;
        }
        else if (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("1", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            _cameraDebugOverlayEnabled = true;
        }
        else if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("0", StringComparison.OrdinalIgnoreCase)
            || parts[1].Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            _cameraDebugOverlayEnabled = false;
        }
        else if (!parts[1].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            AddConsoleLine("usage: camdebug <on|off|toggle|status>");
            return;
        }

        if (!_cameraDebugOverlayEnabled)
        {
            _cameraDebugHasPreviousSample = false;
            _cameraDebugSampleIndex = 0;
            _cameraDebugSampleCount = 0;
        }

        AddConsoleLine(_cameraDebugOverlayEnabled ? "camera debug overlay enabled" : "camera debug overlay disabled");
    }

    private void DrawCameraDebugOverlay(Vector2 cameraPosition)
    {
        if (!_cameraDebugOverlayEnabled)
        {
            return;
        }

        var localPlayer = _world.LocalPlayer;
        var rawPosition = new Vector2(localPlayer.X, localPlayer.Y);
        var renderPosition = localPlayer.IsAlive
            ? GetRenderPosition(localPlayer)
            : rawPosition;
        var screenPosition = localPlayer.IsAlive
            ? GetPlayerSpriteScreenOrigin(renderPosition, cameraPosition)
            : renderPosition - cameraPosition;
        var screenDeltaX = _cameraDebugHasPreviousSample
            ? screenPosition.X - _cameraDebugPreviousScreenX
            : 0f;
        var worldScreenX = -cameraPosition.X;
        var worldDeltaX = _cameraDebugHasPreviousSample
            ? worldScreenX - _cameraDebugPreviousWorldX
            : 0f;
        var renderDeltaX = _cameraDebugHasPreviousSample
            ? renderPosition.X - _cameraDebugPreviousRenderX
            : 0f;
        var cameraDeltaX = _cameraDebugHasPreviousSample
            ? cameraPosition.X - _cameraDebugPreviousCameraX
            : 0f;

        if (_cameraDebugHasPreviousSample)
        {
            _cameraDebugScreenDeltaSamples[_cameraDebugSampleIndex] = screenDeltaX;
            _cameraDebugSampleIndex = (_cameraDebugSampleIndex + 1) % CameraDebugSampleWindow;
            _cameraDebugSampleCount = Math.Min(_cameraDebugSampleCount + 1, CameraDebugSampleWindow);
        }

        GetCameraDebugScreenDeltaStats(
            out var screenDeltaAverage,
            out var screenDeltaRange,
            out var screenDeltaDeviation);
        var jitterLevel = GetCameraDebugJitterLevel(screenDeltaRange, screenDeltaDeviation);

        _cameraDebugPreviousScreenX = screenPosition.X;
        _cameraDebugPreviousRenderX = renderPosition.X;
        _cameraDebugPreviousCameraX = cameraPosition.X;
        _cameraDebugPreviousWorldX = worldScreenX;
        _cameraDebugHasPreviousSample = true;

        _cameraDebugOverlayLines.Clear();
        _cameraDebugOverlayLines.Add("CAMDEBUG: player jitter");
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("verdict", jitterLevel));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("watch", "screen step + wiggle"));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("mode", _smoothCameraRenderingActive ? "smooth/fractional" : "pixel"));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("screen step", screenDeltaX));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("screen avg", screenDeltaAverage));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("wiggle 60f", screenDeltaRange));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("variance", screenDeltaDeviation));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("camera step", cameraDeltaX));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("world step", worldDeltaX));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("player step", renderDeltaX));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("sim alpha", _simulator.InterpolationAlpha));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("client ticks", _cameraDebugLastClientTicks.ToString(CultureInfo.InvariantCulture)));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("raw/pred", FormatCameraDebugPair(rawPosition.X, _hasPredictedLocalPlayerPosition ? _predictedLocalPlayerPosition.X : float.NaN)));
        _cameraDebugOverlayLines.Add(FormatCameraDebugLine("smooth/corr", FormatCameraDebugPair(
            _hasSmoothedLocalPlayerRenderPosition ? _smoothedLocalPlayerRenderPosition.X : float.NaN,
            _predictedLocalPlayerRenderCorrectionOffset.X)));

        const int padding = 10;
        const int lineHeight = 17;
        var maxWidth = 0f;
        for (var index = 0; index < _cameraDebugOverlayLines.Count; index += 1)
        {
            maxWidth = MathF.Max(maxWidth, _consoleFont.MeasureString(_cameraDebugOverlayLines[index]).X);
        }

        var width = Math.Max(270, (int)MathF.Ceiling(maxWidth) + (padding * 2));
        var height = (_cameraDebugOverlayLines.Count * lineHeight) + (padding * 2);
        var rectangle = new Rectangle(18, 18, width, height);
        _spriteBatch.Draw(_pixel, rectangle, new Color(12, 16, 20, 220));
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 2), new Color(255, 220, 90));

        var position = new Vector2(rectangle.X + padding, rectangle.Y + padding);
        for (var index = 0; index < _cameraDebugOverlayLines.Count; index += 1)
        {
            var color = GetCameraDebugLineColor(index, jitterLevel);
            _spriteBatch.DrawString(_consoleFont, _cameraDebugOverlayLines[index], position, color);
            position.Y += lineHeight;
        }
    }

    private void GetCameraDebugScreenDeltaStats(out float average, out float range, out float standardDeviation)
    {
        if (_cameraDebugSampleCount == 0)
        {
            average = 0f;
            range = 0f;
            standardDeviation = 0f;
            return;
        }

        var sum = 0f;
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        for (var index = 0; index < _cameraDebugSampleCount; index += 1)
        {
            var value = _cameraDebugScreenDeltaSamples[index];
            sum += value;
            min = MathF.Min(min, value);
            max = MathF.Max(max, value);
        }

        average = sum / _cameraDebugSampleCount;
        range = max - min;

        var varianceSum = 0f;
        for (var index = 0; index < _cameraDebugSampleCount; index += 1)
        {
            var difference = _cameraDebugScreenDeltaSamples[index] - average;
            varianceSum += difference * difference;
        }

        standardDeviation = MathF.Sqrt(varianceSum / _cameraDebugSampleCount);
    }

    private static string GetCameraDebugJitterLevel(float range, float standardDeviation)
    {
        if (range >= 1.25f || standardDeviation >= 0.45f)
        {
            return "HIGH";
        }

        if (range >= 0.65f || standardDeviation >= 0.25f)
        {
            return "MED";
        }

        return "low";
    }

    private static Color GetCameraDebugLineColor(int index, string jitterLevel)
    {
        if (index == 0)
        {
            return new Color(255, 232, 130);
        }

        if (index == 1)
        {
            return jitterLevel.Equals("HIGH", StringComparison.Ordinal)
                ? new Color(255, 115, 95)
                : jitterLevel.Equals("MED", StringComparison.Ordinal)
                    ? new Color(255, 210, 105)
                    : new Color(130, 235, 160);
        }

        if (index is 4 or 6 or 7)
        {
            return new Color(255, 245, 190);
        }

        return new Color(232, 238, 244);
    }

    private static string FormatCameraDebugLine(string label, string value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{label,-12} {value}");
    }

    private static string FormatCameraDebugLine(string label, float value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{label,-12} {FormatCameraDebugValue(value),9}");
    }

    private static string FormatCameraDebugLine(string label, float value, float delta)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{label,-12} {FormatCameraDebugValue(value),9} d={FormatCameraDebugValue(delta),8}");
    }

    private static string FormatCameraDebugValue(float value)
    {
        return float.IsFinite(value)
            ? value.ToString("0.000", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string FormatCameraDebugPair(float first, float second)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{FormatCameraDebugValue(first)}/{FormatCameraDebugValue(second)}");
    }
}
