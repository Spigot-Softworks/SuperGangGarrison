using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core.BotBrain;

public sealed record TopologyLocalMotionLabOptions(
    string RepoRoot,
    string MapName,
    int AreaIndex,
    PlayerTeam Team,
    PlayerClass ClassId,
    string CaseName,
    TopologyLocalMotionObjectiveKind ObjectiveKind,
    int Ticks,
    int ReportEveryTicks,
    float? StartX,
    float? StartBottom,
    float? TargetX,
    float? TargetBottom,
    IReadOnlyList<float> XOffsets,
    IReadOnlyList<float> BottomOffsets,
    IReadOnlyList<float> HorizontalSpeeds,
    IReadOnlyList<float> VerticalSpeeds,
    string ArtifactMode,
    string ArtifactsDirectory)
{
    public static TopologyLocalMotionLabOptions FromRawOptions(
        IReadOnlyDictionary<string, string> rawOptions,
        string repoRoot)
    {
        var mapName = GetString(rawOptions, "map", "Truefort");
        var area = GetInt(rawOptions, "area", 1);
        var team = GetEnum(rawOptions, "team", PlayerTeam.Red);
        var classId = GetEnum(rawOptions, "class", PlayerClass.Pyro);
        var caseName = GetString(rawOptions, "case", "wall-pocket");
        var objectiveKind = ResolveObjectiveKind(rawOptions, caseName);
        var ticks = GetInt(
            rawOptions,
            "ticks",
            objectiveKind == TopologyLocalMotionObjectiveKind.CaptureIntel
                ? 5200
                : caseName.Equals("spawn", StringComparison.OrdinalIgnoreCase)
                    ? 2400
                    : 360);
        var artifactDirectory = GetString(
            rawOptions,
            "artifacts-dir",
            Path.Combine(
                repoRoot,
                "artifacts",
                "local-navigation-rescue",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)));

        return new TopologyLocalMotionLabOptions(
            repoRoot,
            mapName,
            area,
            team,
            classId,
            caseName,
            objectiveKind,
            ticks,
            GetInt(rawOptions, "report-every", 60),
            TryGetFloat(rawOptions, "start-x"),
            TryGetFloat(rawOptions, "start-bottom"),
            TryGetFloat(rawOptions, "target-x"),
            TryGetFloat(rawOptions, "target-bottom"),
            GetFloatList(rawOptions, "validation-x-offsets", caseName.Equals("spawn", StringComparison.OrdinalIgnoreCase) ? [0f] : [-16f, 0f, 16f]),
            GetFloatList(rawOptions, "validation-bottom-offsets", caseName.Equals("spawn", StringComparison.OrdinalIgnoreCase) ? [0f] : [-4f, 0f, 4f]),
            GetFloatList(rawOptions, "validation-horizontal-speeds", caseName.Equals("spawn", StringComparison.OrdinalIgnoreCase) ? [0f] : [-40f, 0f, 40f]),
            GetFloatList(rawOptions, "validation-vertical-speeds", [0f]),
            GetString(rawOptions, "artifact-mode", "all"),
            artifactDirectory);
    }

    private static TopologyLocalMotionObjectiveKind ResolveObjectiveKind(
        IReadOnlyDictionary<string, string> options,
        string caseName)
    {
        if (options.TryGetValue("objective", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            if (value.Equals("capture", StringComparison.OrdinalIgnoreCase)
                || value.Equals("capture-intel", StringComparison.OrdinalIgnoreCase)
                || value.Equals("roundtrip", StringComparison.OrdinalIgnoreCase)
                || value.Equals("round-trip", StringComparison.OrdinalIgnoreCase))
            {
                return TopologyLocalMotionObjectiveKind.CaptureIntel;
            }

            if (value.Equals("reach", StringComparison.OrdinalIgnoreCase)
                || value.Equals("reach-target", StringComparison.OrdinalIgnoreCase))
            {
                return TopologyLocalMotionObjectiveKind.ReachTarget;
            }

            if (Enum.TryParse<TopologyLocalMotionObjectiveKind>(value, ignoreCase: true, out var parsed))
            {
                return parsed;
            }
        }

        return caseName.Contains("capture", StringComparison.OrdinalIgnoreCase)
            || caseName.Contains("roundtrip", StringComparison.OrdinalIgnoreCase)
            || caseName.Contains("round-trip", StringComparison.OrdinalIgnoreCase)
                ? TopologyLocalMotionObjectiveKind.CaptureIntel
                : TopologyLocalMotionObjectiveKind.ReachTarget;
    }

    private static string GetString(IReadOnlyDictionary<string, string> options, string key, string fallback) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int GetInt(IReadOnlyDictionary<string, string> options, string key, int fallback) =>
        options.TryGetValue(key, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static float? TryGetFloat(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value)
        && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static TEnum GetEnum<TEnum>(IReadOnlyDictionary<string, string> options, string key, TEnum fallback)
        where TEnum : struct =>
        options.TryGetValue(key, out var value) && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;

    private static IReadOnlyList<float> GetFloatList(
        IReadOnlyDictionary<string, string> options,
        string key,
        IReadOnlyList<float> fallback)
    {
        if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var values = new List<float>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                values.Add(parsed);
            }
        }

        return values.Count == 0 ? fallback : values;
    }
}

public enum TopologyLocalMotionObjectiveKind
{
    ReachTarget,
    CaptureIntel,
}

public sealed record TopologyLocalMotionLabSummary(
    string Command,
    string ArtifactDirectory,
    int Cases,
    int Passed,
    int Failed,
    IReadOnlyList<TopologyLocalMotionLabCaseSummary> CaseSummaries);

public sealed record TopologyLocalMotionLabCaseSummary(
    int CaseIndex,
    string Scenario,
    bool Passed,
    string FailureReason,
    int Ticks,
    float StartX,
    float StartBottom,
    float TargetX,
    float TargetBottom,
    float FinalX,
    float FinalBottom,
    bool Reached,
    bool PickedUpIntel,
    bool Captured,
    int PickupTick,
    int ScoreTick,
    string ObjectiveKind,
    bool InitialDirectObstructed,
    bool HadObstructionChangingDecision,
    bool BlindObstructedPressDetected,
    float BestForwardClearance,
    string ReportPath,
    string OverlayPath);

public static class TopologyLocalMotionLab
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static TopologyLocalMotionLabSummary Run(TopologyLocalMotionLabOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ContentRoot.Initialize(Path.Combine(options.RepoRoot, "Core", "Content"));
        Directory.CreateDirectory(options.ArtifactsDirectory);

        var summaries = new List<TopologyLocalMotionLabCaseSummary>();
        var caseIndex = 0;
        foreach (var xOffset in options.XOffsets)
        {
            foreach (var bottomOffset in options.BottomOffsets)
            {
                foreach (var horizontalSpeed in options.HorizontalSpeeds)
                {
                    foreach (var verticalSpeed in options.VerticalSpeeds)
                    {
                        summaries.Add(RunCase(
                            options,
                            caseIndex,
                            xOffset,
                            bottomOffset,
                            horizontalSpeed,
                            verticalSpeed));
                        caseIndex += 1;
                    }
                }
            }
        }

        var command =
            $"dotnet run --project BotBrain.Tools/OpenGarrison.BotBrain.Tools.csproj -- --topology-local-motion-lab true --map {options.MapName} --area {options.AreaIndex} --team {options.Team} --class {options.ClassId}";
        return new TopologyLocalMotionLabSummary(
            command,
            options.ArtifactsDirectory,
            summaries.Count,
            summaries.Count(static summary => summary.Passed),
            summaries.Count(static summary => !summary.Passed),
            summaries);
    }

    private static TopologyLocalMotionLabCaseSummary RunCase(
        TopologyLocalMotionLabOptions options,
        int caseIndex,
        float xOffset,
        float bottomOffset,
        float horizontalSpeed,
        float verticalSpeed)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });

        if (!world.TryLoadLevel(options.MapName, options.AreaIndex, preservePlayerStats: false))
        {
            return CreateLoadFailure(options, caseIndex, "load_failed");
        }

        var enemyTeam = options.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyIntel = world.Level.GetIntelBase(enemyTeam);
        var ownIntel = world.Level.GetIntelBase(options.Team);
        const byte botSlot = 2;
        world.PrepareLocalPlayerJoin();
        if (!world.TryPrepareNetworkPlayerJoin(botSlot)
            || !world.TrySetNetworkPlayerTeam(botSlot, options.Team)
            || !world.TryApplyNetworkPlayerClassSelection(botSlot, options.ClassId)
            || !world.TryGetNetworkPlayer(botSlot, out var bot))
        {
            return CreateLoadFailure(options, caseIndex, "spawn_failed");
        }

        var baseStart = ResolveStart(options, world.Level, bot);
        var captureObjective = options.ObjectiveKind == TopologyLocalMotionObjectiveKind.CaptureIntel;
        TopologyLocalMotionTarget target;
        TopologyLocalMotionTarget? pickupTarget = null;
        TopologyLocalMotionTarget? returnTarget = null;
        if (captureObjective)
        {
            if (!enemyIntel.HasValue || !ownIntel.HasValue)
            {
                return CreateLoadFailure(options, caseIndex, "intel_marker_missing");
            }

            if (!TryResolveIntelContactTarget(world.Level, options.Team, bot, enemyIntel.Value, "enemyIntelPickup", out var resolvedPickupTarget)
                || !TryResolveIntelContactTarget(world.Level, options.Team, bot, ownIntel.Value, "ownIntelCapture", out var resolvedReturnTarget))
            {
                return CreateLoadFailure(options, caseIndex, "intel_contact_target_missing");
            }

            pickupTarget = resolvedPickupTarget;
            returnTarget = resolvedReturnTarget;
            target = resolvedPickupTarget;
        }
        else if (!TryResolveTarget(options, world.Level, options.Team, bot, enemyIntel, out target))
        {
            return CreateLoadFailure(options, caseIndex, "target_missing");
        }
        var startX = baseStart.X + xOffset;
        var startBottom = baseStart.Bottom + bottomOffset;
        var startY = startBottom - bot.CollisionBottomOffset;
        bot.Spawn(options.Team, startX, startY);
        bot.TeleportTo(startX, startY);
        bot.ResolveBlockingOverlap(world.Level, options.Team);
        bot.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: bot.MaxAirJumps, facingDirectionX: target.X >= startX ? 1f : -1f);
        if (horizontalSpeed != 0f || verticalSpeed != 0f)
        {
            bot.AddImpulse(horizontalSpeed, verticalSpeed);
        }

        var scenarioName =
            $"{options.CaseName}.x{xOffset.ToString("0", CultureInfo.InvariantCulture)}.b{bottomOffset.ToString("0", CultureInfo.InvariantCulture)}." +
            $"hs{horizontalSpeed.ToString("0", CultureInfo.InvariantCulture)}.vs{verticalSpeed.ToString("0", CultureInfo.InvariantCulture)}";
        var controller = new TopologyLocalMotionController();
        var report = new TopologyLocalMotionCaseReport(
            Scenario: scenarioName,
            MapName: options.MapName,
            AreaIndex: options.AreaIndex,
            Team: options.Team.ToString(),
            ClassId: options.ClassId.ToString(),
            StartState: TopologyLocalMotionState.From(bot),
            Target: target,
            XOffset: xOffset,
            BottomOffset: bottomOffset,
            HorizontalSpeedOffset: horizontalSpeed,
            VerticalSpeedOffset: verticalSpeed,
            Decisions: [],
            Samples: [])
        {
            ObjectiveKind = options.ObjectiveKind.ToString(),
            PickupTarget = pickupTarget,
            ReturnTarget = returnTarget,
        };

        var reportDecisions = new List<TopologyLocalMotionDecisionReport>();
        var samples = new List<TopologyLocalMotionTickSample>
        {
            TopologyLocalMotionTickSample.From(0, bot, default, world),
        };
        var previousInput = default(PlayerInputSnapshot);
        var reached = false;
        var pickupTick = -1;
        var scoreTick = -1;
        var captured = false;
        var noPlanTick = -1;
        var blindPressDetected = false;
        var hadObstructionChangingDecision = false;
        var initialObstruction = TopologyLocalMotionController.AnalyzeObstruction(world.Level, bot, options.Team, target);
        var bestForwardClearance = initialObstruction.ForwardClearance;
        var lastDecision = default(TopologyLocalMotionDecisionReport);
        var activeTarget = target;
        var initialRedCaps = world.RedCaps;
        var initialBlueCaps = world.BlueCaps;

        for (var tick = 1; tick <= options.Ticks; tick += 1)
        {
            if (captureObjective && bot.IsCarryingIntel && returnTarget is not null && activeTarget != returnTarget)
            {
                activeTarget = returnTarget;
                target = activeTarget;
                controller = new TopologyLocalMotionController();
            }

            if (!controller.TryResolve(world, bot, activeTarget, tick, out var input, out var decision))
            {
                noPlanTick = tick;
                input = default;
            }

            if (decision is not null)
            {
                reportDecisions.Add(decision);
                lastDecision = decision;
                hadObstructionChangingDecision |= decision.SelectedMacro is not null
                    && decision.SelectedMacro.ObstructionChanged;
                blindPressDetected |= decision.SelectedMacro is not null
                    && decision.StartObstruction.DirectPathObstructed
                    && decision.SelectedMacro.PressesSameObstruction;
            }

            if (noPlanTick == tick)
            {
                break;
            }

            if (!world.TrySetNetworkPlayerInput(botSlot, input))
            {
                noPlanTick = tick;
                break;
            }

            world.AdvanceOneTick();
            previousInput = input;
            if (captureObjective && pickupTick < 0 && bot.IsCarryingIntel)
            {
                pickupTick = tick;
                activeTarget = returnTarget ?? activeTarget;
                target = activeTarget;
                controller = new TopologyLocalMotionController();
                samples.Add(TopologyLocalMotionTickSample.From(tick, bot, input, world));
            }

            if (captureObjective
                && pickupTick >= 0
                && (world.RedCaps > initialRedCaps || world.BlueCaps > initialBlueCaps || bot.Caps > 0))
            {
                scoreTick = tick;
                captured = true;
                reached = true;
                samples.Add(TopologyLocalMotionTickSample.From(tick, bot, input, world));
                break;
            }

            var obstruction = TopologyLocalMotionController.AnalyzeObstruction(world.Level, bot, options.Team, activeTarget);
            bestForwardClearance = MathF.Max(bestForwardClearance, obstruction.ForwardClearance);
            if (tick % Math.Max(1, options.ReportEveryTicks) == 0 || tick == options.Ticks)
            {
                samples.Add(TopologyLocalMotionTickSample.From(tick, bot, input, world));
            }

            if (!captureObjective && HasReached(bot, activeTarget))
            {
                reached = true;
                samples.Add(TopologyLocalMotionTickSample.From(tick, bot, input, world));
                break;
            }
        }

        var finalObstruction = TopologyLocalMotionController.AnalyzeObstruction(world.Level, bot, options.Team, activeTarget);
        var pass = captureObjective
            ? captured
            : reached
            || (initialObstruction.DirectPathObstructed
                && hadObstructionChangingDecision
                && !blindPressDetected
                && (bestForwardClearance >= initialObstruction.ForwardClearance + 32f
                    || !finalObstruction.SameObstructionAs(initialObstruction)
                    || MathF.Abs(bot.Bottom - startBottom) >= 42f));
        var failureReason = pass
            ? string.Empty
            : captureObjective
                ? noPlanTick > 0
                    ? $"no_plan_tick:{noPlanTick}"
                    : pickupTick < 0
                        ? "timeout_without_pickup"
                        : "timeout_without_capture"
                : noPlanTick > 0
                    ? $"no_plan_tick:{noPlanTick}"
                    : blindPressDetected
                        ? "blind_obstructed_press"
                        : initialObstruction.DirectPathObstructed && !hadObstructionChangingDecision
                            ? "no_obstruction_changing_decision"
                            : "timeout_without_reachability_change";

        report = report with
        {
            Target = activeTarget,
            FinalState = TopologyLocalMotionState.From(bot),
            FinalObstruction = finalObstruction,
            Decisions = reportDecisions,
            Samples = samples,
            Passed = pass,
            FailureReason = failureReason,
            Reached = reached,
            PickupTick = pickupTick,
            ScoreTick = scoreTick,
            PickedUpIntel = pickupTick >= 0,
            Captured = captured,
            RedCaps = world.RedCaps,
            BlueCaps = world.BlueCaps,
            InitialDirectObstructed = initialObstruction.DirectPathObstructed,
            HadObstructionChangingDecision = hadObstructionChangingDecision,
            BlindObstructedPressDetected = blindPressDetected,
            BestForwardClearance = bestForwardClearance,
        };

        var emitArtifacts = ShouldEmitArtifacts(options.ArtifactMode, pass);
        var reportPath = emitArtifacts
            ? Path.Combine(options.ArtifactsDirectory, $"case_{caseIndex:000}_{SanitizeFileName(scenarioName)}.json")
            : string.Empty;
        var overlayPath = emitArtifacts
            ? Path.Combine(options.ArtifactsDirectory, $"case_{caseIndex:000}_{SanitizeFileName(scenarioName)}.png")
            : string.Empty;
        if (emitArtifacts)
        {
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, ReportJsonOptions));
            TopologyLocalMotionOverlayRenderer.Render(world.Level, report, lastDecision, overlayPath);
        }

        return new TopologyLocalMotionLabCaseSummary(
            caseIndex,
            scenarioName,
            pass,
            failureReason,
            samples.Count == 0 ? 0 : samples[^1].Tick,
            report.StartState.X,
            report.StartState.Bottom,
            target.X,
            target.Bottom,
            bot.X,
            bot.Bottom,
            reached,
            pickupTick >= 0,
            captured,
            pickupTick,
            scoreTick,
            options.ObjectiveKind.ToString(),
            initialObstruction.DirectPathObstructed,
            hadObstructionChangingDecision,
            blindPressDetected,
            bestForwardClearance,
            reportPath,
            overlayPath);
    }

    private static bool ShouldEmitArtifacts(string mode, bool passed) =>
        mode.Equals("all", StringComparison.OrdinalIgnoreCase)
        || (mode.Equals("failures", StringComparison.OrdinalIgnoreCase) && !passed)
        || (mode.Equals("failed", StringComparison.OrdinalIgnoreCase) && !passed);

    private static TopologyLocalMotionLabCaseSummary CreateLoadFailure(
        TopologyLocalMotionLabOptions options,
        int caseIndex,
        string reason) =>
        new(
            caseIndex,
            options.CaseName,
            Passed: false,
            reason,
            Ticks: 0,
            StartX: 0f,
            StartBottom: 0f,
            TargetX: 0f,
            TargetBottom: 0f,
            FinalX: 0f,
            FinalBottom: 0f,
            Reached: false,
            PickedUpIntel: false,
            Captured: false,
            PickupTick: -1,
            ScoreTick: -1,
            ObjectiveKind: options.ObjectiveKind.ToString(),
            InitialDirectObstructed: false,
            HadObstructionChangingDecision: false,
            BlindObstructedPressDetected: false,
            BestForwardClearance: 0f,
            ReportPath: string.Empty,
            OverlayPath: string.Empty);

    private static bool TryResolveTarget(
        TopologyLocalMotionLabOptions options,
        SimpleLevel level,
        PlayerTeam team,
        PlayerEntity bot,
        IntelBaseMarker? enemyIntel,
        out TopologyLocalMotionTarget target)
    {
        if (options.TargetX.HasValue && options.TargetBottom.HasValue)
        {
            target = new TopologyLocalMotionTarget(options.TargetX.Value, options.TargetBottom.Value, "explicit");
            return true;
        }

        if (enemyIntel.HasValue)
        {
            var rawBottom = enemyIntel.Value.Y + bot.CollisionBottomOffset;
            if (TryResolveNearestStandableTarget(level, team, bot, enemyIntel.Value.X, rawBottom, out var standableX, out var standableBottom))
            {
                target = new TopologyLocalMotionTarget(standableX, standableBottom, "enemyIntelStandable");
                return true;
            }

            target = new TopologyLocalMotionTarget(enemyIntel.Value.X, rawBottom, "enemyIntel");
            return true;
        }

        target = new TopologyLocalMotionTarget(0f, 0f, "missing");
        return false;
    }

    private static bool TryResolveIntelContactTarget(
        SimpleLevel level,
        PlayerTeam team,
        PlayerEntity bot,
        IntelBaseMarker intel,
        string label,
        out TopologyLocalMotionTarget target)
    {
        const float intelMarkerSize = 24f;
        var bestScore = float.PositiveInfinity;
        var bestX = intel.X;
        var bestBottom = intel.Y + bot.CollisionBottomOffset;
        for (var radius = 0f; radius <= 132f; radius += 6f)
        {
            for (var x = intel.X - radius; x <= intel.X + radius + 0.01f; x += 6f)
            {
                if (radius > 0f && MathF.Abs(MathF.Abs(x - intel.X) - radius) > 0.01f)
                {
                    continue;
                }

                for (var bottom = intel.Y + bot.CollisionBottomOffset - 104f;
                     bottom <= intel.Y + bot.CollisionBottomOffset + 104f;
                     bottom += 2f)
                {
                    var y = bottom - bot.CollisionBottomOffset;
                    if (!bot.CanOccupy(level, team, x, y)
                        || bot.CanOccupy(level, team, x, y + 2f)
                        || !WouldIntersectMarkerAt(bot, x, bottom, intel.X, intel.Y, intelMarkerSize, intelMarkerSize))
                    {
                        continue;
                    }

                    var score = MathF.Abs(x - intel.X) + (MathF.Abs(bottom - (intel.Y + bot.CollisionBottomOffset)) * 2f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestX = x;
                    bestBottom = bottom;
                }
            }

            if (float.IsFinite(bestScore))
            {
                target = new TopologyLocalMotionTarget(bestX, bestBottom, label);
                return true;
            }
        }

        target = new TopologyLocalMotionTarget(0f, 0f, $"{label}:missing");
        return false;
    }

    private static bool WouldIntersectMarkerAt(
        PlayerEntity bot,
        float x,
        float bottom,
        float markerX,
        float markerY,
        float markerWidth,
        float markerHeight)
    {
        var y = bottom - bot.CollisionBottomOffset;
        bot.GetCollisionBoundsAt(x, y, out var left, out var top, out var right, out var playerBottom);
        var markerLeft = markerX - (markerWidth / 2f);
        var markerRight = markerX + (markerWidth / 2f);
        var markerTop = markerY - (markerHeight / 2f);
        var markerBottom = markerY + (markerHeight / 2f);
        return left < markerRight
            && right > markerLeft
            && top < markerBottom
            && playerBottom > markerTop;
    }

    private static bool TryResolveNearestStandableTarget(
        SimpleLevel level,
        PlayerTeam team,
        PlayerEntity bot,
        float rawX,
        float rawBottom,
        out float standableX,
        out float standableBottom)
    {
        standableX = rawX;
        standableBottom = rawBottom;
        var bestScore = float.PositiveInfinity;
        for (var radius = 0f; radius <= 96f; radius += 6f)
        {
            for (var x = rawX - radius; x <= rawX + radius + 0.01f; x += 6f)
            {
                if (MathF.Abs(MathF.Abs(x - rawX) - radius) > 0.01f && radius > 0f)
                {
                    continue;
                }

                for (var bottom = rawBottom - 72f; bottom <= rawBottom + 72f; bottom += 2f)
                {
                    var y = bottom - bot.CollisionBottomOffset;
                    if (!bot.CanOccupy(level, team, x, y) || bot.CanOccupy(level, team, x, y + 2f))
                    {
                        continue;
                    }

                    var score = MathF.Abs(x - rawX) + (MathF.Abs(bottom - rawBottom) * 2f);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    standableX = x;
                    standableBottom = bottom;
                }
            }

            if (float.IsFinite(bestScore))
            {
                return true;
            }
        }

        return false;
    }

    private static (float X, float Bottom) ResolveStart(
        TopologyLocalMotionLabOptions options,
        SimpleLevel level,
        PlayerEntity bot)
    {
        if (options.StartX.HasValue && options.StartBottom.HasValue)
        {
            return (options.StartX.Value, options.StartBottom.Value);
        }

        var spawn = level.GetSpawn(options.Team, 0);
        bot.Spawn(options.Team, spawn.X, spawn.Y);
        return (bot.X, bot.Bottom);
    }

    private static bool HasReached(PlayerEntity bot, TopologyLocalMotionTarget target) =>
        MathF.Abs(bot.X - target.X) <= 44f
        && MathF.Abs(bot.Bottom - target.Bottom) <= 36f;

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Replace(':', '_');
    }
}

public sealed class TopologyLocalMotionController
{
    private const int ProbeEntityId = -902_100;
    private const int MinimumCommitTicks = 8;
    private const int ReplanIntervalTicks = 8;
    private const int MaxCandidateCount = 22;
    private const int MaxDecisionSimTicks = 960;
    private const float ScanRadiusX = 560f;
    private const float ScanAbove = 260f;
    private const float ScanBelow = 320f;
    private const float SurfaceStepX = 8f;
    private const float SurfaceStepBottom = 2f;
    private const float StandTolerance = 16f;
    private const float ObstructionProbeDistance = 220f;
    private const float LowerBasinLookaheadDistance = 520f;
    private const float ObstructionProbeStep = 6f;
    private const float UpwardReverseEscapeMinGain = 48f;
    private const int LowerHandoffContinuationDepth = 4;
    private const int BoundedSuccessorContinuationDepth = 3;
    private const int BoundedSuccessorStepSimTicks = 520;
    private const int FailedObstructionMemoryTicks = 240;

    private LocalMacroPlan _activePlan;
    private int _activeUntilTick;
    private int _lastDecisionTick = int.MinValue / 2;
    private TopologyLocalMotionObstruction? _failedObstruction;
    private int _failedObstructionUntilTick;

