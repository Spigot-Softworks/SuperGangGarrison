using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Builds the contact-first alpha graph. Geometry proposes a small set of
/// standable surface samples; OG2 PlayerEntity movement is the authority that
/// emits directional contacts between them.
/// </summary>
public static class Og2LocalContactGraphBuilder
{
    private const float SurfaceSampleInset = 8f;
    private const float MinimumSurfaceWidth = 12f;
    private const float SurfaceSampleStep = 8f;
    // Contacts are intentionally local. Keep the default horizon short enough
    // for runtime generation, while retaining an exhaustive mode for map
    // certification when a route exposes a transition beyond the local band.
    private const int DefaultSweepTicks = 96;
    private static int SweepTicks =>
        int.TryParse(Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_SWEEP_TICKS"), out var configured)
            ? Math.Clamp(configured, 24, 128)
            : DefaultSweepTicks;
    // Stair contacts often require the bot to clear a short sequence of
    // six-pixel rises before the launch point. Sampling only the first three
    // ticks can certify Scout's early/double-jump route while missing the
    // later single-jump route used by slower classes. Keep the sweep bounded,
    // but cover the complete local run-up window used by stock maps.
    private static readonly int[] JumpSweepTicks =
    [
        0, 10, 18, 30, 46, 60, 72,
    ];
    private static readonly int[] LogicalObjectiveJumpSweepTicks = [0, 6, 10, 14, 18];
    private const float SurfaceMatchTolerance = 10f;
    private const float ContactBucket = 16f;
    private const int MaximumContactsPerSurfacePair = 8;
    private const float AnchorSearchDistance = 320f;
    private const float AnchorCompletionHorizontalSlack = 28f;
    private const float AnchorCompletionVerticalSlack = 28f;
    private const float AnchorDirectAttachVerticalTolerance = 24f;
    // Stock KOTH/arena maps may expose only a logical control-point marker.
    // Its center is above the walkable support surface, while explicit
    // CaptureZone markers already provide the physically meaningful goal.
    // Keep ordinary anchors strict; widen only this typed logical-objective
    // contract so the graph can represent the actual capture position.
    private const float LogicalObjectiveAttachVerticalTolerance = 64f;
    private const float LogicalObjectiveCompletionVerticalSlack = 64f;
    private const float LogicalObjectiveSearchVerticalDistance = 360f;
    // Heavy has the stock no-air-jump profile and the lowest run power. A
    // 32-tick local sweep can miss a valid long run-up contact even though the
    // same transition is executable in OG2. Keep the shared graph topology,
    // but give this slow grounded profile the extended certification horizon.
    private const int SlowGroundedProfileSweepTicks = 96;

    // The node network is shared, but contact recipes are certified against
    // the actual OG2 movement constants. Runtime testing showed that a Heavy
    // recipe can be structurally valid for another class yet fail when that
    // class executes it, so each distinct movement signature is probed here.
    // Identical movement signatures share one recipe and one capability mask.
    private static readonly PlayerClass[] AllMovementClasses = Enum.GetValues<PlayerClass>();

    public static NavGraph Build(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var stopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();
        var traceBuild = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BUILD_TRACE") is "1" or "true" or "TRUE";
        var geometry = VerifiedNavCandidateBuilder.Build(level, new VerifiedNavBuildOptions
        {
            Team = PlayerTeam.Red,
            ClassId = PlayerClass.Scout,
            SampleStep = SurfaceSampleStep,
            MinSurfaceWidth = MinimumSurfaceWidth,
            SurfaceEndpointInset = SurfaceSampleInset,
        });
        if (traceBuild)
        {
            Console.WriteLine($"[botbrain] contact-nav stage level={level.Name} name=geometry surfaces={geometry.Surfaces.Count} elapsedMs={stageStopwatch.Elapsed.TotalMilliseconds:0.00}");
        }
        stageStopwatch.Restart();

        var maximumCollisionBottom = Enum.GetValues<PlayerClass>()
            .Select(CharacterClassCatalog.GetDefinition)
            .Max(static definition => definition.CollisionBottom);
        var minimumCollisionBottom = Enum.GetValues<PlayerClass>()
            .Select(CharacterClassCatalog.GetDefinition)
            .Min(static definition => definition.CollisionBottom);

        var nodes = new List<NavNode>(geometry.Surfaces.Count * 4 + 32);
        var nodeBySample = new Dictionary<SampleKey, int>();
        var surfaceNodeIndices = new Dictionary<int, List<int>>();
        foreach (var surface in geometry.Surfaces)
        {
            var sampleXs = BuildSurfaceSamples(surface);
            var indices = new List<int>(sampleXs.Count);
            foreach (var x in sampleXs)
            {
                var nodeIndex = GetOrAddSurfaceNode(
                    nodes,
                    nodeBySample,
                    surface,
                    x,
                    maximumCollisionBottom);
                indices.Add(nodeIndex);
            }

            surfaceNodeIndices[surface.Id] = indices;
        }
        if (traceBuild)
        {
            Console.WriteLine($"[botbrain] contact-nav stage level={level.Name} name=surface-nodes nodes={nodes.Count} elapsedMs={stageStopwatch.Elapsed.TotalMilliseconds:0.00}");
        }

        stageStopwatch.Restart();
        var contacts = DiscoverContacts(
            level,
            geometry,
            nodes,
            nodeBySample,
            minimumCollisionBottom,
            maximumCollisionBottom);
        if (traceBuild)
        {
            Console.WriteLine($"[botbrain] contact-nav stage level={level.Name} name=contacts contacts={contacts.Count} elapsedMs={stageStopwatch.Elapsed.TotalMilliseconds:0.00}");
        }
        stageStopwatch.Restart();
        // Contact samples become graph nodes before adjacency is allocated so
        // the immutable NavGraph receives a complete node/index relationship.
        foreach (var contact in contacts.Values)
        {
            GetOrAddSurfaceNode(
                nodes,
                nodeBySample,
                geometry.Surfaces[contact.FromSurfaceId],
                contact.EntryX,
                maximumCollisionBottom);
            GetOrAddSurfaceNode(
                nodes,
                nodeBySample,
                geometry.Surfaces[contact.ToSurfaceId],
                contact.ExitX,
                maximumCollisionBottom);
        }

        var adjacency = CreateAdjacency(nodes.Count + level.RoomObjects.Count + level.RedSpawns.Count + level.BlueSpawns.Count + 64);
        var edgeIndex = new Dictionary<DirectedEdgeKey, int>();
        // Contact nodes are not optional waypoints: they must be reachable from
        // the ordinary corridor samples on their surface. Rebuild the per-
        // surface ordering after contact discovery so every local contact is
        // connected into its parent interval.
        surfaceNodeIndices = BuildAllSurfaceNodeIndices(nodes, geometry.Surfaces);
        AddSameSurfaceWalkEdges(
            level,
            geometry.Surfaces,
            surfaceNodeIndices,
            nodes,
            adjacency,
            edgeIndex,
            minimumCollisionBottom,
            maximumCollisionBottom);
        if (traceBuild)
        {
            Console.WriteLine($"[botbrain] contact-nav stage level={level.Name} name=surface-edges edges={CountEdges(adjacency)} elapsedMs={stageStopwatch.Elapsed.TotalMilliseconds:0.00}");
        }
        stageStopwatch.Restart();

        foreach (var contact in contacts.Values)
        {
            var fromNode = GetOrAddSurfaceNode(
                nodes,
                nodeBySample,
                geometry.Surfaces[contact.FromSurfaceId],
                contact.EntryX,
                maximumCollisionBottom);
            var toNode = GetOrAddSurfaceNode(
                nodes,
                nodeBySample,
                geometry.Surfaces[contact.ToSurfaceId],
                contact.ExitX,
                maximumCollisionBottom);
            EnsureAdjacencyCapacity(adjacency, nodes.Count);
            AddContactEdge(
                fromNode,
                toNode,
                contact,
                geometry.Surfaces[contact.ToSurfaceId],
                nodes,
                adjacency,
                edgeIndex,
                minimumCollisionBottom,
                maximumCollisionBottom);
        }

        // Geometry extraction can expose a short surface that has no valid
        // same-surface span and no OG2 contact in either direction. Keeping
        // that dead sample pollutes the immutable graph and makes structural
        // reachability reports lie about graph quality. Prune only nodes that
        // are proven to have neither incoming nor outgoing edges; this never
        // removes a valid transition endpoint.
        var pruned = PruneIsolatedSurfaceNodes(nodes, adjacency);
        if (pruned.RemovedCount > 0)
        {
            nodes = pruned.Nodes;
            adjacency = pruned.Adjacency;
            nodeBySample = BuildNodeBySample(nodes);
            edgeIndex = BuildEdgeIndex(adjacency, nodes.Count);
        }

        var spawnAnchors = new List<NavSpawnAnchor>(level.RedSpawns.Count + level.BlueSpawns.Count);
        AddAnchors(
            level,
            geometry.Surfaces,
            nodes,
            nodeBySample,
            adjacency,
            edgeIndex,
            spawnAnchors,
            minimumCollisionBottom,
            maximumCollisionBottom);
        if (traceBuild)
        {
            Console.WriteLine($"[botbrain] contact-nav stage level={level.Name} name=anchors nodes={nodes.Count} elapsedMs={stageStopwatch.Elapsed.TotalMilliseconds:0.00}");
        }

        // Objective attachment can create a new surface sample after the
        // contact pass. If that candidate has no certified edge in either
        // direction, remove the dead traversal sample while preserving
        // objective and spawn terminal nodes for their own validation rules.
        var postAnchorPruned = PruneIsolatedSurfaceNodes(
            nodes,
            adjacency,
            retainNonSurfaceNodes: true);
        if (postAnchorPruned.RemovedCount > 0)
        {
            nodes = postAnchorPruned.Nodes;
            adjacency = postAnchorPruned.Adjacency;
        }

        var graph = new NavGraph(
            nodes.ToArray(),
            adjacency,
            level.Name,
            level.Mode,
            spawnAnchors,
            isOg2Alpha: true);

        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BUILD_TRACE") is "1" or "true" or "TRUE")
        {
            Console.WriteLine(
                $"[botbrain] contact-nav build level={level.Name} surfaces={geometry.Surfaces.Count} " +
                $"contacts={contacts.Count} nodes={graph.NodeCount} edges={CountEdges(adjacency)} " +
                $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.00}");
        }

        return graph;
    }

    private static Dictionary<ContactKey, ContactRecord> DiscoverContacts(
        SimpleLevel level,
        VerifiedNavCandidateGraph geometry,
        List<NavNode> nodes,
        Dictionary<SampleKey, int> nodeBySample,
        float minimumCollisionBottom,
        float maximumCollisionBottom)
    {
        var contacts = new Dictionary<ContactKey, ContactRecord>();
        var seedSurfaceIds = BuildContactSeedSurfaceIds(level, geometry.Surfaces, maximumCollisionBottom);
        var surfaceIndex = new SurfaceSpatialIndex(geometry.Surfaces);
        var representativeClasses = EnumerateRepresentativeClasses().ToArray();
        var jobs = new List<(
            bool CarryingIntel,
            PlayerClass PlayerClass,
            PlayerTeam ProbeTeam,
            int SupportedTeamMask)>();
        var carryingStates = level.Mode == GameModeKind.CaptureTheFlag
            ? new[] { false, true }
            : new[] { false };
        foreach (var carryingIntel in carryingStates)
        {
            // PlayerEntity probes are independent per movement signature. The
            // level's gate set is immutable during graph construction; warm
            // the state-specific caches before fanning out so workers perform
            // read-only collision queries against the same state. Static maps
            // have identical collision geometry for both teams, so one Red
            // probe can certify both team masks. Maps with team-dependent
            // blockers retain the separate Red/Blue probes.
            if (CanShareContactGeometryAcrossTeams(level, carryingIntel))
            {
                level.GetBlockingTeamGates(PlayerTeam.Red, carryingIntel);
                jobs.AddRange(
                    representativeClasses.Select(playerClass =>
                        (carryingIntel, playerClass, PlayerTeam.Red, BotBrainTeamMask.All)));
            }
            else
            {
                level.GetBlockingTeamGates(PlayerTeam.Red, carryingIntel);
                level.GetBlockingTeamGates(PlayerTeam.Blue, carryingIntel);
                jobs.AddRange(
                    representativeClasses.SelectMany(playerClass =>
                        Enum.GetValues<PlayerTeam>().Select(probeTeam =>
                            (carryingIntel, playerClass, probeTeam, BotBrainTeamMask.For(probeTeam)))));
            }
        }

        var contactsByJob = new Dictionary<ContactKey, ContactRecord>[jobs.Count];
        var jobElapsedMs = new double[jobs.Count];
        Parallel.For(
            0,
            jobs.Count,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = ResolveContactBuildParallelism(),
            },
            jobIndex =>
            {
                var job = jobs[jobIndex];
                var jobStopwatch = Stopwatch.StartNew();
                contactsByJob[jobIndex] = DiscoverContactsForClassState(
                    level,
                    geometry,
                    surfaceIndex,
                    seedSurfaceIds,
                    job.PlayerClass,
                    job.CarryingIntel,
                    job.ProbeTeam,
                    job.SupportedTeamMask);
                jobStopwatch.Stop();
                jobElapsedMs[jobIndex] = jobStopwatch.Elapsed.TotalMilliseconds;
            });

        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_PROFILE") is "1" or "true" or "TRUE")
        {
            for (var jobIndex = 0; jobIndex < jobs.Count; jobIndex += 1)
            {
                var job = jobs[jobIndex];
                Console.WriteLine(
                    $"[botbrain] contact-nav job level={level.Name} class={job.PlayerClass} " +
                    $"carry={(job.CarryingIntel ? 1 : 0)} team={job.ProbeTeam} " +
                    $"teamMask={job.SupportedTeamMask} " +
                    $"elapsedMs={jobElapsedMs[jobIndex]:0.00} contacts={contactsByJob[jobIndex].Count}");
            }
        }

        // Preserve the serial builder's deterministic state ordering. A
        // parallel completion-order merge changes contact insertion order,
        // node indices, and equal-cost A* tie breaks.
        foreach (var classContacts in contactsByJob)
        {
            foreach (var pair in classContacts)
            {
                if (contacts.TryGetValue(pair.Key, out var existing))
                {
                    var preferred = pair.Value.JumpTriggerTick > existing.JumpTriggerTick
                        ? pair.Value
                        : existing;
                    contacts[pair.Key] = preferred with
                    {
                        SupportedClassMask = existing.SupportedClassMask | pair.Value.SupportedClassMask,
                        SupportedTeamMask = existing.SupportedTeamMask | pair.Value.SupportedTeamMask,
                        ProbeTicks = Math.Min(existing.ProbeTicks, pair.Value.ProbeTicks),
                    };
                }
                else
                {
                    contacts.Add(pair.Key, pair.Value);
                }
            }
        }

        return contacts;
    }

