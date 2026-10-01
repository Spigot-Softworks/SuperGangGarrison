#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class MenuManager
{
    private readonly IMenuContext _context;

    public MenuManager(IMenuContext context)
    {
        _context = context;
        DisplaySettings = new DisplayRuntimeSettings();
        Menu = new MenuController(context);
        MainMenuOverlay = new MainMenuOverlayController(context);
        MainMenuOverlayState = new MainMenuOverlayStateController(context);
        MainMenuPage = new MainMenuPageController(context);
        OptionsMenu = new OptionsMenuController(context);
        PluginOptionsMenu = new PluginOptionsMenuController(context);
        ControlsMenu = new ControlsMenuController(context);
        InGameMenu = new InGameMenuController(context);
        DebugMenu = new DebugMenuController(context);
        AnimatedMenuBackground = new AnimatedMenuBackgroundController(context);
        MenuBottomBarRunners = new MenuBottomBarRunners(context);
    }

    public MenuController Menu { get; }

    public DisplayRuntimeSettings DisplaySettings { get; }

    public MainMenuOverlayController MainMenuOverlay { get; }

    public MainMenuOverlayStateController MainMenuOverlayState { get; }

    public MainMenuPageController MainMenuPage { get; }

    public OptionsMenuController OptionsMenu { get; }

    public PluginOptionsMenuController PluginOptionsMenu { get; }

    public ControlsMenuController ControlsMenu { get; }

    public InGameMenuController InGameMenu { get; }

    public DebugMenuController DebugMenu { get; }

    public AnimatedMenuBackgroundController AnimatedMenuBackground { get; }

    public MenuBottomBarRunners MenuBottomBarRunners { get; }
}