    public bool TryResolve(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        int tick,
        out PlayerInputSnapshot input,
        out TopologyLocalMotionDecisionReport? decision)
    {
        input = default;
        decision = null;
        if (!self.IsAlive)
        {
            return false;
        }

        if (_activePlan.HasPlan && tick <= _activeUntilTick)
        {
            input = BuildInput(self, target, _activePlan, tick - _activePlan.StartTick);
            return true;
        }

        if (tick - _lastDecisionTick < ReplanIntervalTicks)
        {
            return false;
        }

        _lastDecisionTick = tick;
        var stopwatch = Stopwatch.StartNew();
        var startState = TopologyLocalMotionState.From(self);
        var startObstruction = AnalyzeObstruction(world.Level, self, self.Team, target);
        var hasRecentFailedObstruction = _failedObstructionUntilTick >= tick;
        var includeReverseFrontiers = IsNearUpperWallEscapeState(self, startObstruction)
            || hasRecentFailedObstruction;
        var scan = BuildScan(
            world.Level,
            self,
            self.Team,
            target,
            includeReverseFrontiers,
            includeTargetSideLowerHandoffs: startObstruction.DirectPathObstructed);
        var avoidedObstruction = _failedObstructionUntilTick >= tick ? _failedObstruction : null;
        var candidates = EnumerateCandidates(
                self,
                target,
                scan,
                startObstruction,
                prioritizeLowerOverlapDrops: hasRecentFailedObstruction)
            .Take(MaxCandidateCount)
            .ToArray();
        var evaluations = new List<TopologyLocalMotionMacroReport>(candidates.Length);
        var simTicks = 0;
        TopologyLocalMotionMacroReport? best = null;
        foreach (var candidate in candidates)
        {
            if (simTicks + candidate.DurationTicks > MaxDecisionSimTicks)
            {
                break;
            }

            simTicks += candidate.DurationTicks;
            var evaluation = EvaluateCandidate(world, self, target, candidate, scan, startObstruction, avoidedObstruction);
            evaluations.Add(evaluation);
            if (evaluation.Accepted && (best is null || evaluation.Score > best.Score))
            {
                best = evaluation;
            }
        }

        var selected = SelectMacro(evaluations, best, startObstruction, startState);
        if (selected is null && !startObstruction.DirectPathObstructed && !self.IsGrounded)
        {
            selected = SelectLastResortClearAirborneLanding(evaluations);
        }
        if (selected is null && !startObstruction.DirectPathObstructed)
        {
            selected = SelectLastResortClearStartCommitProgress(evaluations, startObstruction);
        }

        var usedTargetSideRescue = false;
        if (selected is null && !includeReverseFrontiers && startObstruction.DirectPathObstructed)
        {
            usedTargetSideRescue = true;
            scan = BuildScan(
                world.Level,
                self,
                self.Team,
                target,
                includeObstructedReverseFrontiers: true,
                includeTargetSideLowerHandoffs: true);
            candidates = EnumerateCandidates(
                    self,
                    target,
                    scan,
                    startObstruction,
                    preferTargetSideFrontiers: true,
                    prioritizeLowerOverlapDrops: hasRecentFailedObstruction)
                .Take(MaxCandidateCount)
                .ToArray();
            evaluations = new List<TopologyLocalMotionMacroReport>(candidates.Length);
            simTicks = 0;
            best = null;
            foreach (var candidate in candidates)
            {
                if (simTicks + candidate.DurationTicks > MaxDecisionSimTicks)
                {
                    break;
                }

                simTicks += candidate.DurationTicks;
                var evaluation = EvaluateCandidate(world, self, target, candidate, scan, startObstruction, avoidedObstruction);
                evaluations.Add(evaluation);
                if (evaluation.Accepted && (best is null || evaluation.Score > best.Score))
                {
                    best = evaluation;
                }
            }

            selected = SelectMacro(evaluations, best, startObstruction, startState);
        }

        if (selected is null && startObstruction.DirectPathObstructed && !usedTargetSideRescue)
        {
            usedTargetSideRescue = true;
            scan = BuildScan(
                world.Level,
                self,
                self.Team,
                target,
                includeObstructedReverseFrontiers: true,
                includeTargetSideLowerHandoffs: true);
            candidates = EnumerateCandidates(
                    self,
                    target,
                    scan,
                    startObstruction,
                    preferTargetSideFrontiers: true,
                    prioritizeLowerOverlapDrops: hasRecentFailedObstruction)
                .Take(MaxCandidateCount)
                .ToArray();
            evaluations = new List<TopologyLocalMotionMacroReport>(candidates.Length);
            simTicks = 0;
            best = null;
            foreach (var candidate in candidates)
            {
                if (simTicks + candidate.DurationTicks > MaxDecisionSimTicks)
                {
                    break;
                }

                simTicks += candidate.DurationTicks;
                var evaluation = EvaluateCandidate(world, self, target, candidate, scan, startObstruction, avoidedObstruction);
                evaluations.Add(evaluation);
                if (evaluation.Accepted && (best is null || evaluation.Score > best.Score))
                {
                    best = evaluation;
                }
            }

            selected = SelectMacro(evaluations, best, startObstruction, startState);
        }

        if (selected is null && startObstruction.DirectPathObstructed)
        {
            scan = BuildScan(
                world.Level,
                self,
                self.Team,
                target,
                includeObstructedReverseFrontiers: true,
                includeReachableReverseStepFrontiers: true,
                includeTargetSideLowerHandoffs: true);
            candidates = EnumerateCandidates(
                    self,
                    target,
                    scan,
                    startObstruction,
                    prioritizeTargetSideUpperFrontiers: true,
                    immediateReverseStepEdgeJump: true,
                    enableReverseAscendingProbes: true,
                    prioritizeFrontierCandidates: true)
                .Select(static candidate => candidate with { AllowRecedingHorizonObstructionGain = true })
                .Take(MaxCandidateCount)
                .ToArray();
            evaluations = new List<TopologyLocalMotionMacroReport>(candidates.Length);
            simTicks = 0;
            best = null;
            foreach (var candidate in candidates)
            {
                if (simTicks + candidate.DurationTicks > MaxDecisionSimTicks)
                {
                    break;
                }

                simTicks += candidate.DurationTicks;
                var evaluation = EvaluateCandidate(world, self, target, candidate, scan, startObstruction, avoidedObstruction);
                evaluations.Add(evaluation);
                if (evaluation.Accepted && (best is null || evaluation.Score > best.Score))
                {
                    best = evaluation;
                }
            }

            selected = SelectMacro(evaluations, best, startObstruction, startState);
        }

        if (selected is null && startObstruction.DirectPathObstructed && !self.IsGrounded)
        {
            selected = SelectLastResortAirborneLandingHandoff(
                world,
                self,
                target,
                evaluations,
                startObstruction,
                avoidedObstruction,
                ref simTicks);
        }
        if (selected is null && startObstruction.DirectPathObstructed)
        {
            selected = SelectLastResortTargetSideNonTerminalProgress(evaluations, startObstruction);
        }
        if (selected is null && startObstruction.DirectPathObstructed)
        {
            selected = SelectLastResortReverseStepUpEscape(evaluations, startObstruction, startState);
        }
        if (selected is null && startObstruction.DirectPathObstructed)
        {
            selected = SelectLastResortClearReverseEscape(evaluations, startObstruction);
        }

        selected = ExtendStableRecedingHandoff(selected, startObstruction);
        selected = ExtendTargetSideLowerBandHandoff(
            world,
            self,
            target,
            selected,
            startObstruction,
            avoidedObstruction,
            ref simTicks);
        selected = ExtendSuccessorlessAirborneHandoff(
            world,
            self,
            target,
            selected,
            startObstruction,
            avoidedObstruction,
            ref simTicks);
        stopwatch.Stop();
        decision = new TopologyLocalMotionDecisionReport(
            Tick: tick,
            StartState: startState,
            Target: target,
            CollisionScan: scan.CollisionScan,
            SurfaceSpans: scan.SurfaceSpans,
            Frontiers: scan.Frontiers,
            StartObstruction: startObstruction,
            MacroCandidates: evaluations,
            SelectedMacro: selected,
            StallReason: selected is null ? "no_accepted_macro" : string.Empty,
            ElapsedMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
            SimulatedTicks: simTicks);

        if (selected is null)
        {
            RememberFailedObstruction(startObstruction, tick);
            _activePlan = default;
            _activeUntilTick = 0;
            return false;
        }

        var selectedMovesAway = MathF.Sign(selected.MoveDirection) != 0f
            && MathF.Sign(selected.MoveDirection) != startObstruction.Direction;
        if (startObstruction.DirectPathObstructed
            && (selectedMovesAway || selected.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal)))
        {
            RememberFailedObstruction(startObstruction, tick);
        }

