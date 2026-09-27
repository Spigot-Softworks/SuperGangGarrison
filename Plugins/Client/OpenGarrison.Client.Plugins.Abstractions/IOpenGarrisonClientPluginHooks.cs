using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// Hooks a client plugin can implement to observe client lifecycle transitions.
/// </summary>
public interface IOpenGarrisonClientLifecycleHooks
{
    /// <summary>
    /// Called when the client is starting, before it is ready.
    /// </summary>
    void OnClientStarting();

    /// <summary>
    /// Called after the client has started.
    /// </summary>
    void OnClientStarted();

    /// <summary>
    /// Called when the client is stopping.
    /// </summary>
    void OnClientStopping();

    /// <summary>
    /// Called after the client has stopped.
    /// </summary>
    void OnClientStopped();
}

/// <summary>
/// Hooks a client plugin can implement to receive per-frame updates.
/// </summary>
public interface IOpenGarrisonClientUpdateHooks
{
    /// <summary>
    /// Called once per client frame with timing and client state.
    /// </summary>
    /// <param name="e">The frame event.</param>
    void OnClientFrame(ClientFrameEvent e);
}

/// <summary>
/// Hooks a client plugin can implement to draw on the gameplay HUD.
/// </summary>
public interface IOpenGarrisonClientHudHooks
{
    /// <summary>
    /// Called to draw plugin content on the gameplay HUD.
    /// </summary>
    /// <param name="canvas">The HUD drawing canvas.</param>
    void OnGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas);
}

/// <summary>
/// Hooks a client plugin can implement to draw on the legacy scoreboard.
/// </summary>
public interface IOpenGarrisonClientScoreboardLegacyHooks
{
    /// <summary>
    /// Called to draw plugin content on the legacy scoreboard.
    /// </summary>
    /// <param name="canvas">The scoreboard drawing canvas.</param>
    /// <param name="state">The current scoreboard render state.</param>
    void OnScoreboardDraw(IOpenGarrisonClientScoreboardCanvas canvas, ClientScoreboardRenderState state);
}

/// <summary>
/// Hooks a client plugin can implement to observe local damage events.
/// </summary>
public interface IOpenGarrisonClientDamageHooks
{
    /// <summary>
    /// Called when a damage event involving the local player is observed.
    /// </summary>
    /// <param name="e">The damage event.</param>
    void OnLocalDamage(LocalDamageEvent e);
}

/// <summary>
/// A world-space sound event raised for positional audio hooks.
/// </summary>
/// <param name="SoundName">The sound asset name.</param>
/// <param name="WorldPosition">The world position the sound originates from.</param>
public sealed record ClientWorldSoundEvent(
    string SoundName,
    Vector2 WorldPosition);

/// <summary>
/// Hooks a client plugin can implement to observe world sounds.
/// </summary>
public interface IOpenGarrisonClientSoundHooks
{
    /// <summary>
    /// Called when a world-space sound is played.
    /// </summary>
    /// <param name="e">The world sound event.</param>
    void OnWorldSound(ClientWorldSoundEvent e);
}

/// <summary>
/// Hooks a client plugin can implement to influence the camera.
/// </summary>
public interface IOpenGarrisonClientCameraHooks
{
    /// <summary>
    /// Gets the plugin's camera offset in screen pixels.
    /// </summary>
    /// <returns>The camera offset to apply.</returns>
    Vector2 GetCameraOffset();
}

/// <summary>
/// Overrides the main menu background with a plugin-provided image.
/// </summary>
/// <param name="ImagePath">The path of the background image.</param>
/// <param name="AttributionText">Attribution text shown for the image.</param>
public sealed record ClientPluginMainMenuBackgroundOverride(
    string ImagePath,
    string AttributionText);

/// <summary>
/// Hooks a client plugin can implement to customize the main menu.
/// </summary>
public interface IOpenGarrisonClientMainMenuHooks
{
    /// <summary>
    /// Gets a background override for the main menu, or null to keep the default.
    /// </summary>
    /// <returns>The background override, or null.</returns>
    ClientPluginMainMenuBackgroundOverride? GetMainMenuBackgroundOverride();
}

/// <summary>
/// Hooks a client plugin can implement to contribute options menu sections.
/// </summary>
public interface IOpenGarrisonClientOptionsHooks
{
    /// <summary>
    /// Gets the options sections contributed by the plugin.
    /// </summary>
    /// <returns>The plugin's options sections.</returns>
    IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections();
}
