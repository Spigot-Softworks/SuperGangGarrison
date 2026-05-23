using System.Text.Json;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class GameplayAbilityParameterReader
{
    public static int GetInt(GameplayAbilityDefinition ability, string key, int defaultValue, int minValue = int.MinValue, int maxValue = int.MaxValue)
    {
        if (!TryGetNumber(ability, key, out var value))
        {
            return defaultValue;
        }

        return int.Clamp((int)MathF.Round((float)value), minValue, maxValue);
    }

    public static float GetFloat(GameplayAbilityDefinition ability, string key, float defaultValue, float minValue = float.NegativeInfinity, float maxValue = float.PositiveInfinity)
    {
        if (!TryGetNumber(ability, key, out var value))
        {
            return defaultValue;
        }

        return float.Clamp((float)value, minValue, maxValue);
    }

    public static bool GetBool(GameplayAbilityDefinition ability, string key, bool defaultValue)
    {
        return ability.Parameters.TryGetValue(key, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : defaultValue;
    }

    public static int GetTicks(
        GameplayAbilityDefinition ability,
        string ticksKey,
        string secondsKey,
        int defaultTicks,
        float ticksPerSecond,
        int minValue = 1,
        int maxValue = int.MaxValue)
    {
        if (TryGetNumber(ability, ticksKey, out var ticks))
        {
            return int.Clamp((int)MathF.Round((float)ticks), minValue, maxValue);
        }

        if (TryGetNumber(ability, secondsKey, out var seconds))
        {
            var convertedTicks = (int)MathF.Round(Math.Max(0f, (float)seconds) * Math.Max(1f, ticksPerSecond));
            return int.Clamp(convertedTicks, minValue, maxValue);
        }

        return int.Clamp(defaultTicks, minValue, maxValue);
    }

    public static bool TryGetNumber(GameplayAbilityDefinition ability, string key, out double value)
    {
        if (ability.Parameters.TryGetValue(key, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}
