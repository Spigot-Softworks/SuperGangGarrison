using System.Text.Json;

namespace OpenGarrison.Client.Plugins.LowHealthIndicator;

internal sealed class LowHealthIndicatorConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public int WarningVolumePercent { get; set; } = 70;

    public int WarningTimerFrames { get; set; } = 60;

    public int WarningHealthThreshold { get; set; } = 40;

    public bool UsePercentageThreshold { get; set; }

    public static LowHealthIndicatorConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<LowHealthIndicatorConfig>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    return Normalize(loaded);
                }
            }
        }
        catch
        {
        }

        return new LowHealthIndicatorConfig();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(Normalize(this), SerializerOptions));
    }

    private static LowHealthIndicatorConfig Normalize(LowHealthIndicatorConfig config)
    {
        return new LowHealthIndicatorConfig
        {
            WarningVolumePercent = int.Clamp(config.WarningVolumePercent, 0, 100),
            WarningTimerFrames = int.Clamp(config.WarningTimerFrames, 0, 180),
            WarningHealthThreshold = int.Clamp(config.WarningHealthThreshold, 0, 100),
            UsePercentageThreshold = config.UsePercentageThreshold,
        };
    }
}
