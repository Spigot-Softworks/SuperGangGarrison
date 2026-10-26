#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

/// <summary>
/// Modern builder: the properties editor lives in the left section instead of a box in the
/// middle of the screen. Opening it widens that section (the toolbar and map move over with
/// it) and the editor slides in over the entity list; closing it (Esc, Enter, clicking away)
/// slides it back out and the entity list returns.
/// </summary>
/// <remarks>
/// The editor's layout and input all come from <see cref="GetGarrisonBuilderPropertyEditorBounds"/>,
/// which in the modern UI is the docked section. Only drawing follows the animation; input
/// uses the final position, so nothing has to wait for the slide to finish.
/// </remarks>
public partial class Game1
{
    /// <summary>How long the left section takes to switch between the entity list and properties.</summary>
    private const float GarrisonBuilderPropertyDockSeconds = 0.18f;

    /// <summary>Width the properties section aims for (the old properties box was this wide).</summary>
    private const int GarrisonBuilderPropertyDockPreferredWidth = 420;

    /// <summary>Map width always left beside the docked properties on small windows.</summary>
    private const int GarrisonBuilderPropertyDockMinMapWidth = 360;

    /// <summary>0 = entity list, 1 = properties fully docked.</summary>
    private float _builderPropertyDockProgress;

    private Rectangle _builderPropertyDockSavedScissor;

    /// <summary>True while properties are open in the modern UI (docked in the left section).</summary>
    private bool IsGarrisonBuilderPropertyDockOpen =>
        _builderUseModernUi && _builderPropertyTarget != GarrisonBuilderPropertyTarget.None;

    private bool IsGarrisonBuilderPropertyMapPickActive =>
        _builderObjectiveMapPickActive
        || _builderLogicMapPickActive
        || _builderEntityMapPickActive
        || _builderMultiEntityMapPickActive;

    /// <summary>True while the left section shows (or is moving to or from) the properties.</summary>
    private bool IsGarrisonBuilderPropertyDockVisible => _builderUseModernUi && _builderPropertyDockProgress > 0f;

    private void UpdateGarrisonBuilderPropertyDock(float deltaSeconds)
    {
        var target = IsGarrisonBuilderPropertyDockOpen ? 1f : 0f;
        var step = MathF.Max(0f, deltaSeconds) / GarrisonBuilderPropertyDockSeconds;
        _builderPropertyDockProgress = _builderPropertyDockProgress < target
            ? MathF.Min(target, _builderPropertyDockProgress + step)
            : MathF.Max(target, _builderPropertyDockProgress - step);
    }

    /// <summary>Jumps straight to the resting state (when the builder is shown again).</summary>
    private void SnapGarrisonBuilderPropertyDock()
    {
        _builderPropertyDockProgress = IsGarrisonBuilderPropertyDockOpen ? 1f : 0f;
    }

