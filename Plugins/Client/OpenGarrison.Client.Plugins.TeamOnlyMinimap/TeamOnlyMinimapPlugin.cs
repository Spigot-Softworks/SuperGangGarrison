using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client.Plugins.TeamOnlyMinimap;

public sealed class TeamOnlyMinimapPlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientUpdateHooks,
    IOpenGarrisonClientHudHooks,
    IOpenGarrisonClientOptionsHooks
{
    private static readonly Color ObjectiveAttackOverlay = new(80, 220, 110);
    private static readonly Color ObjectiveDefendOverlay = new(220, 80, 80);
    private static readonly IReadOnlyList<ClientPluginChoiceOptionValue> ShowMethodOptions =
    [
        new ClientPluginChoiceOptionValue((int)MinimapShowMethod.Dots, "Little dots"),
        new ClientPluginChoiceOptionValue((int)MinimapShowMethod.BigDots, "Big dots"),
        new ClientPluginChoiceOptionValue((int)MinimapShowMethod.ClassBubbles, "Class bubbles"),
    ];
    private static readonly IReadOnlyList<ClientPluginChoiceOptionValue> FitModeOptions =
    [
        new ClientPluginChoiceOptionValue((int)MinimapFitMode.Auto, "Auto fit, no scroll"),
        new ClientPluginChoiceOptionValue((int)MinimapFitMode.Width, "Horizontal fit"),
        new ClientPluginChoiceOptionValue((int)MinimapFitMode.Height, "Vertical fit"),
        new ClientPluginChoiceOptionValue((int)MinimapFitMode.Reverse, "Auto fit, auto scroll"),
    ];
    private static readonly IReadOnlyList<ClientPluginChoiceOptionValue> PlayerScopeOptions =
    [
        new ClientPluginChoiceOptionValue((int)MinimapPlayerScope.None, "No one"),
        new ClientPluginChoiceOptionValue((int)MinimapPlayerScope.Myself, "Myself only"),
        new ClientPluginChoiceOptionValue((int)MinimapPlayerScope.Allies, "Allies"),
    ];
    private static readonly IReadOnlyList<ClientPluginChoiceOptionValue> SelfColorOptions =
    [
        new ClientPluginChoiceOptionValue((int)MinimapMarkerColor.Red, "Red"),
        new ClientPluginChoiceOptionValue((int)MinimapMarkerColor.Yellow, "Yellow"),
        new ClientPluginChoiceOptionValue((int)MinimapMarkerColor.Green, "Green"),
        new ClientPluginChoiceOptionValue((int)MinimapMarkerColor.Blue, "Blue"),
        new ClientPluginChoiceOptionValue((int)MinimapMarkerColor.White, "White"),
    ];

    private IOpenGarrisonClientPluginContext? _context;
    private TeamOnlyMinimapConfig _config = new();
    private string _configPath = string.Empty;
    private bool _zooming;

    public string Id => "teamonlyminimap";

    public string DisplayName => "Team Only Minimap";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
        _configPath = Path.Combine(context.ConfigDirectory, "teamonlyminimap.json");
        _config = TeamOnlyMinimapConfig.Load(_configPath);
        context.Ui.RegisterMenuEntry("toggle-minimap", "Toggle Minimap", ClientPluginMenuLocation.InGameMenu, ToggleFromMenu);
    }

    public void Shutdown()
    {
        _zooming = false;
    }

    public void OnClientFrame(ClientFrameEvent e)
    {
        if (!e.IsGameplayActive || _context is null)
        {
            return;
        }

        var state = _context.ClientState;
        if (!_config.Enabled || state.IsSpectator || state.IsGameplayInputBlocked)
        {
            return;
        }

        if (state.WasKeyPressedThisFrame(_config.ZoomKey))
        {
            _zooming = !_zooming;
        }
    }

    public void OnGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas)
    {
        if (_context is null)
        {
            return;
        }

        var state = _context.ClientState;
        if (!_config.Enabled
            || !state.IsGameplayActive
            || state.IsSpectator
            || state.LocalPlayerTeam == ClientPluginTeam.None
            || state.LevelWidth <= 1f
            || state.LevelHeight <= 1f
            || !state.TryGetLocalPlayerWorldPosition(out var localPlayerWorldPosition))
        {
            return;
        }

        var layout = ResolveLayout(state);
        var view = ResolveView(state, localPlayerWorldPosition, layout);
        if (view.Bounds.Width <= 0 || view.Bounds.Height <= 0)
        {
            return;
        }

        DrawBackground(canvas, state, view);
        if (_config.PlayersShown == MinimapPlayerScope.None)
        {
            return;
        }

        DrawPlayerMarkers(canvas, state, view);
        if (_config.ShowSentry)
        {
            DrawSentryMarkers(canvas, state, view);
        }

        if (_config.ShowObjective)
        {
            DrawObjectiveMarkers(canvas, state, view);
        }
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        return
        [
            new ClientPluginOptionsSection(
                "Team Only Minimap",
                [
                    new ClientPluginBooleanOptionItem("Minimap", () => _config.Enabled, value => SetConfig(config => config.Enabled = value), trueLabel: "Enabled", falseLabel: "Disabled"),
                    new ClientPluginChoiceOptionItem("Players shown", () => (int)_config.PlayersShown, value => SetConfig(config => config.PlayersShown = (MinimapPlayerScope)value), PlayerScopeOptions),
                    new ClientPluginBooleanOptionItem("Show health", () => _config.ShowHealth, value => SetConfig(config => config.ShowHealth = value), trueLabel: "Yes", falseLabel: "No"),
                    new ClientPluginBooleanOptionItem("Show objective", () => _config.ShowObjective, value => SetConfig(config => config.ShowObjective = value), trueLabel: "Yes", falseLabel: "No"),
                    new ClientPluginBooleanOptionItem("Show sentries", () => _config.ShowSentry, value => SetConfig(config => config.ShowSentry = value), trueLabel: "Yes", falseLabel: "No"),
                    new ClientPluginChoiceOptionItem("Show method", () => (int)_config.ShowMethod, value => SetConfig(config => config.ShowMethod = (MinimapShowMethod)value), ShowMethodOptions),
                    new ClientPluginChoiceOptionItem("Fit method", () => (int)_config.FitMode, value => SetConfig(config => config.FitMode = (MinimapFitMode)value), FitModeOptions),
                    new ClientPluginKeyOptionItem("Zoom toggle key", () => _config.ZoomKey, value => SetConfig(config => config.ZoomKey = value), FormatKeyDisplayName),
                    new ClientPluginIntegerOptionItem("Zoom range", () => _config.ZoomRangeTenths, value => SetConfig(config => config.ZoomRangeTenths = value), minValue: 2, maxValue: 50, step: 2, valueLabelFormatter: value => $"{value / 10f:0.0}x"),
                    new ClientPluginIntegerOptionItem("Opacity", () => _config.AlphaPercent, value => SetConfig(config => config.AlphaPercent = value), minValue: 0, maxValue: 100, step: 5, valueLabelFormatter: value => $"{value}%"),
                    new ClientPluginBooleanOptionItem("Move to Healing HUD", () => _config.MoveNearHealingHud, value => SetConfig(config => config.MoveNearHealingHud = value), trueLabel: "Yes", falseLabel: "No"),
                ]),
            new ClientPluginOptionsSection(
                "Layout",
                [
                    new ClientPluginIntegerOptionItem("X position", () => _config.PositionX, value => SetConfig(config => config.PositionX = value), minValue: 0, maxValue: 2000, step: 10),
                    new ClientPluginIntegerOptionItem("Y position", () => _config.PositionY, value => SetConfig(config => config.PositionY = value), minValue: 0, maxValue: 2000, step: 10),
                    new ClientPluginIntegerOptionItem("Width", () => _config.Width, value => SetConfig(config => config.Width = value), minValue: 20, maxValue: 800, step: 20),
                    new ClientPluginIntegerOptionItem("Height", () => _config.Height, value => SetConfig(config => config.Height = value), minValue: 20, maxValue: 800, step: 20),
                    new ClientPluginIntegerOptionItem("Healing X position", () => _config.HealingPositionX, value => SetConfig(config => config.HealingPositionX = value), minValue: 0, maxValue: 2000, step: 10),
                    new ClientPluginIntegerOptionItem("Healing Y position", () => _config.HealingPositionY, value => SetConfig(config => config.HealingPositionY = value), minValue: 0, maxValue: 2000, step: 10),
                    new ClientPluginIntegerOptionItem("Healing width", () => _config.HealingWidth, value => SetConfig(config => config.HealingWidth = value), minValue: 20, maxValue: 800, step: 20),
                    new ClientPluginIntegerOptionItem("Healing height", () => _config.HealingHeight, value => SetConfig(config => config.HealingHeight = value), minValue: 20, maxValue: 800, step: 20),
                ]),
            new ClientPluginOptionsSection(
                "Markers",
                [
                    new ClientPluginIntegerOptionItem("Bubble size", () => _config.BubbleSizePercent, value => SetConfig(config => config.BubbleSizePercent = value), minValue: 10, maxValue: 200, step: 10, valueLabelFormatter: value => $"{value}%"),
                    new ClientPluginIntegerOptionItem("My bubble size", () => _config.SelfBubbleSizePercent, value => SetConfig(config => config.SelfBubbleSizePercent = value), minValue: 10, maxValue: 200, step: 10, valueLabelFormatter: value => $"{value}%"),
                    new ClientPluginChoiceOptionItem("My color", () => (int)_config.SelfColor, value => SetConfig(config => config.SelfColor = (MinimapMarkerColor)value), SelfColorOptions),
                    new ClientPluginIntegerOptionItem("Objective bubble size", () => _config.ObjectiveBubbleSizePercent, value => SetConfig(config => config.ObjectiveBubbleSizePercent = value), minValue: 10, maxValue: 200, step: 10, valueLabelFormatter: value => $"{value}%"),
                ]),
        ];
    }

    private void DrawBackground(IOpenGarrisonClientHudCanvas canvas, IOpenGarrisonClientReadOnlyState state, MinimapView view)
    {
        var alpha = GetAlpha();
        canvas.FillScreenRectangle(view.Bounds, Color.White * (alpha * 0.2f));
        if (canvas.TryGetLevelBackgroundTexture(out var texture))
        {
            var sourceRectangle = CalculateBackgroundSourceRectangle(texture, state, view);
            var scale = new Vector2(
                view.Bounds.Width / (float)sourceRectangle.Width,
                view.Bounds.Height / (float)sourceRectangle.Height);
            canvas.DrawScreenTexture(texture, new Vector2(view.Bounds.X, view.Bounds.Y), Color.White * (alpha * 0.75f), scale, sourceRectangle);
        }
        else
        {
            canvas.FillScreenRectangle(view.Bounds, new Color(34, 44, 60) * alpha);
        }

        canvas.DrawScreenRectangleOutline(view.Bounds, Color.Black * alpha);
    }

    private void DrawPlayerMarkers(IOpenGarrisonClientHudCanvas canvas, IOpenGarrisonClientReadOnlyState state, MinimapView view)
    {
        var localTeam = state.LocalPlayerTeam;
        foreach (var marker in state.GetPlayerMarkers())
        {
            if (marker.Team != localTeam || (_config.PlayersShown == MinimapPlayerScope.Myself && !marker.IsLocalPlayer))
            {
                continue;
            }

            if (!TryProjectToScreen(view, marker.WorldPosition, out var screenPosition))
            {
                continue;
            }

            var frameIndex = GetPlayerBubbleFrame(marker.ClassId, marker.Team);
            var baseColor = marker.IsLocalPlayer ? GetMarkerColor(_config.SelfColor) : Color.White;
            var size = (marker.IsLocalPlayer ? _config.SelfBubbleSizePercent : _config.BubbleSizePercent) / 100f;
            var damageRatio = _config.ShowHealth ? 1f - (marker.Health / (float)Math.Max(1, marker.MaxHealth)) : 0f;
            DrawMarker(canvas, frameIndex, screenPosition, size, baseColor, Color.Red, damageRatio);
        }
    }

    private void DrawSentryMarkers(IOpenGarrisonClientHudCanvas canvas, IOpenGarrisonClientReadOnlyState state, MinimapView view)
    {
        var localTeam = state.LocalPlayerTeam;
        var localPlayerId = state.LocalPlayerId;
        foreach (var marker in state.GetSentryMarkers())
        {
            if (marker.Team != localTeam)
            {
                continue;
            }

            var isLocalOwned = localPlayerId.HasValue && marker.OwnerPlayerId == localPlayerId.Value;
            if (_config.PlayersShown == MinimapPlayerScope.Myself && !isLocalOwned)
            {
                continue;
            }

            if (!TryProjectToScreen(view, marker.WorldPosition, out var screenPosition))
            {
                continue;
            }

            var baseColor = isLocalOwned ? GetMarkerColor(_config.SelfColor) : Color.White;
            var size = (isLocalOwned ? _config.SelfBubbleSizePercent : _config.BubbleSizePercent) / 100f;
            var damageRatio = _config.ShowHealth ? 1f - (marker.Health / (float)Math.Max(1, marker.MaxHealth)) : 0f;
            DrawMarker(canvas, 31, screenPosition, size, baseColor, Color.Red, damageRatio);
        }
    }

    private void DrawObjectiveMarkers(IOpenGarrisonClientHudCanvas canvas, IOpenGarrisonClientReadOnlyState state, MinimapView view)
    {
        var localTeam = state.LocalPlayerTeam;
        var size = _config.ObjectiveBubbleSizePercent / 100f;
        foreach (var marker in state.GetObjectiveMarkers())
        {
            if (!TryProjectToScreen(view, marker.WorldPosition, out var screenPosition))
            {
                continue;
            }

            switch (marker.Kind)
            {
                case ClientObjectiveMarkerKind.Attack:
                    DrawMarker(canvas, 41, screenPosition, size, Color.White, Color.White, 0f);
                    break;
                case ClientObjectiveMarkerKind.Defend:
                    DrawMarker(canvas, 42, screenPosition, size, Color.White, Color.White, 0f);
                    break;
                case ClientObjectiveMarkerKind.ControlPoint:
                    if (marker.IsLocked)
                    {
                        continue;
                    }

                    DrawMarker(
                        canvas,
                        marker.Team == localTeam ? 42 : 41,
                        screenPosition,
                        size,
                        Color.White,
                        marker.Team == localTeam ? ObjectiveDefendOverlay : ObjectiveAttackOverlay,
                        Math.Clamp(marker.Progress, 0f, 1f));
                    break;
                case ClientObjectiveMarkerKind.Generator:
                    DrawMarker(canvas, marker.Team == localTeam ? 42 : 41, screenPosition, size, Color.White, Color.White, 0f);
                    break;
            }
        }
    }

    private void DrawMarker(IOpenGarrisonClientHudCanvas canvas, int bubbleFrame, Vector2 position, float size, Color baseColor, Color overlayColor, float overlayAmount)
    {
        var alpha = GetAlpha();
        var overlay = Math.Clamp(overlayAmount, 0f, 1f);
        switch (_config.ShowMethod)
        {
            case MinimapShowMethod.Dots:
                DrawDot(canvas, position, size, BlendColor(baseColor, overlayColor, overlay), alpha, big: false);
                return;
            case MinimapShowMethod.BigDots:
                DrawDot(canvas, position, size, BlendColor(baseColor, overlayColor, overlay), alpha, big: true);
                return;
            default:
                if (!canvas.TryDrawScreenSprite("BubblesS", bubbleFrame, position, baseColor * alpha, new Vector2(size, size)))
                {
                    DrawDot(canvas, position, size, baseColor, alpha, big: true);
                    return;
                }

                if (overlay > 0.01f)
                {
                    canvas.TryDrawScreenSprite("BubblesS", bubbleFrame, position, overlayColor * (overlay * alpha), new Vector2(size, size));
                }

                return;
        }
    }

    private static void DrawDot(IOpenGarrisonClientHudCanvas canvas, Vector2 position, float size, Color color, float alpha, bool big)
    {
        var pixelSize = big ? Math.Max(4, (int)MathF.Round(8f * size)) : Math.Max(2, (int)MathF.Round(4f * size));
        var rectangle = new Rectangle(
            (int)MathF.Round(position.X - pixelSize / 2f),
            (int)MathF.Round(position.Y - pixelSize / 2f),
            pixelSize,
            pixelSize);
        canvas.FillScreenRectangle(rectangle, color * alpha);
        if (big)
        {
            canvas.DrawScreenRectangleOutline(rectangle, Color.Black * (alpha * 0.35f));
        }
    }

    private static Color BlendColor(Color baseColor, Color overlayColor, float amount)
    {
        return Color.Lerp(baseColor, overlayColor, Math.Clamp(amount, 0f, 1f));
    }

    private static int GetPlayerBubbleFrame(ClientPluginClass classId, ClientPluginTeam team)
    {
        var teamOffset = team == ClientPluginTeam.Blue ? 10 : 0;
        return classId switch
        {
            ClientPluginClass.Scout => 0 + teamOffset,
            ClientPluginClass.Pyro => 1 + teamOffset,
            ClientPluginClass.Soldier => 2 + teamOffset,
            ClientPluginClass.Heavy => 3 + teamOffset,
            ClientPluginClass.Demoman => 4 + teamOffset,
            ClientPluginClass.Medic => 5 + teamOffset,
            ClientPluginClass.Engineer => 6 + teamOffset,
            ClientPluginClass.Spy => team == ClientPluginTeam.Blue ? 17 : 7,
            ClientPluginClass.Sniper => 8 + teamOffset,
            ClientPluginClass.Quote => team == ClientPluginTeam.Blue ? 48 : 47,
            _ => team == ClientPluginTeam.Blue ? 10 : 0,
        };
    }

    private static Color GetMarkerColor(MinimapMarkerColor color)
    {
        return color switch
        {
            MinimapMarkerColor.Red => Color.Red,
            MinimapMarkerColor.Yellow => Color.Yellow,
            MinimapMarkerColor.Blue => Color.Blue,
            MinimapMarkerColor.White => Color.White,
            _ => Color.LimeGreen,
        };
    }

    private MinimapLayout ResolveLayout(IOpenGarrisonClientReadOnlyState state)
    {
        var healingLayout = _config.MoveNearHealingHud && state.IsLocalPlayerHealing;
        return healingLayout
            ? new MinimapLayout(_config.HealingPositionX, _config.HealingPositionY, _config.HealingWidth, _config.HealingHeight)
            : new MinimapLayout(_config.PositionX, _config.PositionY, _config.Width, _config.Height);
    }

    private MinimapView ResolveView(IOpenGarrisonClientReadOnlyState state, Vector2 localPlayerWorldPosition, MinimapLayout layout)
    {
        var worldWidth = Math.Max(1f, state.LevelWidth);
        var worldHeight = Math.Max(1f, state.LevelHeight);
        var layoutWidth = Math.Max(20f, layout.Width);
        var layoutHeight = Math.Max(20f, layout.Height);
        var xScale = layoutWidth / worldWidth;
        var yScale = layoutHeight / worldHeight;

        float visibleWorldWidth;
        float visibleWorldHeight;
        float scale;
        float left;
        float top;
        if (_zooming)
        {
            var zoomFactor = Math.Max(0.2f, _config.ZoomRangeTenths / 10f);
            visibleWorldWidth = Math.Max(1f, worldWidth / zoomFactor);
            visibleWorldHeight = Math.Max(1f, worldHeight / zoomFactor);
            scale = MathF.Min(layoutWidth / visibleWorldWidth, layoutHeight / visibleWorldHeight);
            left = ClampViewportOrigin(localPlayerWorldPosition.X - visibleWorldWidth / 2f, worldWidth, visibleWorldWidth);
            top = ClampViewportOrigin(localPlayerWorldPosition.Y - visibleWorldHeight / 2f, worldHeight, visibleWorldHeight);
        }
        else
        {
            switch (_config.FitMode)
            {
                case MinimapFitMode.Width:
                    scale = xScale;
                    visibleWorldWidth = worldWidth;
                    visibleWorldHeight = Math.Min(worldHeight, layoutHeight / Math.Max(scale, 0.0001f));
                    left = 0f;
                    top = ClampViewportOrigin(localPlayerWorldPosition.Y - visibleWorldHeight / 2f, worldHeight, visibleWorldHeight);
                    break;
                case MinimapFitMode.Height:
                    scale = yScale;
                    visibleWorldWidth = Math.Min(worldWidth, layoutWidth / Math.Max(scale, 0.0001f));
                    visibleWorldHeight = worldHeight;
                    left = ClampViewportOrigin(localPlayerWorldPosition.X - visibleWorldWidth / 2f, worldWidth, visibleWorldWidth);
                    top = 0f;
                    break;
                case MinimapFitMode.Reverse:
                    scale = Math.Max(xScale, yScale);
                    visibleWorldWidth = Math.Min(worldWidth, layoutWidth / Math.Max(scale, 0.0001f));
                    visibleWorldHeight = Math.Min(worldHeight, layoutHeight / Math.Max(scale, 0.0001f));
                    left = ClampViewportOrigin(localPlayerWorldPosition.X - visibleWorldWidth / 2f, worldWidth, visibleWorldWidth);
                    top = ClampViewportOrigin(localPlayerWorldPosition.Y - visibleWorldHeight / 2f, worldHeight, visibleWorldHeight);
                    break;
                default:
                    scale = MathF.Min(xScale, yScale);
                    visibleWorldWidth = worldWidth;
                    visibleWorldHeight = worldHeight;
                    left = 0f;
                    top = 0f;
                    break;
            }
        }

        return new MinimapView(
            new Rectangle(
                (int)MathF.Round(layout.X),
                (int)MathF.Round(layout.Y),
                Math.Max(1, (int)MathF.Round(visibleWorldWidth * scale)),
                Math.Max(1, (int)MathF.Round(visibleWorldHeight * scale))),
            left,
            top,
            visibleWorldWidth,
            visibleWorldHeight,
            scale);
    }

    private static float ClampViewportOrigin(float requested, float worldSize, float visibleSize)
    {
        return Math.Clamp(requested, 0f, Math.Max(0f, worldSize - visibleSize));
    }

    private static bool TryProjectToScreen(MinimapView view, Vector2 worldPosition, out Vector2 screenPosition)
    {
        if (worldPosition.X < view.Left
            || worldPosition.X > view.Left + view.VisibleWorldWidth
            || worldPosition.Y < view.Top
            || worldPosition.Y > view.Top + view.VisibleWorldHeight)
        {
            screenPosition = default;
            return false;
        }

        screenPosition = new Vector2(
            view.Bounds.X + ((worldPosition.X - view.Left) * view.Scale),
            view.Bounds.Y + ((worldPosition.Y - view.Top) * view.Scale));
        return true;
    }

    private static Rectangle CalculateBackgroundSourceRectangle(Texture2D texture, IOpenGarrisonClientReadOnlyState state, MinimapView view)
    {
        var levelWidth = Math.Max(1f, state.LevelWidth);
        var levelHeight = Math.Max(1f, state.LevelHeight);
        var x = (int)MathF.Round((view.Left / levelWidth) * texture.Width);
        var y = (int)MathF.Round((view.Top / levelHeight) * texture.Height);
        var width = (int)MathF.Round((view.VisibleWorldWidth / levelWidth) * texture.Width);
        var height = (int)MathF.Round((view.VisibleWorldHeight / levelHeight) * texture.Height);
        x = Math.Clamp(x, 0, Math.Max(0, texture.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, texture.Height - 1));
        width = Math.Clamp(width, 1, Math.Max(1, texture.Width - x));
        height = Math.Clamp(height, 1, Math.Max(1, texture.Height - y));
        return new Rectangle(x, y, width, height);
    }

    private float GetAlpha()
    {
        return _config.AlphaPercent / 100f;
    }

    private void SetConfig(Action<TeamOnlyMinimapConfig> update)
    {
        update(_config);
        SaveConfig();
    }

    private void ToggleFromMenu()
    {
        _config.Enabled = !_config.Enabled;
        SaveConfig();
        _context?.Ui.ShowNotice(_config.Enabled ? "Team minimap enabled." : "Team minimap disabled.", 160, playSound: false);
    }

    private void SaveConfig()
    {
        try
        {
            _config.Save(_configPath);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to save config: {ex.Message}");
        }
    }

    private static string FormatKeyDisplayName(Keys key)
    {
        return key switch
        {
            Keys.LeftShift => "LShift",
            Keys.RightShift => "RShift",
            Keys.LeftControl => "LCtrl",
            Keys.RightControl => "RCtrl",
            Keys.LeftAlt => "LAlt",
            Keys.RightAlt => "RAlt",
            Keys.OemTilde => "~",
            Keys.OemComma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.Space => "Space",
            Keys.PageUp => "PgUp",
            Keys.PageDown => "PgDn",
            _ => key.ToString(),
        };
    }

    private readonly record struct MinimapLayout(float X, float Y, float Width, float Height);

    private readonly record struct MinimapView(
        Rectangle Bounds,
        float Left,
        float Top,
        float VisibleWorldWidth,
        float VisibleWorldHeight,
        float Scale);
}
