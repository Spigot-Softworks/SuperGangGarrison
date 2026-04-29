using OpenGarrison.Core;
using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Environment;
using OpenGarrison.MLBot.Policies;
using OpenGarrison.MLBot.Tools;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length > 0)
{
    switch (args[0].ToLowerInvariant())
    {
        case "demo-summary":
            {
                var root = GetOption(args, "--root")
                    ?? Path.Combine(RuntimePaths.ConfigDirectory, "mlbot-demos");
                return MLBotDemoInspector.RunSummary(root);
            }
        case "demo-coverage":
            {
                var root = GetOption(args, "--root")
                    ?? Path.Combine(RuntimePaths.ConfigDirectory, "mlbot-demos");
                var levelName = GetOption(args, "--map");
                return MLBotDemoInspector.RunCoverage(root, levelName);
            }
        case "demo-manifest":
            {
                var root = GetOption(args, "--root")
                    ?? Path.Combine(RuntimePaths.ConfigDirectory, "mlbot-demos");
                var outPath = GetOption(args, "--out")
                    ?? Path.Combine(root, "manifest.json");
                return MLBotDemoInspector.RunManifest(root, outPath);
            }
        case "mine-demo-nav":
            {
                return MLBotDemoNavGraphMiner.Run(args[1..]);
            }
        case "schema-manifest":
            {
                return MLBotSchemaManifestWriter.Run(GetOption(args, "--out"));
            }
        case "eval-demos":
            {
                var root = GetOption(args, "--root")
                    ?? Path.Combine(RuntimePaths.ConfigDirectory, "mlbot-demos");
                var modelPath = GetRequiredOption(args, "--model");
                if (modelPath is null)
                {
                    return 1;
                }

                var ticks = ParseIntOption(args, "--ticks", 3000);
                var repetitions = ParseIntOption(args, "--repetitions", 1);
                var traceDirectory = GetOption(args, "--trace-dir");
                var scenarioFilter = ScenarioCommandLineOptions.Parse(args[1..]);
                return MLBotModelEvaluator.RunDemoSuite(root, modelPath, ticks, repetitions, traceDirectory, scenarioFilter);
            }
        case "export-rollout":
            {
                var rolloutOptions = RolloutCommandLineOptions.Parse(args[1..]);
                var outputPath = GetRequiredOption(args, "--out");
                if (outputPath is null)
                {
                    return 1;
                }

                var rolloutConfig = BuildEpisodeConfig(rolloutOptions);
                return MLBotRolloutExporter.ExportEpisode(
                    rolloutConfig,
                    rolloutOptions.ModelPath,
                    rolloutOptions.MaxTicks,
                    outputPath,
                    rolloutOptions.Stochastic,
                    rolloutOptions.Seed,
                    rolloutOptions.Temperature,
                    rolloutOptions.DisablePolicyOverrides,
                    rolloutOptions.LocalTraversalOracle,
                    rolloutOptions.LocalTraversalMpc,
                    rolloutOptions.MoveHoldTicks,
                    rolloutOptions.JumpHoldTicks,
                    rolloutOptions.ReturnFinalizerModelPath,
                    rolloutOptions.ReturnFinalizerEngageDistance,
                    rolloutOptions.ReturnFinalizerLevelNameFilter,
                    rolloutOptions.ReturnFinalizerTeamFilter,
                    rolloutOptions.ReturnFinalizerClassFilter,
                    rolloutOptions.ReturnFinalizerAfterOptions,
                    rolloutOptions.ReturnReplayBankPath,
                    rolloutOptions.ReturnReplayEngageDistance,
                    rolloutOptions.ReturnReplayMaxSelectionScore,
                    rolloutOptions.ReplayBankPath,
                    rolloutOptions.ReplayTaskPhase,
                    rolloutOptions.ReplayEngageDistance,
                    rolloutOptions.ReplayMaxSelectionScore,
                    rolloutOptions.ReplayRequiresCarryingIntel,
                    rolloutOptions.ReturnChunkModelPath,
                    rolloutOptions.ReturnChunkEngageDistance,
                    rolloutOptions.ReturnChunkCommitTicks,
                    rolloutOptions.ReturnChunkOptions,
                    rolloutOptions.ReturnChunkSelectorModelPath,
                    rolloutOptions.ReturnSelectedChunkOptions,
                    rolloutOptions.ReturnHierarchicalChunkModelPath,
                    rolloutOptions.ReturnHierarchicalChunkCommitTicks,
                    rolloutOptions.TaskChunkOptions,
                    rolloutOptions.TaskOptions,
                    rolloutOptions.CompactRollout);
            }
        case "export-intervention-rollout":
            {
                try
                {
                    var interventionOptions = MLBotInterventionRolloutOptions.Parse(args[1..]);
                    return MLBotInterventionRolloutExporter.ExportEpisode(interventionOptions);
                }
                catch (ArgumentException exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    return 1;
                }
            }
        case "analyze-rollout":
            {
                var inputPath = GetRequiredOption(args, "--in");
                if (inputPath is null)
                {
                    return 1;
                }

                var outputPath = GetOption(args, "--out");
                var stallWindow = ParseIntOption(args, "--stall-window", 60);
                return MLBotRolloutDiagnostics.AnalyzeRollout(inputPath, outputPath, stallWindow);
            }
        case "export-outcome-dataset":
            {
                return MLBotOutcomeDatasetExporter.Run(args[1..]);
            }
        case "eval-matrix":
            {
                return MLBotFastMatrixEvaluator.Run(args[1..]);
            }
    }
}

