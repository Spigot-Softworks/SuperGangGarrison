using System.Text.Json;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client.Plugins.TeamOnlyMinimap;

internal enum MinimapShowMethod
{
    Dots = 0,
    BigDots = 1,
    ClassBubbles = 2,
}

internal enum MinimapFitMode
{
    Auto = 0,
    Width = 1,
    Height = 2,
    Reverse = 3,
}

internal enum MinimapPlayerScope
{
    None = 0,
    Myself = 1,
    Allies = 2,
}

internal enum MinimapMarkerColor
{
    Red = 0,
    Yellow = 1,
    Green = 2,
    Blue = 3,
    White = 4,
}

internal sealed class TeamOnlyMinimapConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public bool Enabled { get; set; } = true;

    public bool ShowHealth { get; set; } = true;

    public bool ShowObjective { get; set; } = true;

    public bool ShowSentry { get; set; } = true;

    public int PositionX { get; set; }

    public int PositionY { get; set; }

    public int Width { get; set; } = 200;

    public int Height { get; set; } = 200;

    public int HealingPositionX { get; set; }

    public int HealingPositionY { get; set; }

    public int HealingWidth { get; set; } = 200;

    public int HealingHeight { get; set; } = 200;

    public MinimapShowMethod ShowMethod { get; set; } = MinimapShowMethod.ClassBubbles;

    public MinimapFitMode FitMode { get; set; }

    public MinimapPlayerScope PlayersShown { get; set; } = MinimapPlayerScope.Allies;

    public int BubbleSizePercent { get; set; } = 40;

    public MinimapMarkerColor SelfColor { get; set; } = MinimapMarkerColor.Green;

    public int SelfBubbleSizePercent { get; set; } = 40;

    public int AlphaPercent { get; set; } = 100;

    public int ObjectiveBubbleSizePercent { get; set; } = 80;

    public Keys ZoomKey { get; set; } = Keys.R;

    public int ZoomRangeTenths { get; set; } = 20;

    public bool MoveNearHealingHud { get; set; }

    public static TeamOnlyMinimapConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<TeamOnlyMinimapConfig>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    return Normalize(loaded);
                }
            }
        }
        catch
        {
        }

        return new TeamOnlyMinimapConfig();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(Normalize(this), SerializerOptions));
    }

    private static TeamOnlyMinimapConfig Normalize(TeamOnlyMinimapConfig config)
    {
        return new TeamOnlyMinimapConfig
        {
            Enabled = config.Enabled,
            ShowHealth = config.ShowHealth,
            ShowObjective = config.ShowObjective,
            ShowSentry = config.ShowSentry,
            PositionX = int.Clamp(config.PositionX, 0, 4000),
            PositionY = int.Clamp(config.PositionY, 0, 4000),
            Width = int.Clamp(config.Width, 20, 1000),
            Height = int.Clamp(config.Height, 20, 1000),
            HealingPositionX = int.Clamp(config.HealingPositionX, 0, 4000),
            HealingPositionY = int.Clamp(config.HealingPositionY, 0, 4000),
            HealingWidth = int.Clamp(config.HealingWidth, 20, 1000),
            HealingHeight = int.Clamp(config.HealingHeight, 20, 1000),
            ShowMethod = Enum.IsDefined(config.ShowMethod) ? config.ShowMethod : MinimapShowMethod.ClassBubbles,
            FitMode = Enum.IsDefined(config.FitMode) ? config.FitMode : MinimapFitMode.Auto,
            PlayersShown = Enum.IsDefined(config.PlayersShown) ? config.PlayersShown : MinimapPlayerScope.Allies,
            BubbleSizePercent = int.Clamp(config.BubbleSizePercent, 10, 200),
            SelfColor = Enum.IsDefined(config.SelfColor) ? config.SelfColor : MinimapMarkerColor.Green,
            SelfBubbleSizePercent = int.Clamp(config.SelfBubbleSizePercent, 10, 200),
            AlphaPercent = int.Clamp(config.AlphaPercent, 0, 100),
            ObjectiveBubbleSizePercent = int.Clamp(config.ObjectiveBubbleSizePercent, 10, 200),
            ZoomKey = config.ZoomKey == Keys.None ? Keys.R : config.ZoomKey,
            ZoomRangeTenths = int.Clamp(config.ZoomRangeTenths, 2, 50),
            MoveNearHealingHud = config.MoveNearHealingHud,
        };
    }
}
