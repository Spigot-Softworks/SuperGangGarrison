using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Environment;
using OpenGarrison.MLBot.Policies;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotRolloutExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int ExportEpisode(
        MLBotEpisodeConfig config,
        string? modelPath,
        int maxTicks,
        string outputPath,
        bool stochastic = false,
        int seed = 1337,
        float temperature = 1f,
        bool disablePolicyOverrides = false,
        bool localTraversalOracle = false,
        bool localTraversalMpc = false,
        int moveHoldTicks = 0,
        int jumpHoldTicks = 0,
        string? returnFinalizerModelPath = null,
        float returnFinalizerEngageDistance = 0f,
        string? returnFinalizerLevelNameFilter = null,
        PlayerTeam? returnFinalizerTeamFilter = null,
        PlayerClass? returnFinalizerClassFilter = null,
        bool returnFinalizerAfterOptions = false,
        string? returnReplayBankPath = null,
        float returnReplayEngageDistance = 0f,
        float returnReplayMaxSelectionScore = 0f,
        string? replayBankPath = null,
        MLBotTaskPhase replayTaskPhase = MLBotTaskPhase.ReturnIntel,
        float replayEngageDistance = 0f,
        float replayMaxSelectionScore = 0f,
        bool replayRequiresCarryingIntel = true,
        string? returnChunkModelPath = null,
        float returnChunkEngageDistance = 0f,
        int returnChunkCommitTicks = 0,
        IReadOnlyList<ReturnChunkOptionSpec>? returnChunkOptions = null,
        string? returnChunkSelectorModelPath = null,
        IReadOnlyList<ReturnSelectedChunkOptionSpec>? returnSelectedChunkOptions = null,
        string? returnHierarchicalChunkModelPath = null,
        IReadOnlyList<int>? returnHierarchicalChunkCommitTicks = null,
        IReadOnlyList<TaskChunkOptionSpec>? taskChunkOptions = null,
        IReadOnlyList<TaskOptionSpec>? taskOptions = null,
        bool compactRollout = false)
    {
        var environment = new MLBotEnvironment();
        var policy = CreatePolicy(
            modelPath,
            stochastic,
            seed,
            temperature,
            disablePolicyOverrides,
            localTraversalOracle,
            localTraversalMpc,
            moveHoldTicks,
            jumpHoldTicks,
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
            taskOptions);
        try
        {
            var effectiveConfig = config with { MaxTicks = maxTicks };
            var observation = environment.Reset(effectiveConfig);
            var steps = new List<MLBotRolloutStep>();
            var totalReward = 0f;
            MLBotStepResult result = default;

            for (var tick = 0; tick < effectiveConfig.MaxTicks; tick += 1)
            {
                var action = policy.Evaluate(observation);
                result = environment.Step(action);
                totalReward += result.Reward.Total;
                steps.Add(new MLBotRolloutStep
                {
                    Tick = result.Tick,
                    Observation = observation,
                    Action = action,
                    NextObservation = result.Observation,
                    Reward = result.Reward,
                    IsTerminal = result.IsTerminal,
                    IsSuccess = result.IsSuccess,
                    TerminalReason = result.TerminalReason,
                });

                observation = result.Observation;
                if (result.IsTerminal)
                {
                    break;
                }
            }

            var document = new MLBotRolloutDocument
            {
                LevelName = effectiveConfig.LevelName,
                Team = effectiveConfig.Team,
                ClassId = effectiveConfig.ClassId,
                TaskPhase = effectiveConfig.TaskPhase,
                ModelPath = modelPath ?? "direct-objective",
                TicksElapsed = result.Tick,
                Success = result.IsSuccess,
                TerminalReason = result.TerminalReason,
                TotalReward = totalReward,
                Steps = steps.ToArray(),
            };

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializedDocument = compactRollout
                ? JsonSerializer.Serialize(CreateCompactDocument(document), CompactJsonOptions)
                : JsonSerializer.Serialize(document, JsonOptions);
            File.WriteAllText(outputPath, serializedDocument);
            Console.WriteLine($"saved rollout={outputPath}");
            Console.WriteLine($"ticks={document.TicksElapsed} success={document.Success} terminal_reason={document.TerminalReason} total_reward={document.TotalReward:0.00}");
            return 0;
        }
        finally
        {
            if (policy is IDisposable disposablePolicy)
            {
                disposablePolicy.Dispose();
            }
        }
    }

    private static MLBotCompactRolloutDocument CreateCompactDocument(MLBotRolloutDocument document)
    {
        var steps = new MLBotCompactRolloutStep[document.Steps.Length];
        for (var index = 0; index < document.Steps.Length; index += 1)
        {
            var step = document.Steps[index];
            steps[index] = new MLBotCompactRolloutStep
            {
                Tick = step.Tick,
                Observation = step.Observation,
                Action = step.Action,
                IsTerminal = step.IsTerminal,
                IsSuccess = step.IsSuccess,
                TerminalReason = step.TerminalReason,
            };
        }

        return new MLBotCompactRolloutDocument
        {
            SchemaVersion = "mlbot-rollout-compact-v1",
            LevelName = document.LevelName,
            Team = document.Team,
            ClassId = document.ClassId,
            TaskPhase = document.TaskPhase,
            ModelPath = document.ModelPath,
            TicksElapsed = document.TicksElapsed,
            Success = document.Success,
            TerminalReason = document.TerminalReason,
            TotalReward = document.TotalReward,
            Steps = steps,
        };
    }

    private static IMLBotPolicyRuntime CreatePolicy(
        string? modelPath,
        bool stochastic,
        int seed,
        float temperature,
        bool disablePolicyOverrides,
        bool localTraversalOracle,
        bool localTraversalMpc,
        int moveHoldTicks,
        int jumpHoldTicks,
        string? returnFinalizerModelPath,
        float returnFinalizerEngageDistance,
        string? returnFinalizerLevelNameFilter,
        PlayerTeam? returnFinalizerTeamFilter,
        PlayerClass? returnFinalizerClassFilter,
        bool returnFinalizerAfterOptions,
        string? returnReplayBankPath,
        float returnReplayEngageDistance,
        float returnReplayMaxSelectionScore,
        string? replayBankPath,
        MLBotTaskPhase replayTaskPhase,
        float replayEngageDistance,
        float replayMaxSelectionScore,
        bool replayRequiresCarryingIntel,
        string? returnChunkModelPath,
        float returnChunkEngageDistance,
        int returnChunkCommitTicks,
        IReadOnlyList<ReturnChunkOptionSpec>? returnChunkOptions,
        string? returnChunkSelectorModelPath,
        IReadOnlyList<ReturnSelectedChunkOptionSpec>? returnSelectedChunkOptions,
        string? returnHierarchicalChunkModelPath,
        IReadOnlyList<int>? returnHierarchicalChunkCommitTicks,
        IReadOnlyList<TaskChunkOptionSpec>? taskChunkOptions,
        IReadOnlyList<TaskOptionSpec>? taskOptions)
    {
        var enablePolicyOverrides = !disablePolicyOverrides && MLBotPolicyRuntimeFactory.ArePolicyOverridesEnabled();
        IMLBotPolicyRuntime policy;
        if (!string.IsNullOrWhiteSpace(modelPath) && stochastic)
        {
            policy = new SamplingOnnxMLBotPolicyRuntime(modelPath, seed, temperature, enablePolicyOverrides: enablePolicyOverrides);
        }
        else
        {
            policy = !string.IsNullOrWhiteSpace(modelPath)
                ? new OnnxMLBotPolicyRuntime(modelPath, enablePolicyOverrides: enablePolicyOverrides)
                : new DirectObjectivePolicyRuntime();
        }

        IMLBotPolicyRuntime WrapReturnFinalizer(IMLBotPolicyRuntime innerPolicy)
        {
            if (string.IsNullOrWhiteSpace(returnFinalizerModelPath) || !File.Exists(returnFinalizerModelPath))
            {
                return innerPolicy;
            }

            return new ReturnIntelObjectiveOptionPolicyRuntime(
                innerPolicy,
                new OnnxMLBotPolicyRuntime(returnFinalizerModelPath, enablePolicyOverrides: enablePolicyOverrides),
                returnFinalizerEngageDistance,
                returnFinalizerLevelNameFilter,
                returnFinalizerTeamFilter,
                returnFinalizerClassFilter);
        }

        if (taskOptions is not null)
        {
            foreach (var option in taskOptions.Where(option => File.Exists(option.ModelPath)))
            {
                policy = new FilteredTaskOptionPolicyRuntime(
                    policy,
                    new OnnxMLBotPolicyRuntime(option.ModelPath, enablePolicyOverrides: enablePolicyOverrides),
                    option.TaskPhase,
                    option.EngageDistance,
                    option.LevelNameFilter,
                    option.TeamFilter,
                    option.ClassFilter,
                    option.MinObjectiveRelativeX,
                    option.MaxObjectiveRelativeX,
                    option.MinObjectiveRelativeY,
                    option.MaxObjectiveRelativeY);
            }
        }

        if (!returnFinalizerAfterOptions)
        {
            policy = WrapReturnFinalizer(policy);
        }

        if (!string.IsNullOrWhiteSpace(returnReplayBankPath))
        {
            policy = new ReturnIntelReplayOptionPolicyRuntime(
                policy,
                returnReplayBankPath,
                returnReplayEngageDistance,
                returnReplayMaxSelectionScore);
        }

        if (!string.IsNullOrWhiteSpace(replayBankPath))
        {
            policy = new ReturnIntelReplayOptionPolicyRuntime(
                policy,
                replayBankPath,
                replayEngageDistance,
                replayMaxSelectionScore,
                replayTaskPhase,
                replayRequiresCarryingIntel);
        }

        if (!string.IsNullOrWhiteSpace(returnChunkModelPath) && File.Exists(returnChunkModelPath))
        {
            policy = new ReturnIntelChunkOptionPolicyRuntime(
                policy,
                returnChunkModelPath,
                returnChunkEngageDistance,
                returnChunkCommitTicks);
        }

        if (returnChunkOptions is not null)
        {
            foreach (var option in returnChunkOptions.Where(option => File.Exists(option.ModelPath)))
            {
                policy = new ReturnIntelChunkOptionPolicyRuntime(
                    policy,
                    option.ModelPath,
                    option.EngageDistance,
                    option.CommitTicks,
                    option.LevelNameFilter,
                    option.TeamFilter,
                    option.ClassFilter,
                    option.MinObjectiveRelativeX,
                    option.MaxObjectiveRelativeX,
                    option.MinObjectiveRelativeY,
                    option.MaxObjectiveRelativeY);
            }
        }

        if (!string.IsNullOrWhiteSpace(returnChunkSelectorModelPath)
            && File.Exists(returnChunkSelectorModelPath)
            && returnSelectedChunkOptions is not null
            && returnSelectedChunkOptions.Count > 0)
        {
            policy = new ReturnIntelChunkSelectorPolicyRuntime(
                policy,
                returnChunkSelectorModelPath,
                returnSelectedChunkOptions
                    .Select(option => (option.ModelPath, option.CommitTicks))
                    .ToArray());
        }

        if (!string.IsNullOrWhiteSpace(returnHierarchicalChunkModelPath)
            && File.Exists(returnHierarchicalChunkModelPath)
            && returnHierarchicalChunkCommitTicks is not null
            && returnHierarchicalChunkCommitTicks.Count > 0)
        {
            policy = new ReturnIntelHierarchicalChunkPolicyRuntime(
                policy,
                returnHierarchicalChunkModelPath,
                returnHierarchicalChunkCommitTicks);
        }

        if (taskChunkOptions is not null)
        {
            foreach (var option in taskChunkOptions.Where(option => File.Exists(option.ModelPath)))
            {
                policy = new ReturnIntelChunkOptionPolicyRuntime(
                    policy,
                    option.ModelPath,
                    option.EngageDistance,
                    option.CommitTicks,
                    option.LevelNameFilter,
                    option.TeamFilter,
                    option.ClassFilter,
                    option.MinObjectiveRelativeX,
                    option.MaxObjectiveRelativeX,
                    option.MinObjectiveRelativeY,
                    option.MaxObjectiveRelativeY,
                    option.TaskPhase,
                    option.RequiresCarryingIntel,
                    option.LatchAcrossChunks,
                    option.RequiredIsGrounded,
                    minEngageDistance: option.MinEngageDistance);
            }
        }

        if (returnFinalizerAfterOptions)
        {
            policy = WrapReturnFinalizer(policy);
        }

        if (localTraversalOracle)
        {
            policy = new LocalTraversalRecoveryPolicyRuntime(policy);
        }

        if (localTraversalMpc)
        {
            policy = new LocalTraversalMpcPolicyRuntime(policy);
        }

        return moveHoldTicks > 0 || jumpHoldTicks > 0
            ? new TemporalCommitmentPolicyRuntime(policy, moveHoldTicks, jumpHoldTicks)
            : policy;
    }
}

