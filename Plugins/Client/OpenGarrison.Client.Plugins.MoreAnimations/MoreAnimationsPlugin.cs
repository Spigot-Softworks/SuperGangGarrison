using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using XnaColor = Microsoft.Xna.Framework.Color;
using ImageSharpRectangle = SixLabors.ImageSharp.Rectangle;

namespace OpenGarrison.Client.Plugins.MoreAnimations;

public sealed class MoreAnimationsPlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientLifecycleHooks,
    IOpenGarrisonClientDeadBodyHooks
{
    private const float LegacyAnimationSpeedPerTick = 0.33f;
    private IOpenGarrisonClientPluginContext? _context;
    private readonly Dictionary<string, LoadedAnimation> _animations = new(StringComparer.OrdinalIgnoreCase);

    public string Id => "moreanimations";

    public string DisplayName => "More Animations";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
    }

    public void Shutdown()
    {
        foreach (var animation in _animations.Values)
        {
            animation.Dispose();
        }

        _animations.Clear();
    }

    public void OnClientStarting()
    {
    }

    public void OnClientStarted()
    {
        EnsureAnimationsLoaded();
    }

    public void OnClientStopping()
    {
    }

    public void OnClientStopped()
    {
    }

    public bool TryDrawDeadBody(IOpenGarrisonClientHudCanvas canvas, ClientDeadBodyRenderState deadBody)
    {
        if (deadBody.AnimationKind == ClientDeadBodyAnimationKind.Default
            || deadBody.ClassId == ClientPluginClass.Quote)
        {
            return false;
        }

        EnsureAnimationsLoaded();
        if (!TryResolveAnimationKey(deadBody, out var animationKey)
            || !_animations.TryGetValue(animationKey, out var animation)
            || animation.Frames.Count == 0)
        {
            return false;
        }

        var elapsedTicks = Math.Max(0, 300 - deadBody.TicksRemaining);
        var frameIndex = Math.Clamp((int)MathF.Floor(elapsedTicks * LegacyAnimationSpeedPerTick), 0, animation.Frames.Count - 1);
        canvas.DrawWorldTexture(
            animation.Frames[frameIndex],
            deadBody.WorldPosition,
            XnaColor.White,
            new Vector2(deadBody.FacingLeft ? -1f : 1f, 1f),
            null,
            0f,
            animation.Origin);
        return true;
    }

    private static bool TryResolveAnimationKey(ClientDeadBodyRenderState deadBody, out string animationKey)
    {
        animationKey = deadBody.Team switch
        {
            ClientPluginTeam.Red => deadBody.ClassId switch
            {
                ClientPluginClass.Demoman => "demoman",
                ClientPluginClass.Engineer => "engineer",
                ClientPluginClass.Heavy when deadBody.AnimationKind == ClientDeadBodyAnimationKind.Severe => "heavy3",
                ClientPluginClass.Heavy => "heavy",
                ClientPluginClass.Medic when deadBody.AnimationKind == ClientDeadBodyAnimationKind.Severe => "medic3",
                ClientPluginClass.Medic => "medic",
                ClientPluginClass.Pyro => "pyro",
                ClientPluginClass.Scout => "scout",
                ClientPluginClass.Sniper => "sniper",
                ClientPluginClass.Soldier => "soldier",
                ClientPluginClass.Spy => "spy2",
                _ => string.Empty,
            },
            ClientPluginTeam.Blue => deadBody.ClassId switch
            {
                ClientPluginClass.Demoman => "demoman2",
                ClientPluginClass.Engineer => "engineer2",
                ClientPluginClass.Heavy when deadBody.AnimationKind == ClientDeadBodyAnimationKind.Severe => "heavy4",
                ClientPluginClass.Heavy => "heavy2",
                ClientPluginClass.Medic when deadBody.AnimationKind == ClientDeadBodyAnimationKind.Severe => "medic4",
                ClientPluginClass.Medic => "medic2",
                ClientPluginClass.Pyro => "pyro2",
                ClientPluginClass.Scout => "scout2",
                ClientPluginClass.Sniper => "sniper2",
                ClientPluginClass.Soldier => "soldier2",
                ClientPluginClass.Spy => "spy",
                _ => string.Empty,
            },
            _ => string.Empty,
        };

        return !string.IsNullOrWhiteSpace(animationKey);
    }

    private void EnsureAnimationsLoaded()
    {
        if (_context is null || _animations.Count > 0)
        {
            return;
        }

        var animationDirectory = Path.Combine(_context.PluginDirectory, "Animations");
        foreach (var definition in GetDefinitions())
        {
            var path = Path.Combine(animationDirectory, definition.FileName);
            var animation = LoadAnimation(path, definition.FrameCount, definition.Origin);
            if (animation is not null)
            {
                _animations[definition.Key] = animation;
            }
        }
    }

    private LoadedAnimation? LoadAnimation(string path, int frameCount, Vector2 origin)
    {
        if (_context is null)
        {
            return null;
        }

        if (!File.Exists(path))
        {
            _context.Log($"missing animation asset at {path}");
            return null;
        }

        try
        {
            using var image = Image.Load<Rgba32>(path);
            var frames = new List<Texture2D>(Math.Max(1, frameCount));
            if (Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                var frameTotal = image.Frames.Count;
                for (var frameIndex = 0; frameIndex < frameTotal; frameIndex += 1)
                {
                    using var frameImage = image.Frames.CloneFrame(frameIndex);
                    frames.Add(CreateTexture(frameImage));
                }
            }
            else
            {
                var safeFrameCount = Math.Max(1, frameCount);
                var frameWidth = Math.Max(1, image.Width / safeFrameCount);
                for (var frameIndex = 0; frameIndex < safeFrameCount; frameIndex += 1)
                {
                    var frameRectangle = new ImageSharpRectangle(frameIndex * frameWidth, 0, frameWidth, image.Height);
                    using var frameImage = image.Clone(clone => clone.Crop(frameRectangle));
                    frames.Add(CreateTexture(frameImage));
                }
            }

            return new LoadedAnimation(frames, origin);
        }
        catch (Exception ex)
        {
            _context.Log($"failed to load animation {Path.GetFileName(path)}: {ex.Message}");
            return null;
        }
    }

    private Texture2D CreateTexture(Image<Rgba32> image)
    {
        var pixelData = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixelData);
        var textureData = new XnaColor[pixelData.Length];
        for (var index = 0; index < pixelData.Length; index += 1)
        {
            var pixel = pixelData[index];
            if (pixel.A == 0)
            {
                textureData[index] = XnaColor.Transparent;
                continue;
            }

            var premultipliedRed = (pixel.R * pixel.A + 127) / 255;
            var premultipliedGreen = (pixel.G * pixel.A + 127) / 255;
            var premultipliedBlue = (pixel.B * pixel.A + 127) / 255;
            textureData[index] = new XnaColor(
                (byte)premultipliedRed,
                (byte)premultipliedGreen,
                (byte)premultipliedBlue,
                pixel.A);
        }

        var texture = new Texture2D(_context!.GraphicsDevice, image.Width, image.Height);
        texture.SetData(textureData);
        return texture;
    }

    private static IReadOnlyList<AnimationDefinition> GetDefinitions()
    {
        return
        [
            new AnimationDefinition("demoman", "demoman.gif", 9, new Vector2(33f, 40f)),
            new AnimationDefinition("demoman2", "demoman2.png", 9, new Vector2(33f, 40f)),
            new AnimationDefinition("engineer", "engineer.gif", 10, new Vector2(32f, 40f)),
            new AnimationDefinition("engineer2", "engineer2.png", 10, new Vector2(32f, 40f)),
            new AnimationDefinition("heavy", "heavy.gif", 13, new Vector2(19f, 40f)),
            new AnimationDefinition("heavy2", "heavy2.png", 13, new Vector2(19f, 40f)),
            new AnimationDefinition("heavy3", "heavy3.gif", 18, new Vector2(19f, 40f)),
            new AnimationDefinition("heavy4", "heavy4.png", 18, new Vector2(19f, 40f)),
            new AnimationDefinition("medic", "medic.gif", 11, new Vector2(27f, 40f)),
            new AnimationDefinition("medic2", "medic2.png", 11, new Vector2(27f, 40f)),
            new AnimationDefinition("medic3", "medic3.gif", 22, new Vector2(27f, 40f)),
            new AnimationDefinition("medic4", "medic4.png", 22, new Vector2(27f, 40f)),
            new AnimationDefinition("pyro", "pyro.gif", 10, new Vector2(31f, 40f)),
            new AnimationDefinition("pyro2", "pyro2.png", 10, new Vector2(31f, 40f)),
            new AnimationDefinition("scout", "scout.png", 10, new Vector2(30f, 40f)),
            new AnimationDefinition("scout2", "scout2.png", 10, new Vector2(30f, 40f)),
            new AnimationDefinition("sniper", "sniper.gif", 13, new Vector2(26f, 40f)),
            new AnimationDefinition("sniper2", "sniper2.png", 13, new Vector2(26f, 40f)),
            new AnimationDefinition("soldier", "soldier.gif", 11, new Vector2(30f, 40f)),
            new AnimationDefinition("soldier2", "soldier2.png", 11, new Vector2(30f, 40f)),
            new AnimationDefinition("spy", "spy.gif", 30, new Vector2(28f, 40f)),
            new AnimationDefinition("spy2", "spy2.png", 30, new Vector2(28f, 40f)),
        ];
    }

    private sealed record AnimationDefinition(string Key, string FileName, int FrameCount, Vector2 Origin);

    private sealed class LoadedAnimation(List<Texture2D> frames, Vector2 origin) : IDisposable
    {
        public List<Texture2D> Frames { get; } = frames;

        public Vector2 Origin { get; } = origin;

        public void Dispose()
        {
            for (var index = 0; index < Frames.Count; index += 1)
            {
                try
                {
                    Frames[index].Dispose();
                }
                catch
                {
                }
            }

            Frames.Clear();
        }
    }
}
