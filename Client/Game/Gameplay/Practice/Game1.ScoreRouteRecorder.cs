#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.BotAI;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum ScoreRouteRecorderCaptureState
    {
        None = 0,
        Armed = 1,
        Recording = 2,
    }

    private const float ScoreRouteRecorderNodeSnapDistance = 96f;
    private const int ScoreRouteRecorderMaximumTicks = 14400;
    private const int ScoreRouteRecorderMinimumStableNodeSamples = 2;
    private const float ScoreRouteRecorderCheckpointDistance = 192f;
    private const float ScoreRouteRecorderCheckpointVerticalDistance = 80f;
    private const int ScoreRouteRecorderCheckpointWindowRadius = 8;
    private const float ScoreRouteRecorderTerminalGoalTolerance = 160f;

    private ScoreRouteRecorderCaptureState _scoreRouteRecorderCaptureState;
    private BotNavigationRuntimeGraph? _scoreRouteRecorderGraph;
    private BotNavigationAsset? _scoreRouteRecorderGraphAsset;
    private BotNavigationScoreRoutePhase _scoreRouteRecorderPhase = BotNavigationScoreRoutePhase.None;
    private PlayerTeam _scoreRouteRecorderTeam = PlayerTeam.Red;
    private PlayerClass _scoreRouteRecorderClass = PlayerClass.Scout;
    private BotNavigationProfile _scoreRouteRecorderProfile = BotNavigationProfile.Light;
    private readonly List<ScoreRouteRecorderSample> _scoreRouteRecorderSamples = new();
    private bool _scoreRouteRecorderAutoRun;
    private bool _scoreRouteRecorderPreviousCarryingIntel;
    private PlayerInputSnapshot _scoreRouteRecorderCaptureInput;
    private ScoreRouteRecorderInputSample _scoreRouteRecorderPendingInputSample;

    private bool HandleScoreRouteRecorderConsoleCommand(string commandText, string[] parts)
    {
        if (parts.Length < 2)
        {
            PrintScoreRouteRecorderStatus();
            return true;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "start":
                if (parts.Length < 3 || !TryParseScoreRouteRecorderPhase(parts[2], out var phase))
                {
                    AddConsoleLine("usage: score_route_rec start <capture|attack|return>");
                    return true;
                }

                StartScoreRouteRecorder(phase);
                return true;

            case "stop":
            case "save":
                StopAndSaveScoreRouteRecorder(ExtractScoreRouteRecorderTrailingArgument(commandText, parts[1]));
                return true;

            case "cancel":
            case "clear":
                CancelScoreRouteRecorder("score route recording canceled");
                return true;

            case "status":
                PrintScoreRouteRecorderStatus();
                return true;

            default:
                AddConsoleLine("usage: score_route_rec <start|stop|save|cancel|status> ...");
                return true;
        }
    }

    private void SetScoreRouteRecorderCaptureInput(PlayerInputSnapshot gameplayInput)
    {
        _scoreRouteRecorderCaptureInput = gameplayInput;
    }

    private void OnScoreRouteRecorderBeforeTick()
    {
        if (!IsScoreRouteRecorderActive())
        {
            return;
        }

        _scoreRouteRecorderPendingInputSample = CreateScoreRouteRecorderInputSample(_scoreRouteRecorderCaptureInput);
        if (_scoreRouteRecorderCaptureState != ScoreRouteRecorderCaptureState.Armed)
        {
            return;
        }

        if (!_scoreRouteRecorderPendingInputSample.HasAnyInput)
        {
            return;
        }

        _scoreRouteRecorderCaptureState = ScoreRouteRecorderCaptureState.Recording;
        _scoreRouteRecorderSamples.Clear();
        AddConsoleLine(
            $"score route recording started: {_scoreRouteRecorderTeam} {DescribeScoreRouteRecorderProfile(_scoreRouteRecorderProfile)} {DescribeScoreRouteRecorderPhase(_scoreRouteRecorderPhase)}");
    }

    private void OnScoreRouteRecorderAfterTick()
    {
        if (!IsScoreRouteRecorderActive())
        {
            return;
        }

        if (!_world.LocalPlayer.IsAlive)
        {
            CancelScoreRouteRecorder("score route recording failed: local player died");
            return;
        }

        if (_scoreRouteRecorderCaptureState != ScoreRouteRecorderCaptureState.Recording
            || _scoreRouteRecorderGraph is null)
        {
            return;
        }

        if (TryAdvanceScoreRouteRecorderAutoRun())
        {
            return;
        }

        var nodeId = -1;
        if (_scoreRouteRecorderGraph.TryFindNearestNode(
                _world.LocalPlayer.X,
                _world.LocalPlayer.Y,
                ScoreRouteRecorderNodeSnapDistance,
                requireGroundSupport: false,
                out var nearestNode))
        {
            nodeId = nearestNode.Id;
        }

        _scoreRouteRecorderSamples.Add(new ScoreRouteRecorderSample(
            nodeId,
            _world.LocalPlayer.X,
            _world.LocalPlayer.Y,
            _world.LocalPlayer.IsGrounded,
            _scoreRouteRecorderPendingInputSample));

        if (_scoreRouteRecorderSamples.Count >= ScoreRouteRecorderMaximumTicks)
        {
            CancelScoreRouteRecorder("score route recording timed out");
        }
    }

    private void StartScoreRouteRecorder(BotNavigationScoreRoutePhase phase)
    {
        if (!IsPracticeSessionActive)
        {
            AddConsoleLine("score route recorder is practice-only.");
            return;
        }

        if (_networkClient.IsConnected)
        {
            AddConsoleLine("score route recorder is offline-only.");
            return;
        }

        if (IsNavEditorTraversalCaptureActive() || IsNavEditorTraversalPlaybackActive())
        {
            AddConsoleLine("stop the nav editor traversal test before starting score route recording.");
            return;
        }

        if (IsScoreRouteRecorderActive())
        {
            AddConsoleLine("score route recorder is already active.");
            return;
        }

        if (!TryLoadScoreRouteRecorderGraph(out var graph, out var asset, out var sourceLabel))
        {
            return;
        }

        _scoreRouteRecorderGraph = graph;
        _scoreRouteRecorderGraphAsset = asset;
        _scoreRouteRecorderPhase = phase;
        _scoreRouteRecorderTeam = _world.LocalPlayer.Team;
        _scoreRouteRecorderClass = _world.LocalPlayer.ClassId;
        _scoreRouteRecorderProfile = BotNavigationProfiles.GetProfileForClass(_scoreRouteRecorderClass);
        _scoreRouteRecorderAutoRun = phase == BotNavigationScoreRoutePhase.AttackIntel;
        _scoreRouteRecorderPreviousCarryingIntel = _world.LocalPlayer.IsCarryingIntel;
        _scoreRouteRecorderSamples.Clear();
        _scoreRouteRecorderCaptureInput = default;
        _scoreRouteRecorderPendingInputSample = default;
        _scoreRouteRecorderCaptureState = ScoreRouteRecorderCaptureState.Armed;
        AddConsoleLine(
            $"score route recorder armed: {_world.Level.Name} {_scoreRouteRecorderTeam} {DescribeScoreRouteRecorderProfile(_scoreRouteRecorderProfile)} {DescribeScoreRouteRecorderPhase(_scoreRouteRecorderPhase)} ({sourceLabel})");
        AddConsoleLine(
            _scoreRouteRecorderAutoRun
                ? "move to begin recording; pickup will save attack and switch to return automatically, and score will finish the return route."
                : "move your player to begin recording, then run score_route_rec stop when the route is complete.");
    }

    private void StopAndSaveScoreRouteRecorder(string? requestedLabel)
    {
        if (!IsScoreRouteRecorderActive())
        {
            AddConsoleLine("score route recorder is not active.");
            return;
        }

        if (_scoreRouteRecorderCaptureState != ScoreRouteRecorderCaptureState.Recording || _scoreRouteRecorderSamples.Count == 0)
        {
            CancelScoreRouteRecorder("score route recording canceled: no route was captured");
            return;
        }

        if (!TrySaveScoreRouteRecorderPhase(requestedLabel, out var entry, out var outputPath, out var warnings))
        {
            return;
        }

        ClearScoreRouteRecorderState();

        AddConsoleLine(
            $"score route saved: {entry.Team} {DescribeScoreRouteRecorderProfile(entry.Profile)} {DescribeScoreRouteRecorderPhase(entry.Phase)} nodes={entry.RouteNodeIds.Count} segments={entry.Segments.Count}");
        AddConsoleLine($"score route nodes: {string.Join("->", entry.RouteNodeIds)}");
        AddConsoleLine($"score route path: {outputPath}");
        foreach (var warning in warnings)
        {
            AddConsoleLine($"score route warning: {warning}");
        }
    }

    private bool TrySaveScoreRouteRecorderPhase(
        string? requestedLabel,
        out BotNavigationScoreRouteEntry entry,
        out string outputPath,
        out IReadOnlyList<string> warnings)
    {
        if (!TryBuildScoreRouteRecorderEntry(requestedLabel, out entry, out outputPath, out warnings))
        {
            return false;
        }

        var fingerprint = _scoreRouteRecorderGraphAsset?.LevelFingerprint
            ?? BotNavigationLevelFingerprint.Compute(_world.Level);
        var existingAsset = BotNavigationScoreRouteStore.Load(_world.Level);
        var entryTeam = entry.Team;
        var entryProfile = entry.Profile;
        var entryPhase = entry.Phase;
        var routes = existingAsset?.Routes
            .Where(route => !(route.Team == entryTeam && route.Profile == entryProfile && route.Phase == entryPhase))
            .ToList() ?? new List<BotNavigationScoreRouteEntry>();
        routes.Add(entry);
        routes.Sort(static (left, right) =>
        {
            var team = left.Team.CompareTo(right.Team);
            if (team != 0)
            {
                return team;
            }

            var profile = left.Profile.CompareTo(right.Profile);
            if (profile != 0)
            {
                return profile;
            }

            var phase = left.Phase.CompareTo(right.Phase);
            if (phase != 0)
            {
                return phase;
            }

            return string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);
        });

        var asset = new BotNavigationScoreRouteAsset
        {
            LevelName = _world.Level.Name,
            MapAreaIndex = _world.Level.MapAreaIndex,
            LevelFingerprint = fingerprint,
            Routes = routes.ToArray(),
        };

        BotNavigationScoreRouteStore.Save(asset);
        BotNavigationScoreRouteStore.SetRuntimeOverride(_world.Level.Name, _world.Level.MapAreaIndex, asset);
        return true;
    }

    private bool TryBuildScoreRouteRecorderEntry(
        string? requestedLabel,
        out BotNavigationScoreRouteEntry entry,
        out string outputPath,
        out IReadOnlyList<string> warnings)
    {
        entry = default!;
        outputPath = string.Empty;
        warnings = Array.Empty<string>();

        if (_scoreRouteRecorderGraph is null)
        {
            AddConsoleLine("score route recorder failed: nav graph is not loaded.");
            return false;
        }

        var visits = BuildScoreRouteRecorderVisits();
        if (visits.Count < 2)
        {
            AddConsoleLine("score route recorder failed: not enough snapped nav nodes were captured.");
            return false;
        }

        var directSegmentTapes = BuildScoreRouteRecorderDirectSegmentTapes(visits);
        var routeNodeIds = BuildScoreRouteRecorderCheckpointRoute(_scoreRouteRecorderGraph, out var routeFailureMessage);
        routeNodeIds = SimplifyScoreRouteRecorderRoute(routeNodeIds, directSegmentTapes);
        if (routeNodeIds.Count < 2)
        {
            AddConsoleLine($"score route recorder failed: {routeFailureMessage}");
            return false;
        }

        var finalSample = _scoreRouteRecorderSamples[^1];
        if (!TryFinalizeScoreRouteRecorderTerminalRoute(
                _scoreRouteRecorderGraph,
                routeNodeIds,
                finalSample.X,
                finalSample.Y,
                out routeNodeIds,
                out routeFailureMessage))
        {
            AddConsoleLine($"score route recorder failed: {routeFailureMessage}");
            return false;
        }

        var recordedWarnings = new List<string>();
        var segments = new List<BotNavigationScoreRouteSegment>(routeNodeIds.Count - 1);
        var classDefinition = BotNavigationClasses.GetDefinition(_scoreRouteRecorderClass);
        for (var index = 1; index < routeNodeIds.Count; index += 1)
        {
            var fromNodeId = routeNodeIds[index - 1];
            var toNodeId = routeNodeIds[index];
            if (!_scoreRouteRecorderGraph.TryGetEdge(fromNodeId, toNodeId, out var edge)
                || !_scoreRouteRecorderGraph.TryGetNode(fromNodeId, out var fromNode)
                || !_scoreRouteRecorderGraph.TryGetNode(toNodeId, out var toNode))
            {
                AddConsoleLine($"score route recorder failed: missing graph edge {fromNodeId}->{toNodeId}.");
                return false;
            }

            var hasCapturedTape = directSegmentTapes.TryGetValue((fromNodeId, toNodeId), out var capturedTape);
            var tape = hasCapturedTape && capturedTape is not null && capturedTape.Count > 0
                ? capturedTape
                : edge.InputTape;
            var traversalKind = edge.Kind;
            if (tape.Count > 0)
            {
                if (TryValidateScoreRouteRecorderTape(
                        classDefinition,
                        fromNode.X,
                        fromNode.Y,
                        toNode.X,
                        toNode.Y,
                        tape,
                        toNode.RequiresGroundSupport,
                        out var validatedKind,
                        out _,
                        out var failureMessage))
                {
                    traversalKind = validatedKind;
                }
                else if (hasCapturedTape && capturedTape is not null && capturedTape.Count > 0)
                {
                    recordedWarnings.Add(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"captured tape for {fromNodeId}->{toNodeId} did not validate ({failureMessage}); kept baked edge tape instead."));
                    tape = edge.InputTape;
                }
            }

            segments.Add(new BotNavigationScoreRouteSegment
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                TraversalKind = traversalKind,
                RequireGroundedArrival = toNode.RequiresGroundSupport,
                InputTape = tape.ToArray(),
            });
        }

        var routeLabel = string.IsNullOrWhiteSpace(requestedLabel)
            ? $"manual {_scoreRouteRecorderTeam} {DescribeScoreRouteRecorderProfile(_scoreRouteRecorderProfile)} {DescribeScoreRouteRecorderPhase(_scoreRouteRecorderPhase)}"
            : requestedLabel.Trim();
        var routeKey = BuildScoreRouteRecorderRouteKey(_scoreRouteRecorderTeam, _scoreRouteRecorderProfile, _scoreRouteRecorderPhase);
        entry = new BotNavigationScoreRouteEntry
        {
            Key = routeKey,
            Label = routeLabel,
            Team = _scoreRouteRecorderTeam,
            Profile = _scoreRouteRecorderProfile,
            Phase = _scoreRouteRecorderPhase,
            GoalNodeId = routeNodeIds[^1],
            GoalX = finalSample.X,
            GoalY = finalSample.Y,
            RouteNodeIds = routeNodeIds.ToArray(),
            Segments = segments.ToArray(),
        };

        warnings = recordedWarnings;
        outputPath = BotNavigationScoreRouteStore.ResolveWritablePath(_world.Level.Name, _world.Level.MapAreaIndex);
        return true;
    }

    private List<ScoreRouteRecorderVisit> BuildScoreRouteRecorderVisits()
    {
        if (_scoreRouteRecorderGraph is null)
        {
            return new List<ScoreRouteRecorderVisit>();
        }

        var rawVisits = new List<ScoreRouteRecorderVisit>();
        for (var index = 0; index < _scoreRouteRecorderSamples.Count; index += 1)
        {
            var sample = _scoreRouteRecorderSamples[index];
            if (sample.NodeId < 0)
            {
                continue;
            }

            if (rawVisits.Count > 0 && rawVisits[^1].NodeId == sample.NodeId)
            {
                var previous = rawVisits[^1];
                rawVisits[^1] = previous with { EndSampleIndex = index };
                continue;
            }

            rawVisits.Add(new ScoreRouteRecorderVisit(sample.NodeId, index, index));
        }

        var acceptedVisits = new List<ScoreRouteRecorderVisit>();
        for (var index = 0; index < rawVisits.Count; index += 1)
        {
            var visit = rawVisits[index];
            var sampleCount = visit.EndSampleIndex - visit.StartSampleIndex + 1;
            var isEndpoint = index == 0 || index == rawVisits.Count - 1;
            if (!isEndpoint && sampleCount < ScoreRouteRecorderMinimumStableNodeSamples)
            {
                continue;
            }

            if (acceptedVisits.Count == 0)
            {
                acceptedVisits.Add(visit);
                continue;
            }

            var previousVisit = acceptedVisits[^1];
            if (previousVisit.NodeId == visit.NodeId)
            {
                acceptedVisits[^1] = previousVisit with { EndSampleIndex = visit.EndSampleIndex };
                continue;
            }

            if (_scoreRouteRecorderGraph.TryGetEdge(previousVisit.NodeId, visit.NodeId, out _)
                || _scoreRouteRecorderGraph.FindRoute(previousVisit.NodeId, visit.NodeId) is { Length: > 1 })
            {
                acceptedVisits.Add(visit);
            }
        }

        return acceptedVisits;
    }

    private Dictionary<(int FromNodeId, int ToNodeId), IReadOnlyList<BotNavigationInputFrame>> BuildScoreRouteRecorderDirectSegmentTapes(
        IReadOnlyList<ScoreRouteRecorderVisit> visits)
    {
        var tapes = new Dictionary<(int FromNodeId, int ToNodeId), IReadOnlyList<BotNavigationInputFrame>>();
        for (var index = 1; index < visits.Count; index += 1)
        {
            var fromVisit = visits[index - 1];
            var toVisit = visits[index];
            if (fromVisit.NodeId == toVisit.NodeId || toVisit.EndSampleIndex < fromVisit.StartSampleIndex)
            {
                continue;
            }

            var tape = BuildNormalizedScoreRouteRecorderTape(
                _scoreRouteRecorderSamples
                    .Skip(fromVisit.StartSampleIndex)
                    .Take(toVisit.EndSampleIndex - fromVisit.StartSampleIndex + 1)
                    .Select(static sample => sample.Input)
                    .ToArray(),
                _world.Config.FixedDeltaSeconds);
            if (tape.Count == 0)
            {
                continue;
            }

            var key = (fromVisit.NodeId, toVisit.NodeId);
            if (!tapes.TryGetValue(key, out var existingTape) || GetScoreRouteRecorderTickCount(tape) < GetScoreRouteRecorderTickCount(existingTape))
            {
                tapes[key] = tape;
            }
        }

        return tapes;
    }

    private static List<int> ExpandScoreRouteRecorderRoute(
        IReadOnlyList<ScoreRouteRecorderVisit> visits,
        BotNavigationRuntimeGraph graph,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        var routeNodeIds = new List<int> { visits[0].NodeId };
        for (var index = 1; index < visits.Count; index += 1)
        {
            var targetNodeId = visits[index].NodeId;
            var currentNodeId = routeNodeIds[^1];
            if (targetNodeId == currentNodeId)
            {
                continue;
            }

            if (graph.TryGetEdge(currentNodeId, targetNodeId, out _))
            {
                routeNodeIds.Add(targetNodeId);
                continue;
            }

            var bridge = graph.FindRoute(currentNodeId, targetNodeId);
            if (bridge is null || bridge.Length < 2)
            {
                failureMessage = string.Create(
                    CultureInfo.InvariantCulture,
                    $"could not bridge sampled graph nodes {currentNodeId}->{targetNodeId}");
                return new List<int>();
            }

            for (var bridgeIndex = 1; bridgeIndex < bridge.Length; bridgeIndex += 1)
            {
                if (routeNodeIds[^1] != bridge[bridgeIndex])
                {
                    routeNodeIds.Add(bridge[bridgeIndex]);
                }
            }
        }

        return routeNodeIds;
    }

    private static List<int> SimplifyScoreRouteRecorderRoute(
        List<int> routeNodeIds,
        Dictionary<(int FromNodeId, int ToNodeId), IReadOnlyList<BotNavigationInputFrame>> directSegmentTapes)
    {
        if (routeNodeIds.Count <= 2)
        {
            return routeNodeIds;
        }

        var simplified = new List<int>(routeNodeIds.Count);
        var firstIndexByNodeId = new Dictionary<int, int>();
        for (var index = 0; index < routeNodeIds.Count; index += 1)
        {
            var nodeId = routeNodeIds[index];
            if (simplified.Count == 0)
            {
                simplified.Add(nodeId);
                firstIndexByNodeId[nodeId] = 0;
                continue;
            }

            if (simplified[^1] == nodeId)
            {
                continue;
            }

            if (firstIndexByNodeId.TryGetValue(nodeId, out var previousIndex))
            {
                var hasRecordedSegmentInsideLoop = false;
                for (var loopIndex = previousIndex + 1; loopIndex < simplified.Count; loopIndex += 1)
                {
                    var fromNodeId = simplified[loopIndex - 1];
                    var toNodeId = simplified[loopIndex];
                    if (directSegmentTapes.TryGetValue((fromNodeId, toNodeId), out var tape)
                        && tape.Count > 0)
                    {
                        hasRecordedSegmentInsideLoop = true;
                        break;
                    }
                }

                if (!hasRecordedSegmentInsideLoop)
                {
                    for (var removeIndex = simplified.Count - 1; removeIndex > previousIndex; removeIndex -= 1)
                    {
                        firstIndexByNodeId.Remove(simplified[removeIndex]);
                        simplified.RemoveAt(removeIndex);
                    }

                    continue;
                }
            }

            simplified.Add(nodeId);
            firstIndexByNodeId[nodeId] = simplified.Count - 1;
        }

        return simplified;
    }

    private List<int> BuildScoreRouteRecorderCheckpointRoute(
        BotNavigationRuntimeGraph graph,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        if (_scoreRouteRecorderSamples.Count == 0)
        {
            failureMessage = "no recorded samples";
            return new List<int>();
        }

        var checkpointSampleIndices = new List<int> { 0 };
        var lastCheckpointSampleIndex = 0;
        for (var index = 1; index < _scoreRouteRecorderSamples.Count - 1; index += 1)
        {
            var sample = _scoreRouteRecorderSamples[index];
            var lastCheckpoint = _scoreRouteRecorderSamples[lastCheckpointSampleIndex];
            var horizontalDistance = MathF.Abs(sample.X - lastCheckpoint.X);
            var verticalDistance = MathF.Abs(sample.Y - lastCheckpoint.Y);
            var distance = Vector2.Distance(new Vector2(sample.X, sample.Y), new Vector2(lastCheckpoint.X, lastCheckpoint.Y));
            if (distance < ScoreRouteRecorderCheckpointDistance
                && horizontalDistance < ScoreRouteRecorderCheckpointDistance
                && verticalDistance < ScoreRouteRecorderCheckpointVerticalDistance)
            {
                continue;
            }

            checkpointSampleIndices.Add(index);
            lastCheckpointSampleIndex = index;
        }

        if (checkpointSampleIndices[^1] != _scoreRouteRecorderSamples.Count - 1)
        {
            checkpointSampleIndices.Add(_scoreRouteRecorderSamples.Count - 1);
        }

        var checkpointNodeIds = new List<int>();
        var finalSample = _scoreRouteRecorderSamples[^1];
        for (var checkpointIndex = 0; checkpointIndex < checkpointSampleIndices.Count; checkpointIndex += 1)
        {
            var sampleIndex = checkpointSampleIndices[checkpointIndex];
            if (!TryResolveScoreRouteRecorderCheckpointNode(
                    graph,
                    sampleIndex,
                    checkpointNodeIds.Count > 0 ? checkpointNodeIds[^1] : -1,
                    out var nodeId))
            {
                if (checkpointIndex == checkpointSampleIndices.Count - 1)
                {
                    if (checkpointNodeIds.Count > 0
                        && TryResolveScoreRouteRecorderTerminalNode(graph, sampleIndex, out var terminalNodeId))
                    {
                        var bridge = graph.FindRoute(checkpointNodeIds[^1], terminalNodeId);
                        if (bridge is { Length: > 1 })
                        {
                            checkpointNodeIds.Add(terminalNodeId);
                            break;
                        }
                    }

                    if (checkpointNodeIds.Count > 0
                        && TryBuildScoreRouteRecorderGoalFallback(
                            graph,
                            checkpointNodeIds[^1],
                            finalSample.X,
                            finalSample.Y,
                            out var endpointRoute))
                    {
                        return endpointRoute;
                    }
                }

                continue;
            }

            if (checkpointNodeIds.Count == 0 || checkpointNodeIds[^1] != nodeId)
            {
                checkpointNodeIds.Add(nodeId);
            }
        }

        if (checkpointNodeIds.Count < 2)
        {
            if (checkpointNodeIds.Count == 1
                && TryBuildScoreRouteRecorderGoalFallback(
                    graph,
                    checkpointNodeIds[0],
                    finalSample.X,
                    finalSample.Y,
                    out var endpointRoute))
            {
                return endpointRoute;
            }

            if (TryBuildScoreRouteRecorderDirectFallback(
                    graph,
                    finalSample.X,
                    finalSample.Y,
                    out var directRoute))
            {
                return directRoute;
            }

            failureMessage = "could not build a usable route from recorded samples";
            return new List<int>();
        }

        var routeNodeIds = new List<int> { checkpointNodeIds[0] };
        for (var index = 1; index < checkpointNodeIds.Count; index += 1)
        {
            var currentNodeId = routeNodeIds[^1];
            var targetNodeId = checkpointNodeIds[index];
            if (currentNodeId == targetNodeId)
            {
                continue;
            }

            var bridge = graph.FindRoute(currentNodeId, targetNodeId);
            if (bridge is null || bridge.Length < 2)
            {
                if (index == checkpointNodeIds.Count - 1)
                {
                    if (TryBuildScoreRouteRecorderGoalFallback(
                            graph,
                            currentNodeId,
                            finalSample.X,
                            finalSample.Y,
                            out var endpointRoute))
                    {
                        return routeNodeIds
                            .Concat(endpointRoute.Skip(1))
                            .ToList();
                    }

                    failureMessage = $"could not bridge checkpoint nodes {currentNodeId}->{targetNodeId}";
                    return routeNodeIds;
                }

                continue;
            }

            for (var bridgeIndex = 1; bridgeIndex < bridge.Length; bridgeIndex += 1)
            {
                if (routeNodeIds[^1] != bridge[bridgeIndex])
                {
                    routeNodeIds.Add(bridge[bridgeIndex]);
                }
            }
        }

        return routeNodeIds;
    }

    private static bool TryBuildScoreRouteRecorderGoalFallback(
        BotNavigationRuntimeGraph graph,
        int startNodeId,
        float goalX,
        float goalY,
        out List<int> routeNodeIds)
    {
        routeNodeIds = new List<int>();
        if (graph.TryFindRouteToGoalRadius(
                startNodeId,
                goalX,
                goalY,
                ScoreRouteRecorderNodeSnapDistance * 1.5f,
                out var route,
                out _)
            && route.Length > 1)
        {
            routeNodeIds.AddRange(route);
            return true;
        }

        if (graph.TryFindBestPartialRoute(
                startNodeId,
                goalX,
                goalY,
                minimumImprovementDistance: 64f,
                out route,
                out _)
            && route.Length > 1)
        {
            routeNodeIds.AddRange(route);
            return true;
        }

        return false;
    }

    private bool TryBuildScoreRouteRecorderDirectFallback(
        BotNavigationRuntimeGraph graph,
        float goalX,
        float goalY,
        out List<int> routeNodeIds)
    {
        routeNodeIds = new List<int>();
        for (var index = _scoreRouteRecorderSamples.Count - 1; index >= 0; index -= 1)
        {
            var startNodeId = _scoreRouteRecorderSamples[index].NodeId;
            if (startNodeId < 0)
            {
                continue;
            }

            if (TryBuildScoreRouteRecorderGoalFallback(graph, startNodeId, goalX, goalY, out routeNodeIds))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFinalizeScoreRouteRecorderTerminalRoute(
        BotNavigationRuntimeGraph graph,
        IReadOnlyList<int> routeNodeIds,
        float goalX,
        float goalY,
        out List<int> finalizedRouteNodeIds,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        finalizedRouteNodeIds = routeNodeIds.ToList();
        if (finalizedRouteNodeIds.Count < 2)
        {
            failureMessage = "route did not contain enough nodes";
            return false;
        }

        if (IsScoreRouteRecorderTerminalNodeCloseEnough(graph, finalizedRouteNodeIds[^1], goalX, goalY))
        {
            return true;
        }

        if (TryResolveScoreRouteRecorderTerminalNode(graph, _scoreRouteRecorderSamples.Count - 1, out var terminalNodeId))
        {
            for (var prefixIndex = finalizedRouteNodeIds.Count - 1; prefixIndex >= 0; prefixIndex -= 1)
            {
                var prefixNodeId = finalizedRouteNodeIds[prefixIndex];
                var bridge = graph.FindRoute(prefixNodeId, terminalNodeId);
                if (bridge is null || bridge.Length < 2)
                {
                    continue;
                }

                var candidate = finalizedRouteNodeIds
                    .Take(prefixIndex + 1)
                    .Concat(bridge.Skip(1))
                    .ToList();
                if (candidate.Count >= 2
                    && IsScoreRouteRecorderTerminalNodeCloseEnough(graph, candidate[^1], goalX, goalY))
                {
                    finalizedRouteNodeIds = candidate;
                    return true;
                }
            }
        }

        failureMessage = string.Create(
            CultureInfo.InvariantCulture,
            $"resolved route ended too far from recorded finish ({finalizedRouteNodeIds[^1]} -> {goalX:0.#},{goalY:0.#})");
        return false;
    }

    private static bool IsScoreRouteRecorderTerminalNodeCloseEnough(
        BotNavigationRuntimeGraph graph,
        int nodeId,
        float goalX,
        float goalY)
    {
        if (!graph.TryGetNode(nodeId, out var node))
        {
            return false;
        }

        return Vector2.Distance(new Vector2(node.X, node.Y), new Vector2(goalX, goalY))
            <= ScoreRouteRecorderTerminalGoalTolerance;
    }

    private bool TryResolveScoreRouteRecorderTerminalNode(
        BotNavigationRuntimeGraph graph,
        int sampleIndex,
        out int nodeId)
    {
        nodeId = -1;
        var sample = _scoreRouteRecorderSamples[sampleIndex];
        var startIndex = Math.Max(0, sampleIndex - (ScoreRouteRecorderCheckpointWindowRadius * 2));
        var endIndex = Math.Min(_scoreRouteRecorderSamples.Count - 1, sampleIndex + ScoreRouteRecorderCheckpointWindowRadius);
        var candidateCounts = new Dictionary<int, int>();
        for (var index = startIndex; index <= endIndex; index += 1)
        {
            var candidateNodeId = _scoreRouteRecorderSamples[index].NodeId;
            if (candidateNodeId < 0)
            {
                continue;
            }

            candidateCounts[candidateNodeId] = candidateCounts.GetValueOrDefault(candidateNodeId) + 1;
        }

        foreach (var candidate in candidateCounts
                     .OrderByDescending(static entry => entry.Value)
                     .ThenBy(entry =>
                     {
                         if (!graph.TryGetNode(entry.Key, out var candidateNode))
                         {
                             return float.PositiveInfinity;
                         }

                         return Vector2.Distance(new Vector2(candidateNode.X, candidateNode.Y), new Vector2(sample.X, sample.Y));
                     }))
        {
            if (!graph.TryGetNode(candidate.Key, out _))
            {
                continue;
            }

            nodeId = candidate.Key;
            return true;
        }

        if (graph.TryFindNearestNode(
                sample.X,
                sample.Y,
                ScoreRouteRecorderNodeSnapDistance * 2f,
                requireGroundSupport: false,
                out var nearestNode))
        {
            nodeId = nearestNode.Id;
            return true;
        }

        return false;
    }

    private bool TryResolveScoreRouteRecorderCheckpointNode(
        BotNavigationRuntimeGraph graph,
        int sampleIndex,
        int previousAcceptedNodeId,
        out int nodeId)
    {
        nodeId = -1;
        var sample = _scoreRouteRecorderSamples[sampleIndex];
        var startIndex = Math.Max(0, sampleIndex - ScoreRouteRecorderCheckpointWindowRadius);
        var endIndex = Math.Min(_scoreRouteRecorderSamples.Count - 1, sampleIndex + ScoreRouteRecorderCheckpointWindowRadius);
        var candidateCounts = new Dictionary<int, int>();
        for (var index = startIndex; index <= endIndex; index += 1)
        {
            var candidateNodeId = _scoreRouteRecorderSamples[index].NodeId;
            if (candidateNodeId < 0)
            {
                continue;
            }

            candidateCounts[candidateNodeId] = candidateCounts.GetValueOrDefault(candidateNodeId) + 1;
        }

        foreach (var candidate in candidateCounts
                     .OrderByDescending(static entry => entry.Value)
                     .ThenBy(entry =>
                     {
                         if (!graph.TryGetNode(entry.Key, out var candidateNode))
                         {
                             return float.PositiveInfinity;
                         }

                         return Vector2.Distance(new Vector2(candidateNode.X, candidateNode.Y), new Vector2(sample.X, sample.Y));
                     }))
        {
            if (!graph.TryGetNode(candidate.Key, out _))
            {
                continue;
            }

            if (previousAcceptedNodeId >= 0)
            {
                var bridge = graph.FindRoute(previousAcceptedNodeId, candidate.Key);
                if (bridge is null || bridge.Length < 2)
                {
                    continue;
                }
            }

            nodeId = candidate.Key;
            return true;
        }

        if (graph.TryFindNearestNode(
                sample.X,
                sample.Y,
                ScoreRouteRecorderNodeSnapDistance * 1.5f,
                requireGroundSupport: false,
                out var nearestNode))
        {
            if (previousAcceptedNodeId < 0
                || graph.FindRoute(previousAcceptedNodeId, nearestNode.Id) is { Length: > 1 })
            {
                nodeId = nearestNode.Id;
                return true;
            }
        }

        return false;
    }

    private bool TryLoadScoreRouteRecorderGraph(
        out BotNavigationRuntimeGraph graph,
        out BotNavigationAsset asset,
        out string sourceLabel)
    {
        graph = default!;
        asset = default!;
        sourceLabel = string.Empty;

        if (BotNavigationAssetStore.TryLoadModernShippedAssetForEditing(_world.Level, out var savedAsset, out _, out _, out _)
            && savedAsset is not null)
        {
            asset = savedAsset;
            graph = new BotNavigationRuntimeGraph(asset);
            sourceLabel = "saved modern graph";
            return true;
        }

        try
        {
            var fingerprint = BotNavigationLevelFingerprint.Compute(_world.Level);
            asset = BotNavigationModernPointGraphBuilder.Build(_world.Level, fingerprint);
            graph = new BotNavigationRuntimeGraph(asset);
            sourceLabel = "fresh modern graph";
            return true;
        }
        catch (Exception ex)
        {
            AddConsoleLine($"score route recorder failed: could not load nav graph ({ex.Message})");
            return false;
        }
    }

    private bool TryValidateScoreRouteRecorderTape(
        CharacterClassDefinition classDefinition,
        float sourceX,
        float sourceY,
        float targetX,
        float targetY,
        IReadOnlyList<BotNavigationInputFrame> tape,
        bool requireGroundedArrival,
        out BotNavigationTraversalKind traversalKind,
        out float cost,
        out string failureMessage)
    {
        if (BotNavigationRecordedTraversalValidator.TryValidate(
                _world.Level,
                classDefinition,
                sourceX,
                sourceY,
                targetX,
                targetY,
                _scoreRouteRecorderTeam,
                tape,
                requireGroundedArrival,
                _world.Config.FixedDeltaSeconds,
                out cost,
                out traversalKind,
                out failureMessage))
        {
            return true;
        }

        var oppositeTeam = _scoreRouteRecorderTeam == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        return BotNavigationRecordedTraversalValidator.TryValidate(
            _world.Level,
            classDefinition,
            sourceX,
            sourceY,
            targetX,
            targetY,
            oppositeTeam,
            tape,
            requireGroundedArrival,
            _world.Config.FixedDeltaSeconds,
            out cost,
            out traversalKind,
            out failureMessage);
    }

    private void PrintScoreRouteRecorderStatus()
    {
        if (!IsScoreRouteRecorderActive())
        {
            AddConsoleLine("score route recorder: idle");
            return;
        }

        AddConsoleLine(
            $"score route recorder: {_scoreRouteRecorderCaptureState.ToString().ToLowerInvariant()} {_scoreRouteRecorderTeam} {DescribeScoreRouteRecorderProfile(_scoreRouteRecorderProfile)} {DescribeScoreRouteRecorderPhase(_scoreRouteRecorderPhase)} ticks={_scoreRouteRecorderSamples.Count}");
    }

    private void CancelScoreRouteRecorder(string reason)
    {
        if (!IsScoreRouteRecorderActive())
        {
            AddConsoleLine("score route recorder: idle");
            return;
        }

        ClearScoreRouteRecorderState();
        AddConsoleLine(reason);
    }

    private void ClearScoreRouteRecorderState()
    {
        _scoreRouteRecorderCaptureState = ScoreRouteRecorderCaptureState.None;
        _scoreRouteRecorderGraph = null;
        _scoreRouteRecorderGraphAsset = null;
        _scoreRouteRecorderPhase = BotNavigationScoreRoutePhase.None;
        _scoreRouteRecorderAutoRun = false;
        _scoreRouteRecorderPreviousCarryingIntel = false;
        _scoreRouteRecorderSamples.Clear();
        _scoreRouteRecorderCaptureInput = default;
        _scoreRouteRecorderPendingInputSample = default;
    }

    private bool IsScoreRouteRecorderActive()
    {
        return _scoreRouteRecorderCaptureState != ScoreRouteRecorderCaptureState.None;
    }

    private static bool TryParseScoreRouteRecorderPhase(string value, out BotNavigationScoreRoutePhase phase)
    {
        switch (value.ToLowerInvariant())
        {
            case "capture":
            case "cap":
            case "objective":
                phase = BotNavigationScoreRoutePhase.CaptureObjective;
                return true;
            case "attack":
            case "attackintel":
            case "intel":
            case "pickup":
                phase = BotNavigationScoreRoutePhase.AttackIntel;
                return true;
            case "return":
            case "score":
            case "home":
                phase = BotNavigationScoreRoutePhase.ReturnIntel;
                return true;
            default:
                phase = BotNavigationScoreRoutePhase.None;
                return false;
        }
    }

    private bool TryAdvanceScoreRouteRecorderAutoRun()
    {
        var isCarryingIntel = _world.LocalPlayer.IsCarryingIntel;
        if (_scoreRouteRecorderAutoRun
            && _scoreRouteRecorderPhase == BotNavigationScoreRoutePhase.AttackIntel
            && !_scoreRouteRecorderPreviousCarryingIntel
            && isCarryingIntel)
        {
            if (TrySaveScoreRouteRecorderPhase(
                    requestedLabel: null,
                    out var attackEntry,
                    out var outputPath,
                    out var warnings))
            {
                AddConsoleLine(
                    $"score route auto-saved attack: {attackEntry.Team} {DescribeScoreRouteRecorderProfile(attackEntry.Profile)} nodes={attackEntry.RouteNodeIds.Count} segments={attackEntry.Segments.Count}");
                AddConsoleLine($"score route nodes: {string.Join("->", attackEntry.RouteNodeIds)}");
                AddConsoleLine($"score route path: {outputPath}");
                foreach (var warning in warnings)
                {
                    AddConsoleLine($"score route warning: {warning}");
                }

                _scoreRouteRecorderPhase = BotNavigationScoreRoutePhase.ReturnIntel;
                _scoreRouteRecorderCaptureState = ScoreRouteRecorderCaptureState.Recording;
                _scoreRouteRecorderSamples.Clear();
                _scoreRouteRecorderPreviousCarryingIntel = isCarryingIntel;
                AddConsoleLine("score route recorder switched to return.");
                return true;
            }

            _scoreRouteRecorderPreviousCarryingIntel = isCarryingIntel;
            return false;
        }

        if (_scoreRouteRecorderAutoRun
            && _scoreRouteRecorderPhase == BotNavigationScoreRoutePhase.ReturnIntel
            && _scoreRouteRecorderPreviousCarryingIntel
            && !isCarryingIntel
            && HasScoreRouteRecorderCompletedReturnObjective())
        {
            if (TrySaveScoreRouteRecorderPhase(
                    requestedLabel: null,
                    out var returnEntry,
                    out var outputPath,
                    out var warnings))
            {
                AddConsoleLine(
                    $"score route auto-saved return: {returnEntry.Team} {DescribeScoreRouteRecorderProfile(returnEntry.Profile)} nodes={returnEntry.RouteNodeIds.Count} segments={returnEntry.Segments.Count}");
                AddConsoleLine($"score route nodes: {string.Join("->", returnEntry.RouteNodeIds)}");
                AddConsoleLine($"score route path: {outputPath}");
                foreach (var warning in warnings)
                {
                    AddConsoleLine($"score route warning: {warning}");
                }

                ClearScoreRouteRecorderState();
                AddConsoleLine("score route recorder finished run.");
                return true;
            }

            _scoreRouteRecorderPreviousCarryingIntel = isCarryingIntel;
            return false;
        }

        _scoreRouteRecorderPreviousCarryingIntel = isCarryingIntel;
        return false;
    }

    private bool HasScoreRouteRecorderCompletedReturnObjective()
    {
        if (_world.MatchRules.Mode == GameModeKind.CaptureTheFlag)
        {
            var ownIntel = _scoreRouteRecorderTeam == PlayerTeam.Blue ? _world.BlueIntel : _world.RedIntel;
            return ownIntel.IsAtBase
                && MathF.Abs(_world.LocalPlayer.X - ownIntel.HomeX) <= 96f
                && MathF.Abs(_world.LocalPlayer.Y - ownIntel.HomeY) <= 96f;
        }

        for (var index = 0; index < _world.ControlPoints.Count; index += 1)
        {
            var point = _world.ControlPoints[index];
            if (point.CappingTeam != _scoreRouteRecorderTeam)
            {
                continue;
            }

            if (point.CapTimeTicks <= 0 || point.CappingTicks <= 0f)
            {
                continue;
            }

            return MathF.Abs(_world.LocalPlayer.X - point.Marker.CenterX) <= Math.Max(96f, point.Marker.Width)
                && MathF.Abs(_world.LocalPlayer.Y - point.Marker.CenterY) <= Math.Max(64f, point.Marker.Height);
        }

        return false;
    }

    private static string DescribeScoreRouteRecorderPhase(BotNavigationScoreRoutePhase phase)
    {
        return phase switch
        {
            BotNavigationScoreRoutePhase.CaptureObjective => "capture",
            BotNavigationScoreRoutePhase.AttackIntel => "attack",
            BotNavigationScoreRoutePhase.ReturnIntel => "return",
            _ => "unknown",
        };
    }

    private static string DescribeScoreRouteRecorderProfile(BotNavigationProfile profile)
    {
        return profile switch
        {
            BotNavigationProfile.Light => "light",
            BotNavigationProfile.Heavy => "heavy",
            _ => "standard",
        };
    }

    private static string BuildScoreRouteRecorderRouteKey(
        PlayerTeam team,
        BotNavigationProfile profile,
        BotNavigationScoreRoutePhase phase)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"manual:{team.ToString().ToLowerInvariant()}:{DescribeScoreRouteRecorderProfile(profile)}:{DescribeScoreRouteRecorderPhase(phase)}");
    }

    private static string? ExtractScoreRouteRecorderTrailingArgument(string commandText, string subcommand)
    {
        var prefix = $"score_route_rec {subcommand}";
        if (!commandText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var trailing = commandText[prefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(trailing) ? null : trailing;
    }

    private static IReadOnlyList<BotNavigationInputFrame> BuildNormalizedScoreRouteRecorderTape(
        IReadOnlyList<ScoreRouteRecorderInputSample> samples,
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

            frames.Add(CreateScoreRouteRecorderFrame(currentSample, currentTicks, sampleDurationSeconds));
            currentSample = samples[index];
            currentTicks = 1;
        }

        frames.Add(CreateScoreRouteRecorderFrame(currentSample, currentTicks, sampleDurationSeconds));
        return frames;
    }

    private static int GetScoreRouteRecorderTickCount(IReadOnlyList<BotNavigationInputFrame> tape)
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

    private static BotNavigationInputFrame CreateScoreRouteRecorderFrame(
        ScoreRouteRecorderInputSample sample,
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

    private static ScoreRouteRecorderInputSample CreateScoreRouteRecorderInputSample(PlayerInputSnapshot input)
    {
        var right = input.Right && !input.Left;
        var left = input.Left && !input.Right;
        return new ScoreRouteRecorderInputSample(left, right, input.Up);
    }

    private readonly record struct ScoreRouteRecorderInputSample(bool Left, bool Right, bool Up)
    {
        public bool HasAnyInput => Left || Right || Up;
    }

    private readonly record struct ScoreRouteRecorderSample(
        int NodeId,
        float X,
        float Y,
        bool IsGrounded,
        ScoreRouteRecorderInputSample Input);

    private readonly record struct ScoreRouteRecorderVisit(int NodeId, int StartSampleIndex, int EndSampleIndex);
}
