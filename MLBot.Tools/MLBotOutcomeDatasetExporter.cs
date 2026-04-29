using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Environment;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotOutcomeDatasetExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int Run(string[] args)
    {
        var options = MLBotOutcomeDatasetOptions.Parse(args);
        if (options.OutputPath is null)
        {
            Console.Error.WriteLine("missing required option: --out");
            return 1;
        }

        var anchors = LoadAnchors(options);
        if (options.MaxAnchors > 0 && anchors.Count > options.MaxAnchors)
        {
            anchors = anchors.Take(options.MaxAnchors).ToList();
        }

        if (anchors.Count == 0)
        {
            Console.Error.WriteLine("no eligible outcome anchors were found");
            return 1;
        }

        var actionTemplates = options.SequenceSearch ? [] : BuildActionTemplates(options);
        var samples = new List<MLBotOutcomeSample>(anchors.Count * Math.Max(1, actionTemplates.Length));
        foreach (var anchor in anchors)
        {
            if (options.AffordanceSequenceSearch)
            {
                samples.AddRange(SearchAffordanceCounterfactualSequences(anchor, options));
            }
            else if (options.RandomSequenceSearch)
            {
                samples.AddRange(SearchRandomCounterfactualSequences(anchor, options));
            }
            else if (options.SequenceSearch)
            {
                samples.AddRange(SearchCounterfactualSequences(anchor, options));
            }
            else
            {
                foreach (var actionTemplate in actionTemplates)
                {
                    samples.Add(RunCounterfactual(anchor, actionTemplate, options.HorizonTicks, options.JumpHoldTicks));
                }
            }
        }

        var directory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = options.Compact
            ? JsonSerializer.Serialize(BuildCompactDocument(options, anchors, options.SequenceSearch || options.RandomSequenceSearch || options.AffordanceSequenceSearch ? options.SequenceSearchTopK : actionTemplates.Length, samples), CompactJsonOptions)
            : JsonSerializer.Serialize(
                new MLBotOutcomeDatasetDocument
                {
                    SchemaVersion = "mlbot-outcome-v1",
                    HorizonTicks = options.HorizonTicks,
                    JumpHoldTicks = options.JumpHoldTicks,
                    SourceRollouts = options.DiscoverRolloutPaths(),
                    AnchorCount = anchors.Count,
                    ActionCount = actionTemplates.Length,
                    Samples = samples.ToArray(),
                },
                JsonOptions);

        File.WriteAllText(options.OutputPath, json);
        Console.WriteLine($"saved outcome_dataset={options.OutputPath}");
        Console.WriteLine($"anchors={anchors.Count} actions={(options.SequenceSearch || options.RandomSequenceSearch || options.AffordanceSequenceSearch ? samples.Count : actionTemplates.Length)} samples={samples.Count} horizon={options.HorizonTicks}");
        return 0;
    }

    private static MLBotCompactOutcomeDatasetDocument BuildCompactDocument(
        MLBotOutcomeDatasetOptions options,
        IReadOnlyList<OutcomeAnchor> anchors,
        int actionCount,
        List<MLBotOutcomeSample> samples)
    {
        var anchorIds = new Dictionary<(string SourcePath, int SourceTick), int>();
        var anchorDocuments = new List<MLBotCompactOutcomeAnchor>(anchors.Count);
        for (var index = 0; index < anchors.Count; index += 1)
        {
            var anchor = anchors[index];
            var key = (anchor.SourcePath, anchor.SourceTick);
            if (anchorIds.ContainsKey(key))
            {
                continue;
            }

            var anchorId = anchorDocuments.Count;
            anchorIds[key] = anchorId;
            anchorDocuments.Add(new MLBotCompactOutcomeAnchor
            {
                AnchorId = anchorId,
                SourcePath = anchor.SourcePath,
                SourceTick = anchor.SourceTick,
                StartObservation = anchor.Observation,
            });
        }

        var compactSamples = new List<MLBotCompactOutcomeSample>(samples.Count);
        foreach (var sample in samples)
        {
            var key = (sample.SourcePath, sample.SourceTick);
            compactSamples.Add(new MLBotCompactOutcomeSample
            {
                AnchorId = anchorIds[key],
                ActionName = sample.ActionName,
                MoveDirection = sample.MoveDirection,
                Jump = sample.Jump,
                Crouch = sample.Crouch,
                MoveDirections = sample.MoveDirections,
                JumpSequence = sample.JumpSequence,
                HorizonTicks = sample.HorizonTicks,
                ActualTicks = sample.ActualTicks,
                DeltaX = sample.DeltaX,
                DeltaY = sample.DeltaY,
                DeltaVelocityX = sample.DeltaVelocityX,
                DeltaVelocityY = sample.DeltaVelocityY,
                ObjectiveDistanceDelta = sample.ObjectiveDistanceDelta,
                MinObjectiveDistanceDelta = sample.MinObjectiveDistanceDelta,
                MaxVerticalGain = sample.MaxVerticalGain,
                UpwardLandingProgress = sample.UpwardLandingProgress,
                MaxDistanceFromStart = sample.MaxDistanceFromStart,
                WallContactTicks = sample.WallContactTicks,
                NoProgressTicks = sample.NoProgressTicks,
                ObjectiveRegressionTicks = sample.ObjectiveRegressionTicks,
                UsefulProgressTicks = sample.UsefulProgressTicks,
                JumpTicks = sample.JumpTicks,
                ProductiveLanding = sample.ProductiveLanding,
                LandedHigher = sample.LandedHigher,
                ObjectiveImproved = sample.ObjectiveImproved,
                WastedJump = sample.WastedJump,
                LocalLoop = sample.LocalLoop,
                EndedNearUpwardLanding = sample.EndedNearUpwardLanding,
                EndGrounded = sample.EndGrounded,
                BecameAirborne = sample.BecameAirborne,
                LandedAfterAirborne = sample.LandedAfterAirborne,
                HitWall = sample.HitWall,
                Success = sample.Success,
                TerminalReason = sample.TerminalReason,
                TotalReward = sample.TotalReward,
            });
        }

        return new MLBotCompactOutcomeDatasetDocument
        {
            SchemaVersion = "mlbot-outcome-compact-v1",
            HorizonTicks = options.HorizonTicks,
            JumpHoldTicks = options.JumpHoldTicks,
            SourceRollouts = options.DiscoverRolloutPaths(),
            AnchorCount = anchorDocuments.Count,
            ActionCount = actionCount,
            Anchors = anchorDocuments.ToArray(),
            Samples = compactSamples.ToArray(),
        };
    }

    private static List<OutcomeAnchor> LoadAnchors(MLBotOutcomeDatasetOptions options)
    {
        var sourceAnchors = new List<SourceAnchor>();
        foreach (var path in options.DiscoverRolloutPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var document = JsonSerializer.Deserialize<MLBotRolloutDocument>(File.ReadAllText(path), JsonOptions);
            if (document is null)
            {
                continue;
            }

            foreach (var step in document.Steps.Where((_, index) => index % options.AnchorStride == 0))
            {
                var observation = step.Observation;
                if (options.TaskPhaseFilter.HasValue && observation.TaskPhase != options.TaskPhaseFilter.Value)
                {
                    continue;
                }

                if (options.TeamFilter.HasValue && observation.Team != options.TeamFilter.Value)
                {
                    continue;
                }

                if (options.ClassFilter.HasValue && observation.ClassId != options.ClassFilter.Value)
                {
                    continue;
                }

                if (options.CarryingIntelFilter.HasValue && observation.IsCarryingIntel != options.CarryingIntelFilter.Value)
                {
                    continue;
                }

                if (options.IsGroundedFilter.HasValue && observation.IsGrounded != options.IsGroundedFilter.Value)
                {
                    continue;
                }

                if (options.MinObjectiveDistance > 0f && observation.ObjectiveDistance < options.MinObjectiveDistance)
                {
                    continue;
                }

                if (options.MaxObjectiveDistance > 0f && observation.ObjectiveDistance > options.MaxObjectiveDistance)
                {
                    continue;
                }

                sourceAnchors.Add(new SourceAnchor(path, step.Tick, observation));
            }
        }

        sourceAnchors = SortSourceAnchors(sourceAnchors, options.AnchorSelection);
        if (options.MaxSourceAnchors > 0 && sourceAnchors.Count > options.MaxSourceAnchors)
        {
            sourceAnchors = sourceAnchors.Take(options.MaxSourceAnchors).ToList();
        }

        var anchors = new List<OutcomeAnchor>();
        foreach (var sourceAnchor in sourceAnchors)
        {
            anchors.AddRange(BuildAnchorVariants(sourceAnchor.SourcePath, sourceAnchor.SourceTick, sourceAnchor.Observation, options));
        }

        return anchors;
    }

    private static List<SourceAnchor> SortSourceAnchors(List<SourceAnchor> anchors, string selection)
    {
        return selection.Trim().ToLowerInvariant() switch
        {
            "" or "file-order" => anchors,
            "closest-objective" or "closest" => anchors
                .OrderBy(static anchor => anchor.Observation.ObjectiveDistance)
                .ThenBy(static anchor => anchor.SourceTick)
                .ToList(),
            "highest-stuck" or "stuck" => anchors
                .OrderByDescending(static anchor => anchor.Observation.StuckTicks)
                .ThenBy(static anchor => anchor.Observation.ObjectiveDistance)
                .ToList(),
            "latest" => anchors
                .OrderByDescending(static anchor => anchor.SourceTick)
                .ToList(),
            "earliest" => anchors
                .OrderBy(static anchor => anchor.SourceTick)
                .ToList(),
            _ => anchors,
        };
    }

    private static IEnumerable<OutcomeAnchor> BuildAnchorVariants(
        string sourcePath,
        int sourceTick,
        MLBotObservation observation,
        MLBotOutcomeDatasetOptions options)
    {
        var classVariants = new PlayerClass?[] { null }
            .Concat(options.AugmentClasses.Select(static classId => (PlayerClass?)classId))
            .ToArray();
        var xOffsets = BuildVariantValues(options.XOffsets);
        var yOffsets = BuildVariantValues(options.YOffsets);
        var vxOffsets = BuildVariantValues(options.VelocityXOffsets);
        var vyOffsets = BuildVariantValues(options.VelocityYOffsets);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var classVariant in classVariants)
        {
            foreach (var xOffset in xOffsets)
            {
                foreach (var yOffset in yOffsets)
                {
                    foreach (var vxOffset in vxOffsets)
                    {
                        foreach (var vyOffset in vyOffsets)
                        {
                            var classId = classVariant ?? observation.ClassId;
                            var variantKey = string.Create(
                                System.Globalization.CultureInfo.InvariantCulture,
                                $"{classId}:x{xOffset:0.###}:y{yOffset:0.###}:vx{vxOffset:0.###}:vy{vyOffset:0.###}");
                            if (!seen.Add(variantKey))
                            {
                                continue;
                            }

                            var variant = observation with
                            {
                                ClassId = classId,
                                BotX = observation.BotX + xOffset,
                                BotY = observation.BotY + yOffset,
                                VelocityX = observation.VelocityX + vxOffset,
                                VelocityY = observation.VelocityY + vyOffset,
                            };
                            yield return new OutcomeAnchor(
                                SourcePath: variantKey == $"{observation.ClassId}:x0:y0:vx0:vy0"
                                    ? sourcePath
                                    : $"{sourcePath}#{variantKey}",
                                SourceTick: sourceTick,
                                Observation: variant);
                        }
                    }
                }
            }
        }
    }

    private static float[] BuildVariantValues(List<float> configuredValues)
    {
        return configuredValues.Count == 0
            ? [0f]
            : configuredValues
                .Concat([0f])
                .Distinct()
                .Order()
                .ToArray();
    }

    private static OutcomeActionTemplate[] BuildActionTemplates(MLBotOutcomeDatasetOptions options)
    {
        var names = options.ActionNames.Count > 0
            ? options.ActionNames
            : ["idle", "left", "right", "jump", "jump_left", "jump_right"];
        return names.Select(ParseActionTemplate).ToArray();
    }

    private static OutcomeActionTemplate ParseActionTemplate(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "idle" => new("idle", 0, false, false),
            "left" => new("left", -1, false, false),
            "right" => new("right", 1, false, false),
            "jump" => new("jump", 0, true, false),
            "jump_left" or "jump-left" => new("jump_left", -1, true, false),
            "jump_right" or "jump-right" => new("jump_right", 1, true, false),
            "jump_left_then_right" or "jump-left-then-right" => new("jump_left_then_right", -1, true, false, 1),
            "jump_right_then_left" or "jump-right-then-left" => new("jump_right_then_left", 1, true, false, -1),
            "jump_then_left" or "jump-then-left" => new("jump_then_left", 0, true, false, -1),
            "jump_then_right" or "jump-then-right" => new("jump_then_right", 0, true, false, 1),
            "left_then_jump_right" or "left-then-jump-right" => new("left_then_jump_right", -1, true, false, 1, 0),
            "right_then_jump_left" or "right-then-jump-left" => new("right_then_jump_left", 1, true, false, -1, 0),
            "tap_jump" or "tap-jump" => new("tap_jump", 0, true, false, null, 0, 3),
            "tap_jump_left" or "tap-jump-left" => new("tap_jump_left", -1, true, false, null, 0, 3),
            "tap_jump_right" or "tap-jump-right" => new("tap_jump_right", 1, true, false, null, 0, 3),
            "jump_tap_then_left" or "jump-tap-then-left" => new("jump_tap_then_left", 0, true, false, -1, 0, 3, 3),
            "jump_tap_then_right" or "jump-tap-then-right" => new("jump_tap_then_right", 0, true, false, 1, 0, 3, 3),
            "left_then_tap_jump" or "left-then-tap-jump" => new("left_then_tap_jump", -1, true, false, -1, 4, 3),
            "right_then_tap_jump" or "right-then-tap-jump" => new("right_then_tap_jump", 1, true, false, 1, 4, 3),
            "left_then_tap_jump_right" or "left-then-tap-jump-right" => new("left_then_tap_jump_right", -1, true, false, 1, 4, 3, 7),
            "right_then_tap_jump_left" or "right-then-tap-jump-left" => new("right_then_tap_jump_left", 1, true, false, -1, 4, 3, 7),
            "double_jump_left_early" or "double-jump-left-early" => new("double_jump_left_early", -1, true, false, null, 0, 3, null, 5, 3),
            "double_jump_right_early" or "double-jump-right-early" => new("double_jump_right_early", 1, true, false, null, 0, 3, null, 5, 3),
            "double_jump_left_mid" or "double-jump-left-mid" => new("double_jump_left_mid", -1, true, false, null, 0, 3, null, 8, 3),
            "double_jump_right_mid" or "double-jump-right-mid" => new("double_jump_right_mid", 1, true, false, null, 0, 3, null, 8, 3),
            "double_jump_left_late" or "double-jump-left-late" => new("double_jump_left_late", -1, true, false, null, 0, 3, null, 12, 3),
            "double_jump_right_late" or "double-jump-right-late" => new("double_jump_right_late", 1, true, false, null, 0, 3, null, 12, 3),
            "double_jump_left_then_right" or "double-jump-left-then-right" => new("double_jump_left_then_right", -1, true, false, 1, 0, 3, 8, 6, 3),
            "double_jump_right_then_left" or "double-jump-right-then-left" => new("double_jump_right_then_left", 1, true, false, -1, 0, 3, 8, 6, 3),
            "drop" => new("drop", 0, false, true),
            "drop_left" or "drop-left" => new("drop_left", -1, false, true),
            "drop_right" or "drop-right" => new("drop_right", 1, false, true),
            _ => throw new ArgumentException($"unknown outcome action template: {name}"),
        };
    }

    private static MLBotOutcomeSample RunCounterfactual(
        OutcomeAnchor anchor,
        OutcomeActionTemplate actionTemplate,
        int horizonTicks,
        int jumpHoldTicks)
    {
        var start = anchor.Observation;
        var config = MLBotEpisodeConfig.CreateDefault(
            levelName: start.LevelName,
            taskPhase: start.TaskPhase,
            team: start.Team,
            classId: start.ClassId,
            maxTicks: horizonTicks,
            startX: start.BotX,
            startY: start.BotY,
            startVelocityX: start.VelocityX,
            startVelocityY: start.VelocityY,
            carryingIntel: start.IsCarryingIntel,
            startIsGrounded: start.IsGrounded,
            startRemainingAirJumps: start.RemainingAirJumps,
            startFacingDirectionX: start.FacingDirectionX,
            startPreviousMoveInput: start.PreviousMoveInput,
            startPreviousJumpHeld: start.PreviousJumpHeld,
            startPreviousDropInput: start.PreviousDropInput,
            startPreviousFirePrimary: start.PreviousActionFirePrimary,
            startPreviousFireSecondary: start.PreviousActionFireSecondary,
            startPreviousPositionDeltaX: start.PreviousPositionDeltaX,
            startPreviousPositionDeltaY: start.PreviousPositionDeltaY,
            startPreviousVelocityX: start.PreviousVelocityX,
            startPreviousVelocityY: start.PreviousVelocityY,
            startPreviousFacingDirectionX: start.PreviousFacingDirectionX,
            startPreviousIsGrounded: start.PreviousIsGrounded,
            startObjectiveDistance: start.ObjectiveDistance,
            startObjectiveDistanceDelta: start.ObjectiveDistanceDelta,
            startPreviousObjectiveDistanceDelta: start.PreviousObjectiveDistanceDelta,
            startAirborneTicks: start.AirborneTicks,
            startJumpTicks: start.JumpTicks,
            startFramesSinceJumpPressed: start.FramesSinceJumpPressed,
            startFramesSinceJumpReleased: start.FramesSinceJumpReleased);
        var environment = new MLBotEnvironment();
        var initialObservation = environment.Reset(config);
        var observations = new List<MLBotObservation> { initialObservation };
        var totalReward = 0f;
        var becameAirborne = !initialObservation.IsGrounded;
        var landedAfterAirborne = false;
        var hitWall = initialObservation.Probes.TouchingLeftWall || initialObservation.Probes.TouchingRightWall;
        var minY = initialObservation.BotY;
        var minObjectiveDistance = initialObservation.ObjectiveDistance;
        var maxDistanceFromStart = 0f;
        var wallContactTicks = hitWall ? 1 : 0;
        var noProgressTicks = 0;
        var objectiveRegressionTicks = 0;
        var usefulProgressTicks = 0;
        var jumpTicks = 0;
        var previousObjectiveDistance = initialObservation.ObjectiveDistance;
        var previousX = initialObservation.BotX;
        var previousY = initialObservation.BotY;
        MLBotStepResult result = default;
        var moveDirections = new List<int>(horizonTicks);
        var jumpSequence = new List<bool>(horizonTicks);

        for (var tick = 0; tick < horizonTicks; tick += 1)
        {
            var moveDirection = actionTemplate.MoveDirectionAt(tick, jumpHoldTicks);
            var jump = actionTemplate.JumpAt(tick, jumpHoldTicks);
            if (jump)
            {
                jumpTicks += 1;
            }

            var action = new MLBotAction(
                MoveDirection: moveDirection,
                Jump: jump,
                Crouch: actionTemplate.Crouch,
                FirePrimary: false,
                FireSecondary: false,
                DropIntel: false,
                AimWorldX: initialObservation.Objective.HasObjective ? initialObservation.Objective.WorldX : initialObservation.BotX,
                AimWorldY: initialObservation.Objective.HasObjective ? initialObservation.Objective.WorldY : initialObservation.BotY);
            moveDirections.Add(moveDirection);
            jumpSequence.Add(jump);
            result = environment.Step(action);
            totalReward += result.Reward.Total;
            var observation = result.Observation;
            observations.Add(observation);
            if (!observation.IsGrounded)
            {
                becameAirborne = true;
            }

            if (becameAirborne && observation.IsGrounded && tick > 0)
            {
                landedAfterAirborne = true;
            }

            hitWall |= observation.Probes.TouchingLeftWall || observation.Probes.TouchingRightWall;
            if (observation.Probes.TouchingLeftWall || observation.Probes.TouchingRightWall)
            {
                wallContactTicks += 1;
            }

            minY = MathF.Min(minY, observation.BotY);
            minObjectiveDistance = MathF.Min(minObjectiveDistance, observation.ObjectiveDistance);
            var distanceFromStart = Distance(initialObservation.BotX, initialObservation.BotY, observation.BotX, observation.BotY);
            maxDistanceFromStart = MathF.Max(maxDistanceFromStart, distanceFromStart);
            var stepMovement = MathF.Abs(observation.BotX - previousX) + MathF.Abs(observation.BotY - previousY);
            var stepObjectiveDelta = previousObjectiveDistance - observation.ObjectiveDistance;
            var upwardStepProgress = initialObservation.Objective.RelativeY < -32f
                ? MathF.Max(0f, previousY - observation.BotY)
                : 0f;
            if (stepMovement < 0.35f && stepObjectiveDelta <= 0.25f && upwardStepProgress <= 0.25f)
            {
                noProgressTicks += 1;
            }

            if (stepObjectiveDelta < -1f && upwardStepProgress <= 0.25f)
            {
                objectiveRegressionTicks += 1;
            }

            if (stepObjectiveDelta > 1f || upwardStepProgress > 1f)
            {
                usefulProgressTicks += 1;
            }

            previousObjectiveDistance = observation.ObjectiveDistance;
            previousX = observation.BotX;
            previousY = observation.BotY;
            if (result.IsTerminal)
            {
                break;
            }
        }

        var end = observations[^1];
        var upwardLandingProgress = ComputeUpwardLandingProgress(initialObservation, end);
        var endedNearUpwardLanding = IsNearBestUpwardLanding(initialObservation, end);
        var actualTicks = Math.Max(1, observations.Count - 1);
        var landedHigher = landedAfterAirborne && end.IsGrounded && end.BotY <= initialObservation.BotY - 4f;
        var objectiveImproved = initialObservation.ObjectiveDistance - minObjectiveDistance >= 16f
            || initialObservation.ObjectiveDistance - end.ObjectiveDistance >= 16f;
        var productiveLanding = endedNearUpwardLanding
            || (landedHigher && (upwardLandingProgress >= 12f || objectiveImproved));
        var wastedJump = jumpTicks > 0
            && !productiveLanding
            && !result.IsSuccess
            && upwardLandingProgress < 8f
            && initialObservation.BotY - minY < 8f;
        var localLoop = maxDistanceFromStart < 16f
            || noProgressTicks >= Math.Max(8, actualTicks / 2);
        return new MLBotOutcomeSample
        {
            SourcePath = anchor.SourcePath,
            SourceTick = anchor.SourceTick,
            ActionName = actionTemplate.Name,
            MoveDirection = actionTemplate.MoveDirection,
            Jump = actionTemplate.Jump,
            Crouch = actionTemplate.Crouch,
            MoveDirections = moveDirections.ToArray(),
            JumpSequence = jumpSequence.ToArray(),
            HorizonTicks = horizonTicks,
            ActualTicks = Math.Max(0, observations.Count - 1),
            StartObservation = initialObservation,
            EndObservation = end,
            DeltaX = end.BotX - initialObservation.BotX,
            DeltaY = end.BotY - initialObservation.BotY,
            DeltaVelocityX = end.VelocityX - initialObservation.VelocityX,
            DeltaVelocityY = end.VelocityY - initialObservation.VelocityY,
            ObjectiveDistanceDelta = initialObservation.ObjectiveDistance - end.ObjectiveDistance,
            MinObjectiveDistanceDelta = initialObservation.ObjectiveDistance - minObjectiveDistance,
            MaxVerticalGain = initialObservation.BotY - minY,
            UpwardLandingProgress = upwardLandingProgress,
            MaxDistanceFromStart = maxDistanceFromStart,
            WallContactTicks = wallContactTicks,
            NoProgressTicks = noProgressTicks,
            ObjectiveRegressionTicks = objectiveRegressionTicks,
            UsefulProgressTicks = usefulProgressTicks,
            JumpTicks = jumpTicks,
            ProductiveLanding = productiveLanding,
            LandedHigher = landedHigher,
            ObjectiveImproved = objectiveImproved,
            WastedJump = wastedJump,
            LocalLoop = localLoop,
            EndedNearUpwardLanding = endedNearUpwardLanding,
            EndGrounded = end.IsGrounded,
            BecameAirborne = becameAirborne,
            LandedAfterAirborne = landedAfterAirborne,
            HitWall = hitWall,
            Success = result.IsSuccess,
            TerminalReason = result.TerminalReason,
            TotalReward = totalReward,
        };
    }

    private static List<MLBotOutcomeSample> SearchCounterfactualSequences(
        OutcomeAnchor anchor,
        MLBotOutcomeDatasetOptions options)
    {
        var segmentTicks = Math.Max(1, options.SequenceSearchSegmentTicks);
        var segments = Math.Max(1, (int)MathF.Ceiling(options.HorizonTicks / (float)segmentTicks));
        var beam = new List<SequenceCandidate>
        {
            new([], [], null, float.NegativeInfinity),
        };
        var primitives = new (int MoveDirection, bool Jump)[]
        {
            (-1, false),
            (0, false),
            (1, false),
            (-1, true),
            (0, true),
            (1, true),
        };

        for (var segment = 0; segment < segments; segment += 1)
        {
            var expanded = new List<SequenceCandidate>(beam.Count * primitives.Length);
            foreach (var candidate in beam)
            {
                foreach (var primitive in primitives)
                {
                    var moves = AppendRepeated(candidate.MoveDirections, primitive.MoveDirection, segmentTicks, options.HorizonTicks);
                    var jumps = AppendRepeated(candidate.JumpSequence, primitive.Jump, segmentTicks, options.HorizonTicks);
                    var template = OutcomeActionTemplate.FromSequence($"search_s{segment}_{primitive.MoveDirection}_{primitive.Jump}", moves, jumps);
                    var sample = RunCounterfactual(anchor, template, options.HorizonTicks, options.JumpHoldTicks);
                    var score = ScoreOutcomeSample(sample);
                    expanded.Add(new SequenceCandidate(moves, jumps, sample, score));
                }
            }

            beam = expanded
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Sample?.Success ?? false)
                .Take(options.SequenceSearchBeamWidth)
                .ToList();
        }

        return beam
            .Where(candidate => candidate.Sample is not null)
            .OrderByDescending(candidate => candidate.Score)
            .Take(options.SequenceSearchTopK)
            .Select((candidate, index) =>
            {
                var sample = candidate.Sample!;
                sample.ActionName = $"sequence_search_{index:00}_{sample.ActionName}";
                return sample;
            })
            .ToList();
    }

    private static List<MLBotOutcomeSample> SearchRandomCounterfactualSequences(
        OutcomeAnchor anchor,
        MLBotOutcomeDatasetOptions options)
    {
        var rng = new Random(HashCode.Combine(options.Seed, anchor.SourcePath, anchor.SourceTick));
        var segmentTicks = Math.Max(1, options.SequenceSearchSegmentTicks);
        var segments = Math.Max(1, (int)MathF.Ceiling(options.HorizonTicks / (float)segmentTicks));
        var primitives = new (int MoveDirection, bool Jump)[]
        {
            (-1, false),
            (0, false),
            (1, false),
            (-1, true),
            (0, true),
            (1, true),
        };
        var candidates = new List<SequenceCandidate>(options.RandomSequenceCount);
        for (var candidateIndex = 0; candidateIndex < options.RandomSequenceCount; candidateIndex += 1)
        {
            var moves = new int[options.HorizonTicks];
            var jumps = new bool[options.HorizonTicks];
            for (var segment = 0; segment < segments; segment += 1)
            {
                var primitive = primitives[rng.Next(primitives.Length)];
                var start = segment * segmentTicks;
                var end = Math.Min(options.HorizonTicks, start + segmentTicks);
                for (var tick = start; tick < end; tick += 1)
                {
                    moves[tick] = primitive.MoveDirection;
                    jumps[tick] = primitive.Jump;
                }
            }

            var template = OutcomeActionTemplate.FromSequence($"random_{candidateIndex:0000}", moves, jumps);
            var sample = RunCounterfactual(anchor, template, options.HorizonTicks, options.JumpHoldTicks);
            candidates.Add(new SequenceCandidate(moves, jumps, sample, ScoreOutcomeSample(sample)));
        }

        return candidates
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.Sample?.Success ?? false)
            .Take(options.SequenceSearchTopK)
            .Select((candidate, index) =>
            {
                var sample = candidate.Sample!;
                sample.ActionName = $"random_sequence_{index:00}_{sample.ActionName}";
                return sample;
            })
            .ToList();
    }

    private static List<MLBotOutcomeSample> SearchAffordanceCounterfactualSequences(
        OutcomeAnchor anchor,
        MLBotOutcomeDatasetOptions options)
    {
        var profiles = new AffordanceControllerProfile[]
        {
            new("affordance_w24_hold6_release2", 24f, 8f, 6, 2, 14),
            new("affordance_w36_hold6_release2", 36f, 8f, 6, 2, 14),
            new("affordance_w48_hold6_release2", 48f, 10f, 6, 2, 14),
            new("affordance_w64_hold8_release2", 64f, 12f, 8, 2, 16),
            new("affordance_w36_hold8_release4", 36f, 8f, 8, 4, 16),
            new("affordance_w48_hold8_release4", 48f, 10f, 8, 4, 16),
            new("affordance_w64_hold10_release4", 64f, 12f, 10, 4, 18),
            new("affordance_w80_hold10_release4", 80f, 14f, 10, 4, 18),
            new("affordance_w48_hold6_release8", 48f, 10f, 6, 8, 18),
            new("affordance_w64_hold8_release8", 64f, 12f, 8, 8, 18),
            new("affordance_w80_hold10_release8", 80f, 14f, 10, 8, 20),
            new("affordance_w96_hold12_release8", 96f, 16f, 12, 8, 20),
        };

        return profiles
            .Select(profile => RunAffordanceCounterfactual(anchor, profile, options.HorizonTicks))
            .OrderByDescending(static sample => ScoreOutcomeSample(sample))
            .ThenByDescending(static sample => sample.Success)
            .Take(options.SequenceSearchTopK)
            .ToList();
    }

    private static MLBotOutcomeSample RunAffordanceCounterfactual(
        OutcomeAnchor anchor,
        AffordanceControllerProfile profile,
        int horizonTicks)
    {
        var start = anchor.Observation;
        var config = MLBotEpisodeConfig.CreateDefault(
            levelName: start.LevelName,
            taskPhase: start.TaskPhase,
            team: start.Team,
            classId: start.ClassId,
            maxTicks: horizonTicks,
            startX: start.BotX,
            startY: start.BotY,
            startVelocityX: start.VelocityX,
            startVelocityY: start.VelocityY,
            carryingIntel: start.IsCarryingIntel,
            startIsGrounded: start.IsGrounded,
            startRemainingAirJumps: start.RemainingAirJumps,
            startFacingDirectionX: start.FacingDirectionX,
            startPreviousMoveInput: start.PreviousMoveInput,
            startPreviousJumpHeld: start.PreviousJumpHeld,
            startPreviousDropInput: start.PreviousDropInput,
            startPreviousFirePrimary: start.PreviousActionFirePrimary,
            startPreviousFireSecondary: start.PreviousActionFireSecondary,
            startPreviousPositionDeltaX: start.PreviousPositionDeltaX,
            startPreviousPositionDeltaY: start.PreviousPositionDeltaY,
            startPreviousVelocityX: start.PreviousVelocityX,
            startPreviousVelocityY: start.PreviousVelocityY,
            startPreviousFacingDirectionX: start.PreviousFacingDirectionX,
            startPreviousIsGrounded: start.PreviousIsGrounded,
            startObjectiveDistance: start.ObjectiveDistance,
            startObjectiveDistanceDelta: start.ObjectiveDistanceDelta,
            startPreviousObjectiveDistanceDelta: start.PreviousObjectiveDistanceDelta,
            startAirborneTicks: start.AirborneTicks,
            startJumpTicks: start.JumpTicks,
            startFramesSinceJumpPressed: start.FramesSinceJumpPressed,
            startFramesSinceJumpReleased: start.FramesSinceJumpReleased);
        var environment = new MLBotEnvironment();
        var initialObservation = environment.Reset(config);
        var observations = new List<MLBotObservation> { initialObservation };
        var totalReward = 0f;
        var becameAirborne = !initialObservation.IsGrounded;
        var landedAfterAirborne = false;
        var hitWall = initialObservation.Probes.TouchingLeftWall || initialObservation.Probes.TouchingRightWall;
        var minY = initialObservation.BotY;
        var minObjectiveDistance = initialObservation.ObjectiveDistance;
        var maxDistanceFromStart = 0f;
        var wallContactTicks = hitWall ? 1 : 0;
        var noProgressTicks = 0;
        var objectiveRegressionTicks = 0;
        var usefulProgressTicks = 0;
        var jumpTicks = 0;
        var previousObjectiveDistance = initialObservation.ObjectiveDistance;
        var previousX = initialObservation.BotX;
        var previousY = initialObservation.BotY;
        var jumpHoldRemaining = 0;
        var jumpCooldownRemaining = 0;
        MLBotStepResult result = default;
        var moveDirections = new List<int>(horizonTicks);
        var jumpSequence = new List<bool>(horizonTicks);

        for (var tick = 0; tick < horizonTicks; tick += 1)
        {
            var observationBeforeAction = observations[^1];
            var moveDirection = ChooseAffordanceMoveDirection(observationBeforeAction, profile);
            var jump = false;
            if (tick >= profile.InitialReleaseTicks)
            {
                if (jumpHoldRemaining > 0)
                {
                    jump = true;
                    jumpHoldRemaining -= 1;
                }
                else if (jumpCooldownRemaining <= 0 && ShouldAffordanceJump(observationBeforeAction, moveDirection, profile))
                {
                    jump = true;
                    jumpHoldRemaining = Math.Max(0, profile.JumpHoldTicks - 1);
                    jumpCooldownRemaining = profile.JumpCooldownTicks;
                }
            }

            if (jumpCooldownRemaining > 0)
            {
                jumpCooldownRemaining -= 1;
            }

            if (jump)
            {
                jumpTicks += 1;
            }

            var aimObservation = observationBeforeAction.Objective.HasObjective ? observationBeforeAction : initialObservation;
            var action = new MLBotAction(
                MoveDirection: moveDirection,
                Jump: jump,
                Crouch: false,
                FirePrimary: false,
                FireSecondary: false,
                DropIntel: false,
                AimWorldX: aimObservation.Objective.HasObjective ? aimObservation.Objective.WorldX : aimObservation.BotX,
                AimWorldY: aimObservation.Objective.HasObjective ? aimObservation.Objective.WorldY : aimObservation.BotY);
            moveDirections.Add(moveDirection);
            jumpSequence.Add(jump);
            result = environment.Step(action);
            totalReward += result.Reward.Total;
            var observation = result.Observation;
            observations.Add(observation);
            if (!observation.IsGrounded)
            {
                becameAirborne = true;
            }

            if (becameAirborne && observation.IsGrounded && tick > 0)
            {
                landedAfterAirborne = true;
            }

            hitWall |= observation.Probes.TouchingLeftWall || observation.Probes.TouchingRightWall;
            if (observation.Probes.TouchingLeftWall || observation.Probes.TouchingRightWall)
            {
                wallContactTicks += 1;
            }

            minY = MathF.Min(minY, observation.BotY);
            minObjectiveDistance = MathF.Min(minObjectiveDistance, observation.ObjectiveDistance);
            var distanceFromStart = Distance(initialObservation.BotX, initialObservation.BotY, observation.BotX, observation.BotY);
            maxDistanceFromStart = MathF.Max(maxDistanceFromStart, distanceFromStart);
            var stepMovement = MathF.Abs(observation.BotX - previousX) + MathF.Abs(observation.BotY - previousY);
            var stepObjectiveDelta = previousObjectiveDistance - observation.ObjectiveDistance;
            var upwardStepProgress = initialObservation.Objective.RelativeY < -32f
                ? MathF.Max(0f, previousY - observation.BotY)
                : 0f;
            if (stepMovement < 0.35f && stepObjectiveDelta <= 0.25f && upwardStepProgress <= 0.25f)
            {
                noProgressTicks += 1;
            }

            if (stepObjectiveDelta < -1f && upwardStepProgress <= 0.25f)
            {
                objectiveRegressionTicks += 1;
            }

            if (stepObjectiveDelta > 1f || upwardStepProgress > 1f)
            {
                usefulProgressTicks += 1;
            }

            previousObjectiveDistance = observation.ObjectiveDistance;
            previousX = observation.BotX;
            previousY = observation.BotY;
            if (result.IsTerminal)
            {
                break;
            }
        }

        var end = observations[^1];
        var upwardLandingProgress = ComputeUpwardLandingProgress(initialObservation, end);
        var endedNearUpwardLanding = IsNearBestUpwardLanding(initialObservation, end);
        var actualTicks = Math.Max(1, observations.Count - 1);
        var landedHigher = landedAfterAirborne && end.IsGrounded && end.BotY <= initialObservation.BotY - 4f;
        var objectiveImproved = initialObservation.ObjectiveDistance - minObjectiveDistance >= 16f
            || initialObservation.ObjectiveDistance - end.ObjectiveDistance >= 16f;
        var productiveLanding = endedNearUpwardLanding
            || (landedHigher && (upwardLandingProgress >= 12f || objectiveImproved));
        var wastedJump = jumpTicks > 0
            && !productiveLanding
            && !result.IsSuccess
            && upwardLandingProgress < 8f
            && initialObservation.BotY - minY < 8f;
        var localLoop = maxDistanceFromStart < 16f
            || noProgressTicks >= Math.Max(8, actualTicks / 2);
        return new MLBotOutcomeSample
        {
            SourcePath = anchor.SourcePath,
            SourceTick = anchor.SourceTick,
            ActionName = profile.Name,
            MoveDirection = moveDirections.Count > 0 ? moveDirections[0] : 0,
            Jump = jumpSequence.Any(static value => value),
            Crouch = false,
            MoveDirections = moveDirections.ToArray(),
            JumpSequence = jumpSequence.ToArray(),
            HorizonTicks = horizonTicks,
            ActualTicks = Math.Max(0, observations.Count - 1),
            StartObservation = initialObservation,
            EndObservation = end,
            DeltaX = end.BotX - initialObservation.BotX,
            DeltaY = end.BotY - initialObservation.BotY,
            DeltaVelocityX = end.VelocityX - initialObservation.VelocityX,
            DeltaVelocityY = end.VelocityY - initialObservation.VelocityY,
            ObjectiveDistanceDelta = initialObservation.ObjectiveDistance - end.ObjectiveDistance,
            MinObjectiveDistanceDelta = initialObservation.ObjectiveDistance - minObjectiveDistance,
            MaxVerticalGain = initialObservation.BotY - minY,
            UpwardLandingProgress = upwardLandingProgress,
            MaxDistanceFromStart = maxDistanceFromStart,
            WallContactTicks = wallContactTicks,
            NoProgressTicks = noProgressTicks,
            ObjectiveRegressionTicks = objectiveRegressionTicks,
            UsefulProgressTicks = usefulProgressTicks,
            JumpTicks = jumpTicks,
            ProductiveLanding = productiveLanding,
            LandedHigher = landedHigher,
            ObjectiveImproved = objectiveImproved,
            WastedJump = wastedJump,
            LocalLoop = localLoop,
            EndedNearUpwardLanding = endedNearUpwardLanding,
            EndGrounded = end.IsGrounded,
            BecameAirborne = becameAirborne,
            LandedAfterAirborne = landedAfterAirborne,
            HitWall = hitWall,
            Success = result.IsSuccess,
            TerminalReason = result.TerminalReason,
            TotalReward = totalReward,
        };
    }

    private static int ChooseAffordanceMoveDirection(MLBotObservation observation, AffordanceControllerProfile profile)
    {
        if (observation.Probes.TouchingLeftWall)
        {
            return 1;
        }

        if (observation.Probes.TouchingRightWall)
        {
            return -1;
        }

        var objectiveDirection = DirectionFromDelta(observation.Objective.RelativeX, profile.ObjectiveDeadzone);
        var affordance = observation.TerrainAffordance;
        if (affordance.HasBestUpwardLanding
            && affordance.BestUpwardLandingSurfaceDeltaY < -0.5f
            && (observation.Objective.RelativeY < -16f || affordance.BestUpwardLandingSurfaceDeltaY < -profile.UpwardDeltaThreshold))
        {
            var upwardDirection = Math.Sign(affordance.BestUpwardLandingDirection);
            if (upwardDirection != 0)
            {
                return upwardDirection;
            }
        }

        if (objectiveDirection != 0)
        {
            return objectiveDirection;
        }

        return Math.Sign(observation.FacingDirectionX);
    }

    private static bool ShouldAffordanceJump(MLBotObservation observation, int moveDirection, AffordanceControllerProfile profile)
    {
        if (!observation.IsGrounded)
        {
            return false;
        }

        var affordance = observation.TerrainAffordance;
        if (affordance.HasBestUpwardLanding && affordance.BestUpwardLandingSurfaceDeltaY < -0.5f)
        {
            var landingDirection = Math.Sign(affordance.BestUpwardLandingDirection);
            var landingGap = MathF.Abs(affordance.BestUpwardLandingRelativeX);
            if ((landingDirection == 0 || landingDirection == moveDirection)
                && landingGap <= profile.JumpWindow
                && affordance.BestUpwardLandingHeadroom >= 18f)
            {
                return true;
            }
        }

        if (moveDirection < 0 && (observation.Probes.TouchingLeftWall || observation.Probes.LeftFootObstacleDistance <= 8f))
        {
            return true;
        }

        if (moveDirection > 0 && (observation.Probes.TouchingRightWall || observation.Probes.RightFootObstacleDistance <= 8f))
        {
            return true;
        }

        return observation.Objective.RelativeY < -24f
            && ((moveDirection < 0 && observation.Probes.LeftGroundDistance <= 6f)
                || (moveDirection > 0 && observation.Probes.RightGroundDistance <= 6f));
    }

    private static int DirectionFromDelta(float delta, float deadzone)
    {
        if (delta < -deadzone)
        {
            return -1;
        }

        return delta > deadzone ? 1 : 0;
    }

    private static T[] AppendRepeated<T>(IReadOnlyList<T> values, T value, int count, int maxLength)
    {
        var length = Math.Min(maxLength, values.Count + count);
        var result = new T[length];
        for (var index = 0; index < values.Count && index < length; index += 1)
        {
            result[index] = values[index];
        }

        for (var index = values.Count; index < length; index += 1)
        {
            result[index] = value;
        }

        return result;
    }

    private static float ScoreOutcomeSample(MLBotOutcomeSample sample)
    {
        var moved = MathF.Abs(sample.DeltaX) + MathF.Abs(sample.DeltaY);
        var score = sample.TotalReward;
        score += sample.ObjectiveDistanceDelta * 0.55f;
        score += sample.MinObjectiveDistanceDelta * 0.35f;
        score += sample.MaxVerticalGain * 0.08f;
        score += sample.UpwardLandingProgress * 0.75f;
        score += sample.MaxDistanceFromStart * 0.06f;
        score += sample.UsefulProgressTicks * 3f;
        if (sample.EndedNearUpwardLanding)
        {
            score += 225f;
        }

        if (sample.Success)
        {
            score += 5000f;
        }

        if (sample.HitWall)
        {
            score -= 75f;
        }

        if (sample.HitWall && sample.UsefulProgressTicks <= 1)
        {
            score -= 175f;
        }

        score -= sample.WallContactTicks * 4f;
        score -= sample.NoProgressTicks * 3.5f;
        score -= sample.ObjectiveRegressionTicks * 8f;
        if (moved < 2f)
        {
            score -= 125f;
        }

        if (sample.MaxDistanceFromStart < 16f)
        {
            score -= 90f;
        }

        var actualTicks = Math.Max(1, sample.ActualTicks);
        if (sample.JumpTicks >= Math.Max(2, actualTicks / 3)
            && sample.MaxVerticalGain < 8f
            && sample.UpwardLandingProgress < 8f)
        {
            score -= 160f;
        }

        return score;
    }

    private static float ComputeUpwardLandingProgress(in MLBotObservation start, in MLBotObservation end)
    {
        var terrain = start.TerrainAffordance;
        if (!terrain.HasBestUpwardLanding)
        {
            return 0f;
        }

        var targetX = start.BotX + terrain.BestUpwardLandingRelativeX;
        var targetY = start.BotY + terrain.BestUpwardLandingRelativeY;
        var startDistance = Distance(start.BotX, start.BotY, targetX, targetY);
        var endDistance = Distance(end.BotX, end.BotY, targetX, targetY);
        return startDistance - endDistance;
    }

    private static bool IsNearBestUpwardLanding(in MLBotObservation start, in MLBotObservation end)
    {
        var terrain = start.TerrainAffordance;
        if (!terrain.HasBestUpwardLanding)
        {
            return false;
        }

        var targetX = start.BotX + terrain.BestUpwardLandingRelativeX;
        var targetY = start.BotY + terrain.BestUpwardLandingRelativeY;
        return Distance(end.BotX, end.BotY, targetX, targetY) <= 48f
            && end.IsGrounded
            && end.BotY <= start.BotY - 4f;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private sealed record OutcomeAnchor(string SourcePath, int SourceTick, MLBotObservation Observation);

    private sealed record SourceAnchor(string SourcePath, int SourceTick, MLBotObservation Observation);

    private sealed record SequenceCandidate(
        int[] MoveDirections,
        bool[] JumpSequence,
        MLBotOutcomeSample? Sample,
        float Score);

    private sealed record AffordanceControllerProfile(
        string Name,
        float JumpWindow,
        float ObjectiveDeadzone,
        int JumpHoldTicks,
        int InitialReleaseTicks,
        int JumpCooldownTicks)
    {
        public float UpwardDeltaThreshold { get; init; } = 6f;
    }

    private sealed record OutcomeActionTemplate(
        string Name,
        int MoveDirection,
        bool Jump,
        bool Crouch,
        int? MoveDirectionAfterJump = null,
        int? JumpStartTick = null,
        int? JumpDurationTicks = null,
        int? MoveDirectionSwitchTick = null,
        int? SecondJumpStartTick = null,
        int? SecondJumpDurationTicks = null)
    {
        private int[]? MoveSequence { get; init; }

        private bool[]? JumpSequenceOverride { get; init; }

        public static OutcomeActionTemplate FromSequence(string name, int[] moveDirections, bool[] jumpSequence)
        {
            return new OutcomeActionTemplate(name, moveDirections.Length == 0 ? 0 : moveDirections[0], jumpSequence.Any(value => value), false)
            {
                MoveSequence = moveDirections,
                JumpSequenceOverride = jumpSequence,
            };
        }

        public int MoveDirectionAt(int tick, int jumpHoldTicks)
        {
            if (MoveSequence is { Length: > 0 })
            {
                return MoveSequence[Math.Min(tick, MoveSequence.Length - 1)];
            }

            if (JumpStartTick.HasValue && tick < JumpStartTick.Value)
            {
                return MoveDirection;
            }

            var switchTick = MoveDirectionSwitchTick ?? Math.Max(1, jumpHoldTicks);
            if (MoveDirectionAfterJump.HasValue && tick >= switchTick)
            {
                return MoveDirectionAfterJump.Value;
            }

            return MoveDirection;
        }

        public bool JumpAt(int tick, int jumpHoldTicks)
        {
            if (JumpSequenceOverride is { Length: > 0 })
            {
                return JumpSequenceOverride[Math.Min(tick, JumpSequenceOverride.Length - 1)];
            }

            var jumpStartTick = Math.Max(0, JumpStartTick ?? 0);
            var durationTicks = Math.Max(1, JumpDurationTicks ?? jumpHoldTicks);
            if (!Jump)
            {
                return false;
            }

            if (tick >= jumpStartTick && tick < jumpStartTick + durationTicks)
            {
                return true;
            }

            if (!SecondJumpStartTick.HasValue)
            {
                return false;
            }

            var secondStartTick = Math.Max(0, SecondJumpStartTick.Value);
            var secondDurationTicks = Math.Max(1, SecondJumpDurationTicks ?? durationTicks);
            return tick >= secondStartTick && tick < secondStartTick + secondDurationTicks;
        }
    }
}

internal sealed class MLBotOutcomeDatasetDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public int HorizonTicks { get; set; }

    public int JumpHoldTicks { get; set; }

    public string[] SourceRollouts { get; set; } = [];

    public int AnchorCount { get; set; }

    public int ActionCount { get; set; }

    public MLBotOutcomeSample[] Samples { get; set; } = [];
}

internal sealed class MLBotCompactOutcomeDatasetDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public int HorizonTicks { get; set; }

    public int JumpHoldTicks { get; set; }

    public string[] SourceRollouts { get; set; } = [];

    public int AnchorCount { get; set; }

    public int ActionCount { get; set; }

    public MLBotCompactOutcomeAnchor[] Anchors { get; set; } = [];

    public MLBotCompactOutcomeSample[] Samples { get; set; } = [];
}

internal sealed class MLBotCompactOutcomeAnchor
{
    public int AnchorId { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public int SourceTick { get; set; }

    public MLBotObservation StartObservation { get; set; }
}

internal sealed class MLBotCompactOutcomeSample
{
    public int AnchorId { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public int MoveDirection { get; set; }

    public bool Jump { get; set; }

    public bool Crouch { get; set; }

    public int[] MoveDirections { get; set; } = [];

    public bool[] JumpSequence { get; set; } = [];

    public int HorizonTicks { get; set; }

    public int ActualTicks { get; set; }

    public float DeltaX { get; set; }

    public float DeltaY { get; set; }

    public float DeltaVelocityX { get; set; }

    public float DeltaVelocityY { get; set; }

    public float ObjectiveDistanceDelta { get; set; }

    public float MinObjectiveDistanceDelta { get; set; }

    public float MaxVerticalGain { get; set; }

    public float UpwardLandingProgress { get; set; }

    public float MaxDistanceFromStart { get; set; }

    public int WallContactTicks { get; set; }

    public int NoProgressTicks { get; set; }

    public int ObjectiveRegressionTicks { get; set; }

    public int UsefulProgressTicks { get; set; }

    public int JumpTicks { get; set; }

    public bool ProductiveLanding { get; set; }

    public bool LandedHigher { get; set; }

    public bool ObjectiveImproved { get; set; }

    public bool WastedJump { get; set; }

    public bool LocalLoop { get; set; }

    public bool EndedNearUpwardLanding { get; set; }

    public bool EndGrounded { get; set; }

    public bool BecameAirborne { get; set; }

    public bool LandedAfterAirborne { get; set; }

    public bool HitWall { get; set; }

    public bool Success { get; set; }

    public string TerminalReason { get; set; } = string.Empty;

    public float TotalReward { get; set; }
}

internal sealed class MLBotOutcomeSample
{
    public string SourcePath { get; set; } = string.Empty;