var options = RolloutCommandLineOptions.Parse(args);
var config = BuildEpisodeConfig(options);

var environment = new MLBotEnvironment();
var policy = CreatePolicy(options);
var episode = MLBotModelEvaluator.RunEpisode(environment, policy, config, config.MaxTicks);
if (policy is IDisposable disposablePolicy)
{
    disposablePolicy.Dispose();
}
var result = episode.Result;
var jsonOutputPath = GetOption(args, "--json-out");

Console.WriteLine($"level={result.LevelName} team={result.Team} class={result.ClassId} task={result.TaskPhase} success={result.Success} ticks={result.TicksElapsed} reward={result.TotalReward:0.00} outcome={result.Outcome}");
Console.WriteLine($"terminal_reason={episode.Trace.TerminalReason} pickup_tick={FormatOptionalTick(episode.Trace.PickupTick)} score_tick={FormatOptionalTick(episode.Trace.ScoreTick)} capture_tick={FormatOptionalTick(episode.Trace.CaptureTick)} max_stuck={episode.Trace.MaxStuckTicks:0.0} min_objective_distance={episode.Trace.MinObjectiveDistance:0.0} final_objective_distance={episode.Trace.FinalObjectiveDistance:0.0} final_phase={episode.Trace.FinalPhase}");
Console.WriteLine($"min_navigation_distance={episode.Trace.MinNavigationDistance:0.0} final_navigation_distance={episode.Trace.FinalNavigationDistance:0.0} min_waypoint_distance={episode.Trace.MinWaypointDistance:0.0} final_waypoint_distance={episode.Trace.FinalWaypointDistance:0.0}");
if (!string.IsNullOrWhiteSpace(jsonOutputPath))
{
    var directory = Path.GetDirectoryName(jsonOutputPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(
        jsonOutputPath,
        JsonSerializer.Serialize(
            episode.Trace,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            }));
}
return 0;

static IMLBotPolicyRuntime CreatePolicy(RolloutCommandLineOptions options)
{
    return MLBotPolicyRuntimeBuilder.CreatePolicy(options);
}

static MLBotEpisodeConfig BuildEpisodeConfig(RolloutCommandLineOptions options)
{
    return MLBotEpisodeConfig.CreateDefault(
        levelName: options.LevelName,
        taskPhase: options.TaskPhase,
        team: options.Team,
        classId: options.ClassId,
        maxTicks: options.MaxTicks,
        startNodeId: options.StartNodeId,
        startX: options.StartX,
        startY: options.StartY,
        startVelocityX: options.StartVelocityX,
        startVelocityY: options.StartVelocityY,
        carryingIntel: options.CarryingIntel,
        startIsGrounded: options.StartIsGrounded,
        startRemainingAirJumps: options.StartRemainingAirJumps,
        startFacingDirectionX: options.StartFacingDirectionX,
        startPreviousMoveInput: options.StartPreviousMoveInput,
        startPreviousJumpHeld: options.StartPreviousJumpHeld,
        startPreviousDropInput: options.StartPreviousDropInput,
        startPreviousFirePrimary: options.StartPreviousFirePrimary,
        startPreviousFireSecondary: options.StartPreviousFireSecondary,
        startPreviousPositionDeltaX: options.StartPreviousPositionDeltaX,
        startPreviousPositionDeltaY: options.StartPreviousPositionDeltaY,
        startPreviousVelocityX: options.StartPreviousVelocityX,
        startPreviousVelocityY: options.StartPreviousVelocityY,
        startPreviousFacingDirectionX: options.StartPreviousFacingDirectionX,
        startPreviousIsGrounded: options.StartPreviousIsGrounded,
        startObjectiveDistance: options.StartObjectiveDistance,
        startObjectiveDistanceDelta: options.StartObjectiveDistanceDelta,
        startPreviousObjectiveDistanceDelta: options.StartPreviousObjectiveDistanceDelta,
        startAirborneTicks: options.StartAirborneTicks,
        startJumpTicks: options.StartJumpTicks,
        startFramesSinceJumpPressed: options.StartFramesSinceJumpPressed,
        startFramesSinceJumpReleased: options.StartFramesSinceJumpReleased);
}

static string? GetOption(string[] args, string optionName)
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

