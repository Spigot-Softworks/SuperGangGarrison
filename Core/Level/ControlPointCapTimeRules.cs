namespace OpenGarrison.Core;

public static class ControlPointCapTimeRules
{
    public const float DefaultBaseCapTime = 7f * 30f;

    public static float GetCapTime(int totalPoints, int pointIndex, float baseTime, bool setupMode)
    {
        if (totalPoints <= 1)
        {
            return baseTime * (setupMode ? 15f : 9f);
        }

        if (setupMode)
        {
            return totalPoints switch
            {
                2 => pointIndex == 2 ? baseTime * 2.5f : baseTime * 10f,
                3 => pointIndex == 3 ? baseTime * 2.5f : pointIndex == 2 ? baseTime * 5f : baseTime * 7.5f,
                4 => pointIndex == 4 ? baseTime * 1.5f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 4.5f : baseTime * 6f,
                _ => pointIndex == 5 ? baseTime : pointIndex == 4 ? baseTime * 2f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 4f : baseTime * 5f,
            };
        }

        return totalPoints switch
        {
            2 => baseTime * 4.5f,
            3 => pointIndex == 2 ? baseTime * 4.5f : baseTime * 2.25f,
            4 => pointIndex == 4 ? baseTime * 1.5f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 3f : baseTime * 1.5f,
            _ => pointIndex == 5 ? baseTime : pointIndex == 4 ? baseTime * 2f : pointIndex == 3 ? baseTime * 3f : pointIndex == 2 ? baseTime * 2f : baseTime,
        };
    }

    public static float GetReferenceCapTime(int totalPoints, bool setupMode)
    {
        if (totalPoints <= 0)
        {
            return DefaultBaseCapTime;
        }

        var baseTime = DefaultBaseCapTime;
        var reference = float.MaxValue;
        for (var pointIndex = ControlPointIndexMetadata.MinIndex; pointIndex <= totalPoints; pointIndex += 1)
        {
            reference = MathF.Min(reference, GetCapTime(totalPoints, pointIndex, baseTime, setupMode));
        }

        return reference < float.MaxValue ? reference : DefaultBaseCapTime;
    }
}
