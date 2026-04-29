using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using OpenGarrison.MLBot.Environment;
using OpenGarrison.MLBot.Policies;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotInterventionRolloutExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int ExportEpisode(MLBotInterventionRolloutOptions options)
    {
        var config = MLBotEpisodeConfig.CreateDefault(
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
            carryingIntel: options.CarryingIntel);

        var environment = new MLBotEnvironment();
            var studentPolicy = CreatePolicy(
                options.StudentModelPath,
                options.StudentStochastic,
                options.Seed,
                options.Temperature,
                options.DisablePolicyOverrides);
            var teacherPolicy = CreatePolicy(
                options.TeacherModelPath,
                stochastic: false,
                options.Seed,
                temperature: 1f,
                options.DisablePolicyOverrides,
                options.TeacherReturnFinalizerModelPath,
                options.TeacherReturnFinalizerEngageDistance,
                options.TeacherReturnFinalizerLevelNameFilter,
                options.TeacherReturnFinalizerTeamFilter,
                options.TeacherReturnFinalizerClassFilter,
                options.TeacherReturnFinalizerAfterOptions,
                options.TeacherReturnReplayBankPath,
                options.TeacherReturnReplayEngageDistance,
                options.TeacherReturnReplayMaxSelectionScore,
                options.TeacherReplayBankPath,
                options.TeacherReplayTaskPhase,
                options.TeacherReplayEngageDistance,
                options.TeacherReplayMaxSelectionScore,
                options.TeacherReplayRequiresCarryingIntel,
                options.TeacherReturnChunkModelPath,
                options.TeacherReturnChunkEngageDistance,
                options.TeacherReturnChunkCommitTicks,
                options.TeacherReturnChunkOptions,
                options.TeacherReturnChunkSelectorModelPath,
                options.TeacherReturnSelectedChunkOptions,
                options.TeacherReturnHierarchicalChunkModelPath,
                options.TeacherReturnHierarchicalChunkCommitTicks,
                options.TeacherTaskOptions);
        try
        {
            var observation = environment.Reset(config);
            var preInterventionLabels = new Queue<MLBotInterventionRolloutStep>();
            var teacherRecoverySteps = new List<MLBotInterventionRolloutStep>();
            var totalReward = 0f;
            var interventionStarted = false;
            var interventionTick = 0;
            var triggerNavigationDistance = 0f;
            var bestTeacherNavigationDistance = float.MaxValue;
            var bestStudentNavigationDistance = GetNavigationDistance(observation);
            MLBotStepResult result = default;
            MLBotAction triggerStudentAction = default;
            MLBotAction triggerTeacherAction = default;

            for (var tick = 0; tick < config.MaxTicks; tick += 1)
            {
                var teacherAction = teacherPolicy.Evaluate(observation);
                var studentAction = studentPolicy.Evaluate(observation);
                var startIntervention = !interventionStarted
                    && ShouldIntervene(observation, studentAction, teacherAction, options, tick + 1, bestStudentNavigationDistance);
                var useTeacher = interventionStarted || startIntervention;

                if (startIntervention)
                {
                    interventionStarted = true;
                    interventionTick = tick + 1;
                    triggerNavigationDistance = GetNavigationDistance(observation);
                    bestTeacherNavigationDistance = triggerNavigationDistance;
                    triggerStudentAction = studentAction;
                    triggerTeacherAction = teacherAction;
                }

                var action = useTeacher ? teacherAction : studentAction;
                var previousObservation = observation;
                result = environment.Step(action);
                totalReward += result.Reward.Total;
                observation = result.Observation;
                var navigationDistance = GetNavigationDistance(observation);
                var teacherLabelStep = CreateInterventionStep(
                    result.Tick,
                    previousObservation,
                    teacherAction,
                    result,
                    studentAction,
                    teacherAction,
                    triggerStudentAction,
                    triggerTeacherAction,
                    interventionStarted ? interventionTick : 0,
                    interventionStarted ? triggerNavigationDistance : GetNavigationDistance(previousObservation),
                    navigationDistance,
                    interventionStarted ? bestTeacherNavigationDistance : navigationDistance,
                    useTeacher ? "teacher_recovery" : "student_shadow_label");

                if (!interventionStarted || startIntervention)
                {
                    if (ShouldBufferStudentLabel(
                            previousObservation,
                            studentAction,
                            teacherAction,
                            options,
                            result.Tick,
                            bestStudentNavigationDistance))
                    {
                        preInterventionLabels.Enqueue(teacherLabelStep);
                        while (preInterventionLabels.Count > options.PreInterventionLabelWindow)
                        {
                            preInterventionLabels.Dequeue();
                        }
                    }

                    bestStudentNavigationDistance = MathF.Min(bestStudentNavigationDistance, navigationDistance);
                }

                if (interventionStarted && useTeacher)
                {
                    bestTeacherNavigationDistance = MathF.Min(bestTeacherNavigationDistance, navigationDistance);
                    teacherLabelStep.CorrectionMetadata.InterventionTick = interventionTick;
                    teacherLabelStep.CorrectionMetadata.TriggerNavigationDistance = triggerNavigationDistance;
                    teacherLabelStep.CorrectionMetadata.BestTeacherNavigationDistance = bestTeacherNavigationDistance;
                    if (options.IncludeTeacherRecoverySteps)
                    {
                        teacherRecoverySteps.Add(teacherLabelStep);
                    }

                    if (result.IsTerminal
                        || result.Tick - interventionTick + 1 >= options.InterventionHorizon)
                    {
                        break;
                    }
                }

                if (result.IsTerminal)
                {
                    break;
                }
            }

            var navigationImprovement = interventionStarted
                ? triggerNavigationDistance - bestTeacherNavigationDistance
                : 0f;
            var verified = interventionStarted
                && (options.RequireTerminalSuccess
                    ? result.IsSuccess
                    : result.IsSuccess || navigationImprovement >= options.MinNavigationImprovement);
            var savedSteps = Array.Empty<MLBotInterventionRolloutStep>();
            if (verified)
            {
                foreach (var step in preInterventionLabels)
                {
                    step.CorrectionMetadata.InterventionTick = interventionTick;
                    step.CorrectionMetadata.TriggerStudentAction = triggerStudentAction;
                    step.CorrectionMetadata.TriggerTeacherAction = triggerTeacherAction;
                    step.CorrectionMetadata.TriggerNavigationDistance = triggerNavigationDistance;
                    step.CorrectionMetadata.BestTeacherNavigationDistance = bestTeacherNavigationDistance;
                }

                savedSteps = preInterventionLabels.Concat(teacherRecoverySteps).ToArray();
            }

            var document = new MLBotInterventionRolloutDocument
            {
                LevelName = config.LevelName,
                Team = config.Team,
                ClassId = config.ClassId,
                TaskPhase = config.TaskPhase,
                ModelPath = options.TeacherModelPath,
                StudentModelPath = options.StudentModelPath,
                TeacherModelPath = options.TeacherModelPath,
                TicksElapsed = result.Tick,
                Success = verified,
                TerminalReason = verified
                    ? result.IsSuccess ? result.TerminalReason : "intervention_recovered"
                    : interventionStarted ? "intervention_unverified" : "no_intervention",
                TotalReward = totalReward,
                CorrectionSource = "teacher_intervention_dagger",
                InterventionTick = interventionStarted ? interventionTick : null,
                TriggerNavigationDistance = interventionStarted ? triggerNavigationDistance : null,
                BestTeacherNavigationDistance = interventionStarted ? bestTeacherNavigationDistance : null,
                NavigationImprovement = navigationImprovement,
                VerifiedRecovery = verified,
                RequireTerminalSuccess = options.RequireTerminalSuccess,
                PreInterventionLabelCount = verified ? preInterventionLabels.Count : 0,
                TeacherRecoveryStepCount = verified ? teacherRecoverySteps.Count : 0,
                Steps = savedSteps,
            };

            var directory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(options.OutputPath, JsonSerializer.Serialize(document, JsonOptions));
            Console.WriteLine($"saved intervention_rollout={options.OutputPath}");
            Console.WriteLine(
                $"intervention_tick={FormatOptional(interventionStarted ? interventionTick : null)} verified={verified} " +
                $"steps={document.Steps.Length} success={result.IsSuccess} terminal_reason={document.TerminalReason} " +
                $"nav_improvement={navigationImprovement:0.0} pre_labels={document.PreInterventionLabelCount} " +
                $"teacher_steps={document.TeacherRecoveryStepCount}");
            return 0;
        }
        finally
        {
            DisposePolicy(studentPolicy);
            DisposePolicy(teacherPolicy);
        }
    }

    private static IMLBotPolicyRuntime CreatePolicy(
        string modelPath,
        bool stochastic,
        int seed,
        float temperature,
        bool disablePolicyOverrides,
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
        IReadOnlyList<TaskOptionSpec>? taskOptions = null)
    {
        var enablePolicyOverrides = !disablePolicyOverrides && MLBotPolicyRuntimeFactory.ArePolicyOverridesEnabled();
        IMLBotPolicyRuntime policy = stochastic
            ? new SamplingOnnxMLBotPolicyRuntime(modelPath, seed, temperature, enablePolicyOverrides: enablePolicyOverrides)
            : new OnnxMLBotPolicyRuntime(modelPath, enablePolicyOverrides: enablePolicyOverrides);

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

        if (returnFinalizerAfterOptions)
        {
            policy = WrapReturnFinalizer(policy);
        }

        return policy;
    }

    private static void DisposePolicy(IMLBotPolicyRuntime policy)
    {
        if (policy is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static bool ShouldIntervene(
        in MLBotObservation observation,
        in MLBotAction studentAction,
        in MLBotAction teacherAction,
        MLBotInterventionRolloutOptions options,
        int tick,
        float bestStudentNavigationDistance)
    {
        if (tick < options.TriggerMinTick)
        {
            return false;
        }

        if (options.TriggerRequiresCarryingIntel && !observation.IsCarryingIntel)
        {
            return false;
        }

        if (options.RequireActionDisagreement && ActionsMatch(studentAction, teacherAction))
        {
            return false;
        }

        var navigationDistance = GetNavigationDistance(observation);
        var nearNavigationTarget = options.TriggerNavigationDistance > 0f
            && navigationDistance <= options.TriggerNavigationDistance;
        var verticalWaypoint = options.TriggerWaypointAbsY > 0f
            && observation.Waypoint.HasWaypoint
            && MathF.Abs(observation.Waypoint.RelativeY) >= options.TriggerWaypointAbsY;
        var stuck = options.TriggerStuckTicks > 0f
            && observation.StuckTicks >= options.TriggerStuckTicks;
        var regressedAfterProgress = options.TriggerRegressionAfterProgress > 0f
            && bestStudentNavigationDistance < float.MaxValue
            && navigationDistance - bestStudentNavigationDistance >= options.TriggerRegressionAfterProgress;

        return nearNavigationTarget || verticalWaypoint || stuck || regressedAfterProgress;
    }

    private static bool ShouldBufferStudentLabel(
        in MLBotObservation observation,
        in MLBotAction studentAction,
        in MLBotAction teacherAction,
        MLBotInterventionRolloutOptions options,
        int tick,
        float bestStudentNavigationDistance)
    {
        if (tick < options.TriggerMinTick)
        {
            return false;
        }

        if (!options.BufferRiskOnly)
        {
            return true;
        }

        if (options.RequireActionDisagreement && ActionsMatch(studentAction, teacherAction))
        {
            return false;
        }

        return ShouldIntervene(
            observation,
            studentAction,
            teacherAction,
            options with { RequireActionDisagreement = false },
            tick,
            bestStudentNavigationDistance);
    }

    private static MLBotInterventionRolloutStep CreateInterventionStep(
        int tick,
        in MLBotObservation observation,
        in MLBotAction teacherAction,
        in MLBotStepResult result,
        in MLBotAction studentAction,
        in MLBotAction metadataTeacherAction,
        in MLBotAction triggerStudentAction,
        in MLBotAction triggerTeacherAction,
        int interventionTick,
        float triggerNavigationDistance,
        float navigationDistance,
        float bestTeacherNavigationDistance,
        string labelSource)
    {
        return new MLBotInterventionRolloutStep
        {
            Tick = tick,
            Observation = observation,
            Action = teacherAction,
            NextObservation = result.Observation,
            Reward = result.Reward,
            IsTerminal = result.IsTerminal,
            IsSuccess = result.IsSuccess,
            TerminalReason = result.TerminalReason,
            CorrectionMetadata = new MLBotInterventionCorrectionMetadata
            {
                InterventionTick = interventionTick,
                SourceTick = tick,
                StudentAction = studentAction,
                TeacherAction = metadataTeacherAction,
                TriggerStudentAction = triggerStudentAction,
                TriggerTeacherAction = triggerTeacherAction,
                TriggerNavigationDistance = triggerNavigationDistance,
                NavigationDistance = navigationDistance,
                BestTeacherNavigationDistance = bestTeacherNavigationDistance,
                LabelSource = labelSource,
            },
        };
    }

    private static bool ActionsMatch(in MLBotAction left, in MLBotAction right)
    {
        return left.MoveDirection == right.MoveDirection
            && left.Jump == right.Jump
            && left.Crouch == right.Crouch
            && left.FirePrimary == right.FirePrimary
            && left.FireSecondary == right.FireSecondary
            && left.DropIntel == right.DropIntel;
    }

    private static float GetNavigationDistance(in MLBotObservation observation)
    {
        return observation.Waypoint is { HasWaypoint: true, IsFinalWaypoint: false }
            ? observation.Waypoint.Distance
            : observation.ObjectiveDistance;
    }

    private static string FormatOptional(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "none";
    }
}

internal sealed class MLBotInterventionRolloutDocument
{
    public string LevelName { get; set; } = string.Empty;

    public PlayerTeam Team { get; set; }

    public PlayerClass ClassId { get; set; }

    public MLBotTaskPhase TaskPhase { get; set; }

    public string ModelPath { get; set; } = string.Empty;

    public string StudentModelPath { get; set; } = string.Empty;

    public string TeacherModelPath { get; set; } = string.Empty;

    public int TicksElapsed { get; set; }

    public bool Success { get; set; }

    public string TerminalReason { get; set; } = string.Empty;

    public float TotalReward { get; set; }

    public string CorrectionSource { get; set; } = string.Empty;

    public int? InterventionTick { get; set; }

    public float? TriggerNavigationDistance { get; set; }

    public float? BestTeacherNavigationDistance { get; set; }

    public float NavigationImprovement { get; set; }

    public bool VerifiedRecovery { get; set; }

    public bool RequireTerminalSuccess { get; set; }

    public int PreInterventionLabelCount { get; set; }

    public int TeacherRecoveryStepCount { get; set; }

    public MLBotInterventionRolloutStep[] Steps { get; set; } = [];
}

internal sealed class MLBotInterventionRolloutStep
{
    public int Tick { get; set; }

    public MLBotObservation Observation { get; set; }

    public MLBotAction Action { get; set; }

    public MLBotObservation NextObservation { get; set; }

    public MLBotRewardBreakdown Reward { get; set; }

    public bool IsTerminal { get; set; }

    public bool IsSuccess { get; set; }

    public string TerminalReason { get; set; } = string.Empty;

    public MLBotInterventionCorrectionMetadata CorrectionMetadata { get; set; } = new();
}

internal sealed class MLBotInterventionCorrectionMetadata
{
    public int InterventionTick { get; set; }

    public int SourceTick { get; set; }

    public MLBotAction StudentAction { get; set; }

    public MLBotAction TeacherAction { get; set; }

    public MLBotAction TriggerStudentAction { get; set; }

    public MLBotAction TriggerTeacherAction { get; set; }

    public float TriggerNavigationDistance { get; set; }

    public float NavigationDistance { get; set; }

    public float BestTeacherNavigationDistance { get; set; }

    public string LabelSource { get; set; } = string.Empty;
}

internal sealed record MLBotInterventionRolloutOptions(
    string LevelName,
    PlayerTeam Team,
    PlayerClass ClassId,
    MLBotTaskPhase TaskPhase,
    int MaxTicks,
    int StartNodeId,
    float? StartX,
    float? StartY,
    float? StartVelocityX,
    float? StartVelocityY,
    bool? CarryingIntel,
    string StudentModelPath,
    string TeacherModelPath,
    string? TeacherReturnFinalizerModelPath,
    float TeacherReturnFinalizerEngageDistance,
    string? TeacherReturnFinalizerLevelNameFilter,
    PlayerTeam? TeacherReturnFinalizerTeamFilter,
    PlayerClass? TeacherReturnFinalizerClassFilter,
    bool TeacherReturnFinalizerAfterOptions,
    string? TeacherReturnReplayBankPath,
    float TeacherReturnReplayEngageDistance,
    float TeacherReturnReplayMaxSelectionScore,
    string? TeacherReplayBankPath,
    MLBotTaskPhase TeacherReplayTaskPhase,
    float TeacherReplayEngageDistance,
    float TeacherReplayMaxSelectionScore,
    bool TeacherReplayRequiresCarryingIntel,
    string? TeacherReturnChunkModelPath,
    float TeacherReturnChunkEngageDistance,
    int TeacherReturnChunkCommitTicks,
    IReadOnlyList<ReturnChunkOptionSpec> TeacherReturnChunkOptions,
    string? TeacherReturnChunkSelectorModelPath,
    IReadOnlyList<ReturnSelectedChunkOptionSpec> TeacherReturnSelectedChunkOptions,
    string? TeacherReturnHierarchicalChunkModelPath,
    IReadOnlyList<int> TeacherReturnHierarchicalChunkCommitTicks,
    IReadOnlyList<TaskOptionSpec> TeacherTaskOptions,
    string OutputPath,
    bool StudentStochastic,
    int Seed,
    float Temperature,
    bool DisablePolicyOverrides,
    int InterventionHorizon,
    int TriggerMinTick,
    float TriggerNavigationDistance,
    float TriggerWaypointAbsY,
    float TriggerStuckTicks,
    float TriggerRegressionAfterProgress,
    bool RequireActionDisagreement,
    int PreInterventionLabelWindow,
    bool BufferRiskOnly,
    bool IncludeTeacherRecoverySteps,
    bool TriggerRequiresCarryingIntel,
    float MinNavigationImprovement,
    bool RequireTerminalSuccess)
{
    public static MLBotInterventionRolloutOptions Parse(string[] args)
    {
        var rolloutOptions = RolloutCommandLineOptions.Parse(args);
        var studentModelPath = GetRequired(args, "--student-model");
        var teacherModelPath = GetRequired(args, "--teacher-model");
        var outputPath = GetRequired(args, "--out");
        var teacherReturnFinalizerClass = TryParsePlayerClass(GetOptional(args, "--teacher-return-finalizer-class"));
        var teacherReturnFinalizerTeam = TryParsePlayerTeam(GetOptional(args, "--teacher-return-finalizer-team"));
        return new MLBotInterventionRolloutOptions(
            LevelName: rolloutOptions.LevelName,
            Team: rolloutOptions.Team,
            ClassId: rolloutOptions.ClassId,
            TaskPhase: rolloutOptions.TaskPhase,
            MaxTicks: rolloutOptions.MaxTicks,
            StartNodeId: rolloutOptions.StartNodeId,
            StartX: rolloutOptions.StartX,
            StartY: rolloutOptions.StartY,
            StartVelocityX: rolloutOptions.StartVelocityX,
            StartVelocityY: rolloutOptions.StartVelocityY,
            CarryingIntel: rolloutOptions.CarryingIntel,
            StudentModelPath: studentModelPath,
            TeacherModelPath: teacherModelPath,
            TeacherReturnFinalizerModelPath: GetOptional(args, "--teacher-return-finalizer-model"),
            TeacherReturnFinalizerEngageDistance: ParseFloat(args, "--teacher-return-finalizer-engage-distance", 0f),
            TeacherReturnFinalizerLevelNameFilter: GetOptional(args, "--teacher-return-finalizer-map"),
            TeacherReturnFinalizerTeamFilter: teacherReturnFinalizerTeam,
            TeacherReturnFinalizerClassFilter: teacherReturnFinalizerClass,
            TeacherReturnFinalizerAfterOptions: HasFlag(args, "--teacher-return-finalizer-after-options"),
            TeacherReturnReplayBankPath: GetOptional(args, "--teacher-return-replay-bank"),
            TeacherReturnReplayEngageDistance: ParseFloat(args, "--teacher-return-replay-engage-distance", 0f),
            TeacherReturnReplayMaxSelectionScore: ParseFloat(args, "--teacher-return-replay-max-score", 0f),
            TeacherReplayBankPath: GetOptional(args, "--teacher-replay-bank"),
            TeacherReplayTaskPhase: ParseTaskPhase(GetOptional(args, "--teacher-replay-task"), MLBotTaskPhase.ReturnIntel),
            TeacherReplayEngageDistance: ParseFloat(args, "--teacher-replay-engage-distance", 0f),
            TeacherReplayMaxSelectionScore: ParseFloat(args, "--teacher-replay-max-score", 0f),
            TeacherReplayRequiresCarryingIntel: !HasFlag(args, "--teacher-no-replay-requires-carrying-intel"),
            TeacherReturnChunkModelPath: GetOptional(args, "--teacher-return-chunk-model"),
            TeacherReturnChunkEngageDistance: ParseFloat(args, "--teacher-return-chunk-engage-distance", 0f),
            TeacherReturnChunkCommitTicks: ParseInt(args, "--teacher-return-chunk-commit-ticks", 0),
            TeacherReturnChunkOptions: GetAll(args, "--teacher-return-chunk-spec")
                .Select(ParseReturnChunkOptionSpec)
                .ToArray(),
            TeacherReturnChunkSelectorModelPath: GetOptional(args, "--teacher-return-chunk-selector-model"),
            TeacherReturnSelectedChunkOptions: GetAll(args, "--teacher-return-selected-chunk-spec")
                .Select(ParseReturnSelectedChunkOptionSpec)
                .ToArray(),
            TeacherReturnHierarchicalChunkModelPath: GetOptional(args, "--teacher-return-hierarchical-chunk-model"),
            TeacherReturnHierarchicalChunkCommitTicks: ParseCommitTicks(GetOptional(args, "--teacher-return-hierarchical-chunk-commit-ticks")),
            TeacherTaskOptions: GetAll(args, "--teacher-task-option-spec")
                .Select(ParseTaskOptionSpec)
                .ToArray(),
            OutputPath: outputPath,
            StudentStochastic: HasFlag(args, "--student-stochastic") || rolloutOptions.Stochastic,
            Seed: rolloutOptions.Seed,
            Temperature: rolloutOptions.Temperature,
            DisablePolicyOverrides: rolloutOptions.DisablePolicyOverrides,
            InterventionHorizon: ParseInt(args, "--intervention-horizon", 360),
            TriggerMinTick: ParseInt(args, "--trigger-min-tick", 0),
            TriggerNavigationDistance: ParseFloat(args, "--trigger-navigation-distance", 300f),
            TriggerWaypointAbsY: ParseFloat(args, "--trigger-waypoint-abs-y", 64f),
            TriggerStuckTicks: ParseFloat(args, "--trigger-stuck-ticks", 0f),
            TriggerRegressionAfterProgress: ParseFloat(args, "--trigger-regression-after-progress", 96f),
            RequireActionDisagreement: !HasFlag(args, "--allow-matching-action-trigger"),
            PreInterventionLabelWindow: ParseInt(args, "--pre-intervention-label-window", 180),
            BufferRiskOnly: HasFlag(args, "--buffer-risk-only"),
            IncludeTeacherRecoverySteps: HasFlag(args, "--include-teacher-recovery-steps"),
            TriggerRequiresCarryingIntel: HasFlag(args, "--trigger-requires-carrying-intel"),
            MinNavigationImprovement: ParseFloat(args, "--min-navigation-improvement", 24f),
            RequireTerminalSuccess: HasFlag(args, "--require-terminal-success"));
    }

    private static bool HasFlag(string[] args, string optionName)
    {
        return args.Any(arg => string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRequired(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length - 1; index += 1)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        throw new ArgumentException($"missing required option: {optionName}");
    }

    private static string? GetOptional(string[] args, string optionName)
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

    private static MLBotTaskPhase ParseTaskPhase(string? value, MLBotTaskPhase fallback)
    {
        return Enum.TryParse<MLBotTaskPhase>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    private static IEnumerable<string> GetAll(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length - 1; index += 1)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                yield return args[index + 1];
            }
        }
    }

    private static ReturnChunkOptionSpec ParseReturnChunkOptionSpec(string rawValue)
    {
        var parts = rawValue.Split('|', StringSplitOptions.None);
        var modelPath = parts.Length > 0 ? parts[0] : string.Empty;
        var engageDistance = parts.Length > 1
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedEngageDistance)
                ? Math.Max(0f, parsedEngageDistance)
                : 0f;
        var commitTicks = parts.Length > 2
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCommitTicks)
                ? Math.Max(0, parsedCommitTicks)
                : 0;
        var levelNameFilter = NullIfBlank(parts.Length > 3 ? parts[3] : null);
        var teamFilter = Enum.TryParse<PlayerTeam>(NullIfBlank(parts.Length > 4 ? parts[4] : null), ignoreCase: true, out var parsedTeam)
            ? parsedTeam
            : (PlayerTeam?)null;
        var classFilter = Enum.TryParse<PlayerClass>(NullIfBlank(parts.Length > 5 ? parts[5] : null), ignoreCase: true, out var parsedClass)
            ? parsedClass
            : (PlayerClass?)null;
        return new ReturnChunkOptionSpec(
            modelPath,
            engageDistance,
            commitTicks,
            levelNameFilter,
            teamFilter,
            classFilter,
            TryParseOptionalFloat(parts, 6),
            TryParseOptionalFloat(parts, 7),
            TryParseOptionalFloat(parts, 8),
            TryParseOptionalFloat(parts, 9));
    }

    private static ReturnSelectedChunkOptionSpec ParseReturnSelectedChunkOptionSpec(string rawValue)
    {
        var parts = rawValue.Split('|', StringSplitOptions.None);
        var modelPath = parts.Length > 0 ? parts[0] : string.Empty;
        var commitTicks = parts.Length > 1
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCommit)
                ? Math.Max(0, parsedCommit)
                : 0;
        return new ReturnSelectedChunkOptionSpec(modelPath, commitTicks);
    }

    private static TaskOptionSpec ParseTaskOptionSpec(string rawValue)
    {
        var parts = rawValue.Split('|', StringSplitOptions.None);
        if (parts.Length < 2 || !Enum.TryParse<MLBotTaskPhase>(parts[1], ignoreCase: true, out var taskPhase))
        {
            throw new ArgumentException("--teacher-task-option-spec must be formatted as model|phase|engage_distance|map_filter|team_filter|class_filter");
        }

        var modelPath = parts[0];
        var engageDistance = parts.Length > 2
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedEngageDistance)
            ? Math.Max(0f, parsedEngageDistance)
            : 0f;
        var levelNameFilter = parts.Length > 3 ? NullIfBlank(parts[3]) : null;
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
            modelPath,
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

    private static int[] ParseCommitTicks(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split([',', '|', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Max(0, parsed)
                : 0)
            .ToArray();
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static float? TryParseOptionalFloat(string[] parts, int index)
    {
        if (index >= parts.Length || string.IsNullOrWhiteSpace(parts[index]))
        {
            return null;
        }

        return float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static PlayerClass? TryParsePlayerClass(string? rawValue)
    {
        return Enum.TryParse<PlayerClass>(rawValue, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static PlayerTeam? TryParsePlayerTeam(string? rawValue)
    {
        return Enum.TryParse<PlayerTeam>(rawValue, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static int ParseInt(string[] args, string optionName, int fallback)
    {
        for (var index = 0; index < args.Length - 1; index += 1)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return Math.Max(0, parsed);
            }
        }

        return fallback;
    }

    private static float ParseFloat(string[] args, string optionName, float fallback)
    {
        for (var index = 0; index < args.Length - 1; index += 1)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase)
                && float.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return Math.Max(0f, parsed);
            }
        }

        return fallback;
    }
}
