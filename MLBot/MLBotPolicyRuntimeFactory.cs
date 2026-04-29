using OpenGarrison.MLBot.Policies;
using OpenGarrison.Core;
using System.Globalization;

namespace OpenGarrison.MLBot;

public static class MLBotPolicyRuntimeFactory
{
    public const string ModelPathEnvironmentVariable = "OG_MLBOT_MODEL_PATH";
    public const string DisablePolicyOverridesEnvironmentVariable = "OG_MLBOT_DISABLE_POLICY_OVERRIDES";
    public const string LocalTraversalOracleEnvironmentVariable = "OG_MLBOT_LOCAL_TRAVERSAL_ORACLE";
    public const string ReturnHierarchicalChunkModelPathEnvironmentVariable = "OG_MLBOT_RETURN_HIERARCHICAL_CHUNK_MODEL_PATH";
    public const string ReturnHierarchicalChunkCommitTicksEnvironmentVariable = "OG_MLBOT_RETURN_HIERARCHICAL_CHUNK_COMMIT_TICKS";
    public const string ReturnFinalizerModelPathEnvironmentVariable = "OG_MLBOT_RETURN_FINALIZER_MODEL_PATH";
    public const string ReturnFinalizerEngageDistanceEnvironmentVariable = "OG_MLBOT_RETURN_FINALIZER_ENGAGE_DISTANCE";
    public const string ReturnFinalizerAfterOptionsEnvironmentVariable = "OG_MLBOT_RETURN_FINALIZER_AFTER_OPTIONS";
    public const string ReturnFinalizerLevelNameEnvironmentVariable = "OG_MLBOT_RETURN_FINALIZER_MAP";
    public const string ReturnFinalizerTeamEnvironmentVariable = "OG_MLBOT_RETURN_FINALIZER_TEAM";
    public const string ReturnFinalizerClassEnvironmentVariable = "OG_MLBOT_RETURN_FINALIZER_CLASS";

    public static IMLBotPolicyRuntime CreateDefault()
    {
        var modelPath = Environment.GetEnvironmentVariable(ModelPathEnvironmentVariable);
        return CreateConfigured(modelPath);
    }

    public static IMLBotPolicyRuntime CreateConfigured(string? modelPath)
    {
        IMLBotPolicyRuntime policy;
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            policy = new OnnxMLBotPolicyRuntime(modelPath, enablePolicyOverrides: ArePolicyOverridesEnabled());
        }
        else
        {
            policy = new DirectObjectivePolicyRuntime();
        }

        if (!IsTruthy(Environment.GetEnvironmentVariable(ReturnFinalizerAfterOptionsEnvironmentVariable)))
        {
            policy = WrapReturnFinalizer(policy);
        }

        var hierarchicalModelPath = Environment.GetEnvironmentVariable(ReturnHierarchicalChunkModelPathEnvironmentVariable);
        var hierarchicalCommitTicks = ParseCommitTicks(Environment.GetEnvironmentVariable(ReturnHierarchicalChunkCommitTicksEnvironmentVariable));
        if (!string.IsNullOrWhiteSpace(hierarchicalModelPath)
            && File.Exists(hierarchicalModelPath)
            && hierarchicalCommitTicks.Count > 0)
        {
            policy = new ReturnIntelHierarchicalChunkPolicyRuntime(
                policy,
                hierarchicalModelPath,
                hierarchicalCommitTicks);
        }

        if (IsTruthy(Environment.GetEnvironmentVariable(ReturnFinalizerAfterOptionsEnvironmentVariable)))
        {
            policy = WrapReturnFinalizer(policy);
        }

        if (IsTruthy(Environment.GetEnvironmentVariable(LocalTraversalOracleEnvironmentVariable)))
        {
            policy = new LocalTraversalRecoveryPolicyRuntime(policy);
        }

        return policy;
    }

    public static bool ArePolicyOverridesEnabled()
    {
        var value = Environment.GetEnvironmentVariable(DisablePolicyOverridesEnvironmentVariable);
        return !IsTruthy(value);
    }

    public static string DescribeEnvironmentPolicyConfiguration(string? modelPath)
    {
        var parts = new List<string>();
        parts.Add(!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath)
            ? $"base={Path.GetFileName(modelPath)}"
            : "base=direct");

        var hierarchicalModelPath = Environment.GetEnvironmentVariable(ReturnHierarchicalChunkModelPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(hierarchicalModelPath) && File.Exists(hierarchicalModelPath))
        {
            parts.Add($"hier={Path.GetFileName(hierarchicalModelPath)}");
        }

        var finalizerModelPath = Environment.GetEnvironmentVariable(ReturnFinalizerModelPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(finalizerModelPath) && File.Exists(finalizerModelPath))
        {
            parts.Add($"finalizer={Path.GetFileName(finalizerModelPath)}");
        }

        parts.Add(ArePolicyOverridesEnabled() ? "overrides=on" : "overrides=off");
        if (IsTruthy(Environment.GetEnvironmentVariable(LocalTraversalOracleEnvironmentVariable)))
        {
            parts.Add("local_oracle=on");
        }

        return string.Join(" ", parts);
    }

    private static bool IsTruthy(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static IMLBotPolicyRuntime WrapReturnFinalizer(IMLBotPolicyRuntime innerPolicy)
    {
        var finalizerModelPath = Environment.GetEnvironmentVariable(ReturnFinalizerModelPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(finalizerModelPath) || !File.Exists(finalizerModelPath))
        {
            return innerPolicy;
        }

        return new ReturnIntelObjectiveOptionPolicyRuntime(
            innerPolicy,
            new OnnxMLBotPolicyRuntime(finalizerModelPath, enablePolicyOverrides: ArePolicyOverridesEnabled()),
            ParseFloatEnvironment(ReturnFinalizerEngageDistanceEnvironmentVariable, 260f),
            Environment.GetEnvironmentVariable(ReturnFinalizerLevelNameEnvironmentVariable),
            ParseEnumEnvironment<PlayerTeam>(ReturnFinalizerTeamEnvironmentVariable),
            ParseEnumEnvironment<PlayerClass>(ReturnFinalizerClassEnvironmentVariable));
    }

    private static List<int> ParseCommitTicks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var ticks = new List<int>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                ticks.Add(parsed);
            }
        }

        return ticks;
    }

    private static float ParseFloatEnvironment(string variableName, float fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(0f, parsed)
            : fallback;
    }

    private static TEnum? ParseEnumEnvironment<TEnum>(string variableName)
        where TEnum : struct
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }
}
