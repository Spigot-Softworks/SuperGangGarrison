#nullable enable

namespace OpenGarrison.Client;

internal enum GameplayHudVisibilityMode
{
    Visible,
    Hidden,
    Minimal,
}

internal static class GameplayHudVisibilityRules
{
    public static bool TryApplyConsoleCommand(
        string command,
        string option,
        GameplayHudVisibilityMode currentMode,
        out GameplayHudVisibilityMode nextMode)
    {
        nextMode = currentMode;
        var isHudCommand = string.Equals(command, "hud", System.StringComparison.OrdinalIgnoreCase);
        switch (option.ToLowerInvariant())
        {
            case "off" when isHudCommand:
            case "hide" when isHudCommand:
            case "on" when !isHudCommand:
            case "hide" when !isHudCommand:
                nextMode = GameplayHudVisibilityMode.Hidden;
                return true;
            case "on" when isHudCommand:
            case "show" when isHudCommand:
            case "off" when !isHudCommand:
            case "show" when !isHudCommand:
                nextMode = GameplayHudVisibilityMode.Visible;
                return true;
            case "minimal" when isHudCommand:
                nextMode = GameplayHudVisibilityMode.Minimal;
                return true;
            case "toggle":
                nextMode = currentMode == GameplayHudVisibilityMode.Hidden
                    ? GameplayHudVisibilityMode.Visible
                    : GameplayHudVisibilityMode.Hidden;
                return true;
            default:
                return false;
        }
    }

    public static string GetStatusText(GameplayHudVisibilityMode mode)
    {
        return mode switch
        {
            GameplayHudVisibilityMode.Hidden => "hud is hidden",
            GameplayHudVisibilityMode.Minimal => "hud is minimal",
            _ => "hud is visible",
        };
    }

    public static string GetChangedText(GameplayHudVisibilityMode mode)
    {
        return mode switch
        {
            GameplayHudVisibilityMode.Hidden => "hud hidden",
            GameplayHudVisibilityMode.Minimal => "hud minimal",
            _ => "hud visible",
        };
    }

    public static bool ShouldDrawBattleCursor(GameplayHudVisibilityMode mode, bool otherwiseEligible)
        => mode != GameplayHudVisibilityMode.Hidden && otherwiseEligible;

    public static bool ShouldDrawFloatingHealthBar(
        bool showHealthBarEnabled,
        GameplayHudVisibilityMode mode,
        bool forceHealthBar,
        float visibilityAlpha,
        bool isLocalPlayer,
        bool sharesLocalPlayerTeam)
    {
        if (mode == GameplayHudVisibilityMode.Hidden)
        {
            return false;
        }

        var healthBarsEnabled = showHealthBarEnabled
            || mode == GameplayHudVisibilityMode.Minimal
            || forceHealthBar;
        var isVisibleTeam = isLocalPlayer || sharesLocalPlayerTeam || forceHealthBar;
        return healthBarsEnabled && visibilityAlpha > 0f && isVisibleTeam;
    }
}