    public int SourceTick { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public int MoveDirection { get; set; }

    public bool Jump { get; set; }

    public bool Crouch { get; set; }

    public int[] MoveDirections { get; set; } = [];

    public bool[] JumpSequence { get; set; } = [];

    public int HorizonTicks { get; set; }

    public int ActualTicks { get; set; }

    public MLBotObservation StartObservation { get; set; }

    public MLBotObservation EndObservation { get; set; }

    public float DeltaX { get; set; }

    public float DeltaY { get; set; }

    public float DeltaVelocityX { get; set; }

    public float DeltaVelocityY { get; set; }

    public float ObjectiveDistanceDelta { get; set; }

    public float MinObjectiveDistanceDelta { get; set; }

    public float MaxVerticalGain { get; set; }

    public float UpwardLandingProgress { get; set; }

    public float MaxDistanceFromStart { get; set; }

    public int WallContactTicks { get; set; }

    public int NoProgressTicks { get; set; }

    public int ObjectiveRegressionTicks { get; set; }

    public int UsefulProgressTicks { get; set; }

    public int JumpTicks { get; set; }

    public bool ProductiveLanding { get; set; }

    public bool LandedHigher { get; set; }

    public bool ObjectiveImproved { get; set; }

    public bool WastedJump { get; set; }

    public bool LocalLoop { get; set; }

    public bool EndedNearUpwardLanding { get; set; }

    public bool EndGrounded { get; set; }

    public bool BecameAirborne { get; set; }

    public bool LandedAfterAirborne { get; set; }

    public bool HitWall { get; set; }

    public bool Success { get; set; }

    public string TerminalReason { get; set; } = string.Empty;

    public float TotalReward { get; set; }
}

internal sealed class MLBotOutcomeDatasetOptions
{
    public List<string> RolloutPaths { get; } = [];