    private float GetGarrisonBuilderPropertyDockEase()
    {
        var t = Math.Clamp(_builderPropertyDockProgress, 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>Width of the entity list section on its own (expanded or collapsed).</summary>
    private int GetModernGarrisonBuilderPaletteWidth()
    {
        return _builderEntityPaletteVisible
            ? ModernBuilderExpandedSidebarWidth
            : ModernBuilderCollapsedPaletteWidth;
    }

    /// <summary>Width of the left section with properties docked.</summary>
    private int GetGarrisonBuilderPropertyDockWidth()
    {
        return Math.Max(
            ModernBuilderExpandedSidebarWidth,
            Math.Min(GarrisonBuilderPropertyDockPreferredWidth, BuilderViewportWidth - GarrisonBuilderPropertyDockMinMapWidth));
    }

    /// <summary>The left section's full rectangle at a given width (below the menu bar, above the layer strip).</summary>
    private Rectangle GetModernGarrisonBuilderLeftSectionBounds(int width)
    {
        var top = GetModernBuilderMenuBarHeight();
        return new Rectangle(0, top, width, Math.Max(1, BuilderViewportHeight - top - GetModernGarrisonBuilderLayerStripHeight()));
    }

    /// <summary>Where the docked properties sit (their final place, whatever the animation).</summary>
    private Rectangle GetGarrisonBuilderPropertyDockBounds()
    {
        return GetModernGarrisonBuilderLeftSectionBounds(GetGarrisonBuilderPropertyDockWidth());
    }

    /// <summary>How far left of its docked place the properties panel is drawn mid-slide.</summary>
    private float GetGarrisonBuilderPropertyDockSlideOffset()
    {
        return MathF.Round(-(1f - GetGarrisonBuilderPropertyDockEase()) * GetGarrisonBuilderPropertyDockWidth());
    }

    /// <summary>
    /// Draws the properties editor in the left section, sliding in while the section opens.
    /// Once docked it draws exactly as before, unclipped.
    /// </summary>
    private void DrawGarrisonBuilderPropertyEditor(MouseState mouse)
    {
        if (!_builderUseModernUi
            || _builderPropertyTarget == GarrisonBuilderPropertyTarget.None
            || _builderPropertyDockProgress >= 1f)
        {
            DrawGarrisonBuilderPropertyEditorPanel(mouse);
            return;
        }

        BeginGarrisonBuilderPropertyDockClip(GetModernGarrisonBuilderLeftSectionBounds(GetModernGarrisonBuilderSidebarWidth()), GetGarrisonBuilderPropertyDockSlideOffset());
        try
        {
            DrawGarrisonBuilderPropertyEditorPanel(mouse);
        }
        finally
        {
            EndGarrisonBuilderPropertyDockClip();
        }
    }

    /// <summary>
    /// The left section while properties are not being drawn in it: sliding out after they
    /// close (a plain panel leaving over the entity list), or held open while a property
    /// waits for a pick on the map.
    /// </summary>
    private void DrawGarrisonBuilderPropertyDockCover(Rectangle section)
    {
        if (!IsGarrisonBuilderPropertyDockVisible)
        {
            return;
        }

        var editorDrawn = IsGarrisonBuilderPropertyDockOpen && !IsGarrisonBuilderPropertyMapPickActive;
        if (editorDrawn)
        {
            return;
        }

        var picking = IsGarrisonBuilderPropertyDockOpen && IsGarrisonBuilderPropertyMapPickActive;
        BeginGarrisonBuilderPropertyDockClip(section, GetGarrisonBuilderPropertyDockSlideOffset());
        try
        {
            var dock = GetGarrisonBuilderPropertyDockBounds();
            DrawGarrisonBuilderBrownPanel(dock);
            if (picking)
            {
                var scale = GetGarrisonBuilderBitmapFontScale();
                DrawBitmapFontText(_builderPropertyEditorTitle, new Vector2(dock.X + 12f, dock.Y + 8f), Color.White, scale);
                DrawBitmapFontText("Pick on the map.", new Vector2(dock.X + 12f, dock.Y + 40f), new Color(255, 226, 140), scale);
                DrawBitmapFontText("Wheel: zoom", new Vector2(dock.X + 12f, dock.Y + 68f), new Color(200, 190, 168), scale);
                DrawBitmapFontText("WASD / arrows: pan", new Vector2(dock.X + 12f, dock.Y + 92f), new Color(200, 190, 168), scale);
                DrawBitmapFontText("Middle-drag: pan", new Vector2(dock.X + 12f, dock.Y + 116f), new Color(200, 190, 168), scale);
                DrawBitmapFontText("Esc: back to properties", new Vector2(dock.X + 12f, dock.Bottom - 20f), new Color(200, 190, 168), scale);
            }
        }
        finally
        {
            EndGarrisonBuilderPropertyDockClip();
        }
    }

    /// <summary>Restarts the UI batch clipped to <paramref name="clip"/> and moved right by <paramref name="offsetX"/>.</summary>
    private void BeginGarrisonBuilderPropertyDockClip(Rectangle clip, float offsetX)
    {
        _spriteBatch.End();
        _builderPropertyDockSavedScissor = GraphicsDevice.ScissorRectangle;
        var scissor = Rectangle.Intersect(clip, GraphicsDevice.Viewport.Bounds);
        GraphicsDevice.ScissorRectangle = scissor.Width > 0 && scissor.Height > 0 ? scissor : new Rectangle(0, 0, 1, 1);
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            GarrisonBuilderLightingScissorState,
            null,
            Matrix.CreateTranslation(offsetX, 0f, 0f));
    }

    /// <summary>Back to the builder's normal UI batch.</summary>
    private void EndGarrisonBuilderPropertyDockClip()
    {
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = _builderPropertyDockSavedScissor;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }
}
