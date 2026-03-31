using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using System.Globalization;

namespace OpenGarrison.Client.Plugins.ShowPing;

public sealed class ShowPingPlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientHudHooks,
    IOpenGarrisonClientOptionsHooks
{
    private static readonly Color PingGreen = new(0, 255, 0);
    private static readonly Color PingYellow = Color.Yellow;
    private static readonly Color PingRed = Color.Red;
    private static readonly Color PingGray = Color.Gray;
    private IOpenGarrisonClientPluginContext? _context;
    private ShowPingConfig _config = new();
    private string _configPath = string.Empty;

    public string Id => "showping";

    public string DisplayName => "Show Ping";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
        _configPath = Path.Combine(context.ConfigDirectory, "showping.json");
        _config = ShowPingConfig.Load(_configPath);
    }

    public void Shutdown()
    {
    }

    public void OnGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas)
    {
        if (_context is null)
        {
            return;
        }

        var state = _context.ClientState;
        if (!state.IsGameplayActive || !state.IsConnected)
        {
            return;
        }

        var ping = state.LocalPingMilliseconds;
        var (label, color) = GetDisplayState(ping);
        canvas.DrawBitmapTextCentered(
            label,
            new Vector2(_config.PositionX, _config.PositionY),
            color,
            _config.SizeTenths / 10f);
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        return
        [
            new ClientPluginOptionsSection(
                "Show Ping",
                [
                    new ClientPluginIntegerOptionItem(
                        "Text size",
                        () => _config.SizeTenths,
                        value => SetConfig(config => config.SizeTenths = value),
                        minValue: 10,
                        maxValue: 100,
                        step: 1,
                        valueLabelFormatter: value => $"{value / 10f:0.0}x"),
                    new ClientPluginIntegerOptionItem(
                        "X position",
                        () => _config.PositionX,
                        value => SetConfig(config => config.PositionX = value),
                        minValue: 0,
                        maxValue: 2000,
                        step: 10),
                    new ClientPluginIntegerOptionItem(
                        "Y position",
                        () => _config.PositionY,
                        value => SetConfig(config => config.PositionY = value),
                        minValue: 0,
                        maxValue: 2000,
                        step: 10),
                ]),
        ];
    }

    private void SetConfig(Action<ShowPingConfig> apply)
    {
        apply(_config);
        SaveConfig();
    }

    private void SaveConfig()
    {
        try
        {
            _config.Save(_configPath);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to save config: {ex.Message}");
        }
    }

    private static (string Label, Color Color) GetDisplayState(int ping)
    {
        if (ping < 0)
        {
            return ("TIMEOUT", PingGray);
        }

        if (ping < 135)
        {
            return (ping.ToString(CultureInfo.InvariantCulture), PingGreen);
        }

        if (ping < 275)
        {
            return (ping.ToString(CultureInfo.InvariantCulture), PingYellow);
        }

        return (ping.ToString(CultureInfo.InvariantCulture), PingRed);
    }
}
