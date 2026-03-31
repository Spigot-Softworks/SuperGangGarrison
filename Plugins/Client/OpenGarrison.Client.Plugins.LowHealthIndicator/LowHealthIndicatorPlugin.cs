using Microsoft.Xna.Framework.Audio;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client.Plugins.LowHealthIndicator;

public sealed class LowHealthIndicatorPlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientUpdateHooks,
    IOpenGarrisonClientOptionsHooks
{
    private const float LegacyFramesPerSecond = 30f;
    private IOpenGarrisonClientPluginContext? _context;
    private LowHealthIndicatorConfig _config = new();
    private string _configPath = string.Empty;
    private SoundEffect? _warningSound;
    private float _warningElapsedFrames;

    public string Id => "lowhealthindicator";

    public string DisplayName => "Low Health Indicator";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
        _configPath = Path.Combine(context.ConfigDirectory, "lowhealthindicator.json");
        _config = LowHealthIndicatorConfig.Load(_configPath);
        LoadWarningSound();
    }

    public void Shutdown()
    {
        _warningElapsedFrames = 0f;
        try
        {
            _warningSound?.Dispose();
        }
        catch
        {
        }

        _warningSound = null;
    }

    public void OnClientFrame(ClientFrameEvent e)
    {
        if (!e.IsGameplayActive
            || _context is null
            || _context.ClientState.IsSpectator
            || !_context.ClientState.IsLocalPlayerAlive
            || !_context.ClientState.TryGetLocalPlayerHealth(out var health, out var maxHealth))
        {
            _warningElapsedFrames = 0f;
            return;
        }

        _warningElapsedFrames += e.DeltaSeconds * LegacyFramesPerSecond;
        if (_warningElapsedFrames < _config.WarningTimerFrames)
        {
            return;
        }

        if (!ShouldPlayWarning(health, maxHealth))
        {
            return;
        }

        TryPlayWarning();
        _warningElapsedFrames = 0f;
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        return
        [
            new ClientPluginOptionsSection(
                "Low Health Indicator",
                [
                    new ClientPluginIntegerOptionItem(
                        "Warning volume",
                        () => _config.WarningVolumePercent,
                        value =>
                        {
                            _config.WarningVolumePercent = value;
                            SaveConfig();
                        },
                        minValue: 0,
                        maxValue: 100,
                        step: 5,
                        valueLabelFormatter: value => $"{value}%"),
                    new ClientPluginIntegerOptionItem(
                        "Warning delay",
                        () => _config.WarningTimerFrames,
                        value =>
                        {
                            _config.WarningTimerFrames = value;
                            SaveConfig();
                        },
                        minValue: 0,
                        maxValue: 180,
                        step: 5,
                        valueLabelFormatter: value => $"{value}f"),
                    new ClientPluginBooleanOptionItem(
                        "Percentage based calculations?",
                        () => _config.UsePercentageThreshold,
                        value =>
                        {
                            _config.UsePercentageThreshold = value;
                            SaveConfig();
                        },
                        trueLabel: "Yes",
                        falseLabel: "No"),
                    new ClientPluginIntegerOptionItem(
                        "Warning threshold",
                        () => _config.WarningHealthThreshold,
                        value =>
                        {
                            _config.WarningHealthThreshold = value;
                            SaveConfig();
                        },
                        minValue: 0,
                        maxValue: 100,
                        step: 5,
                        valueLabelFormatter: value => _config.UsePercentageThreshold ? $"{value}%" : value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ]),
        ];
    }

    private bool ShouldPlayWarning(int health, int maxHealth)
    {
        if (_config.UsePercentageThreshold)
        {
            var thresholdHealth = (int)MathF.Floor(maxHealth * (_config.WarningHealthThreshold / 100f));
            return health <= thresholdHealth;
        }

        return health <= _config.WarningHealthThreshold;
    }

    private void LoadWarningSound()
    {
        var soundPath = Path.Combine(_context?.PluginDirectory ?? string.Empty, "Resources", "PrOF", "boop.wav");
        if (!File.Exists(soundPath))
        {
            _context?.Log($"warning sound missing at {soundPath}");
            return;
        }

        try
        {
            using var stream = File.OpenRead(soundPath);
            _warningSound = SoundEffect.FromStream(stream);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to load warning sound: {ex.Message}");
        }
    }

    private void TryPlayWarning()
    {
        if (_warningSound is null)
        {
            return;
        }

        try
        {
            _warningSound.Play(_config.WarningVolumePercent / 100f, 0f, 0f);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to play warning sound: {ex.Message}");
        }
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
}