static string? GetRequiredOption(string[] args, string optionName)
{
    var value = GetOption(args, optionName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    Console.Error.WriteLine($"missing required option: {optionName}");
    return null;
}

static int ParseIntOption(string[] args, string optionName, int fallback)
{
    var value = GetOption(args, optionName);
    return int.TryParse(value, out var parsedValue)
        ? Math.Max(1, parsedValue)
        : fallback;
}

static string FormatOptionalTick(int? value)
{
    return value?.ToString(CultureInfo.InvariantCulture) ?? "none";
}

internal sealed record RolloutCommandLineOptions(
    string LevelName,
    PlayerTeam Team,
    PlayerClass ClassId,
    MLBotTaskPhase TaskPhase,
    int MaxTicks,
    string? ModelPath,
    string? ReturnFinalizerModelPath,
    float ReturnFinalizerEngageDistance,
    string? ReturnFinalizerLevelNameFilter,
    PlayerTeam? ReturnFinalizerTeamFilter,
    PlayerClass? ReturnFinalizerClassFilter,
    bool ReturnFinalizerAfterOptions,
    string? ReturnReplayBankPath,
    float ReturnReplayEngageDistance,
    float ReturnReplayMaxSelectionScore,
    string? ReplayBankPath,
    MLBotTaskPhase ReplayTaskPhase,
    float ReplayEngageDistance,
    float ReplayMaxSelectionScore,
    bool ReplayRequiresCarryingIntel,
    string? ReturnChunkModelPath,
    float ReturnChunkEngageDistance,
    int ReturnChunkCommitTicks,
    IReadOnlyList<ReturnChunkOptionSpec> ReturnChunkOptions,
    string? ReturnChunkSelectorModelPath,
    IReadOnlyList<ReturnSelectedChunkOptionSpec> ReturnSelectedChunkOptions,
    string? ReturnHierarchicalChunkModelPath,
    IReadOnlyList<int> ReturnHierarchicalChunkCommitTicks,
    IReadOnlyList<TaskChunkOptionSpec> TaskChunkOptions,
    IReadOnlyList<TaskOptionSpec> TaskOptions,
    bool Stochastic,
    int Seed,
    float Temperature,
    int StartNodeId,
    float? StartX,
    float? StartY,
    float? StartVelocityX,
    float? StartVelocityY,
    bool? CarryingIntel,
    bool? StartIsGrounded,
    int? StartRemainingAirJumps,
    float? StartFacingDirectionX,
    int? StartPreviousMoveInput,
    bool? StartPreviousJumpHeld,
    bool? StartPreviousDropInput,
    bool? StartPreviousFirePrimary,
    bool? StartPreviousFireSecondary,
    float? StartPreviousPositionDeltaX,
    float? StartPreviousPositionDeltaY,
    float? StartPreviousVelocityX,
    float? StartPreviousVelocityY,
    float? StartPreviousFacingDirectionX,
    bool? StartPreviousIsGrounded,
    float? StartObjectiveDistance,
    float? StartObjectiveDistanceDelta,
    float? StartPreviousObjectiveDistanceDelta,
    float? StartAirborneTicks,
    float? StartJumpTicks,
    float? StartFramesSinceJumpPressed,
    float? StartFramesSinceJumpReleased,
    bool DisablePolicyOverrides,
    bool LocalTraversalOracle,
    bool LocalTraversalMpc,
    int MoveHoldTicks,
    int JumpHoldTicks,
    bool CompactRollout)
{
    public static RolloutCommandLineOptions Parse(string[] args)
    {
        var levelName = "Harvest";
        var team = PlayerTeam.Red;
        var classId = PlayerClass.Scout;
        var taskPhase = MLBotTaskPhase.CaptureObjective;
        var maxTicks = 1800;
        string? modelPath = null;
        string? returnFinalizerModelPath = null;
        var returnFinalizerEngageDistance = 0f;
        string? returnFinalizerLevelNameFilter = null;
        PlayerTeam? returnFinalizerTeamFilter = null;
        PlayerClass? returnFinalizerClassFilter = null;
        var returnFinalizerAfterOptions = false;
        string? returnReplayBankPath = null;
        var returnReplayEngageDistance = 0f;
        var returnReplayMaxSelectionScore = 0f;
        string? replayBankPath = null;
        var replayTaskPhase = MLBotTaskPhase.ReturnIntel;
        var replayEngageDistance = 0f;
        var replayMaxSelectionScore = 0f;
        var replayRequiresCarryingIntel = true;
        string? returnChunkModelPath = null;
        var returnChunkEngageDistance = 0f;
        var returnChunkCommitTicks = 0;
        var returnChunkOptions = new List<ReturnChunkOptionSpec>();
        string? returnChunkSelectorModelPath = null;
        var returnSelectedChunkOptions = new List<ReturnSelectedChunkOptionSpec>();
        string? returnHierarchicalChunkModelPath = null;
        var returnHierarchicalChunkCommitTicks = new List<int>();
        var taskChunkOptions = new List<TaskChunkOptionSpec>();
        var taskOptions = new List<TaskOptionSpec>();
        var stochastic = false;
        var seed = 1337;
        var temperature = 1f;
        var startNodeId = -1;
        float? startX = null;
        float? startY = null;
        float? startVelocityX = null;
        float? startVelocityY = null;
        bool? carryingIntel = null;
        bool? startIsGrounded = null;
        int? startRemainingAirJumps = null;
        float? startFacingDirectionX = null;
        int? startPreviousMoveInput = null;
        bool? startPreviousJumpHeld = null;
        bool? startPreviousDropInput = null;
        bool? startPreviousFirePrimary = null;
        bool? startPreviousFireSecondary = null;
        float? startPreviousPositionDeltaX = null;
        float? startPreviousPositionDeltaY = null;
        float? startPreviousVelocityX = null;
        float? startPreviousVelocityY = null;
        float? startPreviousFacingDirectionX = null;
        bool? startPreviousIsGrounded = null;
        float? startObjectiveDistance = null;
        float? startObjectiveDistanceDelta = null;
        float? startPreviousObjectiveDistanceDelta = null;
        float? startAirborneTicks = null;
        float? startJumpTicks = null;
        float? startFramesSinceJumpPressed = null;
        float? startFramesSinceJumpReleased = null;
        var disablePolicyOverrides = false;
        var localTraversalOracle = false;
        var localTraversalMpc = false;
        var moveHoldTicks = 0;
        var jumpHoldTicks = 0;
        var compactRollout = false;

        for (var index = 0; index < args.Length; index += 1)
        {
            var arg = args[index];
            if (string.Equals(arg, "--stochastic", StringComparison.OrdinalIgnoreCase))
            {
                stochastic = true;
                continue;
            }
            if (string.Equals(arg, "--disable-policy-overrides", StringComparison.OrdinalIgnoreCase))
            {
                disablePolicyOverrides = true;
                continue;
            }
            if (string.Equals(arg, "--local-traversal-oracle", StringComparison.OrdinalIgnoreCase))
            {
                localTraversalOracle = true;
                continue;
            }
            if (string.Equals(arg, "--local-traversal-mpc", StringComparison.OrdinalIgnoreCase))
            {
                localTraversalMpc = true;
                continue;
            }
            if (string.Equals(arg, "--compact-rollout", StringComparison.OrdinalIgnoreCase))
            {
                compactRollout = true;
                continue;
            }
            if (string.Equals(arg, "--carrying-intel", StringComparison.OrdinalIgnoreCase))
            {
                carryingIntel = true;
                continue;
            }
            if (string.Equals(arg, "--no-carrying-intel", StringComparison.OrdinalIgnoreCase))
            {
                carryingIntel = false;
                continue;
            }
            if (string.Equals(arg, "--return-finalizer-after-options", StringComparison.OrdinalIgnoreCase))
            {
                returnFinalizerAfterOptions = true;
                continue;
            }
            if (string.Equals(arg, "--replay-requires-carrying-intel", StringComparison.OrdinalIgnoreCase))
            {
                replayRequiresCarryingIntel = true;
                continue;
            }
            if (string.Equals(arg, "--no-replay-requires-carrying-intel", StringComparison.OrdinalIgnoreCase))
            {
                replayRequiresCarryingIntel = false;
                continue;
            }

            if (index + 1 >= args.Length)
            {
                continue;
            }

            var value = args[index + 1];
            switch (arg)
            {
                case "--map":
                    levelName = value;
                    index += 1;
                    break;
                case "--team" when Enum.TryParse<PlayerTeam>(value, ignoreCase: true, out var parsedTeam):
                    team = parsedTeam;
                    index += 1;
                    break;
                case "--class" when Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var parsedClass):
                    classId = parsedClass;
                    index += 1;
                    break;
                case "--task" when Enum.TryParse<MLBotTaskPhase>(value, ignoreCase: true, out var parsedTask):
                    taskPhase = parsedTask;
                    index += 1;
                    break;
                case "--ticks" when int.TryParse(value, out var parsedTicks):
                    maxTicks = Math.Max(1, parsedTicks);
                    index += 1;
                    break;
                case "--model":
                    modelPath = value;
                    index += 1;
                    break;
                case "--return-finalizer-model":
                    returnFinalizerModelPath = value;
                    index += 1;
                    break;
                case "--return-finalizer-engage-distance" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedReturnFinalizerEngageDistance):
                    returnFinalizerEngageDistance = Math.Max(0f, parsedReturnFinalizerEngageDistance);
                    index += 1;
                    break;
                case "--return-finalizer-map":
                    returnFinalizerLevelNameFilter = value;
                    index += 1;
                    break;
                case "--return-finalizer-team" when Enum.TryParse<PlayerTeam>(value, ignoreCase: true, out var parsedReturnFinalizerTeam):
                    returnFinalizerTeamFilter = parsedReturnFinalizerTeam;
                    index += 1;
                    break;
                case "--return-finalizer-class" when Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var parsedReturnFinalizerClass):
                    returnFinalizerClassFilter = parsedReturnFinalizerClass;
                    index += 1;
                    break;
                case "--return-replay-bank":
                    returnReplayBankPath = value;
                    index += 1;
                    break;
                case "--return-replay-engage-distance" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedReturnReplayEngageDistance):
                    returnReplayEngageDistance = Math.Max(0f, parsedReturnReplayEngageDistance);
                    index += 1;
                    break;
                case "--return-replay-max-score" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedReturnReplayMaxScore):
                    returnReplayMaxSelectionScore = Math.Max(0f, parsedReturnReplayMaxScore);
                    index += 1;
                    break;
                case "--replay-bank":
                    replayBankPath = value;
                    index += 1;
                    break;
                case "--replay-task" when Enum.TryParse<MLBotTaskPhase>(value, ignoreCase: true, out var parsedReplayTask):
                    replayTaskPhase = parsedReplayTask;
                    index += 1;
                    break;
                case "--replay-engage-distance" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedReplayEngageDistance):
                    replayEngageDistance = Math.Max(0f, parsedReplayEngageDistance);
                    index += 1;
                    break;
                case "--replay-max-score" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedReplayMaxScore):
                    replayMaxSelectionScore = Math.Max(0f, parsedReplayMaxScore);
                    index += 1;
                    break;
                case "--return-chunk-model":
                    returnChunkModelPath = value;
                    index += 1;
                    break;
                case "--return-chunk-engage-distance" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedReturnChunkEngageDistance):
                    returnChunkEngageDistance = Math.Max(0f, parsedReturnChunkEngageDistance);
                    index += 1;
                    break;
                case "--return-chunk-commit-ticks" when int.TryParse(value, out var parsedReturnChunkCommitTicks):
                    returnChunkCommitTicks = Math.Max(0, parsedReturnChunkCommitTicks);
                    index += 1;
                    break;
                case "--return-chunk-spec":
                    returnChunkOptions.Add(ParseReturnChunkOptionSpec(value));
                    index += 1;
                    break;
                case "--return-chunk-selector-model":
                    returnChunkSelectorModelPath = value;
                    index += 1;
                    break;
                case "--return-selected-chunk-spec":
                    returnSelectedChunkOptions.Add(ParseReturnSelectedChunkOptionSpec(value));
                    index += 1;
                    break;
                case "--return-hierarchical-chunk-model":
                    returnHierarchicalChunkModelPath = value;
                    index += 1;
                    break;
                case "--return-hierarchical-chunk-commit-ticks":
                    returnHierarchicalChunkCommitTicks = ParseCommitTicks(value);
                    index += 1;
                    break;
                case "--task-option-spec":
                    taskOptions.Add(ParseTaskOptionSpec(value));
                    index += 1;
                    break;
                case "--task-chunk-spec":
                    taskChunkOptions.Add(ParseTaskChunkOptionSpec(value));
                    index += 1;
                    break;
                case "--seed" when int.TryParse(value, out var parsedSeed):
                    seed = parsedSeed;
                    index += 1;
                    break;
                case "--temperature" when float.TryParse(value, out var parsedTemperature):
                    temperature = Math.Max(0.05f, parsedTemperature);
                    index += 1;
                    break;
                case "--start-node-id" when int.TryParse(value, out var parsedStartNodeId):
                    startNodeId = parsedStartNodeId;
                    index += 1;
                    break;
                case "--start-x" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartX):
                    startX = parsedStartX;
                    index += 1;
                    break;
                case "--start-y" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartY):
                    startY = parsedStartY;
                    index += 1;
                    break;
                case "--start-vx" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartVelocityX):
                    startVelocityX = parsedStartVelocityX;
                    index += 1;
                    break;
                case "--start-vy" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartVelocityY):
                    startVelocityY = parsedStartVelocityY;
                    index += 1;
                    break;
                case "--start-is-grounded" when TryParseBool(value, out var parsedStartIsGrounded):
                    startIsGrounded = parsedStartIsGrounded;
                    index += 1;
                    break;
                case "--start-remaining-air-jumps" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStartRemainingAirJumps):
                    startRemainingAirJumps = Math.Max(0, parsedStartRemainingAirJumps);
                    index += 1;
                    break;
                case "--start-facing-dir-x" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartFacingDirectionX):
                    startFacingDirectionX = parsedStartFacingDirectionX;
                    index += 1;
                    break;
                case "--start-prev-move" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStartPreviousMoveInput):
                    startPreviousMoveInput = Math.Clamp(parsedStartPreviousMoveInput, -1, 1);
                    index += 1;
                    break;
                case "--start-prev-jump-held" when TryParseBool(value, out var parsedStartPreviousJumpHeld):
                    startPreviousJumpHeld = parsedStartPreviousJumpHeld;
                    index += 1;
                    break;
                case "--start-prev-drop" when TryParseBool(value, out var parsedStartPreviousDropInput):
                    startPreviousDropInput = parsedStartPreviousDropInput;
                    index += 1;
                    break;
                case "--start-prev-fire-primary" when TryParseBool(value, out var parsedStartPreviousFirePrimary):
                    startPreviousFirePrimary = parsedStartPreviousFirePrimary;
                    index += 1;
                    break;
                case "--start-prev-fire-secondary" when TryParseBool(value, out var parsedStartPreviousFireSecondary):
                    startPreviousFireSecondary = parsedStartPreviousFireSecondary;
                    index += 1;
                    break;
                case "--start-prev-dx" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartPreviousPositionDeltaX):
                    startPreviousPositionDeltaX = parsedStartPreviousPositionDeltaX;
                    index += 1;
                    break;
                case "--start-prev-dy" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartPreviousPositionDeltaY):
                    startPreviousPositionDeltaY = parsedStartPreviousPositionDeltaY;
                    index += 1;
                    break;
                case "--start-prev-vx" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartPreviousVelocityX):
                    startPreviousVelocityX = parsedStartPreviousVelocityX;
                    index += 1;
                    break;
                case "--start-prev-vy" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartPreviousVelocityY):
                    startPreviousVelocityY = parsedStartPreviousVelocityY;
                    index += 1;
                    break;
                case "--start-prev-facing-dir-x" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartPreviousFacingDirectionX):
                    startPreviousFacingDirectionX = parsedStartPreviousFacingDirectionX;
                    index += 1;
                    break;
                case "--start-prev-is-grounded" when TryParseBool(value, out var parsedStartPreviousIsGrounded):
                    startPreviousIsGrounded = parsedStartPreviousIsGrounded;
                    index += 1;
                    break;
                case "--start-objective-distance" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartObjectiveDistance):
                    startObjectiveDistance = parsedStartObjectiveDistance;
                    index += 1;
                    break;
                case "--start-objective-distance-delta" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartObjectiveDistanceDelta):
                    startObjectiveDistanceDelta = parsedStartObjectiveDistanceDelta;
                    index += 1;
                    break;
                case "--start-prev-objective-distance-delta" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartPreviousObjectiveDistanceDelta):
                    startPreviousObjectiveDistanceDelta = parsedStartPreviousObjectiveDistanceDelta;
                    index += 1;
                    break;
                case "--start-airborne-ticks" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartAirborneTicks):
                    startAirborneTicks = parsedStartAirborneTicks;
                    index += 1;
                    break;
                case "--start-jump-ticks" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartJumpTicks):
                    startJumpTicks = parsedStartJumpTicks;
                    index += 1;
                    break;
                case "--start-frames-since-jump-pressed" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartFramesSinceJumpPressed):
                    startFramesSinceJumpPressed = parsedStartFramesSinceJumpPressed;
                    index += 1;
                    break;
                case "--start-frames-since-jump-released" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedStartFramesSinceJumpReleased):
                    startFramesSinceJumpReleased = parsedStartFramesSinceJumpReleased;
                    index += 1;
                    break;
                case "--move-hold-ticks" when int.TryParse(value, out var parsedMoveHoldTicks):
                    moveHoldTicks = Math.Max(0, parsedMoveHoldTicks);
                    index += 1;
                    break;
                case "--jump-hold-ticks" when int.TryParse(value, out var parsedJumpHoldTicks):
                    jumpHoldTicks = Math.Max(0, parsedJumpHoldTicks);
                    index += 1;
                    break;
            }
        }

        return new RolloutCommandLineOptions(
            levelName,
            team,
            classId,
            taskPhase,
            maxTicks,
            modelPath,
            returnFinalizerModelPath,
            returnFinalizerEngageDistance,
            returnFinalizerLevelNameFilter,
            returnFinalizerTeamFilter,
            returnFinalizerClassFilter,
            returnFinalizerAfterOptions,
            returnReplayBankPath,
            returnReplayEngageDistance,
            returnReplayMaxSelectionScore,
            replayBankPath,
            replayTaskPhase,
            replayEngageDistance,
            replayMaxSelectionScore,
            replayRequiresCarryingIntel,
            returnChunkModelPath,
            returnChunkEngageDistance,
            returnChunkCommitTicks,
            returnChunkOptions,
            returnChunkSelectorModelPath,
            returnSelectedChunkOptions,
            returnHierarchicalChunkModelPath,
            returnHierarchicalChunkCommitTicks,
            taskChunkOptions,
            taskOptions,
            stochastic,
            seed,
            temperature,
            startNodeId,
            startX,
            startY,
            startVelocityX,
            startVelocityY,
            carryingIntel,
            startIsGrounded,
            startRemainingAirJumps,
            startFacingDirectionX,
            startPreviousMoveInput,
            startPreviousJumpHeld,
            startPreviousDropInput,
            startPreviousFirePrimary,
            startPreviousFireSecondary,
            startPreviousPositionDeltaX,
            startPreviousPositionDeltaY,
            startPreviousVelocityX,
            startPreviousVelocityY,
            startPreviousFacingDirectionX,
            startPreviousIsGrounded,
            startObjectiveDistance,
            startObjectiveDistanceDelta,
            startPreviousObjectiveDistanceDelta,
            startAirborneTicks,
            startJumpTicks,
            startFramesSinceJumpPressed,
            startFramesSinceJumpReleased,
            disablePolicyOverrides,
            localTraversalOracle,
            localTraversalMpc,
            moveHoldTicks,
            jumpHoldTicks,
            compactRollout);
    }

    private static TaskOptionSpec ParseTaskOptionSpec(string rawValue)
    {
        var parts = rawValue.Split('|', StringSplitOptions.None);
        if (parts.Length < 2 || !Enum.TryParse<MLBotTaskPhase>(parts[1], ignoreCase: true, out var taskPhase))
        {
            throw new ArgumentException("--task-option-spec must be formatted as model|phase|engage_distance|map_filter|team_filter|class_filter|min_rel_x|max_rel_x|min_rel_y|max_rel_y");
        }

        var engageDistance = parts.Length > 2
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedEngage)
            ? Math.Max(0f, parsedEngage)
            : 0f;
        var levelNameFilter = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3])
            ? parts[3]
            : null;
        var teamFilter = parts.Length > 4 && Enum.TryParse<PlayerTeam>(parts[4], ignoreCase: true, out var parsedTeam)
            ? parsedTeam
            : (PlayerTeam?)null;
        var classFilter = parts.Length > 5 && Enum.TryParse<PlayerClass>(parts[5], ignoreCase: true, out var parsedClass)
            ? parsedClass
            : (PlayerClass?)null;
        var minObjectiveRelativeX = TryParseOptionalFloat(parts, 6);
        var maxObjectiveRelativeX = TryParseOptionalFloat(parts, 7);
        var minObjectiveRelativeY = TryParseOptionalFloat(parts, 8);
        var maxObjectiveRelativeY = TryParseOptionalFloat(parts, 9);
        return new TaskOptionSpec(
            parts[0],
            taskPhase,
            engageDistance,
            levelNameFilter,
            teamFilter,
            classFilter,
            minObjectiveRelativeX,
            maxObjectiveRelativeX,
            minObjectiveRelativeY,
            maxObjectiveRelativeY);
    }

    private static TaskChunkOptionSpec ParseTaskChunkOptionSpec(string rawValue)
    {
        var parts = rawValue.Split('|', StringSplitOptions.None);
        if (parts.Length < 2 || !Enum.TryParse<MLBotTaskPhase>(parts[1], ignoreCase: true, out var taskPhase))
        {
            throw new ArgumentException("--task-chunk-spec must be formatted as model|phase|engage_distance|commit_ticks|map_filter|team_filter|class_filter|min_rel_x|max_rel_x|min_rel_y|max_rel_y|requires_carrying_intel|min_engage_distance|latch_across_chunks|required_is_grounded");
        }

        var engageDistance = parts.Length > 2
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedEngage)
            ? Math.Max(0f, parsedEngage)
            : 0f;
        var commitTicks = parts.Length > 3 && int.TryParse(parts[3], out var parsedCommit)
            ? Math.Max(0, parsedCommit)
            : 0;
        var levelNameFilter = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4])
            ? parts[4]
            : null;
        var teamFilter = parts.Length > 5 && Enum.TryParse<PlayerTeam>(parts[5], ignoreCase: true, out var parsedTeam)
            ? parsedTeam
            : (PlayerTeam?)null;
        var classFilter = parts.Length > 6 && Enum.TryParse<PlayerClass>(parts[6], ignoreCase: true, out var parsedClass)
            ? parsedClass
            : (PlayerClass?)null;
        var minObjectiveRelativeX = TryParseOptionalFloat(parts, 7);
        var maxObjectiveRelativeX = TryParseOptionalFloat(parts, 8);
        var minObjectiveRelativeY = TryParseOptionalFloat(parts, 9);
        var maxObjectiveRelativeY = TryParseOptionalFloat(parts, 10);
        var requiresCarryingIntel = taskPhase == MLBotTaskPhase.ReturnIntel;
        if (parts.Length > 11 && !string.IsNullOrWhiteSpace(parts[11]))
        {
            requiresCarryingIntel = ParseBool(parts[11], requiresCarryingIntel);
        }
        var minEngageDistance = TryParseOptionalFloat(parts, 12) ?? 0f;
        var latchAcrossChunks = false;
        if (parts.Length > 13 && !string.IsNullOrWhiteSpace(parts[13]))
        {
            latchAcrossChunks = ParseBool(parts[13], fallback: false);
        }
        bool? requiredIsGrounded = null;
        if (parts.Length > 14 && !string.IsNullOrWhiteSpace(parts[14]) && TryParseBool(parts[14], out var parsedRequiredIsGrounded))
        {
            requiredIsGrounded = parsedRequiredIsGrounded;
        }

        return new TaskChunkOptionSpec(
            parts[0],
            taskPhase,
            engageDistance,
            commitTicks,
            levelNameFilter,
            teamFilter,
            classFilter,
            minObjectiveRelativeX,
            maxObjectiveRelativeX,
            minObjectiveRelativeY,
            maxObjectiveRelativeY,
            requiresCarryingIntel,
            Math.Max(0f, minEngageDistance),
            latchAcrossChunks,
            requiredIsGrounded);
    }

    private static ReturnSelectedChunkOptionSpec ParseReturnSelectedChunkOptionSpec(string rawValue)
    {
        var parts = rawValue.Split('|', StringSplitOptions.None);
        var modelPath = parts.Length > 0 ? parts[0] : string.Empty;
        var commitTicks = parts.Length > 1 && int.TryParse(parts[1], out var parsedCommit)
            ? Math.Max(0, parsedCommit)
            : 0;
        return new ReturnSelectedChunkOptionSpec(modelPath, commitTicks);
    }

    private static List<int> ParseCommitTicks(string rawValue)
    {
        return rawValue
            .Split([',', '|', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Max(0, parsed)
                : 0)
            .ToList();
    }

    private static ReturnChunkOptionSpec ParseReturnChunkOptionSpec(string rawValue)
    {
        var parts = rawValue.Split('|', StringSplitOptions.None);
        var modelPath = parts.Length > 0 ? parts[0] : string.Empty;
        var engageDistance = parts.Length > 1
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedEngage)
            ? Math.Max(0f, parsedEngage)
            : 0f;
        var commitTicks = parts.Length > 2 && int.TryParse(parts[2], out var parsedCommit)
            ? Math.Max(0, parsedCommit)
            : 0;
        var levelNameFilter = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3])
            ? parts[3]
            : null;
        var teamFilter = parts.Length > 4 && Enum.TryParse<PlayerTeam>(parts[4], ignoreCase: true, out var parsedTeam)
            ? parsedTeam
            : (PlayerTeam?)null;
        var classFilter = parts.Length > 5 && Enum.TryParse<PlayerClass>(parts[5], ignoreCase: true, out var parsedClass)
            ? parsedClass
            : (PlayerClass?)null;
        var minObjectiveRelativeX = TryParseOptionalFloat(parts, 6);
        var maxObjectiveRelativeX = TryParseOptionalFloat(parts, 7);
        var minObjectiveRelativeY = TryParseOptionalFloat(parts, 8);
        var maxObjectiveRelativeY = TryParseOptionalFloat(parts, 9);
        return new ReturnChunkOptionSpec(
            modelPath,
            engageDistance,
            commitTicks,
            levelNameFilter,
            teamFilter,
            classFilter,
            minObjectiveRelativeX,
            maxObjectiveRelativeX,
            minObjectiveRelativeY,
            maxObjectiveRelativeY);
    }

    private static float? TryParseOptionalFloat(string[] parts, int index)
    {
        return parts.Length > index
            && !string.IsNullOrWhiteSpace(parts[index])
            && float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool ParseBool(string value, bool fallback)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "yes" or "y" or "carry" or "carrying" => true,
            "0" or "no" or "n" or "none" or "not-carrying" => false,
            _ => fallback,
        };
    }

    private static bool TryParseBool(string value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
            case "true":
                parsed = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "false":
                parsed = false;
                return true;
            default:
                parsed = false;
                return false;
        }
    }
}

