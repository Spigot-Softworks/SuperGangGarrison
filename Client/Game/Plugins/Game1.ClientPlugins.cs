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
    private readonly List<SnapshotDamageEvent> _pendingNetworkDamageEvents = new();
    private readonly HashSet<ulong> _processedNetworkDamageEventIds = new();
    private readonly Queue<ulong> _processedNetworkDamageEventOrder = new();
    private ClientRoundPhase _clientPluginPreviousMatchPhase;
    private bool _clientPluginPreviousLocalAlive;
    private int _clientPluginPreviousLocalHealth;
    private int _clientPluginPreviousLocalAmmo;
    private int _clientPluginPreviousLocalPrimaryCooldownTicks;
    private bool _clientPluginPreviousLocalCarryingIntel;
    private bool _clientPluginPreviousLocalBurning;
    private int _clientPluginPreviousKillFeedCount;
    private readonly Dictionary<int, (ClientPluginTeam Team, ClientPluginTeam CappingTeam, float Progress, bool IsLocked)> _clientPluginPreviousObjectiveStates = new();
    private (bool IsAtBase, bool IsDropped, ClientPluginTeam CarrierTeam, float ReturnProgress, float X, float Y) _clientPluginPreviousRedIntelState;
    private (bool IsAtBase, bool IsDropped, ClientPluginTeam CarrierTeam, float ReturnProgress, float X, float Y) _clientPluginPreviousBlueIntelState;
    private readonly Dictionary<PlayerTeam, (int Health, int MaxHealth, bool IsDestroyed)> _clientPluginPreviousGeneratorStates = new();
    private ClientPluginHost? _clientPluginHost;
    private ClientPluginStateView? _clientPluginStateView;

    private void InitializeClientPlugins()
    {
        _clientPluginRuntimeController.InitializeClientPlugins();
    }

    private void NotifyClientPluginsStarted()
    {
        _clientPluginRuntimeController.NotifyClientPluginsStarted();
    }

    private void ShutdownClientPlugins()
    {
        _clientPluginRuntimeController.ShutdownClientPlugins();
    }

    private void NotifyClientPluginsFrame(GameTime gameTime, int clientTicks)
    {
        _clientPluginRuntimeController.NotifyClientPluginsFrame(gameTime, clientTicks);
    }

    private void QueueResolvedSnapshotDamageEvents(SnapshotMessage resolvedSnapshot)
    {
        _clientPluginEventController.QueueResolvedSnapshotDamageEvents(resolvedSnapshot);
    }

    private void DrawClientPluginHud(Vector2 cameraTopLeft)
    {
        _clientPluginUiBridgeController.DrawClientPluginHud(cameraTopLeft);
    }

    private ClientBubbleMenuUpdateResult? TryHandleClientPluginBubbleMenuInput(ClientBubbleMenuInputState inputState)
    {
        return _clientPluginUiBridgeController.TryHandleClientPluginBubbleMenuInput(inputState);
    }

    private bool TryDrawClientPluginBubbleMenu(Vector2 cameraTopLeft, ClientBubbleMenuRenderState renderState)
    {
        return _clientPluginUiBridgeController.TryDrawClientPluginBubbleMenu(cameraTopLeft, renderState);
    }

    private bool HasClientPluginBubbleMenuOverride()
    {
        return _clientPluginUiBridgeController.HasClientPluginBubbleMenuOverride();
    }

    private bool SetClientPluginEnabled(string pluginId, bool enabled)
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
        return _clientPluginUiBridgeController.TryDrawClientPluginDeadBody(cameraTopLeft, deadBody);
    }

    private ClientPluginMainMenuBackgroundOverride? GetClientPluginMainMenuBackgroundOverride()
    {
        return _clientPluginUiBridgeController.GetClientPluginMainMenuBackgroundOverride();
    }

    private void NotifyClientPluginsWorldSound(WorldSoundEvent soundEvent)
    {
        _clientPluginUiBridgeController.NotifyClientPluginsWorldSound(soundEvent);
    }

    private void NotifyClientPluginsServerMessage(ServerPluginMessage message)
    {
        _clientPluginUiBridgeController.NotifyClientPluginsServerMessage(message);
    }

    private Vector2 GetClientPluginCameraOffset()
    {
        return _clientPluginUiBridgeController.GetClientPluginCameraOffset();
    }

    private int? GetClientPluginLocalPlayerId()
    {
        return _clientPluginUiBridgeController.GetClientPluginLocalPlayerId();
    }

    private Vector2 GetCurrentClientPluginCameraTopLeft()
    {
        return _clientPluginUiBridgeController.GetCurrentClientPluginCameraTopLeft();
    }

    private Texture2D? GetClientPluginLevelBackgroundTexture()
    {
        return _clientPluginUiBridgeController.GetClientPluginLevelBackgroundTexture();
    }

    private bool WasClientPluginKeyPressedThisFrame(Keys key)
    {
        return _clientPluginKeyboard.IsKeyDown(key) && !_clientPluginPreviousKeyboard.IsKeyDown(key);
    }

    private ClientPluginHost CreateClientPluginHost(string pluginsDirectory, string pluginConfigRoot, string pluginStatePath)
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
        return _clientPluginMarkerController.GetClientPluginPlayerMarkers();
    }

    private List<ClientSentryMarker> GetClientPluginSentryMarkers()
    {
        return _clientPluginMarkerController.GetClientPluginSentryMarkers();
    }

    private List<ClientObjectiveMarker> GetClientPluginObjectiveMarkers()
    {
        return _clientPluginMarkerController.GetClientPluginObjectiveMarkers();
    }

    private void DispatchClientSemanticGameplayEvents()
    {
        _clientPluginEventController.DispatchClientSemanticGameplayEvents();
    }

    private void DispatchPendingDamageEventsToPlugins()
    {
        _clientPluginEventController.DispatchPendingDamageEventsToPlugins();
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

    private IReadOnlyList<ClientScoreboardPlayerAction> GetClientPluginScoreboardPlayerActions(ScoreboardPlayerRow row)
    {
        return _clientPluginHost?.GetScoreboardPlayerActions(CreateClientPluginScoreboardPlayerActionContext(row))
            ?? Array.Empty<ClientScoreboardPlayerAction>();
    }

    private ClientScoreboardPlayerActionContext CreateClientPluginScoreboardPlayerActionContext(ScoreboardPlayerRow row)
    {
        return new ClientScoreboardPlayerActionContext(
            row.Slot,
            row.Player.Id,
            row.Player.DisplayName,
            ToClientPluginTeam(row.Player.Team),
            ToClientPluginClass(row.Player.ClassId),
            row.IsLocal);
    }

    private void ResetClientPluginGameplayEventState()
    {
        _clientPluginEventController.ResetClientPluginGameplayEventState();
    }

    private static ClientPluginTeam ToClientPluginTeam(PlayerTeam? team)
    {
        return team switch
        {
            PlayerTeam.Red => ClientPluginTeam.Red,
            PlayerTeam.Blue => ClientPluginTeam.Blue,
            _ => ClientPluginTeam.None,
        };
    }

    private static ClientPluginClass ToClientPluginClass(PlayerClass classId)
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

    private static ClientRoundPhase ToClientRoundPhase(MatchPhase matchPhase)
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
