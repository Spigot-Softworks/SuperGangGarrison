#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    public readonly List<SnapshotDamageEvent> _pendingNetworkDamageEvents = new();
    public readonly HashSet<ulong> _processedNetworkDamageEventIds = new();
    public readonly Queue<ulong> _processedNetworkDamageEventOrder = new();
    public ClientRoundPhase _clientPluginPreviousMatchPhase;
    public bool _clientPluginPreviousLocalAlive;
    public int _clientPluginPreviousLocalAmmo;
    public int _clientPluginPreviousLocalPrimaryCooldownTicks;
    public bool _clientPluginPreviousLocalCarryingIntel;
    public bool _clientPluginPreviousLocalBurning;
    public int _clientPluginPreviousKillFeedCount;
    public readonly Dictionary<int, (ClientPluginTeam Team, ClientPluginTeam CappingTeam, float Progress, bool IsLocked)> _clientPluginPreviousObjectiveStates = new();
    public (bool IsAtBase, bool IsDropped, ClientPluginTeam CarrierTeam, float ReturnProgress, float X, float Y) _clientPluginPreviousRedIntelState;
    public (bool IsAtBase, bool IsDropped, ClientPluginTeam CarrierTeam, float ReturnProgress, float X, float Y) _clientPluginPreviousBlueIntelState;
    public readonly Dictionary<PlayerTeam, (int Health, int MaxHealth, bool IsDestroyed)> _clientPluginPreviousGeneratorStates = new();
    public ClientPluginHost? _clientPluginHost;
    public ClientPluginStateView? _clientPluginStateView;

    public void InitializeClientPlugins()
    {
        _pluginManager.Runtime.InitializeClientPlugins();
    }

    public void NotifyClientPluginsStarted()
    {
        _pluginManager.Runtime.NotifyClientPluginsStarted();
    }

    public void ShutdownClientPlugins()
    {
        _pluginManager.Runtime.ShutdownClientPlugins();
    }

    private void NotifyClientPluginsFrame(GameTime gameTime, int clientTicks)
    {
        _pluginManager.Runtime.NotifyClientPluginsFrame(gameTime, clientTicks);
    }

    private void QueueResolvedSnapshotDamageEvents(SnapshotMessage resolvedSnapshot)
    {
        _pluginManager.Events.QueueResolvedSnapshotDamageEvents(resolvedSnapshot);
    }

    private void DrawClientPluginHud(Vector2 cameraTopLeft)
    {
        _pluginManager.UiBridge.DrawClientPluginHud(cameraTopLeft);
    }

    private ClientBubbleMenuUpdateResult? TryHandleClientPluginBubbleMenuInput(ClientBubbleMenuInputState inputState)
    {
        return _pluginManager.UiBridge.TryHandleClientPluginBubbleMenuInput(inputState);
    }

    private bool TryDrawClientPluginBubbleMenu(Vector2 cameraTopLeft, ClientBubbleMenuRenderState renderState)
    {
        return _pluginManager.UiBridge.TryDrawClientPluginBubbleMenu(cameraTopLeft, renderState);
    }

    private bool HasClientPluginBubbleMenuOverride()
    {
        return _pluginManager.UiBridge.HasClientPluginBubbleMenuOverride();
    }

    public bool SetClientPluginEnabled(string pluginId, bool enabled)
    {
        var hadBubbleMenuOverride = HasClientPluginBubbleMenuOverride();
        var applied = _clientPluginHost?.SetPluginEnabled(pluginId, enabled) ?? false;
        if (applied && !enabled && hadBubbleMenuOverride && !HasClientPluginBubbleMenuOverride())
        {
            ResetBubbleMenuInteractionState();
        }

        return applied;
    }

    private bool TryDrawClientPluginDeadBody(Vector2 cameraTopLeft, ClientDeadBodyRenderState deadBody)
    {
        return _pluginManager.UiBridge.TryDrawClientPluginDeadBody(cameraTopLeft, deadBody);
    }

    public ClientPluginMainMenuBackgroundOverride? GetClientPluginMainMenuBackgroundOverride()
    {
        return _pluginManager.UiBridge.GetClientPluginMainMenuBackgroundOverride();
    }

    public void NotifyClientPluginsWorldSound(WorldSoundEvent soundEvent)
    {
        _pluginManager.UiBridge.NotifyClientPluginsWorldSound(soundEvent);
    }

    private void NotifyClientPluginsServerMessage(ServerPluginMessage message)
    {
        _pluginManager.UiBridge.NotifyClientPluginsServerMessage(message);
    }

    private Vector2 GetClientPluginCameraOffset()
    {
        if (_networkClient.IsLegacyGg2Connection)
        {
            return Vector2.Zero;
        }

        return _pluginManager.UiBridge.GetClientPluginCameraOffset();
    }

    public int? GetClientPluginLocalPlayerId()
    {
        return _pluginManager.UiBridge.GetClientPluginLocalPlayerId();
    }

    private Vector2 GetCurrentClientPluginCameraTopLeft()
    {
        return _pluginManager.UiBridge.GetCurrentClientPluginCameraTopLeft();
    }

    private Texture2D? GetClientPluginLevelBackgroundTexture()
    {
        return _pluginManager.UiBridge.GetClientPluginLevelBackgroundTexture();
    }

    private bool WasClientPluginKeyPressedThisFrame(Keys key)
    {
        return _clientPluginKeyboard.IsKeyDown(key) && !_clientPluginPreviousKeyboard.IsKeyDown(key);
    }

    public ClientPluginHost CreateClientPluginHost(string pluginsDirectory, string pluginConfigRoot, string pluginStatePath)
    {
        return new ClientPluginHost(
            _clientPluginStateView!,
            GraphicsDevice,
            pluginsDirectory,
            pluginConfigRoot,
            pluginStatePath,
            AddConsoleLine,
            WasClientPluginKeyPressedThisFrame,
            (sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion) =>
                _networkClient.SendPluginMessage(sourcePluginId, targetPluginId, messageType, payload, payloadFormat, schemaVersion),
            (_, text, durationTicks, playSound) => QueuePluginNotice(text, durationTicks, playSound),
            (pluginId, title, subtitle, breadcrumb, entries) => ShowClientPluginOverlayMenu(pluginId, title, subtitle, breadcrumb, entries),
            HideClientPluginOverlayMenu);
    }

    private List<ClientPlayerMarker> GetClientPluginPlayerMarkers()
    {
        return _pluginManager.Marker.GetClientPluginPlayerMarkers();
    }

    private List<ClientSentryMarker> GetClientPluginSentryMarkers()
    {
        return _pluginManager.Marker.GetClientPluginSentryMarkers();
    }

    private List<ClientObjectiveMarker> GetClientPluginObjectiveMarkers()
    {
        return _pluginManager.Marker.GetClientPluginObjectiveMarkers();
    }

    public void DispatchClientSemanticGameplayEvents()
    {
        _pluginManager.Events.DispatchClientSemanticGameplayEvents();
    }

    private void DispatchPendingDamageEventsToPlugins()
    {
        _pluginManager.Events.DispatchPendingDamageEventsToPlugins();
    }

    private void NotifyClientPluginsScoreboardDraw(
        Rectangle scoreboardBounds,
        float alpha,
        string serverMetaLabel,
        string mapMetaLabel,
        int redPlayerCount,
        int bluePlayerCount,
        string redCenterText,
        string blueCenterText)
    {
        _clientPluginHost?.NotifyScoreboardDraw(
            new ScoreboardCanvas(this),
            new ClientScoreboardRenderState(
                scoreboardBounds,
                alpha,
                serverMetaLabel,
                mapMetaLabel,
                redPlayerCount,
                bluePlayerCount,
                redCenterText,
                blueCenterText));
    }


    public void ResetClientPluginGameplayEventState()
    {
        _pluginManager.Events.ResetClientPluginGameplayEventState();
    }

    public static ClientPluginTeam ToClientPluginTeam(PlayerTeam? team)
    {
        return team switch
        {
            PlayerTeam.Red => ClientPluginTeam.Red,
            PlayerTeam.Blue => ClientPluginTeam.Blue,
            _ => ClientPluginTeam.None,
        };
    }

    public static ClientPluginClass ToClientPluginClass(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => ClientPluginClass.Scout,
            PlayerClass.Engineer => ClientPluginClass.Engineer,
            PlayerClass.Pyro => ClientPluginClass.Pyro,
            PlayerClass.Soldier => ClientPluginClass.Soldier,
            PlayerClass.Demoman => ClientPluginClass.Demoman,
            PlayerClass.Heavy => ClientPluginClass.Heavy,
            PlayerClass.Sniper => ClientPluginClass.Sniper,
            PlayerClass.Medic => ClientPluginClass.Medic,
            PlayerClass.Spy => ClientPluginClass.Spy,
            PlayerClass.Quote => ClientPluginClass.Quote,
            _ => ClientPluginClass.Unknown,
        };
    }

    public static ClientRoundPhase ToClientRoundPhase(MatchPhase matchPhase)
    {
        return matchPhase switch
        {
            MatchPhase.Running => ClientRoundPhase.Running,
            MatchPhase.Ended => ClientRoundPhase.Ended,
            _ => ClientRoundPhase.Unknown,
        };
    }

    private static ClientDeadBodyAnimationKind ToClientDeadBodyAnimationKind(DeadBodyAnimationKind animationKind)
    {
        return animationKind switch
        {
            DeadBodyAnimationKind.Rifle => ClientDeadBodyAnimationKind.Rifle,
            DeadBodyAnimationKind.Severe => ClientDeadBodyAnimationKind.Severe,
            _ => ClientDeadBodyAnimationKind.Default,
        };
    }
}
