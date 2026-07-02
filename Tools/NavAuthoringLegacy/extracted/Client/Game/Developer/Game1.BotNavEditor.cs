#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.BotAI;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum NavEditorTool
    {
        Select = 0,
        AddAnchor = 1,
        AddLink = 2,
    }

    private enum NavEditorLayerMode
    {
        HintLayer = 0,
        GeneratedGraph = 1,
        Both = 2,
    }

    private enum NavEditorTraversalCaptureMode
    {
        None = 0,
        Armed = 1,
        Recording = 2,
    }

    private enum NavEditorContextMenuTargetKind
    {
        Empty = 0,
        Anchor = 1,
        Link = 2,
        GeneratedGraphNode = 3,
        GeneratedGraphEdge = 4,
    }

    private enum NavEditorGraphEdgeDisplayMode
    {
        Soft = 0,
        Cyan = 1,
        Red = 2,
    }

    private const int NavEditorPanelWidth = 440;
    private const int NavEditorPanelPadding = 12;
    private const int NavEditorPanelExpandedHeight = 656;
    private const int NavEditorPanelCollapsedHeight = 92;
    private const int NavEditorPanelHeaderHeight = 78;
    private const int NavEditorPanelHeaderButtonSize = 24;
    private const int NavEditorPanelMargin = 18;
    private const int NavEditorButtonHeight = 28;
    private const int NavEditorButtonGap = 8;
    private const float NavEditorAnchorHalfSize = 6f;
    private const float NavEditorAnchorPickRadius = 12f;
    private const float NavEditorLinkPickDistance = 10f;
    private const float NavEditorLinkThickness = 2f;
    private const float NavEditorAnchorSnapMaximumDistance = 96f;
    private const float NavEditorAnchorFreePlacementMaximumDistance = 48f;
    private const float NavEditorAnchorPlacementSearchStep = 8f;
    private const int NavEditorContextMenuWidth = 192;
    private const int NavEditorContextMenuItemHeight = 24;
    private const int NavEditorContextMenuPanelGap = 4;
    private const float NavEditorStatusDurationSeconds = 4f;
    private const float NavEditorCostStep = 0.1f;
    private const float NavEditorRecordingTargetToleranceX = 36f;
    private const float NavEditorRecordingTargetToleranceY = 18f;
    private const int NavEditorRecordingMaximumTicks = 240;
    private const float NavEditorPlaybackTargetToleranceX = 36f;
    private const float NavEditorPlaybackTargetToleranceY = 18f;
    private const float NavEditorApproximateGroundedArrivalToleranceY = 64f;
    private const int NavEditorPlaybackMaximumTicks = 720;
    private const int NavEditorPlaybackPostTapeMaximumTicks = 90;
    private const int NavEditorClearAllPromptHeight = 84;
    private const float NavEditorMinimumActivationZoneSize = 16f;
    private bool _navEditorEnabled;
    private bool _navEditorDirty;
    private NavEditorTool _navEditorTool = NavEditorTool.Select;
    private readonly List<NavEditorAnchor> _navEditorAnchors = new();
    private readonly List<NavEditorLink> _navEditorLinks = new();
    private int _navEditorSelectedAnchorIndex = -1;
    private int _navEditorSelectedLinkIndex = -1;
    private int _navEditorPendingLinkAnchorIndex = -1;
    private int _navEditorNextAnchorNumber = 1;
    private string _navEditorStatusMessage = "nav editor disabled";
    private float _navEditorStatusSecondsRemaining;
    private string _navEditorLastConsoleStatusMessage = string.Empty;
    private NavEditorLayerMode _navEditorLayerMode = NavEditorLayerMode.HintLayer;
    private bool _navEditorShowBotTags = true;
    private BotNavigationAsset? _navEditorGeneratedCompatGraph;
    private BotNavigationAsset? _navEditorGeneratedCompatGraphDiagnosticsAsset;
    private ClientBotNavPoints? _navEditorGeneratedCompatGraphDiagnosticsNavPoints;
    private string _navEditorGeneratedCompatGraphStatus = "generated graph not loaded";
    private int _navEditorSelectedGeneratedGraphNodeId = -1;
    private int _navEditorSelectedGeneratedGraphEdgeFromNodeId = -1;
    private int _navEditorSelectedGeneratedGraphEdgeToNodeId = -1;
    private int _navEditorPendingGeneratedGraphSourceNodeId = -1;
    private int _navEditorGraphRouteStartNodeId = -1;
    private int _navEditorGraphRouteGoalNodeId = -1;
    private int[] _navEditorGraphPreviewRouteNodeIds = Array.Empty<int>();
    private PlayerClass[] _navEditorDefaultClasses = Array.Empty<PlayerClass>();
    private BotNavigationNodeKind _navEditorDefaultAnchorKind = BotNavigationNodeKind.RouteAnchor;
    private PlayerTeam? _navEditorDefaultAnchorTeam;
    private BotNavigationHintTraversalKind _navEditorDefaultTraversal = BotNavigationHintTraversalKind.Auto;
    private bool _navEditorDefaultBidirectional = true;
    private NavEditorGraphEdgeDisplayMode _navEditorGraphEdgeDisplayMode;
    private float _navEditorDefaultCostMultiplier = 1f;
    private PlayerClass? _navEditorViewClass;
    private bool _navEditorRenamingAnchor;
    private string _navEditorRenameBuffer = string.Empty;
    private Task<NavEditorRebuildResult>? _navEditorRebuildTask;
    private string _navEditorRebuildStatus = string.Empty;
    private string _navEditorLoadedLevelName = string.Empty;
    private int _navEditorLoadedMapAreaIndex = -1;
    private NavEditorTraversalCaptureMode _navEditorTraversalCaptureMode;
    private PlayerClass _navEditorRecordTestClass = PlayerClass.Soldier;
    private int _navEditorRecordingLinkIndex = -1;
    private Vector2 _navEditorRecordingSourcePosition;
    private Vector2 _navEditorRecordingTargetPosition;
    private string _navEditorRecordingSourceLabel = string.Empty;
    private string _navEditorRecordingTargetLabel = string.Empty;
    private bool _navEditorRecordingRequiresGroundedArrival = true;
    private readonly List<NavEditorRecordedInputSample> _navEditorRecordedSamples = new();
    private PlayerInputSnapshot _navEditorTraversalCaptureInput;
    private readonly List<NavEditorPlaybackStep> _navEditorPlaybackSteps = new();
    private int _navEditorPlaybackStepIndex = -1;
    private int _navEditorPlaybackFrameIndex;
    private double _navEditorPlaybackFrameSecondsRemaining;
    private int _navEditorPlaybackPostTapeTicks;
    private int _navEditorPlaybackElapsedTicks;
    private string _navEditorPlaybackLabel = string.Empty;
    private bool _navEditorPanelCollapsed;
    private bool _navEditorPanelDragging;
    private bool _navEditorPanelPositionInitialized;
    private int _navEditorDraggingAnchorIndex = -1;
    private Vector2 _navEditorDraggingAnchorOffset;
    private bool _navEditorDraggingActivationZone;
    private int _navEditorActivationZoneAnchorIndex = -1;
    private Vector2 _navEditorActivationZoneStart;
    private Vector2 _navEditorActivationZoneCurrent;
    private Point _navEditorPanelPosition;
    private Point _navEditorPanelDragOffset;
    private bool _navEditorContextMenuOpen;
    private Point _navEditorContextMenuPosition;
    private Vector2 _navEditorContextMenuWorldPosition;
    private NavEditorContextMenuTargetKind _navEditorContextMenuTargetKind;
    private int _navEditorContextMenuAnchorIndex = -1;
    private int _navEditorContextMenuLinkIndex = -1;
    private int _navEditorContextMenuGeneratedGraphEdgeFromNodeId = -1;
    private int _navEditorContextMenuGeneratedGraphEdgeToNodeId = -1;
    private int[] _navEditorContextMenuOpenPath = Array.Empty<int>();
    private bool _navEditorClearAllConfirmationOpen;
    private int _navEditorDraggingGeneratedGraphNodeId = -1;
    private Vector2 _navEditorDraggingGeneratedGraphNodeOffset;

    private void UpdateNavEditor(KeyboardState keyboard, MouseState mouse, MouseState panelMouse, Vector2 cameraPosition, float deltaSeconds)
    {
        UpdateNavEditorRebuildTask();
        if (_navEditorStatusSecondsRemaining > 0f)
        {
            _navEditorStatusSecondsRemaining = Math.Max(0f, _navEditorStatusSecondsRemaining - Math.Max(0f, deltaSeconds));
        }

        if (IsKeyPressed(keyboard, Keys.F6))
        {
            if (_navEditorEnabled)
            {
                DisableNavEditor("nav editor disabled");
            }
            else
            {
                EnableNavEditor();
            }
        }

        if (!_navEditorEnabled)
        {
            return;
        }

        if (!IsPracticeSessionActive)
        {
            DisableNavEditor("nav editor closed because practice mode is not active.");
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        var rightClickPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed;
        var rightClickReleased = mouse.RightButton != ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Pressed;

        if (_navEditorClearAllConfirmationOpen)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                CancelNavEditorClearAll();
                return;
            }

            if (IsKeyPressed(keyboard, Keys.Enter))
            {
                ConfirmNavEditorClearAll();
                return;
            }

            if (clickPressed)
            {
                _ = TryHandleNavEditorPanelClick(panelMouse);
                return;
            }

            if (rightClickPressed)
            {
                return;
            }
        }

        if (IsNavEditorTraversalCaptureActive() || IsNavEditorTraversalPlaybackActive())
        {
            if (clickPressed && (TryHandleNavEditorPanelClick(panelMouse) || IsNavEditorPanelHostHit(panelMouse.Position)))
            {
                return;
            }

            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                if (IsNavEditorTraversalCaptureActive())
                {
                    CancelNavEditorTraversalCapture("traversal recording canceled");
                }
                else
                {
                    CancelNavEditorTraversalPlayback("traversal test canceled");
                }
            }

            return;
        }

        if (IsKeyPressed(keyboard, Keys.F5))
        {
            ReloadNavEditorState("nav editor reloaded from disk");
        }

        if (IsKeyPressed(keyboard, Keys.F7))
        {
            SaveNavEditorState();
        }

        if (IsKeyPressed(keyboard, Keys.F8))
        {
            StartNavEditorRebuild();
        }

        if (IsKeyPressed(keyboard, Keys.F9))
        {
            ToggleNavEditorPanelCollapsed();
        }

        if (IsKeyPressed(keyboard, Keys.F10))
        {
            CycleNavEditorGraphEdgeDisplayMode();
        }

        if (!_navEditorRenamingAnchor
            && !_navEditorContextMenuOpen
            && IsKeyPressed(keyboard, Keys.L))
        {
            LinkMostRecentNavEditorAnchors();
        }

        if (_navEditorContextMenuOpen && IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseNavEditorContextMenu();
            return;
        }

        if (_navEditorRenamingAnchor)
        {
            if (IsKeyPressed(keyboard, Keys.Escape))
            {
                CancelNavEditorRename();
            }
            else if (IsKeyPressed(keyboard, Keys.Enter))
            {
                CommitNavEditorRename();
            }
        }
        else
        {
            if (IsKeyPressed(keyboard, Keys.Delete))
            {
                DeleteSelectedNavEditorItem();
            }

            if (IsKeyPressed(keyboard, Keys.Enter) || IsKeyPressed(keyboard, Keys.F2))
            {
                BeginNavEditorRename();
            }
        }

        if (_navEditorPanelDragging)
        {
            if (panelMouse.LeftButton == ButtonState.Pressed)
            {
                UpdateNavEditorPanelDrag(panelMouse);
                return;
            }

            _navEditorPanelDragging = false;
        }

        if (_navEditorDraggingAnchorIndex >= 0)
        {
            if (mouse.LeftButton == ButtonState.Pressed
                && _navEditorLayerMode != NavEditorLayerMode.GeneratedGraph
                && _navEditorDraggingAnchorIndex < _navEditorAnchors.Count)
            {
                DragNavEditorAnchor(_navEditorDraggingAnchorIndex, mouse.Position.ToVector2() + cameraPosition - _navEditorDraggingAnchorOffset);
                return;
            }

            _navEditorDraggingAnchorIndex = -1;
        }

        if (_navEditorDraggingGeneratedGraphNodeId >= 0)
        {
            if (mouse.LeftButton == ButtonState.Pressed
                && _navEditorLayerMode != NavEditorLayerMode.HintLayer)
            {
                DragNavEditorGeneratedGraphNode(_navEditorDraggingGeneratedGraphNodeId, mouse.Position.ToVector2() + cameraPosition - _navEditorDraggingGeneratedGraphNodeOffset);
                return;
            }

            _navEditorDraggingGeneratedGraphNodeId = -1;
        }

        if (_navEditorDraggingActivationZone)
        {
            if (mouse.RightButton == ButtonState.Pressed)
            {
                UpdateNavEditorActivationZoneDrag(mouse, cameraPosition);
                return;
            }

            if (rightClickReleased)
            {
                CommitNavEditorActivationZoneDrag();
                return;
            }

            CancelNavEditorActivationZoneDrag();
        }

        if (_navEditorContextMenuOpen)
        {
            UpdateNavEditorContextMenuHover(mouse.Position);
        }

        if (rightClickPressed)
        {
            if (IsNavEditorPanelHostHit(panelMouse.Position))
            {
                CloseNavEditorContextMenu();
                return;
            }

            if (IsNavEditorControlDown(keyboard))
            {
                BeginNavEditorActivationZoneDrag(mouse, cameraPosition);
                return;
            }

            OpenNavEditorContextMenu(mouse, cameraPosition);
            return;
        }

        if (clickPressed)
        {
            if (_navEditorContextMenuOpen)
            {
                if (TryHandleNavEditorContextMenuClick(mouse.Position))
                {
                    return;
                }

                CloseNavEditorContextMenu();
                return;
            }

            if (TryHandleNavEditorPanelClick(panelMouse) || IsNavEditorPanelHostHit(panelMouse.Position))
            {
                return;
            }

            HandleNavEditorWorldClick(mouse, cameraPosition);
            return;
        }
    }

    private void DrawNavEditorOverlay(MouseState mouse, Vector2 cameraPosition)
    {
        if (!_navEditorEnabled)
        {
            return;
        }

        DrawNavEditorWorldOverlay(cameraPosition);
        DrawNavEditorContextMenu(mouse);
        if (!ShouldUseNavEditorWindowGutter())
        {
            DrawNavEditorPanel(mouse);
        }
    }

    private void DrawNavEditorPresentationOverlay(MouseState mouse)
    {
        if (!_navEditorEnabled || !ShouldUseNavEditorWindowGutter())
        {
            return;
        }

        var hostBounds = GetNavEditorPanelHostBounds();
        if (hostBounds.Width <= 0 || hostBounds.Height <= 0)
        {
            return;
        }

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        _spriteBatch.Draw(_pixel, hostBounds, new Color(8, 10, 12, 232));
        _spriteBatch.Draw(_pixel, new Rectangle(hostBounds.X, hostBounds.Y, 2, hostBounds.Height), new Color(92, 196, 255, 120));
        DrawNavEditorPanel(mouse);
        _spriteBatch.End();
    }

    private bool HandleNavEditorTextInput(TextInputEventArgs e)
    {
        return HandleNavEditorTextInput(e.Character);
    }

    private bool HandleNavEditorTextInput(char character)
    {
        if (!_navEditorEnabled || !_navEditorRenamingAnchor)
        {
            return false;
        }

        switch (character)
        {
            case '\b':
                if (_navEditorRenameBuffer.Length > 0)
                {
                    _navEditorRenameBuffer = _navEditorRenameBuffer[..^1];
                }
                break;
            case '\r':
            case '\n':
                CommitNavEditorRename();
                break;
            default:
                if (!char.IsControl(character) && _navEditorRenameBuffer.Length < 48)
                {
                    _navEditorRenameBuffer += character;
                }
                break;
        }

        return true;
    }

    private void EnableNavEditor()
    {
        if (!IsPracticeSessionActive)
        {
            AddConsoleLine("nav editor is practice-only. Start an offline practice session first.");
            return;
        }

        _navEditorEnabled = true;
        RefreshNavEditorWindowGutter();
        _navEditorTool = NavEditorTool.Select;
        _respawnCameraDetached = true;
        _respawnCameraCenter = ClampRespawnCameraCenter(new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y));
        _navEditorPanelPositionInitialized = false;
        EnsureNavEditorPanelPositionInitialized();
        if (HasRetainedNavEditorStateForCurrentLevel())
        {
            SetNavEditorStatus(_navEditorDirty ? "nav editor restored unsaved edits" : "nav editor enabled");
            AddConsoleLine($"nav editor restored {_navEditorAnchors.Count} anchors and {_navEditorLinks.Count} links for {_world.Level.Name} area {_world.Level.MapAreaIndex}");
            if (_navEditorDirty)
            {
                AddConsoleLine("nav editor restored unsaved in-memory edits. Press F7 to save them or F5 to reload from disk.");
            }
        }
        else
        {
            ReloadNavEditorState("nav editor enabled");
        }

        AddConsoleLine("nav editor controls: drag the header to move, drag selected hint nodes or graph nodes to reposition, F9 collapse/expand, F10 cycle graph edge colors, F7 save, F8 rebuild, F5 reload.");
        AddConsoleLine("nav editor fallback flow: mark one hint node as Entry, link it through waypoints, mark the final node as Exit, then choose a trigger such as NoNext or Stuck.");
        AddConsoleLine("nav editor activation zones: Ctrl+right-drag on a hint node, or with an entry selected, to draw the rectangle where fallback can trigger.");
        AddConsoleLine("nav editor graph edit: switch Layer to Graph to add/move/delete nodes; Link adds directed seams or two-way seams when New Direction is two-way; Save writes the consumed modern graph asset.");
        AddConsoleLine("nav editor graph sim: right-click graph nodes to set start/goal, then run Test Route.");
        AddConsoleLine("nav editor live bots: Run Trio respawns Scout/Heavy/Pyro on the local team from spawn in the current match state; Clear Trio restores the normal practice roster.");
    }

    private void DisableNavEditor(string reason)
    {
        StopNavEditorScoreTrioPracticeBots(silent: true);
        ClearNavEditorTraversalCaptureState();
        ClearNavEditorTraversalPlaybackState();
        _navEditorEnabled = false;
        RefreshNavEditorWindowGutter();
        _navEditorPanelDragging = false;
        _navEditorRenamingAnchor = false;
        _navEditorPendingLinkAnchorIndex = -1;
        _navEditorPendingGeneratedGraphSourceNodeId = -1;
        _navEditorDraggingGeneratedGraphNodeId = -1;
        CancelNavEditorActivationZoneDrag();
        _navEditorSelectedAnchorIndex = -1;
        _navEditorSelectedLinkIndex = -1;
        SetNavEditorStatus(reason);
        AddConsoleLine(reason);
        if (_navEditorDirty)
        {
            AddConsoleLine($"nav editor kept unsaved edits in memory for {_navEditorLoadedLevelName} area {_navEditorLoadedMapAreaIndex}. Reopen the editor on that map to continue, or press F5 after reopening to discard them.");
        }
    }

    private void ReloadNavEditorState(string statusMessage)
    {
        LoadNavEditorStateForCurrentLevel();
        SetNavEditorStatus(statusMessage);
        AddConsoleLine($"{statusMessage} ({_navEditorAnchors.Count} anchors, {_navEditorLinks.Count} links)");
    }

    private void LoadNavEditorStateForCurrentLevel()
    {
        _navEditorAnchors.Clear();
        _navEditorLinks.Clear();
        _navEditorSelectedAnchorIndex = -1;
        _navEditorSelectedLinkIndex = -1;
        _navEditorPendingLinkAnchorIndex = -1;
        _navEditorPendingGeneratedGraphSourceNodeId = -1;
        _navEditorDraggingAnchorIndex = -1;
        _navEditorDraggingGeneratedGraphNodeId = -1;
        CancelNavEditorActivationZoneDrag();
        ClearNavEditorGeneratedGraphSelection();
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        _navEditorDirty = false;
        _navEditorLoadedLevelName = _world.Level.Name;
        _navEditorLoadedMapAreaIndex = _world.Level.MapAreaIndex;
        ClearNavEditorTraversalCaptureState();
        ClearNavEditorTraversalPlaybackState();
        LoadNavEditorGeneratedCompatGraph();

        var asset = BotNavigationHintStore.Load(_world.Level);
        if (asset is not null)
        {
            foreach (var node in asset.Nodes)
            {
                _navEditorAnchors.Add(new NavEditorAnchor
                {
                    Label = node.Label,
                    AutoLabel = node.AutoLabel || IsNavEditorAutoGeneratedAnchorLabel(node.Label),
                    Classes = BotNavigationClasses.ResolveApplicableClasses(node.Classes, node.Profiles).ToArray(),
                    X = node.X,
                    Y = node.Y,
                    Kind = node.Kind,
                    Team = node.Team,
                    FallbackRole = node.FallbackRole,
                    FallbackTriggers = node.FallbackTriggers.ToArray(),
                    FallbackRadius = node.FallbackRadius,
                    RequiresCarryingIntel = node.RequiresCarryingIntel,
                    HasFallbackActivationZone = TryNormalizeNavEditorActivationZone(
                        node.FallbackActivationZone,
                        out var minX,
                        out var minY,
                        out var maxX,
                        out var maxY),
                    FallbackActivationMinX = minX,
                    FallbackActivationMinY = minY,
                    FallbackActivationMaxX = maxX,
                    FallbackActivationMaxY = maxY,
                });
            }

            foreach (var link in asset.Links)
            {
                _navEditorLinks.Add(new NavEditorLink
                {
                    FromLabel = link.FromLabel,
                    ToLabel = link.ToLabel,
                    Classes = BotNavigationClasses.ResolveApplicableClasses(link.Classes, link.Profiles).ToArray(),
                    Traversal = link.Traversal,
                    StartJumpImmediately = link.StartJumpImmediately,
                    Bidirectional = link.Bidirectional,
                    CostMultiplier = link.CostMultiplier,
                    RecordedTraversals = ExpandNavEditorRecordedTraversals(link.RecordedTraversals).ToList(),
                });
            }
        }

        _navEditorNextAnchorNumber = GetNextNavEditorAnchorNumber();
    }

    private void LoadNavEditorGeneratedCompatGraph()
    {
        try
        {
            if (BotNavigationAssetStore.TryLoadModernShippedAssetForEditing(_world.Level, out var savedAsset, out _, out _, out var savedValidation)
                && savedAsset is not null)
            {
                _navEditorGeneratedCompatGraph = savedAsset;
                _navEditorGeneratedCompatGraphStatus = savedValidation.IsStructurallyValid
                    ? $"saved graph: {savedAsset.Nodes.Count} nodes / {savedAsset.Edges.Count} links"
                    : $"saved graph: {savedAsset.Nodes.Count} nodes / {savedAsset.Edges.Count} links INVALID ({savedValidation.BuildSummary()})";
                return;
            }

            var fingerprint = BotNavigationLevelFingerprint.Compute(_world.Level);
            _navEditorGeneratedCompatGraph = BotNavigationModernPointGraphBuilder.Build(_world.Level, fingerprint);
            _navEditorGeneratedCompatGraphStatus = $"fresh graph: {_navEditorGeneratedCompatGraph.Nodes.Count} nodes / {_navEditorGeneratedCompatGraph.Edges.Count} links (geometry edge checks)";
        }
        catch (Exception ex)
        {
            _navEditorGeneratedCompatGraph = null;
            _navEditorGeneratedCompatGraphStatus = $"generated graph failed: {ex.Message}";
        }
    }

    private bool HasRetainedNavEditorStateForCurrentLevel()
    {
        return string.Equals(_navEditorLoadedLevelName, _world.Level.Name, StringComparison.OrdinalIgnoreCase)
            && _navEditorLoadedMapAreaIndex == _world.Level.MapAreaIndex
            && (_navEditorDirty || _navEditorAnchors.Count > 0 || _navEditorLinks.Count > 0);
    }

    private void HandleNavEditorWorldClick(MouseState mouse, Vector2 cameraPosition)
    {
        if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph)
        {
            HandleNavEditorGeneratedGraphWorldClick(mouse, cameraPosition);
            return;
        }

        var mousePosition = mouse.Position.ToVector2();
        switch (_navEditorTool)
        {
            case NavEditorTool.AddAnchor:
                if (TryPickNavEditorAnchor(mousePosition, cameraPosition, out var pickedAnchorIndex))
                {
                    SelectNavEditorAnchor(pickedAnchorIndex);
                    return;
                }

                if (!TryResolveNavEditorAnchorPlacement(mouse.X + cameraPosition.X, mouse.Y + cameraPosition.Y, out var snappedPosition))
                {
                    SetNavEditorStatus("anchor placement needs open space or nearby valid ground");
                    return;
                }

                AddNavEditorAnchor(snappedPosition);
                return;

            case NavEditorTool.AddLink:
                if (TryPickNavEditorAnchor(mousePosition, cameraPosition, out var linkAnchorIndex))
                {
                    HandleNavEditorLinkAnchorClick(linkAnchorIndex);
                    return;
                }

                if (TryPickNavEditorLink(mousePosition, cameraPosition, out var pickedLinkIndex))
                {
                    SelectNavEditorLink(pickedLinkIndex);
                    return;
                }

                SetNavEditorStatus("click one anchor, then another anchor, to create a link");
                return;

            default:
                if (TryPickNavEditorAnchor(mousePosition, cameraPosition, out var selectedAnchorIndex))
                {
                    SelectNavEditorAnchor(selectedAnchorIndex);
                    BeginNavEditorAnchorDrag(selectedAnchorIndex, mouse.Position.ToVector2() + cameraPosition);
                    return;
                }

                if (TryPickNavEditorLink(mousePosition, cameraPosition, out var selectedLinkIndex))
                {
                    SelectNavEditorLink(selectedLinkIndex);
                    return;
                }

                if (_navEditorLayerMode == NavEditorLayerMode.Both
                    && TryPickNavEditorGeneratedGraphNode(mousePosition, cameraPosition, out var selectedGraphNodeId))
                {
                    SelectNavEditorGeneratedGraphNode(selectedGraphNodeId);
                    BeginNavEditorGeneratedGraphNodeDrag(selectedGraphNodeId, mouse.Position.ToVector2() + cameraPosition);
                    return;
                }

                if (_navEditorLayerMode == NavEditorLayerMode.Both
                    && TryPickNavEditorGeneratedGraphEdge(mousePosition, cameraPosition, out var selectedGraphEdgeFromNodeId, out var selectedGraphEdgeToNodeId))
                {
                    SelectNavEditorGeneratedGraphEdge(selectedGraphEdgeFromNodeId, selectedGraphEdgeToNodeId);
                    return;
                }

                ClearNavEditorSelection();
                return;
        }
    }

    private void HandleNavEditorGeneratedGraphWorldClick(MouseState mouse, Vector2 cameraPosition)
    {
        var mousePosition = mouse.Position.ToVector2();
        switch (_navEditorTool)
        {
            case NavEditorTool.AddAnchor:
                if (TryPickNavEditorGeneratedGraphNode(mousePosition, cameraPosition, out var pickedNodeId))
                {
                    SelectNavEditorGeneratedGraphNode(pickedNodeId);
                    return;
                }

                if (!TryResolveNavEditorAnchorPlacement(mouse.X + cameraPosition.X, mouse.Y + cameraPosition.Y, out var snappedPosition))
                {
                    SetNavEditorStatus("graph node placement needs open space or nearby valid ground");
                    return;
                }

                AddNavEditorGeneratedGraphNode(snappedPosition);
                return;

            case NavEditorTool.AddLink:
                if (TryPickNavEditorGeneratedGraphNode(mousePosition, cameraPosition, out var linkNodeId))
                {
                    HandleNavEditorGeneratedGraphLinkNodeClick(linkNodeId);
                    return;
                }

                if (TryPickNavEditorGeneratedGraphEdge(mousePosition, cameraPosition, out var linkEdgeFromNodeId, out var linkEdgeToNodeId))
                {
                    SelectNavEditorGeneratedGraphEdge(linkEdgeFromNodeId, linkEdgeToNodeId);
                    return;
                }

                SetNavEditorStatus("click one graph node, then another, to add or remove a directed edge");
                return;

            default:
                if (TryPickNavEditorGeneratedGraphNode(mousePosition, cameraPosition, out var graphNodeId))
                {
                    SelectNavEditorGeneratedGraphNode(graphNodeId);
                    BeginNavEditorGeneratedGraphNodeDrag(graphNodeId, mouse.Position.ToVector2() + cameraPosition);
                    return;
                }

                if (TryPickNavEditorGeneratedGraphEdge(mousePosition, cameraPosition, out var selectedEdgeFromNodeId, out var selectedEdgeToNodeId))
                {
                    SelectNavEditorGeneratedGraphEdge(selectedEdgeFromNodeId, selectedEdgeToNodeId);
                    return;
                }

                ClearNavEditorSelection();
                return;
        }
    }

    private void AddNavEditorGeneratedGraphNode(Vector2 snappedPosition)
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            SetNavEditorStatus("graph node add failed: generated graph is not loaded");
            return;
        }

        var roundedX = MathF.Round(snappedPosition.X);
        var roundedY = MathF.Round(snappedPosition.Y);
        var surfaceId = ResolveNavEditorGeneratedGraphSurfaceId(roundedX, roundedY);
        _navEditorGeneratedCompatGraph = BotNavigationModernGraphEditor.AddSurfaceNode(asset, surfaceId, roundedX, roundedY);
        _navEditorDirty = true;
        UpdateNavEditorGeneratedGraphEditStatus();
        var newNodeId = _navEditorGeneratedCompatGraph.Nodes.Max(static node => node.Id);
        SelectNavEditorGeneratedGraphNode(newNodeId);
        SetNavEditorStatus($"graph node added: {newNodeId} at ({roundedX:F0}, {roundedY:F0})");
    }

    private void HandleNavEditorGeneratedGraphLinkNodeClick(int nodeId)
    {
        SelectNavEditorGeneratedGraphNode(nodeId);
        if (_navEditorPendingGeneratedGraphSourceNodeId < 0)
        {
            _navEditorPendingGeneratedGraphSourceNodeId = nodeId;
            SetNavEditorStatus($"graph edge start set to node {nodeId}. Click a destination node.");
            return;
        }

        if (_navEditorPendingGeneratedGraphSourceNodeId == nodeId)
        {
            SetNavEditorStatus("pick a different graph node to finish the directed edge");
            return;
        }

        ToggleNavEditorGeneratedGraphEdgeConnection(_navEditorPendingGeneratedGraphSourceNodeId, nodeId);
        _navEditorPendingGeneratedGraphSourceNodeId = nodeId;
    }

    private void BeginNavEditorGeneratedGraphNodeDrag(int nodeId, Vector2 worldPosition)
    {
        if (!TryGetNavEditorGeneratedGraphNode(nodeId, out var node))
        {
            return;
        }

        _navEditorDraggingGeneratedGraphNodeId = nodeId;
        _navEditorDraggingGeneratedGraphNodeOffset = worldPosition - new Vector2(node.X, node.Y);
    }

    private void DragNavEditorGeneratedGraphNode(int nodeId, Vector2 worldPosition)
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset
            || !TryGetNavEditorGeneratedGraphNode(nodeId, out var node))
        {
            return;
        }

        var roundedX = MathF.Round(worldPosition.X);
        var roundedY = MathF.Round(worldPosition.Y);
        if (Math.Abs(node.X - roundedX) < float.Epsilon && Math.Abs(node.Y - roundedY) < float.Epsilon)
        {
            return;
        }

        _navEditorGeneratedCompatGraph = BotNavigationModernGraphEditor.MoveNode(_world.Level, asset, nodeId, roundedX, roundedY);
        _navEditorDirty = true;
        UpdateNavEditorGeneratedGraphEditStatus();
        RefreshNavEditorGeneratedGraphRoutePreview();
        SetNavEditorStatus($"moved graph node {nodeId} to ({roundedX:F0}, {roundedY:F0}); incident seams rebuilt");
    }

    private void ToggleNavEditorGeneratedGraphEdgeConnection(int fromNodeId, int toNodeId)
    {
        if (_navEditorDefaultBidirectional)
        {
            ToggleNavEditorGeneratedGraphBidirectionalEdge(fromNodeId, toNodeId);
            return;
        }

        ToggleNavEditorGeneratedGraphDirectedEdge(fromNodeId, toNodeId);
    }

    private void ToggleNavEditorGeneratedGraphDirectedEdge(int fromNodeId, int toNodeId)
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            SetNavEditorStatus("graph edge edit failed: generated graph is not loaded");
            return;
        }

        if (asset.Edges.Any(edge => edge.FromNodeId == fromNodeId && edge.ToNodeId == toNodeId))
        {
            _navEditorGeneratedCompatGraph = BotNavigationModernGraphEditor.RemoveDirectedEdge(asset, fromNodeId, toNodeId, out var removed);
            if (!removed)
            {
                SetNavEditorStatus($"graph edge {fromNodeId}->{toNodeId} was already gone");
                return;
            }

            _navEditorDirty = true;
            UpdateNavEditorGeneratedGraphEditStatus();
            RefreshNavEditorGeneratedGraphRoutePreview();
            SetNavEditorStatus($"graph edge removed: {fromNodeId} -> {toNodeId}");
            return;
        }

        if (!BotNavigationModernGraphEditor.TryBuildDirectedEdge(_world.Level, asset, fromNodeId, toNodeId, out var edge, out var failureMessage))
        {
            SetNavEditorStatus($"graph edge add failed: {failureMessage}");
            return;
        }

        _navEditorGeneratedCompatGraph = BotNavigationModernGraphEditor.UpsertDirectedEdge(asset, edge);
        _navEditorDirty = true;
        UpdateNavEditorGeneratedGraphEditStatus();
        RefreshNavEditorGeneratedGraphRoutePreview();
        SetNavEditorStatus($"graph edge added: {fromNodeId} -> {toNodeId} ({edge.Kind})");
    }

    private void ToggleNavEditorGeneratedGraphBidirectionalEdge(int firstNodeId, int secondNodeId)
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            SetNavEditorStatus("graph edge edit failed: generated graph is not loaded");
            return;
        }

        var forwardExists = asset.Edges.Any(edge => edge.FromNodeId == firstNodeId && edge.ToNodeId == secondNodeId);
        var reverseExists = asset.Edges.Any(edge => edge.FromNodeId == secondNodeId && edge.ToNodeId == firstNodeId);
        if (forwardExists && reverseExists)
        {
            var updatedAsset = BotNavigationModernGraphEditor.RemoveDirectedEdge(asset, firstNodeId, secondNodeId, out _);
            updatedAsset = BotNavigationModernGraphEditor.RemoveDirectedEdge(updatedAsset, secondNodeId, firstNodeId, out _);
            _navEditorGeneratedCompatGraph = updatedAsset;
            _navEditorDirty = true;
            UpdateNavEditorGeneratedGraphEditStatus();
            RefreshNavEditorGeneratedGraphRoutePreview();
            SetNavEditorStatus($"graph edge pair removed: {firstNodeId} <-> {secondNodeId}");
            return;
        }

        var workingAsset = asset;
        var addedDirections = new List<string>(2);
        var failures = new List<string>(2);
        TryEnsureNavEditorGeneratedGraphDirectedEdge(ref workingAsset, firstNodeId, secondNodeId, $"{firstNodeId}->{secondNodeId}", addedDirections, failures);
        TryEnsureNavEditorGeneratedGraphDirectedEdge(ref workingAsset, secondNodeId, firstNodeId, $"{secondNodeId}->{firstNodeId}", addedDirections, failures);
        if (addedDirections.Count <= 0)
        {
            SetNavEditorStatus(failures.Count > 0
                ? $"graph two-way add failed: {string.Join("; ", failures)}"
                : $"graph edge pair unchanged: {firstNodeId} <-> {secondNodeId}");
            return;
        }

        _navEditorGeneratedCompatGraph = workingAsset;
        _navEditorDirty = true;
        UpdateNavEditorGeneratedGraphEditStatus();
        RefreshNavEditorGeneratedGraphRoutePreview();
        var failureSuffix = failures.Count > 0
            ? $" ({string.Join("; ", failures)})"
            : string.Empty;
        SetNavEditorStatus(
            addedDirections.Count == 2
                ? $"graph edge pair set: {firstNodeId} <-> {secondNodeId}{failureSuffix}"
                : $"graph edge updated: added {string.Join(", ", addedDirections)}{failureSuffix}");
    }

    private void TryEnsureNavEditorGeneratedGraphDirectedEdge(
        ref BotNavigationAsset asset,
        int fromNodeId,
        int toNodeId,
        string label,
        List<string> addedDirections,
        List<string> failures)
    {
        if (asset.Edges.Any(edge => edge.FromNodeId == fromNodeId && edge.ToNodeId == toNodeId))
        {
            return;
        }

        if (!BotNavigationModernGraphEditor.TryBuildDirectedEdge(_world.Level, asset, fromNodeId, toNodeId, out var edge, out var failureMessage))
        {
            failures.Add($"{label} {failureMessage}");
            return;
        }

        asset = BotNavigationModernGraphEditor.UpsertDirectedEdge(asset, edge);
        addedDirections.Add(label);
    }

    private void AddNavEditorAnchor(Vector2 snappedPosition)
    {
        AddNavEditorAnchor(
            snappedPosition,
            _navEditorDefaultAnchorKind,
            _navEditorDefaultAnchorTeam,
            _navEditorDefaultClasses,
            beginRename: true);
    }

    private void AddNavEditorAnchor(
        Vector2 snappedPosition,
        BotNavigationNodeKind kind,
        PlayerTeam? team,
        IReadOnlyList<PlayerClass> classes,
        bool beginRename)
    {
        _navEditorNextAnchorNumber = GetNextNavEditorAnchorNumber();
        var normalizedTeam = NormalizeNavEditorAnchorTeam(kind, team);
        var anchor = new NavEditorAnchor
        {
            Label = BuildSuggestedNavEditorAnchorLabel(kind, normalizedTeam),
            AutoLabel = true,
            Classes = classes.ToArray(),
            X = snappedPosition.X,
            Y = snappedPosition.Y,
            Kind = kind,
            Team = normalizedTeam,
        };
        _navEditorNextAnchorNumber += 1;
        _navEditorAnchors.Add(anchor);
        _navEditorDirty = true;
        SelectNavEditorAnchor(_navEditorAnchors.Count - 1);
        if (beginRename)
        {
            BeginNavEditorRename();
        }

        SetNavEditorStatus($"anchor added at ({anchor.X:F0}, {anchor.Y:F0}) as {(IsNavEditorAnchorGrounded(anchor) ? "grounded" : "floating")}");
    }

    private void LinkMostRecentNavEditorAnchors()
    {
        if (_navEditorAnchors.Count < 2)
        {
            SetNavEditorStatus("place at least two anchors before quick-linking");
            return;
        }

        var fromAnchorIndex = _navEditorAnchors.Count - 2;
        var toAnchorIndex = _navEditorAnchors.Count - 1;
        _navEditorPendingLinkAnchorIndex = -1;
        CreateOrUpdateNavEditorLink(fromAnchorIndex, toAnchorIndex);
    }

    private void HandleNavEditorLinkAnchorClick(int anchorIndex)
    {
        SelectNavEditorAnchor(anchorIndex);
        if (_navEditorPendingLinkAnchorIndex < 0)
        {
            _navEditorPendingLinkAnchorIndex = anchorIndex;
            SetNavEditorStatus($"link start set to {_navEditorAnchors[anchorIndex].Label}. Click a destination anchor.");
            return;
        }

        if (_navEditorPendingLinkAnchorIndex == anchorIndex)
        {
            SetNavEditorStatus("pick a different anchor to finish the link");
            return;
        }

        CreateOrUpdateNavEditorLink(_navEditorPendingLinkAnchorIndex, anchorIndex);
        _navEditorPendingLinkAnchorIndex = anchorIndex;
    }

    private void CreateOrUpdateNavEditorLink(int fromAnchorIndex, int toAnchorIndex)
    {
        var fromAnchor = _navEditorAnchors[fromAnchorIndex];
        var toAnchor = _navEditorAnchors[toAnchorIndex];
        var existingIndex = FindNavEditorLinkIndex(fromAnchor.Label, toAnchor.Label);
        var defaultTraversal = ResolveNavEditorDefaultTraversalForNewLink(fromAnchor, toAnchor);
        if (existingIndex >= 0)
        {
            var link = _navEditorLinks[existingIndex];
            link.Traversal = defaultTraversal;
            link.StartJumpImmediately = false;
            link.Bidirectional = _navEditorDefaultBidirectional;
            link.CostMultiplier = _navEditorDefaultCostMultiplier;
            link.Classes = _navEditorDefaultClasses.ToArray();
            SelectNavEditorLink(existingIndex);
            _navEditorDirty = true;
            SetNavEditorStatus($"link updated: {fromAnchor.Label} -> {toAnchor.Label}");
            return;
        }

        _navEditorLinks.Add(new NavEditorLink
        {
            FromLabel = fromAnchor.Label,
            ToLabel = toAnchor.Label,
            Classes = _navEditorDefaultClasses.ToArray(),
            Traversal = defaultTraversal,
            StartJumpImmediately = false,
            Bidirectional = _navEditorDefaultBidirectional,
            CostMultiplier = _navEditorDefaultCostMultiplier,
        });
        _navEditorDirty = true;
        SelectNavEditorLink(_navEditorLinks.Count - 1);
        SetNavEditorStatus($"link added: {fromAnchor.Label} -> {toAnchor.Label}");
    }

    private BotNavigationHintTraversalKind ResolveNavEditorDefaultTraversalForNewLink(NavEditorAnchor fromAnchor, NavEditorAnchor toAnchor)
    {
        if (_navEditorDefaultTraversal != BotNavigationHintTraversalKind.Auto)
        {
            return _navEditorDefaultTraversal;
        }

        return !IsNavEditorAnchorGrounded(fromAnchor) || !IsNavEditorAnchorGrounded(toAnchor)
            ? BotNavigationHintTraversalKind.Jump
            : BotNavigationHintTraversalKind.Auto;
    }

    private void BeginNavEditorRename()
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            return;
        }

        _navEditorRenamingAnchor = true;
        _navEditorRenameBuffer = _navEditorAnchors[_navEditorSelectedAnchorIndex].Label;
        SetNavEditorStatus("type a new anchor name and press Enter");
    }

    private void CommitNavEditorRename()
    {
        if (!_navEditorRenamingAnchor
            || _navEditorSelectedAnchorIndex < 0
            || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        var oldLabel = anchor.Label;
        var newLabel = MakeUniqueNavEditorAnchorLabel(_navEditorRenameBuffer, oldLabel);
        if (TrySetNavEditorAnchorLabel(anchor, newLabel))
        {
            anchor.AutoLabel = false;
        }

        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        SetNavEditorStatus($"anchor renamed to {newLabel}");
    }

    private void CancelNavEditorRename()
    {
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        SetNavEditorStatus("anchor rename canceled");
    }

    private void DeleteSelectedNavEditorItem()
    {
        if (_navEditorSelectedGeneratedGraphEdgeFromNodeId >= 0
            && _navEditorSelectedGeneratedGraphEdgeToNodeId >= 0
            && _navEditorGeneratedCompatGraph is BotNavigationAsset edgeAsset)
        {
            var fromNodeId = _navEditorSelectedGeneratedGraphEdgeFromNodeId;
            var toNodeId = _navEditorSelectedGeneratedGraphEdgeToNodeId;
            _navEditorGeneratedCompatGraph = BotNavigationModernGraphEditor.RemoveDirectedEdge(edgeAsset, fromNodeId, toNodeId, out var removed);
            if (!removed)
            {
                SetNavEditorStatus($"graph edge {fromNodeId}->{toNodeId} was already gone");
                return;
            }

            _navEditorSelectedGeneratedGraphEdgeFromNodeId = -1;
            _navEditorSelectedGeneratedGraphEdgeToNodeId = -1;
            _navEditorDirty = true;
            UpdateNavEditorGeneratedGraphEditStatus();
            RefreshNavEditorGeneratedGraphRoutePreview();
            SetNavEditorStatus($"graph edge deleted: {fromNodeId} -> {toNodeId}");
            return;
        }

        if (_navEditorSelectedGeneratedGraphNodeId >= 0
            && _navEditorGeneratedCompatGraph is BotNavigationAsset graphAsset)
        {
            var nodeId = _navEditorSelectedGeneratedGraphNodeId;
            var deleteResult = BotNavigationModernGraphEditor.DeleteNodeAndReconnect(_world.Level, graphAsset, nodeId);
            if (deleteResult.RemovedNodes <= 0)
            {
                SetNavEditorStatus($"graph node {nodeId} was already gone");
                return;
            }

            _navEditorGeneratedCompatGraph = deleteResult.Asset;
            _navEditorSelectedGeneratedGraphNodeId = -1;
            if (_navEditorPendingGeneratedGraphSourceNodeId == nodeId)
            {
                _navEditorPendingGeneratedGraphSourceNodeId = -1;
            }

            if (_navEditorGraphRouteStartNodeId == nodeId)
            {
                _navEditorGraphRouteStartNodeId = -1;
            }

            if (_navEditorGraphRouteGoalNodeId == nodeId)
            {
                _navEditorGraphRouteGoalNodeId = -1;
            }

            _navEditorDirty = true;
            UpdateNavEditorGeneratedGraphEditStatus();
            RefreshNavEditorGeneratedGraphRoutePreview();
            SetNavEditorStatus($"graph node deleted: {nodeId} (removed {deleteResult.RemovedEdges} edges, added {deleteResult.AddedEdges} reconnects)");
            return;
        }

        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            var label = _navEditorAnchors[_navEditorSelectedAnchorIndex].Label;
            _navEditorAnchors.RemoveAt(_navEditorSelectedAnchorIndex);
            _navEditorLinks.RemoveAll(link =>
                string.Equals(link.FromLabel, label, StringComparison.OrdinalIgnoreCase)
                || string.Equals(link.ToLabel, label, StringComparison.OrdinalIgnoreCase));
            _navEditorSelectedAnchorIndex = -1;
            _navEditorSelectedLinkIndex = -1;
            _navEditorPendingLinkAnchorIndex = -1;
            _navEditorDirty = true;
            _navEditorRenamingAnchor = false;
            _navEditorRenameBuffer = string.Empty;
            _navEditorNextAnchorNumber = GetNextNavEditorAnchorNumber();
            SetNavEditorStatus($"anchor deleted: {label}");
            return;
        }

        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            _navEditorLinks.RemoveAt(_navEditorSelectedLinkIndex);
            _navEditorSelectedLinkIndex = -1;
            _navEditorDirty = true;
            SetNavEditorStatus($"link deleted: {link.FromLabel} -> {link.ToLabel}");
        }
    }

    private void BeginNavEditorClearAll()
    {
        if (_navEditorAnchors.Count == 0 && _navEditorLinks.Count == 0)
        {
            SetNavEditorStatus("there are no nodes to clear");
            return;
        }

        CloseNavEditorContextMenu();
        _navEditorClearAllConfirmationOpen = true;
        SetNavEditorStatus("Are you sure you want to delete all nodes?");
    }

    private void ConfirmNavEditorClearAll()
    {
        var anchorCount = _navEditorAnchors.Count;
        var linkCount = _navEditorLinks.Count;
        _navEditorAnchors.Clear();
        _navEditorLinks.Clear();
        _navEditorSelectedAnchorIndex = -1;
        _navEditorSelectedLinkIndex = -1;
        _navEditorPendingLinkAnchorIndex = -1;
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        _navEditorNextAnchorNumber = 1;
        _navEditorClearAllConfirmationOpen = false;
        CancelNavEditorActivationZoneDrag();
        _navEditorDirty = true;
        SetNavEditorStatus("cleared all nodes");
        AddConsoleLine($"nav editor cleared {anchorCount} anchors and {linkCount} links");
    }

    private void CancelNavEditorClearAll()
    {
        _navEditorClearAllConfirmationOpen = false;
        SetNavEditorStatus("clear all canceled");
    }

    private void StopNavEditorTraversalActivity()
    {
        if (IsNavEditorTraversalCaptureActive())
        {
            CancelNavEditorTraversalCapture("traversal recording stopped");
            return;
        }

        if (IsNavEditorTraversalPlaybackActive())
        {
            CancelNavEditorTraversalPlayback("traversal test stopped");
            return;
        }

        SetNavEditorStatus("no traversal test is active");
    }

    private void SelectNavEditorAnchor(int anchorIndex)
    {
        _navEditorSelectedAnchorIndex = anchorIndex;
        _navEditorSelectedLinkIndex = -1;
        _navEditorSelectedGeneratedGraphNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeFromNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeToNodeId = -1;
        _navEditorPendingGeneratedGraphSourceNodeId = -1;
        RefreshNavEditorGeneratedGraphRoutePreview();
    }

    private void BeginNavEditorAnchorDrag(int anchorIndex, Vector2 worldPosition)
    {
        if (anchorIndex < 0 || anchorIndex >= _navEditorAnchors.Count)
        {
            return;
        }

        var anchor = _navEditorAnchors[anchorIndex];
        _navEditorDraggingAnchorIndex = anchorIndex;
        _navEditorDraggingAnchorOffset = worldPosition - new Vector2(anchor.X, anchor.Y);
    }

    private void DragNavEditorAnchor(int anchorIndex, Vector2 worldPosition)
    {
        if (anchorIndex < 0 || anchorIndex >= _navEditorAnchors.Count)
        {
            return;
        }

        var anchor = _navEditorAnchors[anchorIndex];
        anchor.X = MathF.Round(worldPosition.X);
        anchor.Y = MathF.Round(worldPosition.Y);
        _navEditorDirty = true;
        SetNavEditorStatus($"moved {anchor.Label} to ({anchor.X:F0}, {anchor.Y:F0})");
    }

    private void BeginNavEditorActivationZoneDrag(MouseState mouse, Vector2 cameraPosition)
    {
        if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph)
        {
            SetNavEditorStatus("activation zones only apply to hint fallback entries. Switch Layer to Hints or Both.");
            return;
        }

        var mouseScreenPosition = mouse.Position.ToVector2();
        var anchorIndex = _navEditorSelectedAnchorIndex;
        if (TryPickNavEditorAnchor(mouseScreenPosition, cameraPosition, out var pickedAnchorIndex))
        {
            anchorIndex = pickedAnchorIndex;
            SelectNavEditorAnchor(anchorIndex);
        }

        if (anchorIndex < 0 || anchorIndex >= _navEditorAnchors.Count)
        {
            SetNavEditorStatus("select a fallback entry node, then Ctrl+right-drag to draw its activation rectangle");
            return;
        }

        _navEditorDraggingActivationZone = true;
        _navEditorActivationZoneAnchorIndex = anchorIndex;
        _navEditorActivationZoneStart = mouseScreenPosition + cameraPosition;
        _navEditorActivationZoneCurrent = _navEditorActivationZoneStart;
        _navEditorDraggingAnchorIndex = -1;
        CloseNavEditorContextMenu();
        SetNavEditorStatus($"drawing activation zone for {_navEditorAnchors[anchorIndex].Label}");
    }

    private void UpdateNavEditorActivationZoneDrag(MouseState mouse, Vector2 cameraPosition)
    {
        _navEditorActivationZoneCurrent = mouse.Position.ToVector2() + cameraPosition;
    }

    private void CommitNavEditorActivationZoneDrag()
    {
        if (_navEditorActivationZoneAnchorIndex < 0 || _navEditorActivationZoneAnchorIndex >= _navEditorAnchors.Count)
        {
            CancelNavEditorActivationZoneDrag();
            return;
        }

        var minX = MathF.Round(MathF.Min(_navEditorActivationZoneStart.X, _navEditorActivationZoneCurrent.X));
        var minY = MathF.Round(MathF.Min(_navEditorActivationZoneStart.Y, _navEditorActivationZoneCurrent.Y));
        var maxX = MathF.Round(MathF.Max(_navEditorActivationZoneStart.X, _navEditorActivationZoneCurrent.X));
        var maxY = MathF.Round(MathF.Max(_navEditorActivationZoneStart.Y, _navEditorActivationZoneCurrent.Y));
        var width = maxX - minX;
        var height = maxY - minY;
        var anchor = _navEditorAnchors[_navEditorActivationZoneAnchorIndex];
        CancelNavEditorActivationZoneDrag();
        if (width < NavEditorMinimumActivationZoneSize || height < NavEditorMinimumActivationZoneSize)
        {
            SetNavEditorStatus("activation zone canceled: drag a wider rectangle");
            return;
        }

        if (anchor.FallbackRole != BotNavigationHintFallbackRole.Entry)
        {
            anchor.FallbackRole = BotNavigationHintFallbackRole.Entry;
            if (anchor.FallbackTriggers.Length == 0)
            {
                anchor.FallbackTriggers = [BotNavigationHintFallbackTrigger.NoNext];
            }
        }

        anchor.HasFallbackActivationZone = true;
        anchor.FallbackActivationMinX = minX;
        anchor.FallbackActivationMinY = minY;
        anchor.FallbackActivationMaxX = maxX;
        anchor.FallbackActivationMaxY = maxY;
        _navEditorDirty = true;
        SetNavEditorStatus($"activation zone set for {anchor.Label}: {width:F0}x{height:F0}");
    }

    private void CancelNavEditorActivationZoneDrag()
    {
        _navEditorDraggingActivationZone = false;
        _navEditorActivationZoneAnchorIndex = -1;
        _navEditorActivationZoneStart = Vector2.Zero;
        _navEditorActivationZoneCurrent = Vector2.Zero;
    }

    private void SelectNavEditorLink(int linkIndex)
    {
        _navEditorSelectedLinkIndex = linkIndex;
        _navEditorSelectedAnchorIndex = -1;
        _navEditorSelectedGeneratedGraphNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeFromNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeToNodeId = -1;
        _navEditorPendingGeneratedGraphSourceNodeId = -1;
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        RefreshNavEditorGeneratedGraphRoutePreview();
    }

    private void ClearNavEditorSelection()
    {
        _navEditorSelectedAnchorIndex = -1;
        _navEditorSelectedLinkIndex = -1;
        _navEditorSelectedGeneratedGraphNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeFromNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeToNodeId = -1;
        _navEditorPendingGeneratedGraphSourceNodeId = -1;
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        RefreshNavEditorGeneratedGraphRoutePreview();
    }

    private void SelectNavEditorGeneratedGraphNode(int nodeId)
    {
        _navEditorSelectedGeneratedGraphNodeId = nodeId;
        _navEditorSelectedGeneratedGraphEdgeFromNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeToNodeId = -1;
        _navEditorSelectedAnchorIndex = -1;
        _navEditorSelectedLinkIndex = -1;
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        RefreshNavEditorGeneratedGraphRoutePreview();
        SetNavEditorStatus($"selected generated graph node {nodeId}");
    }

    private void SelectNavEditorGeneratedGraphEdge(int fromNodeId, int toNodeId)
    {
        _navEditorSelectedGeneratedGraphNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeFromNodeId = fromNodeId;
        _navEditorSelectedGeneratedGraphEdgeToNodeId = toNodeId;
        _navEditorSelectedAnchorIndex = -1;
        _navEditorSelectedLinkIndex = -1;
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        RefreshNavEditorGeneratedGraphRoutePreview();
        SetNavEditorStatus($"selected graph edge {fromNodeId} -> {toNodeId}");
    }

    private void ClearNavEditorGeneratedGraphSelection()
    {
        _navEditorSelectedGeneratedGraphNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeFromNodeId = -1;
        _navEditorSelectedGeneratedGraphEdgeToNodeId = -1;
        _navEditorPendingGeneratedGraphSourceNodeId = -1;
        _navEditorGraphRouteStartNodeId = -1;
        _navEditorGraphRouteGoalNodeId = -1;
        _navEditorGraphPreviewRouteNodeIds = Array.Empty<int>();
    }

    private bool TryPickNavEditorAnchor(Vector2 mouseScreenPosition, Vector2 cameraPosition, out int anchorIndex)
    {
        anchorIndex = -1;
        var bestDistanceSquared = NavEditorAnchorPickRadius * NavEditorAnchorPickRadius;
        for (var index = _navEditorAnchors.Count - 1; index >= 0; index -= 1)
        {
            var anchor = _navEditorAnchors[index];
            if (!AppliesToNavEditorViewClass(anchor.Classes))
            {
                continue;
            }

            var screenPosition = new Vector2(anchor.X - cameraPosition.X, anchor.Y - cameraPosition.Y);
            var distanceSquared = Vector2.DistanceSquared(mouseScreenPosition, screenPosition);
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            anchorIndex = index;
        }

        return anchorIndex >= 0;
    }

    private bool TryPickNavEditorGeneratedGraphNode(Vector2 mouseScreenPosition, Vector2 cameraPosition, out int nodeId)
    {
        nodeId = -1;
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            return false;
        }

        var bestDistanceSquared = NavEditorAnchorPickRadius * NavEditorAnchorPickRadius;
        for (var index = 0; index < asset.Nodes.Count; index += 1)
        {
            var node = asset.Nodes[index];
            var screenPosition = new Vector2(node.X - cameraPosition.X, node.Y - cameraPosition.Y);
            var distanceSquared = Vector2.DistanceSquared(mouseScreenPosition, screenPosition);
            if (distanceSquared > bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            nodeId = node.Id;
        }

        return nodeId >= 0;
    }

    private bool TryPickNavEditorGeneratedGraphEdge(Vector2 mouseScreenPosition, Vector2 cameraPosition, out int fromNodeId, out int toNodeId)
    {
        fromNodeId = -1;
        toNodeId = -1;
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            return false;
        }

        var bestDistance = NavEditorLinkPickDistance;
        for (var index = asset.Edges.Count - 1; index >= 0; index -= 1)
        {
            var edge = asset.Edges[index];
            if (!TryGetNavEditorGeneratedGraphNode(edge.FromNodeId, out var fromNode)
                || !TryGetNavEditorGeneratedGraphNode(edge.ToNodeId, out var toNode))
            {
                continue;
            }

            var fromScreen = new Vector2(fromNode.X - cameraPosition.X, fromNode.Y - cameraPosition.Y);
            var toScreen = new Vector2(toNode.X - cameraPosition.X, toNode.Y - cameraPosition.Y);
            var distance = DistanceToSegment(mouseScreenPosition, fromScreen, toScreen);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            fromNodeId = edge.FromNodeId;
            toNodeId = edge.ToNodeId;
        }

        return fromNodeId >= 0 && toNodeId >= 0;
    }

    private bool TryGetNavEditorGeneratedGraphEdge(int fromNodeId, int toNodeId, out BotNavigationEdge edge)
    {
        edge = default!;
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            return false;
        }

        for (var index = 0; index < asset.Edges.Count; index += 1)
        {
            var candidate = asset.Edges[index];
            if (candidate.FromNodeId != fromNodeId || candidate.ToNodeId != toNodeId)
            {
                continue;
            }

            edge = candidate;
            return true;
        }

        return false;
    }

    private bool TryPickNavEditorLink(Vector2 mouseScreenPosition, Vector2 cameraPosition, out int linkIndex)
    {
        linkIndex = -1;
        var bestDistance = NavEditorLinkPickDistance;
        for (var index = _navEditorLinks.Count - 1; index >= 0; index -= 1)
        {
            var link = _navEditorLinks[index];
            if (!AppliesToNavEditorViewClass(link.Classes))
            {
                continue;
            }

            if (!TryGetNavEditorAnchorPosition(link.FromLabel, out var fromPosition)
                || !TryGetNavEditorAnchorPosition(link.ToLabel, out var toPosition))
            {
                continue;
            }

            var fromScreen = fromPosition - cameraPosition;
            var toScreen = toPosition - cameraPosition;
            var distance = DistanceToSegment(mouseScreenPosition, fromScreen, toScreen);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            linkIndex = index;
        }

        return linkIndex >= 0;
    }

    private bool TryGetNavEditorAnchorPosition(string label, out Vector2 position)
    {
        for (var index = 0; index < _navEditorAnchors.Count; index += 1)
        {
            var anchor = _navEditorAnchors[index];
            if (!string.Equals(anchor.Label, label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            position = new Vector2(anchor.X, anchor.Y);
            return true;
        }

        position = default;
        return false;
    }

    private bool TryGetNavEditorAnchor(string label, out NavEditorAnchor anchor)
    {
        for (var index = 0; index < _navEditorAnchors.Count; index += 1)
        {
            if (!string.Equals(_navEditorAnchors[index].Label, label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            anchor = _navEditorAnchors[index];
            return true;
        }

        anchor = default!;
        return false;
    }

    private int FindNavEditorLinkIndex(string fromLabel, string toLabel)
    {
        for (var index = 0; index < _navEditorLinks.Count; index += 1)
        {
            var link = _navEditorLinks[index];
            if (string.Equals(link.FromLabel, fromLabel, StringComparison.OrdinalIgnoreCase)
                && string.Equals(link.ToLabel, toLabel, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private bool TryResolveNavEditorAnchorPlacement(float worldX, float worldY, out Vector2 snappedPosition)
    {
        if (TryFindNearbyFreeNavEditorAnchorPosition(worldX, worldY, out snappedPosition))
        {
            return true;
        }

        return TrySnapNavEditorAnchorToGround(worldX, worldY, out snappedPosition);
    }

    private bool TryFindNearbyFreeNavEditorAnchorPosition(float worldX, float worldY, out Vector2 snappedPosition)
    {
        snappedPosition = default;
        var classDefinition = GetNavEditorRepresentativeClassDefinition(GetNavEditorPreviewClass());
        if (CanPlaceNavEditorAnchorWithoutGround(classDefinition, worldX, worldY))
        {
            snappedPosition = new Vector2(worldX, worldY);
            return true;
        }

        var bestDistanceSquared = float.PositiveInfinity;
        for (var radius = NavEditorAnchorPlacementSearchStep; radius <= NavEditorAnchorFreePlacementMaximumDistance; radius += NavEditorAnchorPlacementSearchStep)
        {
            for (var offsetY = -radius; offsetY <= radius; offsetY += NavEditorAnchorPlacementSearchStep)
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX += NavEditorAnchorPlacementSearchStep)
                {
                    if (MathF.Abs(offsetX) < radius && MathF.Abs(offsetY) < radius)
                    {
                        continue;
                    }

                    var candidateX = worldX + offsetX;
                    var candidateY = worldY + offsetY;
                    if (!CanPlaceNavEditorAnchorWithoutGround(classDefinition, candidateX, candidateY))
                    {
                        continue;
                    }

                    var distanceSquared = (offsetX * offsetX) + (offsetY * offsetY);
                    if (distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    snappedPosition = new Vector2(candidateX, candidateY);
                }
            }

            if (bestDistanceSquared < float.PositiveInfinity)
            {
                return true;
            }
        }

        return false;
    }

    private bool TrySnapNavEditorAnchorToGround(float worldX, float worldY, out Vector2 snappedPosition)
    {
        snappedPosition = default;
        var classDefinition = GetNavEditorRepresentativeClassDefinition(GetNavEditorPreviewClass());
        var horizontalMargin = MathF.Max(
            MathF.Abs(classDefinition.CollisionLeft),
            MathF.Abs(classDefinition.CollisionRight)) + 4f;
        var bestDistanceSquared = NavEditorAnchorSnapMaximumDistance * NavEditorAnchorSnapMaximumDistance;

        for (var index = 0; index < _world.Level.Solids.Count; index += 1)
        {
            var solid = _world.Level.Solids[index];
            var minX = solid.Left + horizontalMargin;
            var maxX = solid.Right - horizontalMargin;
            if (minX > maxX)
            {
                continue;
            }

            var candidateX = Math.Clamp(worldX, minX, maxX);
            var candidateY = solid.Top - classDefinition.CollisionBottom;
            if (!CanPlaceNavEditorAnchor(classDefinition, candidateX, candidateY))
            {
                continue;
            }

            var deltaX = candidateX - worldX;
            var deltaY = candidateY - worldY;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            snappedPosition = new Vector2(candidateX, candidateY);
        }

        return bestDistanceSquared < NavEditorAnchorSnapMaximumDistance * NavEditorAnchorSnapMaximumDistance;
    }

    private bool CanPlaceNavEditorAnchor(CharacterClassDefinition classDefinition, float x, float y)
    {
        return CanPlaceNavEditorAnchorWithoutGround(classDefinition, x, y)
            && HasNavEditorAnchorGroundSupport(classDefinition, x, y);
    }

    private bool CanPlaceNavEditorAnchorWithoutGround(CharacterClassDefinition classDefinition, float x, float y)
    {
        var left = x + classDefinition.CollisionLeft;
        var top = y + classDefinition.CollisionTop;
        var right = x + classDefinition.CollisionRight;
        var bottom = y + classDefinition.CollisionBottom;

        if (left < 0f
            || top < 0f
            || right > _world.Level.Bounds.Width
            || bottom > _world.Level.Bounds.Height)
        {
            return false;
        }

        for (var index = 0; index < _world.Level.Solids.Count; index += 1)
        {
            var solid = _world.Level.Solids[index];
            if (left < solid.Right && right > solid.Left && top < solid.Bottom && bottom > solid.Top)
            {
                return false;
            }
        }

        foreach (var wall in _world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (left < wall.Right && right > wall.Left && top < wall.Bottom && bottom > wall.Top)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasNavEditorAnchorGroundSupport(CharacterClassDefinition classDefinition, float x, float y)
    {
        return !CanPlaceNavEditorAnchorWithoutGround(classDefinition, x, y + 1f);
    }

    private bool IsNavEditorAnchorGrounded(NavEditorAnchor anchor)
    {
        var classDefinition = GetNavEditorRepresentativeClassDefinition(GetNavEditorPreviewClass());
        return HasNavEditorAnchorGroundSupport(classDefinition, anchor.X, anchor.Y);
    }

    private bool TryHandleNavEditorPanelClick(MouseState mouse)
    {
        var layout = GetNavEditorPanelLayout();
        if (!layout.Panel.Contains(mouse.Position))
        {
            return false;
        }

        if (layout.CollapseButton.Contains(mouse.Position))
        {
            ToggleNavEditorPanelCollapsed();
            return true;
        }

        if (layout.Header.Contains(mouse.Position))
        {
            BeginNavEditorPanelDrag(mouse);
            return true;
        }

        if (_navEditorPanelCollapsed)
        {
            return true;
        }

        if (_navEditorClearAllConfirmationOpen)
        {
            return TryHandleNavEditorClearAllPromptClick(mouse, layout);
        }

        if (layout.StopTestButton.Contains(mouse.Position))
        {
            StopNavEditorTraversalActivity();
            return true;
        }

        if (IsNavEditorTraversalCaptureActive() || IsNavEditorTraversalPlaybackActive())
        {
            return true;
        }

        if (layout.SelectToolButton.Contains(mouse.Position))
        {
            _navEditorTool = NavEditorTool.Select;
            _navEditorPendingLinkAnchorIndex = -1;
            _navEditorPendingGeneratedGraphSourceNodeId = -1;
            SetNavEditorStatus("tool: select");
            return true;
        }

        if (layout.AnchorToolButton.Contains(mouse.Position))
        {
            _navEditorTool = NavEditorTool.AddAnchor;
            _navEditorPendingLinkAnchorIndex = -1;
            _navEditorPendingGeneratedGraphSourceNodeId = -1;
            SetNavEditorStatus(_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph ? "tool: add graph node" : "tool: add anchor");
            return true;
        }

        if (layout.LinkToolButton.Contains(mouse.Position))
        {
            _navEditorTool = NavEditorTool.AddLink;
            _navEditorPendingLinkAnchorIndex = _navEditorSelectedAnchorIndex;
            _navEditorPendingGeneratedGraphSourceNodeId = _navEditorSelectedGeneratedGraphNodeId;
            SetNavEditorStatus(_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph ? "tool: toggle graph edge" : "tool: add link");
            return true;
        }

        if (layout.SaveButton.Contains(mouse.Position))
        {
            SaveNavEditorState();
            return true;
        }

        if (layout.RebuildButton.Contains(mouse.Position))
        {
            StartNavEditorRebuild();
            return true;
        }

        if (layout.ReloadButton.Contains(mouse.Position))
        {
            ReloadNavEditorState("nav editor reloaded from disk");
            return true;
        }

        if (layout.RenameButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph)
            {
                SetNavEditorStatus("graph nodes use numeric ids; drag them to move, Link toggles edges, Delete removes nodes");
            }
            else
            {
                BeginNavEditorRename();
            }
            return true;
        }

        if (layout.DeleteButton.Contains(mouse.Position))
        {
            DeleteSelectedNavEditorItem();
            return true;
        }

        if (layout.ClearAllButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph)
            {
                SetNavEditorStatus("Clear All only applies to hint routes. Use Reload to discard graph edits or Rebuild to regenerate the graph.");
            }
            else
            {
                BeginNavEditorClearAll();
            }
            return true;
        }

        if (layout.RecordButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                StartNavEditorTraversalCapture();
            }
            return true;
        }

        if (layout.RecordProfileButton.Contains(mouse.Position))
        {
            CycleNavEditorRecordTestClass();
            return true;
        }

        if (layout.ClearRecordingButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                ClearSelectedNavEditorRecordedTraversal();
            }
            return true;
        }

        if (layout.TestLinkButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                StartNavEditorSelectedLinkPlayback();
            }
            return true;
        }

        if (layout.TestRouteButton.Contains(mouse.Position))
        {
            StartNavEditorSelectedRoutePlayback();
            return true;
        }

        if (layout.ViewClassButton.Contains(mouse.Position))
        {
            CycleNavEditorViewClass();
            return true;
        }

        if (layout.ClassScopeButton.Contains(mouse.Position))
        {
            CycleNavEditorDefaultClassScope();
            return true;
        }

        if (layout.LayerButton.Contains(mouse.Position))
        {
            CycleNavEditorLayerMode();
            return true;
        }

        if (layout.RunScoreTrioButton.Contains(mouse.Position))
        {
            RunNavEditorScoreTrioPracticeBots();
            return true;
        }

        if (layout.ClearScoreTrioButton.Contains(mouse.Position))
        {
            StopNavEditorScoreTrioPracticeBots();
            return true;
        }

        if (layout.BotTagsButton.Contains(mouse.Position))
        {
            ToggleNavEditorBotTags();
            return true;
        }

        if (layout.AnchorKindButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                CycleNavEditorDefaultAnchorKind();
            }
            return true;
        }

        if (layout.AnchorTeamButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                CycleNavEditorDefaultAnchorTeam();
            }
            return true;
        }

        if (layout.FallbackRoleButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                CycleNavEditorFallbackRole();
            }
            return true;
        }

        if (layout.FallbackTriggerButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                CycleNavEditorFallbackTrigger();
            }
            return true;
        }

        if (layout.FallbackCarryButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                CycleNavEditorCarryRequirement();
            }
            return true;
        }

        if (layout.TraversalButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                CycleNavEditorDefaultTraversal();
            }
            return true;
        }

        if (layout.DirectionButton.Contains(mouse.Position))
        {
            ToggleNavEditorDefaultBidirectional();
            return true;
        }

        if (layout.EdgeDisplayButton.Contains(mouse.Position))
        {
            CycleNavEditorGraphEdgeDisplayMode();
            return true;
        }

        if (layout.CostDownButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                AdjustNavEditorDefaultCostMultiplier(-NavEditorCostStep);
            }
            return true;
        }

        if (layout.CostUpButton.Contains(mouse.Position))
        {
            if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
            {
                AdjustNavEditorDefaultCostMultiplier(NavEditorCostStep);
            }
            return true;
        }

        return true;
    }

    private void OpenNavEditorContextMenu(MouseState mouse, Vector2 cameraPosition)
    {
        _navEditorPendingLinkAnchorIndex = -1;
        _navEditorRenamingAnchor = false;
        _navEditorRenameBuffer = string.Empty;
        _navEditorContextMenuWorldPosition = mouse.Position.ToVector2() + cameraPosition;
        _navEditorContextMenuPosition = mouse.Position;
        _navEditorContextMenuOpenPath = Array.Empty<int>();
        _navEditorContextMenuAnchorIndex = -1;
        _navEditorContextMenuLinkIndex = -1;
        _navEditorContextMenuGeneratedGraphEdgeFromNodeId = -1;
        _navEditorContextMenuGeneratedGraphEdgeToNodeId = -1;

        var mouseScreenPosition = mouse.Position.ToVector2();
        if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph
            && TryPickNavEditorGeneratedGraphNode(mouseScreenPosition, cameraPosition, out var graphNodeId))
        {
            SelectNavEditorGeneratedGraphNode(graphNodeId);
            _navEditorContextMenuTargetKind = NavEditorContextMenuTargetKind.GeneratedGraphNode;
        }
        else if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph
            && TryPickNavEditorGeneratedGraphEdge(mouseScreenPosition, cameraPosition, out var graphEdgeFromNodeId, out var graphEdgeToNodeId))
        {
            SelectNavEditorGeneratedGraphEdge(graphEdgeFromNodeId, graphEdgeToNodeId);
            _navEditorContextMenuTargetKind = NavEditorContextMenuTargetKind.GeneratedGraphEdge;
            _navEditorContextMenuGeneratedGraphEdgeFromNodeId = graphEdgeFromNodeId;
            _navEditorContextMenuGeneratedGraphEdgeToNodeId = graphEdgeToNodeId;
        }
        else if (TryPickNavEditorAnchor(mouseScreenPosition, cameraPosition, out var anchorIndex))
        {
            SelectNavEditorAnchor(anchorIndex);
            _navEditorContextMenuTargetKind = NavEditorContextMenuTargetKind.Anchor;
            _navEditorContextMenuAnchorIndex = anchorIndex;
        }
        else if (TryPickNavEditorLink(mouseScreenPosition, cameraPosition, out var linkIndex))
        {
            SelectNavEditorLink(linkIndex);
            _navEditorContextMenuTargetKind = NavEditorContextMenuTargetKind.Link;
            _navEditorContextMenuLinkIndex = linkIndex;
        }
        else if (_navEditorLayerMode == NavEditorLayerMode.Both
            && TryPickNavEditorGeneratedGraphNode(mouseScreenPosition, cameraPosition, out graphNodeId))
        {
            SelectNavEditorGeneratedGraphNode(graphNodeId);
            _navEditorContextMenuTargetKind = NavEditorContextMenuTargetKind.GeneratedGraphNode;
        }
        else if (_navEditorLayerMode == NavEditorLayerMode.Both
            && TryPickNavEditorGeneratedGraphEdge(mouseScreenPosition, cameraPosition, out var bothGraphEdgeFromNodeId, out var bothGraphEdgeToNodeId))
        {
            SelectNavEditorGeneratedGraphEdge(bothGraphEdgeFromNodeId, bothGraphEdgeToNodeId);
            _navEditorContextMenuTargetKind = NavEditorContextMenuTargetKind.GeneratedGraphEdge;
            _navEditorContextMenuGeneratedGraphEdgeFromNodeId = bothGraphEdgeFromNodeId;
            _navEditorContextMenuGeneratedGraphEdgeToNodeId = bothGraphEdgeToNodeId;
        }
        else
        {
            ClearNavEditorSelection();
            _navEditorContextMenuTargetKind = NavEditorContextMenuTargetKind.Empty;
        }

        _navEditorContextMenuOpen = true;
        UpdateNavEditorContextMenuHover(mouse.Position);
    }

    private void CloseNavEditorContextMenu()
    {
        _navEditorContextMenuOpen = false;
        _navEditorContextMenuOpenPath = Array.Empty<int>();
        _navEditorContextMenuAnchorIndex = -1;
        _navEditorContextMenuLinkIndex = -1;
        _navEditorContextMenuGeneratedGraphEdgeFromNodeId = -1;
        _navEditorContextMenuGeneratedGraphEdgeToNodeId = -1;
    }

    private void UpdateNavEditorContextMenuHover(Point mousePosition)
    {
        if (!_navEditorContextMenuOpen)
        {
            return;
        }

        var items = BuildNavEditorContextMenuItems();
        if (items.Count == 0 || !TryHitNavEditorContextMenu(items, mousePosition, out var hit))
        {
            return;
        }

        if (hit.Item.Children.Count > 0 && hit.Item.Enabled)
        {
            _navEditorContextMenuOpenPath = hit.Path;
            return;
        }

        _navEditorContextMenuOpenPath = hit.Path.Length > 1
            ? hit.Path[..^1]
            : Array.Empty<int>();
    }

    private bool TryHandleNavEditorContextMenuClick(Point mousePosition)
    {
        if (!_navEditorContextMenuOpen)
        {
            return false;
        }

        var items = BuildNavEditorContextMenuItems();
        if (!TryHitNavEditorContextMenu(items, mousePosition, out var hit))
        {
            return false;
        }

        if (!hit.Item.Enabled)
        {
            return true;
        }

        if (hit.Item.Children.Count > 0)
        {
            _navEditorContextMenuOpenPath = hit.Path;
            return true;
        }

        var action = hit.Item.Action;
        CloseNavEditorContextMenu();
        action?.Invoke();
        return true;
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorContextMenuItems()
    {
        return _navEditorContextMenuTargetKind switch
        {
            NavEditorContextMenuTargetKind.Anchor => BuildNavEditorAnchorContextMenuItems(),
            NavEditorContextMenuTargetKind.Link => BuildNavEditorLinkContextMenuItems(),
            NavEditorContextMenuTargetKind.GeneratedGraphNode => BuildNavEditorGeneratedGraphNodeContextMenuItems(),
            NavEditorContextMenuTargetKind.GeneratedGraphEdge => BuildNavEditorGeneratedGraphEdgeContextMenuItems(),
            _ => BuildNavEditorEmptyContextMenuItems(),
        };
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorEmptyContextMenuItems()
    {
        if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph)
        {
            return
            [
                new NavEditorContextMenuItem
                {
                    Label = "Place Graph Node",
                    Action = () =>
                    {
                        if (!TryResolveNavEditorAnchorPlacement(_navEditorContextMenuWorldPosition.X, _navEditorContextMenuWorldPosition.Y, out var snappedPosition))
                        {
                            SetNavEditorStatus("graph node placement needs open space or nearby valid ground");
                            return;
                        }

                        AddNavEditorGeneratedGraphNode(snappedPosition);
                    },
                },
                new NavEditorContextMenuItem
                {
                    Label = "Switch To Hints",
                    Action = () =>
                    {
                        _navEditorLayerMode = NavEditorLayerMode.HintLayer;
                        SetNavEditorStatus("layer: Hints");
                    },
                },
            ];
        }

        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Place Anchor",
                Children = BuildNavEditorAnchorKindMenuItems(
                    PlaceNavEditorContextAnchor,
                    _navEditorDefaultAnchorKind,
                    NormalizeNavEditorAnchorTeam(_navEditorDefaultAnchorKind, _navEditorDefaultAnchorTeam)),
            },
        ];
    }

    private NavEditorContextMenuItem[] BuildNavEditorAnchorContextMenuItems()
    {
        if (_navEditorContextMenuAnchorIndex < 0 || _navEditorContextMenuAnchorIndex >= _navEditorAnchors.Count)
        {
            return Array.Empty<NavEditorContextMenuItem>();
        }

        var anchor = _navEditorAnchors[_navEditorContextMenuAnchorIndex];
        var canTest = !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive();
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Role",
                Children = BuildNavEditorAnchorKindMenuItems(
                    SetNavEditorAnchorKind,
                    anchor.Kind,
                    anchor.Team),
            },
            new NavEditorContextMenuItem
            {
                Label = "Profile",
                Children = BuildNavEditorClassScopeMenuItems(anchor.Classes),
            },
            new NavEditorContextMenuItem
            {
                Label = "Fallback",
                Children = BuildNavEditorFallbackMenuItems(anchor),
            },
            new NavEditorContextMenuItem
            {
                Label = "Rename",
                Action = BeginNavEditorRename,
            },
            new NavEditorContextMenuItem
            {
                Label = "Test Route",
                Enabled = canTest,
                Action = StartNavEditorSelectedRoutePlayback,
            },
            new NavEditorContextMenuItem
            {
                Label = "Delete",
                Action = DeleteSelectedNavEditorItem,
            },
        ];
    }

    private NavEditorContextMenuItem[] BuildNavEditorLinkContextMenuItems()
    {
        if (_navEditorContextMenuLinkIndex < 0 || _navEditorContextMenuLinkIndex >= _navEditorLinks.Count)
        {
            return Array.Empty<NavEditorContextMenuItem>();
        }

        var link = _navEditorLinks[_navEditorContextMenuLinkIndex];
        var canRunTraversal = !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive();
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Traversal",
                Children = BuildNavEditorTraversalMenuItems(link.Traversal),
            },
            new NavEditorContextMenuItem
            {
                Label = "Jump Timing",
                Enabled = link.Traversal == BotNavigationHintTraversalKind.Jump,
                Children = BuildNavEditorJumpTimingMenuItems(link.StartJumpImmediately),
            },
            new NavEditorContextMenuItem
            {
                Label = "Direction",
                Children = BuildNavEditorDirectionMenuItems(link.Bidirectional),
            },
            new NavEditorContextMenuItem
            {
                Label = "Profile",
                Children = BuildNavEditorClassScopeMenuItems(link.Classes),
            },
            new NavEditorContextMenuItem
            {
                Label = "Test Class",
                Children = BuildNavEditorRecordTestClassMenuItems(),
            },
            new NavEditorContextMenuItem
            {
                Label = "Rec. Traversal",
                Enabled = canRunTraversal && link.AppliesToClass(_navEditorRecordTestClass),
                Action = StartNavEditorTraversalCapture,
            },
            new NavEditorContextMenuItem
            {
                Label = "Clear Rec",
                Enabled = canRunTraversal && SelectedNavEditorLinkHasRecordingForClass(_navEditorRecordTestClass),
                Action = ClearSelectedNavEditorRecordedTraversal,
            },
            new NavEditorContextMenuItem
            {
                Label = "Test Link",
                Enabled = canRunTraversal && link.AppliesToClass(_navEditorRecordTestClass),
                Action = StartNavEditorSelectedLinkPlayback,
            },
            new NavEditorContextMenuItem
            {
                Label = "Test Route",
                Enabled = canRunTraversal,
                Action = StartNavEditorSelectedRoutePlayback,
            },
            new NavEditorContextMenuItem
            {
                Label = "Cost",
                Children = BuildNavEditorCostMenuItems(link.CostMultiplier),
            },
            new NavEditorContextMenuItem
            {
                Label = "Delete",
                Action = DeleteSelectedNavEditorItem,
            },
        ];
    }

    private NavEditorContextMenuItem[] BuildNavEditorGeneratedGraphNodeContextMenuItems()
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset
            || !TryGetNavEditorGeneratedGraphNode(_navEditorSelectedGeneratedGraphNodeId, out var node))
        {
            return Array.Empty<NavEditorContextMenuItem>();
        }

        var hasStart = _navEditorGraphRouteStartNodeId >= 0;
        var hasGoal = _navEditorGraphRouteGoalNodeId >= 0;
        var hasPendingEdgeStart = _navEditorPendingGeneratedGraphSourceNodeId >= 0;
        return
        [
            new NavEditorContextMenuItem
            {
                Label = $"Node {node.Id} ({node.X:F0}, {node.Y:F0})",
                Enabled = false,
            },
            new NavEditorContextMenuItem
            {
                Label = "Set Edge Start",
                Active = _navEditorPendingGeneratedGraphSourceNodeId == node.Id,
                Action = () =>
                {
                    _navEditorPendingGeneratedGraphSourceNodeId = node.Id;
                    SelectNavEditorGeneratedGraphNode(node.Id);
                    SetNavEditorStatus($"graph edge start: node {node.Id}");
                },
            },
            new NavEditorContextMenuItem
            {
                Label = "Toggle Start -> Here",
                Enabled = hasPendingEdgeStart && _navEditorPendingGeneratedGraphSourceNodeId != node.Id,
                Action = () => ToggleNavEditorGeneratedGraphDirectedEdge(_navEditorPendingGeneratedGraphSourceNodeId, node.Id),
            },
            new NavEditorContextMenuItem
            {
                Label = "Toggle Start <-> Here",
                Enabled = hasPendingEdgeStart && _navEditorPendingGeneratedGraphSourceNodeId != node.Id,
                Action = () => ToggleNavEditorGeneratedGraphBidirectionalEdge(_navEditorPendingGeneratedGraphSourceNodeId, node.Id),
            },
            new NavEditorContextMenuItem
            {
                Label = "Set Graph Start",
                Active = _navEditorGraphRouteStartNodeId == node.Id,
                Action = () => SetNavEditorGeneratedGraphRouteStart(node.Id),
            },
            new NavEditorContextMenuItem
            {
                Label = "Set Graph Goal",
                Active = _navEditorGraphRouteGoalNodeId == node.Id,
                Action = () => SetNavEditorGeneratedGraphRouteGoal(node.Id),
            },
            new NavEditorContextMenuItem
            {
                Label = "Run Start -> Here",
                Enabled = hasStart && _navEditorGraphRouteStartNodeId != node.Id,
                Action = () =>
                {
                    SetNavEditorGeneratedGraphRouteGoal(node.Id);
                    StartNavEditorGeneratedGraphRoutePlayback();
                },
            },
            new NavEditorContextMenuItem
            {
                Label = "Run Here -> Goal",
                Enabled = hasGoal && _navEditorGraphRouteGoalNodeId != node.Id,
                Action = () =>
                {
                    SetNavEditorGeneratedGraphRouteStart(node.Id);
                    StartNavEditorGeneratedGraphRoutePlayback();
                },
            },
            new NavEditorContextMenuItem
            {
                Label = "Run Graph Route",
                Enabled = hasStart && hasGoal && _navEditorGraphRouteStartNodeId != _navEditorGraphRouteGoalNodeId && asset.Nodes.Count > 0,
                Action = StartNavEditorGeneratedGraphRoutePlayback,
            },
            new NavEditorContextMenuItem
            {
                Label = "Clear Graph Route",
                Enabled = hasStart || hasGoal,
                Action = ClearNavEditorGeneratedGraphSelection,
            },
            new NavEditorContextMenuItem
            {
                Label = "Delete Node",
                Action = DeleteSelectedNavEditorItem,
            },
        ];
    }

    private NavEditorContextMenuItem[] BuildNavEditorGeneratedGraphEdgeContextMenuItems()
    {
        if (!TryGetNavEditorGeneratedGraphEdge(
                _navEditorContextMenuGeneratedGraphEdgeFromNodeId,
                _navEditorContextMenuGeneratedGraphEdgeToNodeId,
                out var edge))
        {
            return Array.Empty<NavEditorContextMenuItem>();
        }

        var hasReverse = TryGetNavEditorGeneratedGraphEdge(edge.ToNodeId, edge.FromNodeId, out _);
        return
        [
            new NavEditorContextMenuItem
            {
                Label = $"Edge {edge.FromNodeId} -> {edge.ToNodeId}",
                Enabled = false,
            },
            new NavEditorContextMenuItem
            {
                Label = $"{edge.Kind} cost {edge.Cost:0.##}",
                Enabled = false,
            },
            new NavEditorContextMenuItem
            {
                Label = "Set Edge Start",
                Action = () =>
                {
                    _navEditorPendingGeneratedGraphSourceNodeId = edge.FromNodeId;
                    SelectNavEditorGeneratedGraphNode(edge.FromNodeId);
                    SetNavEditorStatus($"graph edge start: node {edge.FromNodeId}");
                },
            },
            new NavEditorContextMenuItem
            {
                Label = "Toggle Reverse",
                Active = hasReverse,
                Action = () => ToggleNavEditorGeneratedGraphDirectedEdge(edge.ToNodeId, edge.FromNodeId),
            },
            new NavEditorContextMenuItem
            {
                Label = "Toggle Two-Way",
                Active = hasReverse,
                Action = () => ToggleNavEditorGeneratedGraphBidirectionalEdge(edge.FromNodeId, edge.ToNodeId),
            },
            new NavEditorContextMenuItem
            {
                Label = "Select From Node",
                Action = () => SelectNavEditorGeneratedGraphNode(edge.FromNodeId),
            },
            new NavEditorContextMenuItem
            {
                Label = "Select To Node",
                Action = () => SelectNavEditorGeneratedGraphNode(edge.ToNodeId),
            },
            new NavEditorContextMenuItem
            {
                Label = "Delete Edge",
                Action = DeleteSelectedNavEditorItem,
            },
        ];
    }

    private static IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorAnchorKindMenuItems(
        Action<BotNavigationNodeKind, PlayerTeam?> applySelection,
        BotNavigationNodeKind currentKind,
        PlayerTeam? currentTeam)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Route",
                Active = currentKind == BotNavigationNodeKind.RouteAnchor,
                Action = () => applySelection(BotNavigationNodeKind.RouteAnchor, null),
            },
            new NavEditorContextMenuItem
            {
                Label = "Spawn",
                Active = currentKind == BotNavigationNodeKind.Spawn,
                Children = BuildNavEditorAnchorTeamSelectionMenuItems(
                    team => applySelection(BotNavigationNodeKind.Spawn, team),
                    currentKind == BotNavigationNodeKind.Spawn ? currentTeam : null),
            },
            new NavEditorContextMenuItem
            {
                Label = "Objective",
                Active = currentKind == BotNavigationNodeKind.Objective,
                Children = BuildNavEditorAnchorTeamSelectionMenuItems(
                    team => applySelection(BotNavigationNodeKind.Objective, team),
                    currentKind == BotNavigationNodeKind.Objective ? currentTeam : null),
            },
            new NavEditorContextMenuItem
            {
                Label = "Cabinet",
                Active = currentKind == BotNavigationNodeKind.HealingCabinet,
                Children = BuildNavEditorAnchorTeamSelectionMenuItems(
                    team => applySelection(BotNavigationNodeKind.HealingCabinet, team),
                    currentKind == BotNavigationNodeKind.HealingCabinet ? currentTeam : null),
            },
        ];
    }

    private static IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorAnchorTeamSelectionMenuItems(
        Action<PlayerTeam?> applySelection,
        PlayerTeam? currentTeam)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Red",
                Active = currentTeam == PlayerTeam.Red,
                Action = () => applySelection(PlayerTeam.Red),
            },
            new NavEditorContextMenuItem
            {
                Label = "Blue",
                Active = currentTeam == PlayerTeam.Blue,
                Action = () => applySelection(PlayerTeam.Blue),
            },
            new NavEditorContextMenuItem
            {
                Label = "Unassigned",
                Active = currentTeam is null,
                Action = () => applySelection(null),
            },
        ];
    }

    private List<NavEditorContextMenuItem> BuildNavEditorClassScopeMenuItems(PlayerClass[] currentClasses)
    {
        var items = new List<NavEditorContextMenuItem>
        {
            new()
            {
                Label = "All",
                Active = currentClasses.Length == 0,
                Action = () => SetNavEditorClassScope(Array.Empty<PlayerClass>()),
            },
        };

        foreach (var classId in BotNavigationClasses.All)
        {
            items.Add(new NavEditorContextMenuItem
            {
                Label = BotNavigationClasses.GetDisplayName(classId),
                Active = currentClasses.Length == 1 && currentClasses[0] == classId,
                Action = () => SetNavEditorClassScope([classId]),
            });
        }

        return items;
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorTraversalMenuItems(BotNavigationHintTraversalKind currentTraversal)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Auto",
                Active = currentTraversal == BotNavigationHintTraversalKind.Auto,
                Action = () => SetNavEditorTraversal(BotNavigationHintTraversalKind.Auto),
            },
            new NavEditorContextMenuItem
            {
                Label = "Walk",
                Active = currentTraversal == BotNavigationHintTraversalKind.Walk,
                Action = () => SetNavEditorTraversal(BotNavigationHintTraversalKind.Walk),
            },
            new NavEditorContextMenuItem
            {
                Label = "Jump",
                Active = currentTraversal == BotNavigationHintTraversalKind.Jump,
                Action = () => SetNavEditorTraversal(BotNavigationHintTraversalKind.Jump),
            },
            new NavEditorContextMenuItem
            {
                Label = "Drop",
                Active = currentTraversal == BotNavigationHintTraversalKind.Drop,
                Action = () => SetNavEditorTraversal(BotNavigationHintTraversalKind.Drop),
            },
        ];
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorJumpTimingMenuItems(bool startJumpImmediately)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Delayed",
                Active = !startJumpImmediately,
                Action = () => SetNavEditorStartJumpImmediately(false),
            },
            new NavEditorContextMenuItem
            {
                Label = "Immediate",
                Active = startJumpImmediately,
                Action = () => SetNavEditorStartJumpImmediately(true),
            },
        ];
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorDirectionMenuItems(bool bidirectional)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Two-way",
                Active = bidirectional,
                Action = () => SetNavEditorBidirectional(true),
            },
            new NavEditorContextMenuItem
            {
                Label = "One-way",
                Active = !bidirectional,
                Action = () => SetNavEditorBidirectional(false),
            },
        ];
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorFallbackMenuItems(NavEditorAnchor anchor)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Off",
                Active = anchor.FallbackRole == BotNavigationHintFallbackRole.None,
                Action = () => SetNavEditorFallbackRole(BotNavigationHintFallbackRole.None),
            },
            new NavEditorContextMenuItem
            {
                Label = "Entry",
                Active = anchor.FallbackRole == BotNavigationHintFallbackRole.Entry,
                Children = BuildNavEditorFallbackTriggerMenuItems(anchor.FallbackTriggers),
            },
            new NavEditorContextMenuItem
            {
                Label = "Exit",
                Active = anchor.FallbackRole == BotNavigationHintFallbackRole.Exit,
                Action = () => SetNavEditorFallbackRole(BotNavigationHintFallbackRole.Exit),
            },
            new NavEditorContextMenuItem
            {
                Label = "Carry",
                Children = BuildNavEditorFallbackCarryMenuItems(anchor.RequiresCarryingIntel),
            },
            new NavEditorContextMenuItem
            {
                Label = "Clear Zone",
                Enabled = anchor.HasFallbackActivationZone,
                Action = ClearSelectedNavEditorActivationZone,
            },
        ];
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorFallbackTriggerMenuItems(BotNavigationHintFallbackTrigger[] currentTriggers)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "No Next",
                Active = currentTriggers.Length == 0 || currentTriggers.Contains(BotNavigationHintFallbackTrigger.NoNext),
                Action = () => SetNavEditorFallbackTrigger(BotNavigationHintFallbackTrigger.NoNext),
            },
            new NavEditorContextMenuItem
            {
                Label = "Stuck",
                Active = currentTriggers.Contains(BotNavigationHintFallbackTrigger.Stuck),
                Action = () => SetNavEditorFallbackTrigger(BotNavigationHintFallbackTrigger.Stuck),
            },
            new NavEditorContextMenuItem
            {
                Label = "Reacquire",
                Active = currentTriggers.Contains(BotNavigationHintFallbackTrigger.Reacquire),
                Action = () => SetNavEditorFallbackTrigger(BotNavigationHintFallbackTrigger.Reacquire),
            },
            new NavEditorContextMenuItem
            {
                Label = "Carry Return",
                Active = currentTriggers.Contains(BotNavigationHintFallbackTrigger.CarryReturn),
                Action = () => SetNavEditorFallbackTrigger(BotNavigationHintFallbackTrigger.CarryReturn),
            },
            new NavEditorContextMenuItem
            {
                Label = "Direct Target",
                Active = currentTriggers.Contains(BotNavigationHintFallbackTrigger.DirectTarget),
                Action = () => SetNavEditorFallbackTrigger(BotNavigationHintFallbackTrigger.DirectTarget),
            },
        ];
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorFallbackCarryMenuItems(bool? requiresCarryingIntel)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Any",
                Active = requiresCarryingIntel is null,
                Action = () => SetNavEditorCarryRequirement(null),
            },
            new NavEditorContextMenuItem
            {
                Label = "Must Carry",
                Active = requiresCarryingIntel == true,
                Action = () => SetNavEditorCarryRequirement(true),
            },
            new NavEditorContextMenuItem
            {
                Label = "No Carry",
                Active = requiresCarryingIntel == false,
                Action = () => SetNavEditorCarryRequirement(false),
            },
        ];
    }

    private List<NavEditorContextMenuItem> BuildNavEditorRecordTestClassMenuItems()
    {
        var items = new List<NavEditorContextMenuItem>(BotNavigationClasses.All.Count);
        foreach (var classId in BotNavigationClasses.All)
        {
            items.Add(new NavEditorContextMenuItem
            {
                Label = BotNavigationClasses.GetDisplayName(classId),
                Active = _navEditorRecordTestClass == classId,
                Action = () => SetNavEditorRecordTestClass(classId),
            });
        }

        return items;
    }

    private IReadOnlyList<NavEditorContextMenuItem> BuildNavEditorCostMenuItems(float currentCost)
    {
        return
        [
            new NavEditorContextMenuItem
            {
                Label = "Decrease (-0.1)",
                Action = () => SetNavEditorCostMultiplier(currentCost - NavEditorCostStep),
            },
            new NavEditorContextMenuItem
            {
                Label = "Increase (+0.1)",
                Action = () => SetNavEditorCostMultiplier(currentCost + NavEditorCostStep),
            },
            new NavEditorContextMenuItem
            {
                Label = "Reset (x1.0)",
                Active = MathF.Abs(currentCost - 1f) < 0.001f,
                Action = () => SetNavEditorCostMultiplier(1f),
            },
        ];
    }

    private void PlaceNavEditorContextAnchor(BotNavigationNodeKind kind, PlayerTeam? team)
    {
        _navEditorDefaultAnchorKind = kind;
        _navEditorDefaultAnchorTeam = NormalizeNavEditorAnchorTeam(kind, team);
        if (!TryResolveNavEditorAnchorPlacement(_navEditorContextMenuWorldPosition.X, _navEditorContextMenuWorldPosition.Y, out var snappedPosition))
        {
            SetNavEditorStatus("anchor placement needs open space or nearby valid ground");
            return;
        }

        AddNavEditorAnchor(
            snappedPosition,
            kind,
            team,
            _navEditorDefaultClasses,
            beginRename: false);
    }

    private void SetNavEditorAnchorKind(BotNavigationNodeKind kind, PlayerTeam? team)
    {
        var normalizedTeam = NormalizeNavEditorAnchorTeam(kind, team);
        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
            anchor.Kind = kind;
            anchor.Team = normalizedTeam;
            RefreshNavEditorAnchorAutoLabel(anchor);
            _navEditorDirty = true;
            SetNavEditorStatus($"anchor role: {DescribeNavEditorAnchorRole(kind, normalizedTeam)}");
            return;
        }

        if (_navEditorSelectedLinkIndex >= 0)
        {
            SetNavEditorStatus("anchor role only applies to anchors or new anchor defaults");
            return;
        }

        _navEditorDefaultAnchorKind = kind;
        _navEditorDefaultAnchorTeam = normalizedTeam;
        SetNavEditorStatus($"new anchors use role: {DescribeNavEditorAnchorRole(kind, normalizedTeam)}");
    }

    private void SetNavEditorClassScope(PlayerClass[] classes)
    {
        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
            anchor.Classes = classes.ToArray();
            _navEditorDirty = true;
            if (!AppliesToNavEditorViewClass(anchor.Classes))
            {
                ClearNavEditorSelection();
            }

            SetNavEditorStatus($"anchor profile: {DescribeNavEditorClasses(classes)}");
            return;
        }

        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.Classes = classes.ToArray();
            _navEditorDirty = true;
            if (!AppliesToNavEditorViewClass(link.Classes))
            {
                ClearNavEditorSelection();
            }

            SetNavEditorStatus($"link profile: {DescribeNavEditorClasses(classes)}");
            return;
        }

        _navEditorDefaultClasses = classes.ToArray();
        SetNavEditorStatus($"new items apply to: {DescribeNavEditorClasses(classes)}");
    }

    private void SetNavEditorTraversal(BotNavigationHintTraversalKind traversal)
    {
        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.Traversal = traversal;
            if (traversal != BotNavigationHintTraversalKind.Jump)
            {
                link.StartJumpImmediately = false;
            }

            _navEditorDirty = true;
            SetNavEditorStatus($"link traversal: {traversal}");
            return;
        }

        _navEditorDefaultTraversal = traversal;
        SetNavEditorStatus($"new links use traversal: {traversal}");
    }

    private void SetNavEditorStartJumpImmediately(bool startJumpImmediately)
    {
        if (_navEditorSelectedLinkIndex < 0 || _navEditorSelectedLinkIndex >= _navEditorLinks.Count)
        {
            return;
        }

        var link = _navEditorLinks[_navEditorSelectedLinkIndex];
        if (link.Traversal != BotNavigationHintTraversalKind.Jump)
        {
            SetNavEditorStatus("jump timing only applies to Jump links");
            return;
        }

        link.StartJumpImmediately = startJumpImmediately;
        _navEditorDirty = true;
        SetNavEditorStatus(startJumpImmediately ? "link jump timing: immediate" : "link jump timing: delayed");
    }

    private void SetNavEditorBidirectional(bool bidirectional)
    {
        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.Bidirectional = bidirectional;
            _navEditorDirty = true;
            SetNavEditorStatus(bidirectional ? "link is now bidirectional" : "link is now one-way");
            return;
        }

        _navEditorDefaultBidirectional = bidirectional;
        SetNavEditorStatus(bidirectional ? "new links are bidirectional" : "new links are one-way");
    }

    private void SetNavEditorCostMultiplier(float value)
    {
        value = ClampNavEditorCostMultiplier(value);
        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.CostMultiplier = value;
            _navEditorDirty = true;
            SetNavEditorStatus($"link cost multiplier: {value:F1}");
            return;
        }

        _navEditorDefaultCostMultiplier = value;
        SetNavEditorStatus($"new link cost multiplier: {value:F1}");
    }

    private void SetNavEditorFallbackRole(BotNavigationHintFallbackRole role)
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        anchor.FallbackRole = role;
        if (role == BotNavigationHintFallbackRole.Entry && anchor.FallbackTriggers.Length == 0)
        {
            anchor.FallbackTriggers = [BotNavigationHintFallbackTrigger.NoNext];
        }

        _navEditorDirty = true;
        SetNavEditorStatus($"fallback role: {DescribeNavEditorFallbackRole(role)}");
    }

    private void SetNavEditorFallbackTrigger(BotNavigationHintFallbackTrigger trigger)
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        anchor.FallbackRole = BotNavigationHintFallbackRole.Entry;
        anchor.FallbackTriggers = [trigger];
        _navEditorDirty = true;
        SetNavEditorStatus($"fallback trigger: {DescribeNavEditorFallbackTriggers(anchor.FallbackTriggers)}");
    }

    private void SetNavEditorCarryRequirement(bool? requiresCarryingIntel)
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        anchor.RequiresCarryingIntel = requiresCarryingIntel;
        _navEditorDirty = true;
        SetNavEditorStatus($"carry requirement: {DescribeNavEditorCarryRequirement(requiresCarryingIntel)}");
    }

    private void ClearSelectedNavEditorActivationZone()
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            SetNavEditorStatus("select a fallback entry before clearing its activation zone");
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        if (!anchor.HasFallbackActivationZone)
        {
            SetNavEditorStatus($"{anchor.Label} uses the default circular activation radius");
            return;
        }

        anchor.HasFallbackActivationZone = false;
        anchor.FallbackActivationMinX = 0f;
        anchor.FallbackActivationMinY = 0f;
        anchor.FallbackActivationMaxX = 0f;
        anchor.FallbackActivationMaxY = 0f;
        _navEditorDirty = true;
        SetNavEditorStatus($"cleared activation zone for {anchor.Label}");
    }

    private void SetNavEditorRecordTestClass(PlayerClass classId)
    {
        _navEditorRecordTestClass = classId;
        SetNavEditorStatus($"test class: {BotNavigationClasses.GetShortLabel(classId)}");
    }

    private void DrawNavEditorContextMenu(MouseState mouse)
    {
        if (!_navEditorContextMenuOpen)
        {
            return;
        }

        var items = BuildNavEditorContextMenuItems();
        if (items.Count == 0)
        {
            return;
        }

        var panels = BuildNavEditorContextMenuPanels(items);
        for (var panelIndex = 0; panelIndex < panels.Count; panelIndex += 1)
        {
            var panel = panels[panelIndex];
            _spriteBatch.Draw(_pixel, panel.Bounds, new Color(20, 22, 28, 236));
            _spriteBatch.Draw(_pixel, new Rectangle(panel.Bounds.X, panel.Bounds.Y, panel.Bounds.Width, 1), new Color(92, 196, 255, 220));
            _spriteBatch.Draw(_pixel, new Rectangle(panel.Bounds.X, panel.Bounds.Bottom - 1, panel.Bounds.Width, 1), new Color(8, 10, 14, 255));
            _spriteBatch.Draw(_pixel, new Rectangle(panel.Bounds.X, panel.Bounds.Y, 1, panel.Bounds.Height), new Color(8, 10, 14, 255));
            _spriteBatch.Draw(_pixel, new Rectangle(panel.Bounds.Right - 1, panel.Bounds.Y, 1, panel.Bounds.Height), new Color(8, 10, 14, 255));

            for (var itemIndex = 0; itemIndex < panel.Items.Count; itemIndex += 1)
            {
                var item = panel.Items[itemIndex];
                var itemBounds = GetNavEditorContextMenuItemBounds(panel.Bounds, itemIndex);
                var itemPath = CreateNavEditorContextMenuPath(panel.PrefixPath, itemIndex);
                var hovered = itemBounds.Contains(mouse.Position);
                var open = item.Children.Count > 0 && PathsEqual(_navEditorContextMenuOpenPath, itemPath);
                var background = !item.Enabled
                    ? new Color(42, 46, 52, 210)
                    : hovered || open
                        ? new Color(92, 196, 255, 220)
                        : item.Active
                            ? new Color(52, 78, 98, 220)
                            : new Color(32, 36, 42, 220);
                var textColor = !item.Enabled
                    ? new Color(124, 130, 138)
                    : hovered || open
                        ? new Color(12, 16, 20)
                        : new Color(236, 238, 242);
                _spriteBatch.Draw(_pixel, itemBounds, background);
                _spriteBatch.DrawString(_consoleFont, item.Label, new Vector2(itemBounds.X + 8f, itemBounds.Y + 4f), textColor);
                if (item.Children.Count > 0)
                {
                    _spriteBatch.DrawString(_consoleFont, ">", new Vector2(itemBounds.Right - 14f, itemBounds.Y + 4f), textColor);
                }
            }
        }
    }

    private List<NavEditorContextMenuPanel> BuildNavEditorContextMenuPanels(IReadOnlyList<NavEditorContextMenuItem> rootItems)
    {
        var panels = new List<NavEditorContextMenuPanel>();
        if (rootItems.Count == 0)
        {
            return panels;
        }

        var viewportBounds = new Rectangle(0, 0, ViewportWidth, ViewportHeight);
        var rootBounds = ClampNavEditorContextMenuBounds(new Rectangle(
            _navEditorContextMenuPosition.X,
            _navEditorContextMenuPosition.Y,
            NavEditorContextMenuWidth,
            rootItems.Count * NavEditorContextMenuItemHeight), viewportBounds);
        panels.Add(new NavEditorContextMenuPanel(rootBounds, rootItems, Array.Empty<int>()));

        var currentItems = rootItems;
        var currentBounds = rootBounds;
        for (var depth = 0; depth < _navEditorContextMenuOpenPath.Length; depth += 1)
        {
            var itemIndex = _navEditorContextMenuOpenPath[depth];
            if (itemIndex < 0 || itemIndex >= currentItems.Count)
            {
                break;
            }

            var item = currentItems[itemIndex];
            if (item.Children.Count == 0)
            {
                break;
            }

            var parentItemBounds = GetNavEditorContextMenuItemBounds(currentBounds, itemIndex);
            var submenuBounds = GetNavEditorContextMenuSubmenuBounds(parentItemBounds, item.Children.Count, viewportBounds);
            var prefixPath = _navEditorContextMenuOpenPath[..(depth + 1)];
            panels.Add(new NavEditorContextMenuPanel(submenuBounds, item.Children, prefixPath));
            currentItems = item.Children;
            currentBounds = submenuBounds;
        }

        return panels;
    }

    private static Rectangle GetNavEditorContextMenuItemBounds(Rectangle panelBounds, int itemIndex)
    {
        return new Rectangle(
            panelBounds.X,
            panelBounds.Y + (itemIndex * NavEditorContextMenuItemHeight),
            panelBounds.Width,
            NavEditorContextMenuItemHeight);
    }

    private static Rectangle ClampNavEditorContextMenuBounds(Rectangle bounds, Rectangle viewportBounds)
    {
        var maxX = Math.Max(viewportBounds.X, viewportBounds.Right - bounds.Width);
        var maxY = Math.Max(viewportBounds.Y, viewportBounds.Bottom - bounds.Height);
        return new Rectangle(
            Math.Clamp(bounds.X, viewportBounds.X, maxX),
            Math.Clamp(bounds.Y, viewportBounds.Y, maxY),
            bounds.Width,
            bounds.Height);
    }

    private static Rectangle GetNavEditorContextMenuSubmenuBounds(Rectangle parentItemBounds, int itemCount, Rectangle viewportBounds)
    {
        var height = Math.Max(1, itemCount) * NavEditorContextMenuItemHeight;
        var x = parentItemBounds.Right + NavEditorContextMenuPanelGap;
        if (x + NavEditorContextMenuWidth > viewportBounds.Right)
        {
            x = parentItemBounds.Left - NavEditorContextMenuPanelGap - NavEditorContextMenuWidth;
        }

        var y = Math.Clamp(
            parentItemBounds.Y,
            viewportBounds.Y,
            Math.Max(viewportBounds.Y, viewportBounds.Bottom - height));
        return ClampNavEditorContextMenuBounds(
            new Rectangle(x, y, NavEditorContextMenuWidth, height),
            viewportBounds);
    }

    private bool TryHitNavEditorContextMenu(IReadOnlyList<NavEditorContextMenuItem> rootItems, Point mousePosition, out NavEditorContextMenuHit hit)
    {
        var panels = BuildNavEditorContextMenuPanels(rootItems);
        for (var panelIndex = panels.Count - 1; panelIndex >= 0; panelIndex -= 1)
        {
            var panel = panels[panelIndex];
            if (!panel.Bounds.Contains(mousePosition))
            {
                continue;
            }

            var itemIndex = (mousePosition.Y - panel.Bounds.Y) / NavEditorContextMenuItemHeight;
            if (itemIndex < 0 || itemIndex >= panel.Items.Count)
            {
                continue;
            }

            var path = CreateNavEditorContextMenuPath(panel.PrefixPath, itemIndex);
            hit = new NavEditorContextMenuHit(panel.Items[itemIndex], GetNavEditorContextMenuItemBounds(panel.Bounds, itemIndex), path);
            return true;
        }

        hit = default!;
        return false;
    }

    private static int[] CreateNavEditorContextMenuPath(IReadOnlyList<int> prefixPath, int itemIndex)
    {
        var path = new int[prefixPath.Count + 1];
        for (var index = 0; index < prefixPath.Count; index += 1)
        {
            path[index] = prefixPath[index];
        }

        path[^1] = itemIndex;
        return path;
    }

    private static bool PathsEqual(int[] left, int[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index += 1)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private void CycleNavEditorAnchorKind()
    {
        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
            anchor.Kind = GetNextNavEditorAnchorKind(anchor.Kind);
            anchor.Team = NormalizeNavEditorAnchorTeam(anchor.Kind, anchor.Team);
            RefreshNavEditorAnchorAutoLabel(anchor);
            _navEditorDirty = true;
            SetNavEditorStatus($"anchor role: {DescribeNavEditorAnchorRole(anchor.Kind, anchor.Team)}");
            return;
        }

        if (_navEditorSelectedLinkIndex >= 0)
        {
            SetNavEditorStatus("anchor role only applies to anchors or new anchor defaults");
            return;
        }

        _navEditorDefaultAnchorKind = GetNextNavEditorAnchorKind(_navEditorDefaultAnchorKind);
        _navEditorDefaultAnchorTeam = NormalizeNavEditorAnchorTeam(_navEditorDefaultAnchorKind, _navEditorDefaultAnchorTeam);
        SetNavEditorStatus($"new anchors use role: {DescribeNavEditorAnchorRole(_navEditorDefaultAnchorKind, _navEditorDefaultAnchorTeam)}");
    }

    private void CycleNavEditorAnchorTeam()
    {
        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
            if (!CanNavEditorAnchorKindUseTeam(anchor.Kind))
            {
                SetNavEditorStatus($"{DescribeNavEditorAnchorKind(anchor.Kind)} anchors do not use team metadata");
                return;
            }

            anchor.Team = GetNextNavEditorAnchorTeam(anchor.Team);
            RefreshNavEditorAnchorAutoLabel(anchor);
            _navEditorDirty = true;
            SetNavEditorStatus($"anchor team: {DescribeNavEditorAnchorTeam(anchor.Team)}");
            return;
        }

        if (_navEditorSelectedLinkIndex >= 0)
        {
            SetNavEditorStatus("anchor team only applies to anchors or new anchor defaults");
            return;
        }

        if (!CanNavEditorAnchorKindUseTeam(_navEditorDefaultAnchorKind))
        {
            SetNavEditorStatus($"{DescribeNavEditorAnchorKind(_navEditorDefaultAnchorKind)} anchors do not use team metadata");
            return;
        }

        _navEditorDefaultAnchorTeam = GetNextNavEditorAnchorTeam(_navEditorDefaultAnchorTeam);
        SetNavEditorStatus($"new anchor team: {DescribeNavEditorAnchorTeam(_navEditorDefaultAnchorTeam)}");
    }

    private void CycleNavEditorViewClass()
    {
        _navEditorViewClass = GetNextNavEditorViewClass(_navEditorViewClass);
        if (_navEditorSelectedAnchorIndex >= 0
            && (_navEditorSelectedAnchorIndex >= _navEditorAnchors.Count || !AppliesToNavEditorViewClass(_navEditorAnchors[_navEditorSelectedAnchorIndex].Classes)))
        {
            ClearNavEditorSelection();
        }
        else if (_navEditorSelectedLinkIndex >= 0
            && (_navEditorSelectedLinkIndex >= _navEditorLinks.Count || !AppliesToNavEditorViewClass(_navEditorLinks[_navEditorSelectedLinkIndex].Classes)))
        {
            ClearNavEditorSelection();
        }

        SetNavEditorStatus($"viewing class: {DescribeNavEditorViewClass(_navEditorViewClass)}");
    }

    private void CycleNavEditorDefaultClassScope()
    {
        _navEditorDefaultClasses = GetNextNavEditorClassSelection(_navEditorDefaultClasses);
        SetNavEditorStatus($"new items apply to: {DescribeNavEditorClasses(_navEditorDefaultClasses)}");
    }

    private void CycleNavEditorLayerMode()
    {
        _navEditorLayerMode = _navEditorLayerMode switch
        {
            NavEditorLayerMode.HintLayer => NavEditorLayerMode.Both,
            NavEditorLayerMode.Both => NavEditorLayerMode.GeneratedGraph,
            _ => NavEditorLayerMode.HintLayer,
        };
        _navEditorDraggingAnchorIndex = -1;
        _navEditorDraggingGeneratedGraphNodeId = -1;
        _navEditorPendingGeneratedGraphSourceNodeId = -1;
        CancelNavEditorActivationZoneDrag();
        SetNavEditorStatus($"layer: {DescribeNavEditorLayerMode(_navEditorLayerMode)}");
    }

    private void ToggleNavEditorBotTags()
    {
        _navEditorShowBotTags = !_navEditorShowBotTags;
        SetNavEditorStatus(_navEditorShowBotTags ? "bot state tags enabled" : "bot state tags disabled");
    }

    private void CycleNavEditorDefaultAnchorKind()
    {
        _navEditorDefaultAnchorKind = GetNextNavEditorAnchorKind(_navEditorDefaultAnchorKind);
        _navEditorDefaultAnchorTeam = NormalizeNavEditorAnchorTeam(_navEditorDefaultAnchorKind, _navEditorDefaultAnchorTeam);
        SetNavEditorStatus($"new anchors use role: {DescribeNavEditorAnchorRole(_navEditorDefaultAnchorKind, _navEditorDefaultAnchorTeam)}");
    }

    private void CycleNavEditorDefaultAnchorTeam()
    {
        if (!CanNavEditorAnchorKindUseTeam(_navEditorDefaultAnchorKind))
        {
            SetNavEditorStatus($"{DescribeNavEditorAnchorKind(_navEditorDefaultAnchorKind)} anchors do not use team metadata");
            return;
        }

        _navEditorDefaultAnchorTeam = GetNextNavEditorAnchorTeam(_navEditorDefaultAnchorTeam);
        SetNavEditorStatus($"new anchor team: {DescribeNavEditorAnchorTeam(_navEditorDefaultAnchorTeam)}");
    }

    private void CycleNavEditorDefaultTraversal()
    {
        _navEditorDefaultTraversal = GetNextNavEditorTraversal(_navEditorDefaultTraversal);
        SetNavEditorStatus($"new links use traversal: {_navEditorDefaultTraversal}");
    }

    private void ToggleNavEditorDefaultBidirectional()
    {
        _navEditorDefaultBidirectional = !_navEditorDefaultBidirectional;
        SetNavEditorStatus(_navEditorDefaultBidirectional ? "new links will try both directions when valid" : "new links are one-way");
    }

    private void CycleNavEditorGraphEdgeDisplayMode()
    {
        _navEditorGraphEdgeDisplayMode = (NavEditorGraphEdgeDisplayMode)(((int)_navEditorGraphEdgeDisplayMode + 1) % 3);
        SetNavEditorStatus($"graph edge colors: {DescribeNavEditorGraphEdgeDisplayMode(_navEditorGraphEdgeDisplayMode)}");
    }

    private void AdjustNavEditorDefaultCostMultiplier(float delta)
    {
        _navEditorDefaultCostMultiplier = ClampNavEditorCostMultiplier(_navEditorDefaultCostMultiplier + delta);
        SetNavEditorStatus($"new link cost multiplier: {_navEditorDefaultCostMultiplier:F1}");
    }

    private void CycleNavEditorClassScope()
    {
        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
            anchor.Classes = GetNextNavEditorClassSelection(anchor.Classes);
            _navEditorDirty = true;
            if (!AppliesToNavEditorViewClass(anchor.Classes))
            {
                ClearNavEditorSelection();
            }

            SetNavEditorStatus($"anchor classes: {DescribeNavEditorClasses(anchor.Classes)}");
            return;
        }

        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.Classes = GetNextNavEditorClassSelection(link.Classes);
            _navEditorDirty = true;
            if (!AppliesToNavEditorViewClass(link.Classes))
            {
                ClearNavEditorSelection();
            }

            SetNavEditorStatus($"link classes: {DescribeNavEditorClasses(link.Classes)}");
            return;
        }

        _navEditorDefaultClasses = GetNextNavEditorClassSelection(_navEditorDefaultClasses);
        SetNavEditorStatus($"new items apply to: {DescribeNavEditorClasses(_navEditorDefaultClasses)}");
    }

    private void CycleNavEditorTraversal()
    {
        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.Traversal = GetNextNavEditorTraversal(link.Traversal);
            if (link.Traversal != BotNavigationHintTraversalKind.Jump)
            {
                link.StartJumpImmediately = false;
            }

            _navEditorDirty = true;
            SetNavEditorStatus($"link traversal: {link.Traversal}");
            return;
        }

        _navEditorDefaultTraversal = GetNextNavEditorTraversal(_navEditorDefaultTraversal);
        SetNavEditorStatus($"new links use traversal: {_navEditorDefaultTraversal}");
    }

    private void ToggleNavEditorBidirectional()
    {
        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.Bidirectional = !link.Bidirectional;
            _navEditorDirty = true;
            SetNavEditorStatus(link.Bidirectional ? "link is now bidirectional" : "link is now one-way");
            return;
        }

        _navEditorDefaultBidirectional = !_navEditorDefaultBidirectional;
        SetNavEditorStatus(_navEditorDefaultBidirectional ? "new links are bidirectional" : "new links are one-way");
    }

    private void AdjustNavEditorCostMultiplier(float delta)
    {
        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            link.CostMultiplier = ClampNavEditorCostMultiplier(link.CostMultiplier + delta);
            _navEditorDirty = true;
            SetNavEditorStatus($"link cost multiplier: {link.CostMultiplier:F1}");
            return;
        }

        _navEditorDefaultCostMultiplier = ClampNavEditorCostMultiplier(_navEditorDefaultCostMultiplier + delta);
        SetNavEditorStatus($"new link cost multiplier: {_navEditorDefaultCostMultiplier:F1}");
    }

    private void CycleNavEditorFallbackRole()
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            SetNavEditorStatus("select a hint node before changing fallback role");
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        anchor.FallbackRole = anchor.FallbackRole switch
        {
            BotNavigationHintFallbackRole.None => BotNavigationHintFallbackRole.Entry,
            BotNavigationHintFallbackRole.Entry => BotNavigationHintFallbackRole.Exit,
            _ => BotNavigationHintFallbackRole.None,
        };
        if (anchor.FallbackRole == BotNavigationHintFallbackRole.Entry && anchor.FallbackTriggers.Length == 0)
        {
            anchor.FallbackTriggers = [BotNavigationHintFallbackTrigger.NoNext];
        }

        _navEditorDirty = true;
        SetNavEditorStatus($"fallback role: {DescribeNavEditorFallbackRole(anchor.FallbackRole)}");
    }

    private void CycleNavEditorFallbackTrigger()
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            SetNavEditorStatus("select a fallback entry before changing its trigger");
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        if (anchor.FallbackRole != BotNavigationHintFallbackRole.Entry)
        {
            anchor.FallbackRole = BotNavigationHintFallbackRole.Entry;
        }

        var current = anchor.FallbackTriggers.Length == 1
            ? anchor.FallbackTriggers[0]
            : BotNavigationHintFallbackTrigger.NoNext;
        anchor.FallbackTriggers =
        [
            current switch
            {
                BotNavigationHintFallbackTrigger.NoNext => BotNavigationHintFallbackTrigger.Stuck,
                BotNavigationHintFallbackTrigger.Stuck => BotNavigationHintFallbackTrigger.Reacquire,
                BotNavigationHintFallbackTrigger.Reacquire => BotNavigationHintFallbackTrigger.CarryReturn,
                BotNavigationHintFallbackTrigger.CarryReturn => BotNavigationHintFallbackTrigger.DirectTarget,
                _ => BotNavigationHintFallbackTrigger.NoNext,
            },
        ];
        _navEditorDirty = true;
        SetNavEditorStatus($"fallback trigger: {DescribeNavEditorFallbackTriggers(anchor.FallbackTriggers)}");
    }

    private void CycleNavEditorCarryRequirement()
    {
        if (_navEditorSelectedAnchorIndex < 0 || _navEditorSelectedAnchorIndex >= _navEditorAnchors.Count)
        {
            SetNavEditorStatus("select a fallback entry before changing carry requirement");
            return;
        }

        var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
        anchor.RequiresCarryingIntel = anchor.RequiresCarryingIntel switch
        {
            null => true,
            true => false,
            _ => null,
        };
        _navEditorDirty = true;
        SetNavEditorStatus($"carry requirement: {DescribeNavEditorCarryRequirement(anchor.RequiresCarryingIntel)}");
    }

    private void CycleNavEditorRecordTestClass()
    {
        for (var index = 0; index < BotNavigationClasses.All.Count; index += 1)
        {
            if (BotNavigationClasses.All[index] != _navEditorRecordTestClass)
            {
                continue;
            }

            _navEditorRecordTestClass = index + 1 < BotNavigationClasses.All.Count
                ? BotNavigationClasses.All[index + 1]
                : BotNavigationClasses.All[0];
            SetNavEditorStatus($"test class: {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}");
            return;
        }

        _navEditorRecordTestClass = PlayerClass.Soldier;
        SetNavEditorStatus($"test class: {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}");
    }

    private void StartNavEditorTraversalCapture()
    {
        if (IsNavEditorTraversalCaptureActive())
        {
            SetNavEditorStatus("traversal recording already active");
            return;
        }

        if (_navEditorSelectedLinkIndex < 0 || _navEditorSelectedLinkIndex >= _navEditorLinks.Count)
        {
            SetNavEditorStatus("select a link before recording a traversal");
            return;
        }

        var link = _navEditorLinks[_navEditorSelectedLinkIndex];
        if (!link.AppliesToClass(_navEditorRecordTestClass))
        {
            SetNavEditorStatus($"selected link does not apply to {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}");
            return;
        }

        if (!TryGetNavEditorAnchorPosition(link.FromLabel, out var sourcePosition)
            || !TryGetNavEditorAnchorPosition(link.ToLabel, out var targetPosition))
        {
            SetNavEditorStatus("recording failed: link anchors could not be resolved");
            return;
        }

        var classDefinition = BotNavigationClasses.GetDefinition(_navEditorRecordTestClass);
        _ = _world.TrySetLocalClass(classDefinition.Id);
        _world.SetLocalHealth((int)MathF.Ceiling(_world.LocalPlayer.MaxHealth));
        _world.TeleportLocalPlayer(sourcePosition.X, sourcePosition.Y);

        _navEditorRecordingLinkIndex = _navEditorSelectedLinkIndex;
        _navEditorRecordingSourcePosition = sourcePosition;
        _navEditorRecordingTargetPosition = targetPosition;
        _navEditorRecordingSourceLabel = link.FromLabel;
        _navEditorRecordingTargetLabel = link.ToLabel;
        _navEditorRecordingRequiresGroundedArrival = !TryGetNavEditorAnchor(link.ToLabel, out var targetAnchor) || IsNavEditorAnchorGrounded(targetAnchor);
        _navEditorRecordedSamples.Clear();
        _navEditorTraversalCaptureMode = NavEditorTraversalCaptureMode.Armed;
        _navEditorTraversalCaptureInput = default;
        _respawnCameraDetached = false;

        SetNavEditorStatus($"recording armed: move from {link.FromLabel} to {link.ToLabel}, press Escape to cancel");
        AddConsoleLine($"nav editor recording armed for {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}: {link.FromLabel} -> {link.ToLabel}");
    }

    private void ClearSelectedNavEditorRecordedTraversal()
    {
        if (_navEditorSelectedLinkIndex < 0 || _navEditorSelectedLinkIndex >= _navEditorLinks.Count)
        {
            SetNavEditorStatus("select a link before clearing a recording");
            return;
        }

        var link = _navEditorLinks[_navEditorSelectedLinkIndex];
        var removed = link.RecordedTraversals.RemoveAll(recording => recording.ClassId == _navEditorRecordTestClass);
        if (removed <= 0)
        {
            SetNavEditorStatus($"no recording stored for {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}");
            return;
        }

        _navEditorDirty = true;
        SetNavEditorStatus($"cleared {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)} recording");
    }

    private void StartNavEditorSelectedLinkPlayback()
    {
        if (_navEditorSelectedLinkIndex < 0 || _navEditorSelectedLinkIndex >= _navEditorLinks.Count)
        {
            SetNavEditorStatus("select a link before testing it");
            return;
        }

        var link = _navEditorLinks[_navEditorSelectedLinkIndex];
        if (!link.AppliesToClass(_navEditorRecordTestClass))
        {
            SetNavEditorStatus($"selected link does not apply to {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}");
            return;
        }

        if (!TryGetNavEditorAnchorPosition(link.FromLabel, out var sourcePosition)
            || !TryGetNavEditorAnchorPosition(link.ToLabel, out var targetPosition))
        {
            SetNavEditorStatus("link test failed: link anchors could not be resolved");
            return;
        }

        var hintLink = new BotNavigationHintLink
        {
            FromLabel = link.FromLabel,
            ToLabel = link.ToLabel,
            Classes = link.Classes.ToArray(),
            Bidirectional = link.Bidirectional,
            Traversal = link.Traversal,
            StartJumpImmediately = link.StartJumpImmediately,
            CostMultiplier = link.CostMultiplier,
            RecordedTraversals = link.RecordedTraversals
                .Select(recording => new BotNavigationHintRecordedTraversal
                {
                    ClassId = recording.ClassId,
                    InputTape = recording.InputTape.ToArray(),
                })
                .ToArray(),
        };

        var requiresGroundedArrival = !TryGetNavEditorAnchor(link.ToLabel, out var targetAnchor) || IsNavEditorAnchorGrounded(targetAnchor);
        if (!BotNavigationDebugPlanner.TryBuildHintLinkTraversal(
                _world.Level,
                _navEditorRecordTestClass,
                hintLink,
                sourcePosition.X,
                sourcePosition.Y,
                targetPosition.X,
                targetPosition.Y,
                requiresGroundedArrival,
                _world.Config.FixedDeltaSeconds,
                out var previewStep,
                out var failureMessage))
        {
            SetNavEditorStatus($"link test failed: {failureMessage}");
            AddConsoleLine($"nav editor link test failed for {link.FromLabel} -> {link.ToLabel}: {failureMessage}");
            return;
        }

        BeginNavEditorTraversalPlayback(
            [CreateNavEditorPlaybackStep(previewStep)],
            _navEditorRecordTestClass,
            teleportToFirstStepSource: true,
            label: $"link {link.FromLabel} -> {link.ToLabel}");
    }

    private void StartNavEditorSelectedRoutePlayback()
    {
        if (ShouldUseNavEditorGeneratedGraphRoutePlayback())
        {
            StartNavEditorGeneratedGraphRoutePlayback();
            return;
        }

        if (!TryBuildNavEditorRoutePlayback(
                out var playbackSteps,
                out var classId,
                out var label,
                out var failureMessage))
        {
            SetNavEditorStatus($"route test failed: {failureMessage}");
            AddConsoleLine($"nav editor route test failed: {failureMessage}");
            return;
        }

        BeginNavEditorTraversalPlayback(
            playbackSteps,
            classId,
            teleportToFirstStepSource: true,
            label: label);
    }

    private void SetNavEditorGeneratedGraphRouteStart(int nodeId)
    {
        if (!TryGetNavEditorGeneratedGraphNode(nodeId, out _))
        {
            SetNavEditorStatus("generated graph start node is no longer valid");
            return;
        }

        _navEditorGraphRouteStartNodeId = nodeId;
        _navEditorSelectedGeneratedGraphNodeId = nodeId;
        RefreshNavEditorGeneratedGraphRoutePreview();
        SetNavEditorStatus($"graph route start: node {nodeId}");
    }

    private void SetNavEditorGeneratedGraphRouteGoal(int nodeId)
    {
        if (!TryGetNavEditorGeneratedGraphNode(nodeId, out _))
        {
            SetNavEditorStatus("generated graph goal node is no longer valid");
            return;
        }

        _navEditorGraphRouteGoalNodeId = nodeId;
        _navEditorSelectedGeneratedGraphNodeId = nodeId;
        RefreshNavEditorGeneratedGraphRoutePreview();
        SetNavEditorStatus($"graph route goal: node {nodeId}");
    }

    private void RefreshNavEditorGeneratedGraphRoutePreview()
    {
        if (!TryResolveNavEditorGeneratedGraphRouteEndpoints(out var startNodeId, out var goalNodeId, out _)
            || !TryBuildNavEditorGeneratedGraphRoute(
                startNodeId,
                goalNodeId,
                out var routeNodeIds,
                out _))
        {
            _navEditorGraphPreviewRouteNodeIds = Array.Empty<int>();
            return;
        }

        _navEditorGraphPreviewRouteNodeIds = routeNodeIds;
    }

    private void StartNavEditorGeneratedGraphRoutePlayback()
    {
        if (!TryBuildNavEditorGeneratedGraphRoutePlayback(
                out var playbackSteps,
                out var classId,
                out var label,
                out var failureMessage))
        {
            SetNavEditorStatus($"graph route test failed: {failureMessage}");
            AddConsoleLine($"nav editor graph route test failed: {failureMessage}");
            return;
        }

        BeginNavEditorTraversalPlayback(
            playbackSteps,
            classId,
            teleportToFirstStepSource: true,
            label: label);
    }

    private bool ShouldUseNavEditorGeneratedGraphRoutePlayback()
    {
        if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph)
        {
            return true;
        }

        return _navEditorLayerMode == NavEditorLayerMode.Both
            && (_navEditorSelectedGeneratedGraphNodeId >= 0
                || (_navEditorSelectedAnchorIndex < 0
                    && _navEditorSelectedLinkIndex < 0
                    && (_navEditorGraphRouteStartNodeId >= 0 || _navEditorGraphRouteGoalNodeId >= 0)));
    }

    private bool CanStartNavEditorGeneratedGraphRoutePlayback()
    {
        if (_navEditorGeneratedCompatGraph is null
            || IsNavEditorTraversalCaptureActive()
            || IsNavEditorTraversalPlaybackActive())
        {
            return false;
        }

        return TryResolveNavEditorGeneratedGraphRouteEndpoints(out _, out _, out _);
    }

    private bool TryBuildNavEditorGeneratedGraphRoutePlayback(
        out NavEditorPlaybackStep[] playbackSteps,
        out PlayerClass classId,
        out string label,
        out string failureMessage)
    {
        playbackSteps = Array.Empty<NavEditorPlaybackStep>();
        classId = _navEditorRecordTestClass;
        label = string.Empty;
        failureMessage = string.Empty;

        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            failureMessage = "generated graph is not loaded";
            return false;
        }

        if (!TryResolveNavEditorGeneratedGraphRouteEndpoints(out var startNodeId, out var goalNodeId, out failureMessage))
        {
            return false;
        }

        if (!TryBuildNavEditorGeneratedGraphRoute(startNodeId, goalNodeId, out var routeNodeIds, out failureMessage))
        {
            return false;
        }

        var nodesById = asset.Nodes.ToDictionary(static node => node.Id);
        var edgesByPair = new Dictionary<long, BotNavigationEdge>();
        for (var edgeIndex = 0; edgeIndex < asset.Edges.Count; edgeIndex += 1)
        {
            var edge = asset.Edges[edgeIndex];
            var edgeKey = GetNavEditorGraphEdgeKey(edge.FromNodeId, edge.ToNodeId);
            if (!edgesByPair.ContainsKey(edgeKey))
            {
                edgesByPair[edgeKey] = edge;
            }
        }
        var steps = new List<NavEditorPlaybackStep>(Math.Max(0, routeNodeIds.Length - 1));
        for (var index = 1; index < routeNodeIds.Length; index += 1)
        {
            var fromNodeId = routeNodeIds[index - 1];
            var toNodeId = routeNodeIds[index];
            if (!nodesById.TryGetValue(fromNodeId, out var fromNode)
                || !nodesById.TryGetValue(toNodeId, out var toNode)
                || !edgesByPair.TryGetValue(GetNavEditorGraphEdgeKey(fromNodeId, toNodeId), out var edge))
            {
                failureMessage = $"route edge {fromNodeId} -> {toNodeId} could not be resolved";
                return false;
            }

            steps.Add(new NavEditorPlaybackStep
            {
                FromLabel = $"g{fromNode.Id}",
                ToLabel = $"g{toNode.Id}",
                FromX = fromNode.X,
                FromY = fromNode.Y,
                ToX = toNode.X,
                ToY = toNode.Y,
                Kind = edge.Kind,
                ForcedHorizontalDirection = edge.Kind == BotNavigationTraversalKind.Drop
                    ? (toNode.X >= fromNode.X ? 1 : -1)
                    : 0,
                RequiresGroundedArrival = toNode.RequiresGroundSupport,
                InputTape = edge.InputTape.ToArray(),
            });
        }

        if (steps.Count == 0)
        {
            failureMessage = "generated graph route produced no traversal steps";
            return false;
        }

        _navEditorGraphRouteStartNodeId = startNodeId;
        _navEditorGraphRouteGoalNodeId = goalNodeId;
        _navEditorGraphPreviewRouteNodeIds = routeNodeIds;
        playbackSteps = steps.ToArray();
        label = $"graph {startNodeId} -> {goalNodeId}";
        return true;
    }

    private bool TryResolveNavEditorGeneratedGraphRouteEndpoints(
        out int startNodeId,
        out int goalNodeId,
        out string failureMessage)
    {
        startNodeId = _navEditorGraphRouteStartNodeId;
        goalNodeId = _navEditorGraphRouteGoalNodeId;
        failureMessage = string.Empty;

        if (startNodeId < 0
            && _navEditorSelectedGeneratedGraphNodeId >= 0
            && _navEditorSelectedGeneratedGraphNodeId != goalNodeId)
        {
            startNodeId = _navEditorSelectedGeneratedGraphNodeId;
        }

        if (goalNodeId < 0
            && _navEditorSelectedGeneratedGraphNodeId >= 0
            && _navEditorSelectedGeneratedGraphNodeId != startNodeId)
        {
            goalNodeId = _navEditorSelectedGeneratedGraphNodeId;
        }

        if (goalNodeId < 0)
        {
            failureMessage = "right-click a generated node and choose Set Graph Goal";
            return false;
        }

        if (startNodeId < 0)
        {
            failureMessage = "right-click a generated node and choose Set Graph Start";
            return false;
        }

        if (startNodeId == goalNodeId)
        {
            failureMessage = "start and goal are the same generated node";
            return false;
        }

        return true;
    }

    private bool TryBuildNavEditorGeneratedGraphRoute(
        int startNodeId,
        int goalNodeId,
        out int[] routeNodeIds,
        out string failureMessage)
    {
        routeNodeIds = Array.Empty<int>();
        failureMessage = string.Empty;
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            failureMessage = "generated graph is not loaded";
            return false;
        }

        var nodesById = asset.Nodes.ToDictionary(static node => node.Id);
        if (!nodesById.ContainsKey(startNodeId))
        {
            failureMessage = $"start node {startNodeId} is not in the generated graph";
            return false;
        }

        if (!nodesById.ContainsKey(goalNodeId))
        {
            failureMessage = $"goal node {goalNodeId} is not in the generated graph";
            return false;
        }

        var outgoingByNode = asset.Edges
            .GroupBy(static edge => edge.FromNodeId)
            .ToDictionary(static group => group.Key, static group => group.OrderBy(static edge => edge.Cost).ToArray());
        var distanceByNode = new Dictionary<int, float> { [startNodeId] = 0f };
        var previousByNode = new Dictionary<int, int>();
        var pending = new PriorityQueue<int, float>();
        pending.Enqueue(startNodeId, 0f);

        while (pending.Count > 0)
        {
            var nodeId = pending.Dequeue();
            var currentDistance = distanceByNode[nodeId];
            if (nodeId == goalNodeId)
            {
                break;
            }

            if (!outgoingByNode.TryGetValue(nodeId, out var outgoingEdges))
            {
                continue;
            }

            for (var edgeIndex = 0; edgeIndex < outgoingEdges.Length; edgeIndex += 1)
            {
                var edge = outgoingEdges[edgeIndex];
                if (!nodesById.ContainsKey(edge.ToNodeId))
                {
                    continue;
                }

                var edgeCost = edge.Cost > 0f ? edge.Cost : 1f;
                var candidateDistance = currentDistance + edgeCost;
                if (distanceByNode.TryGetValue(edge.ToNodeId, out var knownDistance)
                    && knownDistance <= candidateDistance)
                {
                    continue;
                }

                distanceByNode[edge.ToNodeId] = candidateDistance;
                previousByNode[edge.ToNodeId] = nodeId;
                pending.Enqueue(edge.ToNodeId, candidateDistance);
            }
        }

        if (!distanceByNode.ContainsKey(goalNodeId))
        {
            failureMessage = $"no generated graph route from node {startNodeId} to node {goalNodeId}";
            return false;
        }

        var route = new List<int> { goalNodeId };
        var currentNodeId = goalNodeId;
        while (currentNodeId != startNodeId)
        {
            if (!previousByNode.TryGetValue(currentNodeId, out var previousNodeId))
            {
                failureMessage = "generated graph route reconstruction failed";
                return false;
            }

            currentNodeId = previousNodeId;
            route.Add(currentNodeId);
        }

        route.Reverse();
        routeNodeIds = route.ToArray();
        return true;
    }

    private bool TryBuildNavEditorRoutePlayback(
        out NavEditorPlaybackStep[] playbackSteps,
        out PlayerClass classId,
        out string label,
        out string failureMessage)
    {
        playbackSteps = Array.Empty<NavEditorPlaybackStep>();
        classId = _navEditorRecordTestClass;
        label = string.Empty;
        failureMessage = string.Empty;

        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            return TryBuildNavEditorRoutePlaybackFromLink(_navEditorSelectedLinkIndex, out playbackSteps, out label, out failureMessage);
        }

        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            return TryBuildNavEditorRoutePlaybackFromAnchor(_navEditorSelectedAnchorIndex, out playbackSteps, out label, out failureMessage);
        }

        failureMessage = "select a starting link or anchor before testing a route";
        return false;
    }

    private bool TryBuildNavEditorRoutePlaybackFromAnchor(
        int anchorIndex,
        out NavEditorPlaybackStep[] playbackSteps,
        out string label,
        out string failureMessage)
    {
        playbackSteps = Array.Empty<NavEditorPlaybackStep>();
        label = string.Empty;
        failureMessage = string.Empty;

        if (anchorIndex < 0 || anchorIndex >= _navEditorAnchors.Count)
        {
            failureMessage = "selected anchor is no longer valid";
            return false;
        }

        var anchor = _navEditorAnchors[anchorIndex];
        var outgoingLinks = GetNavEditorOutgoingRouteCandidates(anchor.Label, new HashSet<int>());
        if (outgoingLinks.Count == 0)
        {
            failureMessage = $"{anchor.Label} has no outgoing links for {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}";
            return false;
        }

        if (outgoingLinks.Count > 1)
        {
            failureMessage = $"{anchor.Label} branches into {outgoingLinks.Count} links; select a specific starting link to test that route";
            return false;
        }

        return TryBuildNavEditorRoutePlaybackFromCandidate(outgoingLinks[0], out playbackSteps, out label, out failureMessage);
    }

    private bool TryBuildNavEditorRoutePlaybackFromLink(
        int linkIndex,
        out NavEditorPlaybackStep[] playbackSteps,
        out string label,
        out string failureMessage)
    {
        playbackSteps = Array.Empty<NavEditorPlaybackStep>();
        label = string.Empty;
        failureMessage = string.Empty;

        if (linkIndex < 0 || linkIndex >= _navEditorLinks.Count)
        {
            failureMessage = "selected link is no longer valid";
            return false;
        }

        var link = _navEditorLinks[linkIndex];
        if (!link.AppliesToClass(_navEditorRecordTestClass))
        {
            failureMessage = $"selected link does not apply to {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}";
            return false;
        }

        return TryBuildNavEditorRoutePlaybackFromCandidate(
            new NavEditorRouteCandidate(linkIndex, false, link.FromLabel, link.ToLabel),
            out playbackSteps,
            out label,
            out failureMessage);
    }

    private bool TryBuildNavEditorRoutePlaybackFromCandidate(
        NavEditorRouteCandidate firstCandidate,
        out NavEditorPlaybackStep[] playbackSteps,
        out string label,
        out string failureMessage)
    {
        playbackSteps = Array.Empty<NavEditorPlaybackStep>();
        label = string.Empty;
        failureMessage = string.Empty;

        var visitedLinkIndices = new HashSet<int>();
        var steps = new List<NavEditorPlaybackStep>();
        var currentCandidate = firstCandidate;

        while (true)
        {
            if (visitedLinkIndices.Contains(currentCandidate.LinkIndex))
            {
                failureMessage = $"route test looped back onto {currentCandidate.FromLabel} -> {currentCandidate.ToLabel}";
                return false;
            }

            if (!TryBuildNavEditorPlaybackStepForRouteCandidate(currentCandidate, out var step, out failureMessage))
            {
                failureMessage = $"{currentCandidate.FromLabel} -> {currentCandidate.ToLabel}: {failureMessage}";
                return false;
            }

            steps.Add(step);
            visitedLinkIndices.Add(currentCandidate.LinkIndex);

            var nextCandidates = GetNavEditorOutgoingRouteCandidates(currentCandidate.ToLabel, visitedLinkIndices);
            if (nextCandidates.Count == 0)
            {
                break;
            }

            if (nextCandidates.Count > 1)
            {
                var options = string.Join(", ", nextCandidates.Select(candidate => $"{candidate.FromLabel} -> {candidate.ToLabel}"));
                failureMessage = $"{currentCandidate.ToLabel} branches into multiple links: {options}. Select the next link you want to test.";
                return false;
            }

            currentCandidate = nextCandidates[0];
            if (steps.Count > _navEditorLinks.Count)
            {
                failureMessage = "route test exceeded the authored link count and was stopped to avoid an infinite loop";
                return false;
            }
        }

        playbackSteps = steps.ToArray();
        label = steps.Count == 1
            ? $"route {steps[0].FromLabel} -> {steps[0].ToLabel}"
            : $"route {steps[0].FromLabel} -> {steps[^1].ToLabel}";
        return true;
    }

    private List<NavEditorRouteCandidate> GetNavEditorOutgoingRouteCandidates(
        string fromLabel,
        HashSet<int> visitedLinkIndices)
    {
        var candidates = new List<NavEditorRouteCandidate>();
        for (var index = 0; index < _navEditorLinks.Count; index += 1)
        {
            if (visitedLinkIndices.Contains(index))
            {
                continue;
            }

            var link = _navEditorLinks[index];
            if (!link.AppliesToClass(_navEditorRecordTestClass))
            {
                continue;
            }

            if (string.Equals(link.FromLabel, fromLabel, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(new NavEditorRouteCandidate(index, false, link.FromLabel, link.ToLabel));
            }
            else if (link.Bidirectional && string.Equals(link.ToLabel, fromLabel, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(new NavEditorRouteCandidate(index, true, link.ToLabel, link.FromLabel));
            }
        }

        return candidates;
    }

    private bool TryBuildNavEditorPlaybackStepForRouteCandidate(
        NavEditorRouteCandidate candidate,
        out NavEditorPlaybackStep playbackStep,
        out string failureMessage)
    {
        playbackStep = default!;
        failureMessage = string.Empty;

        if (candidate.LinkIndex < 0 || candidate.LinkIndex >= _navEditorLinks.Count)
        {
            failureMessage = "route link could not be resolved";
            return false;
        }

        var link = _navEditorLinks[candidate.LinkIndex];
        if (!TryGetNavEditorAnchorPosition(candidate.FromLabel, out var sourcePosition)
            || !TryGetNavEditorAnchorPosition(candidate.ToLabel, out var targetPosition))
        {
            failureMessage = "route link anchors could not be resolved";
            return false;
        }

        var recordedTraversals = candidate.Reverse
            ? Array.Empty<BotNavigationHintRecordedTraversal>()
            : link.RecordedTraversals
                .Select(recording => new BotNavigationHintRecordedTraversal
                {
                    ClassId = recording.ClassId,
                    InputTape = recording.InputTape.ToArray(),
                })
                .ToArray();
        var hintLink = new BotNavigationHintLink
        {
            FromLabel = candidate.FromLabel,
            ToLabel = candidate.ToLabel,
            Classes = link.Classes.ToArray(),
            Bidirectional = false,
            Traversal = link.Traversal,
            StartJumpImmediately = link.StartJumpImmediately,
            CostMultiplier = link.CostMultiplier,
            RecordedTraversals = recordedTraversals,
        };

        var requiresGroundedArrival = !TryGetNavEditorAnchor(candidate.ToLabel, out var targetAnchor) || IsNavEditorAnchorGrounded(targetAnchor);
        if (!BotNavigationDebugPlanner.TryBuildHintLinkTraversal(
                _world.Level,
                _navEditorRecordTestClass,
                hintLink,
                sourcePosition.X,
                sourcePosition.Y,
                targetPosition.X,
                targetPosition.Y,
                requiresGroundedArrival,
                _world.Config.FixedDeltaSeconds,
                out var previewStep,
                out failureMessage))
        {
            return false;
        }

        playbackStep = CreateNavEditorPlaybackStep(previewStep);
        return true;
    }

    private bool TryBuildNavEditorRouteTestAsset(
        PlayerClass classId,
        out BotNavigationAsset asset,
        out BotNavigationValidationResult validation,
        out string failureMessage)
    {
        asset = default!;
        validation = BotNavigationValidationResult.Valid;
        failureMessage = string.Empty;

        try
        {
            var hintAsset = BuildNavEditorHintAssetSnapshot();
            var fingerprint = BotNavigationLevelFingerprint.Compute(_world.Level);
            asset = BotNavigationAssetBuilder.Build(_world.Level, classId, fingerprint, hintAsset);
            validation = BotNavigationAssetValidator.Validate(_world.Level, asset);
            return true;
        }
        catch (Exception ex)
        {
            failureMessage = $"route test could not build a {BotNavigationClasses.GetShortLabel(classId)} nav graph: {ex.Message}";
            return false;
        }
    }

    private void BeginNavEditorTraversalPlayback(
        IReadOnlyList<NavEditorPlaybackStep> steps,
        PlayerClass classId,
        bool teleportToFirstStepSource,
        string label)
    {
        if (steps.Count == 0)
        {
            SetNavEditorStatus("playback needs at least one traversal step");
            return;
        }

        ClearNavEditorTraversalCaptureState();
        ClearNavEditorTraversalPlaybackState();

        var classDefinition = BotNavigationClasses.GetDefinition(classId);
        _ = _world.TrySetLocalClass(classDefinition.Id);
        _world.SetLocalHealth((int)MathF.Ceiling(_world.LocalPlayer.MaxHealth));

        var spawnPosition = teleportToFirstStepSource
            ? new Vector2(steps[0].FromX, steps[0].FromY)
            : new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
        _world.TeleportLocalPlayer(spawnPosition.X, spawnPosition.Y);

        for (var index = 0; index < steps.Count; index += 1)
        {
            _navEditorPlaybackSteps.Add(ConfigureNavEditorPlaybackStepGrounding(steps[index]));
        }
        _navEditorPlaybackStepIndex = 0;
        _navEditorPlaybackFrameIndex = 0;
        _navEditorPlaybackFrameSecondsRemaining = 0d;
        _navEditorPlaybackElapsedTicks = 0;
        _navEditorPlaybackLabel = label;
        _respawnCameraDetached = false;
        ResetNavEditorPlaybackFrameState();

        var classLabel = BotNavigationClasses.GetShortLabel(classId);
        SetNavEditorStatus($"testing {label} as {classLabel}. Press Escape to cancel.");
        AddConsoleLine($"nav editor testing {label} as {classLabel} ({steps.Count} step{(steps.Count == 1 ? string.Empty : "s")})");
    }

    private void CancelNavEditorTraversalPlayback(string reason)
    {
        ClearNavEditorTraversalPlaybackState();
        SetNavEditorStatus(reason);
        AddConsoleLine(reason);
    }

    private void ClearNavEditorTraversalPlaybackState()
    {
        _navEditorPlaybackSteps.Clear();
        _navEditorPlaybackStepIndex = -1;
        _navEditorPlaybackFrameIndex = 0;
        _navEditorPlaybackFrameSecondsRemaining = 0d;
        _navEditorPlaybackPostTapeTicks = 0;
        _navEditorPlaybackElapsedTicks = 0;
        _navEditorPlaybackLabel = string.Empty;
    }

    private static NavEditorPlaybackStep CreateNavEditorPlaybackStep(BotNavigationDebugTraversalStep step)
    {
        return new NavEditorPlaybackStep
        {
            FromLabel = step.FromLabel,
            ToLabel = step.ToLabel,
            FromX = step.FromX,
            FromY = step.FromY,
            ToX = step.ToX,
            ToY = step.ToY,
            Kind = step.Kind,
            ForcedHorizontalDirection = step.ForcedHorizontalDirection,
            InputTape = step.InputTape.ToArray(),
        };
    }

    private NavEditorPlaybackStep ConfigureNavEditorPlaybackStepGrounding(NavEditorPlaybackStep step)
    {
        if (!TryGetNavEditorAnchor(step.ToLabel, out var targetAnchor))
        {
            return step;
        }

        step.RequiresGroundedArrival = IsNavEditorAnchorGrounded(targetAnchor);
        return step;
    }

    private bool SaveNavEditorState()
    {
        var hintsSaved = SaveNavEditorHintAsset();
        var graphSaved = SaveNavEditorGeneratedGraphAsset();
        if (!hintsSaved || !graphSaved)
        {
            SetNavEditorStatus("nav editor save failed");
            return false;
        }

        _navEditorDirty = false;
        _navEditorLoadedLevelName = _world.Level.Name;
        _navEditorLoadedMapAreaIndex = _world.Level.MapAreaIndex;
        SetNavEditorStatus("nav editor saved hints + graph");
        AddConsoleLine($"nav editor saved {BotNavigationHintStore.ResolveWritablePath(_world.Level.Name, _world.Level.MapAreaIndex)}");
        if (_navEditorScoreTrioActive)
        {
            RunNavEditorScoreTrioPracticeBots();
            AddConsoleLine("nav editor respawned the score trio so the live run picks up the saved graph.");
        }

        return true;
    }

    private bool SaveNavEditorHintAsset()
    {
        try
        {
            BotNavigationHintStore.Save(BuildNavEditorHintAssetSnapshot());
            return true;
        }
        catch (Exception ex)
        {
            SetNavEditorStatus($"save failed: {ex.Message}");
            AddConsoleLine($"nav editor save failed: {ex.Message}");
            return false;
        }
    }

    private bool SaveNavEditorGeneratedGraphAsset()
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset)
        {
            SetNavEditorStatus("graph save failed: generated graph is not loaded");
            AddConsoleLine("nav editor graph save failed: generated graph is not loaded");
            return false;
        }

        try
        {
            var graphAsset = BuildNavEditorGeneratedGraphSnapshot();
            var validation = BotNavigationAssetValidator.Validate(_world.Level, graphAsset);
            var outputDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var runtimeOutputDirectory = ContentRoot.GetPath("BotNav");
            if (!string.IsNullOrWhiteSpace(runtimeOutputDirectory))
            {
                outputDirectories.Add(runtimeOutputDirectory);
            }

            var sourceOutputDirectory = ProjectSourceLocator.FindDirectory("Core/Content/BotNav");
            if (!string.IsNullOrWhiteSpace(sourceOutputDirectory))
            {
                outputDirectories.Add(sourceOutputDirectory);
            }

            var resolvedDirectory = Path.GetDirectoryName(BotNavigationAssetStore.ResolveModernShippedPath(_world.Level.Name, _world.Level.MapAreaIndex));
            if (!string.IsNullOrWhiteSpace(resolvedDirectory))
            {
                outputDirectories.Add(resolvedDirectory);
            }

            if (outputDirectories.Count == 0)
            {
                throw new InvalidOperationException("could not resolve a writable BotNav output directory");
            }

            foreach (var outputDirectory in outputDirectories)
            {
                BotNavigationAssetStore.SaveShipped(graphAsset, outputDirectory);
            }

            _navEditorGeneratedCompatGraph = graphAsset;
            _navEditorGeneratedCompatGraphStatus = validation.IsStructurallyValid
                ? $"saved graph: {graphAsset.Nodes.Count} nodes / {graphAsset.Edges.Count} links"
                : $"saved graph: {graphAsset.Nodes.Count} nodes / {graphAsset.Edges.Count} links INVALID ({validation.BuildSummary()})";
            var fileName = BotNavigationAssetStore.GetModernAssetFileName(graphAsset.LevelName, graphAsset.MapAreaIndex);
            foreach (var outputDirectory in outputDirectories)
            {
                AddConsoleLine($"nav editor saved {Path.Combine(outputDirectory, fileName)}");
            }

            if (!validation.IsStructurallyValid)
            {
                AddConsoleLine($"nav editor saved an invalid graph asset. Compat will ignore it until fixed: {validation.BuildSummary()}");
            }

            return true;
        }
        catch (Exception ex)
        {
            SetNavEditorStatus($"graph save failed: {ex.Message}");
            AddConsoleLine($"nav editor graph save failed: {ex.Message}");
            return false;
        }
    }

    private BotNavigationAsset BuildNavEditorGeneratedGraphSnapshot()
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset currentGraph)
        {
            throw new InvalidOperationException("generated graph is not loaded");
        }

        var normalizedGraph = BotNavigationModernGraphEditor.Normalize(currentGraph);
        var fingerprint = BotNavigationLevelFingerprint.Compute(_world.Level);
        return new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = _world.Level.Name,
            MapAreaIndex = _world.Level.MapAreaIndex,
            ClassId = null,
            Profile = BotNavigationProfile.Standard,
            LevelFingerprint = fingerprint,
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            BuiltUtc = DateTime.UtcNow,
            Stats = normalizedGraph.Stats,
            Nodes = normalizedGraph.Nodes.ToArray(),
            Edges = normalizedGraph.Edges.ToArray(),
        };
    }

    private void StartNavEditorRebuild()
    {
        if (_navEditorRebuildTask is not null && !_navEditorRebuildTask.IsCompleted)
        {
            SetNavEditorStatus("nav rebuild already running");
            return;
        }

        if (!SaveNavEditorHintAsset())
        {
            return;
        }
        var levelName = _world.Level.Name;
        var mapAreaIndex = _world.Level.MapAreaIndex;
        _navEditorRebuildStatus = $"rebuilding nav for {levelName} area {mapAreaIndex}...";
        SetNavEditorStatus(_navEditorRebuildStatus);
        AddConsoleLine(_navEditorRebuildStatus);
        _navEditorRebuildTask = Task.Run(() => RebuildNavEditorAssets(levelName, mapAreaIndex));
    }

    private void UpdateNavEditorRebuildTask()
    {
        if (_navEditorRebuildTask is null || !_navEditorRebuildTask.IsCompleted)
        {
            return;
        }

        NavEditorRebuildResult result;
        try
        {
            result = _navEditorRebuildTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            result = new NavEditorRebuildResult(false, $"nav rebuild failed: {ex.Message}", [ex.Message]);
        }

        _navEditorRebuildTask = null;
        _navEditorRebuildStatus = result.Summary;
        SetNavEditorStatus(result.Summary);
        AddConsoleLine(result.Summary);
        foreach (var line in result.Lines)
        {
            AddConsoleLine(line);
        }

        if (result.Success
            && string.Equals(_world.Level.Name, result.LevelName, StringComparison.OrdinalIgnoreCase)
            && _world.Level.MapAreaIndex == result.MapAreaIndex)
        {
            _navEditorDirty = false;
            _navEditorLoadedLevelName = _world.Level.Name;
            _navEditorLoadedMapAreaIndex = _world.Level.MapAreaIndex;
            _navEditorPendingGeneratedGraphSourceNodeId = -1;
            _navEditorDraggingGeneratedGraphNodeId = -1;
            ClearNavEditorGeneratedGraphSelection();
            LoadPracticeNavigationAssetsForCurrentLevel();
            LoadNavEditorGeneratedCompatGraph();
        }
    }

    private static NavEditorRebuildResult RebuildNavEditorAssets(string levelName, int mapAreaIndex)
    {
        var level = SimpleLevelFactory.CreateImportedLevel(levelName, mapAreaIndex);
        if (level is null)
        {
            return new NavEditorRebuildResult(
                Success: false,
                Summary: $"nav rebuild failed: could not load map {levelName} area {mapAreaIndex}",
                Lines: Array.Empty<string>(),
                LevelName: levelName,
                MapAreaIndex: mapAreaIndex);
        }

        var outputDirectory = ProjectSourceLocator.FindDirectory("Core/Content/BotNav")
            ?? Path.GetDirectoryName(BotNavigationAssetStore.ResolveShippedPath(level.Name, level.MapAreaIndex, PlayerClass.Soldier))
            ?? ContentRoot.GetPath("BotNav");
        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        var lines = new List<string>();
        DeleteNavEditorLegacyClassAssets(outputDirectory, level.Name, level.MapAreaIndex);
        DeleteNavEditorLegacyProfileAssets(outputDirectory, level.Name, level.MapAreaIndex);
        var asset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint);
        var validation = BotNavigationAssetValidator.Validate(level, asset);
        BotNavigationAssetStore.SaveShipped(asset, outputDirectory);
        var invalidCount = validation.IsStructurallyValid ? 0 : 1;

        var classLabels = string.Join(
            "/",
            BotNavigationClasses.All.Select(BotNavigationClasses.GetShortLabel));
        lines.Add($"modern [{classLabels}] nodes={asset.Nodes.Count} edges={asset.Edges.Count} edgeValidation=geometry nav={(validation.IsStructurallyValid ? "ok" : validation.BuildSummary())}");

        var summary = invalidCount > 0
            ? $"nav rebuild finished with {invalidCount} invalid profile asset(s) (Modern graph only; editor hints saved separately)"
            : "nav rebuild finished (Modern graph only; editor hints saved separately)";
        return new NavEditorRebuildResult(true, summary, lines.ToArray(), level.Name, level.MapAreaIndex);
    }

    private static void DeleteNavEditorLegacyClassAssets(string outputDirectory, string levelName, int mapAreaIndex)
    {
        foreach (var classId in BotNavigationClasses.All)
        {
            var legacyPath = Path.Combine(outputDirectory, BotNavigationAssetStore.GetAssetFileName(levelName, mapAreaIndex, classId));
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
            }
        }
    }

    private static void DeleteNavEditorLegacyProfileAssets(string outputDirectory, string levelName, int mapAreaIndex)
    {
        foreach (var profile in BotNavigationProfiles.All)
        {
            var legacyPath = Path.Combine(outputDirectory, BotNavigationAssetStore.GetLegacyAssetFileName(levelName, mapAreaIndex, profile));
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
            }
        }
    }

    private BotNavigationHintAsset BuildNavEditorHintAssetSnapshot()
    {
        return new BotNavigationHintAsset
        {
            FormatVersion = BotNavigationHintStore.CurrentFormatVersion,
            LevelName = _world.Level.Name,
            MapAreaIndex = _world.Level.MapAreaIndex,
            BuildMode = BotNavigationHintBuildMode.ExplicitGraph,
            Nodes = _navEditorAnchors.Select(anchor => new BotNavigationHintNode
            {
                Label = anchor.Label,
                AutoLabel = anchor.AutoLabel,
                Classes = anchor.Classes.ToArray(),
                X = anchor.X,
                Y = anchor.Y,
                Kind = anchor.Kind,
                Team = anchor.Team,
                FallbackRole = anchor.FallbackRole,
                FallbackTriggers = anchor.FallbackTriggers.ToArray(),
                FallbackRadius = anchor.FallbackRadius,
                RequiresCarryingIntel = anchor.RequiresCarryingIntel,
                FallbackActivationZone = CreateNavEditorFallbackActivationZone(anchor),
            }).ToArray(),
            Links = _navEditorLinks.Select(link => new BotNavigationHintLink
            {
                FromLabel = link.FromLabel,
                ToLabel = link.ToLabel,
                Classes = link.Classes.ToArray(),
                Bidirectional = link.Bidirectional,
                Traversal = link.Traversal,
                StartJumpImmediately = link.StartJumpImmediately,
                CostMultiplier = link.CostMultiplier,
                RecordedTraversals = link.RecordedTraversals
                    .Select(recording => new BotNavigationHintRecordedTraversal
                    {
                        ClassId = recording.ClassId,
                        InputTape = recording.InputTape.ToArray(),
                    })
                    .ToArray(),
            }).ToArray(),
        };
    }

    private void DrawNavEditorWorldOverlay(Vector2 cameraPosition)
    {
        if (_navEditorLayerMode is NavEditorLayerMode.GeneratedGraph or NavEditorLayerMode.Both)
        {
            DrawNavEditorGeneratedCompatGraph(cameraPosition);
        }

        for (var index = 0; index < _navEditorPlaybackSteps.Count; index += 1)
        {
            var playbackStep = _navEditorPlaybackSteps[index];
            var playbackColor = index == _navEditorPlaybackStepIndex
                ? new Color(255, 98, 92)
                : new Color(255, 124, 168, 180);
            var fromScreen = new Vector2(playbackStep.FromX - cameraPosition.X, playbackStep.FromY - cameraPosition.Y);
            var toScreen = new Vector2(playbackStep.ToX - cameraPosition.X, playbackStep.ToY - cameraPosition.Y);
            DrawNavEditorLine(fromScreen, toScreen, playbackColor, index == _navEditorPlaybackStepIndex ? 5f : 3f);
        }

        if (_navEditorLayerMode != NavEditorLayerMode.GeneratedGraph)
        {
            for (var index = 0; index < _navEditorAnchors.Count; index += 1)
            {
                var anchor = _navEditorAnchors[index];
                if (anchor.FallbackRole != BotNavigationHintFallbackRole.Entry
                    || !anchor.HasFallbackActivationZone
                    || !AppliesToNavEditorViewClass(anchor.Classes))
                {
                    continue;
                }

                var selected = index == _navEditorSelectedAnchorIndex;
                DrawNavEditorActivationZone(anchor, cameraPosition, selected);
            }

            if (_navEditorDraggingActivationZone)
            {
                DrawNavEditorActivationZonePreview(cameraPosition);
            }

            for (var index = 0; index < _navEditorLinks.Count; index += 1)
            {
                var link = _navEditorLinks[index];
                if (!AppliesToNavEditorViewClass(link.Classes))
                {
                    continue;
                }

                if (!TryGetNavEditorAnchorPosition(link.FromLabel, out var fromPosition)
                    || !TryGetNavEditorAnchorPosition(link.ToLabel, out var toPosition))
                {
                    continue;
                }

                var selected = index == _navEditorSelectedLinkIndex;
                var color = selected ? new Color(255, 215, 120) : GetNavEditorLinkColor(link.Traversal);
                var fromScreen = fromPosition - cameraPosition;
                var toScreen = toPosition - cameraPosition;
                DrawNavEditorLine(fromScreen, toScreen, color, selected ? 3f : NavEditorLinkThickness);

                if (IsNavEditorTraversalCaptureActive()
                    && string.Equals(link.FromLabel, _navEditorRecordingSourceLabel, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(link.ToLabel, _navEditorRecordingTargetLabel, StringComparison.OrdinalIgnoreCase))
                {
                    DrawNavEditorLine(fromScreen, toScreen, new Color(255, 98, 92), 4f);
                }

                var midpoint = (fromScreen + toScreen) * 0.5f;
                var timingTag = link.Traversal == BotNavigationHintTraversalKind.Jump && link.StartJumpImmediately ? " immediate" : string.Empty;
                var recordingTag = link.RecordedTraversals.Count > 0 ? $" rec:{string.Join("/", link.RecordedTraversals.Select(recording => BotNavigationClasses.GetShortLabel(recording.ClassId)))}" : string.Empty;
                _spriteBatch.DrawString(_consoleFont, $"{link.Traversal}{timingTag}{(link.Bidirectional ? " <>" : " ->")} x{link.CostMultiplier:F1}{recordingTag}", midpoint + new Vector2(6f, -10f), color);
            }

            for (var index = 0; index < _navEditorAnchors.Count; index += 1)
            {
                var anchor = _navEditorAnchors[index];
                if (!AppliesToNavEditorViewClass(anchor.Classes))
                {
                    continue;
                }

                var selected = index == _navEditorSelectedAnchorIndex;
                var pending = index == _navEditorPendingLinkAnchorIndex;
                var screenPosition = new Vector2(anchor.X - cameraPosition.X, anchor.Y - cameraPosition.Y);
                var rectangle = new Rectangle(
                    (int)MathF.Round(screenPosition.X - NavEditorAnchorHalfSize),
                    (int)MathF.Round(screenPosition.Y - NavEditorAnchorHalfSize),
                    (int)(NavEditorAnchorHalfSize * 2f),
                    (int)(NavEditorAnchorHalfSize * 2f));
                var fillColor = selected ? new Color(255, 215, 120) : pending ? new Color(255, 186, 92) : GetNavEditorAnchorColor(anchor);
                _spriteBatch.Draw(_pixel, rectangle, fillColor);
                var fallbackTag = anchor.FallbackRole == BotNavigationHintFallbackRole.None ? string.Empty : $" [{DescribeNavEditorFallbackRole(anchor.FallbackRole)}]";
                _spriteBatch.DrawString(_consoleFont, anchor.Label + fallbackTag, screenPosition + new Vector2(10f, -10f), new Color(236, 238, 242));
            }
        }

        if (_navEditorShowBotTags)
        {
            DrawNavEditorBotDiagnosticTags(cameraPosition);
        }

        DrawNavEditorTraversalPlaybackTag(cameraPosition);
    }

    private void DrawNavEditorGeneratedCompatGraph(Vector2 cameraPosition)
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            return;
        }

        var nodesById = asset.Nodes.ToDictionary(static node => node.Id);
        var edgeColor = _navEditorGraphEdgeDisplayMode switch
        {
            NavEditorGraphEdgeDisplayMode.Cyan => _navEditorLayerMode == NavEditorLayerMode.GeneratedGraph
                ? new Color(112, 232, 255, 180)
                : new Color(112, 232, 255, 128),
            NavEditorGraphEdgeDisplayMode.Red => _navEditorLayerMode == NavEditorLayerMode.GeneratedGraph
                ? new Color(255, 106, 106, 180)
                : new Color(255, 106, 106, 128),
            _ => _navEditorLayerMode == NavEditorLayerMode.GeneratedGraph
                ? new Color(92, 196, 255, 96)
                : new Color(92, 196, 255, 42),
        };
        var nodeColor = _navEditorLayerMode == NavEditorLayerMode.GeneratedGraph
            ? new Color(180, 228, 255, 150)
            : new Color(180, 228, 255, 72);
        var routeNodeIds = _navEditorGraphPreviewRouteNodeIds;
        var routeNodeSet = routeNodeIds.Length > 0
            ? new HashSet<int>(routeNodeIds)
            : new HashSet<int>();
        var selectedEdgeColor = new Color(255, 215, 120, _navEditorLayerMode == NavEditorLayerMode.GeneratedGraph ? 235 : 196);
        for (var edgeIndex = 0; edgeIndex < asset.Edges.Count; edgeIndex += 1)
        {
            var edge = asset.Edges[edgeIndex];
            if (!nodesById.TryGetValue(edge.FromNodeId, out var from)
                || !nodesById.TryGetValue(edge.ToNodeId, out var to))
            {
                continue;
            }

            var fromScreen = new Vector2(from.X - cameraPosition.X, from.Y - cameraPosition.Y);
            var toScreen = new Vector2(to.X - cameraPosition.X, to.Y - cameraPosition.Y);
            var selected = edge.FromNodeId == _navEditorSelectedGeneratedGraphEdgeFromNodeId
                && edge.ToNodeId == _navEditorSelectedGeneratedGraphEdgeToNodeId;
            var lineColor = selected ? selectedEdgeColor : edgeColor;
            var endpointSize = selected ? 6 : 4;
            DrawNavEditorLine(fromScreen, toScreen, lineColor, selected ? 3f : 1f);
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(
                    (int)MathF.Round(toScreen.X) - (endpointSize / 2),
                    (int)MathF.Round(toScreen.Y) - (endpointSize / 2),
                    endpointSize,
                    endpointSize),
                lineColor);
            if (selected)
            {
                var midpoint = (fromScreen + toScreen) * 0.5f;
                _spriteBatch.DrawString(_consoleFont, $"{edge.FromNodeId}->{edge.ToNodeId}", midpoint + new Vector2(6f, -10f), selectedEdgeColor);
            }
        }

        if (routeNodeIds.Length > 1)
        {
            var routeColor = new Color(255, 214, 120, _navEditorLayerMode == NavEditorLayerMode.GeneratedGraph ? 235 : 190);
            for (var index = 1; index < routeNodeIds.Length; index += 1)
            {
                if (!nodesById.TryGetValue(routeNodeIds[index - 1], out var from)
                    || !nodesById.TryGetValue(routeNodeIds[index], out var to))
                {
                    continue;
                }

                var fromScreen = new Vector2(from.X - cameraPosition.X, from.Y - cameraPosition.Y);
                var toScreen = new Vector2(to.X - cameraPosition.X, to.Y - cameraPosition.Y);
                DrawNavEditorLine(fromScreen, toScreen, routeColor, 4f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)MathF.Round(toScreen.X) - 3, (int)MathF.Round(toScreen.Y) - 3, 6, 6), routeColor);
            }
        }

        for (var nodeIndex = 0; nodeIndex < asset.Nodes.Count; nodeIndex += 1)
        {
            var node = asset.Nodes[nodeIndex];
            var screen = new Vector2(node.X - cameraPosition.X, node.Y - cameraPosition.Y);
            var selected = node.Id == _navEditorSelectedGeneratedGraphNodeId;
            var pendingEdgeStart = node.Id == _navEditorPendingGeneratedGraphSourceNodeId;
            var routeStart = node.Id == _navEditorGraphRouteStartNodeId;
            var routeGoal = node.Id == _navEditorGraphRouteGoalNodeId;
            var routeNode = routeNodeSet.Contains(node.Id);
            var halfSize = selected || routeStart || routeGoal || pendingEdgeStart ? 5 : routeNode ? 4 : 2;
            var fillColor = routeGoal
                ? new Color(255, 98, 92, 235)
                : routeStart
                    ? new Color(132, 255, 168, 235)
                    : pendingEdgeStart
                        ? new Color(255, 186, 92, 235)
                    : selected
                        ? new Color(255, 244, 170, 245)
                        : routeNode
                            ? new Color(255, 214, 120, 225)
                            : nodeColor;
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(
                    (int)MathF.Round(screen.X) - halfSize,
                    (int)MathF.Round(screen.Y) - halfSize,
                    halfSize * 2,
                    halfSize * 2),
                fillColor);
            if (_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph)
            {
                var label = node.Id.ToString(CultureInfo.InvariantCulture);
                if (routeStart)
                {
                    label += " S";
                }

                if (routeGoal)
                {
                    label += " G";
                }

                if (pendingEdgeStart)
                {
                    label += " E";
                }

                var labelColor = selected || routeStart || routeGoal || routeNode || pendingEdgeStart
                    ? new Color(255, 244, 196, 235)
                    : new Color(196, 232, 255, 180);
                _spriteBatch.DrawString(_consoleFont, label, screen + new Vector2(4f, -8f), labelColor);
            }
        }
    }

    private void DrawNavEditorActivationZone(NavEditorAnchor anchor, Vector2 cameraPosition, bool selected)
    {
        if (!TryGetNavEditorActivationZoneRectangle(anchor, cameraPosition, out var rect))
        {
            return;
        }

        var fill = selected
            ? new Color(255, 214, 120, 36)
            : new Color(92, 196, 255, 24);
        var outline = selected
            ? new Color(255, 214, 120, 210)
            : new Color(92, 196, 255, 150);
        _spriteBatch.Draw(_pixel, rect, fill);
        DrawNavEditorRectangleOutline(rect, outline, selected ? 2 : 1);
        if (selected)
        {
            _spriteBatch.DrawString(_consoleFont, "activation zone", new Vector2(rect.X + 4f, rect.Y + 4f), outline);
        }
    }

    private void DrawNavEditorActivationZonePreview(Vector2 cameraPosition)
    {
        var minX = MathF.Min(_navEditorActivationZoneStart.X, _navEditorActivationZoneCurrent.X) - cameraPosition.X;
        var minY = MathF.Min(_navEditorActivationZoneStart.Y, _navEditorActivationZoneCurrent.Y) - cameraPosition.Y;
        var maxX = MathF.Max(_navEditorActivationZoneStart.X, _navEditorActivationZoneCurrent.X) - cameraPosition.X;
        var maxY = MathF.Max(_navEditorActivationZoneStart.Y, _navEditorActivationZoneCurrent.Y) - cameraPosition.Y;
        var rect = new Rectangle(
            (int)MathF.Round(minX),
            (int)MathF.Round(minY),
            Math.Max(1, (int)MathF.Round(maxX - minX)),
            Math.Max(1, (int)MathF.Round(maxY - minY)));
        _spriteBatch.Draw(_pixel, rect, new Color(255, 214, 120, 28));
        DrawNavEditorRectangleOutline(rect, new Color(255, 214, 120, 230), 2);
    }

    private static bool TryGetNavEditorActivationZoneRectangle(NavEditorAnchor anchor, Vector2 cameraPosition, out Rectangle rect)
    {
        rect = default;
        if (!anchor.HasFallbackActivationZone)
        {
            return false;
        }

        var minX = MathF.Min(anchor.FallbackActivationMinX, anchor.FallbackActivationMaxX);
        var minY = MathF.Min(anchor.FallbackActivationMinY, anchor.FallbackActivationMaxY);
        var maxX = MathF.Max(anchor.FallbackActivationMinX, anchor.FallbackActivationMaxX);
        var maxY = MathF.Max(anchor.FallbackActivationMinY, anchor.FallbackActivationMaxY);
        if (maxX - minX < 1f || maxY - minY < 1f)
        {
            return false;
        }

        rect = new Rectangle(
            (int)MathF.Round(minX - cameraPosition.X),
            (int)MathF.Round(minY - cameraPosition.Y),
            Math.Max(1, (int)MathF.Round(maxX - minX)),
            Math.Max(1, (int)MathF.Round(maxY - minY)));
        return true;
    }

    private void DrawNavEditorBotDiagnosticTags(Vector2 cameraPosition)
    {
        var snapshot = _practiceBotController.LastDiagnostics;
        _ = TryGetNavEditorGeneratedGraphDiagnosticsNavPoints(out var navPoints);
        for (var index = 0; index < snapshot.Entries.Count; index += 1)
        {
            var entry = snapshot.Entries[index];
            if (!_world.TryGetNetworkPlayer(entry.Slot, out var player) || !player.IsAlive)
            {
                continue;
            }

            var screen = new Vector2(player.X - cameraPosition.X, player.Y - cameraPosition.Y - 42f);
            var tag = BuildNavEditorBotTag(entry, navPoints);
            var size = _consoleFont.MeasureString(tag);
            var rect = new Rectangle(
                (int)MathF.Round(screen.X - (size.X * 0.5f) - 6f),
                (int)MathF.Round(screen.Y - 4f),
                (int)MathF.Round(size.X + 12f),
                (int)MathF.Round(size.Y + 8f));
            _spriteBatch.Draw(_pixel, rect, new Color(10, 12, 16, 190));
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(255, 214, 120, 220));
            _spriteBatch.DrawString(_consoleFont, tag, new Vector2(rect.X + 6f, rect.Y + 4f), new Color(236, 238, 242));
        }
    }

    private bool TryGetNavEditorGeneratedGraphDiagnosticsNavPoints(out ClientBotNavPoints navPoints)
    {
        navPoints = default!;
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            return false;
        }

        if (!ReferenceEquals(_navEditorGeneratedCompatGraphDiagnosticsAsset, asset)
            || _navEditorGeneratedCompatGraphDiagnosticsNavPoints is null)
        {
            try
            {
                _navEditorGeneratedCompatGraphDiagnosticsNavPoints = ClientBotNavPoints.Build(asset, _world.Level);
                _navEditorGeneratedCompatGraphDiagnosticsAsset = asset;
            }
            catch
            {
                _navEditorGeneratedCompatGraphDiagnosticsAsset = asset;
                _navEditorGeneratedCompatGraphDiagnosticsNavPoints = null;
            }
        }

        navPoints = _navEditorGeneratedCompatGraphDiagnosticsNavPoints!;
        return navPoints is not null;
    }

    private void DrawNavEditorTraversalPlaybackTag(Vector2 cameraPosition)
    {
        if (!TryGetCurrentNavEditorPlaybackStep(out var step))
        {
            return;
        }

        var screen = new Vector2(_world.LocalPlayer.X - cameraPosition.X, _world.LocalPlayer.Y - cameraPosition.Y - 64f);
        var tag = string.Create(
            CultureInfo.InvariantCulture,
            $"test:{_navEditorPlaybackLabel} {step.FromLabel}->{step.ToLabel} {step.Kind} {_navEditorPlaybackStepIndex + 1}/{_navEditorPlaybackSteps.Count}");
        var size = _consoleFont.MeasureString(tag);
        var rect = new Rectangle(
            (int)MathF.Round(screen.X - (size.X * 0.5f) - 6f),
            (int)MathF.Round(screen.Y - 4f),
            (int)MathF.Round(size.X + 12f),
            (int)MathF.Round(size.Y + 8f));
        _spriteBatch.Draw(_pixel, rect, new Color(10, 12, 16, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(255, 214, 120, 235));
        _spriteBatch.DrawString(_consoleFont, tag, new Vector2(rect.X + 6f, rect.Y + 4f), new Color(255, 244, 196));
    }

    private static string BuildNavEditorBotTag(BotControllerDiagnosticsEntry entry, ClientBotNavPoints? navPoints)
    {
        var route = string.IsNullOrWhiteSpace(entry.FallbackTriggerLabel)
            ? entry.RouteLabel
            : $"fallback:{entry.FallbackTriggerLabel}";
        var currentWeightLabel = "?";
        if (navPoints is not null
            && entry.RouteGoalNodeId >= 0
            && entry.CurrentPointId >= 0)
        {
            var goalWeights = navPoints.GetGoalWeights(entry.RouteGoalNodeId, maximumDepth: 130);
            if (goalWeights is not null
                && entry.CurrentPointId < goalWeights.Length)
            {
                currentWeightLabel = goalWeights[entry.CurrentPointId].ToString(CultureInfo.InvariantCulture);
            }
        }

        var issueLabel = string.IsNullOrWhiteSpace(entry.NavigationIssueLabel)
            ? string.Empty
            : $" issue={entry.NavigationIssueLabel} br={entry.BranchTicks}/{entry.BranchNoProgressTicks} dt={entry.DirectTargetTicks}/{entry.DirectTargetNoProgressTicks}";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{route} cp={entry.CurrentPointId} np={entry.NextPointId} goal={entry.RouteGoalNodeId} w={currentWeightLabel} nn={entry.NoNextPointTicks} st={entry.ModernStuckTicks}{issueLabel}");
    }

    private void DrawNavEditorPanel(MouseState mouse)
    {
        var layout = GetNavEditorPanelLayout();
        _spriteBatch.Draw(_pixel, layout.Panel, new Color(18, 20, 24, 220));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, 2), new Color(92, 196, 255));
        _spriteBatch.Draw(_pixel, layout.Header, new Color(24, 28, 34, 230));
        DrawNavEditorButton(layout.CollapseButton, _navEditorPanelCollapsed ? "+" : "-", false, true, mouse);

        var textPosition = new Vector2(layout.Panel.X + NavEditorPanelPadding, layout.Panel.Y + NavEditorPanelPadding);
        _spriteBatch.DrawString(_consoleFont, $"BOT NAV EDITOR {(_navEditorDirty ? "*dirty*" : "saved")}", textPosition, new Color(236, 238, 242));
        textPosition.Y += 18f;
        _spriteBatch.DrawString(_consoleFont, $"{_world.Level.Name} area {_world.Level.MapAreaIndex}", textPosition, new Color(180, 186, 192));
        textPosition.Y += 18f;
        var status = !string.IsNullOrWhiteSpace(_navEditorRebuildStatus) && _navEditorRebuildTask is not null && !_navEditorRebuildTask.IsCompleted
            ? _navEditorRebuildStatus
            : _navEditorStatusSecondsRemaining > 0f ? _navEditorStatusMessage : string.Empty;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusMaxWidth = layout.CollapseButton.X - textPosition.X - 8f;
            foreach (var line in GetWrappedNavEditorStatusLines(status, statusMaxWidth))
            {
                _spriteBatch.DrawString(_consoleFont, line, textPosition, new Color(255, 214, 140));
                textPosition.Y += 18f;
            }
        }

        if (_navEditorPanelCollapsed)
        {
            return;
        }

        var graphToolLabels = _navEditorLayerMode == NavEditorLayerMode.GeneratedGraph;
        DrawNavEditorButton(layout.SelectToolButton, "Select", _navEditorTool == NavEditorTool.Select, true, mouse);
        DrawNavEditorButton(layout.AnchorToolButton, graphToolLabels ? "Node" : "Anchor", _navEditorTool == NavEditorTool.AddAnchor, true, mouse);
        DrawNavEditorButton(layout.LinkToolButton, graphToolLabels ? "Edge" : "Link", _navEditorTool == NavEditorTool.AddLink, true, mouse);
        DrawNavEditorButton(layout.SaveButton, "Save All (F7)", false, true, mouse);
        DrawNavEditorButton(layout.RebuildButton, "Rebuild (F8)", false, _navEditorRebuildTask is null, mouse);
        DrawNavEditorButton(layout.ReloadButton, "Reload (F5)", false, true, mouse);
        var canEditHintLayer = _navEditorLayerMode != NavEditorLayerMode.GeneratedGraph;
        DrawNavEditorButton(layout.RenameButton, "Rename", false, canEditHintLayer && _navEditorSelectedAnchorIndex >= 0 && !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive(), mouse);
        var canDeleteGraphSelection = (_navEditorLayerMode is NavEditorLayerMode.GeneratedGraph or NavEditorLayerMode.Both)
            && (_navEditorSelectedGeneratedGraphNodeId >= 0 || (_navEditorSelectedGeneratedGraphEdgeFromNodeId >= 0 && _navEditorSelectedGeneratedGraphEdgeToNodeId >= 0));
        DrawNavEditorButton(layout.DeleteButton, "Delete", false, (canEditHintLayer && (_navEditorSelectedAnchorIndex >= 0 || _navEditorSelectedLinkIndex >= 0) || canDeleteGraphSelection) && !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive(), mouse);
        DrawNavEditorButton(layout.ClearAllButton, "Clear All", false, canEditHintLayer && (_navEditorAnchors.Count > 0 || _navEditorLinks.Count > 0) && !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive(), mouse);
        DrawNavEditorButton(layout.RecordButton, "Rec. Traversal", false, canEditHintLayer && _navEditorSelectedLinkIndex >= 0 && !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive(), mouse);
        DrawNavEditorButton(layout.RecordProfileButton, BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass), false, !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive(), mouse);
        DrawNavEditorButton(layout.ClearRecordingButton, "Clear Rec", false, canEditHintLayer && _navEditorSelectedLinkIndex >= 0 && SelectedNavEditorLinkHasRecordingForClass(_navEditorRecordTestClass) && !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive(), mouse);
        DrawNavEditorButton(layout.TestLinkButton, "Test Link", false, canEditHintLayer && _navEditorSelectedLinkIndex >= 0 && !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive(), mouse);
        var canTestHintRoute = canEditHintLayer
            && (_navEditorSelectedAnchorIndex >= 0 || _navEditorSelectedLinkIndex >= 0)
            && !IsNavEditorTraversalCaptureActive()
            && !IsNavEditorTraversalPlaybackActive();
        var canTestGraphRoute = ShouldUseNavEditorGeneratedGraphRoutePlayback()
            && CanStartNavEditorGeneratedGraphRoutePlayback();
        DrawNavEditorButton(layout.TestRouteButton, "Test Route", false, canTestHintRoute || canTestGraphRoute, mouse);
        DrawNavEditorButton(layout.StopTestButton, "Stop Test", false, IsNavEditorTraversalCaptureActive() || IsNavEditorTraversalPlaybackActive(), mouse);
        DrawNavEditorButton(layout.ViewClassButton, $"View: {DescribeNavEditorViewClass(_navEditorViewClass)}", false, true, mouse);
        DrawNavEditorButton(layout.ClassScopeButton, $"New Profile: {DescribeNavEditorClasses(_navEditorDefaultClasses)}", false, true, mouse);
        DrawNavEditorButton(layout.LayerButton, $"Layer: {DescribeNavEditorLayerMode(_navEditorLayerMode)}", false, true, mouse);
        var canControlScoreTrio = !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive();
        DrawNavEditorButton(layout.BotTagsButton, _navEditorShowBotTags ? "Bot Tags: On" : "Bot Tags: Off", false, true, mouse);
        DrawNavEditorButton(layout.EdgeDisplayButton, $"Edges: {DescribeNavEditorGraphEdgeDisplayMode(_navEditorGraphEdgeDisplayMode)}", false, true, mouse);
        DrawNavEditorButton(layout.RunScoreTrioButton, _navEditorScoreTrioActive ? "Respawn Trio" : "Run Trio", _navEditorScoreTrioActive, canControlScoreTrio, mouse);
        DrawNavEditorButton(layout.ClearScoreTrioButton, "Clear Trio", false, _navEditorScoreTrioActive && canControlScoreTrio, mouse);
        var anchorControlsEnabled = _navEditorLayerMode != NavEditorLayerMode.GeneratedGraph;
        DrawNavEditorButton(layout.AnchorKindButton, $"New Role: {DescribeNavEditorAnchorKind(_navEditorDefaultAnchorKind)}", false, anchorControlsEnabled, mouse);
        DrawNavEditorButton(
            layout.AnchorTeamButton,
            $"New Team: {DescribeNavEditorAnchorTeam(_navEditorDefaultAnchorTeam)}",
            false,
            anchorControlsEnabled && CanNavEditorAnchorKindUseTeam(_navEditorDefaultAnchorKind),
            mouse);
        DrawNavEditorButton(layout.FallbackRoleButton, $"Fallback: {DescribeNavEditorFallbackRole(GetNavEditorSelectedFallbackRoleOrDefault())}", false, anchorControlsEnabled, mouse);
        DrawNavEditorButton(layout.FallbackTriggerButton, $"Trigger: {DescribeNavEditorFallbackTriggers(GetNavEditorSelectedFallbackTriggersOrDefault())}", false, anchorControlsEnabled, mouse);
        DrawNavEditorButton(layout.FallbackCarryButton, $"Carry: {DescribeNavEditorCarryRequirement(GetNavEditorSelectedCarryRequirementOrDefault())}", false, anchorControlsEnabled, mouse);

        var linkControlsEnabled = _navEditorLayerMode != NavEditorLayerMode.GeneratedGraph
            && !IsNavEditorTraversalCaptureActive()
            && !IsNavEditorTraversalPlaybackActive();
        var directionControlsEnabled = !IsNavEditorTraversalCaptureActive() && !IsNavEditorTraversalPlaybackActive();
        var directionLabel = _navEditorLayerMode == NavEditorLayerMode.HintLayer
            ? (_navEditorDefaultBidirectional ? "New Direction: Two-way" : "New Direction: One-way")
            : (_navEditorDefaultBidirectional ? "Graph Direction: Two-way" : "Graph Direction: Directed");
        DrawNavEditorButton(layout.TraversalButton, $"New Traversal: {_navEditorDefaultTraversal}", false, linkControlsEnabled, mouse);
        DrawNavEditorButton(
            layout.DirectionButton,
            directionLabel,
            false,
            directionControlsEnabled,
            mouse);
        DrawNavEditorButton(layout.CostDownButton, "-", false, linkControlsEnabled, mouse);
        DrawNavEditorButton(layout.CostUpButton, "+", false, linkControlsEnabled, mouse);
        _spriteBatch.DrawString(_consoleFont, $"New Cost x{_navEditorDefaultCostMultiplier:F1}", new Vector2(layout.CostDownButton.Right + 10f, layout.CostDownButton.Y + 6f), linkControlsEnabled ? new Color(236, 238, 242) : new Color(132, 138, 144));

        var detailPosition = new Vector2(layout.Panel.X + NavEditorPanelPadding, layout.CostDownButton.Bottom + 18f);
        foreach (var line in BuildNavEditorDetailLines())
        {
            _spriteBatch.DrawString(_consoleFont, line, detailPosition, new Color(204, 210, 216));
            detailPosition.Y += 18f;
        }

        if (_navEditorClearAllConfirmationOpen)
        {
            DrawNavEditorClearAllPrompt(mouse, layout);
        }
    }

    private List<string> BuildNavEditorDetailLines()
    {
        var lines = new List<string>
        {
            $"hints={_navEditorAnchors.Count} nodes / {_navEditorLinks.Count} links",
            _navEditorGeneratedCompatGraphStatus,
            $"graph add mode={(_navEditorDefaultBidirectional ? "two-way if valid" : "directed")} display={DescribeNavEditorGraphEdgeDisplayMode(_navEditorGraphEdgeDisplayMode)}",
            _navEditorScoreTrioActive
                ? $"score trio active: {_world.LocalPlayerTeam} {DescribeNavEditorScoreTrioClasses()} from spawn (current state)"
                : $"score trio ready: Run Trio spawns {DescribeNavEditorScoreTrioClasses()} from local spawn",
        };

        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            var anchor = _navEditorAnchors[_navEditorSelectedAnchorIndex];
            lines.Add($"selected {anchor.Label}: {DescribeNavEditorAnchorRole(anchor.Kind, anchor.Team)} fallback={DescribeNavEditorFallbackRole(anchor.FallbackRole)}");
            if (anchor.FallbackRole == BotNavigationHintFallbackRole.Entry)
            {
                var activation = anchor.HasFallbackActivationZone
                    ? $"zone={MathF.Abs(anchor.FallbackActivationMaxX - anchor.FallbackActivationMinX):F0}x{MathF.Abs(anchor.FallbackActivationMaxY - anchor.FallbackActivationMinY):F0}"
                    : $"radius={GetNavEditorFallbackRadius(anchor):F0}";
                lines.Add($"entry trigger={DescribeNavEditorFallbackTriggers(anchor.FallbackTriggers)} {activation} carry={DescribeNavEditorCarryRequirement(anchor.RequiresCarryingIntel)}");
            }
        }
        else if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            var link = _navEditorLinks[_navEditorSelectedLinkIndex];
            var timing = link.Traversal == BotNavigationHintTraversalKind.Jump
                ? $" jumpTiming={(link.StartJumpImmediately ? "immediate" : "delayed")}"
                : string.Empty;
            lines.Add($"selected link: {link.FromLabel} -> {link.ToLabel} {link.Traversal}{timing} {(link.Bidirectional ? "two-way" : "one-way")}");
        }
        else if (_navEditorSelectedGeneratedGraphNodeId >= 0
            && TryGetNavEditorGeneratedGraphNode(_navEditorSelectedGeneratedGraphNodeId, out var graphNode))
        {
            var outgoingCount = _navEditorGeneratedCompatGraph?.Edges.Count(edge => edge.FromNodeId == graphNode.Id) ?? 0;
            var incomingCount = _navEditorGeneratedCompatGraph?.Edges.Count(edge => edge.ToNodeId == graphNode.Id) ?? 0;
            lines.Add($"selected graph node {graphNode.Id}: ({graphNode.X:F0}, {graphNode.Y:F0}) {graphNode.Kind} out={outgoingCount} in={incomingCount}");
            var outgoingLabels = _navEditorGeneratedCompatGraph?.Edges
                .Where(edge => edge.FromNodeId == graphNode.Id)
                .OrderBy(edge => edge.ToNodeId)
                .Take(8)
                .Select(edge => $"{edge.ToNodeId}:{edge.Kind}")
                .ToArray() ?? Array.Empty<string>();
            if (outgoingLabels.Length > 0)
            {
                var suffix = outgoingCount > outgoingLabels.Length ? " ..." : string.Empty;
                lines.Add($"outgoing: {string.Join(", ", outgoingLabels)}{suffix}");
            }
            lines.Add($"edge start={DescribeNavEditorGraphRouteEndpoint(_navEditorPendingGeneratedGraphSourceNodeId)}");
            lines.Add($"graph route start={DescribeNavEditorGraphRouteEndpoint(_navEditorGraphRouteStartNodeId)} goal={DescribeNavEditorGraphRouteEndpoint(_navEditorGraphRouteGoalNodeId)}");
        }
        else if (_navEditorSelectedGeneratedGraphEdgeFromNodeId >= 0
            && _navEditorSelectedGeneratedGraphEdgeToNodeId >= 0
            && TryGetNavEditorGeneratedGraphEdge(_navEditorSelectedGeneratedGraphEdgeFromNodeId, _navEditorSelectedGeneratedGraphEdgeToNodeId, out var graphEdge))
        {
            lines.Add($"selected graph edge: {graphEdge.FromNodeId} -> {graphEdge.ToNodeId} {graphEdge.Kind} cost={graphEdge.Cost:0.##}");
            lines.Add($"reverse edge={(TryGetNavEditorGeneratedGraphEdge(graphEdge.ToNodeId, graphEdge.FromNodeId, out _) ? "present" : "missing")}");
            lines.Add($"edge start={DescribeNavEditorGraphRouteEndpoint(_navEditorPendingGeneratedGraphSourceNodeId)}");
        }
        else
        {
            lines.Add(_navEditorLayerMode == NavEditorLayerMode.GeneratedGraph
                ? _navEditorTool switch
                {
                    NavEditorTool.AddAnchor => "Node tool adds a surface node on valid ground",
                    NavEditorTool.AddLink => _navEditorPendingGeneratedGraphSourceNodeId >= 0
                        ? $"Edge tool: start={_navEditorPendingGeneratedGraphSourceNodeId}; click a destination node to toggle {(_navEditorDefaultBidirectional ? "a two-way seam when valid" : "a directed seam")}"
                        : $"Edge tool: click one graph node, then another, to toggle {(_navEditorDefaultBidirectional ? "a two-way seam when valid" : "a directed seam")}",
                    _ => "Select picks seams or drags graph nodes; Delete removes the selected seam or node"
                }
                : "right-click map to place fallback nodes; Ctrl+right-drag draws an activation zone");
        }

        if ((_navEditorGraphRouteStartNodeId >= 0 || _navEditorGraphRouteGoalNodeId >= 0)
            && _navEditorSelectedGeneratedGraphNodeId < 0)
        {
            lines.Add($"graph route start={DescribeNavEditorGraphRouteEndpoint(_navEditorGraphRouteStartNodeId)} goal={DescribeNavEditorGraphRouteEndpoint(_navEditorGraphRouteGoalNodeId)}");
        }

        if (_navEditorGraphPreviewRouteNodeIds.Length > 1)
        {
            lines.Add($"graph preview: {_navEditorGraphPreviewRouteNodeIds.Length} nodes / {_navEditorGraphPreviewRouteNodeIds.Length - 1} links");
        }

        return lines;
    }

    private static string DescribeNavEditorGraphRouteEndpoint(int nodeId)
    {
        return nodeId >= 0
            ? nodeId.ToString(CultureInfo.InvariantCulture)
            : "unset";
    }

    private IReadOnlyList<string> GetWrappedNavEditorStatusLines(string status, float maxWidth)
    {
        var lines = new List<string>();
        AppendWrappedConsoleLines(lines, status, maxWidth);
        if (lines.Count <= 2)
        {
            return lines;
        }

        var trimmed = lines.Take(2).ToArray();
        trimmed[1] = $"{trimmed[1].TrimEnd()}...";
        return trimmed;
    }

    private NavEditorPanelLayout GetNavEditorPanelLayout()
    {
        EnsureNavEditorPanelPositionInitialized();
        var panelSize = GetNavEditorPanelSize();
        _navEditorPanelPosition = ClampNavEditorPanelPosition(_navEditorPanelPosition, panelSize);
        var panel = new Rectangle(
            _navEditorPanelPosition.X,
            _navEditorPanelPosition.Y,
            panelSize.X,
            panelSize.Y);
        var header = new Rectangle(panel.X, panel.Y, panel.Width, NavEditorPanelHeaderHeight);
        var collapseButton = new Rectangle(
            panel.Right - NavEditorPanelPadding - NavEditorPanelHeaderButtonSize,
            panel.Y + NavEditorPanelPadding,
            NavEditorPanelHeaderButtonSize,
            NavEditorPanelHeaderButtonSize);
        var contentX = panel.X + NavEditorPanelPadding;
        var contentWidth = panel.Width - (NavEditorPanelPadding * 2);
        var y = panel.Y + NavEditorPanelHeaderHeight + NavEditorButtonGap;

        var toolRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 3);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var actionRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 3);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var itemRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 3);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var recordRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 3);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var testRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 3);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var classRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 2);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var layerRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 3);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var scoreSimRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 2);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var anchorKindRect = new Rectangle(contentX, y, contentWidth, NavEditorButtonHeight);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var anchorTeamRect = new Rectangle(contentX, y, contentWidth, NavEditorButtonHeight);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var fallbackRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 3);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var traversalRect = new Rectangle(contentX, y, contentWidth, NavEditorButtonHeight);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var directionRect = new Rectangle(contentX, y, contentWidth, NavEditorButtonHeight);
        y += NavEditorButtonHeight + NavEditorButtonGap;
        var costRow = CreateNavEditorButtonRow(contentX, y, contentWidth, 5);

        return new NavEditorPanelLayout(
            panel,
            header,
            collapseButton,
            toolRow[0],
            toolRow[1],
            toolRow[2],
            actionRow[0],
            actionRow[1],
            actionRow[2],
            itemRow[0],
            itemRow[1],
            itemRow[2],
            recordRow[0],
            recordRow[1],
            recordRow[2],
            testRow[0],
            testRow[1],
            testRow[2],
            classRow[0],
            classRow[1],
            layerRow[0],
            layerRow[1],
            layerRow[2],
            scoreSimRow[0],
            scoreSimRow[1],
            anchorKindRect,
            anchorTeamRect,
            fallbackRow[0],
            fallbackRow[1],
            fallbackRow[2],
            traversalRect,
            directionRect,
            costRow[0],
            costRow[1]);
    }

    private static Rectangle[] CreateNavEditorButtonRow(int x, int y, int width, int columns)
    {
        var buttonWidth = (width - ((columns - 1) * NavEditorButtonGap)) / columns;
        var rectangles = new Rectangle[columns];
        for (var index = 0; index < columns; index += 1)
        {
            rectangles[index] = new Rectangle(x + (index * (buttonWidth + NavEditorButtonGap)), y, buttonWidth, NavEditorButtonHeight);
        }

        return rectangles;
    }

    private static bool IsNavEditorControlDown(KeyboardState keyboard)
    {
        return keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
    }

    private void EnsureNavEditorPanelPositionInitialized()
    {
        if (_navEditorPanelPositionInitialized)
        {
            return;
        }

        _navEditorPanelPositionInitialized = true;
        _navEditorPanelPosition = GetDefaultNavEditorPanelPosition();
    }

    private Point GetDefaultNavEditorPanelPosition()
    {
        var size = GetNavEditorPanelSize();
        var hostBounds = GetNavEditorPanelHostBounds();
        return ClampNavEditorPanelPosition(
            new Point(
                hostBounds.Right - size.X - NavEditorPanelMargin,
                ShouldUseNavEditorWindowGutter()
                    ? hostBounds.Y + NavEditorPanelMargin
                    : (_consoleOpen ? 210 : NavEditorPanelMargin)),
            size);
    }

    private Point GetNavEditorPanelSize()
    {
        return new Point(
            NavEditorPanelWidth,
            _navEditorPanelCollapsed ? NavEditorPanelCollapsedHeight : NavEditorPanelExpandedHeight);
    }

    private Point ClampNavEditorPanelPosition(Point position, Point size)
    {
        var hostBounds = GetNavEditorPanelHostBounds();
        var minX = hostBounds.X + NavEditorPanelMargin;
        var minY = hostBounds.Y + NavEditorPanelMargin;
        var maxX = Math.Max(minX, hostBounds.Right - size.X - NavEditorPanelMargin);
        var maxY = Math.Max(minY, hostBounds.Bottom - size.Y - NavEditorPanelMargin);
        return new Point(
            Math.Clamp(position.X, minX, maxX),
            Math.Clamp(position.Y, minY, maxY));
    }

    private Rectangle GetNavEditorPanelHostBounds()
    {
        if (!ShouldUseNavEditorWindowGutter())
        {
            return new Rectangle(0, 0, ViewportWidth, ViewportHeight);
        }

        var surfaceBounds = GetNavEditorWindowSurfaceBounds();
        var gameplayBounds = GetGameplayDestinationRectangle(surfaceBounds.Width, surfaceBounds.Height);
        return new Rectangle(
            gameplayBounds.Right,
            0,
            Math.Max(0, surfaceBounds.Width - gameplayBounds.Right),
            surfaceBounds.Height);
    }

    private Rectangle GetNavEditorWindowSurfaceBounds()
    {
        var clientBounds = Window.ClientBounds;
        if (clientBounds.Width > 0 && clientBounds.Height > 0)
        {
            return new Rectangle(0, 0, clientBounds.Width, clientBounds.Height);
        }

        var viewport = GraphicsDevice.Viewport;
        return new Rectangle(0, 0, viewport.Width, viewport.Height);
    }

    private bool IsNavEditorPanelHostHit(Point position)
    {
        return ShouldUseNavEditorWindowGutter() && GetNavEditorPanelHostBounds().Contains(position);
    }

    private void BeginNavEditorPanelDrag(MouseState mouse)
    {
        _navEditorPanelDragging = true;
        _navEditorPanelDragOffset = mouse.Position - _navEditorPanelPosition;
        SetNavEditorStatus("drag the panel to reposition it");
    }

    private void UpdateNavEditorPanelDrag(MouseState mouse)
    {
        var targetPosition = mouse.Position - _navEditorPanelDragOffset;
        _navEditorPanelPosition = ClampNavEditorPanelPosition(targetPosition, GetNavEditorPanelSize());
    }

    private void ToggleNavEditorPanelCollapsed()
    {
        _navEditorPanelCollapsed = !_navEditorPanelCollapsed;
        _navEditorPanelDragging = false;
        _navEditorPanelPosition = ClampNavEditorPanelPosition(_navEditorPanelPosition, GetNavEditorPanelSize());
        SetNavEditorStatus(_navEditorPanelCollapsed ? "nav editor collapsed (press F9 or click + to expand)" : "nav editor expanded");
    }

    private bool TryHandleNavEditorClearAllPromptClick(MouseState mouse, NavEditorPanelLayout panelLayout)
    {
        var promptLayout = GetNavEditorClearAllPromptLayout(panelLayout);
        if (promptLayout.YesButton.Contains(mouse.Position))
        {
            ConfirmNavEditorClearAll();
            return true;
        }

        if (promptLayout.NoButton.Contains(mouse.Position))
        {
            CancelNavEditorClearAll();
            return true;
        }

        return promptLayout.Dialog.Contains(mouse.Position);
    }

    private void DrawNavEditorClearAllPrompt(MouseState mouse, NavEditorPanelLayout panelLayout)
    {
        var promptLayout = GetNavEditorClearAllPromptLayout(panelLayout);
        _spriteBatch.Draw(_pixel, promptLayout.Dialog, new Color(12, 14, 18, 238));
        _spriteBatch.Draw(_pixel, new Rectangle(promptLayout.Dialog.X, promptLayout.Dialog.Y, promptLayout.Dialog.Width, 2), new Color(255, 184, 120));
        _spriteBatch.DrawString(
            _consoleFont,
            "Are you sure you want to delete all nodes?",
            new Vector2(promptLayout.Dialog.X + 12f, promptLayout.Dialog.Y + 12f),
            new Color(236, 238, 242));
        DrawNavEditorButton(promptLayout.YesButton, "Yes", false, true, mouse);
        DrawNavEditorButton(promptLayout.NoButton, "No", false, true, mouse);
    }

    private static NavEditorClearAllPromptLayout GetNavEditorClearAllPromptLayout(NavEditorPanelLayout panelLayout)
    {
        var dialog = new Rectangle(
            panelLayout.Panel.X + NavEditorPanelPadding,
            panelLayout.Panel.Bottom - NavEditorPanelPadding - NavEditorClearAllPromptHeight,
            panelLayout.Panel.Width - (NavEditorPanelPadding * 2),
            NavEditorClearAllPromptHeight);
        var buttons = CreateNavEditorButtonRow(
            dialog.X + NavEditorPanelPadding,
            dialog.Bottom - NavEditorPanelPadding - NavEditorButtonHeight,
            dialog.Width - (NavEditorPanelPadding * 2),
            2);
        return new NavEditorClearAllPromptLayout(dialog, buttons[0], buttons[1]);
    }

    private void DrawNavEditorButton(Rectangle bounds, string label, bool active, bool enabled, MouseState mouse)
    {
        var hovered = bounds.Contains(mouse.Position);
        var background = !enabled
            ? new Color(48, 52, 58, 180)
            : active
                ? new Color(92, 196, 255, 220)
                : hovered
                    ? new Color(52, 66, 78, 220)
                    : new Color(36, 40, 46, 220);
        var textColor = !enabled
            ? new Color(132, 138, 144)
            : active
                ? new Color(12, 16, 20)
                : new Color(236, 238, 242);
        _spriteBatch.Draw(_pixel, bounds, background);
        _spriteBatch.DrawString(_consoleFont, label, new Vector2(bounds.X + 8f, bounds.Y + 6f), textColor);
    }

    private void DrawNavEditorLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.1f)
        {
            return;
        }

        var angle = MathF.Atan2(delta.Y, delta.X);
        _spriteBatch.Draw(_pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private void DrawNavEditorRectangleOutline(Rectangle rect, Color color, int thickness)
    {
        var lineThickness = Math.Max(1, thickness);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, lineThickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - lineThickness, rect.Width, lineThickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, lineThickness, rect.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - lineThickness, rect.Y, lineThickness, rect.Height), color);
    }

    private PlayerClass[] GetNavEditorSelectedClassesOrDefault()
    {
        if (_navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count)
        {
            return _navEditorAnchors[_navEditorSelectedAnchorIndex].Classes;
        }

        if (_navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count)
        {
            return _navEditorLinks[_navEditorSelectedLinkIndex].Classes;
        }

        return _navEditorDefaultClasses;
    }

    private PlayerClass GetNavEditorPreviewClass()
    {
        if (_navEditorViewClass.HasValue)
        {
            return _navEditorViewClass.Value;
        }

        var classes = GetNavEditorSelectedClassesOrDefault();
        return classes.Length == 1 ? classes[0] : PlayerClass.Soldier;
    }

    private BotNavigationNodeKind GetNavEditorSelectedAnchorKindOrDefault()
    {
        return _navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count
            ? _navEditorAnchors[_navEditorSelectedAnchorIndex].Kind
            : _navEditorDefaultAnchorKind;
    }

    private PlayerTeam? GetNavEditorSelectedAnchorTeamOrDefault()
    {
        return _navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count
            ? NormalizeNavEditorAnchorTeam(_navEditorAnchors[_navEditorSelectedAnchorIndex].Kind, _navEditorAnchors[_navEditorSelectedAnchorIndex].Team)
            : NormalizeNavEditorAnchorTeam(_navEditorDefaultAnchorKind, _navEditorDefaultAnchorTeam);
    }

    private BotNavigationHintTraversalKind GetNavEditorSelectedTraversalOrDefault()
    {
        return _navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count
            ? _navEditorLinks[_navEditorSelectedLinkIndex].Traversal
            : _navEditorDefaultTraversal;
    }

    private bool GetNavEditorSelectedBidirectionalOrDefault()
    {
        return _navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count
            ? _navEditorLinks[_navEditorSelectedLinkIndex].Bidirectional
            : _navEditorDefaultBidirectional;
    }

    private float GetNavEditorSelectedCostMultiplierOrDefault()
    {
        return _navEditorSelectedLinkIndex >= 0 && _navEditorSelectedLinkIndex < _navEditorLinks.Count
            ? _navEditorLinks[_navEditorSelectedLinkIndex].CostMultiplier
            : _navEditorDefaultCostMultiplier;
    }

    private BotNavigationHintFallbackRole GetNavEditorSelectedFallbackRoleOrDefault()
    {
        return _navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count
            ? _navEditorAnchors[_navEditorSelectedAnchorIndex].FallbackRole
            : BotNavigationHintFallbackRole.None;
    }

    private BotNavigationHintFallbackTrigger[] GetNavEditorSelectedFallbackTriggersOrDefault()
    {
        return _navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count
            ? _navEditorAnchors[_navEditorSelectedAnchorIndex].FallbackTriggers
            : Array.Empty<BotNavigationHintFallbackTrigger>();
    }

    private bool? GetNavEditorSelectedCarryRequirementOrDefault()
    {
        return _navEditorSelectedAnchorIndex >= 0 && _navEditorSelectedAnchorIndex < _navEditorAnchors.Count
            ? _navEditorAnchors[_navEditorSelectedAnchorIndex].RequiresCarryingIntel
            : null;
    }

    private static PlayerClass[] GetNextNavEditorClassSelection(PlayerClass[] current)
    {
        if (current.Length == 0)
        {
            return [BotNavigationClasses.All[0]];
        }

        if (current.Length == 1)
        {
            for (var index = 0; index < BotNavigationClasses.All.Count; index += 1)
            {
                if (BotNavigationClasses.All[index] == current[0])
                {
                    return index + 1 < BotNavigationClasses.All.Count
                        ? [BotNavigationClasses.All[index + 1]]
                        : Array.Empty<PlayerClass>();
                }
            }
        }

        return Array.Empty<PlayerClass>();
    }

    private static string DescribeNavEditorClasses(PlayerClass[] classes)
    {
        return classes.Length == 0 ? "All" : string.Join("/", classes.Select(BotNavigationClasses.GetShortLabel));
    }

    private static string DescribeNavEditorViewClass(PlayerClass? classId)
    {
        return classId.HasValue ? BotNavigationClasses.GetShortLabel(classId.Value) : "All";
    }

    private static string DescribeNavEditorLayerMode(NavEditorLayerMode mode)
    {
        return mode switch
        {
            NavEditorLayerMode.GeneratedGraph => "Graph",
            NavEditorLayerMode.Both => "Both",
            _ => "Hints",
        };
    }

    private static string DescribeNavEditorGraphEdgeDisplayMode(NavEditorGraphEdgeDisplayMode mode)
    {
        return mode switch
        {
            NavEditorGraphEdgeDisplayMode.Cyan => "Cyan",
            NavEditorGraphEdgeDisplayMode.Red => "Red",
            _ => "Soft",
        };
    }

    private static string DescribeNavEditorFallbackRole(BotNavigationHintFallbackRole role)
    {
        return role switch
        {
            BotNavigationHintFallbackRole.Entry => "Entry",
            BotNavigationHintFallbackRole.Exit => "Exit",
            _ => "Off",
        };
    }

    private static string DescribeNavEditorFallbackTriggers(BotNavigationHintFallbackTrigger[] triggers)
    {
        return triggers.Length == 0
            ? "NoNext"
            : string.Join("/", triggers.Select(DescribeNavEditorFallbackTrigger));
    }

    private static string DescribeNavEditorFallbackTrigger(BotNavigationHintFallbackTrigger trigger)
    {
        return trigger switch
        {
            BotNavigationHintFallbackTrigger.Stuck => "Stuck",
            BotNavigationHintFallbackTrigger.Reacquire => "Reacq",
            BotNavigationHintFallbackTrigger.CarryReturn => "Carry",
            BotNavigationHintFallbackTrigger.DirectTarget => "Direct",
            _ => "NoNext",
        };
    }

    private static string DescribeNavEditorCarryRequirement(bool? requiresCarryingIntel)
    {
        return requiresCarryingIntel switch
        {
            true => "Must Carry",
            false => "No Carry",
            _ => "Any",
        };
    }

    private static float GetNavEditorFallbackRadius(NavEditorAnchor anchor)
    {
        return anchor.FallbackRadius > 0f
            ? anchor.FallbackRadius
            : anchor.FallbackRole == BotNavigationHintFallbackRole.Exit ? 72f : 112f;
    }

    private static bool TryNormalizeNavEditorActivationZone(
        BotNavigationHintFallbackActivationZone? zone,
        out float minX,
        out float minY,
        out float maxX,
        out float maxY)
    {
        minX = minY = maxX = maxY = 0f;
        if (zone is null)
        {
            return false;
        }

        minX = MathF.Min(zone.MinX, zone.MaxX);
        minY = MathF.Min(zone.MinY, zone.MaxY);
        maxX = MathF.Max(zone.MinX, zone.MaxX);
        maxY = MathF.Max(zone.MinY, zone.MaxY);
        if (maxX - minX < 1f || maxY - minY < 1f)
        {
            minX = minY = maxX = maxY = 0f;
            return false;
        }

        return true;
    }

    private static BotNavigationHintFallbackActivationZone? CreateNavEditorFallbackActivationZone(NavEditorAnchor anchor)
    {
        if (!anchor.HasFallbackActivationZone)
        {
            return null;
        }

        var minX = MathF.Min(anchor.FallbackActivationMinX, anchor.FallbackActivationMaxX);
        var minY = MathF.Min(anchor.FallbackActivationMinY, anchor.FallbackActivationMaxY);
        var maxX = MathF.Max(anchor.FallbackActivationMinX, anchor.FallbackActivationMaxX);
        var maxY = MathF.Max(anchor.FallbackActivationMinY, anchor.FallbackActivationMaxY);
        if (maxX - minX < 1f || maxY - minY < 1f)
        {
            return null;
        }

        return new BotNavigationHintFallbackActivationZone
        {
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
        };
    }

    private bool AppliesToNavEditorViewClass(IReadOnlyList<PlayerClass> classes)
    {
        return !_navEditorViewClass.HasValue
            || BotNavigationClasses.AppliesToClass(classes, legacyProfiles: null, _navEditorViewClass.Value);
    }

    private static PlayerClass? GetNextNavEditorViewClass(PlayerClass? current)
    {
        if (!current.HasValue)
        {
            return BotNavigationClasses.All[0];
        }

        for (var index = 0; index < BotNavigationClasses.All.Count; index += 1)
        {
            if (BotNavigationClasses.All[index] == current.Value)
            {
                return index + 1 < BotNavigationClasses.All.Count
                    ? BotNavigationClasses.All[index + 1]
                    : null;
            }
        }

        return null;
    }

    private static string DescribeNavEditorAnchorKind(BotNavigationNodeKind kind)
    {
        return kind switch
        {
            BotNavigationNodeKind.Spawn => "Spawn",
            BotNavigationNodeKind.Objective => "Objective",
            BotNavigationNodeKind.HealingCabinet => "Cabinet",
            _ => "Route",
        };
    }

    private static string DescribeNavEditorAnchorTeam(PlayerTeam? team)
    {
        return team switch
        {
            PlayerTeam.Red => "Red",
            PlayerTeam.Blue => "Blue",
            _ => "Unassigned",
        };
    }

    private static string DescribeNavEditorAnchorRole(BotNavigationNodeKind kind, PlayerTeam? team)
    {
        return CanNavEditorAnchorKindUseTeam(kind)
            ? $"{DescribeNavEditorAnchorKind(kind)} ({DescribeNavEditorAnchorTeam(team)})"
            : DescribeNavEditorAnchorKind(kind);
    }

    private static BotNavigationNodeKind GetNextNavEditorAnchorKind(BotNavigationNodeKind kind)
    {
        return kind switch
        {
            BotNavigationNodeKind.RouteAnchor => BotNavigationNodeKind.Spawn,
            BotNavigationNodeKind.Spawn => BotNavigationNodeKind.Objective,
            BotNavigationNodeKind.Objective => BotNavigationNodeKind.HealingCabinet,
            _ => BotNavigationNodeKind.RouteAnchor,
        };
    }

    private static PlayerTeam? GetNextNavEditorAnchorTeam(PlayerTeam? team)
    {
        return team switch
        {
            PlayerTeam.Red => PlayerTeam.Blue,
            PlayerTeam.Blue => null,
            _ => PlayerTeam.Red,
        };
    }

    private static bool CanNavEditorAnchorKindUseTeam(BotNavigationNodeKind kind)
    {
        return kind is BotNavigationNodeKind.RouteAnchor or BotNavigationNodeKind.Spawn or BotNavigationNodeKind.Objective or BotNavigationNodeKind.HealingCabinet;
    }

    private static PlayerTeam? NormalizeNavEditorAnchorTeam(BotNavigationNodeKind kind, PlayerTeam? team)
    {
        return CanNavEditorAnchorKindUseTeam(kind) ? team : null;
    }

    private static BotNavigationHintTraversalKind GetNextNavEditorTraversal(BotNavigationHintTraversalKind traversal)
    {
        return traversal switch
        {
            BotNavigationHintTraversalKind.Auto => BotNavigationHintTraversalKind.Walk,
            BotNavigationHintTraversalKind.Walk => BotNavigationHintTraversalKind.Jump,
            BotNavigationHintTraversalKind.Jump => BotNavigationHintTraversalKind.Drop,
            _ => BotNavigationHintTraversalKind.Auto,
        };
    }

    private static float ClampNavEditorCostMultiplier(float value)
    {
        return MathF.Round(Math.Clamp(value, 0.1f, 4f), 1, MidpointRounding.AwayFromZero);
    }

    private bool TrySetNavEditorAnchorLabel(NavEditorAnchor anchor, string newLabel)
    {
        var oldLabel = anchor.Label;
        if (string.Equals(oldLabel, newLabel, StringComparison.Ordinal))
        {
            return false;
        }

        anchor.Label = newLabel;
        for (var index = 0; index < _navEditorLinks.Count; index += 1)
        {
            var link = _navEditorLinks[index];
            if (string.Equals(link.FromLabel, oldLabel, StringComparison.OrdinalIgnoreCase))
            {
                link.FromLabel = newLabel;
            }

            if (string.Equals(link.ToLabel, oldLabel, StringComparison.OrdinalIgnoreCase))
            {
                link.ToLabel = newLabel;
            }
        }

        _navEditorDirty = true;
        return true;
    }

    private void RefreshNavEditorAnchorAutoLabel(NavEditorAnchor anchor)
    {
        if (!anchor.AutoLabel)
        {
            return;
        }

        var suggestedLabel = BuildSuggestedNavEditorAnchorLabel(anchor.Kind, anchor.Team, anchor.Label);
        TrySetNavEditorAnchorLabel(anchor, suggestedLabel);
    }

    private string BuildSuggestedNavEditorAnchorLabel(BotNavigationNodeKind kind, PlayerTeam? team, string? currentLabel = null)
    {
        var prefix = GetNavEditorAutoLabelPrefix(kind, team);
        return MakeUniqueNavEditorAnchorLabel(prefix, currentLabel);
    }

    private static string GetNavEditorAutoLabelPrefix(BotNavigationNodeKind kind, PlayerTeam? team)
    {
        return kind switch
        {
            BotNavigationNodeKind.Spawn => team switch
            {
                PlayerTeam.Red => "red-spawn",
                PlayerTeam.Blue => "blue-spawn",
                _ => "spawn",
            },
            BotNavigationNodeKind.Objective => team switch
            {
                PlayerTeam.Red => "red-objective",
                PlayerTeam.Blue => "blue-objective",
                _ => "objective",
            },
            BotNavigationNodeKind.HealingCabinet => team switch
            {
                PlayerTeam.Red => "red-cabinet",
                PlayerTeam.Blue => "blue-cabinet",
                _ => "cabinet",
            },
            _ => "route",
        };
    }

    private static bool IsNavEditorAutoGeneratedAnchorLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        return IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "anchor")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "route")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "spawn")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "red-spawn")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "blue-spawn")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "objective")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "red-objective")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "blue-objective")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "cabinet")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "red-cabinet")
            || IsNavEditorAutoGeneratedAnchorLabelForPrefix(label, "blue-cabinet");
    }

    private static bool IsNavEditorAutoGeneratedAnchorLabelForPrefix(string label, string prefix)
    {
        if (string.Equals(label, prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!label.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = label[(prefix.Length + 1)..];
        return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private string MakeUniqueNavEditorAnchorLabel(string requestedLabel, string? currentLabel = null)
    {
        var sanitized = string.IsNullOrWhiteSpace(requestedLabel) ? $"anchor-{_navEditorNextAnchorNumber:000}" : requestedLabel.Trim();
        var label = sanitized;
        var suffix = 2;
        while (_navEditorAnchors.Any(anchor => !string.Equals(anchor.Label, currentLabel, StringComparison.OrdinalIgnoreCase) && string.Equals(anchor.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            label = $"{sanitized}-{suffix}";
            suffix += 1;
        }

        return label;
    }

    private int GetNextNavEditorAnchorNumber()
    {
        var maxNumber = 0;
        foreach (var anchor in _navEditorAnchors)
        {
            if (!anchor.Label.StartsWith("anchor-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = anchor.Label["anchor-".Length..];
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                maxNumber = Math.Max(maxNumber, number);
            }
        }

        return maxNumber + 1;
    }

    private void SetNavEditorStatus(string message)
    {
        _navEditorStatusMessage = message;
        _navEditorStatusSecondsRemaining = NavEditorStatusDurationSeconds;
        MaybeMirrorNavEditorStatusToConsole(message);
    }

    private void MaybeMirrorNavEditorStatusToConsole(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !ShouldMirrorNavEditorStatusToConsole(message))
        {
            return;
        }

        if (string.Equals(_navEditorLastConsoleStatusMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        _navEditorLastConsoleStatusMessage = message;
        AddConsoleLine($"nav editor: {message}");
    }

    private static bool ShouldMirrorNavEditorStatusToConsole(string message)
    {
        return message.Length > 64
            || message.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || message.Contains("warning", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not", StringComparison.OrdinalIgnoreCase)
            || message.Contains("needs ", StringComparison.OrdinalIgnoreCase);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        var delta = segmentEnd - segmentStart;
        var lengthSquared = delta.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return Vector2.Distance(point, segmentStart);
        }

        var t = Vector2.Dot(point - segmentStart, delta) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        var projection = segmentStart + (delta * t);
        return Vector2.Distance(point, projection);
    }

    private static long GetNavEditorGraphEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) ^ (uint)toNodeId;
    }

    private static Color GetNavEditorClassColor(PlayerClass[] classes)
    {
        if (classes.Length == 0)
        {
            return new Color(236, 238, 242);
        }

        if (classes.Length > 1)
        {
            return new Color(208, 212, 218);
        }

        return classes[0] switch
        {
            PlayerClass.Scout => new Color(116, 210, 255),
            PlayerClass.Engineer => new Color(255, 214, 120),
            PlayerClass.Pyro => new Color(255, 154, 120),
            PlayerClass.Soldier => new Color(168, 255, 156),
            PlayerClass.Demoman => new Color(255, 186, 92),
            PlayerClass.Heavy => new Color(255, 124, 92),
            PlayerClass.Sniper => new Color(132, 255, 224),
            PlayerClass.Medic => new Color(132, 255, 168),
            PlayerClass.Spy => new Color(204, 164, 255),
            _ => new Color(236, 238, 242),
        };
    }

    private static Color GetNavEditorAnchorColor(NavEditorAnchor anchor)
    {
        return anchor.Kind switch
        {
            BotNavigationNodeKind.Spawn => anchor.Team == PlayerTeam.Red
                ? new Color(255, 124, 92)
                : anchor.Team == PlayerTeam.Blue
                    ? new Color(116, 210, 255)
                    : new Color(208, 212, 218),
            BotNavigationNodeKind.Objective => anchor.Team == PlayerTeam.Red
                ? new Color(255, 174, 120)
                : anchor.Team == PlayerTeam.Blue
                    ? new Color(132, 226, 255)
                    : new Color(255, 214, 120),
            BotNavigationNodeKind.HealingCabinet => new Color(132, 255, 168),
            _ => GetNavEditorClassColor(anchor.Classes),
        };
    }

    private static Color GetNavEditorLinkColor(BotNavigationHintTraversalKind traversal)
    {
        return traversal switch
        {
            BotNavigationHintTraversalKind.Walk => new Color(132, 255, 168),
            BotNavigationHintTraversalKind.Jump => new Color(116, 210, 255),
            BotNavigationHintTraversalKind.Drop => new Color(255, 186, 92),
            _ => new Color(200, 204, 208),
        };
    }

    private static CharacterClassDefinition GetNavEditorRepresentativeClassDefinition(PlayerClass classId)
    {
        return BotNavigationClasses.GetDefinition(classId);
    }

    private bool IsNavEditorTraversalCaptureActive()
    {
        return _navEditorTraversalCaptureMode != NavEditorTraversalCaptureMode.None;
    }

    private bool IsNavEditorTraversalPlaybackActive()
    {
        return _navEditorPlaybackStepIndex >= 0 && _navEditorPlaybackStepIndex < _navEditorPlaybackSteps.Count;
    }

    private bool ShouldBlockGameplayForNavEditor()
    {
        return _navEditorEnabled && !IsNavEditorTraversalCaptureActive();
    }

    private PlayerInputSnapshot ResolveNavEditorGameplayInput(PlayerInputSnapshot gameplayInput)
    {
        if (!IsNavEditorTraversalPlaybackActive())
        {
            return gameplayInput;
        }

        if (!TryGetCurrentNavEditorPlaybackStep(out var step))
        {
            return default;
        }

        if (step.InputTape.Length > 0)
        {
            if (_navEditorPlaybackFrameIndex < 0)
            {
                return default;
            }

            if (_navEditorPlaybackFrameIndex >= step.InputTape.Length)
            {
                return CreateNavEditorDirectionalPlaybackInput(step);
            }

            var frame = step.InputTape[_navEditorPlaybackFrameIndex];
            return CreateNavEditorPlaybackInput(frame);
        }

        return CreateNavEditorDirectionalPlaybackInput(step);
    }

    private PlayerInputSnapshot CreateNavEditorDirectionalPlaybackInput(NavEditorPlaybackStep step)
    {
        var horizontalDirection = step.ForcedHorizontalDirection;
        if (horizontalDirection == 0)
        {
            horizontalDirection = GetNavEditorHorizontalDirectionToTarget(step.ToX);
        }

        var forceApproximateJump = step.Kind == BotNavigationTraversalKind.Jump
            && ShouldForceApproximateNavEditorJump(step, horizontalDirection);
        return CreateNavEditorDirectionalInput(horizontalDirection, jump: forceApproximateJump);
    }

    private void SetNavEditorTraversalCaptureInput(PlayerInputSnapshot gameplayInput)
    {
        _navEditorTraversalCaptureInput = gameplayInput;
    }

    private void OnNavEditorTraversalCaptureBeforeTick()
    {
        if (!IsNavEditorTraversalCaptureActive())
        {
            return;
        }

        var sample = CreateNavEditorRecordedInputSample(_navEditorTraversalCaptureInput);
        if (_navEditorTraversalCaptureMode == NavEditorTraversalCaptureMode.Armed)
        {
            if (!sample.HasAnyInput)
            {
                return;
            }

            _navEditorTraversalCaptureMode = NavEditorTraversalCaptureMode.Recording;
            _navEditorRecordedSamples.Clear();
            SetNavEditorStatus($"recording traversal to {_navEditorRecordingTargetLabel}...");
            AddConsoleLine($"nav editor recording started for {BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass)}");
        }

        _navEditorRecordedSamples.Add(sample);
    }

    private void OnNavEditorTraversalCaptureAfterTick()
    {
        AdvanceNavEditorTraversalPlaybackAfterTick();

        if (!IsNavEditorTraversalCaptureActive())
        {
            return;
        }

        if (!_world.LocalPlayer.IsAlive)
        {
            CancelNavEditorTraversalCapture("traversal recording failed: local player died");
            return;
        }

        if (_navEditorTraversalCaptureMode != NavEditorTraversalCaptureMode.Recording)
        {
            return;
        }

        if (HasReachedNavEditorRecordingTarget())
        {
            CompleteNavEditorTraversalCapture();
            return;
        }

        if (_navEditorRecordedSamples.Count >= NavEditorRecordingMaximumTicks)
        {
            CancelNavEditorTraversalCapture("traversal recording timed out before reaching the target anchor");
        }
    }

    private void AdvanceNavEditorTraversalPlaybackAfterTick()
    {
        if (!IsNavEditorTraversalPlaybackActive())
        {
            return;
        }

        if (!_world.LocalPlayer.IsAlive)
        {
            CancelNavEditorTraversalPlayback("traversal test failed: local player died");
            return;
        }

        _navEditorPlaybackElapsedTicks += 1;
        if (_navEditorPlaybackElapsedTicks >= NavEditorPlaybackMaximumTicks)
        {
            CancelNavEditorTraversalPlayback("traversal test timed out");
            return;
        }

        if (!TryGetCurrentNavEditorPlaybackStep(out var step))
        {
            CancelNavEditorTraversalPlayback("traversal test lost its active step");
            return;
        }

        if (HasReachedNavEditorPlaybackTarget(step))
        {
            AdvanceNavEditorPlaybackStep();
            return;
        }

        if (step.InputTape.Length == 0)
        {
            return;
        }

        if (_navEditorPlaybackFrameIndex >= step.InputTape.Length)
        {
            _navEditorPlaybackPostTapeTicks += 1;
            if (_navEditorPlaybackPostTapeTicks > NavEditorPlaybackPostTapeMaximumTicks)
            {
                CancelNavEditorTraversalPlayback(BuildNavEditorPlaybackMissMessage("traversal test ended before reaching", step));
            }

            return;
        }

        if (_navEditorPlaybackFrameIndex < 0 || _navEditorPlaybackFrameIndex >= step.InputTape.Length)
        {
            CancelNavEditorTraversalPlayback($"traversal test missed {step.ToLabel}");
            return;
        }

        if (_navEditorPlaybackFrameSecondsRemaining <= 0d)
        {
            _navEditorPlaybackFrameSecondsRemaining = GetNavEditorFrameDurationSeconds(step.InputTape[_navEditorPlaybackFrameIndex]);
        }

        var remainingSeconds = _navEditorPlaybackFrameSecondsRemaining - Math.Max(0d, _world.Config.FixedDeltaSeconds);
        while (remainingSeconds <= 0d)
        {
            _navEditorPlaybackFrameIndex += 1;
            if (_navEditorPlaybackFrameIndex >= step.InputTape.Length)
            {
                if (HasReachedNavEditorPlaybackTarget(step))
                {
                    AdvanceNavEditorPlaybackStep();
                    return;
                }

                _navEditorPlaybackFrameSecondsRemaining = 0d;
                return;
            }

            remainingSeconds += GetNavEditorFrameDurationSeconds(step.InputTape[_navEditorPlaybackFrameIndex]);
        }

        _navEditorPlaybackFrameSecondsRemaining = remainingSeconds;
    }

    private void AdvanceNavEditorPlaybackStep()
    {
        if (!IsNavEditorTraversalPlaybackActive())
        {
            return;
        }

        _navEditorPlaybackStepIndex += 1;
        if (_navEditorPlaybackStepIndex >= _navEditorPlaybackSteps.Count)
        {
            var summary = _navEditorPlaybackLabel;
            ClearNavEditorTraversalPlaybackState();
            SetNavEditorStatus($"{summary} completed");
            AddConsoleLine($"nav editor {summary} completed");
            return;
        }

        ResetNavEditorPlaybackFrameState();
        if (TryGetCurrentNavEditorPlaybackStep(out var nextStep))
        {
            SetNavEditorStatus($"testing {_navEditorPlaybackLabel}: {nextStep.FromLabel} -> {nextStep.ToLabel}");
        }
    }

    private void ResetNavEditorPlaybackFrameState()
    {
        _navEditorPlaybackFrameIndex = 0;
        _navEditorPlaybackFrameSecondsRemaining = 0d;
        _navEditorPlaybackPostTapeTicks = 0;
    }

    private bool TryGetCurrentNavEditorPlaybackStep(out NavEditorPlaybackStep step)
    {
        if (!IsNavEditorTraversalPlaybackActive())
        {
            step = default!;
            return false;
        }

        step = _navEditorPlaybackSteps[_navEditorPlaybackStepIndex];
        return true;
    }

    private bool TryGetNavEditorGeneratedGraphNode(int nodeId, out BotNavigationNode node)
    {
        node = default!;
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            return false;
        }

        for (var index = 0; index < asset.Nodes.Count; index += 1)
        {
            if (asset.Nodes[index].Id != nodeId)
            {
                continue;
            }

            node = asset.Nodes[index];
            return true;
        }

        return false;
    }

    private void UpdateNavEditorGeneratedGraphEditStatus()
    {
        if (_navEditorGeneratedCompatGraph is not BotNavigationAsset asset)
        {
            _navEditorGeneratedCompatGraphStatus = "generated graph not loaded";
            return;
        }

        _navEditorGeneratedCompatGraphStatus = $"graph edits: {asset.Nodes.Count} nodes / {asset.Edges.Count} links (unsaved)";
    }

    private int ResolveNavEditorGeneratedGraphSurfaceId(float x, float y)
    {
        var bestSurfaceId = -1;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < _world.Level.Solids.Count; index += 1)
        {
            var solid = _world.Level.Solids[index];
            if (x < solid.Left || x > solid.Right || y < solid.Top || y > solid.Bottom)
            {
                continue;
            }

            var distance = MathF.Abs(y - solid.Top);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestSurfaceId = index;
        }

        return bestSurfaceId;
    }

    private bool HasReachedNavEditorPlaybackTarget(NavEditorPlaybackStep step)
    {
        var horizontalDelta = MathF.Abs(_world.LocalPlayer.X - step.ToX);
        var verticalDelta = MathF.Abs(_world.LocalPlayer.Y - step.ToY);
        if (horizontalDelta <= NavEditorPlaybackTargetToleranceX
            && verticalDelta <= NavEditorPlaybackTargetToleranceY
            && (!step.RequiresGroundedArrival || _world.LocalPlayer.IsGrounded))
        {
            return true;
        }

        return step.Kind == BotNavigationTraversalKind.Walk
            && step.RequiresGroundedArrival
            && _world.LocalPlayer.IsGrounded
            && horizontalDelta <= NavEditorPlaybackTargetToleranceX
            && verticalDelta <= NavEditorApproximateGroundedArrivalToleranceY;
    }

    private string BuildNavEditorPlaybackMissMessage(string prefix, NavEditorPlaybackStep step)
    {
        var dx = _world.LocalPlayer.X - step.ToX;
        var dy = _world.LocalPlayer.Y - step.ToY;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix} {step.ToLabel} (dx={dx:0.#}, dy={dy:0.#}, grounded={_world.LocalPlayer.IsGrounded}, tape={step.InputTape.Length})");
    }

    private bool ShouldForceApproximateNavEditorJump(NavEditorPlaybackStep step, int horizontalDirection)
    {
        if (!_world.LocalPlayer.IsGrounded || _navEditorPlaybackFrameIndex > 0)
        {
            return false;
        }

        var targetIsAbove = step.ToY < _world.LocalPlayer.Y - 8f;
        var nearTargetX = MathF.Abs(step.ToX - _world.LocalPlayer.X) <= 96f;
        return targetIsAbove || nearTargetX;
    }

    private int GetNavEditorHorizontalDirectionToTarget(float targetX)
    {
        var deltaX = targetX - _world.LocalPlayer.X;
        if (deltaX > 4f)
        {
            return 1;
        }

        if (deltaX < -4f)
        {
            return -1;
        }

        return 0;
    }

    private PlayerInputSnapshot CreateNavEditorDirectionalInput(int direction, bool jump)
    {
        var aimWorldX = _world.LocalPlayer.X + ((direction == 0 ? 1 : direction) * 256f);
        return new PlayerInputSnapshot(
            Left: direction < 0,
            Right: direction > 0,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: aimWorldX,
            AimWorldY: _world.LocalPlayer.Y,
            DebugKill: false);
    }

    private PlayerInputSnapshot CreateNavEditorPlaybackInput(BotNavigationInputFrame frame)
    {
        var direction = frame.Right ? 1 : frame.Left ? -1 : 0;
        return CreateNavEditorDirectionalInput(direction, frame.Up);
    }

    private static double GetNavEditorFrameDurationSeconds(BotNavigationInputFrame frame)
    {
        if (frame.DurationSeconds > 0d)
        {
            return frame.DurationSeconds;
        }

        return Math.Max(1, frame.Ticks) / (double)SimulationConfig.DefaultTicksPerSecond;
    }

    private void CompleteNavEditorTraversalCapture()
    {
        if (_navEditorRecordingLinkIndex < 0 || _navEditorRecordingLinkIndex >= _navEditorLinks.Count)
        {
            CancelNavEditorTraversalCapture("traversal recording failed: selected link was lost");
            return;
        }

        var recordedTape = BuildNormalizedNavEditorRecordedTraversalTape(_navEditorRecordedSamples, _world.Config.FixedDeltaSeconds);
        if (recordedTape.Count == 0)
        {
            CancelNavEditorTraversalCapture("traversal recording failed: no movement was captured");
            return;
        }

        var validatorAccepted = TryValidateNavEditorRecordedTraversal(
            recordedTape,
            _navEditorRecordingSourcePosition.X,
            _navEditorRecordingSourcePosition.Y,
            _navEditorRecordingTargetPosition.X,
            _navEditorRecordingTargetPosition.Y,
            _navEditorRecordingRequiresGroundedArrival,
            _navEditorRecordTestClass,
            out var traversalKind,
            out var cost,
            out var failureMessage);
        if (!validatorAccepted)
        {
            traversalKind = ResolveNavEditorRecordedTraversalKind(
                _navEditorRecordingSourcePosition.Y,
                _navEditorRecordingTargetPosition.Y,
                recordedTape);
            cost = GetNavEditorRecordedTraversalTickCount(recordedTape) * 12f;
        }

        var link = _navEditorLinks[_navEditorRecordingLinkIndex];
        link.RecordedTraversals.RemoveAll(recording => recording.ClassId == _navEditorRecordTestClass);
        link.RecordedTraversals.Add(new NavEditorRecordedTraversal
        {
            ClassId = _navEditorRecordTestClass,
            InputTape = recordedTape.ToArray(),
        });
        link.Traversal = traversalKind switch
        {
            BotNavigationTraversalKind.Drop => BotNavigationHintTraversalKind.Drop,
            BotNavigationTraversalKind.Jump => BotNavigationHintTraversalKind.Jump,
            _ => BotNavigationHintTraversalKind.Walk,
        };
        _navEditorDirty = true;

        var recordedClassLabel = BotNavigationClasses.GetShortLabel(_navEditorRecordTestClass);
        ClearNavEditorTraversalCaptureState();
        SetNavEditorStatus(
            validatorAccepted
                ? $"recorded {recordedClassLabel} traversal ({GetNavEditorRecordedTraversalTickCount(recordedTape)} ticks, cost {cost:F0})"
                : $"recorded {recordedClassLabel} traversal ({GetNavEditorRecordedTraversalTickCount(recordedTape)} ticks, validator warning; see console)");
        AddConsoleLine($"nav editor stored {recordedClassLabel} traversal for {link.FromLabel} -> {link.ToLabel}");
        if (!validatorAccepted)
        {
            AddConsoleLine($"nav editor warning: offline validation disagreed with the live recording for {link.FromLabel} -> {link.ToLabel}: {failureMessage}");
        }
    }

    private void CancelNavEditorTraversalCapture(string reason)
    {
        ClearNavEditorTraversalCaptureState();
        SetNavEditorStatus(reason);
        AddConsoleLine(reason);
    }

    private void ClearNavEditorTraversalCaptureState()
    {
        _navEditorTraversalCaptureMode = NavEditorTraversalCaptureMode.None;
        _navEditorRecordingLinkIndex = -1;
        _navEditorRecordingSourcePosition = default;
        _navEditorRecordingTargetPosition = default;
        _navEditorRecordingSourceLabel = string.Empty;
        _navEditorRecordingTargetLabel = string.Empty;
        _navEditorRecordingRequiresGroundedArrival = true;
        _navEditorRecordedSamples.Clear();
        _navEditorTraversalCaptureInput = default;
    }

    private bool HasReachedNavEditorRecordingTarget()
    {
        return MathF.Abs(_world.LocalPlayer.X - _navEditorRecordingTargetPosition.X) <= NavEditorRecordingTargetToleranceX
            && MathF.Abs(_world.LocalPlayer.Y - _navEditorRecordingTargetPosition.Y) <= NavEditorRecordingTargetToleranceY
            && (!_navEditorRecordingRequiresGroundedArrival || _world.LocalPlayer.IsGrounded);
    }

    private bool TryValidateNavEditorRecordedTraversal(
        IReadOnlyList<BotNavigationInputFrame> tape,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        bool requireGroundedArrival,
        PlayerClass classId,
        out BotNavigationTraversalKind traversalKind,
        out float cost,
        out string failureMessage)
    {
        var classDefinition = BotNavigationClasses.GetDefinition(classId);
        if (BotNavigationRecordedTraversalValidator.TryValidate(
                _world.Level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                PlayerTeam.Red,
                tape,
                requireGroundedArrival,
                _world.Config.FixedDeltaSeconds,
                out cost,
                out traversalKind,
                out failureMessage))
        {
            return true;
        }

        return BotNavigationRecordedTraversalValidator.TryValidate(
            _world.Level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            PlayerTeam.Blue,
            tape,
            requireGroundedArrival,
            _world.Config.FixedDeltaSeconds,
            out cost,
            out traversalKind,
            out failureMessage);
    }

    private bool SelectedNavEditorLinkHasRecordingForClass(PlayerClass classId)
    {
        return _navEditorSelectedLinkIndex >= 0
            && _navEditorSelectedLinkIndex < _navEditorLinks.Count
            && LinkHasRecordedTraversalForClass(_navEditorLinks[_navEditorSelectedLinkIndex], classId);
    }

    private static bool LinkHasRecordedTraversalForClass(NavEditorLink link, PlayerClass classId)
    {
        return link.RecordedTraversals.Any(recording => recording.ClassId == classId && recording.InputTape.Length > 0);
    }

    private static string DescribeRecordedTraversalClasses(IReadOnlyList<NavEditorRecordedTraversal> recordedTraversals)
    {
        return recordedTraversals.Count == 0
            ? "none"
            : string.Join("/", recordedTraversals
                .OrderBy(recording => recording.ClassId)
                .Select(recording => BotNavigationClasses.GetShortLabel(recording.ClassId)));
    }

    private static List<NavEditorRecordedTraversal> ExpandNavEditorRecordedTraversals(IReadOnlyList<BotNavigationHintRecordedTraversal> recordedTraversals)
    {
        var expanded = new List<NavEditorRecordedTraversal>();
        foreach (var recording in recordedTraversals)
        {
            if (recording.ClassId.HasValue)
            {
                expanded.Add(new NavEditorRecordedTraversal
                {
                    ClassId = recording.ClassId.Value,
                    InputTape = recording.InputTape.ToArray(),
                });
                continue;
            }

            if (!recording.Profile.HasValue)
            {
                continue;
            }

            foreach (var classId in BotNavigationClasses.GetClassesForProfile(recording.Profile.Value))
            {
                expanded.Add(new NavEditorRecordedTraversal
                {
                    ClassId = classId,
                    InputTape = recording.InputTape.ToArray(),
                });
            }
        }

        return expanded;
    }

    private static IReadOnlyList<BotNavigationInputFrame> BuildNormalizedNavEditorRecordedTraversalTape(
        IReadOnlyList<NavEditorRecordedInputSample> samples,
        double sampleDurationSeconds)
    {
        if (samples.Count == 0 || sampleDurationSeconds <= 0d)
        {
            return Array.Empty<BotNavigationInputFrame>();
        }

        var frames = new List<BotNavigationInputFrame>();
        var currentSample = samples[0];
        var currentTicks = 1;
        for (var index = 1; index < samples.Count; index += 1)
        {
            if (samples[index].Equals(currentSample))
            {
                currentTicks += 1;
                continue;
            }

            frames.Add(CreateNavEditorRecordedFrame(currentSample, currentTicks, sampleDurationSeconds));
            currentSample = samples[index];
            currentTicks = 1;
        }

        frames.Add(CreateNavEditorRecordedFrame(currentSample, currentTicks, sampleDurationSeconds));
        return frames;
    }

    private static int GetNavEditorRecordedTraversalTickCount(IReadOnlyList<BotNavigationInputFrame> tape)
    {
        var tickDuration = 1d / SimulationConfig.DefaultTicksPerSecond;
        var totalSeconds = 0d;
        for (var index = 0; index < tape.Count; index += 1)
        {
            var frame = tape[index];
            totalSeconds += frame.DurationSeconds > 0d
                ? frame.DurationSeconds
                : Math.Max(1, frame.Ticks) * tickDuration;
        }

        return Math.Max(1, (int)Math.Round(totalSeconds / tickDuration, MidpointRounding.AwayFromZero));
    }

    private static BotNavigationTraversalKind ResolveNavEditorRecordedTraversalKind(
        float sourceY,
        float targetY,
        IReadOnlyList<BotNavigationInputFrame> tape)
    {
        if (tape.Any(frame => frame.Up))
        {
            return BotNavigationTraversalKind.Jump;
        }

        return targetY > sourceY + 8f
            ? BotNavigationTraversalKind.Drop
            : BotNavigationTraversalKind.Walk;
    }

    private static BotNavigationInputFrame CreateNavEditorRecordedFrame(
        NavEditorRecordedInputSample sample,
        int sampleCount,
        double sampleDurationSeconds)
    {
        return new BotNavigationInputFrame
        {
            Left = sample.Left,
            Right = sample.Right,
            Up = sample.Up,
            DurationSeconds = sampleCount * sampleDurationSeconds,
            Ticks = 0,
        };
    }

    private static NavEditorRecordedInputSample CreateNavEditorRecordedInputSample(PlayerInputSnapshot input)
    {
        var right = input.Right && !input.Left;
        var left = input.Left && !input.Right;
        return new NavEditorRecordedInputSample(left, right, input.Up);
    }

    private sealed class NavEditorAnchor
    {
        public string Label { get; set; } = string.Empty;
        public bool AutoLabel { get; set; }
        public PlayerClass[] Classes { get; set; } = Array.Empty<PlayerClass>();
        public float X { get; set; }
        public float Y { get; set; }
        public BotNavigationNodeKind Kind { get; set; } = BotNavigationNodeKind.RouteAnchor;
        public PlayerTeam? Team { get; set; }
        public BotNavigationHintFallbackRole FallbackRole { get; set; }
        public BotNavigationHintFallbackTrigger[] FallbackTriggers { get; set; } = Array.Empty<BotNavigationHintFallbackTrigger>();
        public float FallbackRadius { get; set; }
        public bool? RequiresCarryingIntel { get; set; }
        public bool HasFallbackActivationZone { get; set; }
        public float FallbackActivationMinX { get; set; }
        public float FallbackActivationMinY { get; set; }
        public float FallbackActivationMaxX { get; set; }
        public float FallbackActivationMaxY { get; set; }
    }

    private sealed class NavEditorLink
    {
        public string FromLabel { get; set; } = string.Empty;
        public string ToLabel { get; set; } = string.Empty;
        public PlayerClass[] Classes { get; set; } = Array.Empty<PlayerClass>();
        public bool Bidirectional { get; set; }
        public BotNavigationHintTraversalKind Traversal { get; set; } = BotNavigationHintTraversalKind.Auto;
        public bool StartJumpImmediately { get; set; }
        public float CostMultiplier { get; set; } = 1f;

        public List<NavEditorRecordedTraversal> RecordedTraversals { get; set; } = new();

        public bool AppliesToProfile(BotNavigationProfile profile)
        {
            return BotNavigationClasses.AppliesToProfile(Classes, legacyProfiles: null, profile);
        }

        public bool AppliesToClass(PlayerClass? classId)
        {
            return !classId.HasValue || BotNavigationClasses.AppliesToClass(Classes, legacyProfiles: null, classId.Value);
        }
    }

    private sealed class NavEditorRecordedTraversal
    {
        public PlayerClass ClassId { get; set; }

        public BotNavigationInputFrame[] InputTape { get; set; } = Array.Empty<BotNavigationInputFrame>();
    }

    private readonly record struct NavEditorRouteCandidate(
        int LinkIndex,
        bool Reverse,
        string FromLabel,
        string ToLabel);

    private sealed class NavEditorPlaybackStep
    {
        public string FromLabel { get; set; } = string.Empty;

        public string ToLabel { get; set; } = string.Empty;

        public float FromX { get; set; }

        public float FromY { get; set; }

        public float ToX { get; set; }

        public float ToY { get; set; }

        public BotNavigationTraversalKind Kind { get; set; } = BotNavigationTraversalKind.Walk;

        public int ForcedHorizontalDirection { get; set; }

        public bool RequiresGroundedArrival { get; set; } = true;

        public BotNavigationInputFrame[] InputTape { get; set; } = Array.Empty<BotNavigationInputFrame>();
    }

    private readonly record struct NavEditorClearAllPromptLayout(
        Rectangle Dialog,
        Rectangle YesButton,
        Rectangle NoButton);

    private readonly record struct NavEditorRecordedInputSample(bool Left, bool Right, bool Up)
    {
        public bool HasAnyInput => Left || Right || Up;
    }

    private readonly record struct NavEditorPanelLayout(
        Rectangle Panel,
        Rectangle Header,
        Rectangle CollapseButton,
        Rectangle SelectToolButton,
        Rectangle AnchorToolButton,
        Rectangle LinkToolButton,
        Rectangle SaveButton,
        Rectangle RebuildButton,
        Rectangle ReloadButton,
        Rectangle RenameButton,
        Rectangle DeleteButton,
        Rectangle ClearAllButton,
        Rectangle RecordButton,
        Rectangle RecordProfileButton,
        Rectangle ClearRecordingButton,
        Rectangle TestLinkButton,
        Rectangle TestRouteButton,
        Rectangle StopTestButton,
        Rectangle ViewClassButton,
        Rectangle ClassScopeButton,
        Rectangle LayerButton,
        Rectangle BotTagsButton,
        Rectangle EdgeDisplayButton,
        Rectangle RunScoreTrioButton,
        Rectangle ClearScoreTrioButton,
        Rectangle AnchorKindButton,
        Rectangle AnchorTeamButton,
        Rectangle FallbackRoleButton,
        Rectangle FallbackTriggerButton,
        Rectangle FallbackCarryButton,
        Rectangle TraversalButton,
        Rectangle DirectionButton,
        Rectangle CostDownButton,
        Rectangle CostUpButton);

    private sealed record NavEditorRebuildResult(
        bool Success,
        string Summary,
        IReadOnlyList<string> Lines,
        string LevelName = "",
        int MapAreaIndex = 1);

    private sealed class NavEditorContextMenuItem
    {
        public string Label { get; init; } = string.Empty;

        public bool Enabled { get; init; } = true;

        public bool Active { get; init; }

        public IReadOnlyList<NavEditorContextMenuItem> Children { get; init; } = Array.Empty<NavEditorContextMenuItem>();

        public Action? Action { get; init; }
    }

    private readonly record struct NavEditorContextMenuPanel(
        Rectangle Bounds,
        IReadOnlyList<NavEditorContextMenuItem> Items,
        IReadOnlyList<int> PrefixPath);

    private readonly record struct NavEditorContextMenuHit(
        NavEditorContextMenuItem Item,
        Rectangle Bounds,
        int[] Path);
}
