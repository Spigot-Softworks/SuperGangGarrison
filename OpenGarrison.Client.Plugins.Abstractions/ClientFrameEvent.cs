namespace OpenGarrison.Client.Plugins;

public readonly record struct ClientFrameEvent(
    float DeltaSeconds,
    int ClientTicks,
    bool IsMainMenuOpen,
    bool IsGameplayActive,
    bool IsConnected,
    bool IsSpectator);