internal sealed class MLBotRolloutDocument
{
    public string LevelName { get; set; } = string.Empty;

    public PlayerTeam Team { get; set; }

    public PlayerClass ClassId { get; set; }

    public MLBotTaskPhase TaskPhase { get; set; }

    public string ModelPath { get; set; } = string.Empty;

    public int TicksElapsed { get; set; }

    public bool Success { get; set; }

    public string TerminalReason { get; set; } = string.Empty;

    public float TotalReward { get; set; }

    public MLBotRolloutStep[] Steps { get; set; } = [];
}

internal sealed class MLBotRolloutStep
{
    public int Tick { get; set; }

    public MLBotObservation Observation { get; set; }

    public MLBotAction Action { get; set; }

    public MLBotObservation NextObservation { get; set; }

    public MLBotRewardBreakdown Reward { get; set; }

    public bool IsTerminal { get; set; }

    public bool IsSuccess { get; set; }

    public string TerminalReason { get; set; } = string.Empty;
}

internal sealed class MLBotCompactRolloutDocument
{
    public string SchemaVersion { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public PlayerTeam Team { get; set; }

    public PlayerClass ClassId { get; set; }

    public MLBotTaskPhase TaskPhase { get; set; }

    public string ModelPath { get; set; } = string.Empty;

    public int TicksElapsed { get; set; }

    public bool Success { get; set; }

    public string TerminalReason { get; set; } = string.Empty;

    public float TotalReward { get; set; }

    public MLBotCompactRolloutStep[] Steps { get; set; } = [];
}

internal sealed class MLBotCompactRolloutStep
{
    public int Tick { get; set; }

    public MLBotObservation Observation { get; set; }

    public MLBotAction Action { get; set; }

    public bool IsTerminal { get; set; }

    public bool IsSuccess { get; set; }

    public string TerminalReason { get; set; } = string.Empty;
}