    public List<string> RolloutDirectories { get; } = [];

    public string RolloutGlob { get; private set; } = "*.json";

    public string? OutputPath { get; private set; }

    public int HorizonTicks { get; private set; } = 30;

    public int JumpHoldTicks { get; private set; } = 8;

    public int AnchorStride { get; private set; } = 30;

    public int MaxAnchors { get; private set; }

    public int MaxSourceAnchors { get; private set; }

    public string AnchorSelection { get; private set; } = "file-order";

    public float MinObjectiveDistance { get; private set; }

    public float MaxObjectiveDistance { get; private set; }

    public MLBotTaskPhase? TaskPhaseFilter { get; private set; }

    public PlayerTeam? TeamFilter { get; private set; }

    public PlayerClass? ClassFilter { get; private set; }

    public bool? CarryingIntelFilter { get; private set; }

    public bool? IsGroundedFilter { get; private set; }

    public List<string> ActionNames { get; } = [];

    public List<PlayerClass> AugmentClasses { get; } = [];

    public List<float> XOffsets { get; } = [];

    public List<float> YOffsets { get; } = [];

    public List<float> VelocityXOffsets { get; } = [];

    public List<float> VelocityYOffsets { get; } = [];

    public bool Compact { get; private set; }

    public bool SequenceSearch { get; private set; }

