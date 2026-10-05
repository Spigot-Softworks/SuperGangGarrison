using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameMakerRuntimeSoundResolutionTests
{
    [Fact]
    public void PackagedSoundPathsResolveUnderContentRootAndSourcePathsStayAbsolute()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "og2-install", "Content");

        var packagedPath = GameMakerRuntimeAssetCache.ResolveDesktopSoundAssetPath(
            "Content/Sounds/ChaingunSnd.wav",
            contentRoot);
        var sourcePath = Path.Combine(contentRoot, "Sounds", "CustomSnd.wav");

        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "Sounds", "ChaingunSnd.wav")), packagedPath);
        Assert.Equal(sourcePath, GameMakerRuntimeAssetCache.ResolveDesktopSoundAssetPath(sourcePath, contentRoot));
    }

    [Fact]
    public void MissingSoundPathIsProbedOnceAndPendingNetworkEventsAreConsumed()
    {
        var path = Path.Combine(Path.GetTempPath(), "og2-missing-sound", "ChaingunSnd.wav");
        var probeCount = 0;
        using var assets = CreateAssetCache("ChaingunSnd", path, _ =>
        {
            probeCount += 1;
            return false;
        });
        var context = CreateAudioContext(assets, tryPlaySound: false);
        var events = new GameplayAudioEventController(context);

        for (var eventId = 1; eventId <= 8; eventId += 1)
        {
            events.QueuePendingNetworkSoundEvent(new WorldSoundEvent("ChaingunSnd", 12f, 34f, EventId: (ulong)eventId));
            events.PlayPendingSoundEvents();
            Assert.Equal(0, GetPendingNetworkSoundCount(events));
        }

        events.QueuePendingNetworkSoundEvent(new WorldSoundEvent("UnrecognizedSnd", 12f, 34f, EventId: 100));
        events.PlayPendingSoundEvents();

        Assert.Equal(0, GetPendingNetworkSoundCount(events));
        Assert.Equal(1, probeCount);
        Assert.True(assets.IsDesktopSoundPermanentlyUnavailable("ChaingunSnd"));
    }

    [Fact]
    public void FilePresentSoundDecodeFailureRemainsPendingForRetry()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "og2-audio-retry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var path = Path.Combine(temporaryDirectory, "broken.wav");
            File.WriteAllBytes(path, [1, 2, 3]);

            using var assets = CreateAssetCache("TestSnd", path, File.Exists);
            var events = new GameplayAudioEventController(CreateAudioContext(assets, tryPlaySound: false));
            var soundEvent = new WorldSoundEvent("TestSnd", 12f, 34f, EventId: 99);
            events.QueuePendingNetworkSoundEvent(soundEvent);

            events.PlayPendingSoundEvents();
            Assert.Equal(1, GetPendingNetworkSoundCount(events));
            events.PlayPendingSoundEvents();
            Assert.Equal(1, GetPendingNetworkSoundCount(events));
            Assert.False(assets.IsDesktopSoundPermanentlyUnavailable("TestSnd"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadedSoundPlaybackFailureRemainsPendingForRetry()
    {
        var path = Path.Combine(Path.GetTempPath(), "og2-loaded-sound", "TestSnd.wav");
        using var assets = CreateAssetCache("TestSnd", path, _ => true);
        CacheUninitializedSound(assets, "TestSnd");
        try
        {
            var events = new GameplayAudioEventController(CreateAudioContext(assets, tryPlaySound: false));
            events.QueuePendingNetworkSoundEvent(new WorldSoundEvent("TestSnd", 12f, 34f, EventId: 101));

            events.PlayPendingSoundEvents();
            Assert.Equal(1, GetPendingNetworkSoundCount(events));
            events.PlayPendingSoundEvents();
            Assert.Equal(1, GetPendingNetworkSoundCount(events));
            Assert.False(assets.IsDesktopSoundPermanentlyUnavailable("TestSnd"));
        }
        finally
        {
            RemoveCachedSound(assets, "TestSnd");
        }
    }

    [Fact]
    public void MissingFlareImpactAndMissingFallbackAreConsumed()
    {
        var flarePath = Path.Combine(Path.GetTempPath(), "og2-missing-sound", "FlareImpactSnd.wav");
        var fallbackPath = Path.Combine(Path.GetTempPath(), "og2-missing-sound", "DirecthitSnd.wav");
        var probeCount = 0;
        using var assets = CreateAssetCache(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FlareImpactSnd"] = flarePath,
            ["DirecthitSnd"] = fallbackPath,
        }, _ =>
        {
            probeCount += 1;
            return false;
        });
        var events = new GameplayAudioEventController(CreateAudioContext(assets, tryPlaySound: false));
        events.QueuePendingNetworkSoundEvent(new WorldSoundEvent("FlareImpactSnd", 12f, 34f, EventId: 102));

        events.PlayPendingSoundEvents();

        Assert.Equal(0, GetPendingNetworkSoundCount(events));
        Assert.Equal(2, probeCount);
    }

    private static GameMakerRuntimeAssetCache CreateAssetCache(
        string soundName,
        string audioPath,
        Func<string, bool> fileExists)
    {
        return CreateAssetCache(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [soundName] = audioPath,
        }, fileExists);
    }

    private static GameMakerRuntimeAssetCache CreateAssetCache(
        IReadOnlyDictionary<string, string> soundPaths,
        Func<string, bool> fileExists)
    {
        var sounds = new Dictionary<string, GameMakerSoundAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var (soundName, audioPath) in soundPaths)
        {
            sounds[soundName] = new GameMakerSoundAsset(
                soundName,
                "metadata.xml",
                audioPath,
                Path.GetExtension(audioPath).TrimStart('.'),
                "normal",
                0f,
                1f,
                Preload: false);
        }

        var manifest = new GameMakerAssetManifest(
            sourceRootPath: null,
            sprites: new Dictionary<string, GameMakerSpriteAsset>(StringComparer.OrdinalIgnoreCase),
            backgrounds: new Dictionary<string, GameMakerBackgroundAsset>(StringComparer.OrdinalIgnoreCase),
            sounds: sounds);

        return new GameMakerRuntimeAssetCache(null!, manifest, fileExists);
    }

    private static void CacheUninitializedSound(GameMakerRuntimeAssetCache assets, string soundName)
    {
        GetCachedSounds(assets)[soundName] = (Microsoft.Xna.Framework.Audio.SoundEffect)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(Microsoft.Xna.Framework.Audio.SoundEffect));
    }

    private static void RemoveCachedSound(GameMakerRuntimeAssetCache assets, string soundName)
    {
        GetCachedSounds(assets).Remove(soundName);
    }

    private static Dictionary<string, Microsoft.Xna.Framework.Audio.SoundEffect> GetCachedSounds(
        GameMakerRuntimeAssetCache assets)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        return (Dictionary<string, Microsoft.Xna.Framework.Audio.SoundEffect>)typeof(GameMakerRuntimeAssetCache)
            .GetField("_sounds", flags)!
            .GetValue(assets)!;
    }

    private static IAudioContext CreateAudioContext(GameMakerRuntimeAssetCache assets, bool tryPlaySound)
    {
        var context = DispatchProxy.Create<IAudioContext, TestAudioContextProxy>();
        ((TestAudioContextProxy)(object)context).Configure(assets, tryPlaySound);
        return context;
    }

    private static int GetPendingNetworkSoundCount(GameplayAudioEventController controller)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pending = (List<WorldSoundEvent>)controller.GetType()
            .GetField("_pendingNetworkSoundEvents", flags)!
            .GetValue(controller)!;
        return pending.Count;
    }

    public class TestAudioContextProxy : DispatchProxy
    {
        private GameMakerRuntimeAssetCache _assets = null!;
        private SimulationWorld _world = new();
        private bool _tryPlaySound;

        public void Configure(GameMakerRuntimeAssetCache assets, bool tryPlaySound)
        {
            _assets = assets;
            _tryPlaySound = tryPlaySound;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get__audioAvailable" => true,
                "get__config" => _world.Config,
                "get__runtimeAssets" => _assets,
                "get__world" => _world,
                "TryPlaySound" => _tryPlaySound,
                "GetWorldSoundMix" => (1f, 0f),
                _ when targetMethod?.ReturnType == typeof(void) => null,
                _ when targetMethod?.ReturnType == typeof(bool) => false,
                _ when targetMethod?.ReturnType == typeof(int) => 0,
                _ when targetMethod?.ReturnType == typeof(float) => 0f,
                _ when targetMethod?.ReturnType == typeof(double) => 0d,
                _ when targetMethod?.ReturnType.IsValueType == true => Activator.CreateInstance(targetMethod.ReturnType),
                _ => null,
            };
        }
    }
}
