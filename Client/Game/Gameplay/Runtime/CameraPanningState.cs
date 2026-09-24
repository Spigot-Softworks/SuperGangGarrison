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
    /// <summary>
    /// Dead-zone horizontal radius as a fraction of screen width (diameter = 40% wide).
    /// </summary>
    internal const float DeadZoneRadiusScreenWidthFraction = 0.2f;
    /// <summary>
    /// Max dead-zone height as a fraction of screen height (diameter ≤ 60% tall).
    /// On wide aspect ratios this clamps the vertical radius below the width-based value.
    /// </summary>
    internal const float DeadZoneMaxHeightScreenFraction = 0.6f;
    /// <summary>
    /// Fraction of the centre-to-edge distance along the cursor ray at which pan
    /// reaches full strength.
    /// </summary>
    internal const float FullPanScreenFraction = 0.9f;
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

            return ToPanOffset(direction);
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

        var panTarget = ToPanOffset(direction);
        if (panTarget.LengthSquared() > DirectionEpsilonSquared)
        {
            _targetOffset = panTarget;
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
            // Inside the centre dead zone: keep the last target until a valid
            // direction arrives again.
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

    /// <summary>
    /// Returns a screen-space aim vector whose length is pan strength in [0, 1].
    /// Inside the centre dead-zone ellipse, strength is 0. Outside it ramps smoothly
    /// and reaches 1 when the cursor is <see cref="FullPanScreenFraction"/> of the
    /// way from centre to the viewport edge along the cursor ray.
    /// </summary>
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

        var offset = new Vector2(mouseX - halfWidth, mouseY - halfHeight);
        if (!IsFinite(offset))
        {
            return Vector2.Zero;
        }

        var (deadZoneRadiusX, deadZoneRadiusY) = ResolveDeadZoneRadii(viewportWidth, viewportHeight);
        if (IsInsideDeadZoneEllipse(offset, deadZoneRadiusX, deadZoneRadiusY))
        {
            return Vector2.Zero;
        }

        var distanceSquared = offset.LengthSquared();
        if (distanceSquared <= DirectionEpsilonSquared)
        {
            return Vector2.Zero;
        }

        var distance = MathF.Sqrt(distanceSquared);
        var direction = offset / distance;
        var deadZoneDistance = GetRayDistanceToDeadZoneEdge(
            direction.X,
            direction.Y,
            deadZoneRadiusX,
            deadZoneRadiusY);
        var edgeDistance = GetRayDistanceToViewportEdge(direction.X, direction.Y, halfWidth, halfHeight);
        var fullPanDistance = edgeDistance * FullPanScreenFraction;
        if (!float.IsFinite(deadZoneDistance)
            || !float.IsFinite(fullPanDistance)
            || fullPanDistance <= deadZoneDistance)
        {
            return direction;
        }

        var strength = Math.Clamp(
            (distance - deadZoneDistance) / (fullPanDistance - deadZoneDistance),
            0f,
            1f);
        direction *= strength;
        return IsFinite(direction) ? direction : Vector2.Zero;
    }

    internal static (float RadiusX, float RadiusY) ResolveDeadZoneRadii(int viewportWidth, int viewportHeight)
    {
        var radiusX = MathF.Max(0f, viewportWidth * DeadZoneRadiusScreenWidthFraction);
        var maxRadiusY = MathF.Max(0f, viewportHeight * DeadZoneMaxHeightScreenFraction * 0.5f);
        var radiusY = MathF.Min(radiusX, maxRadiusY);
        return (radiusX, radiusY);
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

    /// <summary>
    /// Maps a strength-scaled aim vector (length in [0, 1]) to a world-pixel pan offset.
    /// </summary>
    internal static Vector2 ToPanOffset(Vector2 direction)
    {
        if (!IsFinite(direction))
        {
            return Vector2.Zero;
        }

        var lengthSquared = direction.LengthSquared();
        if (lengthSquared <= DirectionEpsilonSquared)
        {
            return Vector2.Zero;
        }

        var length = MathF.Sqrt(lengthSquared);
        if (length > 1f)
        {
            direction /= length;
        }

        return direction * OffsetPixels;
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

    private static float GetRayDistanceToViewportEdge(float dirX, float dirY, float halfWidth, float halfHeight)
    {
        var distanceX = MathF.Abs(dirX) > 0.000001f
            ? halfWidth / MathF.Abs(dirX)
            : float.PositiveInfinity;
        var distanceY = MathF.Abs(dirY) > 0.000001f
            ? halfHeight / MathF.Abs(dirY)
            : float.PositiveInfinity;
        return MathF.Min(distanceX, distanceY);
    }

    private static float GetRayDistanceToDeadZoneEdge(
        float dirX,
        float dirY,
        float deadZoneRadiusX,
        float deadZoneRadiusY)
    {
        if (deadZoneRadiusX <= 0f || deadZoneRadiusY <= 0f)
        {
            return 0f;
        }

        var scaleSquared = (dirX * dirX) / (deadZoneRadiusX * deadZoneRadiusX)
            + (dirY * dirY) / (deadZoneRadiusY * deadZoneRadiusY);
        if (!float.IsFinite(scaleSquared) || scaleSquared <= 0f)
        {
            return 0f;
        }

        return 1f / MathF.Sqrt(scaleSquared);
    }

    private static bool IsInsideDeadZoneEllipse(Vector2 offset, float deadZoneRadiusX, float deadZoneRadiusY)
    {
        if (deadZoneRadiusX <= 0f || deadZoneRadiusY <= 0f)
        {
            return false;
        }

        var normalized = (offset.X * offset.X) / (deadZoneRadiusX * deadZoneRadiusX)
            + (offset.Y * offset.Y) / (deadZoneRadiusY * deadZoneRadiusY);
        return float.IsFinite(normalized) && normalized <= 1f;
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
