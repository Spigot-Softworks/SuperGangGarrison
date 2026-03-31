using System.Text.Json;

namespace OpenGarrison.Client.Plugins.CameraShake;

internal sealed class CameraShakeConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public int ShakeLevel { get; set; } = 4;

    public static CameraShakeConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<CameraShakeConfig>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    return Normalize(loaded);
                }
            }
        }
        catch
        {
        }

        return new CameraShakeConfig();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(Normalize(this), SerializerOptions));
    }

    private static CameraShakeConfig Normalize(CameraShakeConfig config)
    {
        return new CameraShakeConfig
        {
            ShakeLevel = int.Clamp(config.ShakeLevel, 0, 8),
        };
    }
}
