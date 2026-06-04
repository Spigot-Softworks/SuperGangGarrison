#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace OpenGarrison.Client;

internal readonly struct ScrollbarMetrics
{
    public Rectangle TrackBounds { get; init; }
    public Rectangle ThumbBounds { get; init; }
    public int MaxScrollOffset { get; init; }
    public bool IsScrollable => MaxScrollOffset > 0 && TrackBounds.Width > 0 && TrackBounds.Height > 0;
}

internal static class ScrollbarLayout
{
    public static ScrollbarMetrics Compute(
        Rectangle trackBounds,
        int scrollOffset,
        int itemCount,
        int visibleItemCount,
        int minThumbHeight = 24,
        bool invertVertical = false)
    {
        if (itemCount <= visibleItemCount || trackBounds.Height <= 0)
        {
            return new ScrollbarMetrics
            {
                TrackBounds = trackBounds,
                ThumbBounds = trackBounds,
                MaxScrollOffset = 0,
            };
        }

        var maxScrollOffset = Math.Max(0, itemCount - visibleItemCount);
        var clampedOffset = Math.Clamp(scrollOffset, 0, maxScrollOffset);
        var thumbHeight = Math.Max(minThumbHeight, (int)MathF.Round(trackBounds.Height * (visibleItemCount / (float)itemCount)));
        var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
        var fraction = maxScrollOffset == 0 ? 0f : clampedOffset / (float)maxScrollOffset;
        if (invertVertical)
        {
            fraction = 1f - fraction;
        }

        var thumbY = trackBounds.Y + (int)MathF.Round(fraction * thumbTravel);
        return new ScrollbarMetrics
        {
            TrackBounds = trackBounds,
            ThumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight),
            MaxScrollOffset = maxScrollOffset,
        };
    }

    public static ScrollbarMetrics ComputeForRange(
        Rectangle trackBounds,
        int scrollOffset,
        int maxScrollOffset,
        int viewportSize,
        int contentSize,
        int minThumbHeight = 24,
        bool invertVertical = false)
    {
        if (contentSize <= viewportSize || maxScrollOffset <= 0 || trackBounds.Height <= 0)
        {
            return new ScrollbarMetrics
            {
                TrackBounds = trackBounds,
                ThumbBounds = trackBounds,
                MaxScrollOffset = 0,
            };
        }

        var clampedOffset = Math.Clamp(scrollOffset, 0, maxScrollOffset);
        var thumbHeight = Math.Max(minThumbHeight, (int)MathF.Round(trackBounds.Height * (viewportSize / (float)contentSize)));
        var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
        var fraction = clampedOffset / (float)maxScrollOffset;
        if (invertVertical)
        {
            fraction = 1f - fraction;
        }

        var thumbY = trackBounds.Y + (int)MathF.Round(fraction * thumbTravel);
        return new ScrollbarMetrics
        {
            TrackBounds = trackBounds,
            ThumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight),
            MaxScrollOffset = maxScrollOffset,
        };
    }
}

internal sealed class ScrollbarDragController
{
    private object? _owner;
    private Rectangle _trackBounds;
    private int _thumbHeight;
    private int _maxScrollOffset;
    private int _grabOffsetFromThumbTop;
    private bool _invertVertical;

    public bool IsActive => _owner is not null;

    public bool IsOwnedBy(object owner) => ReferenceEquals(_owner, owner);

    public void NotifyMouseReleased(MouseState mouse, MouseState previousMouse)
    {
        if (_owner is null)
        {
            return;
        }

        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            Clear();
        }
    }

    public void Clear()
    {
        _owner = null;
    }

    public bool Handle(
        MouseState mouse,
        MouseState previousMouse,
        object owner,
        ScrollbarMetrics metrics,
        ref int scrollOffset)
    {
        if (!metrics.IsScrollable)
        {
            if (ReferenceEquals(_owner, owner))
            {
                Clear();
            }

            return false;
        }

        if (ReferenceEquals(_owner, owner))
        {
            if (mouse.LeftButton != ButtonState.Pressed)
            {
                Clear();
                return false;
            }

            scrollOffset = GetScrollOffsetFromMouseY(mouse.Y);
            return true;
        }

        if (_owner is not null)
        {
            return false;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return false;
        }

        if (!metrics.TrackBounds.Contains(mouse.Position) && !metrics.ThumbBounds.Contains(mouse.Position))
        {
            return false;
        }

        Begin(owner, metrics, mouse.Position);
        scrollOffset = GetScrollOffsetFromMouseY(mouse.Y);
        return true;
    }

    private void Begin(object owner, ScrollbarMetrics metrics, Point mousePosition)
    {
        _owner = owner;
        _trackBounds = metrics.TrackBounds;
        _thumbHeight = metrics.ThumbBounds.Height;
        _maxScrollOffset = metrics.MaxScrollOffset;
        _invertVertical = false;
        _grabOffsetFromThumbTop = metrics.ThumbBounds.Contains(mousePosition)
            ? mousePosition.Y - metrics.ThumbBounds.Y
            : _thumbHeight / 2;
    }

    public bool HandleInverted(
        MouseState mouse,
        MouseState previousMouse,
        object owner,
        ScrollbarMetrics metrics,
        ref int scrollOffset)
    {
        if (!metrics.IsScrollable)
        {
            if (ReferenceEquals(_owner, owner))
            {
                Clear();
            }

            return false;
        }

        if (ReferenceEquals(_owner, owner))
        {
            if (mouse.LeftButton != ButtonState.Pressed)
            {
                Clear();
                return false;
            }

            scrollOffset = GetScrollOffsetFromMouseY(mouse.Y, invertVertical: true);
            return true;
        }

        if (_owner is not null)
        {
            return false;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return false;
        }

        if (!metrics.TrackBounds.Contains(mouse.Position) && !metrics.ThumbBounds.Contains(mouse.Position))
        {
            return false;
        }

        _owner = owner;
        _trackBounds = metrics.TrackBounds;
        _thumbHeight = metrics.ThumbBounds.Height;
        _maxScrollOffset = metrics.MaxScrollOffset;
        _invertVertical = true;
        _grabOffsetFromThumbTop = metrics.ThumbBounds.Contains(mouse.Position)
            ? mouse.Y - metrics.ThumbBounds.Y
            : _thumbHeight / 2;
        scrollOffset = GetScrollOffsetFromMouseY(mouse.Y, invertVertical: true);
        return true;
    }

    private int GetScrollOffsetFromMouseY(int mouseY, bool? invertVertical = null)
    {
        var invert = invertVertical ?? _invertVertical;
        var thumbTravel = Math.Max(0, _trackBounds.Height - _thumbHeight);
        var thumbY = Math.Clamp(mouseY - _grabOffsetFromThumbTop, _trackBounds.Y, _trackBounds.Y + thumbTravel);
        if (thumbTravel == 0)
        {
            return 0;
        }

        var fraction = (thumbY - _trackBounds.Y) / (float)thumbTravel;
        if (invert)
        {
            fraction = 1f - fraction;
        }

        return Math.Clamp((int)MathF.Round(fraction * _maxScrollOffset), 0, _maxScrollOffset);
    }
}