    private static Dictionary<ContactKey, ContactRecord> DiscoverContactsForClassState(
        SimpleLevel level,
        VerifiedNavCandidateGraph geometry,
        SurfaceSpatialIndex surfaceIndex,
        IReadOnlySet<int> seedSurfaceIds,
        PlayerClass playerClass,
        bool carryingIntel,
        PlayerTeam probeTeam,
        int supportedTeamMask)
    {
        var contacts = new Dictionary<ContactKey, ContactRecord>();
        // Admission is capped per directed surface pair. Counting that cap by
        // scanning every existing contact made dense maps quadratic in the
        // number of discovered contacts. Keep the same rule in O(1).
        var contactCountsByPair = new Dictionary<ContactPairKey, int>();
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        var classMask = ResolveMovementClassMask(playerClass);
        var sweepTicks = ResolveContactSweepTicks(playerClass);
        // Every trajectory starts with Spawn/TeleportTo and restores the same
        // probe state. Reusing one probe per class/team/carrying job avoids
        // allocating tens of thousands of PlayerEntity instances during a
        // graph build without changing the OG2 movement authority.
        var probe = new PlayerEntity(-910_001, definition, "Og2LocalContactProbe");
        var visitedSurfaces = new HashSet<int>(seedSurfaceIds);
        var pendingSurfaces = new Queue<int>(seedSurfaceIds);
        while (pendingSurfaces.Count > 0)
        {
            var surface = geometry.Surfaces[pendingSurfaces.Dequeue()];
            var discoveredSurfaceIds = new HashSet<int>();
            foreach (var startX in BuildContactSamples(surface))
            {
                foreach (var direction in new[] { -1, 1 })
                {
                    if (HasPotentialTransition(
                            geometry.Surfaces,
                            surfaceIndex,
                            level,
                            definition,
                            surface,
                            startX,
                            direction,
                            sweepTicks,
                            jumped: false))
                    {
                        DiscoverSweep(
                            level,
                            geometry,
                            definition,
                            classMask,
                            supportedTeamMask,
                            carryingIntel,
                            probeTeam,
                            surface,
                            startX,
                            direction,
                            sweepTicks,
                            jumpTick: -1,
                            probe,
                            contacts,
                            contactCountsByPair,
                            discoveredSurfaceIds);
                    }

                    foreach (var jumpTick in JumpSweepTicks)
                    {
                        if (jumpTick >= sweepTicks)
                        {
                            continue;
                        }

                        if (!HasPotentialTransition(
                                geometry.Surfaces,
                                surfaceIndex,
                                level,
                                definition,
                                surface,
                                startX,
                                direction,
                                sweepTicks,
                                jumped: true))
                        {
                            continue;
                        }

                        DiscoverSweep(
                                level,
                                geometry,
                                definition,
                            classMask,
                            supportedTeamMask,
                            carryingIntel,
                            probeTeam,
                            surface,
                            startX,
                            direction,
                            sweepTicks,
                            jumpTick,
                            probe,
                            contacts,
                            contactCountsByPair,
                            discoveredSurfaceIds);
                    }
                }
            }

            foreach (var destinationSurfaceId in discoveredSurfaceIds)
            {
                if (!visitedSurfaces.Add(destinationSurfaceId))
                {
                    continue;
                }

                pendingSurfaces.Enqueue(destinationSurfaceId);
            }
        }

        return contacts;
    }

    private static bool CanShareContactGeometryAcrossTeams(SimpleLevel level, bool carryingIntel)
    {
        var redGates = level.GetBlockingTeamGates(PlayerTeam.Red, carryingIntel);
        var blueGates = level.GetBlockingTeamGates(PlayerTeam.Blue, carryingIntel);
        if (redGates.Count != blueGates.Count)
        {
            return false;
        }

        for (var index = 0; index < redGates.Count; index += 1)
        {
            var redGate = redGates[index];
            var blueGate = blueGates[index];
            if (redGate.Type != blueGate.Type
                || redGate.Left != blueGate.Left
                || redGate.Top != blueGate.Top
                || redGate.Right != blueGate.Right
                || redGate.Bottom != blueGate.Bottom)
            {
                return false;
            }
        }

        return true;
    }

    private static int ResolveContactBuildParallelism()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BUILD_PARALLELISM"), out var configured))
        {
            return Math.Clamp(configured, 1, Environment.ProcessorCount);
        }

