namespace OpenGarrison.Client.Plugins;

/// <summary>
/// Per-frame timing and client state delivered to client plugin update hooks.
/// </summary>
/// <param name="DeltaSeconds">The elapsed time since the previous frame, in seconds.</param>
/// <param name="ClientTicks">The number of client ticks elapsed.</param>
/// <param name="IsMainMenuOpen">Whether the main menu is open.</param>
/// <param name="IsGameplayActive">Whether gameplay is active.</param>
/// <param name="IsConnected">Whether the client is connected to a server.</param>
/// <param name="IsSpectator">Whether the local player is spectating.</param>
public readonly record struct ClientFrameEvent(
    float DeltaSeconds,
    int ClientTicks,
    bool IsMainMenuOpen,
    bool IsGameplayActive,
    bool IsConnected,
    bool IsSpectator);
