namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientLifecycleHooks
{
    void OnClientStarting();

    void OnClientStarted();

    void OnClientStopping();

    void OnClientStopped();
}

public interface IOpenGarrisonClientUpdateHooks
{
    void OnClientFrame(ClientFrameEvent e);
}

public interface IOpenGarrisonClientHudHooks
{
    void OnGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas);
}

public interface IOpenGarrisonClientDamageHooks
{
    void OnLocalDamage(LocalDamageEvent e);
}

public sealed record ClientPluginMainMenuBackgroundOverride(
    string ImagePath,
    string AttributionText);

public interface IOpenGarrisonClientMainMenuHooks
{
    ClientPluginMainMenuBackgroundOverride? GetMainMenuBackgroundOverride();
}

public interface IOpenGarrisonClientOptionsHooks
{
    IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections();
}