internal sealed record ScenarioCommandLineOptions(
    string? LevelName,
    PlayerTeam? Team,
    PlayerClass? ClassId,
    MLBotTaskPhase? TaskPhase)
{
    public static ScenarioCommandLineOptions Parse(string[] args)
    {
        string? levelName = null;
        PlayerTeam? team = null;
        PlayerClass? classId = null;
        MLBotTaskPhase? taskPhase = null;

        for (var index = 0; index < args.Length; index += 1)
        {
            var arg = args[index];
            if (index + 1 >= args.Length)
            {
                continue;
            }

            var value = args[index + 1];
            switch (arg)
            {
                case "--map":
                    levelName = value;
                    index += 1;
                    break;
                case "--team" when Enum.TryParse<PlayerTeam>(value, ignoreCase: true, out var parsedTeam):
                    team = parsedTeam;
                    index += 1;
                    break;
                case "--class" when Enum.TryParse<PlayerClass>(value, ignoreCase: true, out var parsedClass):
                    classId = parsedClass;
                    index += 1;
                    break;
                case "--task" when Enum.TryParse<MLBotTaskPhase>(value, ignoreCase: true, out var parsedTask):
                    taskPhase = parsedTask;
                    index += 1;
                    break;
            }
        }

        return new ScenarioCommandLineOptions(levelName, team, classId, taskPhase);
    }
}
