using System.Text.Json;

namespace OpenGarrison.Client.Plugins.DamageIndicator;

internal sealed class DamageIndicatorConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public int Style { get; set; } = 1;

    public bool PlayDing { get; set; } = true;

    public bool MoveCounterForHud { get; set; }

    public bool StereoDing { get; set; }

    public static DamageIndicatorConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<DamageIndicatorConfig>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
        }

        return new DamageIndicatorConfig();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }
}
