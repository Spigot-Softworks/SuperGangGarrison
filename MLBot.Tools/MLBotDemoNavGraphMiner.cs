using OpenGarrison.BotAI;
using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotDemoNavGraphMiner
{
    private const float NodeMergeDistance = 22f;
    private const float MinimumWalkEdgeDistance = 64f;
    private const float MaximumWalkEdgeDistance = 180f;
    private const int MinimumSegmentTicks = 6;

    public static int Run(string[] args)
    {
        var dataRoot = GetOption(args, "--root")
            ?? Path.Combine(RuntimePaths.ApplicationRoot, ".mlbot-data");
        var mapName = GetRequiredOption(args, "--map");
        if (mapName is null)
        {
            return 1;
        }

        var areaIndex = ParseIntOption(args, "--area", 1);
        var outputPath = GetOption(args, "--out");
        var includeSynthetic = HasFlag(args, "--include-synthetic");
        var successfulOnly = !HasFlag(args, "--include-unsuccessful");
        var writeShipped = HasFlag(args, "--write-shipped");
        var keepAutomaticJumpEdges = HasFlag(args, "--keep-auto-jumps");
        var suppressScoreRoutes = HasFlag(args, "--suppress-score-routes");
        var includeMirrored = HasFlag(args, "--include-mirrored");
        var mirroredClasses = ParseClassOptions(args, "--mirror-class");

        var sourceContentRoot = ProjectSourceLocator.FindDirectory("Core/Content") ?? ContentRoot.Path;
        ContentRoot.Initialize(sourceContentRoot);

        var level = SimpleLevelFactory.CreateImportedLevel(mapName, areaIndex);
        if (level is null)
        {
            Console.Error.WriteLine($"failed_to_load_level map={mapName} area={areaIndex}");
            return 2;
        }

        var documents = EnumerateDemoFiles(dataRoot, includeSynthetic)
            .Select(TryLoadDemo)
            .Where(document => document is not null)
            .Select(document => document!)
            .Where(document => string.Equals(document.Metadata.LevelName, level.Name, StringComparison.OrdinalIgnoreCase))
            .Where(document => document.Metadata.MapAreaIndex == level.MapAreaIndex)
            .Where(document => !successfulOnly || document.Metadata.Success || document.Metadata.ShortCapture)
            .ToArray();
        if (documents.Length == 0)
        {
            Console.WriteLine($"demo-nav map={level.Name} area={level.MapAreaIndex} status=no_matching_demos root={dataRoot}");
            return 2;
        }

        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        var baseAsset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint, validateTraversals: false);
        var result = BuildAugmentedAsset(level, baseAsset, documents, keepAutomaticJumpEdges, includeMirrored, mirroredClasses);
        var validation = BotNavigationAssetValidator.Validate(level, result.Asset);
        var audit = BotNavigationAssetValidator.AuditAttackReachability(level, result.Asset);

        if (writeShipped)
        {
            var outputDirectory = ProjectSourceLocator.FindDirectory("Core/Content/BotNav")
                ?? Path.Combine(sourceContentRoot, "BotNav");
            BotNavigationAssetStore.SaveShipped(result.Asset, outputDirectory);
            outputPath = Path.Combine(outputDirectory, BotNavigationAssetStore.GetModernAssetFileName(level.Name, level.MapAreaIndex));
            if (suppressScoreRoutes)
            {
                BotNavigationScoreRouteStore.Save(new BotNavigationScoreRouteAsset
                {
                    LevelName = level.Name,
                    MapAreaIndex = level.MapAreaIndex,
                    LevelFingerprint = fingerprint,
                    Routes = Array.Empty<BotNavigationScoreRouteEntry>(),
                });
            }
        }
        else
        {
            outputPath ??= Path.Combine(dataRoot, "demo-nav", BotNavigationAssetStore.GetModernAssetFileName(level.Name, level.MapAreaIndex));
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SaveAsset(outputPath, result.Asset);
        }

        Console.WriteLine(
            $"demo-nav map={level.Name} area={level.MapAreaIndex} status=written path={outputPath} demos={documents.Length} baseNodes={baseAsset.Nodes.Count} baseEdges={baseAsset.Edges.Count} addedNodes={result.AddedNodes} addedEdges={result.AddedEdges} validEdges={result.ValidatedSegments} rejected={result.RejectedSegments} nav={(validation.IsStructurallyValid ? "ok" : validation.BuildSummary())} reachability={(audit.IsStructurallyValid ? "ok" : audit.BuildSummary())}");
        if (result.PrunedBaseJumpEdges > 0)
        {
            Console.WriteLine($"demo-nav-pruned untapedAutoJumpEdges={result.PrunedBaseJumpEdges}");
        }
        if (result.MirroredDocuments > 0)
        {
            Console.WriteLine($"demo-nav-mirrored documents={result.MirroredDocuments}");
        }

        foreach (var route in ProbeRoutes(level, result.Asset))
        {
            Console.WriteLine(route);
        }

        return validation.IsStructurallyValid ? 0 : 3;
    }

    private static DemoNavBuildResult BuildAugmentedAsset(
        SimpleLevel level,
        BotNavigationAsset baseAsset,
        MLBotDemonstrationDocument[] documents,
        bool keepAutomaticJumpEdges,
        bool includeMirrored,
        HashSet<PlayerClass> mirroredClasses)
    {
        var nodes = baseAsset.Nodes
            .Select(static node => new BotNavigationNode
            {
                Id = node.Id,
                X = node.X,
                Y = node.Y,
                SurfaceId = node.SurfaceId,
                Kind = node.Kind,
                Team = node.Team,
                Label = node.Label,
                RequiresGroundSupport = node.RequiresGroundSupport,
                ReverseOnlyBlockedFromNodeIds = node.ReverseOnlyBlockedFromNodeIds.ToArray(),
            })
            .ToList();
        var prunedBaseJumpEdges = keepAutomaticJumpEdges
            ? 0
            : baseAsset.Edges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Jump && edge.InputTape.Count == 0);
        var edges = baseAsset.Edges
            .Where(edge => keepAutomaticJumpEdges || edge.Kind != BotNavigationTraversalKind.Jump || edge.InputTape.Count > 0)
            .Select(static edge => new BotNavigationEdge
            {
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Kind = edge.Kind,
                Cost = edge.Cost,
                InputTape = edge.InputTape.ToArray(),
            })
            .ToList();
        var edgeKeys = edges
            .Select(static edge => GetEdgeKey(edge.FromNodeId, edge.ToNodeId))
            .ToHashSet();
        var nextSurfaceId = nodes.Count == 0 ? -1 : nodes.Min(static node => node.SurfaceId) - 1;
        var addedNodes = 0;
        var addedEdges = 0;
        var validatedSegments = 0;
        var rejectedSegments = 0;

        var documentsToMirror = includeMirrored
            ? documents
            : mirroredClasses.Count > 0
                ? documents
                    .Where(document => mirroredClasses.Contains(document.Metadata.ClassId))
                    .ToArray()
                : [];
        var documentsToMine = documentsToMirror.Length > 0
            ? documents.Concat(documentsToMirror.Select(document => MirrorDocument(level, document))).ToArray()
            : documents;
        var mirroredDocuments = documentsToMirror.Length;

        foreach (var document in documentsToMine)
        {
            var classDefinition = BotNavigationClasses.GetDefinition(document.Metadata.ClassId);
            var samples = document.Samples
                .Where(static sample => !sample.Died)
                .OrderBy(static sample => sample.Tick)
                .ToArray();
            if (samples.Length < 2)
            {
                continue;
            }

            var startIndex = FindNextGroundedSample(samples, 0);
            while (startIndex >= 0 && startIndex < samples.Length - 1)
            {
                var endIndex = FindSegmentEnd(samples, startIndex, classDefinition);
                if (endIndex <= startIndex)
                {
                    startIndex = FindNextGroundedSample(samples, startIndex + 1);
                    continue;
                }

                if (TryBuildDemoEdge(level, document.Metadata, classDefinition, samples, startIndex, endIndex, nodes, ref nextSurfaceId, edgeKeys, edges, out var addedNodeCount))
                {
                    addedNodes += addedNodeCount;
                    addedEdges += 1;
                    validatedSegments += 1;
                    startIndex = endIndex;
                }
                else
                {
                    rejectedSegments += 1;
                    startIndex = FindNextGroundedSample(samples, startIndex + 1);
                }
            }
        }

        var asset = new BotNavigationAsset
        {
            FormatVersion = BotNavigationAssetStore.CurrentFormatVersion,
            LevelName = baseAsset.LevelName,
            MapAreaIndex = baseAsset.MapAreaIndex,
            ClassId = null,
            Profile = BotNavigationProfile.Standard,
            LevelFingerprint = baseAsset.LevelFingerprint,
            BuildStrategy = BotNavigationBuildStrategy.ModernClientBotPointGraph,
            BuiltUtc = DateTime.UtcNow,
            Stats = new BotNavigationBuildStats
            {
                SurfaceCount = baseAsset.Stats.SurfaceCount,
                CandidateNodeCount = nodes.Count,
                SurfaceSampleNodeCount = baseAsset.Stats.SurfaceSampleNodeCount,
                AutoAnchorNodeCount = baseAsset.Stats.AutoAnchorNodeCount + addedNodes,
                NodeCount = nodes.Count,
                EdgeCount = edges.Count,
                WalkEdgeCount = edges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Walk),
                JumpEdgeCount = edges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Jump),
                DropEdgeCount = edges.Count(static edge => edge.Kind == BotNavigationTraversalKind.Drop),
                HintEdgeCount = addedEdges,
                BuildMilliseconds = baseAsset.Stats.BuildMilliseconds,
            },
            Nodes = nodes,
            Edges = edges,
        };

        return new DemoNavBuildResult(asset, addedNodes, addedEdges, validatedSegments, rejectedSegments, prunedBaseJumpEdges, mirroredDocuments);
    }

    private static MLBotDemonstrationDocument MirrorDocument(SimpleLevel level, MLBotDemonstrationDocument document)
    {
        return new MLBotDemonstrationDocument
        {
            Metadata = new MLBotDemonstrationMetadata
            {
                SchemaVersion = document.Metadata.SchemaVersion,
                LevelName = document.Metadata.LevelName,
                MapAreaIndex = document.Metadata.MapAreaIndex,
                Mode = document.Metadata.Mode,
                Team = GetOpposingTeam(document.Metadata.Team),
                ClassId = document.Metadata.ClassId,
                RequestedPhase = document.Metadata.RequestedPhase,
                CaptureKind = document.Metadata.CaptureKind,
                PolicyModelPath = document.Metadata.PolicyModelPath,
                Label = string.IsNullOrWhiteSpace(document.Metadata.Label)
                    ? "mirrored"
                    : $"{document.Metadata.Label}|mirrored",
                RecordedAtUtc = document.Metadata.RecordedAtUtc,
                TickCount = document.Metadata.TickCount,
                CaptureMaxTicks = document.Metadata.CaptureMaxTicks,
                ShortCapture = document.Metadata.ShortCapture,
                Success = document.Metadata.Success,
                Outcome = document.Metadata.Outcome,
            },
            Samples = document.Samples
                .Select(sample => MirrorSample(level, sample))
                .ToArray(),
        };
    }

    private static MLBotDemonstrationSample MirrorSample(SimpleLevel level, MLBotDemonstrationSample sample)
    {
        return new MLBotDemonstrationSample
        {
            Tick = sample.Tick,
            ResolvedPhase = sample.ResolvedPhase,
            Observation = MirrorObservation(level, sample.Observation),
            Action = MirrorAction(level, sample.Action),
            HumanAction = sample.HumanAction.HasValue ? MirrorAction(level, sample.HumanAction.Value) : null,
            SuggestedAction = sample.SuggestedAction.HasValue ? MirrorAction(level, sample.SuggestedAction.Value) : null,
            UsedHumanOverride = sample.UsedHumanOverride,
            NextObservation = MirrorObservation(level, sample.NextObservation),
            PickedUpIntel = sample.PickedUpIntel,
            ScoredIntel = sample.ScoredIntel,
            Died = sample.Died,
            EpisodeEnded = sample.EpisodeEnded,
        };
    }

    private static MLBotObservation MirrorObservation(SimpleLevel level, MLBotObservation observation)
    {
        return observation with
        {
            Team = GetOpposingTeam(observation.Team),
            BotX = MirrorX(level, observation.BotX),
            VelocityX = -observation.VelocityX,
            FacingDirectionX = -observation.FacingDirectionX,
            PreviousVelocityX = -observation.PreviousVelocityX,
            PreviousPositionDeltaX = -observation.PreviousPositionDeltaX,
            PreviousFacingDirectionX = -observation.PreviousFacingDirectionX,
            PreviousMoveInput = -observation.PreviousMoveInput,
        };
    }

    private static MLBotAction MirrorAction(SimpleLevel level, MLBotAction action)
    {
        return action with
        {
            MoveDirection = -action.MoveDirection,
            AimWorldX = MirrorX(level, action.AimWorldX),
        };
    }

    private static float MirrorX(SimpleLevel level, float x)
    {
        return level.Bounds.Width - x;
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    }

    private static int FindSegmentEnd(
        MLBotDemonstrationSample[] samples,
        int startIndex,
        CharacterClassDefinition classDefinition)
    {
        var start = samples[startIndex].Observation;
        var sawAirborne = false;
        var sawJump = false;
        for (var index = startIndex + 1; index < samples.Length; index += 1)
        {
            var observation = samples[index].Observation;
            var action = ResolveActualAction(samples[index - 1]);
            sawAirborne |= !observation.IsGrounded;
            sawJump |= action.Jump;
            var distance = DistanceBetween(start.BotX, GetFeetY(start, classDefinition), observation.BotX, GetFeetY(observation, classDefinition));
            var elapsedTicks = Math.Max(0, samples[index].Tick - samples[startIndex].Tick);
            if (observation.IsGrounded
                && elapsedTicks >= MinimumSegmentTicks
                && ((sawAirborne && elapsedTicks >= 4)
                    || sawJump
                    || distance >= MaximumWalkEdgeDistance))
            {
                return index;
            }

            if (!sawAirborne && distance >= MinimumWalkEdgeDistance && elapsedTicks >= MinimumSegmentTicks)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindNextGroundedSample(MLBotDemonstrationSample[] samples, int startIndex)
    {
        for (var index = Math.Max(0, startIndex); index < samples.Length; index += 1)
        {
            if (samples[index].Observation.IsGrounded)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryBuildDemoEdge(
        SimpleLevel level,
        MLBotDemonstrationMetadata metadata,
        CharacterClassDefinition classDefinition,
        MLBotDemonstrationSample[] samples,
        int startIndex,
        int endIndex,
        List<BotNavigationNode> nodes,
        ref int nextSurfaceId,
        HashSet<long> edgeKeys,
        List<BotNavigationEdge> edges,
        out int addedNodeCount)
    {
        addedNodeCount = 0;
        var startObservation = samples[startIndex].Observation;
        var endObservation = samples[endIndex].Observation;
        var startFeetY = GetFeetY(startObservation, classDefinition);
        var endFeetY = GetFeetY(endObservation, classDefinition);
        var tape = BuildInputTape(samples, startIndex, endIndex);
        var hasJump = tape.Any(static frame => frame.Up);
        var isDrop = !hasJump && endFeetY > startFeetY + 12f;

        var kind = hasJump
            ? BotNavigationTraversalKind.Jump
            : isDrop
                ? BotNavigationTraversalKind.Drop
                : BotNavigationTraversalKind.Walk;

        var inputTape = Array.Empty<BotNavigationInputFrame>() as IReadOnlyList<BotNavigationInputFrame>;
        var cost = MathF.Max(1f, DistanceBetween(startObservation.BotX, startFeetY, endObservation.BotX, endFeetY));
        if (kind == BotNavigationTraversalKind.Jump)
        {
            if (!BotNavigationMovementValidator.TryValidateRecordedTraversalTape(
                    level,
                    classDefinition,
                    startObservation.BotX,
                    startObservation.BotY,
                    endObservation.BotX,
                    endObservation.BotY,
                    metadata.Team,
                    tape,
                    requireGroundedArrival: true,
                    out cost,
                    out _))
            {
                return false;
            }

            inputTape = tape;
        }
        else if (kind == BotNavigationTraversalKind.Walk)
        {
            if (!BotNavigationMovementValidator.TryBuildGroundTape(
                    level,
                    classDefinition,
                    startObservation.BotX,
                    startObservation.BotY,
                    endObservation.BotX,
                    endObservation.BotY,
                    metadata.Team,
                    out _,
                    out cost))
            {
                return false;
            }
        }
        else
        {
            cost *= 0.8f;
            inputTape = tape;
        }

        var fromNode = GetOrAddDemoNode(nodes, ref nextSurfaceId, startObservation.BotX, startFeetY, metadata, out var addedFrom);
        var toNode = GetOrAddDemoNode(nodes, ref nextSurfaceId, endObservation.BotX, endFeetY, metadata, out var addedTo);
        addedNodeCount = (addedFrom ? 1 : 0) + (addedTo ? 1 : 0);
        if (fromNode.Id == toNode.Id)
        {
            return false;
        }

        var edgeKey = GetEdgeKey(fromNode.Id, toNode.Id);
        if (!edgeKeys.Add(edgeKey))
        {
            var allowWalkJumpRepair = kind == BotNavigationTraversalKind.Jump;
            return TryReplaceUntapedEdge(edges, fromNode.Id, toNode.Id, kind, cost, inputTape, allowWalkJumpRepair);
        }

        edges.Add(new BotNavigationEdge
        {
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id,
            Kind = kind,
            Cost = MathF.Max(1f, cost),
            InputTape = inputTape.ToArray(),
        });
        return true;
    }

    private static bool TryReplaceUntapedEdge(
        List<BotNavigationEdge> edges,
        int fromNodeId,
        int toNodeId,
        BotNavigationTraversalKind kind,
        float cost,
        IReadOnlyList<BotNavigationInputFrame> inputTape,
        bool allowWalkJumpRepair)
    {
        if (inputTape.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < edges.Count; index += 1)
        {
            var existing = edges[index];
            if (existing.FromNodeId != fromNodeId || existing.ToNodeId != toNodeId)
            {
                continue;
            }

            if (existing.InputTape.Count > 0 || !allowWalkJumpRepair)
            {
                return false;
            }

            edges[index] = new BotNavigationEdge
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Kind = kind,
                Cost = MathF.Max(1f, cost),
                InputTape = inputTape.ToArray(),
            };
            return true;
        }

        return false;
    }

    private static BotNavigationNode GetOrAddDemoNode(
        List<BotNavigationNode> nodes,
        ref int nextSurfaceId,
        float x,
        float feetY,
        MLBotDemonstrationMetadata metadata,
        out bool added)
    {
        var bestDistanceSquared = NodeMergeDistance * NodeMergeDistance;
        BotNavigationNode? best = null;
        for (var index = 0; index < nodes.Count; index += 1)
        {
            var node = nodes[index];
            var distanceSquared = DistanceSquared(node.X, node.Y, x, feetY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            best = node;
        }

        if (best is not null)
        {
            added = false;
            return best;
        }

        var created = new BotNavigationNode
        {
            Id = nodes.Count,
            X = x,
            Y = feetY,
            SurfaceId = nextSurfaceId--,
            Kind = BotNavigationNodeKind.RouteAnchor,
            Team = null,
            Label = $"demo:{metadata.Team}:{metadata.ClassId}:{metadata.RequestedPhase}",
            RequiresGroundSupport = true,
        };
        nodes.Add(created);
        added = true;
        return created;
    }

    private static BotNavigationInputFrame[] BuildInputTape(
        MLBotDemonstrationSample[] samples,
        int startIndex,
        int endIndex)
    {
        var frames = new List<BotNavigationInputFrame>();
        BotNavigationInputFrame? currentFrame = null;
        var currentTicks = 0;

        for (var index = startIndex; index < endIndex; index += 1)
        {
            var action = ResolveActualAction(samples[index]);
            var frame = new BotNavigationInputFrame
            {
                Left = action.MoveDirection < 0,
                Right = action.MoveDirection > 0,
                Up = action.Jump,
                Ticks = 1,
            };
            if (currentFrame is not null
                && currentFrame.Left == frame.Left
                && currentFrame.Right == frame.Right
                && currentFrame.Up == frame.Up)
            {
                currentTicks += 1;
                continue;
            }

            if (currentFrame is not null)
            {
                frames.Add(WithTicks(currentFrame, currentTicks));
            }

            currentFrame = frame;
            currentTicks = 1;
        }

        if (currentFrame is not null)
        {
            frames.Add(WithTicks(currentFrame, currentTicks));
        }

        return frames.ToArray();
    }

    private static BotNavigationInputFrame WithTicks(BotNavigationInputFrame frame, int ticks)
    {
        return new BotNavigationInputFrame
        {
            Left = frame.Left,
            Right = frame.Right,
            Up = frame.Up,
            Ticks = Math.Max(1, ticks),
        };
    }

    private static MLBotAction ResolveActualAction(MLBotDemonstrationSample sample)
    {
        return sample.UsedHumanOverride && sample.HumanAction.HasValue
            ? sample.HumanAction.Value
            : sample.Action;
    }

    private static IEnumerable<string> EnumerateDemoFiles(string rootPath, bool includeSynthetic)
    {
        if (!Directory.Exists(rootPath))
        {
            return Array.Empty<string>();
        }

        string[] allowedNames = includeSynthetic
            ?
            [
                "mlbot-demos",
                "mlbot-dagger-demos",
                "mlbot-synthetic",
                "mlbot-teacher",
                "teacher-demos",
            ]
            :
            [
                "mlbot-demos",
                "mlbot-dagger-demos",
            ];

        return Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories)
            .Where(path => allowedNames.Any(name => path.Contains($"{Path.DirectorySeparatorChar}{name}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)));
    }

    private static MLBotDemonstrationDocument? TryLoadDemo(string path)
    {
        try
        {
            var document = JsonConfigurationFile.LoadOrCreate(path, static () => new MLBotDemonstrationDocument());
            return document.Metadata is not null && document.Samples.Length > 0
                ? document
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ProbeRoutes(SimpleLevel level, BotNavigationAsset asset)
    {
        var graph = new BotNavigationRuntimeGraph(asset);
        foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
        {
            var classDefinition = BotNavigationClasses.GetDefinition(PlayerClass.Scout);
            var spawn = level.GetSpawn(team, 0);
            var startY = spawn.Y + classDefinition.CollisionBottom;
            if (!graph.TryFindNearestNode(spawn.X, startY, 220f, requireGroundSupport: true, out var startNode))
            {
                yield return $"demo-nav-route team={team} status=start_miss";
                continue;
            }

            var goal = ResolveAttackObjective(level, team);
            if (!goal.HasValue)
            {
                yield return $"demo-nav-route team={team} status=objective_miss";
                continue;
            }

            if (graph.TryFindRouteToGoalRadius(startNode.Id, goal.Value.X, goal.Value.Y, 220f, out var route, out var goalNodeId))
            {
                yield return $"demo-nav-route team={team} status=pass start={startNode.Id} goal={goalNodeId} length={route.Length}";
            }
            else
            {
                yield return $"demo-nav-route team={team} status=fail start={startNode.Id} objective=({goal.Value.X:0.0},{goal.Value.Y:0.0})";
            }
        }
    }

    private static (float X, float Y)? ResolveAttackObjective(SimpleLevel level, PlayerTeam team)
    {
        if (level.Mode == GameModeKind.CaptureTheFlag)
        {
            var enemyTeam = team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
            var enemyIntel = level.GetIntelBase(enemyTeam);
            return enemyIntel.HasValue ? (enemyIntel.Value.X, enemyIntel.Value.Y) : null;
        }

        var captureZones = level.GetRoomObjects(RoomObjectType.CaptureZone);
        if (captureZones.Count > 0)
        {
            var captureZone = captureZones[0];
            return (captureZone.CenterX, captureZone.CenterY);
        }

        var controlPoints = level.GetRoomObjects(RoomObjectType.ControlPoint);
        return controlPoints.Count > 0
            ? (controlPoints[0].CenterX, controlPoints[0].CenterY)
            : null;
    }

    private static void SaveAsset(string path, BotNavigationAsset asset)
    {
        var outputDirectory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var tempDirectory = Path.Combine(Path.GetDirectoryName(path) ?? RuntimePaths.ApplicationRoot, ".tmp-demo-nav");
        Directory.CreateDirectory(tempDirectory);
        BotNavigationAssetStore.SaveShipped(asset, tempDirectory);
        var generatedPath = Path.Combine(tempDirectory, BotNavigationAssetStore.GetModernAssetFileName(asset.LevelName, asset.MapAreaIndex));
        File.Copy(generatedPath, path, overwrite: true);
        File.Delete(generatedPath);
    }

    private static float GetFeetY(MLBotObservation observation, CharacterClassDefinition classDefinition)
    {
        return observation.BotY + classDefinition.CollisionBottom;
    }

    private static float DistanceBetween(float ax, float ay, float bx, float by)
    {
        return MathF.Sqrt(DistanceSquared(ax, ay, bx, by));
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    private static long GetEdgeKey(int fromNodeId, int toNodeId)
    {
        return ((long)fromNodeId << 32) | (uint)toNodeId;
    }

    private static string? GetOption(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length - 1; index += 1)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string? GetRequiredOption(string[] args, string optionName)
    {
        var value = GetOption(args, optionName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        Console.Error.WriteLine($"missing required option: {optionName}");
        return null;
    }

    private static int ParseIntOption(string[] args, string optionName, int fallback)
    {
        var value = GetOption(args, optionName);
        return int.TryParse(value, out var parsedValue)
            ? parsedValue
            : fallback;
    }

    private static bool HasFlag(string[] args, string optionName)
    {
        return args.Any(arg => string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<PlayerClass> ParseClassOptions(string[] args, string optionName)
    {
        var values = new HashSet<PlayerClass>();
        for (var index = 0; index < args.Length - 1; index += 1)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<PlayerClass>(args[index + 1], ignoreCase: true, out var classId))
            {
                values.Add(classId);
            }
        }

        return values;
    }

    private sealed record DemoNavBuildResult(
        BotNavigationAsset Asset,
        int AddedNodes,
        int AddedEdges,
        int ValidatedSegments,
        int RejectedSegments,
        int PrunedBaseJumpEdges,
        int MirroredDocuments);
}
