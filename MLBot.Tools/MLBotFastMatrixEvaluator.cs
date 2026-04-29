using OpenGarrison.Core;
using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Environment;
using OpenGarrison.MLBot.Tools;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class MLBotFastMatrixEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int Run(string[] args)
    {
        var options = FastMatrixOptions.Parse(args);
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            Console.Error.WriteLine("missing required option: --output-dir");
            return 1;
        }

        if (options.ScenarioFiles.Count == 0)
        {
            Console.Error.WriteLine("missing required option: --scenario-file");
            return 1;
        }

        var scenarios = options.ScenarioFiles.SelectMany(LoadScenarioFile).ToArray();
        if (options.FocusScenarios.Count > 0)
        {
            scenarios = scenarios
                .Where(scenario => options.FocusScenarios.Contains(scenario.Name))
                .ToArray();
        }

        if (options.MaxScenarios > 0)
        {
            scenarios = scenarios.Take(options.MaxScenarios).ToArray();
        }

        if (scenarios.Length == 0)
        {
            Console.Error.WriteLine("no scenarios selected");
            return 1;
        }

        var models = options.Models.Count > 0
            ? options.Models
            : [new FastMatrixModel("direct", string.Empty)];

        Directory.CreateDirectory(options.OutputDirectory);
        var results = new List<FastMatrixResult>(models.Count * scenarios.Length);
        var totalStopwatch = Stopwatch.StartNew();
        foreach (var model in models)
        {
            var policyArgs = options.ConfigFiles
                .SelectMany(LoadStackPolicyArgs)
                .Concat(options.PolicyArgs)
                .ToList();
            if (!string.IsNullOrWhiteSpace(model.Path)
                && !string.Equals(model.Path, "direct", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(model.Path, "direct-objective", StringComparison.OrdinalIgnoreCase))
            {
                policyArgs.Add("--model");
                policyArgs.Add(model.Path);
            }

            var environment = new MLBotEnvironment();
            foreach (var scenario in scenarios)
            {
                var policyOptions = RolloutCommandLineOptions.Parse(policyArgs.ToArray());
                var policy = MLBotPolicyRuntimeBuilder.CreatePolicy(policyOptions);
                using var disposablePolicy = policy as IDisposable;
                var scenarioStopwatch = Stopwatch.StartNew();
                var config = scenario.ToEpisodeConfig();
                var episode = MLBotModelEvaluator.RunEpisode(environment, policy, config, config.MaxTicks);
                scenarioStopwatch.Stop();

                var criterionSuccess = EvaluateSuccessCriterion(episode.Trace, scenario.SuccessCriterion);
                var stuckGateSuccess = options.MaxStuckTicks <= 0f || episode.Trace.MaxStuckTicks <= options.MaxStuckTicks;
                var success = criterionSuccess && stuckGateSuccess;
                var tracePath = Path.Combine(options.OutputDirectory, $"{model.Name}__{scenario.Name}.json");
                File.WriteAllText(tracePath, JsonSerializer.Serialize(episode.Trace, JsonOptions));
                var result = new FastMatrixResult(
                    model,
                    scenario,
                    success,
                    episode.Trace.Success,
                    scenario.SuccessCriterion,
                    episode.Trace.TerminalReason,
                    episode.Trace.TicksElapsed,
                    episode.Trace.PickupTick,
                    episode.Trace.ScoreTick,
                    episode.Trace.CaptureTick,
                    episode.Trace.MinNavigationDistance,
                    episode.Trace.FinalNavigationDistance,
                    episode.Trace.MaxStuckTicks,
                    scenarioStopwatch.ElapsedMilliseconds,
                    tracePath);
                results.Add(result);
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"model={model.Name} scenario={scenario.Name} success={result.Success} criterion={result.SuccessCriterion} raw_success={result.RawTerminalSuccess} terminal={result.TerminalReason} ticks={result.TicksElapsed} elapsed_ms={result.ElapsedMilliseconds} pickup={FormatTick(result.PickupTick)} score={FormatTick(result.ScoreTick)} capture={FormatTick(result.CaptureTick)} min_nav={result.MinNavigationDistance:0.0} final_nav={result.FinalNavigationDistance:0.0} max_stuck={result.MaxStuckTicks:0.0}"));

                if (options.StopOnFailure && !success)
                {
                    WriteSummary(options, models, scenarios, results, totalStopwatch.ElapsedMilliseconds, completed: false);
                    return 1;
                }
            }
        }

        totalStopwatch.Stop();
        WriteSummary(options, models, scenarios, results, totalStopwatch.ElapsedMilliseconds, completed: true);
        return 0;
    }

    private static FastMatrixScenario[] LoadScenarioFile(string path)
    {
        var document = JsonSerializer.Deserialize<FastMatrixScenarioDocument>(File.ReadAllText(path), JsonOptions)
            ?? new FastMatrixScenarioDocument();
        return document.Scenarios ?? [];
    }

    private static bool EvaluateSuccessCriterion(MLBotEvaluationTrace trace, string criterion)
    {
        var normalized = criterion.Trim().ToLowerInvariant().Replace("-", "_");
        var terminalReason = trace.TerminalReason.ToLowerInvariant();
        return normalized switch
        {
            "" or "terminal" or "terminal_success" => trace.Success,
            "attack_pickup" or "pickup" or "intel_pickup" => trace.PickupTick is not null || terminalReason == "picked_up_intel",
            "return_score" or "score" or "scored" => trace.ScoreTick is not null || terminalReason == "scored",
            "full_score" or "ctf_score" or "attack_return_score" => trace.ScoreTick is not null || terminalReason is "scored" or "completed_primary_objective",
            "capture" or "cap" or "capture_hold" or "koth_capture_hold" => trace.CaptureTick is not null || terminalReason == "captured",
            _ => throw new ArgumentException($"unknown success_criterion: {criterion}"),
        };
    }

    private static void WriteSummary(
        FastMatrixOptions options,
        IReadOnlyList<FastMatrixModel> models,
        IReadOnlyList<FastMatrixScenario> scenarios,
        IReadOnlyList<FastMatrixResult> results,
        long elapsedMilliseconds,
        bool completed)
    {
        var summary = new
        {
            schema = "mlbot-fast-matrix-v1",
            completed,
            elapsed_ms = elapsedMilliseconds,
            max_stuck_ticks = options.MaxStuckTicks,
            models = models.Select(static model => new
            {
                name = model.Name,
                path = model.Path,
            }).ToArray(),
            scenarios = scenarios.Select(static scenario => new
            {
                name = scenario.Name,
                level_name = scenario.LevelName,
                team = scenario.Team.ToString(),
                class_id = scenario.ClassId.ToString(),
                task = scenario.Task.ToString(),
                ticks = scenario.Ticks,
                start_node_id = scenario.StartNodeId,
                start_x = scenario.StartX,
                start_y = scenario.StartY,
                start_vx = scenario.StartVelocityX,
                start_vy = scenario.StartVelocityY,
                carrying_intel = scenario.CarryingIntel,
                start_is_grounded = scenario.StartIsGrounded,
                start_remaining_air_jumps = scenario.StartRemainingAirJumps,
                start_facing_dir_x = scenario.StartFacingDirectionX,
                start_prev_move = scenario.StartPreviousMoveInput,
                start_prev_jump_held = scenario.StartPreviousJumpHeld,
                start_prev_drop = scenario.StartPreviousDropInput,
                start_prev_fire_primary = scenario.StartPreviousFirePrimary,
                start_prev_fire_secondary = scenario.StartPreviousFireSecondary,
                start_prev_dx = scenario.StartPreviousPositionDeltaX,
                start_prev_dy = scenario.StartPreviousPositionDeltaY,
                start_prev_vx = scenario.StartPreviousVelocityX,
                start_prev_vy = scenario.StartPreviousVelocityY,
                start_prev_facing_dir_x = scenario.StartPreviousFacingDirectionX,
                start_prev_is_grounded = scenario.StartPreviousIsGrounded,
                start_objective_distance = scenario.StartObjectiveDistance,
                start_objective_distance_delta = scenario.StartObjectiveDistanceDelta,
                start_prev_objective_distance_delta = scenario.StartPreviousObjectiveDistanceDelta,
                start_airborne_ticks = scenario.StartAirborneTicks,
                start_jump_ticks = scenario.StartJumpTicks,
                start_frames_since_jump_pressed = scenario.StartFramesSinceJumpPressed,
                start_frames_since_jump_released = scenario.StartFramesSinceJumpReleased,
                success_criterion = scenario.SuccessCriterion,
            }).ToArray(),
            results = results.Select(static result => new
            {
                model = new
                {
                    name = result.Model.Name,
                    path = result.Model.Path,
                },
                scenario = new
                {
                    name = result.Scenario.Name,
                    level_name = result.Scenario.LevelName,
                    team = result.Scenario.Team.ToString(),
                    class_id = result.Scenario.ClassId.ToString(),
                    task = result.Scenario.Task.ToString(),
                    ticks = result.Scenario.Ticks,
                    start_node_id = result.Scenario.StartNodeId,
                    start_x = result.Scenario.StartX,
                    start_y = result.Scenario.StartY,
                    start_vx = result.Scenario.StartVelocityX,
                    start_vy = result.Scenario.StartVelocityY,
                    carrying_intel = result.Scenario.CarryingIntel,
                    start_is_grounded = result.Scenario.StartIsGrounded,
                    start_remaining_air_jumps = result.Scenario.StartRemainingAirJumps,
                    start_facing_dir_x = result.Scenario.StartFacingDirectionX,
                    start_prev_move = result.Scenario.StartPreviousMoveInput,
                    start_prev_jump_held = result.Scenario.StartPreviousJumpHeld,
                    start_prev_drop = result.Scenario.StartPreviousDropInput,
                    start_prev_fire_primary = result.Scenario.StartPreviousFirePrimary,
                    start_prev_fire_secondary = result.Scenario.StartPreviousFireSecondary,
                    start_prev_dx = result.Scenario.StartPreviousPositionDeltaX,
                    start_prev_dy = result.Scenario.StartPreviousPositionDeltaY,
                    start_prev_vx = result.Scenario.StartPreviousVelocityX,
                    start_prev_vy = result.Scenario.StartPreviousVelocityY,
                    start_prev_facing_dir_x = result.Scenario.StartPreviousFacingDirectionX,
                    start_prev_is_grounded = result.Scenario.StartPreviousIsGrounded,
                    start_objective_distance = result.Scenario.StartObjectiveDistance,
                    start_objective_distance_delta = result.Scenario.StartObjectiveDistanceDelta,
                    start_prev_objective_distance_delta = result.Scenario.StartPreviousObjectiveDistanceDelta,
                    start_airborne_ticks = result.Scenario.StartAirborneTicks,
                    start_jump_ticks = result.Scenario.StartJumpTicks,
                    start_frames_since_jump_pressed = result.Scenario.StartFramesSinceJumpPressed,
                    start_frames_since_jump_released = result.Scenario.StartFramesSinceJumpReleased,
                    success_criterion = result.Scenario.SuccessCriterion,
                },
                success = result.Success,
                raw_terminal_success = result.RawTerminalSuccess,
                success_criterion = result.SuccessCriterion,
                terminal_reason = result.TerminalReason,
                ticks_elapsed = result.TicksElapsed,
                pickup_tick = result.PickupTick,
                score_tick = result.ScoreTick,
                capture_tick = result.CaptureTick,
                min_navigation_distance = result.MinNavigationDistance,
                final_navigation_distance = result.FinalNavigationDistance,
                max_stuck_ticks = result.MaxStuckTicks,
                elapsed_ms = result.ElapsedMilliseconds,
                trace_path = result.TracePath,
            }).ToArray(),
        };
        var path = Path.Combine(options.OutputDirectory, "matrix-summary.json");
        File.WriteAllText(path, JsonSerializer.Serialize(summary, JsonOptions));
    }

    private static string FormatTick(int? tick)
    {
        return tick?.ToString(CultureInfo.InvariantCulture) ?? "none";
    }

    private sealed record FastMatrixOptions(
        List<FastMatrixModel> Models,
        List<string> ScenarioFiles,
        List<string> ConfigFiles,
        List<string> FocusScenarios,
        string OutputDirectory,
        bool StopOnFailure,
        int MaxScenarios,
        float MaxStuckTicks,
        List<string> PolicyArgs)
    {
        public static FastMatrixOptions Parse(string[] args)
        {
            var models = new List<FastMatrixModel>();
            var scenarioFiles = new List<string>();
            var configFiles = new List<string>();
            var focusScenarios = new List<string>();
            var policyArgs = new List<string>();
            var outputDirectory = string.Empty;
            var stopOnFailure = false;
            var maxScenarios = 0;
            var maxStuckTicks = 0f;

            for (var index = 0; index < args.Length; index += 1)
            {
                var arg = args[index];
                if (string.Equals(arg, "--stop-on-failure", StringComparison.OrdinalIgnoreCase))
                {
                    stopOnFailure = true;
                    continue;
                }

                if (IsPassthroughPolicyFlag(arg))
                {
                    policyArgs.Add(arg);
                    continue;
                }

                if (index + 1 >= args.Length)
                {
                    policyArgs.Add(arg);
                    continue;
                }

                var value = args[index + 1];
                switch (arg)
                {
                    case "--model":
                        models.Add(ParseModel(value));
                        index += 1;
                        break;
                    case "--scenario-file":
                        scenarioFiles.Add(value);
                        index += 1;
                        break;
                    case "--config":
                        configFiles.Add(value);
                        index += 1;
                        break;
                    case "--focus-scenario":
                        focusScenarios.Add(value);
                        index += 1;
                        break;
                    case "--output-dir":
                        outputDirectory = value;
                        index += 1;
                        break;
                    case "--max-scenarios" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxScenarios):
                        maxScenarios = Math.Max(0, parsedMaxScenarios);
                        index += 1;
                        break;
                    case "--max-stuck-ticks" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMaxStuckTicks):
                        maxStuckTicks = Math.Max(0f, parsedMaxStuckTicks);
                        index += 1;
                        break;
                    default:
                        policyArgs.Add(arg);
                        policyArgs.Add(value);
                        index += 1;
                        break;
                }
            }

            return new FastMatrixOptions(models, scenarioFiles, configFiles, focusScenarios, outputDirectory, stopOnFailure, maxScenarios, maxStuckTicks, policyArgs);
        }

        private static FastMatrixModel ParseModel(string rawValue)
        {
            var separatorIndex = rawValue.IndexOf('=');
            if (separatorIndex < 0)
            {
                return new FastMatrixModel(Path.GetFileNameWithoutExtension(rawValue), rawValue);
            }

            return new FastMatrixModel(
                rawValue[..separatorIndex],
                rawValue[(separatorIndex + 1)..]);
        }

        private static bool IsPassthroughPolicyFlag(string value)
        {
            return value.Equals("--stochastic", StringComparison.OrdinalIgnoreCase)
                || value.Equals("--disable-policy-overrides", StringComparison.OrdinalIgnoreCase)
                || value.Equals("--local-traversal-oracle", StringComparison.OrdinalIgnoreCase)
                || value.Equals("--return-finalizer-after-options", StringComparison.OrdinalIgnoreCase)
                || value.Equals("--replay-requires-carrying-intel", StringComparison.OrdinalIgnoreCase)
                || value.Equals("--no-replay-requires-carrying-intel", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static List<string> LoadStackPolicyArgs(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var args = new List<string>();
        if (root.TryGetProperty("disable_policy_overrides", out var disablePolicyOverrides)
            && disablePolicyOverrides.ValueKind == JsonValueKind.True)
        {
            args.Add("--disable-policy-overrides");
        }

        if (TryGetString(root, "return_hierarchical_chunk_model") is { Length: > 0 } hierarchicalModel)
        {
            args.Add("--return-hierarchical-chunk-model");
            args.Add(hierarchicalModel);
        }

        if (root.TryGetProperty("return_hierarchical_chunk_commit_ticks", out var commitTicks)
            && commitTicks.ValueKind == JsonValueKind.Array)
        {
            var rawTicks = commitTicks
                .EnumerateArray()
                .Select(static item => item.GetInt32().ToString(CultureInfo.InvariantCulture));
            args.Add("--return-hierarchical-chunk-commit-ticks");
            args.Add(string.Join(",", rawTicks));
        }

        AddSpecArray(root, "task_options", "--task-option-spec", args);
        AddSpecArray(root, "task_chunks", "--task-chunk-spec", args);
        AddSpecArray(root, "return_chunks", "--return-chunk-spec", args);
        return args;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static void AddSpecArray(JsonElement root, string propertyName, string optionName, List<string> args)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            args.Add(optionName);
            args.Add(value);
        }
    }
}

