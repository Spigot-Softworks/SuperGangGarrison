#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private static (
        FrameController FrameController,
        GameplayController GameplayController,
        GameplayScreenStateController GameplayScreenStateController,
        GameplayPresentationStateController GameplayPresentationStateController,
        GameplayImpactEffectsController GameplayImpactEffectsController,
        GameplayGoreEffectsController GameplayGoreEffectsController,
        GameplaySmokeEffectsController GameplaySmokeEffectsController,
        GameplayMaterialEffectsController GameplayMaterialEffectsController,
        GameplayVisualEventController GameplayVisualEventController,
        GameplayAudioMusicController GameplayAudioMusicController,
        GameplayAudioEventController GameplayAudioEventController,
        GameplayRapidFireAudioController GameplayRapidFireAudioController,
        GameplayLocalStatusHudController GameplayLocalStatusHudController,
        GameplayMedicHudController GameplayMedicHudController,
        GameplayEngineerHudController GameplayEngineerHudController,
        GameplayAimHudController GameplayAimHudController,
        GameplayPlayerNameHudController GameplayPlayerNameHudController,
        GameplayPlayerRenderController GameplayPlayerRenderController,
        GameplayDeadBodyRenderController GameplayDeadBodyRenderController,
        GameplayPlayerSpriteRenderController GameplayPlayerSpriteRenderController,
        GameplayWeaponRenderController GameplayWeaponRenderController,
        GameplayPlayerStatusEffectRenderController GameplayPlayerStatusEffectRenderController,
        GameplaySessionController GameplaySessionController,
        GameplayOverlayStateController GameplayOverlayStateController,
        GameplayResetController GameplayResetController)
        CreateGameplayControllerBundle(Game1 game)
    {
        return (
            new FrameController(game),
            new GameplayController(game),
            new GameplayScreenStateController(game),
            new GameplayPresentationStateController(game),
            new GameplayImpactEffectsController(game),
            new GameplayGoreEffectsController(game),
            new GameplaySmokeEffectsController(game),
            new GameplayMaterialEffectsController(game),
            new GameplayVisualEventController(game),
            new GameplayAudioMusicController(game),
            new GameplayAudioEventController(game),
            new GameplayRapidFireAudioController(game),
            new GameplayLocalStatusHudController(game),
            new GameplayMedicHudController(game),
            new GameplayEngineerHudController(game),
            new GameplayAimHudController(game),
            new GameplayPlayerNameHudController(game),
            new GameplayPlayerRenderController(game),
            new GameplayDeadBodyRenderController(game),
            new GameplayPlayerSpriteRenderController(game),
            new GameplayWeaponRenderController(game),
            new GameplayPlayerStatusEffectRenderController(game),
            new GameplaySessionController(game),
            new GameplayOverlayStateController(game),
            new GameplayResetController(game));
    }

    private static (
        ClientPluginRuntimeController ClientPluginRuntimeController,
        ClientPluginEventController ClientPluginEventController,
        ClientPluginUiBridgeController ClientPluginUiBridgeController,
        ClientPluginMarkerController ClientPluginMarkerController,
        MenuController MenuController,
        ConnectionFlowController ConnectionFlowController,
        MainMenuOverlayController MainMenuOverlayController,
        MainMenuOverlayStateController MainMenuOverlayStateController,
        HostSetupFlowController HostSetupFlowController,
        WindowTextInputController WindowTextInputController,
        MenuTextInputController MenuTextInputController,
        NetworkPromptTextInputController NetworkPromptTextInputController,
        ChatTextInputController ChatTextInputController,
        ConsoleTextInputController ConsoleTextInputController,
        BootstrapController BootstrapController,
        OptionsMenuController OptionsMenuController,
        MainMenuPageController MainMenuPageController,
        PluginOptionsMenuController PluginOptionsMenuController,
        ControlsMenuController ControlsMenuController,
        InGameMenuController InGameMenuController,
        GameplayOverlayController GameplayOverlayController)
        CreateShellControllerBundle(Game1 game)
    {
        return (
            new ClientPluginRuntimeController(game),
            new ClientPluginEventController(game),
            new ClientPluginUiBridgeController(game),
            new ClientPluginMarkerController(game),
            new MenuController(game),
            new ConnectionFlowController(game),
            new MainMenuOverlayController(game),
            new MainMenuOverlayStateController(game),
            new HostSetupFlowController(game),
            new WindowTextInputController(game),
            new MenuTextInputController(game),
            new NetworkPromptTextInputController(game),
            new ChatTextInputController(game),
            new ConsoleTextInputController(game),
            new BootstrapController(game),
            new OptionsMenuController(game),
            new MainMenuPageController(game),
            new PluginOptionsMenuController(game),
            new ControlsMenuController(game),
            new InGameMenuController(game),
            new GameplayOverlayController(game));
    }

    private static (
        ClientSettings ClientSettings,
        InputBindingsSettings InputBindings,
        HostedServerRuntimeController HostedServerRuntime,
        Microsoft.Xna.Framework.GraphicsDeviceManager GraphicsDeviceManager)
        CreateRuntimeServices(Game1 game, HostedServerConsoleState hostedServerConsole)
    {
        return (
            ClientSettings.Load(),
            InputBindingsSettings.Load(),
            new HostedServerRuntimeController(hostedServerConsole),
            new Microsoft.Xna.Framework.GraphicsDeviceManager(game));
    }
}
