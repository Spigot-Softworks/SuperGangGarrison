using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Policies;

internal static class MLBotPolicyRuntimeBuilder
{
    public static IMLBotPolicyRuntime CreatePolicy(RolloutCommandLineOptions options)
    {
        IMLBotPolicyRuntime policy;
        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            policy = new DirectObjectivePolicyRuntime();
        }
        else
        {
            var enablePolicyOverrides = !options.DisablePolicyOverrides && MLBotPolicyRuntimeFactory.ArePolicyOverridesEnabled();
            policy = options.Stochastic
                ? new SamplingOnnxMLBotPolicyRuntime(options.ModelPath, options.Seed, options.Temperature, enablePolicyOverrides: enablePolicyOverrides)
                : new OnnxMLBotPolicyRuntime(options.ModelPath, enablePolicyOverrides: enablePolicyOverrides);
        }

        IMLBotPolicyRuntime WrapReturnFinalizer(IMLBotPolicyRuntime innerPolicy)
        {
            if (string.IsNullOrWhiteSpace(options.ReturnFinalizerModelPath) || !File.Exists(options.ReturnFinalizerModelPath))
            {
                return innerPolicy;
            }

            var enableFinalizerPolicyOverrides = !options.DisablePolicyOverrides && MLBotPolicyRuntimeFactory.ArePolicyOverridesEnabled();
            return new ReturnIntelObjectiveOptionPolicyRuntime(
                innerPolicy,
                new OnnxMLBotPolicyRuntime(options.ReturnFinalizerModelPath, enablePolicyOverrides: enableFinalizerPolicyOverrides),
                options.ReturnFinalizerEngageDistance,
                options.ReturnFinalizerLevelNameFilter,
                options.ReturnFinalizerTeamFilter,
                options.ReturnFinalizerClassFilter);
        }

        foreach (var option in options.TaskOptions.Where(option => File.Exists(option.ModelPath)))
        {
            var enableTaskOptionPolicyOverrides = !options.DisablePolicyOverrides && MLBotPolicyRuntimeFactory.ArePolicyOverridesEnabled();
            policy = new FilteredTaskOptionPolicyRuntime(
                policy,
                new OnnxMLBotPolicyRuntime(option.ModelPath, enablePolicyOverrides: enableTaskOptionPolicyOverrides),
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

        if (!options.ReturnFinalizerAfterOptions)
        {
            policy = WrapReturnFinalizer(policy);
        }

        if (!string.IsNullOrWhiteSpace(options.ReturnReplayBankPath))
        {
            policy = new ReturnIntelReplayOptionPolicyRuntime(
                policy,
                options.ReturnReplayBankPath,
                options.ReturnReplayEngageDistance,
                options.ReturnReplayMaxSelectionScore);
        }

        if (!string.IsNullOrWhiteSpace(options.ReplayBankPath))
        {
            policy = new ReturnIntelReplayOptionPolicyRuntime(
                policy,
                options.ReplayBankPath,
                options.ReplayEngageDistance,
                options.ReplayMaxSelectionScore,
                options.ReplayTaskPhase,
                options.ReplayRequiresCarryingIntel);
        }

        if (!string.IsNullOrWhiteSpace(options.ReturnChunkModelPath) && File.Exists(options.ReturnChunkModelPath))
        {
            policy = new ReturnIntelChunkOptionPolicyRuntime(
                policy,
                options.ReturnChunkModelPath,
                options.ReturnChunkEngageDistance,
                options.ReturnChunkCommitTicks);
        }

        foreach (var option in options.ReturnChunkOptions.Where(option => File.Exists(option.ModelPath)))
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

        if (!string.IsNullOrWhiteSpace(options.ReturnChunkSelectorModelPath)
            && File.Exists(options.ReturnChunkSelectorModelPath)
            && options.ReturnSelectedChunkOptions.Count > 0)
        {
            policy = new ReturnIntelChunkSelectorPolicyRuntime(
                policy,
                options.ReturnChunkSelectorModelPath,
                options.ReturnSelectedChunkOptions
                    .Select(option => (option.ModelPath, option.CommitTicks))
                    .ToArray());
        }

        if (!string.IsNullOrWhiteSpace(options.ReturnHierarchicalChunkModelPath)
            && File.Exists(options.ReturnHierarchicalChunkModelPath)
            && options.ReturnHierarchicalChunkCommitTicks.Count > 0)
        {
            policy = new ReturnIntelHierarchicalChunkPolicyRuntime(
                policy,
                options.ReturnHierarchicalChunkModelPath,
                options.ReturnHierarchicalChunkCommitTicks);
        }

        foreach (var option in options.TaskChunkOptions.Where(option => File.Exists(option.ModelPath)))
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

        if (options.ReturnFinalizerAfterOptions)
        {
            policy = WrapReturnFinalizer(policy);
        }

        if (options.LocalTraversalOracle)
        {
            policy = new LocalTraversalRecoveryPolicyRuntime(policy);
        }

        if (options.LocalTraversalMpc)
        {
            policy = new LocalTraversalMpcPolicyRuntime(policy);
        }

        return options.MoveHoldTicks > 0 || options.JumpHoldTicks > 0
            ? new TemporalCommitmentPolicyRuntime(policy, options.MoveHoldTicks, options.JumpHoldTicks)
            : policy;
    }
}