internal sealed class FastMatrixScenarioDocument
{
    [JsonPropertyName("scenarios")]
    public FastMatrixScenario[]? Scenarios { get; set; }
}

internal sealed record FastMatrixModel(
    string Name,
    string Path);

internal sealed record FastMatrixScenario(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("level_name")] string LevelName,
    [property: JsonPropertyName("team")] PlayerTeam Team,
    [property: JsonPropertyName("class_id")] PlayerClass ClassId,
    [property: JsonPropertyName("task")] MLBotTaskPhase Task,
    [property: JsonPropertyName("ticks")] int Ticks,
    [property: JsonPropertyName("start_node_id")] int StartNodeId = -1,
    [property: JsonPropertyName("start_x")] float? StartX = null,
    [property: JsonPropertyName("start_y")] float? StartY = null,
    [property: JsonPropertyName("start_vx")] float? StartVelocityX = null,
    [property: JsonPropertyName("start_vy")] float? StartVelocityY = null,
    [property: JsonPropertyName("carrying_intel")] bool? CarryingIntel = null,
    [property: JsonPropertyName("start_is_grounded")] bool? StartIsGrounded = null,
    [property: JsonPropertyName("start_remaining_air_jumps")] int? StartRemainingAirJumps = null,
    [property: JsonPropertyName("start_facing_dir_x")] float? StartFacingDirectionX = null,
    [property: JsonPropertyName("start_prev_move")] int? StartPreviousMoveInput = null,
    [property: JsonPropertyName("start_prev_jump_held")] bool? StartPreviousJumpHeld = null,
    [property: JsonPropertyName("start_prev_drop")] bool? StartPreviousDropInput = null,
    [property: JsonPropertyName("start_prev_fire_primary")] bool? StartPreviousFirePrimary = null,
    [property: JsonPropertyName("start_prev_fire_secondary")] bool? StartPreviousFireSecondary = null,
    [property: JsonPropertyName("start_prev_dx")] float? StartPreviousPositionDeltaX = null,
    [property: JsonPropertyName("start_prev_dy")] float? StartPreviousPositionDeltaY = null,
    [property: JsonPropertyName("start_prev_vx")] float? StartPreviousVelocityX = null,
    [property: JsonPropertyName("start_prev_vy")] float? StartPreviousVelocityY = null,
    [property: JsonPropertyName("start_prev_facing_dir_x")] float? StartPreviousFacingDirectionX = null,
    [property: JsonPropertyName("start_prev_is_grounded")] bool? StartPreviousIsGrounded = null,
    [property: JsonPropertyName("start_objective_distance")] float? StartObjectiveDistance = null,
    [property: JsonPropertyName("start_objective_distance_delta")] float? StartObjectiveDistanceDelta = null,
    [property: JsonPropertyName("start_prev_objective_distance_delta")] float? StartPreviousObjectiveDistanceDelta = null,
    [property: JsonPropertyName("start_airborne_ticks")] float? StartAirborneTicks = null,
    [property: JsonPropertyName("start_jump_ticks")] float? StartJumpTicks = null,
    [property: JsonPropertyName("start_frames_since_jump_pressed")] float? StartFramesSinceJumpPressed = null,
    [property: JsonPropertyName("start_frames_since_jump_released")] float? StartFramesSinceJumpReleased = null,
    [property: JsonPropertyName("success_criterion")] string SuccessCriterion = "terminal_success")
{
    public MLBotEpisodeConfig ToEpisodeConfig()
    {
        return MLBotEpisodeConfig.CreateDefault(
            levelName: LevelName,
            taskPhase: Task,
            team: Team,
            classId: ClassId,
            maxTicks: Ticks,
            startNodeId: StartNodeId,
            startX: StartX,
            startY: StartY,
            startVelocityX: StartVelocityX,
            startVelocityY: StartVelocityY,
            carryingIntel: CarryingIntel,
            startIsGrounded: StartIsGrounded,
            startRemainingAirJumps: StartRemainingAirJumps,
            startFacingDirectionX: StartFacingDirectionX,
            startPreviousMoveInput: StartPreviousMoveInput,
            startPreviousJumpHeld: StartPreviousJumpHeld,
            startPreviousDropInput: StartPreviousDropInput,
            startPreviousFirePrimary: StartPreviousFirePrimary,
            startPreviousFireSecondary: StartPreviousFireSecondary,
            startPreviousPositionDeltaX: StartPreviousPositionDeltaX,
            startPreviousPositionDeltaY: StartPreviousPositionDeltaY,
            startPreviousVelocityX: StartPreviousVelocityX,
            startPreviousVelocityY: StartPreviousVelocityY,
            startPreviousFacingDirectionX: StartPreviousFacingDirectionX,
            startPreviousIsGrounded: StartPreviousIsGrounded,
            startObjectiveDistance: StartObjectiveDistance,
            startObjectiveDistanceDelta: StartObjectiveDistanceDelta,
            startPreviousObjectiveDistanceDelta: StartPreviousObjectiveDistanceDelta,
            startAirborneTicks: StartAirborneTicks,
            startJumpTicks: StartJumpTicks,
            startFramesSinceJumpPressed: StartFramesSinceJumpPressed,
            startFramesSinceJumpReleased: StartFramesSinceJumpReleased);
    }
}

internal sealed record FastMatrixResult(
    FastMatrixModel Model,
    FastMatrixScenario Scenario,
    bool Success,
    bool RawTerminalSuccess,
    string SuccessCriterion,
    string TerminalReason,
    int TicksElapsed,
    int? PickupTick,
    int? ScoreTick,
    int? CaptureTick,
    float MinNavigationDistance,
    float FinalNavigationDistance,
    float MaxStuckTicks,
    long ElapsedMilliseconds,
    string TracePath);

internal sealed record FastMatrixSummary(
    string Schema,
    bool Completed,
    long ElapsedMilliseconds,
    IReadOnlyList<FastMatrixModel> Models,
    IReadOnlyList<FastMatrixScenario> Scenarios,
    IReadOnlyList<FastMatrixResult> Results);
