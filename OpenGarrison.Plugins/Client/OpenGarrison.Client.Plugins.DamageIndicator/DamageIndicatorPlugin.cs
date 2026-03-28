using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client.Plugins.DamageIndicator;

public sealed class DamageIndicatorPlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientUpdateHooks,
    IOpenGarrisonClientHudHooks,
    IOpenGarrisonClientDamageHooks,
    IOpenGarrisonClientOptionsHooks
{
    private const float WorldIndicatorLifetimeSeconds = 0.67f;
    private const float HudIndicatorLifetimeSeconds = 0.67f;
    private const float RollupIdleSeconds = 2f;
    private const float WorldIndicatorRiseSpeed = 150f;
    private const float HudIndicatorRiseSpeed = 150f;
    private static readonly Color OffWhite = new(217, 217, 183);
    private static readonly Color WareyaGreen = new(0, 255, 0);
    private static readonly Color LorganRed = Color.Red;
    private readonly List<WorldDamageIndicator> _worldIndicators = new();
    private readonly List<HudDamageIndicator> _hudIndicators = new();
    private IOpenGarrisonClientPluginContext? _context;
    private DamageIndicatorConfig _config = new();
    private string _configPath = string.Empty;
    private SoundEffect? _dingSound;
    private int _rollingDamage;
    private float _rollingDamageTimerSeconds;

    public string Id => "damageindicator";

    public string DisplayName => "Damage Indicator";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
        _configPath = Path.Combine(context.ConfigDirectory, "damageindicator.json");
        _config = DamageIndicatorConfig.Load(_configPath);
        LoadDingSound();
    }

    public void Shutdown()
    {
        _worldIndicators.Clear();
        _hudIndicators.Clear();
        _rollingDamage = 0;
        _rollingDamageTimerSeconds = 0f;
        try
        {
            _dingSound?.Dispose();
        }
        catch
        {
        }

        _dingSound = null;
    }

    public void OnClientFrame(ClientFrameEvent e)
    {
        if (!e.IsGameplayActive)
        {
            _worldIndicators.Clear();
            _hudIndicators.Clear();
            _rollingDamage = 0;
            _rollingDamageTimerSeconds = 0f;
            return;
        }

        AdvanceWorldIndicators(e.DeltaSeconds);
        AdvanceHudIndicators(e.DeltaSeconds);

        if (_rollingDamage <= 0)
        {
            return;
        }

        _rollingDamageTimerSeconds -= e.DeltaSeconds;
        if (_rollingDamageTimerSeconds > 0f)
        {
            return;
        }

        _hudIndicators.Add(new HudDamageIndicator(_rollingDamage));
        _rollingDamage = 0;
        _rollingDamageTimerSeconds = 0f;
    }

    public void OnGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas)
    {
        DrawWorldIndicators(canvas);
        DrawHudDamageIndicators(canvas);
        DrawRollingDamage(canvas);
    }

    public void OnLocalDamage(LocalDamageEvent e)
    {
        if (!e.DealtByLocalPlayer && !e.AssistedByLocalPlayer)
        {
            return;
        }

        if (e.Amount <= 1)
        {
            return;
        }

        if (TryMergeWorldIndicator(e))
        {
            _rollingDamage += e.Amount;
            _rollingDamageTimerSeconds = RollupIdleSeconds;
            return;
        }

        _worldIndicators.Add(new WorldDamageIndicator(
            e.TargetKind,
            e.TargetEntityId,
            e.TargetWorldPosition,
            e.Amount));
        _rollingDamage += e.Amount;
        _rollingDamageTimerSeconds = RollupIdleSeconds;
        PlayDing(e.TargetWorldPosition);
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        return
        [
            new ClientPluginOptionsSection(
                "Damage Indicator",
                [
                    new ClientPluginChoiceOptionItem(
                        "Damage indicator style",
                        () => _config.Style,
                        value =>
                        {
                            _config.Style = value;
                            SaveConfig();
                        },
                        [
                            new ClientPluginChoiceOptionValue(0, "Wareya's"),
                            new ClientPluginChoiceOptionValue(1, "Lorgan's"),
                        ]),
                    new ClientPluginBooleanOptionItem(
                        "Ding sound on hit",
                        () => _config.PlayDing,
                        value =>
                        {
                            _config.PlayDing = value;
                            SaveConfig();
                        }),
                    new ClientPluginBooleanOptionItem(
                        "Move counter for HUDs",
                        () => _config.MoveCounterForHud,
                        value =>
                        {
                            _config.MoveCounterForHud = value;
                            SaveConfig();
                        }),
                    new ClientPluginBooleanOptionItem(
                        "Ding sound is stereo",
                        () => _config.StereoDing,
                        value =>
                        {
                            _config.StereoDing = value;
                            SaveConfig();
                        }),
                ]),
        ];
    }

    private void LoadDingSound()
    {
        var dingPath = Path.Combine(_context?.PluginDirectory ?? string.Empty, "dingaling.wav");
        if (!File.Exists(dingPath))
        {
            _context?.Log($"ding sound missing at {dingPath}");
            return;
        }

        try
        {
            using var stream = File.OpenRead(dingPath);
            _dingSound = SoundEffect.FromStream(stream);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to load ding sound: {ex.Message}");
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

    private bool TryMergeWorldIndicator(LocalDamageEvent e)
    {
        for (var index = 0; index < _worldIndicators.Count; index += 1)
        {
            var indicator = _worldIndicators[index];
            if (indicator.TargetKind != e.TargetKind
                || indicator.TargetEntityId != e.TargetEntityId
                || indicator.AgeSeconds > 0.15f)
            {
                continue;
            }

            indicator.Amount += e.Amount;
            indicator.WorldPosition = e.TargetWorldPosition;
            indicator.AgeSeconds = 0f;
            indicator.YOffset = 0f;
            _worldIndicators[index] = indicator;
            return true;
        }

        return false;
    }

    private void AdvanceWorldIndicators(float deltaSeconds)
    {
        for (var index = _worldIndicators.Count - 1; index >= 0; index -= 1)
        {
            var indicator = _worldIndicators[index];
            indicator.AgeSeconds += deltaSeconds;
            indicator.YOffset -= WorldIndicatorRiseSpeed * deltaSeconds;
            if (indicator.AgeSeconds >= WorldIndicatorLifetimeSeconds)
            {
                _worldIndicators.RemoveAt(index);
                continue;
            }

            _worldIndicators[index] = indicator;
        }
    }

    private void AdvanceHudIndicators(float deltaSeconds)
    {
        for (var index = _hudIndicators.Count - 1; index >= 0; index -= 1)
        {
            var indicator = _hudIndicators[index];
            indicator.AgeSeconds += deltaSeconds;
            indicator.YOffset -= HudIndicatorRiseSpeed * deltaSeconds;
            if (indicator.AgeSeconds >= HudIndicatorLifetimeSeconds)
            {
                _hudIndicators.RemoveAt(index);
                continue;
            }

            _hudIndicators[index] = indicator;
        }
    }

    private void DrawWorldIndicators(IOpenGarrisonClientHudCanvas canvas)
    {
        for (var index = 0; index < _worldIndicators.Count; index += 1)
        {
            var indicator = _worldIndicators[index];
            var position = ResolveWorldIndicatorPosition(canvas, indicator);
            var scale = 1f + MathF.Floor(indicator.Amount / 100f);
            DrawDamageText(
                canvas,
                $"-{indicator.Amount}",
                position + new Vector2(0f, indicator.YOffset),
                scale,
                Math.Clamp(1f - (indicator.AgeSeconds / WorldIndicatorLifetimeSeconds), 0f, 1f),
                centered: true,
                bottomAligned: false);
        }
    }

    private void DrawHudDamageIndicators(IOpenGarrisonClientHudCanvas canvas)
    {
        for (var index = 0; index < _hudIndicators.Count; index += 1)
        {
            var indicator = _hudIndicators[index];
            var (position, scale, bottomAligned) = GetHudAnchor(canvas, indicator.YOffset);
            DrawDamageText(
                canvas,
                $"-{indicator.Amount}",
                position,
                scale,
                Math.Clamp(1f - (indicator.AgeSeconds / HudIndicatorLifetimeSeconds), 0f, 1f),
                centered: false,
                bottomAligned: bottomAligned);
        }
    }

    private void DrawRollingDamage(IOpenGarrisonClientHudCanvas canvas)
    {
        if (_rollingDamage <= 0)
        {
            return;
        }

        var (position, scale, bottomAligned) = GetHudAnchor(canvas, 0f);
        DrawDamageText(canvas, $"-{_rollingDamage}", position, scale, 1f, centered: false, bottomAligned: bottomAligned);
    }

    private Vector2 ResolveWorldIndicatorPosition(IOpenGarrisonClientHudCanvas canvas, WorldDamageIndicator indicator)
    {
        var worldPosition = indicator.WorldPosition;
        if (indicator.TargetKind == DamageTargetKind.Player
            && _context?.ClientState.TryGetPlayerWorldPosition(indicator.TargetEntityId, out var playerPosition) == true)
        {
            worldPosition = playerPosition;
        }

        return canvas.WorldToScreen(worldPosition);
    }

    private (Vector2 Position, float Scale, bool BottomAligned) GetHudAnchor(IOpenGarrisonClientHudCanvas canvas, float yOffset)
    {
        if (_config.MoveCounterForHud)
        {
            return (new Vector2(64f, 547f + yOffset), 2f, true);
        }

        return (new Vector2(89f, 567f + yOffset), 3f, false);
    }

    private void DrawDamageText(
        IOpenGarrisonClientHudCanvas canvas,
        string text,
        Vector2 position,
        float scale,
        float alpha,
        bool centered,
        bool bottomAligned)
    {
        var drawPosition = position;
        var height = canvas.MeasureBitmapTextHeight(scale);
        if (centered)
        {
            drawPosition.Y -= height / 2f;
        }
        else if (bottomAligned)
        {
            drawPosition.Y -= height;
        }

        if (_config.Style == 1)
        {
            DrawText(canvas, text, drawPosition, LorganRed * alpha, scale, centered);
            return;
        }

        var shadowOffset = new Vector2(scale, scale);
        DrawText(canvas, text, drawPosition + shadowOffset, OffWhite * alpha, scale, centered);
        DrawText(canvas, text, drawPosition, WareyaGreen * alpha, scale, centered);
    }

    private static void DrawText(IOpenGarrisonClientHudCanvas canvas, string text, Vector2 position, Color color, float scale, bool centered)
    {
        if (centered)
        {
            canvas.DrawBitmapTextCentered(text, position, color, scale);
        }
        else
        {
            canvas.DrawBitmapText(text, position, color, scale);
        }
    }

    private void PlayDing(Vector2 targetWorldPosition)
    {
        if (!_config.PlayDing || _dingSound is null)
        {
            return;
        }

        try
        {
            var pan = 0f;
            if (_config.StereoDing && _context is not null)
            {
                var state = _context.ClientState;
                var clampedScreenX = Math.Clamp(targetWorldPosition.X - state.CameraTopLeft.X, 0f, state.ViewportWidth);
                pan = state.ViewportWidth <= 0
                    ? 0f
                    : Math.Clamp((clampedScreenX / (state.ViewportWidth / 2f)) - 1f, -1f, 1f);
            }

            _dingSound.Play(1f, 0f, pan);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to play ding sound: {ex.Message}");
        }
    }

    private struct WorldDamageIndicator(
        DamageTargetKind targetKind,
        int targetEntityId,
        Vector2 worldPosition,
        int amount)
    {
        public DamageTargetKind TargetKind = targetKind;
        public int TargetEntityId = targetEntityId;
        public Vector2 WorldPosition = worldPosition;
        public int Amount = amount;
        public float AgeSeconds;
        public float YOffset;
    }

    private struct HudDamageIndicator(int amount)
    {
        public int Amount = amount;
        public float AgeSeconds;
        public float YOffset;
    }
}
