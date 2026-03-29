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

public interface IOpenGarrisonClientOptionsHooks
{
    IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections();
}
