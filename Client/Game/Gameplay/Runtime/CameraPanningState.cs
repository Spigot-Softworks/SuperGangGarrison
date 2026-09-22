#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Stateful, frame-clocked camera panning direction.  The state object owns the
/// offset interpolation so camera probes can ask for a preview without
/// advancing the offset a second time in the same frame.
/// </summary>
internal sealed class CameraPanningState
{
    internal const float OffsetPixels = 64f;
    internal const float CenterDeadZoneRadius = 0.06f;
    private const float OffsetCatchUpRate = 18f;
    private const float DirectionEpsilonSquared = 0.000001f;

    private Vector2 _offset;
    private Vector2 _targetOffset;
    private double _lastUpdateClockSeconds = double.NaN;
    private bool _hasDirection;

    public Vector2 Update(Vector2 direction, float elapsedSeconds, double frameClockSeconds, bool advance)
    {
        if (!advance)
        {
            if (_hasDirection)
            {
                return _offset;
            }

            return direction.LengthSquared() > DirectionEpsilonSquared && IsFinite(direction)
                ? Vector2.Normalize(direction) * OffsetPixels
                : Vector2.Zero;
        }

        if (IsFiniteClock(frameClockSeconds)
            && IsFiniteClock(_lastUpdateClockSeconds)
            && frameClockSeconds == _lastUpdateClockSeconds)
        {
            return _hasDirection ? _offset : Vector2.Zero;
        }

        if (IsFiniteClock(frameClockSeconds))
        {
            _lastUpdateClockSeconds = frameClockSeconds;
        }

        if (direction.LengthSquared() > DirectionEpsilonSquared && IsFinite(direction))
        {
            direction.Normalize();
            _targetOffset = direction * OffsetPixels;
            if (!_hasDirection)
            {
                _offset = _targetOffset;
                _hasDirection = true;
            }
            else
            {
                _offset = AdvanceOffset(_offset, _targetOffset, elapsedSeconds);
            }
        }
        else if (_hasDirection)
        {
            // A zero direction is the aspect-correct mouse center dead zone.
            // Keep the last target offset until a valid direction
            // arrives again.
            _offset = AdvanceOffset(_offset, _targetOffset, elapsedSeconds);
        }

        return _hasDirection ? _offset : Vector2.Zero;
    }

    public void Reset()
    {
        _offset = Vector2.Zero;
        _targetOffset = Vector2.Zero;
        _lastUpdateClockSeconds = double.NaN;
        _hasDirection = false;
    }

    internal static Vector2 GetMouseDirection(int viewportWidth, int viewportHeight, float mouseX, float mouseY)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || !float.IsFinite(mouseX) || !float.IsFinite(mouseY))
        {
            return Vector2.Zero;
        }

        var halfWidth = viewportWidth * 0.5f;
        var halfHeight = viewportHeight * 0.5f;
        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return Vector2.Zero;
        }

        // Normalize each axis before calculating the angle.  This turns the
        // viewport ellipse into a circle, so equal screen fractions pan equally
        // at 16:9, 4:3, and other aspect ratios.
        var direction = new Vector2(
            (mouseX - halfWidth) / halfWidth,
            (mouseY - halfHeight) / halfHeight);
        var lengthSquared = direction.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared <= CenterDeadZoneRadius * CenterDeadZoneRadius)
        {
            return Vector2.Zero;
        }

        direction /= MathF.Sqrt(lengthSquared);
        return IsFinite(direction) ? direction : Vector2.Zero;
    }

    internal static Vector2 AdvanceOffset(Vector2 current, Vector2 target, float elapsedSeconds)
    {
        if (!IsFinite(current) || !IsFinite(target))
            return IsFinite(target) ? target : Vector2.Zero;
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
            return current;
        var catchUp = 1f - MathF.Exp(-OffsetCatchUpRate * MathF.Min(elapsedSeconds, 1f));
        return Vector2.Lerp(current, target, catchUp);
    }

    internal static Vector2 GetMouseDirectionFromPlayer(int width, int height, Vector2 mouse, Vector2 playerScreen)
        => GetMouseDirection(width, height, mouse.X - playerScreen.X + width * 0.5f,
            mouse.Y - playerScreen.Y + height * 0.5f);

    internal static Vector2 ClampToMap(Vector2 topLeft, int viewportWidth, int viewportHeight, WorldBounds bounds)
    {
        if (!IsFinite(topLeft) || viewportWidth < 0 || viewportHeight < 0
            || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)
            || bounds.Width < 0f || bounds.Height < 0f)
        {
            return IsFinite(topLeft) ? topLeft : Vector2.Zero;
        }

        return new Vector2(
            ClampCameraAxis(topLeft.X, viewportWidth, bounds.Width),
            ClampCameraAxis(topLeft.Y, viewportHeight, bounds.Height));
    }

    private static float ClampCameraAxis(float value, int viewportSize, float worldSize)
    {
        var max = worldSize - viewportSize;
        return max >= 0f
            ? Math.Clamp(value, 0f, max)
            : (worldSize - viewportSize) * 0.5f;
    }

    private static bool IsFinite(Vector2 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsFiniteClock(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);
}