    public bool RandomSequenceSearch { get; private set; }

    public bool AffordanceSequenceSearch { get; private set; }

    public int SequenceSearchBeamWidth { get; private set; } = 24;

    public int SequenceSearchTopK { get; private set; } = 4;

    public int SequenceSearchSegmentTicks { get; private set; } = 3;

    public int RandomSequenceCount { get; private set; } = 256;

    public int Seed { get; private set; } = 1337;

    public static MLBotOutcomeDatasetOptions Parse(string[] args)
    {
        var options = new MLBotOutcomeDatasetOptions();
        for (var index = 0; index < args.Length; index += 1)
        {
            var arg = args[index];
            if (string.Equals(arg, "--carrying-intel-only", StringComparison.OrdinalIgnoreCase))
            {
                options.CarryingIntelFilter = true;
                continue;
            }

            if (string.Equals(arg, "--exclude-carrying-intel", StringComparison.OrdinalIgnoreCase))
            {
                options.CarryingIntelFilter = false;
                continue;
            }

            if (string.Equals(arg, "--grounded-only", StringComparison.OrdinalIgnoreCase))
            {
                options.IsGroundedFilter = true;
                continue;
            }

            if (string.Equals(arg, "--airborne-only", StringComparison.OrdinalIgnoreCase))
            {
                options.IsGroundedFilter = false;
                continue;
            }

            if (string.Equals(arg, "--compact", StringComparison.OrdinalIgnoreCase))
            {
                options.Compact = true;
                continue;
            }

            if (string.Equals(arg, "--sequence-search", StringComparison.OrdinalIgnoreCase))
            {
                options.SequenceSearch = true;
                continue;
            }

            if (string.Equals(arg, "--random-sequence-search", StringComparison.OrdinalIgnoreCase))
            {
                options.RandomSequenceSearch = true;
                continue;
            }

            if (string.Equals(arg, "--affordance-sequence-search", StringComparison.OrdinalIgnoreCase))
            {
                options.AffordanceSequenceSearch = true;
                continue;
            }

            if (index + 1 >= args.Length)
            {
                continue;
            }

            var value = args[index + 1];
            switch (arg)
            {
                case "--rollout-path":
                    options.RolloutPaths.Add(value);
                    index += 1;
                    break;
                case "--rollout-dir":
                    options.RolloutDirectories.Add(value);
                    index += 1;
                    break;
                case "--rollout-glob":
                    options.RolloutGlob = value;
                    index += 1;
                    break;
                case "--out":
                    options.OutputPath = value;
                    index += 1;
                    break;
                case "--horizon" when int.TryParse(value, out var horizon):
                    options.HorizonTicks = Math.Max(1, horizon);
                    index += 1;
                    break;
                case "--jump-hold-ticks" when int.TryParse(value, out var jumpHoldTicks):
                    options.JumpHoldTicks = Math.Max(0, jumpHoldTicks);
                    index += 1;
                    break;
                case "--anchor-stride" when int.TryParse(value, out var stride):
                    options.AnchorStride = Math.Max(1, stride);
                    index += 1;
                    break;
                case "--max-anchors" when int.TryParse(value, out var maxAnchors):
                    options.MaxAnchors = Math.Max(0, maxAnchors);
                    index += 1;
                    break;
                case "--max-source-anchors" when int.TryParse(value, out var maxSourceAnchors):
                    options.MaxSourceAnchors = Math.Max(0, maxSourceAnchors);
                    index += 1;
                    break;
                case "--anchor-selection":
                    options.AnchorSelection = value;
                    index += 1;
                    break;
                case "--min-objective-distance" when float.TryParse(value, out var minObjectiveDistance):
                    options.MinObjectiveDistance = Math.Max(0f, minObjectiveDistance);
                    index += 1;
                    break;
                case "--max-objective-distance" when float.TryParse(value, out var maxObjectiveDistance):
                    options.MaxObjectiveDistance = Math.Max(0f, maxObjectiveDistance);
                    index += 1;
                    break;
                case "--task" when Enum.TryParse<MLBotTaskPhase>(value, ignoreCase: true, out var taskPhase):
                    options.TaskPhaseFilter = taskPhase;
                    index += 1;
                    break;
                case "--team" when Enum.TryParse<PlayerTeam>(value, ignoreCase: true, out var team):
                    options.TeamFilter = team;
                    index += 1;
                    break;
                case "--class" when Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var classId):
                    options.ClassFilter = classId;
                    index += 1;
                    break;
                case "--augment-class" when Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var augmentClass):
                    options.AugmentClasses.Add(augmentClass);
                    index += 1;
                    break;
                case "--x-offset":
                    AddFloatValues(options.XOffsets, value);
                    index += 1;
                    break;
                case "--y-offset":
                    AddFloatValues(options.YOffsets, value);
                    index += 1;
                    break;
                case "--vx-offset":
                    AddFloatValues(options.VelocityXOffsets, value);
                    index += 1;
                    break;
                case "--vy-offset":
                    AddFloatValues(options.VelocityYOffsets, value);
                    index += 1;
                    break;
                case "--action":
                    options.ActionNames.Add(value);
                    index += 1;
                    break;
                case "--sequence-beam-width" when int.TryParse(value, out var sequenceBeamWidth):
                    options.SequenceSearchBeamWidth = Math.Max(1, sequenceBeamWidth);
                    index += 1;
                    break;
                case "--sequence-top-k" when int.TryParse(value, out var sequenceTopK):
                    options.SequenceSearchTopK = Math.Max(1, sequenceTopK);
                    index += 1;
                    break;
                case "--sequence-segment-ticks" when int.TryParse(value, out var sequenceSegmentTicks):
                    options.SequenceSearchSegmentTicks = Math.Max(1, sequenceSegmentTicks);
                    index += 1;
                    break;
                case "--random-sequence-count" when int.TryParse(value, out var randomSequenceCount):
                    options.RandomSequenceCount = Math.Max(1, randomSequenceCount);
                    index += 1;
                    break;
                case "--seed" when int.TryParse(value, out var seed):
                    options.Seed = seed;
                    index += 1;
                    break;
            }
        }

        return options;
    }

    private static void AddFloatValues(List<float> values, string rawValue)
    {
        foreach (var part in rawValue.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                values.Add(parsed);
            }
        }
    }

    public string[] DiscoverRolloutPaths()
    {
        var paths = new List<string>(RolloutPaths);
        foreach (var directory in RolloutDirectories.Where(Directory.Exists))
        {
            paths.AddRange(Directory.EnumerateFiles(directory, RolloutGlob, SearchOption.AllDirectories));
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