        _activePlan = LocalMacroPlan.From(selected.Label, selected.MoveDirection, selected.DurationTicks, selected.CommitTicks, selected.JumpStartTick, selected.JumpHoldTicks, selected.DropTicks, selected.PreMoveDirection, selected.PreMoveTicks, selected.MoveStartTick, selected.MoveEndTick, tick);
        _activeUntilTick = tick + _activePlan.CommitTicks;
        input = BuildInput(self, target, _activePlan, 0);
        return true;
    }

    private static TopologyLocalMotionMacroReport? SelectMacro(
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionMacroReport? best,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionState startState)
    {
        if (best is null)
        {
            return startObstruction.DirectPathObstructed
                ? SelectLastResortVerticalRelief(evaluations)
                : null;
        }

        if (!startObstruction.DirectPathObstructed)
        {
            var clearGroundedProgress = evaluations
                .Where(evaluation => IsClearGroundedTargetProgress(evaluation, startObstruction))
                .OrderBy(evaluation => evaluation.FinalMetric)
                .ThenByDescending(evaluation => evaluation.Score)
                .FirstOrDefault();
            if (clearGroundedProgress is not null
                && clearGroundedProgress.FinalMetric <= best.FinalMetric - 48f)
            {
                return clearGroundedProgress with
                {
                    Reasons = clearGroundedProgress.Reasons
                        .Concat(new[] { "preferred_clear_grounded_target_progress" })
                        .ToArray()
                };
            }

            return best;
        }

        var targetBandEntry = evaluations
            .Where(evaluation => evaluation.Accepted
                && evaluation.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (targetBandEntry is not null
            && !best.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal))
        {
            return targetBandEntry;
        }

        var nonTerminalTopologyAlternative = evaluations
            .Where(evaluation => IsNonTerminalTopologyAlternative(evaluation, startObstruction))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (nonTerminalTopologyAlternative is not null
            && HasTerminalWallRisk(best)
            && !IsTerminalRiskAllowedHandoff(best)
            && nonTerminalTopologyAlternative.Score >= best.Score - 2600f)
        {
            return nonTerminalTopologyAlternative with
            {
                Reasons = nonTerminalTopologyAlternative.Reasons
                    .Concat(new[] { "preferred_nonterminal_topology_alternative_over_terminal_wall_risk" })
                    .ToArray()
            };
        }

        var stableUpperFrontier = evaluations
            .Where(evaluation => evaluation.Accepted
                && IsStableUpperFrontierProgress(evaluation, startState))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (stableUpperFrontier is not null
            && (IsTerminalLowerDescent(best, startState)
                || IsLowerDescentCorridorWithoutTargetBandEntry(best, startState)))
        {
            return stableUpperFrontier;
        }
        if (stableUpperFrontier is not null
            && MathF.Sign(best.MoveDirection) != startObstruction.Direction
            && IsUnverifiedReverseEscape(best))
        {
            return stableUpperFrontier;
        }

        var stableAirborneSettle = evaluations
            .Where(evaluation => IsStableAirborneSettle(evaluation, startObstruction))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (stableAirborneSettle is not null
            && MathF.Sign(best.MoveDirection) != startObstruction.Direction
            && IsUnverifiedReverseEscape(best))
        {
            return stableAirborneSettle with
            {
                Reasons = stableAirborneSettle.Reasons
                    .Concat(new[] { "preferred_stable_airborne_settle_over_unverified_reverse_escape" })
                    .ToArray()
            };
        }

        var targetSideRelief = evaluations
            .Where(evaluation => IsTargetSideReliefProgress(evaluation, startObstruction))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (targetSideRelief is not null
            && MathF.Sign(best.MoveDirection) != startObstruction.Direction
            && IsUnverifiedReverseEscape(best))
        {
            return targetSideRelief with
            {
                Reasons = targetSideRelief.Reasons
                    .Concat(new[] { "preferred_target_side_relief_over_unverified_reverse_escape" })
                    .ToArray()
            };
        }

        var verifiedReverseDetour = evaluations
            .Where(evaluation => evaluation.Accepted
                && MathF.Sign(evaluation.MoveDirection) != 0f
                && MathF.Sign(evaluation.MoveDirection) != startObstruction.Direction
                && IsVerifiedReverseUpperDetour(evaluation))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (verifiedReverseDetour is not null
            && IsFragileTargetDirectedObstructionGain(best, startObstruction)
            && verifiedReverseDetour.Score >= best.Score - 900f)
        {
            return verifiedReverseDetour;
        }

        var targetSideProgress = evaluations
            .Where(evaluation => evaluation.Accepted
                && MathF.Sign(evaluation.MoveDirection) == startObstruction.Direction
                && IsTargetSideTopologyProgress(evaluation))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (targetSideProgress is not null
            && MathF.Sign(best.MoveDirection) != startObstruction.Direction
            && ShouldPreferTargetSideProgress(targetSideProgress, best, startState))
        {
            return targetSideProgress;
        }

        var bestMovesAway = MathF.Sign(best.MoveDirection) != 0f
            && MathF.Sign(best.MoveDirection) != startObstruction.Direction;
        var targetOrNeutralProgress = evaluations
            .Where(evaluation => evaluation.Accepted
                && (MathF.Sign(evaluation.MoveDirection) == 0f
                    || MathF.Sign(evaluation.MoveDirection) == startObstruction.Direction)
                && IsTargetSideTopologyProgress(evaluation))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (targetOrNeutralProgress is not null
            && bestMovesAway
            && ShouldPreferTargetSideProgress(targetOrNeutralProgress, best, startState))
        {
            return targetOrNeutralProgress;
        }

        return best;
    }

    private static TopologyLocalMotionMacroReport? SelectLastResortVerticalRelief(
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations)
    {
        var relief = evaluations
            .Where(evaluation => evaluation.BlockReason == "none"
                && MathF.Abs(evaluation.MoveDirection) < 0.01f
                && !evaluation.EvaluationState.IsGrounded
                && evaluation.CommitTicks < evaluation.DurationTicks
                && evaluation.Reasons.Contains("useful_upper_route", StringComparer.Ordinal)
                && evaluation.Reasons.Contains("transient_upper_relief_no_exit_rejected", StringComparer.Ordinal)
                && !evaluation.Reasons.Contains("presses_same_blocking_wall", StringComparer.Ordinal))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (relief is null)
        {
            return null;
        }

        var reasons = relief.Reasons
            .Concat(new[] { "last_resort_vertical_relief_replan" })
            .ToArray();
        return relief with
        {
            Accepted = true,
            Score = MathF.Max(1f, relief.Score),
            Reasons = reasons
        };
    }

    private static TopologyLocalMotionMacroReport? SelectLastResortClearAirborneLanding(
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations)
    {
        var landing = evaluations
            .Where(evaluation => evaluation.BlockReason == "none"
                && !evaluation.Accepted
                && evaluation.FinalState.IsGrounded
                && evaluation.FinalMetric <= evaluation.StartMetric - 96f
                && !evaluation.Reasons.Contains("clear_airborne_drop_into_obstruction_rejected", StringComparer.Ordinal)
                && !evaluation.Reasons.Contains("clear_start_rollout_into_obstructed_lower_basin_rejected", StringComparer.Ordinal)
                && !evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
                && !evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal))
            .OrderByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (landing is null)
        {
            return null;
        }

        var reasons = landing.Reasons
            .Concat(new[] { "last_resort_clear_airborne_landing_progress" })
            .ToArray();
        return landing with
        {
            Accepted = true,
            Score = MathF.Max(1f, landing.Score),
            Reasons = reasons
        };
    }

    private static TopologyLocalMotionMacroReport? SelectLastResortClearStartCommitProgress(
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionObstruction startObstruction)
    {
        var progress = evaluations
            .Where(evaluation => IsLastResortClearStartCommitProgress(evaluation, startObstruction))
            .OrderBy(evaluation => evaluation.EvaluationMetric)
            .ThenByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (progress is null)
        {
            return null;
        }

        var reasons = progress.Reasons
            .Concat(new[] { "last_resort_clear_start_commit_progress" })
            .ToArray();
        return progress with
        {
            Accepted = true,
            Score = MathF.Max(1f, progress.Score),
            Reasons = reasons
        };
    }

    private static bool IsLastResortClearStartCommitProgress(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction)
    {
        var moveDirection = MathF.Sign(evaluation.MoveDirection);
        return evaluation.BlockReason == "none"
            && !evaluation.Accepted
            && evaluation.EvaluationHorizon == "commit"
            && moveDirection != 0f
            && moveDirection == startObstruction.Direction
            && evaluation.EvaluationMetric <= evaluation.StartMetric - 48f
            && evaluation.BestMetric <= evaluation.StartMetric - 48f
            && (!evaluation.EvaluationObstruction.DirectPathObstructed
                || evaluation.EvaluationObstruction.ForwardClearance >= 96f)
            && !evaluation.Reasons.Contains("final_hard_blocked_wall_penalty", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("clear_airborne_drop_into_obstruction_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("clear_start_rollout_into_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("risky_lower_descent_rejected", StringComparer.Ordinal);
    }

    private static TopologyLocalMotionMacroReport? SelectLastResortTargetSideNonTerminalProgress(
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionObstruction startObstruction)
    {
        var progress = evaluations
            .Where(evaluation => IsLastResortTargetSideNonTerminalProgress(evaluation, startObstruction))
            .OrderBy(evaluation => evaluation.FinalMetric)
            .ThenByDescending(evaluation => evaluation.FinalObstruction.ForwardClearance)
            .ThenByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (progress is null)
        {
            return null;
        }

        var reasons = progress.Reasons
            .Concat(new[] { "last_resort_target_side_nonterminal_progress" })
            .ToArray();
        return progress with
        {
            Accepted = true,
            Score = MathF.Max(1f, progress.Score),
            Reasons = reasons
        };
    }

    private static bool IsLastResortTargetSideNonTerminalProgress(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction)
    {
        var moveDirection = MathF.Sign(evaluation.MoveDirection);
        return evaluation.BlockReason == "none"
            && !evaluation.Accepted
            && (moveDirection == 0f || moveDirection == startObstruction.Direction)
            && evaluation.FinalMetric <= evaluation.StartMetric - 32f
            && evaluation.BestMetric <= evaluation.StartMetric - 48f
            && !HasTerminalWallRisk(evaluation)
            && !evaluation.Reasons.Contains("presses_same_blocking_wall", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("risky_lower_descent_rejected", StringComparer.Ordinal)
            && (!evaluation.FinalObstruction.DirectPathObstructed
                || evaluation.FinalObstruction.ForwardClearance >= MathF.Max(48f, startObstruction.ForwardClearance * 0.4f));
    }

    private static TopologyLocalMotionMacroReport? SelectLastResortReverseStepUpEscape(
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionState startState)
    {
        var escape = evaluations
            .Where(evaluation => IsLastResortReverseStepUpEscape(evaluation, startObstruction, startState))
            .OrderByDescending(evaluation => evaluation.FinalObstruction.ForwardClearance)
            .ThenBy(evaluation => evaluation.FinalMetric)
            .ThenByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (escape is null)
        {
            return null;
        }

        var reasons = escape.Reasons
            .Concat(new[] { "last_resort_reverse_step_up_frontier_escape" })
            .ToArray();
        return escape with
        {
            Accepted = true,
            Score = MathF.Max(1f, escape.Score),
            Reasons = reasons
        };
    }

    private static bool IsLastResortReverseStepUpEscape(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionState startState)
    {
        var moveDirection = MathF.Sign(evaluation.MoveDirection);
        return evaluation.BlockReason == "none"
            && !evaluation.Accepted
            && moveDirection != 0f
            && moveDirection != startObstruction.Direction
            && evaluation.FinalState.IsGrounded
            && evaluation.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal)
            && evaluation.Reasons.Contains("useful_upper_route", StringComparer.Ordinal)
            && evaluation.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal)
            && evaluation.Reasons.Any(static reason => reason.StartsWith("new_surface_span:", StringComparison.Ordinal))
            && evaluation.FinalState.Bottom <= startState.Bottom - MathF.Max(32f, UpwardReverseEscapeMinGain * 0.5f)
            && evaluation.FinalState.Bottom <= startState.Bottom + 8f
            && evaluation.FinalState.Bottom - startState.Bottom <= 96f
            && !HasTerminalWallRisk(evaluation)
            && !evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("risky_lower_descent_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("returns_to_recent_failed_obstruction", StringComparer.Ordinal)
            && !evaluation.Reasons.Any(static reason => reason.StartsWith("returns_to_recent_failed_obstruction:", StringComparison.Ordinal))
            && !evaluation.Reasons.Any(static reason => reason.StartsWith("recycles_recent_failed_obstruction_column:", StringComparison.Ordinal))
            && (!evaluation.FinalObstruction.DirectPathObstructed
                || evaluation.FinalObstruction.ForwardClearance >= startObstruction.ForwardClearance + 64f);
    }

    private static TopologyLocalMotionMacroReport? SelectLastResortClearReverseEscape(
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionObstruction startObstruction)
    {
        var escape = evaluations
            .Where(evaluation => IsLastResortClearReverseEscape(evaluation, startObstruction))
            .OrderByDescending(evaluation => evaluation.FinalObstruction.DirectPathObstructed ? 0 : 1)
            .ThenByDescending(evaluation => evaluation.FinalObstruction.ForwardClearance)
            .ThenByDescending(evaluation => evaluation.Score)
            .FirstOrDefault();
        if (escape is null)
        {
            return null;
        }

        var reasons = escape.Reasons
            .Concat(new[] { "last_resort_clear_reverse_escape" })
            .ToArray();
        return escape with
        {
            Accepted = true,
            Score = MathF.Max(1f, escape.Score),
            Reasons = reasons
        };
    }

    private static bool IsLastResortClearReverseEscape(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction)
    {
        var moveDirection = MathF.Sign(evaluation.MoveDirection);
        if (evaluation.BlockReason != "none"
            || evaluation.Accepted
            || moveDirection == 0f
            || moveDirection == startObstruction.Direction
            || HasTerminalWallRisk(evaluation)
            || evaluation.PressesSameObstruction
            || evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("risky_lower_descent_rejected", StringComparer.Ordinal)
            || evaluation.Reasons.Any(static reason => reason.StartsWith("returns_to_recent_failed_obstruction:", StringComparison.Ordinal))
            || evaluation.Reasons.Any(static reason => reason.StartsWith("recycles_recent_failed_obstruction_column:", StringComparison.Ordinal)))
        {
            return false;
        }

        var usefulEscapeSignal = evaluation.ObstructionChanged
            || evaluation.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal)
            || evaluation.Reasons.Any(static reason => reason.StartsWith("forward_clearance_gain:", StringComparison.Ordinal));
        if (!usefulEscapeSignal)
        {
            return false;
        }

        return !evaluation.FinalObstruction.DirectPathObstructed
            || evaluation.FinalObstruction.ForwardClearance >= startObstruction.ForwardClearance + 32f;
    }

    private static TopologyLocalMotionMacroReport? SelectLastResortAirborneLandingHandoff(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        var landings = evaluations
            .Where(evaluation => evaluation.BlockReason == "none"
                && !evaluation.Accepted
                && evaluation.FinalState.IsGrounded
                && MathF.Sign(evaluation.MoveDirection) == startObstruction.Direction
                && evaluation.Reasons.Contains("airborne_settle_to_ground", StringComparer.Ordinal)
                && evaluation.Reasons.Any(static reason => reason.StartsWith("new_surface_span:", StringComparison.Ordinal))
                && evaluation.Reasons.Contains("final_hard_blocked_wall_penalty", StringComparer.Ordinal)
                && !evaluation.Reasons.Contains("returns_to_recent_failed_obstruction", StringComparer.Ordinal)
                && !evaluation.Reasons.Contains("recycles_recent_failed_obstruction_column", StringComparer.Ordinal))
            .OrderByDescending(evaluation => evaluation.Progress)
            .ThenByDescending(evaluation => evaluation.Score)
            .ToArray();
        foreach (var landing in landings)
        {
            var landingProbe = SimulateSelectedMacro(
                world,
                self,
                target,
                landing,
                landing.DurationTicks,
                ProbeEntityId - 30);
            if (!landingProbe.IsGrounded || !IsValidProbeState(world, landingProbe, self.Team))
            {
                continue;
            }

            if (!HasVerifiedLandingSuccessorMacro(world, landingProbe, target, avoidedObstruction, ref simTicks))
            {
                continue;
            }

            var reasons = landing.Reasons
                .Concat(new[] { "last_resort_airborne_landing_handoff", "verified_landing_successor" })
                .ToArray();
            return landing with
            {
                Accepted = true,
                CommitTicks = landing.DurationTicks,
                Score = MathF.Max(1f, landing.Score),
                Reasons = reasons
            };
        }

        return null;
    }

    private static bool HasVerifiedLandingSuccessorMacro(
        SimulationWorld world,
        PlayerEntity state,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        var startState = TopologyLocalMotionState.From(state);
        var startObstruction = AnalyzeObstruction(world.Level, state, state.Team, target);
        var scan = BuildScan(
            world.Level,
            state,
            state.Team,
            target,
            includeObstructedReverseFrontiers: IsNearUpperWallEscapeState(state, startObstruction) || avoidedObstruction is not null,
            includeTargetSideLowerHandoffs: startObstruction.DirectPathObstructed);
        var candidates = EnumerateCandidates(
                state,
                target,
                scan,
                startObstruction,
                preferTargetSideFrontiers: true,
                prioritizeLowerOverlapDrops: startObstruction.DirectPathObstructed)
            .Take(MaxCandidateCount)
            .ToArray();
        var evaluations = new List<TopologyLocalMotionMacroReport>(candidates.Length);
        var localSimTicks = 0;
        TopologyLocalMotionMacroReport? best = null;
        foreach (var candidate in candidates)
        {
            if (localSimTicks + candidate.DurationTicks > MaxDecisionSimTicks)
            {
                break;
            }

            localSimTicks += candidate.DurationTicks;
            var evaluation = EvaluateCandidate(world, state, target, candidate, scan, startObstruction, avoidedObstruction);
            evaluations.Add(evaluation);
            if (evaluation.Accepted && (best is null || evaluation.Score > best.Score))
            {
                best = evaluation;
            }
        }

        simTicks += localSimTicks;
        var selected = SelectMacro(evaluations, best, startObstruction, startState);
        return selected is not null
            && (selected.Reasons.Contains("overlap_lower_drop_handoff", StringComparer.Ordinal)
                || selected.Reasons.Contains("target_band_lower_followup_visible", StringComparer.Ordinal)
                || selected.Reasons.Contains("target_side_lower_handoff_commit", StringComparer.Ordinal)
                || selected.Reasons.Contains("target_side_descent_entry_commit", StringComparer.Ordinal)
                || selected.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal)
                || selected.Reasons.Contains("clear_near_target_approach_commit", StringComparer.Ordinal)
                || selected.Reasons.Contains("crossed_block_endpoint", StringComparer.Ordinal));
    }

    private static bool IsStableUpperFrontierProgress(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionState startState) =>
        evaluation.Reasons.Contains("stable_upper_frontier_handoff", StringComparer.Ordinal)
        && evaluation.Reasons.Contains("upper_frontier_commit", StringComparer.Ordinal)
        && evaluation.CommitTicks < evaluation.DurationTicks
        && evaluation.EvaluationState.IsGrounded
        && evaluation.EvaluationState.Bottom <= startState.Bottom + 8f
        && (evaluation.EvaluationObstruction.ForwardClearance >= 48f
            || evaluation.Reasons.Contains("crossed_block_endpoint", StringComparer.Ordinal))
        && evaluation.Score > 0f
        && !evaluation.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("unstable_upper_relief_handoff_rejected", StringComparer.Ordinal);

    private static bool IsUnverifiedReverseEscape(TopologyLocalMotionMacroReport evaluation) =>
        evaluation.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("verified_reverse_upper_detour", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("reverse_upper_followup_verified", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("reverse_step_followup_verified", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("reverse_step_receding_upper_handoff", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("reverse_step_setup_positioning", StringComparer.Ordinal);

    private static bool IsTargetSideReliefProgress(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction)
    {
        if (!evaluation.Accepted
            || MathF.Sign(evaluation.MoveDirection) != startObstruction.Direction
            || !evaluation.ObstructionChanged
            || evaluation.Score <= 0f
            || HasTerminalWallRisk(evaluation)
            || evaluation.Reasons.Contains("returns_to_recent_failed_obstruction", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("recycles_recent_failed_obstruction_column", StringComparer.Ordinal))
        {
            return false;
        }

        var clearanceGain = evaluation.EvaluationObstruction.ForwardClearance - startObstruction.ForwardClearance;
        return IsTargetSideTopologyProgress(evaluation)
            || (evaluation.Reasons.Contains("useful_upper_route", StringComparer.Ordinal)
                && clearanceGain >= 32f)
            || (evaluation.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal)
                && clearanceGain >= 24f)
            || evaluation.Reasons.Contains("shifted_obstruction_segment", StringComparer.Ordinal);
    }

    private static bool IsClearGroundedTargetProgress(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction) =>
        evaluation.Accepted
        && MathF.Sign(evaluation.MoveDirection) == startObstruction.Direction
        && evaluation.FinalState.IsGrounded
        && evaluation.FinalMetric <= evaluation.StartMetric - 32f
        && !HasTerminalWallRisk(evaluation)
        && !evaluation.Reasons.Contains("clear_start_rollout_into_obstructed_lower_basin_rejected", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal);

    private static bool IsStableAirborneSettle(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction)
    {
        var moveDirection = MathF.Sign(evaluation.MoveDirection);
        return evaluation.Accepted
            && evaluation.FinalState.IsGrounded
            && evaluation.Reasons.Contains("airborne_settle_to_ground", StringComparer.Ordinal)
            && (moveDirection == 0f || moveDirection == startObstruction.Direction)
            && !HasTerminalWallRisk(evaluation)
            && !evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            && !evaluation.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal);
    }

    private static bool IsTerminalLowerDescent(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionState startState) =>
        evaluation.FinalState.IsGrounded
        && evaluation.FinalState.Bottom >= startState.Bottom + 48f
        && HasTerminalWallRisk(evaluation)
        && !evaluation.Reasons.Contains("stable_upper_frontier_handoff", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("upper_frontier_commit", StringComparer.Ordinal);

    private static bool IsLowerDescentCorridorWithoutTargetBandEntry(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionState startState) =>
        evaluation.FinalState.IsGrounded
        && evaluation.FinalState.Bottom >= startState.Bottom + 48f
        && evaluation.Reasons.Contains("target_side_descent_corridor_commit", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("target_side_descent_entry_commit", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("overlap_lower_drop_handoff", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("stable_upper_frontier_handoff", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("upper_frontier_commit", StringComparer.Ordinal);

    private static bool ShouldPreferTargetSideProgress(
        TopologyLocalMotionMacroReport targetSideProgress,
        TopologyLocalMotionMacroReport best,
        TopologyLocalMotionState startState)
    {
        if (IsUpwardReverseObstructionEscape(best, startState)
            && HasTerminalWallRisk(targetSideProgress))
        {
            return false;
        }

        if (IsUpwardReverseObstructionEscape(best, startState)
            && MathF.Abs(targetSideProgress.MoveDirection) < 0.01f
            && targetSideProgress.FinalState.IsGrounded
            && targetSideProgress.FinalObstruction.DirectPathObstructed
            && MathF.Abs(targetSideProgress.FinalState.X - startState.X) <= 42f
            && !targetSideProgress.Reasons.Any(static reason => reason.StartsWith("new_surface_span:", StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static bool HasTerminalWallRisk(TopologyLocalMotionMacroReport evaluation) =>
        evaluation.Reasons.Contains("final_hard_blocked_wall_penalty", StringComparer.Ordinal)
        || evaluation.Reasons.Contains("final_rollout_hard_blocked_wall_risk", StringComparer.Ordinal);

    private static bool IsTerminalRiskAllowedHandoff(TopologyLocalMotionMacroReport evaluation) =>
        evaluation.Reached
        || evaluation.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal)
        || evaluation.Reasons.Contains("target_side_descent_entry_commit", StringComparer.Ordinal)
        || evaluation.Reasons.Contains("target_side_lower_handoff_commit", StringComparer.Ordinal)
        || evaluation.Reasons.Contains("overlap_lower_drop_handoff", StringComparer.Ordinal);

    private static bool IsNonTerminalTopologyAlternative(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction) =>
        evaluation.Accepted
        && !HasTerminalWallRisk(evaluation)
        && !evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("risky_lower_descent_rejected", StringComparer.Ordinal)
        && !evaluation.Reasons.Any(static reason => reason.StartsWith("returns_to_recent_failed_obstruction:", StringComparison.Ordinal))
        && !evaluation.Reasons.Any(static reason => reason.StartsWith("recycles_recent_failed_obstruction_column:", StringComparison.Ordinal))
        && (IsTargetSideTopologyProgress(evaluation)
            || evaluation.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("verified_reverse_upper_detour", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("reverse_step_up_escape", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("reverse_upper_escape", StringComparer.Ordinal));

    private static bool IsUpwardReverseObstructionEscape(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionState startState) =>
        evaluation.Accepted
        && evaluation.FinalState.IsGrounded
        && evaluation.FinalState.Bottom <= startState.Bottom - UpwardReverseEscapeMinGain
        && evaluation.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal)
        && !evaluation.Reasons.Any(static reason => reason.StartsWith("lower_descent_penalty:", StringComparison.Ordinal))
        && (evaluation.Reasons.Contains("reverse_step_up_escape", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("reverse_upper_escape", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("useful_upper_route", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal));

    private static TopologyLocalMotionMacroReport? ExtendStableRecedingHandoff(
        TopologyLocalMotionMacroReport? selected,
        TopologyLocalMotionObstruction startObstruction)
    {
        if (selected is null || !startObstruction.DirectPathObstructed)
        {
            return selected;
        }

        var selectedMovesAway = MathF.Sign(selected.MoveDirection) != 0f
            && MathF.Sign(selected.MoveDirection) != startObstruction.Direction;
        if (selectedMovesAway
            && selected.Reasons.Contains("same_surface_edge_positioning", StringComparer.Ordinal)
            && selected.CommitTicks < selected.DurationTicks)
        {
            var extendedReasons = selected.Reasons
                .Concat(new[] { "extended_same_surface_edge_positioning" })
                .ToArray();
            return selected with
            {
                CommitTicks = selected.DurationTicks,
                Reasons = extendedReasons
            };
        }

        if (selected.Reasons.Contains("target_side_descent_corridor_commit", StringComparer.Ordinal)
            && !selected.EvaluationState.IsGrounded
            && selected.FinalState.IsGrounded
            && selected.PreMoveTicks == 0
            && selected.MoveEndTick == int.MaxValue
            && selected.CommitTicks < selected.DurationTicks)
        {
            var extendedReasons = selected.Reasons
                .Concat(new[] { "extended_target_side_descent_handoff" })
                .ToArray();
            return selected with
            {
                CommitTicks = selected.DurationTicks,
                Reasons = extendedReasons
            };
        }

        var selectedHasTerminalWallRisk = selected.Reasons.Contains("final_hard_blocked_wall_penalty", StringComparer.Ordinal)
            || selected.Reasons.Contains("final_rollout_hard_blocked_wall_risk", StringComparer.Ordinal);
        var selectedHasLowerBasinRisk = selected.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
            || selected.Reasons.Contains("risky_lower_descent_rejected", StringComparer.Ordinal)
            || selected.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            || selected.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal);
        if (!selectedMovesAway
            && !selected.EvaluationState.IsGrounded
            && selected.FinalState.IsGrounded
            && selected.CommitTicks < selected.DurationTicks
            && selected.Reasons.Contains("receding_horizon_obstruction_gain", StringComparer.Ordinal)
            && (selected.Reasons.Contains("useful_upper_route", StringComparer.Ordinal)
                || selected.Reasons.Contains("crossed_block_endpoint", StringComparer.Ordinal)
                || selected.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal))
            && !selectedHasTerminalWallRisk
            && !selectedHasLowerBasinRisk)
        {
            var extendedReasons = selected.Reasons
                .Concat(new[] { "extended_target_side_receding_handoff" })
                .ToArray();
            return selected with
            {
                CommitTicks = selected.DurationTicks,
                Reasons = extendedReasons
            };
        }

        if (!selectedMovesAway
            || selected.EvaluationState.IsGrounded
            || !selected.FinalState.IsGrounded
            || !selected.Reasons.Contains("receding_horizon_obstruction_gain", StringComparer.Ordinal)
            || selected.CommitTicks >= selected.DurationTicks)
        {
            if (!selectedMovesAway
                || selected.EvaluationState.IsGrounded
                || selected.FinalObstruction.DirectPathObstructed
                || !selected.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal)
                || selected.CommitTicks >= selected.DurationTicks)
            {
                return selected;
            }

            var unstableReasons = selected.Reasons
                .Concat(new[] { "extended_unstable_reverse_escape_handoff" })
                .ToArray();
            return selected with
            {
                CommitTicks = selected.DurationTicks,
                Reasons = unstableReasons
            };
        }

        var reasons = selected.Reasons
            .Concat(new[] { "extended_to_stable_receding_handoff" })
            .ToArray();
        return selected with
        {
            CommitTicks = selected.DurationTicks,
            Reasons = reasons
        };
    }

    private static bool IsVerifiedReverseUpperDetour(TopologyLocalMotionMacroReport evaluation) =>
        evaluation.Reasons.Contains("verified_reverse_upper_detour", StringComparer.Ordinal)
        || evaluation.Reasons.Contains("reverse_upper_followup_verified", StringComparer.Ordinal)
        || evaluation.Reasons.Contains("reverse_step_followup_verified", StringComparer.Ordinal);

    private static bool IsFragileTargetDirectedObstructionGain(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction) =>
        MathF.Sign(evaluation.MoveDirection) == startObstruction.Direction
        && evaluation.FinalState.IsGrounded
        && evaluation.FinalObstruction.DirectPathObstructed
        && evaluation.FinalObstruction.ForwardClearance <= 72f
        && evaluation.Reasons.Contains("receding_horizon_obstruction_gain", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("target_side_descent_entry_commit", StringComparer.Ordinal)
        && !evaluation.Reasons.Contains("target_side_descent_corridor_commit", StringComparer.Ordinal);

    private static TopologyLocalMotionMacroReport? VerifySelectedBoundedSuccessor(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        List<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionMacroReport? selected,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        if (selected is null || !NeedsBoundedSuccessorVerification(selected, startObstruction))
        {
            return selected;
        }

        var commitProbe = SimulateSelectedMacro(
            world,
            self,
            target,
            selected,
            Math.Min(selected.CommitTicks + 1, selected.DurationTicks),
            ProbeEntityId - 40);
        if (IsValidProbeState(world, commitProbe, self.Team)
            && HasBoundedTopologyContinuation(
                world,
                commitProbe,
                target,
                startObstruction,
                avoidedObstruction,
                ref simTicks))
        {
            return selected with
            {
                Reasons = selected.Reasons
                    .Concat(new[] { "bounded_commit_successor_verified" })
                    .ToArray()
            };
        }

        if (selected.CommitTicks < selected.DurationTicks)
        {
            var fullProbe = SimulateSelectedMacro(
                world,
                self,
                target,
                selected,
                selected.DurationTicks,
                ProbeEntityId - 41);
            if (IsValidProbeState(world, fullProbe, self.Team)
                && HasBoundedTopologyContinuation(
                    world,
                    fullProbe,
                    target,
                    startObstruction,
                    avoidedObstruction,
                    ref simTicks))
            {
                return selected with
                {
                    CommitTicks = selected.DurationTicks,
                    Reasons = selected.Reasons
                        .Concat(new[] { "commit_handoff_had_no_bounded_successor", "extended_to_bounded_successor_landing" })
                        .ToArray()
                };
            }
        }

        var alternative = SelectBoundedSuccessorAlternative(
            world,
            self,
            target,
            evaluations,
            selected,
            startObstruction,
            avoidedObstruction,
            ref simTicks);
        if (alternative is not null)
        {
            return alternative with
            {
                Reasons = alternative.Reasons
                    .Concat(new[] { "preferred_bounded_successor_alternative" })
                    .ToArray()
            };
        }

        MarkEvaluationRejected(
            evaluations,
            selected,
            "bounded_successor_chain_missing_rejected");
        return null;
    }

    private static bool NeedsBoundedSuccessorVerification(
        TopologyLocalMotionMacroReport evaluation,
        TopologyLocalMotionObstruction startObstruction)
    {
        if (!evaluation.Accepted
            || evaluation.Reached
            || !startObstruction.DirectPathObstructed
            || IsTerminalRiskAllowedHandoff(evaluation))
        {
            return false;
        }

        var topologyChanging = evaluation.ObstructionChanged
            || evaluation.Reasons.Contains("useful_upper_route", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("crossed_block_endpoint", StringComparer.Ordinal)
            || evaluation.Reasons.Any(static reason => reason.StartsWith("new_surface_span:", StringComparison.Ordinal));
        if (!topologyChanging)
        {
            return false;
        }

        var targetDirected = MathF.Sign(evaluation.MoveDirection) == startObstruction.Direction
            || MathF.Abs(evaluation.MoveDirection) < 0.01f;
        var obstructedEarlyCommit = evaluation.CommitTicks < evaluation.DurationTicks
            && evaluation.EvaluationObstruction.DirectPathObstructed
            && (targetDirected
                || evaluation.Reasons.Contains("preferred_target_side_relief_over_unverified_reverse_escape", StringComparer.Ordinal)
                || evaluation.Reasons.Contains("receding_horizon_obstruction_gain", StringComparer.Ordinal));
        var terminalObstructedLanding = evaluation.FinalObstruction.DirectPathObstructed
            && (HasTerminalWallRisk(evaluation)
                || evaluation.FinalObstruction.ForwardClearance <= MathF.Max(48f, startObstruction.ForwardClearance * 0.5f)
                || evaluation.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal));
        return obstructedEarlyCommit || terminalObstructedLanding;
    }

    private static TopologyLocalMotionMacroReport? SelectBoundedSuccessorAlternative(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        IReadOnlyList<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionMacroReport selected,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        foreach (var alternative in evaluations
            .Where(evaluation => evaluation.Accepted
                && !SameMacroIdentity(evaluation, selected)
                && IsNonTerminalTopologyAlternative(evaluation, startObstruction)
                && evaluation.Score >= selected.Score - 3200f)
            .OrderByDescending(evaluation => evaluation.Score))
        {
            var probe = SimulateSelectedMacro(
                world,
                self,
                target,
                alternative,
                Math.Min(alternative.CommitTicks + 1, alternative.DurationTicks),
                ProbeEntityId - 42);
            if (IsValidProbeState(world, probe, self.Team)
                && HasBoundedTopologyContinuation(
                    world,
                    probe,
                    target,
                    startObstruction,
                    avoidedObstruction,
                    ref simTicks))
            {
                return alternative;
            }
        }

        return null;
    }

    private static bool HasBoundedTopologyContinuation(
        SimulationWorld world,
        PlayerEntity seed,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionObstruction referenceObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        var cursor = new PlayerEntity(ProbeEntityId - 43, seed.ClassDefinition, "TopologyLocalMotionBoundedSuccessorProbe");
        var seedState = seed.CapturePredictionState();
        cursor.RestorePredictionState(in seedState);
        var sawObstructionImprovement = false;
        for (var depth = 0; depth < BoundedSuccessorContinuationDepth; depth += 1)
        {
            if (MeasureMetric(cursor.X, cursor.Bottom, target) <= 0f)
            {
                return true;
            }

            var cursorObstruction = AnalyzeObstruction(world.Level, cursor, cursor.Team, target);
            if (!cursorObstruction.DirectPathObstructed)
            {
                return true;
            }

            if (referenceObstruction.DirectPathObstructed
                && !cursorObstruction.SameObstructionFamilyAs(referenceObstruction)
                && cursorObstruction.ForwardClearance >= MathF.Max(96f, referenceObstruction.ForwardClearance - 24f))
            {
                sawObstructionImprovement = true;
            }

            if (referenceObstruction.DirectPathObstructed
                && cursorObstruction.ForwardClearance >= referenceObstruction.ForwardClearance + 96f)
            {
                sawObstructionImprovement = true;
            }

            var startState = TopologyLocalMotionState.From(cursor);
            var selected = SelectSuccessorFromState(
                world,
                cursor,
                target,
                cursorObstruction,
                avoidedObstruction,
                includeReverseFrontiers: true,
                includeTargetSideLowerHandoffs: cursorObstruction.DirectPathObstructed,
                startState: startState,
                simTicks: ref simTicks,
                includeReachableReverseStepFrontiers: true,
                preferTargetSideFrontiers: true,
                prioritizeTargetSideUpperFrontiers: true,
                immediateReverseStepEdgeJump: true,
                enableReverseAscendingProbes: true,
                prioritizeFrontierCandidates: true,
                allowRecedingHorizonObstructionGain: true,
                maxSimTicks: BoundedSuccessorStepSimTicks);
            if (selected is null || (HasTerminalWallRisk(selected) && !IsTerminalRiskAllowedHandoff(selected)))
            {
                return false;
            }

            var next = SimulateSelectedMacro(
                world,
                cursor,
                target,
                selected,
                Math.Min(selected.CommitTicks + 1, selected.DurationTicks),
                ProbeEntityId - 44 - depth);
            if (!IsValidProbeState(world, next, cursor.Team))
            {
                return false;
            }

            var nextObstruction = AnalyzeObstruction(world.Level, next, next.Team, target);
            var crossedReferenceBlock = referenceObstruction.DirectPathObstructed
                && ((referenceObstruction.Direction > 0f && next.X > referenceObstruction.BlockX + 20f)
                    || (referenceObstruction.Direction < 0f && next.X < referenceObstruction.BlockX - 20f));
            if (crossedReferenceBlock
                && (!nextObstruction.DirectPathObstructed
                    || nextObstruction.ForwardClearance >= cursorObstruction.ForwardClearance + 32f
                    || !nextObstruction.SameObstructionFamilyAs(referenceObstruction)))
            {
                sawObstructionImprovement = true;
            }

            var topologyProgress = IsTargetSideTopologyProgress(selected)
                || selected.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal)
                || selected.Reasons.Contains("verified_reverse_upper_detour", StringComparer.Ordinal)
                || selected.Reasons.Contains("reverse_step_up_escape", StringComparer.Ordinal)
                || selected.Reasons.Contains("reverse_upper_escape", StringComparer.Ordinal)
                || selected.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal);
            if (!topologyProgress)
            {
                return false;
            }

            cursor = next;
        }

        var finalObstruction = AnalyzeObstruction(world.Level, cursor, cursor.Team, target);
        return sawObstructionImprovement
            && !finalObstruction.DirectPathObstructed;
    }

    private static void MarkEvaluationRejected(
        List<TopologyLocalMotionMacroReport> evaluations,
        TopologyLocalMotionMacroReport selected,
        string reason)
    {
        for (var index = 0; index < evaluations.Count; index += 1)
        {
            if (!SameMacroIdentity(evaluations[index], selected))
            {
                continue;
            }

            evaluations[index] = evaluations[index] with
            {
                Accepted = false,
                Score = MathF.Min(evaluations[index].Score, -1f),
                Reasons = evaluations[index].Reasons
                    .Concat(new[] { reason })
                    .ToArray()
            };
            return;
        }
    }

    private static bool SameMacroIdentity(
        TopologyLocalMotionMacroReport left,
        TopologyLocalMotionMacroReport right) =>
        left.Label == right.Label
        && MathF.Abs(left.MoveDirection - right.MoveDirection) < 0.01f
        && left.DurationTicks == right.DurationTicks
        && left.CommitTicks == right.CommitTicks
        && left.JumpStartTick == right.JumpStartTick
        && left.JumpHoldTicks == right.JumpHoldTicks
        && left.DropTicks == right.DropTicks
        && MathF.Abs(left.PreMoveDirection - right.PreMoveDirection) < 0.01f
        && left.PreMoveTicks == right.PreMoveTicks
        && left.MoveStartTick == right.MoveStartTick
        && left.MoveEndTick == right.MoveEndTick;

    private static TopologyLocalMotionMacroReport? ExtendTargetSideLowerBandHandoff(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionMacroReport? selected,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        if (selected is null || !startObstruction.DirectPathObstructed)
        {
            return selected;
        }

        var selectedMovesAway = MathF.Sign(selected.MoveDirection) != 0f
            && MathF.Sign(selected.MoveDirection) != startObstruction.Direction;
        var lowerBandHandoffCandidate = !selectedMovesAway
            && selected.CommitTicks < selected.DurationTicks
            && selected.EvaluationState.IsGrounded
            && selected.FinalState.IsGrounded
            && selected.FinalState.Bottom >= selected.EvaluationState.Bottom + 48f
            && selected.FinalMetric <= selected.StartMetric - 64f
            && selected.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal)
            && selected.Reasons.Contains("final_rollout_hard_blocked_wall_risk", StringComparer.Ordinal)
            && !selected.FinalObstruction.SameObstructionAs(startObstruction);
        if (!lowerBandHandoffCandidate)
        {
            return selected;
        }

        var landingProbe = SimulateSelectedMacro(
            world,
            self,
            target,
            selected,
            selected.DurationTicks,
            ProbeEntityId - 12);
        if (!landingProbe.IsGrounded || !IsValidProbeState(world, landingProbe, self.Team))
        {
            var rejectedReasons = selected.Reasons
                .Concat(new[] { "target_side_lower_handoff_extension_rejected_invalid_landing" })
                .ToArray();
            return selected with
            {
                Reasons = rejectedReasons
            };
        }

        if (!HasBoundedClearTargetContinuation(
            world,
            landingProbe,
            target,
            selected.FinalObstruction,
            avoidedObstruction,
            ref simTicks))
        {
            var rejectedReasons = selected.Reasons
                .Concat(new[] { "target_side_lower_handoff_extension_rejected_no_clear_continuation" })
                .ToArray();
            return selected with
            {
                Reasons = rejectedReasons
            };
        }

        var extendedReasons = selected.Reasons
            .Concat(new[] { "verified_lower_handoff_clear_continuation", "extended_target_side_lower_band_handoff" })
            .ToArray();
        return selected with
        {
            CommitTicks = selected.DurationTicks,
            Reasons = extendedReasons
        };
    }

    private static TopologyLocalMotionMacroReport? ExtendSuccessorlessAirborneHandoff(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionMacroReport? selected,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        if (selected is null
            || !selected.Accepted
            || selected.CommitTicks >= selected.DurationTicks
            || selected.EvaluationState.IsGrounded
            || selected.FinalState.IsGrounded is false
            || selected.EvaluationState.RemainingAirJumps > 0
            || MathF.Sign(selected.MoveDirection) != startObstruction.Direction
            || selected.Reasons.Contains("extended_successorless_airborne_handoff_to_landing", StringComparer.Ordinal))
        {
            return selected;
        }

        var topologyChangingAirborneHandoff = selected.Reasons.Contains("useful_upper_route", StringComparer.Ordinal)
            || selected.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal)
            || selected.Reasons.Contains("target_side_descent_corridor_commit", StringComparer.Ordinal)
            || selected.Reasons.Contains("receding_horizon_obstruction_gain", StringComparer.Ordinal)
            || selected.Reasons.Contains("clear_airborne_grounding_handoff", StringComparer.Ordinal);
        if (!topologyChangingAirborneHandoff)
        {
            return selected;
        }

        var selectedHasTerminalWallRisk = selected.Reasons.Contains("final_hard_blocked_wall_penalty", StringComparer.Ordinal)
            || selected.Reasons.Contains("final_rollout_hard_blocked_wall_risk", StringComparer.Ordinal);
        var selectedHasLowerBasinRisk = selected.Reasons.Contains("persistent_obstructed_lower_rollout_penalty", StringComparer.Ordinal)
            || selected.Reasons.Contains("risky_lower_descent_rejected", StringComparer.Ordinal)
            || selected.Reasons.Contains("full_rollout_enters_obstructed_lower_basin_rejected", StringComparer.Ordinal)
            || selected.Reasons.Contains("lost_upper_relief_to_obstructed_rollout_rejected", StringComparer.Ordinal);
        if (selectedHasTerminalWallRisk || selectedHasLowerBasinRisk)
        {
            return selected;
        }

        var commitProbe = SimulateSelectedMacro(
            world,
            self,
            target,
            selected,
            Math.Min(selected.CommitTicks + 1, selected.DurationTicks),
            ProbeEntityId - 10);
        if (!IsValidProbeState(world, commitProbe, self.Team))
        {
            return selected;
        }

        if (HasAcceptedSuccessorMacro(world, commitProbe, target, avoidedObstruction, ref simTicks))
        {
            return selected;
        }

        var landingProbe = SimulateSelectedMacro(
            world,
            self,
            target,
            selected,
            selected.DurationTicks,
            ProbeEntityId - 11);
        if (!landingProbe.IsGrounded || !IsValidProbeState(world, landingProbe, self.Team))
        {
            return selected;
        }

        if (!HasAcceptedSuccessorMacro(world, landingProbe, target, avoidedObstruction, ref simTicks))
        {
            return selected;
        }

        var extendedReasons = selected.Reasons
            .Concat(new[] { "commit_handoff_had_no_successor", "extended_successorless_airborne_handoff_to_landing" })
            .ToArray();
        return selected with
        {
            CommitTicks = selected.DurationTicks,
            Reasons = extendedReasons
        };
    }

    private static PlayerEntity SimulateSelectedMacro(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionMacroReport selected,
        int ticks,
        int probeEntityId)
    {
        var probe = new PlayerEntity(probeEntityId, self.ClassDefinition, "TopologyLocalMotionHandoffProbe");
        var state = self.CapturePredictionState();
        probe.RestorePredictionState(in state);
        var plan = LocalMacroPlan.From(
            selected.Label,
            selected.MoveDirection,
            selected.DurationTicks,
            selected.CommitTicks,
            selected.JumpStartTick,
            selected.JumpHoldTicks,
            selected.DropTicks,
            selected.PreMoveDirection,
            selected.PreMoveTicks,
            selected.MoveStartTick,
            selected.MoveEndTick,
            startTick: 0);
        var previousInput = default(PlayerInputSnapshot);
        for (var age = 0; age < ticks; age += 1)
        {
            var input = BuildInput(probe, target, plan, age);
            var jumpPressed = input.Up && !previousInput.Up;
            probe.Advance(input, jumpPressed, world.Level, self.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;
            if (!IsValidProbeState(world, probe, self.Team))
            {
                break;
            }
        }

        return probe;
    }

    private static bool HasAcceptedSuccessorMacro(
        SimulationWorld world,
        PlayerEntity state,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        var startState = TopologyLocalMotionState.From(state);
        var startObstruction = AnalyzeObstruction(world.Level, state, state.Team, target);
        var selected = SelectSuccessorFromState(
            world,
            state,
            target,
            startObstruction,
            avoidedObstruction,
            includeReverseFrontiers: IsNearUpperWallEscapeState(state, startObstruction) || avoidedObstruction is not null,
            includeTargetSideLowerHandoffs: startObstruction.DirectPathObstructed,
            startState: startState,
            simTicks: ref simTicks);
        if (selected is not null)
        {
            return true;
        }

        if (!startObstruction.DirectPathObstructed)
        {
            return false;
        }

        selected = SelectSuccessorFromState(
            world,
            state,
            target,
            startObstruction,
            avoidedObstruction,
            includeReverseFrontiers: true,
            includeTargetSideLowerHandoffs: true,
            startState: startState,
            simTicks: ref simTicks,
            preferTargetSideFrontiers: true);
        if (selected is not null)
        {
            return true;
        }

        selected = SelectSuccessorFromState(
            world,
            state,
            target,
            startObstruction,
            avoidedObstruction,
            includeReverseFrontiers: true,
            includeReachableReverseStepFrontiers: true,
            includeTargetSideLowerHandoffs: true,
            startState: startState,
            simTicks: ref simTicks,
            prioritizeTargetSideUpperFrontiers: true,
            immediateReverseStepEdgeJump: true,
            enableReverseAscendingProbes: true,
            prioritizeFrontierCandidates: true,
            allowRecedingHorizonObstructionGain: true);
        return selected is not null;
    }

    private static bool HasBoundedClearTargetContinuation(
        SimulationWorld world,
        PlayerEntity landing,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionObstruction landingObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        ref int simTicks)
    {
        var cursor = new PlayerEntity(ProbeEntityId - 20, landing.ClassDefinition, "TopologyLocalMotionLowerHandoffContinuationProbe");
        var seed = landing.CapturePredictionState();
        cursor.RestorePredictionState(in seed);
        var landingMetric = MeasureMetric(cursor.X, cursor.Bottom, target);
        var bestMetric = landingMetric;
        var sawTargetDirectedMove = false;
        for (var depth = 0; depth < LowerHandoffContinuationDepth; depth += 1)
        {
            if (MeasureMetric(cursor.X, cursor.Bottom, target) <= 0f)
            {
                return true;
            }

            var cursorObstruction = AnalyzeObstruction(world.Level, cursor, cursor.Team, target);
            if (sawTargetDirectedMove
                && bestMetric <= landingMetric - 64f
                && (!cursorObstruction.DirectPathObstructed
                    || cursorObstruction.ForwardClearance >= MathF.Max(120f, cursor.Width * 5f)))
            {
                return true;
            }

            var startState = TopologyLocalMotionState.From(cursor);
            var selected = SelectSuccessorFromState(
                world,
                cursor,
                target,
                cursorObstruction,
                avoidedObstruction,
                includeReverseFrontiers: true,
                includeTargetSideLowerHandoffs: cursorObstruction.DirectPathObstructed,
                startState: startState,
                simTicks: ref simTicks,
                includeReachableReverseStepFrontiers: true,
                prioritizeTargetSideUpperFrontiers: true,
                immediateReverseStepEdgeJump: true,
                enableReverseAscendingProbes: true,
                prioritizeFrontierCandidates: true,
                allowRecedingHorizonObstructionGain: true);
            if (selected is null)
            {
                return false;
            }

            var targetDirection = MathF.Sign(target.X - cursor.X);
            var targetDirected = targetDirection != 0f
                && (MathF.Sign(selected.MoveDirection) == targetDirection
                    || selected.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal)
                    || selected.Reasons.Contains("target_side_descent_corridor_commit", StringComparer.Ordinal)
                    || selected.Reasons.Contains("target_side_descent_entry_commit", StringComparer.Ordinal));
            var usefulReverseSetup = !targetDirected
                && selected.Reasons.Contains("useful_reverse_escape", StringComparer.Ordinal);
            if (!targetDirected && !usefulReverseSetup)
            {
                return false;
            }

            var next = SimulateSelectedMacro(
                world,
                cursor,
                target,
                selected,
                selected.CommitTicks,
                ProbeEntityId - 21 - depth);
            if (!IsValidProbeState(world, next, cursor.Team))
            {
                return false;
            }

            if (targetDirected)
            {
                sawTargetDirectedMove = true;
            }

            var nextMetric = MeasureMetric(next.X, next.Bottom, target);
            bestMetric = MathF.Min(bestMetric, nextMetric);
            var nextObstruction = AnalyzeObstruction(world.Level, next, next.Team, target);
            if (targetDirected
                && bestMetric <= landingMetric - 64f
                && (!nextObstruction.DirectPathObstructed
                    || nextObstruction.ForwardClearance >= MathF.Max(120f, next.Width * 5f)
                    || (bestMetric <= MeasureMetric(landing.X, landing.Bottom, target) - 96f
                        && nextObstruction.ForwardClearance >= landingObstruction.ForwardClearance + 96f
                        && !nextObstruction.SameObstructionFamilyAs(landingObstruction))))
            {
                return true;
            }

            if (nextObstruction.SameObstructionFamilyAs(landingObstruction)
                && nextObstruction.ForwardClearance <= MathF.Max(48f, next.Width * 2f))
            {
                return false;
            }

            cursor = next;
        }

        return false;
    }

    private static TopologyLocalMotionMacroReport? SelectSuccessorFromState(
        SimulationWorld world,
        PlayerEntity state,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction,
        bool includeReverseFrontiers,
        bool includeTargetSideLowerHandoffs,
        TopologyLocalMotionState startState,
        ref int simTicks,
        bool includeReachableReverseStepFrontiers = false,
        bool preferTargetSideFrontiers = false,
        bool prioritizeTargetSideUpperFrontiers = false,
        bool immediateReverseStepEdgeJump = false,
        bool enableReverseAscendingProbes = false,
        bool prioritizeFrontierCandidates = false,
        bool allowRecedingHorizonObstructionGain = false,
        int maxSimTicks = MaxDecisionSimTicks)
    {
        var scan = BuildScan(
            world.Level,
            state,
            state.Team,
            target,
            includeReverseFrontiers,
            includeReachableReverseStepFrontiers,
            includeTargetSideLowerHandoffs);
        var candidates = EnumerateCandidates(
                state,
                target,
                scan,
                startObstruction,
                preferTargetSideFrontiers,
                prioritizeTargetSideUpperFrontiers,
                immediateReverseStepEdgeJump,
                enableReverseAscendingProbes,
                prioritizeFrontierCandidates)
            .Select(candidate => allowRecedingHorizonObstructionGain
                ? candidate with { AllowRecedingHorizonObstructionGain = true }
                : candidate)
            .Take(MaxCandidateCount)
            .ToArray();
        var evaluations = new List<TopologyLocalMotionMacroReport>(candidates.Length);
        var localSimTicks = 0;
        TopologyLocalMotionMacroReport? best = null;
        foreach (var candidate in candidates)
        {
            if (localSimTicks + candidate.DurationTicks > maxSimTicks)
            {
                break;
            }

            localSimTicks += candidate.DurationTicks;
            var evaluation = EvaluateCandidate(world, state, target, candidate, scan, startObstruction, avoidedObstruction);
            evaluations.Add(evaluation);
            if (evaluation.Accepted && (best is null || evaluation.Score > best.Score))
            {
                best = evaluation;
            }
        }

        simTicks += localSimTicks;
        var selected = SelectMacro(evaluations, best, startObstruction, startState);
        return ExtendStableRecedingHandoff(selected, startObstruction);
    }

    private static bool IsTargetSideTopologyProgress(TopologyLocalMotionMacroReport evaluation)
    {
        var stableLanding = evaluation.FinalState.IsGrounded;
        var terminalWallRisk = HasTerminalWallRisk(evaluation);
        return evaluation.Reasons.Contains("target_band_entry_commit", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("target_side_descent_corridor_commit", StringComparer.Ordinal)
            || evaluation.Reasons.Contains("target_side_descent_entry_commit", StringComparer.Ordinal)
            || (stableLanding
                && !terminalWallRisk
                && evaluation.Reasons.Contains("useful_upper_route", StringComparer.Ordinal))
            || (stableLanding
                && !terminalWallRisk
                && evaluation.Reasons.Contains("entered_clearer_corridor", StringComparer.Ordinal)
                && evaluation.Reasons.Any(static reason => reason.StartsWith("new_surface_span:", StringComparison.Ordinal)))
            || (evaluation.Reasons.Contains("crossed_block_endpoint", StringComparer.Ordinal)
                && !evaluation.Reasons.Contains("final_hard_blocked_wall_penalty", StringComparer.Ordinal));
    }

    private void RememberFailedObstruction(TopologyLocalMotionObstruction obstruction, int tick)
    {
        if (!obstruction.DirectPathObstructed)
        {
            return;
        }

        _failedObstruction = obstruction;
        _failedObstructionUntilTick = tick + FailedObstructionMemoryTicks;
    }

    public static TopologyLocalMotionObstruction AnalyzeObstruction(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        TopologyLocalMotionTarget target) =>
        AnalyzeObstruction(level, self, team, target, ObstructionProbeDistance);

    private static TopologyLocalMotionObstruction AnalyzeObstruction(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        TopologyLocalMotionTarget target,
        float probeDistance)
    {
        var direction = (float)MathF.Sign(target.X - self.X);
        if (direction == 0f)
        {
            direction = self.FacingDirectionX == 0f ? 1f : MathF.Sign(self.FacingDirectionX);
        }

        var targetDistance = MathF.Abs(target.X - self.X);
        var probeLimit = MathF.Min(probeDistance, MathF.Max(24f, targetDistance));
        var forwardClearance = MeasureForwardClearance(level, self, team, direction, probeLimit, out var forwardBlockX);
        var directClearance = MeasureDirectLineClearance(level, self, team, target, direction, probeLimit, out var lineBlockX, out var lineBlockBottom);
        var localLimit = MathF.Min(probeDistance, targetDistance);
        var obstructed = forwardClearance < localLimit - 0.1f || directClearance < localLimit - 0.1f;
        var blockX = forwardClearance <= directClearance ? forwardBlockX : lineBlockX;
        var blockBottom = forwardClearance <= directClearance ? self.Bottom : lineBlockBottom;
        return new TopologyLocalMotionObstruction(
            Direction: direction,
            DirectPathObstructed: obstructed,
            ForwardClearance: forwardClearance,
            DirectLineClearance: directClearance,
            BlockX: blockX,
            BlockBottom: blockBottom,
            ObstructionKey: obstructed ? $"{Quantize(blockX, 16f)}:{Quantize(blockBottom, 16f)}" : "clear");
    }

    private static TopologyLocalMotionScan BuildScan(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        TopologyLocalMotionTarget target,
        bool includeObstructedReverseFrontiers,
        bool includeReachableReverseStepFrontiers = false,
        bool includeTargetSideLowerHandoffs = false)
    {
        var minX = MathF.Max(0f, self.X - ScanRadiusX);
        var maxX = MathF.Min(level.Bounds.Width, self.X + ScanRadiusX);
        var minBottom = MathF.Max(0f, self.Bottom - ScanAbove);
        var maxBottom = MathF.Min(level.Bounds.Height, self.Bottom + ScanBelow);
        var localSolids = level.Solids
            .Where(solid => solid.Right >= minX && solid.Left <= maxX && solid.Bottom >= minBottom - self.CollisionBottomOffset && solid.Top <= maxBottom)
            .Select(static solid => new TopologyLocalMotionSolidRect(solid.Left, solid.Top, solid.Right, solid.Bottom))
            .Take(96)
            .ToArray();
        var scanSummary = new TopologyLocalMotionCollisionScan(
            MinX: minX,
            MaxX: maxX,
            MinBottom: minBottom,
            MaxBottom: maxBottom,
            SampleStepX: SurfaceStepX,
            SampleStepBottom: SurfaceStepBottom,
            SolidRectangles: localSolids);

        var points = new List<(float X, float Bottom)>();
        for (var x = minX; x <= maxX + 0.01f; x += SurfaceStepX)
        {
            var bottomsAtX = 0;
            for (var bottom = minBottom; bottom <= maxBottom + 0.01f; bottom += SurfaceStepBottom)
            {
                if (!CanStandAt(level, self, team, x, bottom))
                {
                    continue;
                }

                points.Add((x, bottom));
                bottomsAtX += 1;
                if (bottomsAtX >= 5)
                {
                    break;
                }
            }
        }

        var spans = BuildSurfaceSpans(level, self, team, target, points);
        var frontiers = BuildFrontiers(
            spans,
            self,
            target,
            includeObstructedReverseFrontiers,
            includeReachableReverseStepFrontiers,
            includeTargetSideLowerHandoffs);
        return new TopologyLocalMotionScan(scanSummary, spans, frontiers);
    }

    private static IReadOnlyList<TopologyLocalMotionSurfaceSpan> BuildSurfaceSpans(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        TopologyLocalMotionTarget target,
        IReadOnlyList<(float X, float Bottom)> points)
    {
        var ordered = points
            .OrderBy(static point => MathF.Round(point.Bottom / 12f))
            .ThenBy(static point => point.X)
            .ToArray();
        var builders = new List<SurfaceSpanBuilder>();
        foreach (var point in ordered)
        {
            SurfaceSpanBuilder? match = null;
            foreach (var builder in builders)
            {
                if (point.X <= builder.LastX + SurfaceStepX + 0.1f
                    && point.X >= builder.LastX - 0.1f
                    && MathF.Abs(point.Bottom - builder.AverageBottom) <= StandTolerance)
                {
                    match = builder;
                    break;
                }
            }

            if (match is null)
            {
                match = new SurfaceSpanBuilder(point.X, point.Bottom);
                builders.Add(match);
            }
            else
            {
                match.Add(point.X, point.Bottom);
            }
        }

        var spans = new List<TopologyLocalMotionSurfaceSpan>();
        var id = 1;
        foreach (var builder in builders.Where(static builder => builder.Width >= 16f).OrderBy(static builder => builder.XMin))
        {
            var direction = (float)MathF.Sign(target.X - self.X);
            if (direction == 0f)
            {
                direction = 1f;
            }

            var blockedLeft = !self.CanOccupy(level, team, builder.XMin - 6f, builder.AverageBottom - self.CollisionBottomOffset);
            var blockedRight = !self.CanOccupy(level, team, builder.XMax + 6f, builder.AverageBottom - self.CollisionBottomOffset);
            var isCurrent = self.X >= builder.XMin - 12f
                && self.X <= builder.XMax + 12f
                && MathF.Abs(self.Bottom - builder.AverageBottom) <= StandTolerance;
            var side = MathF.Sign(builder.CenterX - self.X);
            var towardTarget = side == direction || MathF.Abs(builder.CenterX - self.X) <= 12f;
            var relation = isCurrent
                ? "current"
                : builder.AverageBottom < self.Bottom - 24f
                    ? "upper"
                    : builder.AverageBottom > self.Bottom + 24f
                        ? "lower"
                        : "level";
            spans.Add(new TopologyLocalMotionSurfaceSpan(
                Id: id,
                XMin: builder.XMin,
                XMax: builder.XMax,
                Bottom: builder.AverageBottom,
                SampleCount: builder.Count,
                Relation: relation,
                IsCurrent: isCurrent,
                TowardTarget: towardTarget,
                BlockedLeft: blockedLeft,
                BlockedRight: blockedRight));
            id += 1;
        }

        return spans;
    }

    private static IReadOnlyList<TopologyLocalMotionFrontier> BuildFrontiers(
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> spans,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        bool includeObstructedReverseFrontiers,
        bool includeReachableReverseStepFrontiers,
        bool includeTargetSideLowerHandoffs)
    {
        var direction = (float)MathF.Sign(target.X - self.X);
        if (direction == 0f)
        {
            direction = 1f;
        }

        var current = spans.FirstOrDefault(static span => span.IsCurrent);
        var frontiers = new List<TopologyLocalMotionFrontier>();
        if (current is not null)
        {
            AddFrontier(frontiers, current.Id, current.XMin, current.Bottom, "leftEdge", current.BlockedLeft, self, target);
            AddFrontier(frontiers, current.Id, current.XMax, current.Bottom, "rightEdge", current.BlockedRight, self, target);
        }

        var currentBlockedTowardTarget = current is not null
            && (direction < 0f ? current.BlockedLeft : current.BlockedRight);
        var currentPocketLike = current is not null
            && (currentBlockedTowardTarget || (current.BlockedLeft && current.BlockedRight));
        foreach (var span in spans)
        {
            if (span.IsCurrent)
            {
                continue;
            }

            var dx = span.CenterX - self.X;
            var oppositeSide = MathF.Sign(dx) != direction && MathF.Abs(dx) > 12f;
            var oppositeTargetDirection = oppositeSide && MathF.Abs(dx) > 48f;
            var reachableReverseStep = includeReachableReverseStepFrontiers
                && oppositeSide
                && self.IsGrounded
                && MathF.Abs(dx) <= 176f
                && span.XMax - span.XMin >= 16f
                && span.Bottom >= self.Bottom - 112f
                && span.Bottom <= self.Bottom + 8f
                && self.Bottom - span.Bottom >= 20f;
            var reverseStepUpCandidate = includeObstructedReverseFrontiers
                && oppositeSide
                && (!self.IsGrounded || currentPocketLike || reachableReverseStep)
                && MathF.Abs(dx) <= 176f
                && span.XMax - span.XMin >= 16f
                && span.Bottom >= self.Bottom - (includeReachableReverseStepFrontiers ? 112f : 96f)
                && span.Bottom <= self.Bottom + (self.IsGrounded ? 8f : 48f);
            var reverseUpperCandidate = includeObstructedReverseFrontiers
                && oppositeTargetDirection
                && span.Relation == "upper"
                && self.Bottom - span.Bottom >= 24f
                && !reverseStepUpCandidate;
            var targetSideLowerHandoff = includeTargetSideLowerHandoffs
                && current is not null
                && self.IsGrounded
                && span.Relation == "lower"
                && span.TowardTarget
                && MathF.Abs(dx) <= MathF.Max(160f, self.Width * 6f)
                && span.Bottom - self.Bottom is >= 12f and <= 56f
                && span.XMax - span.XMin >= MathF.Max(48f, self.Width * 2f)
                && (direction < 0f ? !span.BlockedLeft : !span.BlockedRight);
            if (oppositeTargetDirection && !reverseUpperCandidate && !reverseStepUpCandidate)
            {
                continue;
            }

            var kind = span.Relation switch
            {
                _ when reverseStepUpCandidate => "reverseStepUpLanding",
                _ when targetSideLowerHandoff => "targetSideLowerHandoff",
                "upper" => reverseUpperCandidate ? "reverseUpperLandingBand" : "upperLandingBand",
                "lower" => "lowerLandingBand",
                _ => span.TowardTarget ? "visibleSurface" : "alternateSurface",
            };
            AddFrontier(frontiers, span.Id, span.CenterX, span.Bottom, kind, isBlockedEdge: false, self, target);
        }

        return frontiers
            .OrderByDescending(static frontier => frontier.Score)
            .Take(8)
            .ToArray();
    }

    private static void AddFrontier(
        List<TopologyLocalMotionFrontier> frontiers,
        int spanId,
        float x,
        float bottom,
        string kind,
        bool isBlockedEdge,
        PlayerEntity self,
        TopologyLocalMotionTarget target)
    {
        var towardTarget = MathF.Sign(x - self.X) == MathF.Sign(target.X - self.X);
        var upperGain = MathF.Max(0f, self.Bottom - bottom);
        var targetBand = MathF.Max(0f, MathF.Abs(target.Bottom - self.Bottom) - MathF.Abs(target.Bottom - bottom));
        var score = (towardTarget ? 300f : -120f)
            + (upperGain * 4f)
            + (targetBand * 1.2f)
            - (MathF.Abs(x - self.X) * 0.25f)
            - (isBlockedEdge ? 250f : 0f)
            + (kind.Contains("Landing", StringComparison.Ordinal) ? 240f : 0f)
            + (kind.Contains("targetSideLowerHandoff", StringComparison.Ordinal) ? 1300f : 0f)
            + (kind.Contains("reverseStepUp", StringComparison.Ordinal) ? 1600f : 0f)
            + (kind.Contains("reverseUpper", StringComparison.Ordinal) ? 820f : 0f);
        frontiers.Add(new TopologyLocalMotionFrontier(spanId, x, bottom, kind, isBlockedEdge, score));
    }

    private static bool IsNearUpperWallEscapeState(PlayerEntity self, TopologyLocalMotionObstruction obstruction) =>
        obstruction.DirectPathObstructed
        && obstruction.ForwardClearance <= MathF.Max(72f, self.Width * 3f);

    private static IEnumerable<LocalMacroCandidate> EnumerateCandidates(
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionScan scan,
        TopologyLocalMotionObstruction obstruction,
        bool preferTargetSideFrontiers = false,
        bool prioritizeTargetSideUpperFrontiers = false,
        bool immediateReverseStepEdgeJump = false,
        bool enableReverseAscendingProbes = false,
        bool prioritizeFrontierCandidates = false,
        bool prioritizeLowerOverlapDrops = false)
    {
        var direction = obstruction.Direction == 0f ? MathF.Sign(target.X - self.X) : obstruction.Direction;
        if (direction == 0f)
        {
            direction = self.FacingDirectionX == 0f ? 1f : MathF.Sign(self.FacingDirectionX);
        }

        var reverse = -direction;
        yield return new LocalMacroCandidate($"runShort:{direction:0}", direction, 16, 10, int.MaxValue, 0, 0);
        yield return new LocalMacroCandidate($"runLong:{direction:0}", direction, 34, 16, int.MaxValue, 0, 0);
        yield return new LocalMacroCandidate($"jump:{direction:0}", direction, 32, 18, 0, 5, 0);
        yield return new LocalMacroCandidate($"runupJump:{direction:0}", direction, 48, 24, 7, 5, 0);
        yield return new LocalMacroCandidate($"lateRunupJump:{direction:0}", direction, 66, 30, 15, 6, 0);
        yield return new LocalMacroCandidate($"verticalJump", 0f, 28, 12, 0, 5, 0);
        yield return new LocalMacroCandidate("coast", 0f, 18, 10, int.MaxValue, 0, 0);
        yield return new LocalMacroCandidate($"brake:{reverse:0}", reverse, 18, 10, int.MaxValue, 0, 0);

        if (prioritizeFrontierCandidates && obstruction.DirectPathObstructed)
        {
            yield return new LocalMacroCandidate($"wallLipEscapeBackoffJump:{direction:0}", direction, 82, 36, 7, 6, 0, reverse, 14);
            yield return new LocalMacroCandidate($"backoffVerticalLipPop:{direction:0}", direction, 82, 38, 0, 9, 0, reverse, 14, MoveStartTick: 9, MoveEndTick: 42);
            foreach (var candidate in EnumerateBlockedTargetLowerOverlapDrops(self, target, scan, obstruction))
            {
                yield return candidate;
            }

            if (obstruction.ForwardClearance <= MathF.Max(48f, self.Width * 2f)
                && HasTargetBandLowerLanding(scan.SurfaceSpans, self, target))
            {
                yield return new LocalMacroCandidate($"targetSideDescentEntry:{direction:0}", direction, 48, 48, 14, 6, 0, reverse, 20, MoveEndTick: 24);
            }
        }

        if (prioritizeFrontierCandidates)
        {
            foreach (var candidate in EnumerateFrontierMacros())
            {
                yield return candidate;
            }
        }

        if (obstruction.DirectPathObstructed)
        {
            yield return new LocalMacroCandidate($"verticalLipPop:{direction:0}", direction, 54, 24, 0, 8, 0, MoveStartTick: 8);
            yield return new LocalMacroCandidate($"verticalLipPopLate:{direction:0}", direction, 70, 28, 0, 9, 0, MoveStartTick: 12);
            if (!prioritizeFrontierCandidates)
            {
                yield return new LocalMacroCandidate($"wallLipEscapeBackoffJump:{direction:0}", direction, 82, 36, 7, 6, 0, reverse, 14);
            }

            yield return new LocalMacroCandidate($"wallLipEscapeLateBackoffJump:{direction:0}", direction, 96, 42, 14, 6, 0, reverse, 20);
            if (!prioritizeFrontierCandidates)
            {
                yield return new LocalMacroCandidate($"backoffVerticalLipPop:{direction:0}", direction, 82, 38, 0, 9, 0, reverse, 14, MoveStartTick: 9, MoveEndTick: 42);
            }

            yield return new LocalMacroCandidate($"backoffVerticalLipPopLate:{direction:0}", direction, 96, 44, 0, 10, 0, reverse, 20, MoveStartTick: 12, MoveEndTick: 50);
            if (enableReverseAscendingProbes && HasReverseAscendingFrontier(scan.SurfaceSpans, self, target))
            {
                yield return new LocalMacroCandidate($"reverseStairRun:{reverse:0}", reverse, 26, 18, int.MaxValue, 0, 0, MoveEndTick: 26);
                yield return new LocalMacroCandidate($"reverseStairJump:{reverse:0}", reverse, 48, 30, 0, 6, 0, MoveEndTick: 44);
                yield return new LocalMacroCandidate($"reverseStairRunupJump:{reverse:0}", reverse, 62, 36, 8, 6, 0, MoveEndTick: 56);
            }

            if (obstruction.ForwardClearance <= MathF.Max(48f, self.Width * 2f)
                && HasTargetBandLowerLanding(scan.SurfaceSpans, self, target))
            {
                yield return new LocalMacroCandidate($"targetSideDescentEntry:{direction:0}", direction, 48, 48, 14, 6, 0, reverse, 20, MoveEndTick: 24);
            }

            if (prioritizeLowerOverlapDrops)
            {
                foreach (var candidate in EnumerateBlockedTargetLowerOverlapDrops(self, target, scan, obstruction))
                {
                    yield return candidate;
                }
            }

            yield return new LocalMacroCandidate($"reversePocketExit:{reverse:0}", reverse, 40, 40, int.MaxValue, 0, 0);
            yield return new LocalMacroCandidate($"reverseJumpPocketExit:{reverse:0}", reverse, 52, 46, 0, 5, 0);
        }

        if (!prioritizeFrontierCandidates)
        {
            foreach (var candidate in EnumerateFrontierMacros())
            {
                yield return candidate;
            }
        }

        IEnumerable<LocalMacroCandidate> EnumerateFrontierMacros()
        {
            var emitted = 0;
            var currentSpan = scan.SurfaceSpans.FirstOrDefault(static span => span.IsCurrent);
            foreach (var frontier in OrderFrontiersForCandidateBudget(
                self,
                target,
                scan,
                obstruction,
                preferTargetSideFrontiers,
                prioritizeTargetSideUpperFrontiers))
            {
                var frontierDirection = MathF.Sign(frontier.X - self.X);
                if (frontierDirection == 0f)
                {
                    continue;
                }

                var dx = MathF.Abs(frontier.X - self.X);
                var dy = frontier.Bottom - self.Bottom;
                var reverseStep = IsReverseStepFrontier(frontier);
                var targetSideLowerHandoff = IsTargetSideLowerHandoffFrontier(frontier);
                var targetSideUpper = IsTargetSideUpperFrontier(frontier, self, target);
                var departureEdgeDistance = currentSpan is null
                    ? float.PositiveInfinity
                    : frontierDirection > 0f
                        ? MathF.Max(0f, currentSpan.XMax - self.X)
                        : MathF.Max(0f, self.X - currentSpan.XMin);
                var duration = reverseStep
                    ? (int)Math.Clamp((dx / 3.4f) + 24f + MathF.Max(0f, -dy) * 0.12f, 26f, 48f)
                    : targetSideLowerHandoff
                        ? (int)Math.Clamp((dx / 3.6f) + 18f + (dy * 0.16f), 28f, 48f)
                    : (int)Math.Clamp((dx / 3.2f) + 26f + MathF.Max(0f, -dy) * 0.18f, 28f, 96f);
                var jumpStart = dy < -18f || (!reverseStep && frontier.Kind.Contains("Landing", StringComparison.Ordinal))
                    ? immediateReverseStepEdgeJump && reverseStep && departureEdgeDistance <= MathF.Max(44f, self.Width * 3f)
                        ? 0
                        : dx > 92f ? 8 : 0
                    : int.MaxValue;
                var jumpHold = jumpStart == int.MaxValue ? 0 : 6;
                var commitTicks = reverseStep
                    ? (int)Math.Clamp((dx / 4.5f) + 14f, 16f, 28f)
                    : targetSideLowerHandoff
                        ? duration
                    : IsReverseUpperFrontier(frontier) ? Math.Max(duration, 72) : Math.Max(MinimumCommitTicks, Math.Min(duration, 38));
                var moveEndTick = reverseStep
                    ? (int)Math.Clamp((dx / 5.5f) + 8f, 12f, 28f)
                    : IsReverseUpperFrontier(frontier)
                        ? (int)Math.Clamp(dx / 5.5f, 24f, 42f)
                        : targetSideLowerHandoff
                            ? (int)Math.Clamp((dx / 8f) + 2f, 8f, 16f)
                        : int.MaxValue;
                yield return new LocalMacroCandidate(
                    $"frontier:{frontier.Kind}:s{frontier.SpanId}:{frontierDirection:0}",
                    frontierDirection,
                    IsReverseUpperFrontier(frontier) ? Math.Max(duration, 72) : duration,
                    commitTicks,
                    jumpStart,
                    jumpHold,
                    0,
                    MoveEndTick: moveEndTick,
                    TargetSpanId: frontier.SpanId,
                    TargetFrontierKind: frontier.Kind);
                emitted += 1;
                var reverseUpper = IsReverseUpperFrontier(frontier);
                var reverseEdgeJumpFrontier = (reverseStep || reverseUpper)
                    && self.Bottom - frontier.Bottom >= MathF.Max(64f, self.Height * 2.5f);
                var openDepartureEdge = currentSpan is not null
                    && (frontierDirection < 0f ? !currentSpan.BlockedLeft : !currentSpan.BlockedRight);
                var edgeJumpFrontier = (targetSideUpper || reverseEdgeJumpFrontier)
                    && openDepartureEdge
                    && currentSpan is not null
                    && departureEdgeDistance <= MathF.Max(96f, self.Width * 4f);
                if (edgeJumpFrontier)
                {
                    var edgeJumpStart = reverseEdgeJumpFrontier
                        ? (int)Math.Clamp(departureEdgeDistance / 7f, 3f, 8f)
                        : (int)Math.Clamp((departureEdgeDistance / 5f) + 3f, 6f, 16f);
                    var edgeDuration = Math.Max(duration, 72);
                    var edgeCommitTicks = Math.Clamp(edgeJumpStart + 28, 32, Math.Min(edgeDuration, 52));
                    var edgeMoveEndTick = reverseStep || reverseUpper
                        ? Math.Min(edgeDuration, edgeJumpStart + 26)
                        : int.MaxValue;
                    var edgeJumpHold = reverseEdgeJumpFrontier ? 9 : 8;
                    yield return new LocalMacroCandidate(
                        $"frontierEdgeJump:{frontier.Kind}:s{frontier.SpanId}:{frontierDirection:0}",
                        frontierDirection,
                        edgeDuration,
                        edgeCommitTicks,
                        edgeJumpStart,
                        edgeJumpHold,
                        0,
                        MoveEndTick: edgeMoveEndTick,
                        TargetSpanId: frontier.SpanId,
                        TargetFrontierKind: frontier.Kind);
                    emitted += 1;
                }

                if (emitted >= 8)
                {
                    break;
                }
            }
        }
    }

    private static IEnumerable<LocalMacroCandidate> EnumerateBlockedTargetLowerOverlapDrops(
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionScan scan,
        TopologyLocalMotionObstruction obstruction)
    {
        if (!obstruction.DirectPathObstructed)
        {
            yield break;
        }

        var targetDirection = MathF.Sign(target.X - self.X);
        if (targetDirection == 0f)
        {
            yield break;
        }

        var current = scan.SurfaceSpans.FirstOrDefault(static span => span.IsCurrent);
        if (current is null)
        {
            yield break;
        }

        var startOnCurrentSpan = self.IsGrounded
            || (MathF.Abs(self.Bottom - current.Bottom) <= StandTolerance
                && MathF.Abs(self.VerticalSpeed) <= 80f);
        if (!startOnCurrentSpan)
        {
            yield break;
        }

        var targetEdgeBlocked = targetDirection < 0f ? current.BlockedLeft : current.BlockedRight;
        var openEdgeDirection = targetDirection < 0f
            ? current.BlockedRight ? 0f : 1f
            : current.BlockedLeft ? 0f : -1f;
        if (!targetEdgeBlocked || openEdgeDirection == 0f)
        {
            yield break;
        }

        var edgeX = openEdgeDirection > 0f ? current.XMax : current.XMin;
        var distanceToEdge = MathF.Abs(edgeX - self.X);
        if (distanceToEdge > MathF.Max(96f, self.Width * 4f))
        {
            yield break;
        }

        var minDrop = MathF.Max(96f, self.Height * 1.75f);
        var maxDrop = MathF.Max(280f, self.Height * 5.5f);
        var minLandingWidth = MathF.Max(16f, self.Width * 0.7f);
        var edgeReach = MathF.Max(48f, self.Width * 2f);
        var lower = scan.SurfaceSpans
            .Where(span =>
                !span.IsCurrent
                && span.Relation == "lower"
                && span.Bottom - current.Bottom >= minDrop
                && span.Bottom - current.Bottom <= maxDrop
                && span.XMax - span.XMin >= minLandingWidth
                && span.XMax >= current.XMin - edgeReach
                && span.XMin <= current.XMax + edgeReach
                && edgeX >= span.XMin - edgeReach
                && edgeX <= span.XMax + edgeReach)
            .OrderBy(span => span.Bottom)
            .ThenBy(span => MathF.Abs(span.CenterX - edgeX))
            .FirstOrDefault();
        if (lower is null)
        {
            yield break;
        }

        var preMoveTicks = (int)Math.Clamp((distanceToEdge / 4f) + 10f, 8f, 22f);
        var dropTicks = (int)Math.Clamp(((lower.Bottom - current.Bottom) / 5f) + 36f, 48f, 88f);
        var durationTicks = preMoveTicks + dropTicks;
        var commitTicks = durationTicks;
        var pullTicks = (int)Math.Clamp(dropTicks * 0.55f, 18f, 42f);
        var pullStartTick = (int)Math.Clamp(dropTicks * 0.18f, 8f, 16f);
        var delayedPullTicks = (int)Math.Clamp(dropTicks * 0.34f, 16f, 34f);
        var delayedPullStartTick = (int)Math.Clamp(dropTicks * 0.44f, 20f, 42f);

        yield return new LocalMacroCandidate(
            $"dropLowerOverlapCoast:s{lower.Id}:{openEdgeDirection:0}",
            0f,
            durationTicks,
            commitTicks,
            int.MaxValue,
            0,
            10,
            openEdgeDirection,
            preMoveTicks,
            TargetSpanId: lower.Id,
            TargetFrontierKind: "overlapLowerDrop");
        yield return new LocalMacroCandidate(
            $"dropLowerOverlapPull:s{lower.Id}:{openEdgeDirection:0}",
            targetDirection,
            durationTicks,
            commitTicks,
            int.MaxValue,
            0,
            10,
            openEdgeDirection,
            preMoveTicks,
            MoveEndTick: pullStartTick + pullTicks,
            MoveStartTick: pullStartTick,
            TargetSpanId: lower.Id,
            TargetFrontierKind: "overlapLowerDrop");
        yield return new LocalMacroCandidate(
            $"dropLowerOverlapDelayedPull:s{lower.Id}:{openEdgeDirection:0}",
            targetDirection,
            durationTicks,
            commitTicks,
            int.MaxValue,
            0,
            10,
            openEdgeDirection,
            preMoveTicks,
            MoveEndTick: delayedPullStartTick + delayedPullTicks,
            MoveStartTick: delayedPullStartTick,
            TargetSpanId: lower.Id,
            TargetFrontierKind: "overlapLowerDrop");
    }

    private static IEnumerable<TopologyLocalMotionFrontier> OrderFrontiersForCandidateBudget(
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionScan scan,
        TopologyLocalMotionObstruction obstruction,
        bool preferTargetSideFrontiers,
        bool prioritizeTargetSideUpperFrontiers)
    {
        var baseOrder = scan.Frontiers
            .OrderBy(frontier => obstruction.DirectPathObstructed && (IsTargetSideLowerHandoffFrontier(frontier) || IsTargetSideLowerLandingFrontier(frontier, self, target)) ? 0 : 1)
            .ThenBy(frontier => obstruction.DirectPathObstructed && IsCloseTargetSideUpperFrontier(frontier, self, target) ? 0 : 1)
            .ThenBy(frontier => obstruction.DirectPathObstructed && IsReverseStepFrontier(frontier) ? 0 : 1)
            .ThenBy(frontier => obstruction.DirectPathObstructed && IsReverseUpperFrontier(frontier) ? 0 : 1)
            .ThenBy(frontier => EstimateFrontierReachCost(self, frontier))
            .ThenByDescending(frontier => frontier.Score)
            .ToArray();
        if (obstruction.DirectPathObstructed
            && prioritizeTargetSideUpperFrontiers
            && !preferTargetSideFrontiers)
        {
            var priorityEmitted = new List<TopologyLocalMotionFrontier>();
            foreach (var frontier in scan.Frontiers
                .Where(frontier => IsTargetSideUpperFrontier(frontier, self, target))
                .OrderBy(frontier => EstimateBlockEndpointReachCost(obstruction, frontier))
                .ThenBy(frontier => EstimateFrontierReachCost(self, frontier))
                .ThenByDescending(frontier => frontier.Score)
                .Take(1))
            {
                priorityEmitted.Add(frontier);
                yield return frontier;
            }

            foreach (var frontier in scan.Frontiers
                .Where(IsReverseStepFrontier)
                .OrderBy(frontier => EstimateFrontierReachCost(self, frontier))
                .ThenByDescending(frontier => frontier.Score)
                .Take(2))
            {
                if (priorityEmitted.Contains(frontier))
                {
                    continue;
                }

                priorityEmitted.Add(frontier);
                yield return frontier;
            }

            foreach (var frontier in scan.Frontiers
                .Where(IsReverseUpperFrontier)
                .OrderBy(frontier => EstimateFrontierReachCost(self, frontier))
                .ThenByDescending(frontier => frontier.Score)
                .Take(2))
            {
                if (priorityEmitted.Contains(frontier))
                {
                    continue;
                }

                priorityEmitted.Add(frontier);
                yield return frontier;
            }

            foreach (var frontier in baseOrder)
            {
                if (priorityEmitted.Contains(frontier))
                {
                    continue;
                }

                yield return frontier;
            }

            yield break;
        }

        if (obstruction.DirectPathObstructed
            && !preferTargetSideFrontiers
            && IsTargetDirectedEdgeOpen(scan.SurfaceSpans, self, target))
        {
            var priorityEmitted = new List<TopologyLocalMotionFrontier>();
            foreach (var frontier in scan.Frontiers
                .Where(frontier => IsTargetSideUpperFrontier(frontier, self, target))
                .OrderBy(frontier => EstimateBlockEndpointReachCost(obstruction, frontier))
                .ThenBy(frontier => EstimateFrontierReachCost(self, frontier))
                .ThenByDescending(frontier => frontier.Score)
                .Take(2))
            {
                priorityEmitted.Add(frontier);
                yield return frontier;
            }

            foreach (var frontier in baseOrder)
            {
                if (priorityEmitted.Contains(frontier))
                {
                    continue;
                }

                yield return frontier;
            }

            yield break;
        }

        if (!obstruction.DirectPathObstructed || !preferTargetSideFrontiers)
        {
            if (obstruction.DirectPathObstructed
                && !self.IsGrounded
                && obstruction.ForwardClearance >= MathF.Max(120f, self.Width * 5f))
            {
                var priorityEmitted = new List<TopologyLocalMotionFrontier>();
                foreach (var frontier in scan.Frontiers
                    .Where(frontier => IsTargetSideUpperFrontier(frontier, self, target))
                    .OrderBy(frontier => EstimateBlockEndpointReachCost(obstruction, frontier))
                    .ThenBy(frontier => EstimateFrontierReachCost(self, frontier))
                    .ThenByDescending(frontier => frontier.Score)
                    .Take(2))
                {
                    priorityEmitted.Add(frontier);
                    yield return frontier;
                }

                foreach (var frontier in baseOrder)
                {
                    if (priorityEmitted.Contains(frontier))
                    {
                        continue;
                    }

                    yield return frontier;
                }

                yield break;
            }

            foreach (var frontier in baseOrder)
            {
                yield return frontier;
            }

            yield break;
        }

        var emitted = new List<TopologyLocalMotionFrontier>();
        foreach (var frontier in scan.Frontiers
            .Where(frontier => IsTargetSideFrontier(frontier, self, target))
            .OrderBy(frontier => EstimateFrontierReachCost(self, frontier))
            .ThenByDescending(frontier => frontier.Score)
            .Take(3))
        {
            emitted.Add(frontier);
            yield return frontier;
        }

        foreach (var frontier in scan.Frontiers
            .Where(IsReverseStepFrontier)
            .OrderBy(frontier => EstimateFrontierReachCost(self, frontier))
            .ThenByDescending(frontier => frontier.Score)
            .Take(3))
        {
            if (emitted.Contains(frontier))
            {
                continue;
            }

            emitted.Add(frontier);
            yield return frontier;
        }

        foreach (var frontier in baseOrder)
        {
            if (emitted.Contains(frontier))
            {
                continue;
            }

            yield return frontier;
        }
    }

    private static float EstimateFrontierReachCost(PlayerEntity self, TopologyLocalMotionFrontier frontier)
    {
        var dx = MathF.Abs(frontier.X - self.X);
        var upward = MathF.Max(0f, self.Bottom - frontier.Bottom);
        var downward = MathF.Max(0f, frontier.Bottom - self.Bottom);
        var blockedEdgePenalty = frontier.IsBlockedEdge ? 90f : 0f;
        return dx + (upward * 1.15f) + (downward * 1.8f) + blockedEdgePenalty;
    }

    private static float EstimateBlockEndpointReachCost(
        TopologyLocalMotionObstruction obstruction,
        TopologyLocalMotionFrontier frontier)
    {
        if (!obstruction.DirectPathObstructed || obstruction.Direction == 0f)
        {
            return 0f;
        }

        var frontierReachesEndpoint = obstruction.Direction > 0f
            ? frontier.X >= obstruction.BlockX - 12f
            : frontier.X <= obstruction.BlockX + 12f;
        return frontierReachesEndpoint
            ? MathF.Abs(frontier.X - obstruction.BlockX)
            : 1000f + MathF.Abs(frontier.X - obstruction.BlockX);
    }

    private static bool IsReverseUpperFrontier(TopologyLocalMotionFrontier frontier) =>
        frontier.Kind.Contains("reverseUpper", StringComparison.Ordinal);

    private static bool IsReverseStepFrontier(TopologyLocalMotionFrontier frontier) =>
        frontier.Kind.Contains("reverseStepUp", StringComparison.Ordinal);

    private static bool HasReverseAscentContinuationFrontier(
        IReadOnlyList<TopologyLocalMotionFrontier> frontiers,
        PlayerEntity landing,
        TopologyLocalMotionObstruction startObstruction)
    {
        var reverseDirection = -MathF.Sign(startObstruction.Direction);
        if (reverseDirection == 0f)
        {
            return false;
        }

        return frontiers.Any(frontier =>
            !frontier.IsBlockedEdge
            && (IsReverseStepFrontier(frontier) || IsReverseUpperFrontier(frontier))
            && MathF.Sign(frontier.X - landing.X) == reverseDirection
            && MathF.Abs(frontier.X - landing.X) <= MathF.Max(280f, landing.Width * 11f)
            && frontier.Bottom <= landing.Bottom - MathF.Max(18f, landing.Height * 0.35f));
    }

    private static bool IsTargetSideLowerHandoffFrontier(TopologyLocalMotionFrontier frontier) =>
        frontier.Kind.Contains("targetSideLowerHandoff", StringComparison.Ordinal);

    private static bool IsTargetSideLowerLandingFrontier(
        TopologyLocalMotionFrontier frontier,
        PlayerEntity self,
        TopologyLocalMotionTarget target) =>
        frontier.Kind.Equals("lowerLandingBand", StringComparison.Ordinal)
        && IsTargetSideFrontier(frontier, self, target);

    private static bool IsTargetSideUpperFrontier(
        TopologyLocalMotionFrontier frontier,
        PlayerEntity self,
        TopologyLocalMotionTarget target) =>
        frontier.Kind.Equals("upperLandingBand", StringComparison.Ordinal)
        && IsTargetSideFrontier(frontier, self, target);

    private static bool IsCloseTargetSideUpperFrontier(
        TopologyLocalMotionFrontier frontier,
        PlayerEntity self,
        TopologyLocalMotionTarget target) =>
        IsTargetSideUpperFrontier(frontier, self, target)
        && self.Bottom - frontier.Bottom >= MathF.Max(72f, self.Height * 3f)
        && MathF.Abs(frontier.X - self.X) <= MathF.Max(180f, self.Width * 7.5f);

    private static bool IsTargetDirectedEdgeOpen(
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> spans,
        PlayerEntity self,
        TopologyLocalMotionTarget target)
    {
        var direction = MathF.Sign(target.X - self.X);
        if (direction == 0f)
        {
            return false;
        }

        var current = spans.FirstOrDefault(static span => span.IsCurrent);
        return current is not null
            && (direction < 0f ? !current.BlockedLeft : !current.BlockedRight);
    }

    private static bool HasReverseAscendingFrontier(
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> spans,
        PlayerEntity self,
        TopologyLocalMotionTarget target)
    {
        var targetDirection = MathF.Sign(target.X - self.X);
        if (targetDirection == 0f)
        {
            return false;
        }

        return spans.Any(span =>
            !span.IsCurrent
            && MathF.Sign(span.CenterX - self.X) == -targetDirection
            && MathF.Abs(span.CenterX - self.X) <= MathF.Max(220f, self.Width * 8f)
            && span.Bottom <= self.Bottom + 12f
            && self.Bottom - span.Bottom >= 8f
            && self.Bottom - span.Bottom <= MathF.Max(190f, self.Height * 3.5f));
    }

    private static bool IsTargetSideFrontier(
        TopologyLocalMotionFrontier frontier,
        PlayerEntity self,
        TopologyLocalMotionTarget target)
    {
        var targetDirection = MathF.Sign(target.X - self.X);
        return targetDirection != 0f
            && MathF.Sign(frontier.X - self.X) == targetDirection
            && !frontier.IsBlockedEdge;
    }

    private static bool HasTargetSideDescentCorridor(
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> spans,
        PlayerEntity self,
        TopologyLocalMotionTarget target)
    {
        var direction = MathF.Sign(target.X - self.X);
        if (direction == 0f)
        {
            return false;
        }

        var lowerSpans = spans
            .Where(span =>
                MathF.Sign(span.CenterX - self.X) == direction
                && span.Bottom >= self.Bottom + 18f
                && span.Bottom <= self.Bottom + 360f
                && MathF.Abs(span.CenterX - self.X) <= ScanRadiusX
                && span.XMax - span.XMin >= 16f)
            .OrderBy(span => MathF.Abs(span.CenterX - self.X))
            .ToArray();
        if (lowerSpans.Length == 0)
        {
            return false;
        }

        var first = lowerSpans.FirstOrDefault(span =>
            MathF.Abs(span.CenterX - self.X) <= 240f
            && span.Bottom - self.Bottom <= 150f);
        if (first is null)
        {
            return false;
        }

        var previous = first;
        var chainLength = 1;
        foreach (var span in lowerSpans.Skip(1))
        {
            var stepX = MathF.Abs(span.CenterX - previous.CenterX);
            var stepDown = span.Bottom - previous.Bottom;
            if (stepX > 260f || stepDown < -24f || stepDown > 180f)
            {
                continue;
            }

            chainLength += 1;
            if (chainLength >= 2)
            {
                return true;
            }

            previous = span;
        }

        return false;
    }

    private static bool HasTargetBandLowerLanding(
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> spans,
        PlayerEntity self,
        TopologyLocalMotionTarget target)
    {
        return HasTargetBandLowerLandingFromState(spans, self.X, self.Bottom, self.Width, self.Height, target);
    }

    private static bool HasTargetBandLowerLandingFromState(
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> spans,
        float x,
        float bottom,
        float width,
        float height,
        TopologyLocalMotionTarget target)
    {
        var direction = MathF.Sign(target.X - x);
        if (direction == 0f)
        {
            return false;
        }

        var minDescent = MathF.Max(48f, height * 0.9f);
        var maxDescent = MathF.Max(260f, height * 5f);
        var targetBandTolerance = MathF.Max(96f, height * 2f);
        var minLandingWidth = MathF.Max(64f, width * 2.5f);
        return spans.Any(span =>
            MathF.Sign(span.CenterX - x) == direction
            && span.Bottom >= bottom + minDescent
            && span.Bottom <= bottom + maxDescent
            && MathF.Abs(span.Bottom - target.Bottom) <= targetBandTolerance
            && MathF.Abs(span.CenterX - x) <= ScanRadiusX
            && span.XMax - span.XMin >= minLandingWidth);
    }

    private static TopologyLocalMotionMacroReport EvaluateCandidate(
        SimulationWorld world,
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        LocalMacroCandidate candidate,
        TopologyLocalMotionScan scan,
        TopologyLocalMotionObstruction startObstruction,
        TopologyLocalMotionObstruction? avoidedObstruction)
    {
        var probe = new PlayerEntity(ProbeEntityId, self.ClassDefinition, "TopologyLocalMotionProbe");
        var state = self.CapturePredictionState();
        probe.RestorePredictionState(in state);

        var previousInput = default(PlayerInputSnapshot);
        var startMetric = MeasureMetric(self.X, self.Bottom, target);
        var bestMetric = startMetric;
        var evaluationBestMetric = startMetric;
        var evaluationAge = Math.Clamp(candidate.CommitTicks - 1, 0, Math.Max(0, candidate.DurationTicks - 1));
        var evaluationState = default(PlayerEntity.PredictionState);
        var hasEvaluationState = false;
        var reached = false;
        var blockReason = "none";
        var selectedPath = new List<TopologyLocalMotionPathPoint>();
        for (var age = 0; age < candidate.DurationTicks; age += 1)
        {
            var input = BuildInput(probe, target, LocalMacroPlan.From(candidate, startTick: 0), age);
            var jumpPressed = input.Up && !previousInput.Up;
            probe.Advance(input, jumpPressed, world.Level, self.Team, 1d / SimulationConfig.DefaultTicksPerSecond);
            previousInput = input;

            if (age % 4 == 0 || age == candidate.DurationTicks - 1)
            {
                selectedPath.Add(new TopologyLocalMotionPathPoint(age, probe.X, probe.Bottom));
            }

            if (!IsValidProbeState(world, probe, self.Team))
            {
                blockReason = "invalid_probe_state";
                break;
            }

            var metric = MeasureMetric(probe.X, probe.Bottom, target);
            bestMetric = MathF.Min(bestMetric, metric);
            if (age <= evaluationAge)
            {
                evaluationBestMetric = MathF.Min(evaluationBestMetric, metric);
            }

            if (age == evaluationAge)
            {
                evaluationState = probe.CapturePredictionState();
                hasEvaluationState = true;
            }

            if (MathF.Abs(probe.X - target.X) <= 44f && MathF.Abs(probe.Bottom - target.Bottom) <= 36f)
            {
                reached = true;
                if (!hasEvaluationState)
                {
                    evaluationState = probe.CapturePredictionState();
                    hasEvaluationState = true;
                }

                break;
            }
        }

        var evaluationProbe = probe;
        var evaluationHorizon = "duration";
        if (hasEvaluationState)
        {
            evaluationProbe = new PlayerEntity(ProbeEntityId - 1, self.ClassDefinition, "TopologyLocalMotionEvaluationProbe");
            evaluationProbe.RestorePredictionState(in evaluationState);
            evaluationHorizon = "commit";
        }

        var finalState = TopologyLocalMotionState.From(probe);
        var scoreState = TopologyLocalMotionState.From(evaluationProbe);
        var finalMetric = MeasureMetric(probe.X, probe.Bottom, target);
        var scoreMetric = MeasureMetric(evaluationProbe.X, evaluationProbe.Bottom, target);
        var progress = startMetric - evaluationBestMetric;
        var endpointProgress = startMetric - scoreMetric;
        var finalObstruction = AnalyzeObstruction(world.Level, probe, self.Team, target);
        var finalLookaheadObstruction = AnalyzeObstruction(world.Level, probe, self.Team, target, LowerBasinLookaheadDistance);
        var scoreObstruction = AnalyzeObstruction(world.Level, evaluationProbe, self.Team, target);
        var finalSpan = FindMatchingSpan(scan.SurfaceSpans, evaluationProbe.X, evaluationProbe.Bottom);
        var currentSpan = scan.SurfaceSpans.FirstOrDefault(static span => span.IsCurrent);
        var targetedFrontier = candidate.TargetSpanId > 0;
        var reachedTargetSpan = targetedFrontier && finalSpan?.Id == candidate.TargetSpanId;
        var missedTargetSpan = targetedFrontier && !reachedTargetSpan;
        var clearanceGain = scoreObstruction.ForwardClearance - startObstruction.ForwardClearance;
        var upperBandGain = MathF.Max(0f, self.Bottom - evaluationProbe.Bottom);
        var descent = evaluationProbe.Bottom - self.Bottom;
        var finalDescent = probe.Bottom - self.Bottom;
        var intendedUpperFrontier = candidate.TargetFrontierKind.Contains("upperLandingBand", StringComparison.OrdinalIgnoreCase);
        var intendedLowerFrontier = candidate.TargetFrontierKind.Contains("lowerLandingBand", StringComparison.OrdinalIgnoreCase);
        var missedUpperFrontier = intendedUpperFrontier && (missedTargetSpan || descent > 24f);
        var missedLowerFrontier = intendedLowerFrontier && missedTargetSpan;
        var nearLowerBasinBlock = finalLookaheadObstruction.DirectPathObstructed
            && MathF.Min(finalLookaheadObstruction.ForwardClearance, finalLookaheadObstruction.DirectLineClearance) < 180f;
        var fullRolloutLowerBasin = finalDescent > 96f
            && nearLowerBasinBlock
            && !reached;
        var finalEndpointHardBlockedWall = finalObstruction.DirectPathObstructed
            && finalObstruction.ForwardClearance <= 32f
            && finalObstruction.DirectLineClearance <= 32f
            && !reached;
        var finalHardBlockedWall = finalEndpointHardBlockedWall
            && scoreObstruction.DirectPathObstructed
            && scoreObstruction.ForwardClearance <= 32f
            && !reached;
        var riskyLowerDescent = descent > 96f
            && !reached
            && (candidate.Label.Contains("lowerLandingBand", StringComparison.Ordinal)
                || scoreObstruction.DirectPathObstructed
                || scoreObstruction.ForwardClearance < 180f);
        var newSpan = finalSpan is not null && (currentSpan is null || finalSpan.Id != currentSpan.Id);
        var airborneSettle = !self.IsGrounded
            && evaluationProbe.IsGrounded
            && descent >= 4f
            && descent <= MathF.Max(40f, self.Height * 1.5f)
            && blockReason == "none"
            && !reached;
        var movingTowardTarget = MathF.Sign(candidate.MoveDirection) != 0f
            && MathF.Sign(candidate.MoveDirection) == startObstruction.Direction;
        var actuallyAdvancingTowardTarget = MathF.Sign(evaluationProbe.X - self.X) == startObstruction.Direction
            && MathF.Abs(evaluationProbe.X - self.X) >= 16f;
        var velocityManagedDescent = !movingTowardTarget
            && actuallyAdvancingTowardTarget
            && (!self.IsGrounded || MathF.Abs(self.HorizontalSpeed) >= 80f);
        var maxTargetSideDescent = velocityManagedDescent ? 140f : 80f;
        var targetSideDescentCorridor = (!startObstruction.DirectPathObstructed
                || MathF.Min(startObstruction.ForwardClearance, startObstruction.DirectLineClearance) >= MathF.Max(72f, self.Width * 2.5f))
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && HasTargetSideDescentCorridor(scan.SurfaceSpans, self, target)
            && descent > 8f
            && descent <= maxTargetSideDescent
            && finalDescent <= 140f
            && endpointProgress >= 18f
            && (finalDescent < 24f
                || startObstruction.DirectPathObstructed
                || !finalObstruction.DirectPathObstructed
                || finalObstruction.ForwardClearance >= MathF.Max(180f, self.Width * 7.5f))
            && (!finalEndpointHardBlockedWall || velocityManagedDescent)
            && (!finalHardBlockedWall || velocityManagedDescent)
            && !reached;
        var crossedBlockEndpoint = startObstruction.DirectPathObstructed
            && startObstruction.Direction != 0f
            && ((startObstruction.Direction > 0f && evaluationProbe.X > startObstruction.BlockX + 20f)
                || (startObstruction.Direction < 0f && evaluationProbe.X < startObstruction.BlockX - 20f));
        var shiftedObstructionSegment = startObstruction.DirectPathObstructed
            && scoreObstruction.DirectPathObstructed
            && !scoreObstruction.SameObstructionAs(startObstruction)
            && scoreObstruction.ForwardClearance >= 48f
            && endpointProgress >= 18f;
        var usefulShiftedObstructionSegment = shiftedObstructionSegment
            && (clearanceGain >= 0f
                || upperBandGain >= 24f
                || crossedBlockEndpoint);
        var targetSideDescentEntry = candidate.Label.StartsWith("targetSideDescentEntry:", StringComparison.Ordinal)
            && startObstruction.DirectPathObstructed
            && startObstruction.ForwardClearance <= MathF.Max(48f, self.Width * 2f)
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && descent >= MathF.Max(48f, self.Height * 0.9f)
            && descent <= MathF.Max(260f, self.Height * 5f)
            && newSpan
            && !scoreObstruction.DirectPathObstructed
            && !finalEndpointHardBlockedWall
            && HasTargetBandLowerLanding(scan.SurfaceSpans, self, target)
            && !reached;
        var targetSideLowerHandoffCommit = candidate.TargetFrontierKind.Contains("targetSideLowerHandoff", StringComparison.Ordinal)
            && startObstruction.DirectPathObstructed
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && reachedTargetSpan
            && newSpan
            && descent is >= 8f and <= 72f
            && finalDescent <= 88f
            && finalObstruction.DirectPathObstructed
            && !crossedBlockEndpoint
            && shiftedObstructionSegment
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !reached;
        var targetSideLowerHandoffClearDrop = candidate.TargetFrontierKind.Contains("targetSideLowerHandoff", StringComparison.Ordinal)
            && startObstruction.DirectPathObstructed
            && (!finalObstruction.DirectPathObstructed || crossedBlockEndpoint);
        var targetBandEntry = startObstruction.DirectPathObstructed
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && finalMetric <= MathF.Max(96f, self.Width * 3f)
            && bestMetric <= MathF.Max(96f, self.Width * 3f)
            && (!scoreObstruction.DirectPathObstructed
                || scoreObstruction.ForwardClearance >= startObstruction.ForwardClearance + 64f)
            && !reached;
        var clearNearTargetApproach = !startObstruction.DirectPathObstructed
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && finalMetric <= MathF.Max(180f, self.Width * 7.5f)
            && progress >= 24f
            && MathF.Abs(scoreState.Bottom - target.Bottom) <= MathF.Max(72f, self.Height * 1.5f)
            && !finalHardBlockedWall
            && !reached;
        var clearAirborneGroundingHandoff = !startObstruction.DirectPathObstructed
            && !self.IsGrounded
            && evaluationHorizon == "commit"
            && evaluationProbe.IsGrounded
            && newSpan
            && descent >= 12f
            && descent <= MathF.Max(120f, self.Height * 4f)
            && endpointProgress >= 18f
            && finalObstruction.DirectPathObstructed
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetBandEntry
            && !airborneSettle
            && (!scoreObstruction.DirectPathObstructed
                || scoreObstruction.ForwardClearance >= MathF.Max(160f, self.Width * 6f))
            && !reached;
        var unstableClearAirborneDropIntoObstruction = !startObstruction.DirectPathObstructed
            && !self.IsGrounded
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && finalDescent >= 12f
            && finalObstruction.DirectPathObstructed
            && MathF.Min(finalObstruction.ForwardClearance, finalObstruction.DirectLineClearance) <= MathF.Max(132f, self.Width * 5.5f)
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !clearNearTargetApproach
            && !targetBandEntry
            && !clearAirborneGroundingHandoff
            && !reached;
        var lostUpperReliefToObstructedRollout = startObstruction.DirectPathObstructed
            && evaluationHorizon == "commit"
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && upperBandGain >= 28f
            && finalState.IsGrounded
            && finalState.Bottom >= scoreState.Bottom + 36f
            && finalObstruction.DirectPathObstructed
            && (finalHardBlockedWall || finalEndpointHardBlockedWall)
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetSideLowerHandoffCommit
            && !targetBandEntry
            && !reached;
        var clearStartRolloutIntoObstructedLowerBasin = !startObstruction.DirectPathObstructed
            && evaluationHorizon == "commit"
            && finalState.IsGrounded
            && finalDescent >= 24f
            && finalObstruction.DirectPathObstructed
            && MathF.Min(finalObstruction.ForwardClearance, finalObstruction.DirectLineClearance) <= MathF.Max(180f, self.Width * 7.5f)
            && finalMetric >= MathF.Max(320f, self.Width * 12f)
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetBandEntry
            && !clearAirborneGroundingHandoff
            && !reached;
        var visibleCorridor = startObstruction.DirectPathObstructed
            && (!scoreObstruction.DirectPathObstructed || scoreObstruction.ForwardClearance >= startObstruction.ForwardClearance + 64f);
        var overlapLowerDropCandidate = candidate.TargetFrontierKind.Contains("overlapLowerDrop", StringComparison.Ordinal);
        var overlapLowerDropFollowupVisible = overlapLowerDropCandidate
            && evaluationProbe.IsGrounded
            && HasTargetBandLowerLandingFromState(scan.SurfaceSpans, evaluationProbe.X, evaluationProbe.Bottom, self.Width, self.Height, target);
        var startOnCurrentSpan = currentSpan is not null
            && (self.IsGrounded
                || (MathF.Abs(self.Bottom - currentSpan.Bottom) <= StandTolerance
                    && MathF.Abs(self.VerticalSpeed) <= 80f));
        var overlapLowerDropHandoff = overlapLowerDropCandidate
            && startObstruction.DirectPathObstructed
            && startOnCurrentSpan
            && evaluationHorizon == "commit"
            && evaluationProbe.IsGrounded
            && reachedTargetSpan
            && newSpan
            && descent >= MathF.Max(96f, self.Height * 1.75f)
            && descent <= MathF.Max(300f, self.Height * 5.8f)
            && (overlapLowerDropFollowupVisible
                || visibleCorridor
                || shiftedObstructionSegment
                || clearanceGain >= 24f
                || !scoreObstruction.DirectPathObstructed)
            && !reached;
        var missedOverlapLowerDrop = overlapLowerDropCandidate
            && !overlapLowerDropHandoff
            && !reached;
        var upperGainChangesTopology = upperBandGain >= 28f
            && (newSpan || crossedBlockEndpoint || visibleCorridor || clearanceGain >= 32f);
        var movingAgainstTarget = MathF.Sign(candidate.MoveDirection) != 0f
            && MathF.Sign(candidate.MoveDirection) != startObstruction.Direction;
        var reverseAscendingStepEscape = candidate.Label.StartsWith("reverseStair", StringComparison.Ordinal)
            && movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && upperBandGain >= 12f
            && finalDescent <= 48f
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && (newSpan || visibleCorridor || clearanceGain >= 24f)
            && !riskyLowerDescent
            && !reached;
        var reverseStepUpFrontier = candidate.TargetFrontierKind.Contains("reverseStepUp", StringComparison.Ordinal);
        var reverseStepUpTopologyChange = movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && reverseStepUpFrontier
            && (reachedTargetSpan || newSpan)
            && descent <= (self.IsGrounded ? 8f : 56f)
            && finalDescent <= 96f
            && !finalHardBlockedWall
            && !reached;
        var lowerDescent = MathF.Max(0f, descent - 24f);
        var deepLowerDescent = MathF.Max(0f, descent - 72f);
        var lowerDescentPenalty = reached
            ? 0f
            : targetSideDescentCorridor
                ? (lowerDescent * 1.25f) + (deepLowerDescent * 2f)
                : (lowerDescent * (startObstruction.DirectPathObstructed ? 2.25f : 5.5f))
                    + (deepLowerDescent * (startObstruction.DirectPathObstructed ? 4.5f : 8f));
        if (overlapLowerDropHandoff)
        {
            lowerDescentPenalty = MathF.Min(lowerDescentPenalty, (lowerDescent * 1.1f) + (deepLowerDescent * 1.8f));
        }

        var finalEndpointProgress = startMetric - finalMetric;
        var transientUpperReliefNoExit = startObstruction.DirectPathObstructed
            && evaluationHorizon == "commit"
            && upperBandGain >= 24f
            && finalState.IsGrounded
            && MathF.Abs(probe.X - self.X) <= MathF.Max(18f, self.Width)
            && finalEndpointProgress <= 8f
            && finalObstruction.DirectPathObstructed
            && finalObstruction.SameObstructionFamilyAs(startObstruction)
            && finalObstruction.ForwardClearance <= startObstruction.ForwardClearance + 48f
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetBandEntry
            && !reached;
        var stableAwayRecedingHandoff = movingAgainstTarget
            && probe.IsGrounded
            && finalEndpointProgress >= -64f
            && finalDescent <= 56f
            && finalObstruction.DirectPathObstructed
            && finalObstruction.ForwardClearance >= 48f;
        var recedingHorizonObstructionGainCandidate = candidate.AllowRecedingHorizonObstructionGain
            && startObstruction.DirectPathObstructed
            && evaluationHorizon == "commit"
            && !evaluationProbe.IsGrounded
            && clearanceGain >= 32f
            && upperBandGain >= 8f
            && !reached;
        var recedingHorizonObstructionGain = recedingHorizonObstructionGainCandidate
            && (!movingAgainstTarget || endpointProgress >= -64f || crossedBlockEndpoint || stableAwayRecedingHandoff);
        var rejectedRegressiveRecedingGain = recedingHorizonObstructionGainCandidate
            && !recedingHorizonObstructionGain;
        var reverseDirection = MathF.Sign(candidate.MoveDirection);
        var sameSurfaceEdgePositioning = currentSpan is not null
            && reverseDirection != 0f
            && movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && avoidedObstruction is not null
            && self.IsGrounded
            && evaluationProbe.IsGrounded
            && MathF.Abs(evaluationProbe.Bottom - currentSpan.Bottom) <= 12f
            && MathF.Abs(probe.Bottom - currentSpan.Bottom) <= 12f
            && probe.X >= currentSpan.XMin - 12f
            && probe.X <= currentSpan.XMax + 12f
            && (reverseDirection < 0f ? !currentSpan.BlockedLeft : !currentSpan.BlockedRight)
            && (reverseDirection < 0f
                ? probe.X - currentSpan.XMin <= MathF.Max(18f, self.Width)
                : probe.X - currentSpan.XMax >= -MathF.Max(18f, self.Width))
            && scan.Frontiers.Any(frontier =>
                MathF.Sign(frontier.X - self.X) == reverseDirection
                && (frontier.Kind.Contains("reverseUpper", StringComparison.Ordinal)
                    || frontier.Kind.Contains("reverseStepUp", StringComparison.Ordinal))
                && frontier.Bottom <= self.Bottom + 12f)
            && finalDescent <= 24f
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !fullRolloutLowerBasin
            && !reached;
        var safeAirborneLandingHandoff = !self.IsGrounded
            && evaluationHorizon == "commit"
            && evaluationProbe.IsGrounded
            && finalState.IsGrounded
            && newSpan
            && descent <= MathF.Max(120f, self.Height * 4f)
            && finalDescent <= MathF.Max(140f, self.Height * 4.5f)
            && !scoreObstruction.DirectPathObstructed
            && !finalObstruction.DirectPathObstructed
            && !fullRolloutLowerBasin
            && !reached;
        var stableUpperFrontierHandoff = startObstruction.DirectPathObstructed
            && intendedUpperFrontier
            && evaluationHorizon == "commit"
            && evaluationProbe.IsGrounded
            && newSpan
            && upperBandGain >= 24f
            && finalState.IsGrounded
            && finalState.Bottom <= scoreState.Bottom + 16f
            && finalDescent <= 24f
            && !fullRolloutLowerBasin
            && !reached;
        var obstructionChanged = reached
            || !startObstruction.DirectPathObstructed
            || visibleCorridor
            || crossedBlockEndpoint
            || usefulShiftedObstructionSegment
            || clearanceGain >= 32f
            || upperGainChangesTopology
            || reverseAscendingStepEscape
            || reverseStepUpTopologyChange
            || targetSideDescentCorridor
            || targetSideDescentEntry
            || targetSideLowerHandoffCommit
            || targetBandEntry
            || overlapLowerDropHandoff
            || recedingHorizonObstructionGain
            || sameSurfaceEdgePositioning
            || safeAirborneLandingHandoff
            || clearAirborneGroundingHandoff
            || stableUpperFrontierHandoff
            || airborneSettle
            || newSpan;
        var commitEscapesLowerBasin = upperBandGain >= 28f
            || visibleCorridor
            || crossedBlockEndpoint
            || usefulShiftedObstructionSegment
            || clearanceGain >= 64f
            || reverseAscendingStepEscape
            || reverseStepUpTopologyChange
            || targetSideDescentCorridor
            || targetSideDescentEntry
            || targetSideLowerHandoffCommit
            || targetBandEntry
            || overlapLowerDropHandoff
            || recedingHorizonObstructionGain
            || sameSurfaceEdgePositioning
            || clearAirborneGroundingHandoff
            || stableUpperFrontierHandoff;
        fullRolloutLowerBasin = fullRolloutLowerBasin && !commitEscapesLowerBasin;
        var upperFrontierCommit = intendedUpperFrontier
            && (reachedTargetSpan || stableUpperFrontierHandoff)
            && upperBandGain >= 18f
            && (!finalHardBlockedWall || stableUpperFrontierHandoff);
        var usefulUpperRoute = upperBandGain >= 28f
            && finalDescent <= 36f
            && !finalEndpointHardBlockedWall
            && !lostUpperReliefToObstructedRollout
            && (crossedBlockEndpoint || visibleCorridor || clearanceGain >= 32f || newSpan || endpointProgress >= 18f);
        var clearStartUpperRouteAfterFailedObstruction = !startObstruction.DirectPathObstructed
            && avoidedObstruction is not null
            && movingTowardTarget
            && usefulUpperRoute
            && upperBandGain >= 24f
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !fullRolloutLowerBasin
            && !clearStartRolloutIntoObstructedLowerBasin
            && !unstableClearAirborneDropIntoObstruction
            && !reached;
        var persistentObstructedLowerRollout = startObstruction.DirectPathObstructed
            && finalDescent >= 42f
            && finalObstruction.DirectPathObstructed
            && upperBandGain < 24f
            && !upperFrontierCommit
            && !reverseStepUpTopologyChange
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetSideLowerHandoffCommit
            && !targetBandEntry
            && !overlapLowerDropHandoff
            && !airborneSettle
            && !visibleCorridor
            && clearanceGain < 48f
            && !reached;
        var stationaryVerticalHop = MathF.Abs(evaluationProbe.X - self.X) < 42f
            && MathF.Abs(candidate.MoveDirection) < 0.01f
            && !startObstruction.DirectPathObstructed
            && !newSpan
            && upperBandGain >= 24f
            && !reached;
        var reverseUpperFrontier = candidate.TargetFrontierKind.Contains("reverseUpper", StringComparison.Ordinal);
        var reverseUpperFollowupVerified = reverseUpperFrontier
            && HasBoundedTargetFollowupImprovement(world, evaluationProbe, self.Team, target, startObstruction);
        var reverseStepFollowupVerified = reverseStepUpFrontier
            && HasBoundedTargetFollowupImprovement(
                world,
                evaluationProbe,
                self.Team,
                target,
                startObstruction,
                requireSimulatedTargetMove: true);
        var clearStartVerifiedUpperDetour = !startObstruction.DirectPathObstructed
            && movingAgainstTarget
            && reverseUpperFrontier
            && reverseUpperFollowupVerified
            && evaluationProbe.IsGrounded
            && finalState.IsGrounded
            && newSpan
            && upperBandGain >= 24f
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !fullRolloutLowerBasin
            && !riskyLowerDescent
            && !reached;
        var reverseStepRecedingUpperHandoff = reverseStepUpTopologyChange
            && evaluationHorizon == "commit"
            && upperBandGain >= 24f
            && clearanceGain >= 64f
            && scoreObstruction.DirectPathObstructed
            && scoreObstruction.ForwardClearance >= MathF.Max(120f, self.Width * 5f)
            && (!finalObstruction.DirectPathObstructed
                || finalMetric <= startMetric + 64f
                || reverseStepFollowupVerified)
            && !fullRolloutLowerBasin
            && !reached;
        var reverseStepSetupPositioning = reverseStepUpTopologyChange
            && finalState.IsGrounded
            && newSpan
            && upperBandGain >= 32f
            && clearanceGain >= 48f
            && finalDescent <= 8f
            && (!finalObstruction.DirectPathObstructed
                || finalMetric <= startMetric + 64f
                || reverseStepFollowupVerified)
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !fullRolloutLowerBasin
            && !riskyLowerDescent
            && !reached;
        var reverseStepAscentChainContinuation = reverseStepUpTopologyChange
            && finalState.IsGrounded
            && newSpan
            && upperBandGain >= 24f
            && clearanceGain >= 24f
            && finalDescent <= 24f
            && HasReverseAscentContinuationFrontier(scan.Frontiers, probe, startObstruction)
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !fullRolloutLowerBasin
            && !riskyLowerDescent
            && !reached;
        var reverseUpperEscape = movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && IsNearUpperWallEscapeState(self, startObstruction)
            && reverseUpperFollowupVerified
            && newSpan
            && upperBandGain >= 36f
            && descent <= -28f
            && !finalEndpointHardBlockedWall
            && !fullRolloutLowerBasin
            && !reached;
        var reverseUpperRelocationEscape = movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && usefulUpperRoute
            && newSpan
            && finalState.IsGrounded
            && upperBandGain >= 40f
            && finalDescent <= 24f
            && (!finalObstruction.SameObstructionFamilyAs(startObstruction)
                || HasReverseAscentContinuationFrontier(scan.Frontiers, probe, startObstruction))
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !fullRolloutLowerBasin
            && !riskyLowerDescent
            && !reached;
        var reverseStepNeedsVerifiedContinuation = reverseStepUpTopologyChange
            && movingAgainstTarget
            && finalObstruction.DirectPathObstructed
            && finalMetric > startMetric + 64f;
        var reverseStepUpEscape = reverseStepUpTopologyChange
            && (reverseStepFollowupVerified
                || reverseStepRecedingUpperHandoff
                || reverseStepSetupPositioning
                || reverseStepAscentChainContinuation
                || visibleCorridor
                || crossedBlockEndpoint
                || clearanceGain >= 64f
                || (finalState.IsGrounded && upperBandGain >= 48f && clearanceGain >= 32f))
            && (!reverseStepNeedsVerifiedContinuation || reverseStepFollowupVerified || reverseStepAscentChainContinuation)
            && (!finalEndpointHardBlockedWall || reverseStepFollowupVerified || reverseStepRecedingUpperHandoff || reverseStepSetupPositioning || reverseStepAscentChainContinuation)
            && !fullRolloutLowerBasin;
        var unstableUpperReliefHandoff = evaluationHorizon == "commit"
            && !evaluationProbe.IsGrounded
            && upperBandGain >= 24f
            && finalState.IsGrounded
            && finalState.Bottom >= scoreState.Bottom + 24f
            && MathF.Abs(finalState.Bottom - target.Bottom) <= MathF.Max(96f, self.Height * 2f)
            && finalObstruction.DirectPathObstructed
            && finalObstruction.ForwardClearance <= MathF.Max(96f, self.Width * 4f)
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetSideLowerHandoffCommit
            && !targetBandEntry
            && !upperFrontierCommit
            && !reverseStepUpEscape
            && !reverseUpperEscape
            && !recedingHorizonObstructionGain
            && !reached;
        var targetSideDescentAirborneObstructedBand = targetSideDescentCorridor
            && evaluationHorizon == "commit"
            && !finalState.IsGrounded
            && finalObstruction.DirectPathObstructed
            && MathF.Min(finalObstruction.ForwardClearance, finalObstruction.DirectLineClearance) <= MathF.Max(96f, self.Width * 4f)
            && MathF.Abs(finalState.Bottom - target.Bottom) <= MathF.Max(96f, self.Height * 2f)
            && !targetBandEntry
            && !reached;
        var unverifiedReverseStepEscape = reverseStepUpTopologyChange && !reverseStepUpEscape;
        var reverseAscendingAlternativeAvailable = movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && scan.Frontiers.Any(frontier =>
                (IsReverseStepFrontier(frontier) || IsReverseUpperFrontier(frontier))
                && MathF.Sign(frontier.X - self.X) == -startObstruction.Direction
                && self.Bottom - frontier.Bottom >= MathF.Max(24f, self.Height));
        var reverseDescentIntoObstructedSpan = reverseAscendingAlternativeAvailable
            && descent > 36f
            && finalState.IsGrounded
            && finalObstruction.DirectPathObstructed
            && finalState.Bottom >= self.Bottom + 24f
            && !reached;
        var verifiedReverseUpperDetour = movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && reverseUpperFrontier
            && reverseUpperFollowupVerified
            && (visibleCorridor || clearanceGain >= 24f || newSpan)
            && descent <= 24f
            && finalDescent <= 24f
            && !finalEndpointHardBlockedWall
            && !finalHardBlockedWall
            && !fullRolloutLowerBasin
            && !reverseDescentIntoObstructedSpan
            && !reached;
        var directReverseEscape = startObstruction.ForwardClearance <= MathF.Max(80f, self.Width * 3.25f)
            && (clearanceGain >= 48f || visibleCorridor || newSpan)
            && descent <= 96f
            && !finalEndpointHardBlockedWall
            && !reverseDescentIntoObstructedSpan;
        var usefulReverseEscape = movingAgainstTarget
            && startObstruction.DirectPathObstructed
            && (directReverseEscape
            || reverseAscendingStepEscape
            || reverseStepUpEscape
            || reverseUpperEscape
            || reverseUpperRelocationEscape
            || verifiedReverseUpperDetour
            || targetSideDescentCorridor
            || sameSurfaceEdgePositioning)
            && !reached;
        var missedUpperFrontierButVerifiedEscape = missedUpperFrontier
            && (reverseStepUpEscape
                || reverseUpperEscape
                || verifiedReverseUpperDetour
                || stableUpperFrontierHandoff
                || clearStartVerifiedUpperDetour
                || targetSideDescentCorridor
                || targetSideDescentEntry
                || targetSideLowerHandoffCommit
                || targetBandEntry
                || overlapLowerDropHandoff
                || safeAirborneLandingHandoff);
        var targetSideObstructedRegression = startObstruction.DirectPathObstructed
            && (movingTowardTarget || actuallyAdvancingTowardTarget)
            && scoreObstruction.DirectPathObstructed
            && finalObstruction.DirectPathObstructed
            && !usefulShiftedObstructionSegment
            && !crossedBlockEndpoint
            && !visibleCorridor
            && !usefulUpperRoute
            && !upperFrontierCommit
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetSideLowerHandoffCommit
            && !targetBandEntry
            && !overlapLowerDropHandoff
            && !safeAirborneLandingHandoff
            && !stableUpperFrontierHandoff
            && (clearanceGain < 0f
                || finalDescent >= 24f
                || finalEndpointHardBlockedWall
                || scoreObstruction.ForwardClearance < MathF.Max(72f, self.Width * 3f))
            && !reached;
        riskyLowerDescent = riskyLowerDescent && !targetSideDescentEntry;
        riskyLowerDescent = riskyLowerDescent && !targetBandEntry;
        riskyLowerDescent = riskyLowerDescent && !overlapLowerDropHandoff;
        riskyLowerDescent = riskyLowerDescent && !usefulReverseEscape;
        riskyLowerDescent = riskyLowerDescent && !clearStartVerifiedUpperDetour;
        var targetDirectedShiftAfterEscape = movingTowardTarget
            && endpointProgress >= 8f
            && (usefulShiftedObstructionSegment || crossedBlockEndpoint || clearanceGain >= 24f);
        var returnsToAvoidedObstruction = startObstruction.DirectPathObstructed
            && avoidedObstruction is not null
            && !usefulReverseEscape
            && !upperFrontierCommit
            && !usefulUpperRoute
            && !targetSideDescentEntry
            && !targetSideLowerHandoffCommit
            && !targetBandEntry
            && !overlapLowerDropHandoff
            && !airborneSettle
            && !recedingHorizonObstructionGain
            && !targetDirectedShiftAfterEscape
            && !reached
            && (scoreObstruction.SameObstructionAs(avoidedObstruction)
                || finalObstruction.SameObstructionAs(avoidedObstruction)
                || scoreObstruction.SameObstructionFamilyAs(avoidedObstruction)
                || finalObstruction.SameObstructionFamilyAs(avoidedObstruction));
        var lowClearanceAfterRecentEscape = MathF.Max(48f, self.Width * 2f);
        var recyclesAvoidedObstructionColumn = startObstruction.DirectPathObstructed
            && avoidedObstruction is not null
            && !usefulReverseEscape
            && !upperFrontierCommit
            && !usefulUpperRoute
            && !targetSideDescentEntry
            && !targetSideLowerHandoffCommit
            && !targetBandEntry
            && !overlapLowerDropHandoff
            && !safeAirborneLandingHandoff
            && !stableUpperFrontierHandoff
            && !airborneSettle
            && !velocityManagedDescent
            && !reached
            && ((scoreObstruction.SameObstructionColumnAs(avoidedObstruction)
                    && scoreObstruction.ForwardClearance <= lowClearanceAfterRecentEscape)
                || (finalObstruction.SameObstructionColumnAs(avoidedObstruction)
                    && finalObstruction.ForwardClearance <= lowClearanceAfterRecentEscape));
        var reverseEndpointRegression = startObstruction.DirectPathObstructed
            && movingAgainstTarget
            && endpointProgress < -8f
            && !usefulReverseEscape
            && !reached;
        var reverseLowerPocketDescent = startObstruction.DirectPathObstructed
            && movingAgainstTarget
            && descent > 36f
            && scoreObstruction.DirectPathObstructed
            && !visibleCorridor
            && !usefulReverseEscape
            && !reached;
        var weakReverseDetour = startObstruction.DirectPathObstructed
            && movingAgainstTarget
            && startObstruction.ForwardClearance > 48f
            && clearanceGain < 64f
            && upperBandGain < 32f
            && !crossedBlockEndpoint
            && !visibleCorridor
            && !usefulReverseEscape;
        var pressesSameObstruction = startObstruction.DirectPathObstructed
            && MathF.Sign(candidate.MoveDirection) == startObstruction.Direction
            && scoreObstruction.SameObstructionAs(startObstruction)
            && scoreObstruction.ForwardClearance <= startObstruction.ForwardClearance + 10f
            && upperBandGain < 22f
            && !crossedBlockEndpoint
            && !overlapLowerDropHandoff;
        var transientVerticalRelief = startObstruction.DirectPathObstructed
            && finalEndpointHardBlockedWall
            && upperBandGain >= 24f
            && !newSpan
            && !crossedBlockEndpoint
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetBandEntry
            && !reverseUpperEscape
            && !reverseStepUpEscape
            && !reached;
        var neutralVerticalReliefNoTopologyChange = startObstruction.DirectPathObstructed
            && MathF.Abs(candidate.MoveDirection) < 0.01f
            && evaluationHorizon == "commit"
            && !evaluationProbe.IsGrounded
            && upperBandGain >= 24f
            && !newSpan
            && finalObstruction.DirectPathObstructed
            && !targetSideDescentCorridor
            && !targetSideDescentEntry
            && !targetSideLowerHandoffCommit
            && !targetBandEntry
            && !overlapLowerDropHandoff
            && !safeAirborneLandingHandoff
            && !clearAirborneGroundingHandoff
            && !stableUpperFrontierHandoff
            && !airborneSettle
            && !reached;
        var accepted = blockReason == "none"
            && (reached
                || (startObstruction.DirectPathObstructed
                    ? obstructionChanged && !pressesSameObstruction && !weakReverseDetour && !reverseLowerPocketDescent && !targetSideObstructedRegression && !transientVerticalRelief && !neutralVerticalReliefNoTopologyChange && !transientUpperReliefNoExit && !unstableUpperReliefHandoff && !targetSideDescentAirborneObstructedBand && !unverifiedReverseStepEscape && !targetSideLowerHandoffClearDrop && !missedOverlapLowerDrop && (!missedUpperFrontier || missedUpperFrontierButVerifiedEscape) && !missedLowerFrontier && !riskyLowerDescent && !fullRolloutLowerBasin && !lostUpperReliefToObstructedRollout && !returnsToAvoidedObstruction && !recyclesAvoidedObstructionColumn
                    : (progress >= 14f || endpointProgress >= 18f || newSpan || upperBandGain >= 24f || targetSideDescentCorridor || clearNearTargetApproach) && !missedOverlapLowerDrop && (!missedUpperFrontier || missedUpperFrontierButVerifiedEscape) && !missedLowerFrontier && !riskyLowerDescent && !fullRolloutLowerBasin && !unstableClearAirborneDropIntoObstruction && !clearStartRolloutIntoObstructedLowerBasin && !unstableUpperReliefHandoff && !targetSideDescentAirborneObstructedBand && !lostUpperReliefToObstructedRollout && !returnsToAvoidedObstruction && !recyclesAvoidedObstructionColumn));
        var reasons = new List<string>();
        if (reached)
        {
            reasons.Add("reached_target");
        }
        if (startObstruction.DirectPathObstructed)
        {
            reasons.Add("direct_path_obstructed");
            if (obstructionChanged)
            {
                reasons.Add("changes_obstruction_relationship");
            }
            if (pressesSameObstruction)
            {
                reasons.Add("presses_same_blocking_wall");
            }
            if (weakReverseDetour)
            {
                reasons.Add("weak_reverse_detour_rejected");
            }
            if (reverseEndpointRegression)
            {
                reasons.Add($"reverse_endpoint_regression_penalty:{-endpointProgress:0.0}");
            }
        if (reverseLowerPocketDescent)
        {
            reasons.Add("reverse_lower_pocket_descent_rejected");
        }
        if (targetSideObstructedRegression)
        {
            reasons.Add("target_side_obstructed_regression_rejected");
        }
        if (reverseDescentIntoObstructedSpan)
        {
            reasons.Add("reverse_descent_into_obstructed_span_with_upper_alternative_rejected");
        }
            if (!obstructionChanged)
            {
                reasons.Add("raw_target_distance_not_counted");
            }
        }
        if (evaluationHorizon == "commit")
        {
            reasons.Add("scored_at_commit_horizon");
        }
        if (newSpan)
        {
            reasons.Add($"new_surface_span:{finalSpan?.Id}");
        }
        if (missedUpperFrontier)
        {
            reasons.Add("missed_upper_frontier_fell_lower");
        }
        if (missedUpperFrontierButVerifiedEscape)
        {
            reasons.Add("missed_upper_frontier_verified_escape");
        }
        if (missedLowerFrontier)
        {
            reasons.Add("missed_lower_frontier_span");
        }
        if (missedOverlapLowerDrop)
        {
            reasons.Add("missed_overlap_lower_drop_handoff");
        }
        if (riskyLowerDescent)
        {
            reasons.Add("risky_lower_descent_rejected");
        }
        if (fullRolloutLowerBasin)
        {
            reasons.Add("full_rollout_enters_obstructed_lower_basin_rejected");
        }
        if (lostUpperReliefToObstructedRollout)
        {
            reasons.Add("lost_upper_relief_to_obstructed_rollout_rejected");
        }
        if (unstableUpperReliefHandoff)
        {
            reasons.Add("unstable_upper_relief_handoff_rejected");
        }
        if (clearStartRolloutIntoObstructedLowerBasin)
        {
            reasons.Add("clear_start_rollout_into_obstructed_lower_basin_rejected");
        }
        if (unstableClearAirborneDropIntoObstruction)
        {
            reasons.Add("clear_airborne_drop_into_obstruction_rejected");
        }
        if (finalHardBlockedWall)
        {
            reasons.Add("final_hard_blocked_wall_penalty");
        }
        else if (finalEndpointHardBlockedWall)
        {
            reasons.Add("final_rollout_hard_blocked_wall_risk");
        }
        if (returnsToAvoidedObstruction)
        {
            reasons.Add($"returns_to_recent_failed_obstruction:{avoidedObstruction?.ObstructionKey}");
        }
        if (recyclesAvoidedObstructionColumn)
        {
            reasons.Add($"recycles_recent_failed_obstruction_column:{avoidedObstruction?.ObstructionKey}");
        }
        if (clearanceGain >= 1f)
        {
            reasons.Add($"forward_clearance_gain:{clearanceGain:0.0}");
        }
        if (upperBandGain >= 1f)
        {
            reasons.Add($"upper_band_gain:{upperBandGain:0.0}");
        }
        if (upperFrontierCommit)
        {
            reasons.Add("upper_frontier_commit");
        }
        if (targetSideDescentCorridor)
        {
            reasons.Add("target_side_descent_corridor_commit");
        }
        if (targetSideDescentAirborneObstructedBand)
        {
            reasons.Add("target_side_descent_airborne_obstructed_band_rejected");
        }
        if (targetSideDescentEntry)
        {
            reasons.Add("target_side_descent_entry_commit");
        }
        if (targetSideLowerHandoffCommit)
        {
            reasons.Add("target_side_lower_handoff_commit");
        }
        if (targetSideLowerHandoffClearDrop)
        {
            reasons.Add("target_side_lower_handoff_clear_drop_rejected");
        }
        if (overlapLowerDropHandoff)
        {
            reasons.Add("overlap_lower_drop_handoff");
        }
        if (overlapLowerDropFollowupVisible)
        {
            reasons.Add("target_band_lower_followup_visible");
        }
        if (targetBandEntry)
        {
            reasons.Add("target_band_entry_commit");
        }
        if (clearNearTargetApproach)
        {
            reasons.Add("clear_near_target_approach_commit");
        }
        if (clearAirborneGroundingHandoff)
        {
            reasons.Add("clear_airborne_grounding_handoff");
        }
        if (recedingHorizonObstructionGain)
        {
            reasons.Add("receding_horizon_obstruction_gain");
        }
        if (sameSurfaceEdgePositioning)
        {
            reasons.Add("same_surface_edge_positioning");
        }
        if (safeAirborneLandingHandoff)
        {
            reasons.Add("safe_airborne_landing_handoff");
        }
        if (stableUpperFrontierHandoff)
        {
            reasons.Add("stable_upper_frontier_handoff");
        }
        if (rejectedRegressiveRecedingGain)
        {
            reasons.Add("regressive_receding_horizon_gain_rejected");
        }
        if (targetSideDescentCorridor && velocityManagedDescent)
        {
            reasons.Add("velocity_managed_descent_corridor");
        }
        if (airborneSettle)
        {
            reasons.Add("airborne_settle_to_ground");
        }
        if (usefulUpperRoute)
        {
            reasons.Add("useful_upper_route");
        }
        if (clearStartUpperRouteAfterFailedObstruction)
        {
            reasons.Add("clear_start_upper_route_after_failed_obstruction");
        }
        if (persistentObstructedLowerRollout)
        {
            reasons.Add("persistent_obstructed_lower_rollout_penalty");
        }
        if (usefulReverseEscape)
        {
            reasons.Add("useful_reverse_escape");
        }
        if (verifiedReverseUpperDetour)
        {
            reasons.Add("verified_reverse_upper_detour");
        }
        if (clearStartVerifiedUpperDetour)
        {
            reasons.Add("clear_start_verified_upper_detour");
        }
        if (reverseAscendingStepEscape)
        {
            reasons.Add("reverse_ascending_step_escape");
        }
        if (reverseStepUpEscape)
        {
            reasons.Add("reverse_step_up_escape");
        }
        if (reverseStepSetupPositioning)
        {
            reasons.Add("reverse_step_setup_positioning");
        }
        if (reverseStepRecedingUpperHandoff)
        {
            reasons.Add("reverse_step_receding_upper_handoff");
        }
        if (reverseStepAscentChainContinuation)
        {
            reasons.Add("reverse_step_ascent_chain_continuation");
        }
        if (unverifiedReverseStepEscape)
        {
            reasons.Add("reverse_step_up_followup_rejected");
        }
        if (reverseUpperEscape)
        {
            reasons.Add("reverse_upper_escape");
        }
        if (reverseUpperRelocationEscape)
        {
            reasons.Add("reverse_upper_relocation_escape");
        }
        if (reverseUpperFollowupVerified)
        {
            reasons.Add("reverse_upper_followup_verified");
        }
        if (reverseStepFollowupVerified)
        {
            reasons.Add("reverse_step_followup_verified");
        }
        if (stationaryVerticalHop)
        {
            reasons.Add("stationary_vertical_hop_penalty");
        }
        if (transientVerticalRelief)
        {
            reasons.Add("transient_vertical_relief_rejected");
        }
        if (neutralVerticalReliefNoTopologyChange)
        {
            reasons.Add("neutral_vertical_relief_no_topology_change_rejected");
        }
        if (transientUpperReliefNoExit)
        {
            reasons.Add("transient_upper_relief_no_exit_rejected");
        }
        if (lowerDescentPenalty >= 1f)
        {
            reasons.Add($"lower_descent_penalty:{lowerDescentPenalty:0.0}");
        }
        if (crossedBlockEndpoint)
        {
            reasons.Add("crossed_block_endpoint");
        }
        if (usefulShiftedObstructionSegment)
        {
            reasons.Add("shifted_obstruction_segment");
        }
        else if (shiftedObstructionSegment)
        {
            reasons.Add("shifted_obstruction_segment_without_clearance_rejected");
        }
        if (visibleCorridor)
        {
            reasons.Add("entered_clearer_corridor");
        }
        if (!accepted && blockReason != "none")
        {
            reasons.Add(blockReason);
        }

        var distanceTieBreaker = startObstruction.DirectPathObstructed && !obstructionChanged
            ? 0f
            : endpointProgress * 0.1f;
        var endpointRegression = MathF.Max(0f, finalMetric - startMetric);
        var reverseRegressionPenalty = reverseEndpointRegression
            ? 2400f + (-endpointProgress * 4f)
            : movingAgainstTarget && startObstruction.ForwardClearance > 48f && !targetSideDescentCorridor && !usefulReverseEscape && !clearStartVerifiedUpperDetour
                ? 2000f + (endpointRegression * 2f)
                : 0f;
        var positiveClearanceGain = MathF.Max(0f, clearanceGain);
        var progressWeight = startObstruction.DirectPathObstructed ? 0.15f : 0.45f;
        var hardWallPenalty = finalHardBlockedWall
            ? stableUpperFrontierHandoff || clearAirborneGroundingHandoff || (targetSideDescentCorridor && velocityManagedDescent) || targetSideDescentEntry || overlapLowerDropHandoff || airborneSettle ? 250f : 2600f
            : 0f;
        var endpointHardWallPenalty = finalEndpointHardBlockedWall
            ? stableUpperFrontierHandoff || clearAirborneGroundingHandoff || (targetSideDescentCorridor && velocityManagedDescent) || targetSideDescentEntry || overlapLowerDropHandoff || airborneSettle
                ? 100f
                : movingTowardTarget ? 1600f : 600f
            : 0f;
        var score = (obstructionChanged ? 1200f : startObstruction.DirectPathObstructed ? -1800f : 0f)
            + (positiveClearanceGain * 7.5f)
            + (upperBandGain * 9f)
            + (upperFrontierCommit ? 1500f + (upperBandGain * 8f) : 0f)
            + (targetSideDescentCorridor ? 950f + (progress * 0.35f) : 0f)
            + (targetSideDescentEntry ? 1800f + (progress * 0.25f) : 0f)
            + (targetSideLowerHandoffCommit ? 1500f + (progress * 0.25f) : 0f)
            + (overlapLowerDropHandoff ? 1700f + (descent * 2f) : 0f)
            + (targetBandEntry ? 2200f + (progress * 0.4f) : 0f)
            + (clearNearTargetApproach ? 1500f + (progress * 0.3f) : 0f)
            + (recedingHorizonObstructionGain ? 950f + (clearanceGain * 4f) + (upperBandGain * 5f) : 0f)
            + (sameSurfaceEdgePositioning ? 1150f : 0f)
            + (safeAirborneLandingHandoff ? 900f : 0f)
            + (airborneSettle ? 850f : 0f)
            + (usefulUpperRoute ? 850f + (upperBandGain * 6f) : 0f)
            + (clearStartUpperRouteAfterFailedObstruction ? 750f : 0f)
            + (reverseAscendingStepEscape ? 1300f + (upperBandGain * 7f) + (positiveClearanceGain * 2f) : 0f)
            + (reverseStepUpEscape ? 1600f : 0f)
            + (reverseUpperEscape ? 1200f + (upperBandGain * 3f) : 0f)
            + (reverseUpperRelocationEscape ? 1100f + (upperBandGain * 3f) : 0f)
            + (startObstruction.DirectPathObstructed && verifiedReverseUpperDetour ? 1700f : 0f)
            + (newSpan ? 420f : 0f)
            + (crossedBlockEndpoint ? 720f : 0f)
            + (usefulShiftedObstructionSegment ? 420f : 0f)
            + (visibleCorridor ? 620f : 0f)
            + (progress * progressWeight)
            + distanceTieBreaker
            + (reached ? 4000f : 0f)
            + (probe.IsGrounded ? 80f : -60f)
            - (candidate.DurationTicks * 1.2f)
            - reverseRegressionPenalty
            - lowerDescentPenalty
            - (pressesSameObstruction ? 5000f : 0f)
            - hardWallPenalty
            - endpointHardWallPenalty
            - (persistentObstructedLowerRollout ? 1800f + (finalDescent * 8f) : 0f)
            - (unstableClearAirborneDropIntoObstruction ? 3200f : 0f)
            - (clearStartRolloutIntoObstructedLowerBasin ? 4200f : 0f)
            - (lostUpperReliefToObstructedRollout ? 5200f : 0f)
            - (unstableUpperReliefHandoff ? 4800f : 0f)
            - (targetSideDescentAirborneObstructedBand ? 4800f : 0f)
            - (targetSideObstructedRegression ? 5200f : 0f)
            - (stationaryVerticalHop ? 950f : 0f)
            - (transientVerticalRelief ? 2600f : 0f)
            - (neutralVerticalReliefNoTopologyChange ? 4200f : 0f)
            - (transientUpperReliefNoExit ? 3600f : 0f)
            - (targetSideLowerHandoffClearDrop ? 4200f : 0f)
            - (returnsToAvoidedObstruction ? 7000f : 0f)
            - (recyclesAvoidedObstructionColumn ? 7200f : 0f);
        if (weakReverseDetour)
        {
            score -= 2400f;
        }
        if (missedUpperFrontier && !missedUpperFrontierButVerifiedEscape)
        {
            score -= 3200f;
        }
        if (missedLowerFrontier)
        {
            score -= 2800f;
        }
        if (missedOverlapLowerDrop)
        {
            score -= 4200f;
        }
        if (riskyLowerDescent)
        {
            score -= 3600f;
        }
        if (reverseLowerPocketDescent)
        {
            score -= 3600f;
        }
        if (unverifiedReverseStepEscape)
        {
            score -= 3200f;
        }
        if (fullRolloutLowerBasin)
        {
            score -= 5200f;
        }
        if (accepted && score <= 0f && !reached)
        {
            reasons.Add("non_positive_topology_score_rejected");
            accepted = false;
        }
        if (!accepted)
        {
            score = MathF.Min(score, -1f);
        }

        return new TopologyLocalMotionMacroReport(
            Label: candidate.Label,
            MoveDirection: candidate.MoveDirection,
            DurationTicks: candidate.DurationTicks,
            CommitTicks: candidate.CommitTicks,
            JumpStartTick: candidate.JumpStartTick,
            JumpHoldTicks: candidate.JumpHoldTicks,
            DropTicks: candidate.DropTicks,
            PreMoveDirection: candidate.PreMoveDirection,
            PreMoveTicks: candidate.PreMoveTicks,
            MoveStartTick: candidate.MoveStartTick,
            MoveEndTick: candidate.MoveEndTick,
            EvaluationState: scoreState,
            EvaluationHorizon: evaluationHorizon,
            EvaluationMetric: scoreMetric,
            EvaluationObstruction: scoreObstruction,
            FinalState: finalState,
            SurfaceSpanId: finalSpan?.Id,
            BlockReason: blockReason,
            StartMetric: startMetric,
            BestMetric: bestMetric,
            FinalMetric: finalMetric,
            Progress: progress,
            Score: score,
            Accepted: accepted,
            Reached: reached,
            ObstructionChanged: obstructionChanged,
            PressesSameObstruction: pressesSameObstruction,
            FinalObstruction: finalObstruction,
            Reasons: reasons,
            Path: selectedPath);
    }

    private static PlayerInputSnapshot BuildInput(
        PlayerEntity self,
        TopologyLocalMotionTarget target,
        LocalMacroPlan plan,
        int age)
    {
        var inPreMove = age < plan.PreMoveTicks && plan.PreMoveDirection != 0f;
        var macroAge = inPreMove ? 0 : age - plan.PreMoveTicks;
        var moveDirection = inPreMove
            ? plan.PreMoveDirection
            : macroAge < plan.MoveStartAge
                || macroAge >= plan.MoveEndAge
                ? 0f
                : plan.MoveDirection;
        var aimDirection = moveDirection == 0f
            ? target.X >= self.X ? 1f : -1f
            : moveDirection;
        return new PlayerInputSnapshot(
            Left: moveDirection < 0f,
            Right: moveDirection > 0f,
            Up: !inPreMove && macroAge >= plan.JumpStartAge && macroAge < plan.JumpEndAge,
            Down: !inPreMove && macroAge < plan.DropEndAge,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: self.X + (aimDirection * 256f),
            AimWorldY: target.Bottom - self.CollisionBottomOffset,
            DebugKill: false);
    }

    private static bool IsValidProbeState(SimulationWorld world, PlayerEntity probe, PlayerTeam team) =>
        probe.IsAlive
        && probe.Bottom >= -128f
        && probe.Bottom <= world.Level.Bounds.Height + 128f
        && probe.CanOccupy(world.Level, team, probe.X, probe.Y)
        && !probe.IsInsideBlockingTeamGate(world.Level, team);

    private static bool HasBoundedTargetFollowupImprovement(
        SimulationWorld world,
        PlayerEntity landing,
        PlayerTeam team,
        TopologyLocalMotionTarget target,
        TopologyLocalMotionObstruction startObstruction,
        bool requireSimulatedTargetMove = false)
    {
        var direction = MathF.Sign(target.X - landing.X);
        if (direction == 0f)
        {
            return false;
        }

        var landingObstruction = AnalyzeObstruction(world.Level, landing, team, target);
        if (!requireSimulatedTargetMove
            && (!landingObstruction.DirectPathObstructed
                || landingObstruction.ForwardClearance >= startObstruction.ForwardClearance + 48f))
        {
            return true;
        }

        var followups = new[]
        {
            new LocalMacroCandidate($"followupRun:{direction:0}", direction, 36, 24, int.MaxValue, 0, 0),
            new LocalMacroCandidate($"followupJump:{direction:0}", direction, 42, 28, 0, 5, 0),
            new LocalMacroCandidate($"followupRunupJump:{direction:0}", direction, 58, 34, 8, 5, 0),
        };
        var landingState = landing.CapturePredictionState();
        foreach (var followup in followups)
        {
            var probe = new PlayerEntity(ProbeEntityId - 2, landing.ClassDefinition, "TopologyLocalMotionFollowupProbe");
            probe.RestorePredictionState(in landingState);
            var previousInput = default(PlayerInputSnapshot);
            for (var age = 0; age < followup.DurationTicks; age += 1)
            {
                var input = BuildInput(probe, target, LocalMacroPlan.From(followup, startTick: 0), age);
                var jumpPressed = input.Up && !previousInput.Up;
                probe.Advance(input, jumpPressed, world.Level, team, 1d / SimulationConfig.DefaultTicksPerSecond);
                previousInput = input;
                if (!IsValidProbeState(world, probe, team))
                {
                    break;
                }
            }

            if (!IsValidProbeState(world, probe, team))
            {
                continue;
            }

            var followupObstruction = AnalyzeObstruction(world.Level, probe, team, target);
            var movedTowardTarget = MathF.Sign(probe.X - landing.X) == direction
                && MathF.Abs(probe.X - landing.X) >= 24f;
            var crossedOriginalBlock = startObstruction.Direction != 0f
                && ((startObstruction.Direction > 0f && probe.X > startObstruction.BlockX + 20f)
                    || (startObstruction.Direction < 0f && probe.X < startObstruction.BlockX - 20f));
            if (movedTowardTarget
                && (!followupObstruction.DirectPathObstructed
                    || followupObstruction.ForwardClearance >= landingObstruction.ForwardClearance + 32f
                    || crossedOriginalBlock))
            {
                return true;
            }
        }

        return false;
    }

    private static TopologyLocalMotionSurfaceSpan? FindMatchingSpan(
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> spans,
        float x,
        float bottom)
    {
        TopologyLocalMotionSurfaceSpan? best = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var span in spans)
        {
            if (x < span.XMin - 18f || x > span.XMax + 18f)
            {
                continue;
            }

            var distance = MathF.Abs(bottom - span.Bottom);
            if (distance <= 24f && distance < bestDistance)
            {
                best = span;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static bool CanStandAt(SimpleLevel level, PlayerEntity self, PlayerTeam team, float x, float bottom)
    {
        var y = bottom - self.CollisionBottomOffset;
        return self.CanOccupy(level, team, x, y)
            && !self.CanOccupy(level, team, x, y + 2f);
    }

    private static float MeasureForwardClearance(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        float direction,
        float maxDistance,
        out float blockX)
    {
        blockX = self.X + (direction * maxDistance);
        if (direction == 0f)
        {
            return maxDistance;
        }

        for (var distance = ObstructionProbeStep; distance <= maxDistance + 0.01f; distance += ObstructionProbeStep)
        {
            var x = self.X + (direction * distance);
            if (!self.CanOccupy(level, team, x, self.Y))
            {
                blockX = x;
                return distance;
            }
        }

        return maxDistance;
    }

    private static float MeasureDirectLineClearance(
        SimpleLevel level,
        PlayerEntity self,
        PlayerTeam team,
        TopologyLocalMotionTarget target,
        float direction,
        float maxDistance,
        out float blockX,
        out float blockBottom)
    {
        blockX = self.X + (direction * maxDistance);
        blockBottom = self.Bottom;
        var dx = target.X - self.X;
        if (MathF.Abs(dx) <= 0.01f)
        {
            return maxDistance;
        }

        for (var distance = ObstructionProbeStep; distance <= maxDistance + 0.01f; distance += ObstructionProbeStep)
        {
            var x = self.X + (direction * distance);
            var t = MathF.Min(1f, MathF.Abs((x - self.X) / dx));
            var interpolatedBottom = self.Bottom + ((target.Bottom - self.Bottom) * t);
            var bottom = target.Bottom > self.Bottom
                ? self.Bottom
                : interpolatedBottom;
            var y = bottom - self.CollisionBottomOffset;
            if (!self.CanOccupy(level, team, x, y))
            {
                blockX = x;
                blockBottom = bottom;
                return distance;
            }
        }

        return maxDistance;
    }

    private static float MeasureMetric(float x, float bottom, TopologyLocalMotionTarget target)
    {
        var horizontal = MathF.Max(0f, MathF.Abs(target.X - x) - 44f);
        var vertical = MathF.Max(0f, MathF.Abs(target.Bottom - bottom) - 36f);
        return horizontal + (vertical * 2.4f);
    }

    private static int Quantize(float value, float bucketSize) =>
        (int)MathF.Round(value / bucketSize);

    private sealed class SurfaceSpanBuilder
    {
        private float _bottomTotal;

        public SurfaceSpanBuilder(float x, float bottom)
        {
            XMin = x;
            XMax = x;
            LastX = x;
            _bottomTotal = bottom;
            Count = 1;
        }

        public float XMin { get; private set; }

        public float XMax { get; private set; }

        public float LastX { get; private set; }

        public int Count { get; private set; }

        public float AverageBottom => _bottomTotal / Math.Max(1, Count);

        public float Width => XMax - XMin;

        public float CenterX => (XMin + XMax) * 0.5f;

        public void Add(float x, float bottom)
        {
            XMin = MathF.Min(XMin, x);
            XMax = MathF.Max(XMax, x);
            LastX = x;
            _bottomTotal += bottom;
            Count += 1;
        }
    }

    private readonly record struct LocalMacroCandidate(
        string Label,
        float MoveDirection,
        int DurationTicks,
        int CommitTicks,
        int JumpStartTick,
        int JumpHoldTicks,
        int DropTicks,
        float PreMoveDirection = 0f,
        int PreMoveTicks = 0,
        int MoveStartTick = 0,
        int MoveEndTick = int.MaxValue,
        int TargetSpanId = 0,
        string TargetFrontierKind = "",
        bool AllowRecedingHorizonObstructionGain = false);

    private readonly record struct LocalMacroPlan(
        bool HasPlan,
        string Label,
        float MoveDirection,
        int StartTick,
        int CommitTicks,
        int JumpStartAge,
        int JumpEndAge,
        int DropEndAge,
        float PreMoveDirection,
        int PreMoveTicks,
        int MoveStartAge,
        int MoveEndAge)
    {
        public static LocalMacroPlan From(LocalMacroCandidate candidate, int startTick) =>
            From(
                candidate.Label,
                candidate.MoveDirection,
                candidate.DurationTicks,
                candidate.CommitTicks,
                candidate.JumpStartTick,
                candidate.JumpHoldTicks,
                candidate.DropTicks,
                candidate.PreMoveDirection,
                candidate.PreMoveTicks,
                candidate.MoveStartTick,
                candidate.MoveEndTick,
                startTick);

        public static LocalMacroPlan From(
            string label,
            float moveDirection,
            int durationTicks,
            int commitTicks,
            int jumpStartTick,
            int jumpHoldTicks,
            int dropTicks,
            float preMoveDirection,
            int preMoveTicks,
            int moveStartTick,
            int moveEndTick,
            int startTick)
        {
            var jumpStart = jumpStartTick == int.MaxValue ? int.MaxValue : jumpStartTick;
            return new LocalMacroPlan(
                HasPlan: true,
                label,
                moveDirection,
                startTick,
                Math.Max(MinimumCommitTicks, Math.Min(commitTicks, durationTicks)),
                jumpStart,
                jumpStartTick == int.MaxValue ? int.MaxValue : jumpStartTick + jumpHoldTicks,
                dropTicks,
                preMoveDirection,
                preMoveTicks,
                moveStartTick,
                moveEndTick);
        }
    }

    private sealed record TopologyLocalMotionScan(
        TopologyLocalMotionCollisionScan CollisionScan,
        IReadOnlyList<TopologyLocalMotionSurfaceSpan> SurfaceSpans,
        IReadOnlyList<TopologyLocalMotionFrontier> Frontiers);
}

public sealed record TopologyLocalMotionCaseReport(
    string Scenario,
    string MapName,
    int AreaIndex,
    string Team,
    string ClassId,
    TopologyLocalMotionState StartState,
    TopologyLocalMotionTarget Target,
    float XOffset,
    float BottomOffset,
    float HorizontalSpeedOffset,
    float VerticalSpeedOffset,
    IReadOnlyList<TopologyLocalMotionDecisionReport> Decisions,
    IReadOnlyList<TopologyLocalMotionTickSample> Samples)
{
    public string ObjectiveKind { get; init; } = TopologyLocalMotionObjectiveKind.ReachTarget.ToString();

    public TopologyLocalMotionTarget? PickupTarget { get; init; }

    public TopologyLocalMotionTarget? ReturnTarget { get; init; }

    public TopologyLocalMotionState? FinalState { get; init; }

    public TopologyLocalMotionObstruction? FinalObstruction { get; init; }

    public bool Passed { get; init; }

    public string FailureReason { get; init; } = string.Empty;

    public bool Reached { get; init; }

    public bool PickedUpIntel { get; init; }

    public bool Captured { get; init; }

    public int PickupTick { get; init; } = -1;

    public int ScoreTick { get; init; } = -1;

    public int RedCaps { get; init; }

    public int BlueCaps { get; init; }

    public bool InitialDirectObstructed { get; init; }

    public bool HadObstructionChangingDecision { get; init; }

    public bool BlindObstructedPressDetected { get; init; }

    public float BestForwardClearance { get; init; }
}

public sealed record TopologyLocalMotionDecisionReport(
    int Tick,
    TopologyLocalMotionState StartState,
    TopologyLocalMotionTarget Target,
    TopologyLocalMotionCollisionScan CollisionScan,
    IReadOnlyList<TopologyLocalMotionSurfaceSpan> SurfaceSpans,
    IReadOnlyList<TopologyLocalMotionFrontier> Frontiers,
    TopologyLocalMotionObstruction StartObstruction,
    IReadOnlyList<TopologyLocalMotionMacroReport> MacroCandidates,
    TopologyLocalMotionMacroReport? SelectedMacro,
    string StallReason,
    double ElapsedMilliseconds,
    int SimulatedTicks);

public sealed record TopologyLocalMotionState(
    float X,
    float Y,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    int RemainingAirJumps,
    float FacingDirectionX)
{
    public static TopologyLocalMotionState From(PlayerEntity player) =>
        new(
            player.X,
            player.Y,
            player.Bottom,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            player.RemainingAirJumps,
            player.FacingDirectionX);
}

public sealed record TopologyLocalMotionTarget(float X, float Bottom, string Label);

public sealed record TopologyLocalMotionTickSample(
    int Tick,
    float X,
    float Bottom,
    float HorizontalSpeed,
    float VerticalSpeed,
    bool IsGrounded,
    bool Left,
    bool Right,
    bool Up,
    bool Down,
    bool IsCarryingIntel,
    int RedCaps,
    int BlueCaps)
{
    public static TopologyLocalMotionTickSample From(
        int tick,
        PlayerEntity player,
        PlayerInputSnapshot input,
        SimulationWorld world) =>
        new(
            tick,
            player.X,
            player.Bottom,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            input.Left,
            input.Right,
            input.Up,
            input.Down,
            player.IsCarryingIntel,
            world.RedCaps,
            world.BlueCaps);
}

public sealed record TopologyLocalMotionCollisionScan(
    float MinX,
    float MaxX,
    float MinBottom,
    float MaxBottom,
    float SampleStepX,
    float SampleStepBottom,
    IReadOnlyList<TopologyLocalMotionSolidRect> SolidRectangles);

public sealed record TopologyLocalMotionSolidRect(float Left, float Top, float Right, float Bottom);

public sealed record TopologyLocalMotionSurfaceSpan(
    int Id,
    float XMin,
    float XMax,
    float Bottom,
    int SampleCount,
    string Relation,
    bool IsCurrent,
    bool TowardTarget,
    bool BlockedLeft,
    bool BlockedRight)
{
    public float CenterX => (XMin + XMax) * 0.5f;
}

public sealed record TopologyLocalMotionFrontier(
    int SpanId,
    float X,
    float Bottom,
    string Kind,
    bool IsBlockedEdge,
    float Score);

public sealed record TopologyLocalMotionObstruction(
    float Direction,
    bool DirectPathObstructed,
    float ForwardClearance,
    float DirectLineClearance,
    float BlockX,
    float BlockBottom,
    string ObstructionKey)
{
    public bool SameObstructionAs(TopologyLocalMotionObstruction other) =>
        DirectPathObstructed
        && other.DirectPathObstructed
        && ObstructionKey == other.ObstructionKey;

    public bool SameObstructionFamilyAs(TopologyLocalMotionObstruction other) =>
        DirectPathObstructed
        && other.DirectPathObstructed
        && MathF.Sign(Direction) == MathF.Sign(other.Direction)
        && MathF.Abs(BlockX - other.BlockX) <= 48f
        && MathF.Abs(BlockBottom - other.BlockBottom) <= 96f;

    public bool SameObstructionColumnAs(TopologyLocalMotionObstruction other) =>
        DirectPathObstructed
        && other.DirectPathObstructed
        && MathF.Sign(Direction) == MathF.Sign(other.Direction)
        && MathF.Abs(BlockX - other.BlockX) <= 48f;
}

public sealed record TopologyLocalMotionMacroReport(
    string Label,
    float MoveDirection,
    int DurationTicks,
    int CommitTicks,
    int JumpStartTick,
    int JumpHoldTicks,
    int DropTicks,
    float PreMoveDirection,
    int PreMoveTicks,
    int MoveStartTick,
    int MoveEndTick,
    TopologyLocalMotionState EvaluationState,
    string EvaluationHorizon,
    float EvaluationMetric,
    TopologyLocalMotionObstruction EvaluationObstruction,
    TopologyLocalMotionState FinalState,
    int? SurfaceSpanId,
    string BlockReason,
    float StartMetric,
    float BestMetric,
    float FinalMetric,
    float Progress,
    float Score,
    bool Accepted,
    bool Reached,
    bool ObstructionChanged,
    bool PressesSameObstruction,
    TopologyLocalMotionObstruction FinalObstruction,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<TopologyLocalMotionPathPoint> Path);

public sealed record TopologyLocalMotionPathPoint(int Tick, float X, float Bottom);

internal static class TopologyLocalMotionOverlayRenderer
{
    private static readonly Rgba32 Background = new(18, 20, 24, 255);
    private static readonly Rgba32 SolidColor = new(91, 83, 82, 255);
    private static readonly Rgba32 ScanColor = new(54, 80, 112, 255);
    private static readonly Rgba32 SpanColor = new(72, 214, 128, 255);
    private static readonly Rgba32 FrontierColor = new(255, 216, 80, 255);
    private static readonly Rgba32 BotColor = new(72, 160, 255, 255);
    private static readonly Rgba32 TargetColor = new(255, 80, 80, 255);
    private static readonly Rgba32 PickupTargetColor = new(255, 176, 64, 255);
    private static readonly Rgba32 ReturnTargetColor = new(160, 96, 255, 255);
    private static readonly Rgba32 PathColor = new(255, 255, 255, 255);
    private static readonly Rgba32 BlockColor = new(255, 72, 72, 255);

    public static void Render(
        SimpleLevel level,
        TopologyLocalMotionCaseReport report,
        TopologyLocalMotionDecisionReport? lastDecision,
        string path)
    {
        var focusX = report.FinalState?.X ?? report.StartState.X;
        var focusBottom = report.FinalState?.Bottom ?? report.StartState.Bottom;
        if (lastDecision is not null)
        {
            focusX = lastDecision.StartState.X;
            focusBottom = lastDecision.StartState.Bottom;
        }

        var worldLeft = MathF.Max(0f, focusX - 440f);
        var worldRight = MathF.Min(level.Bounds.Width, focusX + 620f);
        var worldTop = MathF.Max(0f, focusBottom - 420f);
        var worldBottom = MathF.Min(level.Bounds.Height, focusBottom + 280f);
        var scale = MathF.Min(1.25f, MathF.Min(1200f / MathF.Max(1f, worldRight - worldLeft), 820f / MathF.Max(1f, worldBottom - worldTop)));
        var width = Math.Max(640, (int)MathF.Ceiling((worldRight - worldLeft) * scale));
        var height = Math.Max(420, (int)MathF.Ceiling((worldBottom - worldTop) * scale));
        using var image = new Image<Rgba32>(width, height, Background);

        foreach (var solid in level.Solids)
        {
            if (solid.Right < worldLeft || solid.Left > worldRight || solid.Bottom < worldTop || solid.Top > worldBottom)
            {
                continue;
            }

            FillRect(image, worldLeft, worldTop, scale, solid.Left, solid.Top, solid.Right, solid.Bottom, SolidColor);
        }

        if (lastDecision is not null)
        {
            var scan = lastDecision.CollisionScan;
            StrokeRect(image, worldLeft, worldTop, scale, scan.MinX, scan.MinBottom - 24f, scan.MaxX, scan.MaxBottom, ScanColor);
            foreach (var span in lastDecision.SurfaceSpans)
            {
                DrawLine(image, worldLeft, worldTop, scale, span.XMin, span.Bottom, span.XMax, span.Bottom, span.IsCurrent ? BotColor : SpanColor);
            }

            foreach (var frontier in lastDecision.Frontiers)
            {
                DrawCross(image, worldLeft, worldTop, scale, frontier.X, frontier.Bottom, 7, FrontierColor);
            }

            DrawCross(image, worldLeft, worldTop, scale, lastDecision.StartObstruction.BlockX, lastDecision.StartObstruction.BlockBottom, 9, BlockColor);
            if (lastDecision.SelectedMacro is not null)
            {
                var pathPoints = lastDecision.SelectedMacro.Path;
                for (var index = 1; index < pathPoints.Count; index += 1)
                {
                    DrawLine(
                        image,
                        worldLeft,
                        worldTop,
                        scale,
                        pathPoints[index - 1].X,
                        pathPoints[index - 1].Bottom,
                        pathPoints[index].X,
                        pathPoints[index].Bottom,
                        PathColor);
                }
            }
        }

        foreach (var sample in report.Samples)
        {
            DrawCross(image, worldLeft, worldTop, scale, sample.X, sample.Bottom, 4, new Rgba32(120, 180, 255, 210));
        }

        DrawCross(image, worldLeft, worldTop, scale, report.StartState.X, report.StartState.Bottom, 9, BotColor);
        if (report.FinalState is not null)
        {
            DrawCross(image, worldLeft, worldTop, scale, report.FinalState.X, report.FinalState.Bottom, 11, new Rgba32(92, 255, 160, 255));
        }

        if (report.PickupTarget is not null)
        {
            DrawCross(image, worldLeft, worldTop, scale, report.PickupTarget.X, report.PickupTarget.Bottom, 11, PickupTargetColor);
        }

        if (report.ReturnTarget is not null)
        {
            DrawCross(image, worldLeft, worldTop, scale, report.ReturnTarget.X, report.ReturnTarget.Bottom, 11, ReturnTargetColor);
        }

        DrawCross(image, worldLeft, worldTop, scale, report.Target.X, report.Target.Bottom, 12, TargetColor);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        image.SaveAsPng(path);
    }

    private static void FillRect(
        Image<Rgba32> image,
        float worldLeft,
        float worldTop,
        float scale,
        float left,
        float top,
        float right,
        float bottom,
        Rgba32 color)
    {
        var x0 = ClampX(image, WorldToScreenX(worldLeft, scale, left));
        var y0 = ClampY(image, WorldToScreenY(worldTop, scale, top));
        var x1 = ClampX(image, WorldToScreenX(worldLeft, scale, right));
        var y1 = ClampY(image, WorldToScreenY(worldTop, scale, bottom));
        for (var y = y0; y <= y1; y += 1)
        {
            for (var x = x0; x <= x1; x += 1)
            {
                image[x, y] = color;
            }
        }
    }

    private static void StrokeRect(
        Image<Rgba32> image,
        float worldLeft,
        float worldTop,
        float scale,
        float left,
        float top,
        float right,
        float bottom,
        Rgba32 color)
    {
        DrawLine(image, worldLeft, worldTop, scale, left, top, right, top, color);
        DrawLine(image, worldLeft, worldTop, scale, right, top, right, bottom, color);
        DrawLine(image, worldLeft, worldTop, scale, right, bottom, left, bottom, color);
        DrawLine(image, worldLeft, worldTop, scale, left, bottom, left, top, color);
    }

    private static void DrawCross(
        Image<Rgba32> image,
        float worldLeft,
        float worldTop,
        float scale,
        float x,
        float y,
        int radius,
        Rgba32 color)
    {
        DrawLine(image, worldLeft, worldTop, scale, x - radius, y, x + radius, y, color);
        DrawLine(image, worldLeft, worldTop, scale, x, y - radius, x, y + radius, color);
    }

    private static void DrawLine(
        Image<Rgba32> image,
        float worldLeft,
        float worldTop,
        float scale,
        float x0,
        float y0,
        float x1,
        float y1,
        Rgba32 color)
    {
        var sx0 = WorldToScreenX(worldLeft, scale, x0);
        var sy0 = WorldToScreenY(worldTop, scale, y0);
        var sx1 = WorldToScreenX(worldLeft, scale, x1);
        var sy1 = WorldToScreenY(worldTop, scale, y1);
        var dx = Math.Abs(sx1 - sx0);
        var dy = -Math.Abs(sy1 - sy0);
        var stepX = sx0 < sx1 ? 1 : -1;
        var stepY = sy0 < sy1 ? 1 : -1;
        var error = dx + dy;
        var x = sx0;
        var y = sy0;
        while (true)
        {
            if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
            {
                image[x, y] = color;
            }

            if (x == sx1 && y == sy1)
            {
                break;
            }

            var twiceError = 2 * error;
            if (twiceError >= dy)
            {
                error += dy;
                x += stepX;
            }

            if (twiceError <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    private static int WorldToScreenX(float worldLeft, float scale, float x) =>
        (int)MathF.Round((x - worldLeft) * scale);

    private static int WorldToScreenY(float worldTop, float scale, float y) =>
        (int)MathF.Round((y - worldTop) * scale);

    private static int ClampX(Image<Rgba32> image, int x) =>
        int.Clamp(x, 0, image.Width - 1);

    private static int ClampY(Image<Rgba32> image, int y) =>
        int.Clamp(y, 0, image.Height - 1);
}
