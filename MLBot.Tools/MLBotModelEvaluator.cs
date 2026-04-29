using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Environment;
using OpenGarrison.MLBot.Policies;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotModelEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int RunDemoSuite(string root, string modelPath, int maxTicks, int repetitions, string? traceDirectory, ScenarioCommandLineOptions? scenarioFilter = null)
    {
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"demo root does not exist: {root}");
            return 1;
        }

        if (!File.Exists(modelPath))
        {
            Console.Error.WriteLine($"model file does not exist: {modelPath}");
            return 1;
        }

        var scenarios = DiscoverScenarios(root, scenarioFilter).ToArray();
        if (scenarios.Length == 0)
        {
            Console.Error.WriteLine($"no demos found under {root}");
            return 1;
        }

        using var policy = new OnnxMLBotPolicyRuntime(modelPath);
        var environment = new MLBotEnvironment();
        var totalRuns = 0;
        var totalSuccesses = 0;

        foreach (var scenario in scenarios.OrderBy(static item => item.LevelName).ThenBy(static item => item.Team).ThenBy(static item => item.ClassId).ThenBy(static item => item.TaskPhase))
        {
            var successes = 0;
            var bestTicks = int.MaxValue;
            var totalTicks = 0;
            var outcomeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var attempt = 0; attempt < repetitions; attempt += 1)
            {
                var episode = RunEpisode(environment, policy, scenario, maxTicks);
                totalRuns += 1;
                totalTicks += episode.Result.TicksElapsed;
                IncrementCount(outcomeCounts, episode.Trace.TerminalReason);
                if (episode.Result.Success)
                {
                    totalSuccesses += 1;
                    successes += 1;
                    bestTicks = Math.Min(bestTicks, episode.Result.TicksElapsed);
                }

                if (!string.IsNullOrWhiteSpace(traceDirectory))
                {
                    SaveTrace(traceDirectory, scenario, attempt, episode.Trace);
                }
            }

            var averageTicks = totalTicks / (float)repetitions;
            var successRate = successes / (float)repetitions;
            Console.WriteLine(
                $"level={scenario.LevelName} team={scenario.Team} class={scenario.ClassId} task={DescribeTaskPhase(scenario.TaskPhase)} success_rate={successRate:0.00} avg_ticks={averageTicks:0.0} best_ticks={(bestTicks == int.MaxValue ? 0 : bestTicks)} outcomes={FormatOutcomeCounts(outcomeCounts)}");
        }

        Console.WriteLine(
            $"summary scenarios={scenarios.Length} runs={totalRuns} successes={totalSuccesses} success_rate={(totalSuccesses / (float)totalRuns):0.00}");
        return 0;
    }

    public static MLBotEpisodeRunResult RunEpisode(
        MLBotEnvironment environment,
        IMLBotPolicyRuntime policy,
        MLBotEpisodeConfig config,
        int maxTicks)
    {
        var effectiveConfig = config with { MaxTicks = maxTicks };
        var observation = environment.Reset(effectiveConfig);
        var initialNavigationDistance = GetNavigationDistance(observation);
        var initialWaypointDistance = GetWaypointDistance(observation);
        var totalReward = 0f;
        MLBotStepResult step = default;
        var trace = new MLBotEvaluationTrace
        {
            LevelName = effectiveConfig.LevelName,
            Team = effectiveConfig.Team,
            ClassId = effectiveConfig.ClassId,
            TaskPhase = effectiveConfig.TaskPhase,
            MinObjectiveDistance = observation.ObjectiveDistance,
            FinalObjectiveDistance = observation.ObjectiveDistance,
            MinNavigationDistance = initialNavigationDistance,
            FinalNavigationDistance = initialNavigationDistance,
            MinWaypointDistance = initialWaypointDistance,
            FinalWaypointDistance = initialWaypointDistance,
            FinalPhase = observation.TaskPhase,
        };
        var events = new List<MLBotTraceEvent>();
        var previousObservation = observation;

        for (var tick = 0; tick < effectiveConfig.MaxTicks; tick += 1)
        {
            var action = policy.Evaluate(observation);
            step = environment.Step(action);
            totalReward += step.Reward.Total;
            observation = step.Observation;
            var navigationDistance = GetNavigationDistance(observation);
            var waypointDistance = GetWaypointDistance(observation);
            trace.MaxStuckTicks = Math.Max(trace.MaxStuckTicks, observation.StuckTicks);
            trace.MinObjectiveDistance = Math.Min(trace.MinObjectiveDistance, observation.ObjectiveDistance);
            trace.FinalObjectiveDistance = observation.ObjectiveDistance;
            trace.MinNavigationDistance = Math.Min(trace.MinNavigationDistance, navigationDistance);
            trace.FinalNavigationDistance = navigationDistance;
            trace.MinWaypointDistance = Math.Min(trace.MinWaypointDistance, waypointDistance);
            trace.FinalWaypointDistance = waypointDistance;
            trace.FinalPhase = observation.TaskPhase;

            if (observation.TaskPhase != previousObservation.TaskPhase)
            {
                events.Add(new MLBotTraceEvent
                {
                    Tick = step.Tick,
                    Kind = "phase_change",
                    Value = $"{previousObservation.TaskPhase}->{observation.TaskPhase}",
                });
            }

            if (!previousObservation.IsCarryingIntel && observation.IsCarryingIntel)
            {
                trace.PickupTick ??= step.Tick;
                events.Add(new MLBotTraceEvent
                {
                    Tick = step.Tick,
                    Kind = "picked_up_intel",
                    Value = observation.TaskPhase.ToString(),
                });
            }

            if (previousObservation.IsCarryingIntel && !observation.IsCarryingIntel && step.IsSuccess)
            {
                trace.ScoreTick ??= step.Tick;
                events.Add(new MLBotTraceEvent
                {
                    Tick = step.Tick,
                    Kind = "scored",
                    Value = step.TerminalReason,
                });
            }

            if (step.IsSuccess && string.Equals(step.TerminalReason, "captured", StringComparison.OrdinalIgnoreCase))
            {
                trace.CaptureTick ??= step.Tick;
                events.Add(new MLBotTraceEvent
                {
                    Tick = step.Tick,
                    Kind = "captured",
                    Value = observation.TaskPhase.ToString(),
                });
            }

            previousObservation = observation;
            if (step.IsTerminal)
            {
                break;
            }
        }

        var result = new MLBotEvaluationResult(
            LevelName: effectiveConfig.LevelName,
            Team: effectiveConfig.Team,
            ClassId: effectiveConfig.ClassId,
            TaskPhase: effectiveConfig.TaskPhase,
            Success: step.IsSuccess,
            TicksElapsed: step.Tick,
            TotalReward: totalReward,
            Outcome: step.IsSuccess ? "success" : step.IsTerminal ? "terminal_failure" : "timeout");
        trace.Success = result.Success;
        trace.TicksElapsed = result.TicksElapsed;
        trace.TotalReward = result.TotalReward;
        trace.Outcome = result.Outcome;
        trace.TerminalReason = string.IsNullOrWhiteSpace(step.TerminalReason) ? result.Outcome : step.TerminalReason;
        trace.Events = events.ToArray();
        return new MLBotEpisodeRunResult(result, trace);
    }

    private static float GetNavigationDistance(in MLBotObservation observation)
    {
        return observation.Waypoint is { HasWaypoint: true, IsFinalWaypoint: false }
            ? observation.Waypoint.Distance
            : observation.ObjectiveDistance;
    }

    private static float GetWaypointDistance(in MLBotObservation observation)
    {
        return observation.Waypoint.HasWaypoint ? observation.Waypoint.Distance : observation.ObjectiveDistance;
    }

    private static IEnumerable<MLBotEpisodeConfig> DiscoverScenarios(string root, ScenarioCommandLineOptions? scenarioFilter)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            MLBotDemonstrationDocument? document;
            try
            {
                document = JsonSerializer.Deserialize<MLBotDemonstrationDocument>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception)
            {
                continue;
            }

            if (document?.Metadata is not { } metadata)
            {
                continue;
            }

            if (scenarioFilter is not null)
            {
                if (!string.IsNullOrWhiteSpace(scenarioFilter.LevelName)
                    && !string.Equals(metadata.LevelName, scenarioFilter.LevelName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (scenarioFilter.Team is not null && metadata.Team != scenarioFilter.Team.Value)
                {
                    continue;
                }

                if (scenarioFilter.ClassId is not null && metadata.ClassId != scenarioFilter.ClassId.Value)
                {
                    continue;
                }

                if (scenarioFilter.TaskPhase is not null && metadata.RequestedPhase != scenarioFilter.TaskPhase.Value)
                {
                    continue;
                }
            }

            var key = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{metadata.LevelName}|{metadata.MapAreaIndex}|{metadata.Team}|{metadata.ClassId}|{metadata.RequestedPhase}");
            if (!seen.Add(key))
            {
                continue;
            }

            yield return new MLBotEpisodeConfig(
                metadata.LevelName,
                metadata.MapAreaIndex,
                metadata.Team,
                metadata.ClassId,
                metadata.RequestedPhase,
                Math.Max(1, metadata.TickCount));
        }
    }

    private static string DescribeTaskPhase(MLBotTaskPhase taskPhase)
    {
        return taskPhase == MLBotTaskPhase.None ? "Auto" : taskPhase.ToString();
    }

    private static string FormatOutcomeCounts(Dictionary<string, int> outcomeCounts)
    {
        return string.Join(",", outcomeCounts.OrderBy(static pair => pair.Key).Select(static pair => $"{pair.Key}:{pair.Value}"));
    }

    private static void IncrementCount(Dictionary<string, int> outcomeCounts, string key)
    {
        var resolvedKey = string.IsNullOrWhiteSpace(key) ? "unknown" : key;
        outcomeCounts.TryGetValue(resolvedKey, out var count);
        outcomeCounts[resolvedKey] = count + 1;
    }

    private static void SaveTrace(string root, MLBotEpisodeConfig config, int attempt, MLBotEvaluationTrace trace)
    {
        Directory.CreateDirectory(root);
        var fileName = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{config.LevelName}_{config.Team}_{config.ClassId}_{DescribeTaskPhase(config.TaskPhase)}_{attempt:D2}.json");
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(trace, JsonOptions));
    }
}

internal readonly record struct MLBotEpisodeRunResult(
    MLBotEvaluationResult Result,
    MLBotEvaluationTrace Trace);