        // Graph generation is an offline/prewarm operation, not a render-loop
        // job. On the development machine the previous processor-count-minus-
        // one default left a full class/team wave serialized behind an unused
        // logical core; using the available workers materially shortens dense
        // map generation while the deterministic merge still preserves graph
        // ordering.
        return Math.Max(1, Environment.ProcessorCount);
    }

    private static int ResolveContactSweepTicks(PlayerClass playerClass)
    {
        var configuredExtendedClasses = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_EXTENDED_CONTACT_CLASSES");
        if (string.IsNullOrWhiteSpace(configuredExtendedClasses))
        {
            return playerClass == PlayerClass.Heavy
                ? Math.Max(SweepTicks, SlowGroundedProfileSweepTicks)
                : SweepTicks;
        }

        var extendedClasses = configuredExtendedClasses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var configuredClass)
                ? (PlayerClass?)configuredClass
                : null)
            .Where(static configuredClass => configuredClass.HasValue)
            .Select(static configuredClass => configuredClass!.Value)
            .ToHashSet();
        if (extendedClasses.Count == 0 || extendedClasses.Contains(playerClass))
        {
            return SweepTicks;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BASE_SWEEP_TICKS"), out var baseSweepTicks))
        {
            return Math.Clamp(baseSweepTicks, 24, 128);
        }

        return Math.Min(SweepTicks, 32);
    }

    private static IReadOnlySet<int> BuildContactSeedSurfaceIds(
        SimpleLevel level,
        IReadOnlyList<VerifiedNavSurface> surfaces,
        float maximumCollisionBottom)
    {
        var seeds = new HashSet<int>();
        var anchors = new List<(float X, float Y)>();
        anchors.AddRange(level.RedSpawns.Select(static spawn => (spawn.X, spawn.Y)));
        anchors.AddRange(level.BlueSpawns.Select(static spawn => (spawn.X, spawn.Y)));
        anchors.AddRange(level.IntelBases.Select(static intel => (intel.X, intel.Y)));
        anchors.AddRange(level.RoomObjects
            .Where(static roomObject => roomObject.Type is RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator)
            .Select(static roomObject => (roomObject.CenterX, roomObject.CenterY)));

        foreach (var anchor in anchors)
        {
            foreach (var candidate in surfaces
                         .Where(surface => surface.Left <= anchor.X + AnchorSearchDistance
                             && surface.Right >= anchor.X - AnchorSearchDistance)
                         .Select(surface => (
                             Surface: surface,
                             Distance: MathF.Abs(surface.Top - (anchor.Y + maximumCollisionBottom))
                                 + DistanceToInterval(surface, anchor.X)))
                         .OrderBy(static candidate => candidate.Distance)
                         .Take(4))
            {
                seeds.Add(candidate.Surface.Id);
            }
        }

        // Imported maps without explicit anchors still need a useful graph.
        // Falling back to all surfaces keeps this an optimization rather than
        // a correctness dependency.
        return seeds.Count == 0
            ? new HashSet<int>(surfaces.Select(static surface => surface.Id))
            : seeds;
    }

    private static bool HasPotentialTransition(
        IReadOnlyList<VerifiedNavSurface> surfaces,
        SurfaceSpatialIndex surfaceIndex,
        SimpleLevel level,
        CharacterClassDefinition definition,
        VerifiedNavSurface source,
        float startX,
        int direction,
        int sweepTicks,
        bool jumped)
    {
        // This is deliberately a one-sided, conservative broad phase. It can
        // only reject a sweep when no destination surface lies inside the
        // movement envelope; the OG2 PlayerEntity sweep remains the authority
        // for the emitted contact and its execution recipe.
        var maxTravel = definition.MaxRunSpeed
            * (sweepTicks / (float)SimulationConfig.DefaultTicksPerSecond)
            + 64f;
        var minX = direction < 0 ? startX - maxTravel : startX - 32f;
        var maxX = direction > 0 ? startX + maxTravel : startX + 32f;
        var maximumRise = jumped
            ? MathF.Max(640f, definition.JumpSpeed * 2.5f)
            : 32f;
        var minimumTop = source.Top - maximumRise;
        var maximumTop = source.Top + level.Bounds.Height;

        return surfaceIndex.HasCandidate(source.Id, minX, maxX, minimumTop, maximumTop);
    }

    private static void DiscoverSweep(
        SimpleLevel level,
        VerifiedNavCandidateGraph geometry,
        CharacterClassDefinition definition,
        int classMask,
        int supportedTeamMask,
        bool carryingIntel,
        PlayerTeam probeTeam,
        VerifiedNavSurface startSurface,
        float startX,
        int direction,
        int sweepTicks,
        int jumpTick,
        PlayerEntity probe,
        Dictionary<ContactKey, ContactRecord> contacts,
        Dictionary<ContactPairKey, int> contactCountsByPair,
        HashSet<int> discoveredSurfaceIds)
    {
        var startY = startSurface.Top - definition.CollisionBottom;
        probe.Spawn(probeTeam, startX, startY);
        probe.TeleportTo(startX, startY);
        if (carryingIntel)
        {
            probe.PickUpIntel();
        }
        probe.ResolveBlockingOverlap(level, probeTeam);
        probe.RestoreMovementProbeState(isGrounded: true, probe.MaxAirJumps, direction);

        var previousInput = default(PlayerInputSnapshot);
        var currentSurfaceId = startSurface.Id;
        var pendingSurfaceId = -1;
        var pendingTicks = 0;
        var entryX = startX;
        var entryBottom = probe.Bottom;
        var launchCaptured = false;
        var jumpStartsGrounded = false;
        var launchX = 0f;
        var launchY = 0f;
        var launchHorizontalSpeed = 0f;
        for (var tick = 0; tick < sweepTicks; tick += 1)
        {
            var jump = tick == jumpTick;
            var input = new PlayerInputSnapshot(
                Left: direction < 0,
                Right: direction > 0,
                Up: jump,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: probe.X + direction * 256f,
                AimWorldY: probe.Y,
                DebugKill: false,
                DropIntel: false,
                UseAbility: false);
            var jumpPressed = input.Up && !previousInput.Up;
            var canConsumeJump = jumpPressed && (probe.IsGrounded || probe.RemainingAirJumps > 0);
            if (canConsumeJump && !launchCaptured)
            {
                launchCaptured = true;
                jumpStartsGrounded = probe.IsGrounded;
                launchX = probe.X;
                launchY = probe.Y;
                launchHorizontalSpeed = probe.HorizontalSpeed;
            }
            probe.Advance(
                input,
                jumpPressed,
                level,
                probeTeam,
                1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;

            if (!probe.IsGrounded)
            {
                pendingSurfaceId = -1;
                pendingTicks = 0;
                continue;
            }

            var detectedSurfaceId = FindSurfaceAt(geometry.Surfaces, probe);
            if (detectedSurfaceId < 0 || detectedSurfaceId == currentSurfaceId)
            {
                continue;
            }

            if (pendingSurfaceId == detectedSurfaceId)
            {
                pendingTicks += 1;
            }
            else
            {
                pendingSurfaceId = detectedSurfaceId;
                pendingTicks = 1;
            }

            var sourceSurface = geometry.Surfaces[currentSurfaceId];
            var destinationSurface = geometry.Surfaces[detectedSurfaceId];
            var isOneTickDownwardLanding = !launchCaptured
                && destinationSurface.Top > sourceSurface.Top + SurfaceMatchTolerance;
            var requiredGroundedTicks = isOneTickDownwardLanding ? 1 : 2;
            if (pendingTicks < requiredGroundedTicks)
            {
                continue;
            }

            // A jump sweep is a separate input experiment. If the requested
            // jump was not actually consumed (for example, a grounded probe
            // reached a stair lip with no air jump remaining), its landing is
            // not evidence for a no-jump Walk/Fall contact. The explicit
            // jumpTick == -1 sweep is the only source allowed to certify that
            // capability; otherwise a rejected jump input can manufacture a
            // false direct walk edge across a stair chain.
            if (jumpTick >= 0 && !launchCaptured)
            {
                continue;
            }

            var kind = ResolveContactKind(
                launchCaptured,
                probe.Bottom,
                entryBottom,
                destinationSurface.Kind == VerifiedNavSurfaceKind.DropdownPlatform);
            var record = new ContactRecord(
                currentSurfaceId,
                detectedSurfaceId,
                entryX,
                probe.X,
                startY,
                probe.Bottom,
                kind,
                launchCaptured ? jumpTick : -1,
                Math.Max(1, tick + 1),
                direction,
                classMask,
                carryingIntel,
                supportedTeamMask,
                launchCaptured ? launchX : entryX,
                launchCaptured ? launchY : startY,
                launchCaptured ? launchHorizontalSpeed : probe.HorizontalSpeed,
                launchCaptured && jumpStartsGrounded);
            if (ShouldTraceContact(currentSurfaceId, detectedSurfaceId, carryingIntel))
            {
                Console.WriteLine(
                    $"[botbrain] contact-trace from={currentSurfaceId} to={detectedSurfaceId} " +
                    $"carry={(carryingIntel ? 1 : 0)} start=({startX:0.0},{startY:0.0}) " +
                    $"actualCarry={(probe.IsCarryingIntel ? 1 : 0)} maxRun={probe.MaxRunSpeed:0.0} " +
                    $"entry=({entryX:0.0},{entryBottom:0.0}) landing=({probe.X:0.0},{probe.Bottom:0.0}) " +
                    $"kind={record.Kind} launchCaptured={(launchCaptured ? 1 : 0)} " +
                    $"jump={jumpTick} tick={tick + 1} launch=({record.LaunchX:0.0},{record.LaunchY:0.0})");
            }
            var key = new ContactKey(
                record.FromSurfaceId,
                record.ToSurfaceId,
                record.Kind,
                Quantize(record.EntryX),
                Quantize(record.ExitX),
                classMask,
                carryingIntel,
                record.JumpStartsGrounded);
            var recorded = false;
            if (contacts.TryGetValue(key, out var existing))
            {
                var preferred = record.JumpTriggerTick > existing.JumpTriggerTick
                    ? record
                    : existing;
                contacts[key] = preferred with
                {
                    SupportedClassMask = existing.SupportedClassMask | classMask,
                    SupportedTeamMask = existing.SupportedTeamMask | supportedTeamMask,
                    ProbeTicks = Math.Min(existing.ProbeTicks, record.ProbeTicks),
                };
                recorded = true;
            }
            else
            {
                var pairKey = new ContactPairKey(
                    record.FromSurfaceId,
                    record.ToSurfaceId,
                    classMask,
                    carryingIntel);
                var pairCount = contactCountsByPair.GetValueOrDefault(pairKey);
                if (pairCount < MaximumContactsPerSurfacePair)
                {
                    contacts.Add(key, record);
                    contactCountsByPair[pairKey] = pairCount + 1;
                    recorded = true;
                }
            }

            if (recorded)
            {
                discoveredSurfaceIds.Add(record.ToSurfaceId);

                // A sweep is a local contact experiment, not a route replay.
                // Once the first destination has been grounded and certified,
                // the frontier will enqueue that surface and probe it with its
                // own launch samples. Continuing through additional landings
                // in this same experiment duplicates work and causes long
                // horizons to grow superlinearly on stair-heavy maps.
                break;
            }

            currentSurfaceId = detectedSurfaceId;
            pendingSurfaceId = -1;
            pendingTicks = 0;
            entryX = probe.X;
            entryBottom = probe.Bottom;
            launchCaptured = false;
            launchX = 0f;
            launchY = 0f;
            launchHorizontalSpeed = 0f;
            jumpStartsGrounded = false;
        }
    }

    private static int FindSurfaceAt(
        IReadOnlyList<VerifiedNavSurface> surfaces,
        PlayerEntity player)
    {
        var bestId = -1;
        var bestError = float.MaxValue;
        var lowerTop = player.Bottom - SurfaceMatchTolerance;
        var upperTop = player.Bottom + SurfaceMatchTolerance;
        var firstCandidate = 0;
        var lastCandidate = surfaces.Count;
        while (firstCandidate < lastCandidate)
        {
            var midpoint = firstCandidate + ((lastCandidate - firstCandidate) / 2);
            if (surfaces[midpoint].Top < lowerTop)
            {
                firstCandidate = midpoint + 1;
            }
            else
            {
                lastCandidate = midpoint;
            }
        }

        for (var index = firstCandidate;
             index < surfaces.Count && surfaces[index].Top <= upperTop;
             index += 1)
        {
            var surface = surfaces[index];
            if (player.X < surface.Left - 2f || player.X > surface.Right + 2f)
            {
                continue;
            }

            var error = MathF.Abs(player.Bottom - surface.Top);
            if (error > SurfaceMatchTolerance || error >= bestError)
            {
                continue;
            }

            bestId = surface.Id;
            bestError = error;
        }

        return bestId;
    }

    private static NavEdgeKind ResolveContactKind(
        bool jumped,
        float destinationBottom,
        float sourceBottom,
        bool destinationIsDropdown)
    {
        if (destinationIsDropdown)
        {
            return NavEdgeKind.Dropdown;
        }

        if (jumped)
        {
            return NavEdgeKind.Jump;
        }

        return destinationBottom > sourceBottom + SurfaceMatchTolerance
            ? NavEdgeKind.Fall
            : NavEdgeKind.Walk;
    }

    private static void AddSameSurfaceWalkEdges(
        SimpleLevel level,
        IReadOnlyList<VerifiedNavSurface> surfaces,
        IReadOnlyDictionary<int, List<int>> surfaceNodeIndices,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        Dictionary<DirectedEdgeKey, int> edgeIndex,
        float minimumCollisionBottom,
        float maximumCollisionBottom)
    {
        foreach (var surface in surfaces)
        {
            if (!surfaceNodeIndices.TryGetValue(surface.Id, out var indices))
            {
                continue;
            }

            for (var index = 0; index + 1 < indices.Count; index += 1)
            {
                var fromNode = nodes[indices[index]];
                var toNode = nodes[indices[index + 1]];
                foreach (var carryingIntel in new[] { false, true })
                {
                    foreach (var playerClass in EnumerateRepresentativeClasses())
                    {
                        var supportedTeamMask = ResolveStaticWalkTeamMask(
                            level,
                            fromNode,
                            toNode,
                            playerClass,
                            carryingIntel,
                            maximumCollisionBottom);
                        if (supportedTeamMask == 0)
                        {
                            continue;
                        }

                        AddSimpleEdge(
                            indices[index],
                            indices[index + 1],
                            NavEdgeKind.Walk,
                            nodes,
                            adjacency,
                            edgeIndex,
                            BotBrainClassMask.For(playerClass),
                            minimumCollisionBottom,
                            maximumCollisionBottom,
                            surface.Id,
                            carryingIntel,
                            supportedTeamMask);
                    }
                }

                foreach (var carryingIntel in new[] { false, true })
                {
                    foreach (var playerClass in EnumerateRepresentativeClasses())
                    {
                        var supportedTeamMask = ResolveStaticWalkTeamMask(
                            level,
                            toNode,
                            fromNode,
                            playerClass,
                            carryingIntel,
                            maximumCollisionBottom);
                        if (supportedTeamMask == 0)
                        {
                            continue;
                        }

                        AddSimpleEdge(
                            indices[index + 1],
                            indices[index],
                            NavEdgeKind.Walk,
                            nodes,
                            adjacency,
                            edgeIndex,
                            BotBrainClassMask.For(playerClass),
                            minimumCollisionBottom,
                            maximumCollisionBottom,
                            surface.Id,
                            carryingIntel,
                            supportedTeamMask);
                    }
                }
            }
        }
    }

    private static int ResolveStaticWalkTeamMask(
        SimpleLevel level,
        NavNode fromNode,
        NavNode toNode,
        PlayerClass playerClass,
        bool carryingIntel,
        float maximumCollisionBottom)
    {
        var supportedTeamMask = 0;
        foreach (var team in Enum.GetValues<PlayerTeam>())
        {
            if (IsStaticWalkSpanClear(
                    level,
                    fromNode,
                    toNode,
                    playerClass,
                    team,
                    carryingIntel,
                    maximumCollisionBottom))
            {
                supportedTeamMask |= BotBrainTeamMask.For(team);
            }
        }

        return supportedTeamMask;
    }

    private static bool IsStaticWalkSpanClear(
        SimpleLevel level,
        NavNode fromNode,
        NavNode toNode,
        PlayerClass playerClass,
        PlayerTeam team,
        bool carryingIntel,
        float maximumCollisionBottom)
    {
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        var probe = new PlayerEntity(-910_002, definition, "Og2LocalWalkClearanceProbe");
        var probeY = fromNode.Y + maximumCollisionBottom - definition.CollisionBottom;
        probe.Spawn(team, fromNode.X, probeY);
        probe.TeleportTo(fromNode.X, probeY);
        if (carryingIntel)
        {
            probe.PickUpIntel();
        }
        probe.ResolveBlockingOverlap(level, team);

        var distance = MathF.Abs(toNode.X - fromNode.X);
        var samples = Math.Max(1, (int)MathF.Ceiling(distance / 8f));
        for (var sample = 0; sample <= samples; sample += 1)
        {
            var fraction = sample / (float)samples;
            var x = fromNode.X + ((toNode.X - fromNode.X) * fraction);
            if (!probe.CanOccupy(level, team, x, probeY))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<int, List<int>> BuildAllSurfaceNodeIndices(
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<VerifiedNavSurface> surfaces)
    {
        var result = surfaces.ToDictionary(static surface => surface.Id, static _ => new List<int>());
        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
        {
            if (nodes[nodeIndex].SurfaceId is { } surfaceId
                && result.TryGetValue(surfaceId, out var indices))
            {
                indices.Add(nodeIndex);
            }
        }

        foreach (var indices in result.Values)
        {
            indices.Sort((left, right) => nodes[left].X.CompareTo(nodes[right].X));
        }

        return result;
    }

    private static void AddContactEdge(
        int fromNode,
        int toNode,
        ContactRecord contact,
        VerifiedNavSurface destinationSurface,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        Dictionary<DirectedEdgeKey, int> edgeIndex,
        float minimumCollisionBottom,
        float maximumCollisionBottom)
    {
        var target = nodes[toNode];
        var completion = CreateSurfaceCompletion(
            target.X,
            destinationSurface.Top,
            destinationSurface.Id,
            minimumCollisionBottom,
            maximumCollisionBottom);
        // Keep the measured launch state on every jump contact. Whether a
        // class consumes it is decided by SteeringMachine; this lets slower
        // members of a movement profile opt into the proof without forcing
        // the representative's timing onto every class.
        var launchRecipe = contact.Kind == NavEdgeKind.Jump
            ? CreateContactLaunchRecipe(contact)
            : NavEdgeLaunchRecipe.None;
        var edge = new NavEdge(
            toNode,
            contact.Kind,
            MathF.Max(1f, contact.ProbeTicks + MathF.Abs(target.X - nodes[fromNode].X) * 0.05f),
            completion,
            // The OG2 sweep's launch tick is part of the contact proof. Keep it
            // attached to the edge: several stock-map transitions are only
            // executable after a short run-up, and jumping immediately misses
            // the platform even though the abstract edge is reachable.
            contact.JumpTriggerTick,
            contact.ProbeTicks,
            contact.MoveDirection,
            ProbeVariantAttempts: 1,
            ProbeVariantSuccesses: 1,
            contact.SupportedClassMask,
            contact.SupportedTeamMask,
            RequiresGroundedContinuation: contact.Kind == NavEdgeKind.Jump,
            RequiresCarryingIntel: contact.RequiresCarryingIntel,
            launchRecipe,
            CarryingIntelRequirement: contact.RequiresCarryingIntel,
            IsOg2Contact: true);
        AddOrMergeEdge(
            fromNode,
            edge,
            adjacency,
            edgeIndex,
            Quantize(contact.EntryX),
            Quantize(contact.ExitX));
    }

    private static NavEdgeLaunchRecipe CreateContactLaunchRecipe(ContactRecord contact)
    {
        var playerClass = Enum.GetValues<PlayerClass>()
            .FirstOrDefault(candidate => ResolveMovementClassMask(candidate) == contact.SupportedClassMask);
        var definition = CharacterClassCatalog.GetDefinition(playerClass);

        // The launch state is sampled immediately before the OG2 jump input is
        // consumed. Keep the acceptance band to the state that one fixed
        // update can physically move into. A broad band hides residual
        // momentum errors and turns a valid isolated contact into an invalid
        // runtime composition.
        var positionTolerance = MathF.Max(
            4f,
            definition.MaxRunSpeed / LegacyMovementModel.SourceTicksPerSecond);
        var speedTolerance = MathF.Max(
            8f,
            definition.RunPower * LegacyMovementModel.SourceTicksPerSecond * 0.25f);

        return new NavEdgeLaunchRecipe(
            StartGrounded: true,
            LaunchTick: Math.Max(0, contact.JumpTriggerTick),
            LaunchMinX: contact.LaunchX - positionTolerance,
            LaunchMaxX: contact.LaunchX + positionTolerance,
            // Stair run-ups can climb several lips before the measured jump
            // input is consumed. The source surface Y is not the live launch
            // state; preserve the OG2 probe's measured launch height so the
            // runtime can recognize the same contact after composing walk
            // links into the route.
            LaunchMinY: contact.LaunchY - 8f,
            LaunchMaxY: contact.LaunchY + 8f,
            LaunchMinHorizontalSpeed: contact.LaunchHorizontalSpeed - speedTolerance,
            LaunchMaxHorizontalSpeed: contact.LaunchHorizontalSpeed + speedTolerance,
            ExpectedMoveDirectionX: contact.MoveDirection,
            JumpStartsGrounded: contact.JumpStartsGrounded);
    }

    private static void AddSimpleEdge(
        int fromNode,
        int toNode,
        NavEdgeKind kind,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        Dictionary<DirectedEdgeKey, int> edgeIndex,
        int supportedClassMask,
        float minimumCollisionBottom,
        float maximumCollisionBottom,
        int surfaceId,
        bool? carryingIntelRequirement = null,
        int supportedTeamMask = BotBrainTeamMask.All)
    {
        var target = nodes[toNode];
        var completion = CreateSurfaceCompletion(
            target.X,
            target.Y + maximumCollisionBottom,
            surfaceId,
            minimumCollisionBottom,
            maximumCollisionBottom);
        var edge = new NavEdge(
            toNode,
            kind,
            MathF.Max(1f, Distance(nodes[fromNode], target)),
            completion,
            JumpTriggerTick: 0,
            ProbeTicks: 0,
            ProbeMoveDirectionX: MathF.Sign(target.X - nodes[fromNode].X),
            ProbeVariantAttempts: 0,
            ProbeVariantSuccesses: 0,
            supportedClassMask,
            supportedTeamMask,
            RequiresGroundedContinuation: false,
            RequiresCarryingIntel: carryingIntelRequirement == true,
            NavEdgeLaunchRecipe.None,
            CarryingIntelRequirement: carryingIntelRequirement);
        AddOrMergeEdge(fromNode, edge, adjacency, edgeIndex, mergeClassVariants: true);
    }

    private static void AddAnchors(
        SimpleLevel level,
        IReadOnlyList<VerifiedNavSurface> surfaces,
        List<NavNode> nodes,
        Dictionary<SampleKey, int> nodeBySample,
        IReadOnlyList<List<NavEdge>> adjacency,
        Dictionary<DirectedEdgeKey, int> edgeIndex,
        List<NavSpawnAnchor> spawnAnchors,
        float minimumCollisionBottom,
        float maximumCollisionBottom)
    {
        foreach (var spawn in level.RedSpawns)
        {
            AddAnchor(level, surfaces, nodes, nodeBySample, adjacency, edgeIndex, spawn.X, spawn.Y, NavNodeKind.Spawn, PlayerTeam.Red, false, spawnAnchors, minimumCollisionBottom, maximumCollisionBottom);
        }

        foreach (var spawn in level.BlueSpawns)
        {
            AddAnchor(level, surfaces, nodes, nodeBySample, adjacency, edgeIndex, spawn.X, spawn.Y, NavNodeKind.Spawn, PlayerTeam.Blue, false, spawnAnchors, minimumCollisionBottom, maximumCollisionBottom);
        }

        foreach (var intel in level.IntelBases)
        {
            AddAnchor(level, surfaces, nodes, nodeBySample, adjacency, edgeIndex, intel.X, intel.Y, NavNodeKind.Objective, intel.Team, true, spawnAnchors, minimumCollisionBottom, maximumCollisionBottom);
        }

        var hasCaptureZones = level.GetRoomObjects(RoomObjectType.CaptureZone).Count > 0;
        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type is not (RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator))
            {
                continue;
            }

            // ControlPoint/ArenaControlPoint markers are logical or visual
            // anchors above the floor. Runtime alpha objective selection
            // already resolves them to the associated CaptureZone, which is
            // the coordinate a bot can actually occupy. Do not add a second
            // dangling objective node when that gameplay zone exists.
            if (hasCaptureZones
                && roomObject.Type is RoomObjectType.ArenaControlPoint or RoomObjectType.ControlPoint)
            {
                continue;
            }

            AddAnchor(
                level,
                surfaces,
                nodes,
                nodeBySample,
                adjacency,
                edgeIndex,
                roomObject.CenterX,
                roomObject.CenterY,
                NavNodeKind.Objective,
                roomObject.Team,
                true,
                spawnAnchors,
                minimumCollisionBottom,
                maximumCollisionBottom,
                roomObject.Type == RoomObjectType.CaptureZone ? roomObject.Left : null,
                roomObject.Type == RoomObjectType.CaptureZone ? roomObject.Right : null,
                roomObject.Type == RoomObjectType.CaptureZone ? roomObject.Top : null,
                roomObject.Type == RoomObjectType.CaptureZone ? roomObject.Bottom : null,
                allowLogicalObjectiveSupport: roomObject.Type == RoomObjectType.CaptureZone
                    || (!hasCaptureZones
                        && (roomObject.Type is RoomObjectType.ArenaControlPoint or RoomObjectType.ControlPoint)));
        }
    }

    private static void AddAnchor(
        SimpleLevel level,
        IReadOnlyList<VerifiedNavSurface> surfaces,
        List<NavNode> nodes,
        Dictionary<SampleKey, int> nodeBySample,
        IReadOnlyList<List<NavEdge>> adjacency,
        Dictionary<DirectedEdgeKey, int> edgeIndex,
        float x,
        float y,
        NavNodeKind kind,
        PlayerTeam? team,
        bool objective,
        List<NavSpawnAnchor> spawnAnchors,
        float minimumCollisionBottom,
        float maximumCollisionBottom,
        float? horizontalMinX = null,
        float? horizontalMaxX = null,
        float? verticalMinY = null,
        float? verticalMaxY = null,
        bool allowLogicalObjectiveSupport = false)
    {
        var anchorIndex = nodes.Count;
        nodes.Add(new NavNode(x, y, kind));
        var attached = false;

        var candidateMinX = horizontalMinX ?? (x - 2f);
        var candidateMaxX = horizontalMaxX ?? (x + 2f);
        var orderedCandidates = surfaces
            // Ordinary anchors require the supporting surface to contain the
            // anchor's horizontal position. A capture zone is different: its
            // gameplay volume can extend over several adjacent surface
            // intervals, and the center may sit just beyond a wall/ledge
            // boundary. In that case attach to the nearest overlap within the
            // actual zone bounds so the objective remains a real graph goal.
            .Where(surface => allowLogicalObjectiveSupport
                ? surface.Left <= x + AnchorSearchDistance
                    && surface.Right >= x - AnchorSearchDistance
                    && MathF.Abs((surface.Top - maximumCollisionBottom) - y)
                        <= LogicalObjectiveSearchVerticalDistance
                : surface.Left <= candidateMaxX && surface.Right >= candidateMinX)
            .Select(surface =>
            {
                var attachMinX = horizontalMinX.HasValue
                    ? MathF.Max(surface.Left, horizontalMinX.Value)
                    : surface.Left;
                var attachMaxX = horizontalMaxX.HasValue
                    ? MathF.Min(surface.Right, horizontalMaxX.Value)
                    : surface.Right;
                var attachX = attachMinX <= attachMaxX
                    ? Math.Clamp(x, attachMinX, attachMaxX)
                    : Math.Clamp(x, surface.Left, surface.Right);
                return (
                    Surface: surface,
                    AttachX: attachX,
                    Distance: MathF.Abs(surface.Top - (y + maximumCollisionBottom)) + MathF.Abs(attachX - x));
            })
            .OrderBy(static candidate => candidate.Distance)
            .ToArray();
        var candidates = allowLogicalObjectiveSupport
            ? orderedCandidates
                .Take(4)
                .Concat(orderedCandidates
                    .Where(candidate => candidate.Surface.Top - maximumCollisionBottom
                        > y + LogicalObjectiveAttachVerticalTolerance)
                    .Take(2))
                .DistinctBy(static candidate => candidate.Surface.Id)
                .ToArray()
            : orderedCandidates.Take(4).ToArray();
        var logicalObjectiveClassMask = 0;
        var logicalObjectiveTeamMask = 0;
        foreach (var candidate in candidates)
        {
            var surfaceX = candidate.AttachX;
            var surfaceY = candidate.Surface.Top - maximumCollisionBottom;
            var attachVerticalTolerance = allowLogicalObjectiveSupport
                ? LogicalObjectiveAttachVerticalTolerance
                : AnchorDirectAttachVerticalTolerance;
            if (!allowLogicalObjectiveSupport
                && MathF.Abs(surfaceY - y) > attachVerticalTolerance)
            {
                // Do not materialize a surface node for an anchor that is not
                // actually attached to it. A rejected candidate must not leave
                // a dangling nearest-node target for runtime path attachment.
                continue;
            }

            var surfaceIndex = GetOrAddSurfaceNode(nodes, nodeBySample, candidate.Surface, surfaceX, maximumCollisionBottom);
            EnsureAdjacencyCapacity(adjacency, nodes.Count);
            var surfaceNode = nodes[surfaceIndex];
            ConnectAnchorSurfaceNode(
                level,
                candidate.Surface,
                surfaceIndex,
                nodes,
                adjacency,
                edgeIndex,
                minimumCollisionBottom,
                maximumCollisionBottom);

            if (objective && allowLogicalObjectiveSupport)
            {
                // A logical capture zone can be centered above the actual
                // standing surface. When the zone overlaps that surface and
                // the OG2 occupancy probe confirms the short horizontal span,
                // attach it as a grounded walk goal before trying jump-only
                // objective approaches. Without this edge a perfectly
                // reachable KOTH point can remain a dangling objective node.
                var objectiveMinX = horizontalMinX ?? (x - 21f);
                var objectiveMaxX = horizontalMaxX ?? (x + 21f);
                var objectiveMinY = verticalMinY ?? (y - 21f);
                var objectiveMaxY = verticalMaxY ?? (y + 21f);
                var directWalkSupported = candidate.Surface.Left <= objectiveMaxX
                    && candidate.Surface.Right >= objectiveMinX
                    && MathF.Abs(surfaceY - y) <= LogicalObjectiveAttachVerticalTolerance;
                var nearbyWalkProbeSupported = !directWalkSupported
                    && MathF.Abs(surfaceX - x) <= 96f;
                if (directWalkSupported || nearbyWalkProbeSupported)
                {
                    var directCompletion = new NavEdgeCompletion(
                        objectiveMinX,
                        objectiveMaxX,
                        objectiveMinY,
                        objectiveMaxY,
                        [],
                        AllowsAirborneObjective: true);
                    foreach (var movementClass in EnumerateRepresentativeClasses())
                    {
                        var supportedTeamMask = directWalkSupported
                            ? ResolveStaticWalkTeamMask(
                                level,
                                surfaceNode,
                                nodes[anchorIndex],
                                movementClass,
                                carryingIntel: false,
                                maximumCollisionBottom)
                            : ProbeLogicalObjectiveWalkTeamMask(
                                level,
                                candidate.Surface,
                                surfaceX,
                                x,
                                y,
                                objectiveMinX,
                                objectiveMaxX,
                                objectiveMinY,
                                objectiveMaxY,
                                movementClass);
                        if (supportedTeamMask == 0)
                        {
                            continue;
                        }

                        var supportedClassMask = ResolveMovementClassMask(movementClass);
                        AddOrMergeEdge(
                            surfaceIndex,
                            new NavEdge(
                                anchorIndex,
                                NavEdgeKind.Walk,
                                MathF.Max(1f, MathF.Abs(x - surfaceNode.X) + 12f),
                                directCompletion,
                                JumpTriggerTick: 0,
                                ProbeTicks: 0,
                                ProbeMoveDirectionX: MathF.Sign(x - surfaceNode.X),
                                ProbeVariantAttempts: 1,
                                ProbeVariantSuccesses: 1,
                                supportedClassMask,
                                supportedTeamMask,
                                RequiresGroundedContinuation: false,
                                RequiresCarryingIntel: false,
                                NavEdgeLaunchRecipe.None,
                                IsOg2Contact: false),
                            adjacency,
                            edgeIndex);
                        AddSimpleEdge(
                            anchorIndex,
                            surfaceIndex,
                            NavEdgeKind.Walk,
                            nodes,
                            adjacency,
                            edgeIndex,
                            supportedClassMask,
                            minimumCollisionBottom,
                            maximumCollisionBottom,
                            candidate.Surface.Id,
                            carryingIntelRequirement: false,
                            supportedTeamMask);
                        attached = true;
                    }
                }

                var logicalApproaches = FindLogicalObjectiveApproaches(
                    level,
                    candidate.Surface,
                    surfaceX,
                    x,
                    y,
                    objectiveMinX,
                    objectiveMaxX,
                    objectiveMinY,
                    objectiveMaxY);
                foreach (var approach in logicalApproaches)
                {
                    AddOrMergeEdge(
                        surfaceIndex,
                        new NavEdge(
                            anchorIndex,
                            NavEdgeKind.Jump,
                            approach.ProbeTicks + MathF.Abs(x - surfaceNode.X) * 0.05f + 12f,
                            approach.Completion,
                            approach.JumpTriggerTick,
                            approach.ProbeTicks,
                            approach.MoveDirectionX,
                            approach.VariantAttempts,
                            approach.VariantSuccesses,
                            approach.SupportedClassMask,
                            approach.SupportedTeamMask,
                            RequiresGroundedContinuation: false,
                            RequiresCarryingIntel: false,
                            approach.LaunchRecipe,
                            IsOg2Contact: true),
                        adjacency,
                        edgeIndex,
                        Quantize(surfaceX),
                        Quantize(x));
                }

                if (logicalApproaches.Count > 0)
                {
                    attached = true;
                    foreach (var approach in logicalApproaches)
                    {
                        logicalObjectiveClassMask |= approach.SupportedClassMask;
                        logicalObjectiveTeamMask |= approach.SupportedTeamMask;
                    }
                    if (BotBrainClassMask.CoversAll(logicalObjectiveClassMask)
                        && BotBrainTeamMask.CoversAll(logicalObjectiveTeamMask))
                    {
                        break;
                    }
                }

                continue;
            }

            attached = true;
            if (objective)
            {
                var completionVerticalSlack = allowLogicalObjectiveSupport
                    ? LogicalObjectiveCompletionVerticalSlack
                    : AnchorCompletionVerticalSlack;
                var approachCompletion = new NavEdgeCompletion(
                    x - AnchorCompletionHorizontalSlack,
                    x + AnchorCompletionHorizontalSlack,
                    y - completionVerticalSlack,
                    y + completionVerticalSlack,
                    []);
                AddOrMergeEdge(
                    surfaceIndex,
                    new NavEdge(
                        anchorIndex,
                        NavEdgeKind.Walk,
                        candidate.Distance + 12f,
                        approachCompletion,
                        0,
                        0,
                        MathF.Sign(x - surfaceNode.X),
                        0,
                        0,
                        BotBrainClassMask.All,
                        BotBrainTeamMask.All,
                        false,
                        false,
                        NavEdgeLaunchRecipe.None),
                    adjacency,
                    edgeIndex);

                // Objective nodes are valid route goals, but they can also be
                // the nearest traversal attachment immediately after pickup
                // (or after reaching a control point). Keep the approach edge
                // directional for its precise objective completion semantics,
                // while giving the anchor a normal walk exit back onto the
                // supporting surface. Without this reverse edge, a carrier
                // that attaches to the objective node has no way to begin its
                // return route.
                AddSimpleEdge(
                    anchorIndex,
                    surfaceIndex,
                    NavEdgeKind.Walk,
                    nodes,
                    adjacency,
                    edgeIndex,
                    BotBrainClassMask.All,
                    minimumCollisionBottom,
                    maximumCollisionBottom,
                    candidate.Surface.Id);
            }
            else
            {
                AddSimpleEdge(surfaceIndex, anchorIndex, NavEdgeKind.Walk, nodes, adjacency, edgeIndex, BotBrainClassMask.All, minimumCollisionBottom, maximumCollisionBottom, candidate.Surface.Id);
                AddSimpleEdge(anchorIndex, surfaceIndex, NavEdgeKind.Walk, nodes, adjacency, edgeIndex, BotBrainClassMask.All, minimumCollisionBottom, maximumCollisionBottom, candidate.Surface.Id);
            }
        }

        if (!attached)
        {
            // Do not leave a logical marker in the nearest-node search when
            // no walkable surface actually supports it. Runtime objective
            // selection will attach to the nearest reachable surface instead.
            nodes.RemoveAt(anchorIndex);
            return;
        }

        if (team.HasValue && kind == NavNodeKind.Spawn)
        {
            spawnAnchors.Add(new NavSpawnAnchor(x, y, team.Value));
        }
    }

    private static List<LogicalObjectiveApproach> FindLogicalObjectiveApproaches(
        SimpleLevel level,
        VerifiedNavSurface surface,
        float startX,
        float objectiveX,
        float objectiveY,
        float objectiveMinX,
        float objectiveMaxX,
        float objectiveMinY,
        float objectiveMaxY)
    {
        var approaches = new List<LogicalObjectiveApproach>();
        foreach (var movementClassGroup in EnumerateRepresentativeClasses()
                     .GroupBy(MovementSignature.For))
        {
            var movementClass = movementClassGroup.First();
            var supportedClassMask = ResolveMovementClassMask(movementClass);
            foreach (var team in Enum.GetValues<PlayerTeam>())
            {
                if (!TryProbeLogicalObjectiveJump(
                        level,
                        surface,
                        startX,
                        objectiveX,
                        objectiveY,
                        objectiveMinX,
                        objectiveMaxX,
                        objectiveMinY,
                        objectiveMaxY,
                        movementClass,
                        team,
                        out var probe))
                {
                    continue;
                }

                approaches.Add(new LogicalObjectiveApproach(
                    supportedClassMask,
                    BotBrainTeamMask.For(team),
                    probe.JumpTriggerTick,
                    probe.ProbeTicks,
                    probe.MoveDirectionX,
                    probe.VariantAttempts,
                    probe.VariantSuccesses,
                    probe.Completion,
                    probe.LaunchRecipe));
            }
        }

        return approaches;
    }

    private static int ProbeLogicalObjectiveWalkTeamMask(
        SimpleLevel level,
        VerifiedNavSurface surface,
        float startX,
        float objectiveX,
        float objectiveY,
        float objectiveMinX,
        float objectiveMaxX,
        float objectiveMinY,
        float objectiveMaxY,
        PlayerClass playerClass)
    {
        var teamMask = 0;
        foreach (var team in Enum.GetValues<PlayerTeam>())
        {
            if (TryProbeLogicalObjectiveWalk(
                    level,
                    surface,
                    startX,
                    objectiveX,
                    objectiveY,
                    objectiveMinX,
                    objectiveMaxX,
                    objectiveMinY,
                    objectiveMaxY,
                    playerClass,
                    team))
            {
                teamMask |= BotBrainTeamMask.For(team);
            }
        }

        return teamMask;
    }

    private static bool TryProbeLogicalObjectiveWalk(
        SimpleLevel level,
        VerifiedNavSurface surface,
        float startX,
        float objectiveX,
        float objectiveY,
        float objectiveMinX,
        float objectiveMaxX,
        float objectiveMinY,
        float objectiveMaxY,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        var startY = surface.Top - definition.CollisionBottom;
        var movementDirection = (float)MathF.Sign(objectiveX - startX);
        if (movementDirection == 0f)
        {
            movementDirection = 1f;
        }

        var player = new PlayerEntity(-910_004, definition, "Og2LogicalObjectiveWalkProbe");
        player.Spawn(team, startX, startY);
        player.TeleportTo(startX, startY);
        player.ResolveBlockingOverlap(level, team);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, movementDirection);

        var input = new PlayerInputSnapshot(
            Left: movementDirection < 0f,
            Right: movementDirection > 0f,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: objectiveX,
            AimWorldY: objectiveY,
            DebugKill: false,
            DropIntel: false,
            UseAbility: false);
        var objectiveCenterX = (objectiveMinX + objectiveMaxX) * 0.5f;
        var objectiveCenterY = (objectiveMinY + objectiveMaxY) * 0.5f;
        var objectiveWidth = objectiveMaxX - objectiveMinX;
        var objectiveHeight = objectiveMaxY - objectiveMinY;
        for (var tick = 0; tick < Math.Max(SweepTicks, 96); tick += 1)
        {
            player.Advance(
                input,
                jumpPressed: false,
                level,
                team,
                1d / SimulationConfig.DefaultTicksPerSecond);
            if (player.IntersectsMarker(
                    objectiveCenterX,
                    objectiveCenterY,
                    objectiveWidth,
                    objectiveHeight))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryProbeLogicalObjectiveJump(
        SimpleLevel level,
        VerifiedNavSurface surface,
        float startX,
        float objectiveX,
        float objectiveY,
        float objectiveMinX,
        float objectiveMaxX,
        float objectiveMinY,
        float objectiveMaxY,
        PlayerClass playerClass,
        PlayerTeam team,
        out LogicalObjectiveProbe result)
    {
        result = default;
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        var startY = surface.Top - definition.CollisionBottom;
        var direction = MathF.Sign(objectiveX - startX);
        var movementDirection = direction == 0f ? 1f : direction;
        foreach (var jumpTick in LogicalObjectiveJumpSweepTicks)
        {
            var player = new PlayerEntity(-910_003, definition, "Og2LogicalObjectiveProbe");
            player.Spawn(team, startX, startY);
            player.TeleportTo(startX, startY);
            player.ResolveBlockingOverlap(level, team);
            player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, movementDirection);

            var previousInput = default(PlayerInputSnapshot);
            var launchCaptured = false;
            var launchX = 0f;
            var launchY = 0f;
            var launchHorizontalSpeed = 0f;
            var jumpStartsGrounded = false;
            for (var tick = 0; tick < Math.Max(SweepTicks, 96); tick += 1)
            {
                var input = new PlayerInputSnapshot(
                    Left: movementDirection < 0f,
                    Right: movementDirection > 0f,
                    Up: tick == jumpTick,
                    Down: false,
                    BuildSentry: false,
                    DestroySentry: false,
                    Taunt: false,
                    FirePrimary: false,
                    FireSecondary: false,
                    AimWorldX: objectiveX,
                    AimWorldY: objectiveY,
                    DebugKill: false,
                    DropIntel: false,
                    UseAbility: false);
                var jumpPressed = input.Up && !previousInput.Up;
                var jumpConsumed = jumpPressed && (player.IsGrounded || player.RemainingAirJumps > 0);
                if (jumpConsumed)
                {
                    launchCaptured = true;
                    jumpStartsGrounded = player.IsGrounded;
                    launchX = player.X;
                    launchY = player.Y;
                    launchHorizontalSpeed = player.HorizontalSpeed;
                }

                player.Advance(
                    input,
                    jumpPressed,
                    level,
                    team,
                    1d / SimulationConfig.DefaultTicksPerSecond);
                previousInput = input;

                if (!launchCaptured
                    || !player.IntersectsMarker(
                        (objectiveMinX + objectiveMaxX) * 0.5f,
                        (objectiveMinY + objectiveMaxY) * 0.5f,
                        objectiveMaxX - objectiveMinX,
                        objectiveMaxY - objectiveMinY))
                {
                    continue;
                }

                var launchPositionTolerance = MathF.Max(
                    4f,
                    definition.MaxRunSpeed / LegacyMovementModel.SourceTicksPerSecond);
                var launchSpeedTolerance = MathF.Max(
                    8f,
                    definition.RunPower * LegacyMovementModel.SourceTicksPerSecond * 0.25f);
                result = new LogicalObjectiveProbe(
                    new NavEdgeCompletion(
                        player.X - 42f,
                        player.X + 42f,
                        player.Y - LogicalObjectiveCompletionVerticalSlack,
                        player.Y + LogicalObjectiveCompletionVerticalSlack,
                        [],
                        AllowsAirborneObjective: true),
                    tick + 1,
                    jumpTick,
                    movementDirection,
                    LogicalObjectiveJumpSweepTicks.Length,
                    1,
                    new NavEdgeLaunchRecipe(
                        StartGrounded: jumpStartsGrounded,
                        LaunchTick: jumpTick,
                        LaunchMinX: launchX - launchPositionTolerance,
                        LaunchMaxX: launchX + launchPositionTolerance,
                        LaunchMinY: launchY - 8f,
                        LaunchMaxY: launchY + 8f,
                        LaunchMinHorizontalSpeed: launchHorizontalSpeed - launchSpeedTolerance,
                        LaunchMaxHorizontalSpeed: launchHorizontalSpeed + launchSpeedTolerance,
                        ExpectedMoveDirectionX: movementDirection,
                        JumpStartsGrounded: jumpStartsGrounded));
                return true;
            }

            if (launchCaptured && player.IsGrounded)
            {
                break;
            }
        }

        return false;
    }

    private static void ConnectAnchorSurfaceNode(
        SimpleLevel level,
        VerifiedNavSurface surface,
        int surfaceIndex,
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        Dictionary<DirectedEdgeKey, int> edgeIndex,
        float minimumCollisionBottom,
        float maximumCollisionBottom)
    {
        var attachment = nodes[surfaceIndex];
        var nearestExisting = -1;
        var nearestDistance = float.MaxValue;
        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
        {
            if (nodeIndex == surfaceIndex
                || nodes[nodeIndex].SurfaceId != surface.Id)
            {
                continue;
            }

            var distance = MathF.Abs(nodes[nodeIndex].X - attachment.X);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestExisting = nodeIndex;
            }
        }

        if (nearestExisting < 0)
        {
            return;
        }

        var existing = nodes[nearestExisting];
        foreach (var carryingIntel in new[] { false, true })
        {
            foreach (var playerClass in EnumerateRepresentativeClasses())
            {
                var forwardTeamMask = ResolveStaticWalkTeamMask(
                    level,
                    existing,
                    attachment,
                    playerClass,
                    carryingIntel,
                    maximumCollisionBottom);
                if (forwardTeamMask != 0)
                {
                    AddSimpleEdge(
                        nearestExisting,
                        surfaceIndex,
                        NavEdgeKind.Walk,
                        nodes,
                        adjacency,
                        edgeIndex,
                        BotBrainClassMask.For(playerClass),
                        minimumCollisionBottom,
                        maximumCollisionBottom,
                        surface.Id,
                        carryingIntel,
                        forwardTeamMask);
                }

                var reverseTeamMask = ResolveStaticWalkTeamMask(
                    level,
                    attachment,
                    existing,
                    playerClass,
                    carryingIntel,
                    maximumCollisionBottom);
                if (reverseTeamMask != 0)
                {
                    AddSimpleEdge(
                        surfaceIndex,
                        nearestExisting,
                        NavEdgeKind.Walk,
                        nodes,
                        adjacency,
                        edgeIndex,
                        BotBrainClassMask.For(playerClass),
                        minimumCollisionBottom,
                        maximumCollisionBottom,
                        surface.Id,
                        carryingIntel,
                        reverseTeamMask);
                }
            }
        }
    }

    private static int GetOrAddSurfaceNode(
        List<NavNode> nodes,
        Dictionary<SampleKey, int> nodeBySample,
        VerifiedNavSurface surface,
        float x,
        float maximumCollisionBottom)
    {
        var clampedX = Math.Clamp(x, surface.Left, surface.Right);
        var key = new SampleKey(surface.Id, Quantize(clampedX));
        if (nodeBySample.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var nodeIndex = nodes.Count;
        nodes.Add(new NavNode(
            clampedX,
            surface.Top - maximumCollisionBottom,
            clampedX <= surface.Left + 1f || clampedX >= surface.Right - 1f
                ? NavNodeKind.Ledge
                : NavNodeKind.Surface,
            surface.Id));
        nodeBySample.Add(key, nodeIndex);
        return nodeIndex;
    }

    private static List<float> BuildSurfaceSamples(VerifiedNavSurface surface)
    {
        if (surface.Width <= 2f * SurfaceSampleInset)
        {
            return [surface.CenterX];
        }

        var samples = new List<float>
        {
            surface.Left,
            (surface.Left + surface.Right) * 0.5f,
            surface.Right,
        };
        for (var x = surface.Left + 48f; x < surface.Right - 48f; x += 96f)
        {
            samples.Add(x);
        }

        return samples
            .Select(x => Math.Clamp(x, surface.Left + SurfaceSampleInset, surface.Right - SurfaceSampleInset))
            .DistinctBy(Quantize)
            .OrderBy(static x => x)
            .ToList();
    }

    private static IEnumerable<float> BuildContactSamples(VerifiedNavSurface surface)
    {
        if (surface.Width <= 2f * SurfaceSampleInset)
        {
            yield return surface.CenterX;
            yield break;
        }

        var left = MathF.Min(surface.Right, surface.Left + SurfaceSampleInset);
        var right = MathF.Max(surface.Left, surface.Right - SurfaceSampleInset);
        yield return left;
        if (MathF.Abs(right - left) > ContactBucket)
        {
            // Endpoint-only probes can miss the intended route on wide
            // platforms: a stair/drop may be reachable from the interior
            // while both inset endpoints commit to a different side path.
            // Keep the sample set bounded; each trajectory is still certified
            // independently by the class/team OG2 probe.
            yield return (left + right) * 0.5f;
            yield return right;
        }

    }

    /// <summary>
    /// Broad-phase index for contact probes. The old implementation tested
    /// every other surface for every probe launch, which made a local sweep
    /// quadratic in the number of extracted surfaces. The index only narrows
    /// that conservative query; OG2 movement remains the authority for the
    /// emitted contact.
    /// </summary>
    private sealed class SurfaceSpatialIndex
    {
        private const float CellSize = 128f;
        private readonly IReadOnlyList<VerifiedNavSurface> _surfaces;
        private readonly Dictionary<int, List<int>> _surfaceIdsByCell = [];

        public SurfaceSpatialIndex(IReadOnlyList<VerifiedNavSurface> surfaces)
        {
            _surfaces = surfaces;
            for (var index = 0; index < surfaces.Count; index += 1)
            {
                var surface = surfaces[index];
                var firstCell = Cell(surface.Left);
                var lastCell = Cell(surface.Right);
                for (var cell = firstCell; cell <= lastCell; cell += 1)
                {
                    if (!_surfaceIdsByCell.TryGetValue(cell, out var surfaceIds))
                    {
                        surfaceIds = [];
                        _surfaceIdsByCell[cell] = surfaceIds;
                    }

                    surfaceIds.Add(index);
                }
            }
        }

        public bool HasCandidate(
            int sourceSurfaceId,
            float minX,
            float maxX,
            float minimumTop,
            float maximumTop)
        {
            var firstCell = Cell(minX);
            var lastCell = Cell(maxX);
            for (var cell = firstCell; cell <= lastCell; cell += 1)
            {
                if (!_surfaceIdsByCell.TryGetValue(cell, out var surfaceIds))
                {
                    continue;
                }

                foreach (var surfaceIndex in surfaceIds)
                {
                    var candidate = _surfaces[surfaceIndex];
                    if (candidate.Id != sourceSurfaceId
                        && candidate.Right >= minX
                        && candidate.Left <= maxX
                        && candidate.Top >= minimumTop
                        && candidate.Top <= maximumTop)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int Cell(float value) => (int)MathF.Floor(value / CellSize);
    }

    private static NavEdgeCompletion CreateSurfaceCompletion(
        float x,
        float surfaceTop,
        int surfaceId,
        float minimumCollisionBottom,
        float maximumCollisionBottom) =>
        new(
            x - 28f,
            x + 28f,
            surfaceTop - maximumCollisionBottom - 20f,
            surfaceTop - minimumCollisionBottom + 20f,
            [surfaceId]);

    private static void AddOrMergeEdge(
        int fromNode,
        NavEdge edge,
        IReadOnlyList<List<NavEdge>> adjacency,
        Dictionary<DirectedEdgeKey, int> edgeIndex,
        int entryBucket = 0,
        int exitBucket = 0,
        bool mergeClassVariants = false)
    {
        var key = new DirectedEdgeKey(
            fromNode,
            edge.ToNode,
            edge.Kind,
            mergeClassVariants ? 0 : edge.SupportedClassMask,
            edge.SupportedTeamMask,
            edge.RequiresCarryingIntel,
            entryBucket,
            exitBucket,
            edge.LaunchRecipe.JumpStartsGrounded,
            mergeClassVariants);
        if (edgeIndex.TryGetValue(key, out var index))
        {
            var existing = adjacency[fromNode][index];
            adjacency[fromNode][index] = existing with
            {
                SupportedClassMask = existing.SupportedClassMask | edge.SupportedClassMask,
                SupportedTeamMask = existing.SupportedTeamMask | edge.SupportedTeamMask,
                Cost = MathF.Min(existing.Cost, edge.Cost),
                ProbeTicks = existing.ProbeTicks == 0
                    ? edge.ProbeTicks
                    : edge.ProbeTicks == 0
                        ? existing.ProbeTicks
                        : Math.Min(existing.ProbeTicks, edge.ProbeTicks),
            };
            return;
        }

        edgeIndex.Add(key, adjacency[fromNode].Count);
        adjacency[fromNode].Add(edge);
    }

    private static void EnsureAdjacencyCapacity(IReadOnlyList<List<NavEdge>> adjacency, int nodeCount)
    {
        // The graph starts with surface nodes. Anchor/contact nodes are appended
        // later, so their adjacency lists are created by the caller's capacity
        // helper below when the list is grown.
        while (adjacency.Count < nodeCount)
        {
            throw new InvalidOperationException("Navigation adjacency cannot grow after graph construction.");
        }
    }

    private static List<NavEdge>[] CreateAdjacency(int nodeCount)
    {
        var adjacency = new List<NavEdge>[nodeCount];
        for (var i = 0; i < nodeCount; i += 1)
        {
            adjacency[i] = [];
        }

        return adjacency;
    }

    private static (List<NavNode> Nodes, List<NavEdge>[] Adjacency, int RemovedCount) PruneIsolatedSurfaceNodes(
        IReadOnlyList<NavNode> nodes,
        IReadOnlyList<List<NavEdge>> adjacency,
        bool retainNonSurfaceNodes = false)
    {
        var incoming = new int[nodes.Count];
        for (var fromNode = 0; fromNode < nodes.Count; fromNode += 1)
        {
            foreach (var edge in adjacency[fromNode])
            {
                if (edge.ToNode >= 0 && edge.ToNode < nodes.Count)
                {
                    incoming[edge.ToNode] += 1;
                }
            }
        }

        var remap = Enumerable.Repeat(-1, nodes.Count).ToArray();
        var retainedNodes = new List<NavNode>(nodes.Count);
        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
        {
            if ((!retainNonSurfaceNodes || nodes[nodeIndex].SurfaceId.HasValue)
                && adjacency[nodeIndex].Count == 0
                && incoming[nodeIndex] == 0)
            {
                continue;
            }

            remap[nodeIndex] = retainedNodes.Count;
            retainedNodes.Add(nodes[nodeIndex]);
        }

        if (retainedNodes.Count == nodes.Count)
        {
            return (nodes.ToList(), adjacency.ToArray(), 0);
        }

        var remappedAdjacency = new List<NavEdge>[adjacency.Count];
        for (var index = 0; index < remappedAdjacency.Length; index += 1)
        {
            remappedAdjacency[index] = [];
        }

        for (var oldFromNode = 0; oldFromNode < nodes.Count; oldFromNode += 1)
        {
            var newFromNode = remap[oldFromNode];
            if (newFromNode < 0)
            {
                continue;
            }

            foreach (var edge in adjacency[oldFromNode])
            {
                if (edge.ToNode < 0 || edge.ToNode >= nodes.Count)
                {
                    continue;
                }

                var newToNode = remap[edge.ToNode];
                if (newToNode >= 0)
                {
                    remappedAdjacency[newFromNode].Add(edge with { ToNode = newToNode });
                }
            }
        }

        return (
            retainedNodes,
            remappedAdjacency,
            nodes.Count - retainedNodes.Count);
    }

    private static Dictionary<SampleKey, int> BuildNodeBySample(IReadOnlyList<NavNode> nodes)
    {
        var nodeBySample = new Dictionary<SampleKey, int>();
        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex += 1)
        {
            if (!nodes[nodeIndex].SurfaceId.HasValue)
            {
                continue;
            }

            nodeBySample.TryAdd(
                new SampleKey(nodes[nodeIndex].SurfaceId!.Value, Quantize(nodes[nodeIndex].X)),
                nodeIndex);
        }

        return nodeBySample;
    }

    private static Dictionary<DirectedEdgeKey, int> BuildEdgeIndex(
        IReadOnlyList<List<NavEdge>> adjacency,
        int nodeCount)
    {
        var edgeIndex = new Dictionary<DirectedEdgeKey, int>();
        for (var fromNode = 0; fromNode < nodeCount; fromNode += 1)
        {
            var edges = adjacency[fromNode];
            for (var edgeIndexInList = 0; edgeIndexInList < edges.Count; edgeIndexInList += 1)
            {
                var edge = edges[edgeIndexInList];
                edgeIndex[new DirectedEdgeKey(
                    fromNode,
                    edge.ToNode,
                    edge.Kind,
                    !edge.IsOg2Contact && edge.ProbeTicks == 0 ? 0 : edge.SupportedClassMask,
                    edge.SupportedTeamMask,
                    edge.RequiresCarryingIntel,
                    0,
                    0,
                    edge.LaunchRecipe.JumpStartsGrounded,
                    !edge.IsOg2Contact && edge.ProbeTicks == 0)] = edgeIndexInList;
            }
        }

        return edgeIndex;
    }

    private static float Distance(NavNode from, NavNode to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float DistanceToInterval(VerifiedNavSurface surface, float x) =>
        x < surface.Left ? surface.Left - x : x > surface.Right ? x - surface.Right : 0f;

    private static int Quantize(float x) => (int)MathF.Round(x / ContactBucket);

    private static bool ShouldTraceContact(int fromSurfaceId, int toSurfaceId, bool carryingIntel)
    {
        var configured = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_TRACE_CONTACTS");
        return !string.IsNullOrWhiteSpace(configured)
            && configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value =>
                {
                    var parts = value.Split(',', StringSplitOptions.TrimEntries);
                    return parts.Length == 2
                        && int.TryParse(parts[0], out var from)
                        && int.TryParse(parts[1], out var to)
                        && from == fromSurfaceId
                        && to == toSurfaceId;
                });
    }

    private static PlayerClass[] EnumerateRepresentativeClasses()
    {
        // A production graph must contain every movement signature. The
        // diagnostic class list controls route validation, not graph
        // generation; allowing it to shrink the graph would make a shipped
        // cache unsafe for a live roster.
        if (Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_ALLOW_PARTIAL_CONTACT_GRAPH")
            is not ("1" or "true" or "TRUE"))
        {
            return AllMovementClasses
                .GroupBy(MovementSignature.For)
                .Select(static group => group.First())
                .ToArray();
        }

        var configured = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES");
        var candidates = !string.IsNullOrWhiteSpace(configured)
            ? configured
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var playerClass) ? (PlayerClass?)playerClass : null)
                .Where(static playerClass => playerClass.HasValue)
                .Select(static playerClass => playerClass!.Value)
                .Distinct()
                .ToArray()
            : AllMovementClasses;

        if (candidates.Length == 0)
        {
            return AllMovementClasses;
        }

        var seenSignatures = new HashSet<MovementSignature>();
        return candidates
            .Where(playerClass => seenSignatures.Add(MovementSignature.For(playerClass)))
            .ToArray();
    }

    private static int ResolveMovementClassMask(PlayerClass playerClass)
    {
        var mask = 0;
        var signature = MovementSignature.For(playerClass);
        foreach (var classId in Enum.GetValues<PlayerClass>())
        {
            if (MovementSignature.For(classId) == signature)
            {
                mask |= BotBrainClassMask.For(classId);
            }
        }

        return mask;
    }

    private static int CountEdges(IReadOnlyList<List<NavEdge>> adjacency) => adjacency.Sum(static edges => edges.Count);

    private readonly record struct SampleKey(int SurfaceId, int XBucket);

    private readonly record struct DirectedEdgeKey(
        int FromNode,
        int ToNode,
        NavEdgeKind Kind,
        int SupportedClassMask,
        int SupportedTeamMask,
        bool RequiresCarryingIntel,
        int EntryBucket,
        int ExitBucket,
        bool JumpStartsGrounded,
        bool MergeClassVariants);

    private readonly record struct ContactKey(
        int FromSurfaceId,
        int ToSurfaceId,
        NavEdgeKind Kind,
        int EntryBucket,
        int ExitBucket,
        int ClassMask,
        bool RequiresCarryingIntel,
        bool JumpStartsGrounded);

    private readonly record struct ContactPairKey(
        int FromSurfaceId,
        int ToSurfaceId,
        int ClassMask,
        bool RequiresCarryingIntel);

    private readonly record struct ContactRecord(
        int FromSurfaceId,
        int ToSurfaceId,
        float EntryX,
        float ExitX,
        float SourceY,
        float ExitBottom,
        NavEdgeKind Kind,
        int JumpTriggerTick,
        int ProbeTicks,
        float MoveDirection,
        int SupportedClassMask,
        bool RequiresCarryingIntel,
        int SupportedTeamMask,
        float LaunchX,
        float LaunchY,
        float LaunchHorizontalSpeed,
        bool JumpStartsGrounded);

    private readonly record struct LogicalObjectiveApproach(
        int SupportedClassMask,
        int SupportedTeamMask,
        int JumpTriggerTick,
        int ProbeTicks,
        float MoveDirectionX,
        int VariantAttempts,
        int VariantSuccesses,
        NavEdgeCompletion Completion,
        NavEdgeLaunchRecipe LaunchRecipe);

    private readonly record struct LogicalObjectiveProbe(
        NavEdgeCompletion Completion,
        int ProbeTicks,
        int JumpTriggerTick,
        float MoveDirectionX,
        int VariantAttempts,
        int VariantSuccesses,
        NavEdgeLaunchRecipe LaunchRecipe);

    private readonly record struct MovementSignature(
        float RunPower,
        float JumpStrength,
        float MaxRunSpeed,
        float GroundAcceleration,
        float GroundDeceleration,
        float Gravity,
        float JumpSpeed,
        int MaxAirJumps,
        float CollisionLeft,
        float CollisionTop,
        float CollisionRight,
        float CollisionBottom)
    {
        public static MovementSignature For(PlayerClass playerClass)
        {
            var definition = CharacterClassCatalog.GetDefinition(playerClass);
            return new MovementSignature(
                definition.RunPower,
                definition.JumpStrength,
                definition.MaxRunSpeed,
                definition.GroundAcceleration,
                definition.GroundDeceleration,
                definition.Gravity,
                definition.JumpSpeed,
                definition.MaxAirJumps,
                definition.CollisionLeft,
                definition.CollisionTop,
                definition.CollisionRight,
                definition.CollisionBottom);
        }
    }
}
