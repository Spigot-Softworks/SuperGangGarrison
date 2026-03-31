using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client.Plugins.CameraShake;

public sealed class CameraShakePlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientUpdateHooks,
    IOpenGarrisonClientSoundHooks,
    IOpenGarrisonClientCameraHooks,
    IOpenGarrisonClientOptionsHooks
{
    private const float ShakeDecayPerReferenceFrame = 0.8f;
    private const float ReferenceFramesPerSecond = 30f;
    private const float IntensityEpsilon = 0.0005f;
    private const float SourceClassDetectionDistanceSquared = 32f * 32f;
    private const float LocalSourceDistanceSquared = 20f * 20f;

    private IOpenGarrisonClientPluginContext? _context;
    private CameraShakeConfig _config = new();
    private string _configPath = string.Empty;
    private float _currentShakeIntensity;
    private Vector2 _currentCameraOffset;

    public string Id => "camerashake";

    public string DisplayName => "Camera Shake";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
        _configPath = Path.Combine(context.ConfigDirectory, "camerashake.json");
        _config = CameraShakeConfig.Load(_configPath);
        ResetShakeState();
    }

    public void Shutdown()
    {
        ResetShakeState();
        _context = null;
    }

    public void OnClientFrame(ClientFrameEvent e)
    {
        if (!e.IsGameplayActive || _config.ShakeLevel <= 0)
        {
            ResetShakeState();
            return;
        }

        if (_currentShakeIntensity <= IntensityEpsilon)
        {
            ResetShakeState();
            return;
        }

        _currentCameraOffset = CreateRandomOffset(_currentShakeIntensity);
        var referenceFrames = MathF.Max(0f, e.DeltaSeconds * ReferenceFramesPerSecond);
        _currentShakeIntensity *= MathF.Pow(ShakeDecayPerReferenceFrame, referenceFrames);
        if (_currentShakeIntensity <= IntensityEpsilon)
        {
            _currentShakeIntensity = 0f;
        }
    }

    public void OnWorldSound(ClientWorldSoundEvent e)
    {
        if (_context is null || !_context.ClientState.IsGameplayActive || _config.ShakeLevel <= 0)
        {
            return;
        }

        var contribution = GetShakeContribution(e);
        if (contribution <= 0f)
        {
            return;
        }

        _currentShakeIntensity += contribution;
        _currentCameraOffset = CreateRandomOffset(_currentShakeIntensity);
    }

    public Vector2 GetCameraOffset()
    {
        return _config.ShakeLevel > 0 ? _currentCameraOffset : Vector2.Zero;
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        return
        [
            new ClientPluginOptionsSection(
                "Camera Shake",
                [
                    new ClientPluginIntegerOptionItem(
                        "Shake level",
                        () => _config.ShakeLevel,
                        value =>
                        {
                            _config.ShakeLevel = value;
                            SaveConfig();
                            if (_config.ShakeLevel <= 0)
                            {
                                ResetShakeState();
                            }
                        },
                        0,
                        8,
                        1,
                        value => value <= 0 ? "Off" : value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ]),
        ];
    }

    private void SaveConfig()
    {
        try
        {
            _config.Save(_configPath);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to save camera shake config: {ex.Message}");
        }
    }

    private float GetShakeContribution(ClientWorldSoundEvent soundEvent)
    {
        var distance = GetDistanceToCameraCenter(soundEvent.WorldPosition);
        return soundEvent.SoundName switch
        {
            "ExplosionSnd" => 40f / distance,
            "ChaingunSnd" => MathF.Min(0.1f, 30f / distance),
            "ShotgunSnd" => MathF.Min(GetShotgunShakeCap(soundEvent.WorldPosition), 30f / distance),
            "RevolverSnd" => MathF.Min(0.1f, 30f / distance),
            "RifleSnd" => GetRifleShakeContribution(soundEvent.WorldPosition, distance),
            _ => 0f,
        };
    }

    private float GetShotgunShakeCap(Vector2 soundPosition)
    {
        return ResolveLikelyShooterClass(soundPosition) == ClientPluginClass.Scout
            ? 0.3f
            : 0.2f;
    }

    private float GetRifleShakeContribution(Vector2 soundPosition, float distance)
    {
        var localScopedBonus = IsLocalScopedSniperSource(soundPosition) ? 2f : 0f;
        var baseContribution = 0.25f + localScopedBonus;
        if (IsLocalSniperSource(soundPosition))
        {
            return baseContribution;
        }

        return MathF.Min(baseContribution, 40f / distance);
    }

    private float GetDistanceToCameraCenter(Vector2 worldPosition)
    {
        var state = _context?.ClientState;
        if (state is null)
        {
            return 1f;
        }

        var cameraCenter = state.CameraTopLeft + new Vector2(state.ViewportWidth * 0.5f, state.ViewportHeight * 0.5f);
        return MathF.Max(1f, Vector2.Distance(worldPosition, cameraCenter));
    }

    private ClientPluginClass ResolveLikelyShooterClass(Vector2 soundPosition)
    {
        var state = _context?.ClientState;
        if (state is null)
        {
            return ClientPluginClass.Unknown;
        }

        var markers = state.GetPlayerMarkers();
        var closestClass = ClientPluginClass.Unknown;
        var closestDistanceSquared = SourceClassDetectionDistanceSquared;
        for (var index = 0; index < markers.Count; index += 1)
        {
            var marker = markers[index];
            if (!marker.IsAlive)
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(marker.WorldPosition, soundPosition);
            if (distanceSquared > closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestClass = marker.ClassId;
        }

        return closestClass;
    }

    private bool IsLocalSniperSource(Vector2 soundPosition)
    {
        return IsLocalSource(soundPosition, ClientPluginClass.Sniper);
    }

    private bool IsLocalScopedSniperSource(Vector2 soundPosition)
    {
        var state = _context?.ClientState;
        return state is not null
            && state.IsLocalPlayerScoped
            && IsLocalSource(soundPosition, ClientPluginClass.Sniper);
    }

    private bool IsLocalSource(Vector2 soundPosition, ClientPluginClass expectedClass)
    {
        var state = _context?.ClientState;
        if (state is null || state.LocalPlayerClass != expectedClass || !state.TryGetLocalPlayerWorldPosition(out var localPosition))
        {
            return false;
        }

        return Vector2.DistanceSquared(localPosition, soundPosition) <= LocalSourceDistanceSquared;
    }

    private Vector2 CreateRandomOffset(float intensity)
    {
        if (_config.ShakeLevel <= 0 || intensity <= IntensityEpsilon)
        {
            return Vector2.Zero;
        }

        return new Vector2(
            RandomRange(-intensity, intensity) * _config.ShakeLevel,
            RandomRange(-intensity, intensity) * _config.ShakeLevel);
    }

    private static float RandomRange(float minValue, float maxValue)
    {
        return minValue + (Random.Shared.NextSingle() * (maxValue - minValue));
    }

    private void ResetShakeState()
    {
        _currentShakeIntensity = 0f;
        _currentCameraOffset = Vector2.Zero;
    }
}
