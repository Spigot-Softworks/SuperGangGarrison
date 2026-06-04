namespace OpenGarrison.Client;

/// <summary>
/// Release gating for the Garrison map builder. Flip <see cref="IsMainMenuEntryEnabled"/> when the builder is ready for players.
/// </summary>
public static class GarrisonBuilderFeature
{
    /// <summary>
    /// When false, the main-menu footer button is hidden and menu entry is blocked.
    /// Developer console commands can still open the builder when <see cref="AllowConsoleAccess"/> is true.
    /// </summary>
    public const bool IsMainMenuEntryEnabled = true;

    /// <summary>
    /// Allows <c>builder on</c> and related console commands while the menu entry remains hidden.
    /// </summary>
    public const bool AllowConsoleAccess = true;

    public static bool CanOpenFromMainMenu => IsMainMenuEntryEnabled;

    public static bool CanOpenFromConsole => AllowConsoleAccess;
}
