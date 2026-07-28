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
    private static readonly int[] JumpSweepTicks = [0, 6, 10];
    private const float SurfaceMatchTolerance = 10f;
    private const float ContactBucket = 16f;
    private const int MaximumContactsPerSurfacePair = 8;
    private const float AnchorSearchDistance = 320f;
    private const float AnchorCompletionHorizontalSlack = 28f;
    private const float AnchorCompletionVerticalSlack = 28f;

    // The graph's node network is shared, but contact capabilities must be
    // certified against the actual OG2 movement constants. Legacy bot
    // profiles are intentionally not used here: Pyro, Medic, Spy, Quote,
    // etc. do not all execute a Soldier/Scout recipe at the same speed or
    // with the same collision envelope.
    private static readonly PlayerClass[] RepresentativeClasses =
        Enum.GetValues<PlayerClass>()
            .GroupBy(static playerClass => MovementSignature.For(playerClass))
            .Select(static group => group.First())
            .ToArray();

    public static NavGraph Build(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var stopwatch = Stopwatch.StartNew();
        var geometry = VerifiedNavCandidateBuilder.Build(level, new VerifiedNavBuildOptions
        {
            Team = PlayerTeam.Red,
            ClassId = PlayerClass.Scout,
            SampleStep = SurfaceSampleStep,
            MinSurfaceWidth = MinimumSurfaceWidth,
            SurfaceEndpointInset = SurfaceSampleInset,
        });

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

        var contacts = DiscoverContacts(
            level,
            geometry,
            nodes,
            nodeBySample,
            minimumCollisionBottom,
            maximumCollisionBottom);
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
        var representativeClasses = EnumerateRepresentativeClasses().ToArray();
        foreach (var carryingIntel in new[] { false, true })
        {
            // PlayerEntity probes are independent per movement signature. The
            // level's gate set is immutable during graph construction; warm
            // the state-specific cache before fanning out so workers perform
            // read-only collision queries against the same state.
            level.GetBlockingTeamGates(PlayerTeam.Red, carryingIntel);
            var classContactsByIndex = new Dictionary<ContactKey, ContactRecord>[representativeClasses.Length];
            Parallel.For(
                0,
                representativeClasses.Length,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = ResolveContactBuildParallelism(),
                },
                classIndex =>
                {
                    classContactsByIndex[classIndex] = DiscoverContactsForClassState(
                        level,
                        geometry,
                        seedSurfaceIds,
                        representativeClasses[classIndex],
                        carryingIntel);
                });

            // Preserve the serial builder's deterministic class ordering. A
            // parallel completion-order merge changes node indices and can
            // change equal-cost A* tie breaks between runs.
            foreach (var classContacts in classContactsByIndex)
            {
                foreach (var pair in classContacts)
                {
                    contacts[pair.Key] = pair.Value;
                }
            }
        }

        return contacts;
    }

    private static Dictionary<ContactKey, ContactRecord> DiscoverContactsForClassState(
        SimpleLevel level,
        VerifiedNavCandidateGraph geometry,
        IReadOnlySet<int> seedSurfaceIds,
        PlayerClass playerClass,
        bool carryingIntel)
    {
        var contacts = new Dictionary<ContactKey, ContactRecord>();
        var definition = CharacterClassCatalog.GetDefinition(playerClass);
        var classMask = ResolveMovementClassMask(playerClass);
        var visitedSurfaces = new HashSet<int>(seedSurfaceIds);
        var pendingSurfaces = new Queue<int>(seedSurfaceIds);
        while (pendingSurfaces.Count > 0)
        {
            var surface = geometry.Surfaces[pendingSurfaces.Dequeue()];
            foreach (var startX in BuildContactSamples(surface))
            {
                foreach (var direction in new[] { -1, 1 })
                {
                    if (HasPotentialTransition(
                            geometry.Surfaces,
                            level,
                            definition,
                            surface,
                            startX,
                            direction,
                            jumped: false))
                    {
                        DiscoverSweep(
                            level,
                            geometry,
                            definition,
                            classMask,
                            carryingIntel,
                            surface,
                            startX,
                            direction,
                            jumpTick: -1,
                            contacts);
                    }

                    foreach (var jumpTick in JumpSweepTicks)
                    {
                        if (!HasPotentialTransition(
                                geometry.Surfaces,
                                level,
                                definition,
                                surface,
                                startX,
                                direction,
                                jumped: true))
                        {
                            continue;
                        }

                        DiscoverSweep(
                            level,
                            geometry,
                            definition,
                            classMask,
                            carryingIntel,
                            surface,
                            startX,
                            direction,
                            jumpTick,
                            contacts);
                    }
                }
            }

            foreach (var record in contacts.Values)
            {
                if (record.FromSurfaceId != surface.Id
                    || !visitedSurfaces.Add(record.ToSurfaceId))
                {
                    continue;
                }

                pendingSurfaces.Enqueue(record.ToSurfaceId);
            }
        }

        return contacts;
    }

    private static int ResolveContactBuildParallelism()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_BUILD_PARALLELISM"), out var configured))
        {
            return Math.Clamp(configured, 1, Environment.ProcessorCount);
        }

        return Math.Max(1, Environment.ProcessorCount - 1);
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
        SimpleLevel level,
        CharacterClassDefinition definition,
        VerifiedNavSurface source,
        float startX,
        int direction,
        bool jumped)
    {
        // This is deliberately a one-sided, conservative broad phase. It can
        // only reject a sweep when no destination surface lies inside the
        // movement envelope; the OG2 PlayerEntity sweep remains the authority
        // for the emitted contact and its execution recipe.
        var maxTravel = definition.MaxRunSpeed
            * (SweepTicks / (float)SimulationConfig.DefaultTicksPerSecond)
            + 64f;
        var minX = direction < 0 ? startX - maxTravel : startX - 32f;
        var maxX = direction > 0 ? startX + maxTravel : startX + 32f;
        var maximumRise = jumped
            ? MathF.Max(640f, definition.JumpSpeed * 2.5f)
            : 32f;
        var minimumTop = source.Top - maximumRise;
        var maximumTop = source.Top + level.Bounds.Height;

        return surfaces.Any(candidate =>
            candidate.Id != source.Id
            && candidate.Right >= minX
            && candidate.Left <= maxX
            && candidate.Top >= minimumTop
            && candidate.Top <= maximumTop);
    }

    private static void DiscoverSweep(
        SimpleLevel level,
        VerifiedNavCandidateGraph geometry,
        CharacterClassDefinition definition,
        int classMask,
        bool carryingIntel,
        VerifiedNavSurface startSurface,
        float startX,
        int direction,
        int jumpTick,
        Dictionary<ContactKey, ContactRecord> contacts)
    {
        var startY = startSurface.Top - definition.CollisionBottom;
        var player = new PlayerEntity(-910_001, definition, "Og2LocalContactProbe");
        player.Spawn(PlayerTeam.Red, startX, startY);
        player.TeleportTo(startX, startY);
        if (carryingIntel)
        {
            player.PickUpIntel();
        }
        player.ResolveBlockingOverlap(level, PlayerTeam.Red);
        player.RestoreMovementProbeState(isGrounded: true, player.MaxAirJumps, direction);

        var previousInput = default(PlayerInputSnapshot);
        var currentSurfaceId = startSurface.Id;
        var pendingSurfaceId = -1;
        var pendingTicks = 0;
        var entryX = startX;
        var entryBottom = player.Bottom;
        var launchCaptured = false;
        var launchX = 0f;
        var launchY = 0f;
        var launchHorizontalSpeed = 0f;
        for (var tick = 0; tick < SweepTicks; tick += 1)
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
                AimWorldX: player.X + direction * 256f,
                AimWorldY: player.Y,
                DebugKill: false,
                DropIntel: false,
                UseAbility: false);
            var jumpPressed = input.Up && !previousInput.Up;
            if (jumpPressed && !launchCaptured)
            {
                launchCaptured = true;
                launchX = player.X;
                launchY = player.Y;
                launchHorizontalSpeed = player.HorizontalSpeed;
            }
            player.Advance(
                input,
                jumpPressed,
                level,
                PlayerTeam.Red,
                1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;

            if (!player.IsGrounded)
            {
                pendingSurfaceId = -1;
                pendingTicks = 0;
                continue;
            }

            var detectedSurfaceId = FindSurfaceAt(geometry.Surfaces, player);
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

            if (pendingTicks < 2)
            {
                continue;
            }

            var destinationSurface = geometry.Surfaces[detectedSurfaceId];
            var kind = ResolveContactKind(
                jumpTick >= 0,
                player.Bottom,
                entryBottom,
                destinationSurface.Kind == VerifiedNavSurfaceKind.DropdownPlatform);
            var record = new ContactRecord(
                currentSurfaceId,
                detectedSurfaceId,
                entryX,
                player.X,
                startY,
                player.Bottom,
                kind,
                jumpTick >= 0 ? jumpTick : -1,
                Math.Max(1, tick + 1),
                direction,
                classMask,
                carryingIntel,
                launchCaptured ? launchX : entryX,
                launchCaptured ? launchY : startY,
                launchCaptured ? launchHorizontalSpeed : player.HorizontalSpeed);
            if (ShouldTraceContact(currentSurfaceId, detectedSurfaceId, carryingIntel))
            {
                Console.WriteLine(
                    $"[botbrain] contact-trace from={currentSurfaceId} to={detectedSurfaceId} " +
                    $"carry={(carryingIntel ? 1 : 0)} start=({startX:0.0},{startY:0.0}) " +
                    $"actualCarry={(player.IsCarryingIntel ? 1 : 0)} maxRun={player.MaxRunSpeed:0.0} " +
                    $"entry=({entryX:0.0},{entryBottom:0.0}) landing=({player.X:0.0},{player.Bottom:0.0}) " +
                    $"jump={jumpTick} tick={tick + 1} launch=({record.LaunchX:0.0},{record.LaunchY:0.0})");
            }
            var key = new ContactKey(
                record.FromSurfaceId,
                record.ToSurfaceId,
                record.Kind,
                Quantize(record.EntryX),
                Quantize(record.ExitX),
                classMask,
                carryingIntel);
            if (contacts.TryGetValue(key, out var existing))
            {
                var preferred = record.JumpTriggerTick > existing.JumpTriggerTick
                    ? record
                    : existing;
                contacts[key] = preferred with
                {
                    SupportedClassMask = existing.SupportedClassMask | classMask,
                    ProbeTicks = Math.Min(existing.ProbeTicks, record.ProbeTicks),
                };
            }
            else if (CountPairContacts(
                         contacts,
                         record.FromSurfaceId,
                         record.ToSurfaceId,
                         classMask,
                         carryingIntel) < MaximumContactsPerSurfacePair)
            {
                contacts.Add(key, record);
            }

            currentSurfaceId = detectedSurfaceId;
            pendingSurfaceId = -1;
            pendingTicks = 0;
            entryX = player.X;
            entryBottom = player.Bottom;
            launchCaptured = false;
            launchX = 0f;
            launchY = 0f;
            launchHorizontalSpeed = 0f;
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
                    if (!IsStaticWalkSpanClear(level, fromNode, toNode, carryingIntel))
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
                        BotBrainClassMask.All,
                        minimumCollisionBottom,
                        maximumCollisionBottom,
                        surface.Id,
                        carryingIntel);
                }

                foreach (var carryingIntel in new[] { false, true })
                {
                    if (!IsStaticWalkSpanClear(level, toNode, fromNode, carryingIntel))
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
                        BotBrainClassMask.All,
                        minimumCollisionBottom,
                        maximumCollisionBottom,
                        surface.Id,
                        carryingIntel);
                }
            }
        }
    }

    private static bool IsStaticWalkSpanClear(
        SimpleLevel level,
        NavNode fromNode,
        NavNode toNode,
        bool carryingIntel)
    {
        var definition = CharacterClassCatalog.Heavy;
        var probe = new PlayerEntity(-910_002, definition, "Og2LocalWalkClearanceProbe");
        probe.Spawn(PlayerTeam.Red, fromNode.X, fromNode.Y);
        probe.TeleportTo(fromNode.X, fromNode.Y);
        if (carryingIntel)
        {
            probe.PickUpIntel();
        }
        probe.ResolveBlockingOverlap(level, PlayerTeam.Red);

        var distance = MathF.Abs(toNode.X - fromNode.X);
        var samples = Math.Max(1, (int)MathF.Ceiling(distance / 8f));
        for (var sample = 0; sample <= samples; sample += 1)
        {
            var fraction = sample / (float)samples;
            var x = fromNode.X + ((toNode.X - fromNode.X) * fraction);
            if (!probe.CanOccupy(level, PlayerTeam.Red, x, fromNode.Y))
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
            BotBrainTeamMask.All,
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
            // The contact edge begins at the source surface node. Keep the
            // vertical launch band anchored to that source state; a sweep can
            // momentarily report an intermediate stair sample before the
            // jump input is consumed, but that sample is not a composable
            // runtime node and must not invalidate the source contact.
            LaunchMinY: contact.SourceY - 8f,
            LaunchMaxY: contact.SourceY + 8f,
            LaunchMinHorizontalSpeed: contact.LaunchHorizontalSpeed - speedTolerance,
            LaunchMaxHorizontalSpeed: contact.LaunchHorizontalSpeed + speedTolerance,
            ExpectedMoveDirectionX: contact.MoveDirection);
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
        bool? carryingIntelRequirement = null)
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
            BotBrainTeamMask.All,
            RequiresGroundedContinuation: false,
            RequiresCarryingIntel: carryingIntelRequirement == true,
            NavEdgeLaunchRecipe.None,
            CarryingIntelRequirement: carryingIntelRequirement);
        AddOrMergeEdge(fromNode, edge, adjacency, edgeIndex);
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

        foreach (var roomObject in level.RoomObjects)
        {
            if (roomObject.Type is not (RoomObjectType.ArenaControlPoint
                or RoomObjectType.ControlPoint
                or RoomObjectType.CaptureZone
                or RoomObjectType.Generator))
            {
                continue;
            }

            AddAnchor(level, surfaces, nodes, nodeBySample, adjacency, edgeIndex, roomObject.CenterX, roomObject.CenterY, NavNodeKind.Objective, roomObject.Team, true, spawnAnchors, minimumCollisionBottom, maximumCollisionBottom);
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
        float maximumCollisionBottom)
    {
        var anchorIndex = nodes.Count;
        nodes.Add(new NavNode(x, y, kind));
        if (team.HasValue && kind == NavNodeKind.Spawn)
        {
            spawnAnchors.Add(new NavSpawnAnchor(x, y, team.Value));
        }

        var candidates = surfaces
            .Where(surface => surface.Left <= x + AnchorSearchDistance && surface.Right >= x - AnchorSearchDistance)
            .Select(surface => (Surface: surface, Distance: MathF.Abs(surface.Top - (y + maximumCollisionBottom)) + DistanceToInterval(surface, x)))
            .OrderBy(static candidate => candidate.Distance)
            .Take(4)
            .ToArray();
        foreach (var candidate in candidates)
        {
            var surfaceX = Math.Clamp(x, candidate.Surface.Left, candidate.Surface.Right);
            var surfaceIndex = GetOrAddSurfaceNode(nodes, nodeBySample, candidate.Surface, surfaceX, maximumCollisionBottom);
            EnsureAdjacencyCapacity(adjacency, nodes.Count);
            var surfaceNode = nodes[surfaceIndex];
            if (objective)
            {
                var approachCompletion = new NavEdgeCompletion(
                    x - AnchorCompletionHorizontalSlack,
                    x + AnchorCompletionHorizontalSlack,
                    y - AnchorCompletionVerticalSlack,
                    y + AnchorCompletionVerticalSlack,
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
            }
            else
            {
                AddSimpleEdge(surfaceIndex, anchorIndex, NavEdgeKind.Walk, nodes, adjacency, edgeIndex, BotBrainClassMask.All, minimumCollisionBottom, maximumCollisionBottom, candidate.Surface.Id);
                AddSimpleEdge(anchorIndex, surfaceIndex, NavEdgeKind.Walk, nodes, adjacency, edgeIndex, BotBrainClassMask.All, minimumCollisionBottom, maximumCollisionBottom, candidate.Surface.Id);
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
        var left = MathF.Min(surface.Right, surface.Left + SurfaceSampleInset);
        var right = MathF.Max(surface.Left, surface.Right - SurfaceSampleInset);
        yield return left;
        if (MathF.Abs(right - left) > ContactBucket)
        {
            yield return right;
        }
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
        int exitBucket = 0)
    {
        var key = new DirectedEdgeKey(
            fromNode,
            edge.ToNode,
            edge.Kind,
            edge.SupportedClassMask,
            edge.RequiresCarryingIntel,
            entryBucket,
            exitBucket);
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
        IReadOnlyList<List<NavEdge>> adjacency)
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
            if (adjacency[nodeIndex].Count == 0 && incoming[nodeIndex] == 0)
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
                    edge.SupportedClassMask,
                    edge.RequiresCarryingIntel,
                    0,
                    0)] = edgeIndexInList;
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

    private static int CountPairContacts(
        IReadOnlyDictionary<ContactKey, ContactRecord> contacts,
        int fromSurfaceId,
        int toSurfaceId,
        int classMask,
        bool carryingIntel) =>
        contacts.Values.Count(record =>
            record.FromSurfaceId == fromSurfaceId
            && record.ToSurfaceId == toSurfaceId
            && record.RequiresCarryingIntel == carryingIntel
            && (record.SupportedClassMask & classMask) != 0);

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

    private static IEnumerable<PlayerClass> EnumerateRepresentativeClasses()
    {
        var configured = Environment.GetEnvironmentVariable("BOTBRAIN_NAV_ALPHA_CONTACT_CLASSES");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var selected = configured
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var playerClass) ? (PlayerClass?)playerClass : null)
                .Where(static playerClass => playerClass.HasValue)
                .Select(static playerClass => playerClass!.Value)
                .Distinct()
                .ToArray();
            if (selected.Length > 0)
            {
                return selected;
            }
        }

        return RepresentativeClasses;
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
        bool RequiresCarryingIntel,
        int EntryBucket,
        int ExitBucket);

    private readonly record struct ContactKey(
        int FromSurfaceId,
        int ToSurfaceId,
        NavEdgeKind Kind,
        int EntryBucket,
        int ExitBucket,
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
        float LaunchX,
        float LaunchY,
        float LaunchHorizontalSpeed);

    private readonly record struct MovementSignature(
        float RunPower,
        float JumpStrength,
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
                definition.MaxAirJumps,
                definition.CollisionLeft,
                definition.CollisionTop,
                definition.CollisionRight,
                definition.CollisionBottom);
        }
    }
}
