using System.Text.Json;

namespace OpenGarrison.Client.Plugins.ShowPing;

internal sealed class ShowPingConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public int PositionX { get; set; } = 170;

    public int PositionY { get; set; } = 560;

    public int SizeTenths { get; set; } = 10;

    public static ShowPingConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<ShowPingConfig>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    return Normalize(loaded);
                }
            }
        }
        catch
        {
        }

        return new ShowPingConfig();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(Normalize(this), SerializerOptions));
    }

    private static ShowPingConfig Normalize(ShowPingConfig config)
    {
        return new ShowPingConfig
        {
            PositionX = int.Clamp(config.PositionX, 0, 4000),
            PositionY = int.Clamp(config.PositionY, 0, 4000),
            SizeTenths = int.Clamp(config.SizeTenths, 10, 100),
        };
    }
}
