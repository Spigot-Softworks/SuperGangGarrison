using System.Net;
using System.Net.Http;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayPackArtSyncTests : IDisposable
{
    private const string PackId = "test.art";
    private const string OwnerKey = "server-plugins:test/art";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenGarrison.PackArtTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ManifestHashIsDeterministicAndChangesWithContent()
    {
        var packDirectory = CreatePackDirectory();
        var registry = RegisterPack(packDirectory);
        var sprites = GetPackSprites(registry);

        Assert.True(GameplayPackAssetManifest.TryCreate(PackId, packDirectory, sprites, out var first, out var error), error);
        Assert.True(GameplayPackAssetManifest.TryCreate(PackId, packDirectory, sprites, out var second, out error), error);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(["assets/ArtRedS/0.png", "assets/ArtRedS/1.png"], first.Files.Select(static file => file.Path));

        File.WriteAllBytes(Path.Combine(packDirectory, "assets", "ArtRedS", "1.png"), [9, 9, 9]);
        Assert.True(GameplayPackAssetManifest.TryCreate(PackId, packDirectory, sprites, out var changed, out error), error);
        Assert.NotEqual(first.ContentHash, changed.ContentHash);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("assets/../../escape.png")]
    [InlineData("/rooted.png")]
    [InlineData("C:/drive.png")]
    [InlineData("assets/script.lua")]
    [InlineData("assets//double.png")]
    public void ManifestRejectsUnsafePaths(string path)
    {
        Assert.False(GameplayPackAssetManifest.TryNormalizeRelativePath(path, out _));
    }

    [Fact]
    public void ContentSyncAdvertisesPackArtAndClientAppliesSpritesUntilDisconnect()
    {
        var packDirectory = CreatePackDirectory();
        var server = RegisterPack(packDirectory);

        var content = server.CreateReplicatedContent();
        var assetPack = Assert.Single(content.AssetPacks!);
        Assert.Equal(PackId, assetPack.PackId);
        Assert.Equal(GameplayPackAssetManifest.BuildManifestUrl(PackId, assetPack.ContentHash), assetPack.ManifestUrl);
        Assert.Contains("ArtRedS", assetPack.Sprites.Keys);

        Assert.True(GameplayReplicatedContent.TryDeserialize(content.SerializeToUtf8Json(), out var received, out var error), error);
        var client = GameplayRuntimeRegistry.CreateStock();
        client.ApplyReplicatedContent(received, GameplayRuntimeRegistry.ReplicatedContentOwnerKey);

        var clientPack = Assert.Single(client.ModPacks, pack => pack.Id == PackId);
        Assert.Equal(12, clientPack.Assets.Sprites["ArtRedS"].Mask?.Left);
        Assert.False(client.TryGetPackDirectory(PackId, out _));

        Assert.True(client.RemoveOwnedRegistrations(GameplayRuntimeRegistry.ReplicatedContentOwnerKey));
        Assert.DoesNotContain(client.ModPacks, pack => pack.Id == PackId);
    }

    [Fact]
    public void PackSoundsAreDownloadableAndSyncedWithTheArt()
    {
        var packDirectory = CreatePackDirectory();
        AddSound(packDirectory, "ArtHitSnd", "assets/sounds/hit.wav");
        var server = RegisterPack(packDirectory);

        var sound = Assert.Single(server.ModPacks.Single(pack => pack.Id == PackId).Assets.SoundsOrEmpty.Values);
        Assert.Equal(new OpenGarrison.GameplayModding.GameplaySoundAssetDefinition("ArtHitSnd", "assets/sounds/hit.wav"), sound);
        Assert.True(GameplayPackAssetManifest.TryCreate(PackId, packDirectory, server.ModPacks.Single(pack => pack.Id == PackId).Assets, out var manifest, out var error), error);
        Assert.Contains("assets/sounds/hit.wav", manifest.Files.Select(static file => file.Path));

        var content = server.CreateReplicatedContent();
        Assert.True(GameplayReplicatedContent.TryDeserialize(content.SerializeToUtf8Json(), out var received, out error), error);
        var client = GameplayRuntimeRegistry.CreateStock();
        client.ApplyReplicatedContent(received, GameplayRuntimeRegistry.ReplicatedContentOwnerKey);
        Assert.Equal("assets/sounds/hit.wav", client.ModPacks.Single(pack => pack.Id == PackId).Assets.SoundsOrEmpty["ArtHitSnd"].Path);

        // A sound path can't stand in for a sprite frame, or point at a PNG.
        Assert.False(GameplayPackAssetManifest.TryCreate(
            PackId,
            packDirectory,
            [],
            [new OpenGarrison.GameplayModding.GameplaySoundAssetDefinition("BadSnd", "assets/ArtRedS/0.png")],
            out _,
            out _));
    }

    [Theory]
    [InlineData("ArtSnd", "assets/sounds/hit.mp3")]
    [InlineData("ArtSnd", "../hit.wav")]
    [InlineData("ThisSoundNameIsMuchTooLongToFitInASoundEventBecauseItGoesPast64Bytes", "assets/sounds/hit.wav")]
    public void PackLoaderRejectsBadSounds(string soundId, string soundPath)
    {
        var packDirectory = CreatePackDirectory();
        AddSound(packDirectory, soundId, soundPath);

        Assert.Throws<InvalidOperationException>(() => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));
    }

    [Fact]
    public void PackDirectoryIsRemovedWithItsPluginRegistrations()
    {
        var registry = RegisterPack(CreatePackDirectory());
        Assert.True(registry.TryGetPackDirectory(PackId, out _));

        Assert.True(registry.RemoveOwnedRegistrations(OwnerKey));

        Assert.False(registry.TryGetPackDirectory(PackId, out _));
        Assert.True(registry.CreateReplicatedContent().IsEmpty);
    }

    [Fact]
    public void ServerOnlyServesListedFilesForTheCurrentHash()
    {
        var packDirectory = CreatePackDirectory();
        File.WriteAllText(Path.Combine(packDirectory, "secret.png"), "not listed");
        var registry = RegisterPack(packDirectory);
        var resolver = new ServerPluginPackFileResolver(registry);
        var hashHex = GameplayPackAssetManifest.GetHashHex(Assert.Single(registry.CreateReplicatedContent().AssetPacks!).ContentHash);

        Assert.True(resolver.TryGetManifest(PackId, hashHex, out var manifest, out _));
        Assert.Equal(2, manifest.Files.Count);
        Assert.True(resolver.TryResolveFile(PackId, hashHex, "assets/ArtRedS/0.png", out var fullPath));
        Assert.True(File.Exists(fullPath));

        Assert.False(resolver.TryResolveFile(PackId, hashHex, "secret.png", out _));
        Assert.False(resolver.TryResolveFile(PackId, hashHex, "assets/../secret.png", out _));
        Assert.False(resolver.TryGetManifest(PackId, new string('0', 64), out _, out _));
        Assert.False(resolver.TryGetManifest("other.pack", hashHex, out _, out _));
    }

    [Fact]
    public async Task ClientDownloadsVerifiesAndCachesPackArt()
    {
        var packDirectory = CreatePackDirectory();
        var registry = RegisterPack(packDirectory);
        var assetPack = Assert.Single(registry.CreateReplicatedContent().AssetPacks!);
        var handler = new PackServerHandler(new ServerPluginPackFileResolver(registry));
        var service = new GameplayPackSyncService(new HttpClient(handler), Path.Combine(_root, "cache"));
        var serverBase = new Uri("http://127.0.0.1:8190/");

        var directory = await service.EnsureAvailableAsync(assetPack, serverBase, CancellationToken.None);

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(packDirectory, "assets", "ArtRedS", "1.png")),
            File.ReadAllBytes(Path.Combine(directory, "assets", "ArtRedS", "1.png")));
        Assert.True(service.TryGetCached(assetPack, out var cached));
        Assert.Equal(directory, cached);
        var requestsAfterFirstDownload = handler.RequestCount;

        Assert.Equal(directory, await service.EnsureAvailableAsync(assetPack, serverBase, CancellationToken.None));
        Assert.Equal(requestsAfterFirstDownload, handler.RequestCount);
    }

    [Fact]
    public async Task ClientRejectsTamperedPackArt()
    {
        var packDirectory = CreatePackDirectory();
        var registry = RegisterPack(packDirectory);
        var assetPack = Assert.Single(registry.CreateReplicatedContent().AssetPacks!);
        var handler = new PackServerHandler(new ServerPluginPackFileResolver(registry)) { TamperWith = "assets/ArtRedS/1.png" };
        var service = new GameplayPackSyncService(new HttpClient(handler), Path.Combine(_root, "cache"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.EnsureAvailableAsync(assetPack, new Uri("http://127.0.0.1:8190/"), CancellationToken.None));

        Assert.False(service.TryGetCached(assetPack, out var cacheDirectory));
        Assert.False(Directory.Exists(cacheDirectory));
    }

    private string CreatePackDirectory()
    {
        var packDirectory = Path.Combine(_root, "pack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "sprites"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "assets", "ArtRedS"));
        File.WriteAllText(Path.Combine(packDirectory, "pack.json"), $$"""{ "id": "{{PackId}}", "displayName": "Test Art", "version": "1.0.0" }""");
        File.WriteAllText(Path.Combine(packDirectory, "sprites", "ArtRedS.json"), """
            {
              "id": "ArtRedS",
              "framePaths": [ "assets/ArtRedS/0.png", "assets/ArtRedS/1.png" ],
              "originX": 30,
              "originY": 40,
              "mask": { "separate": false, "shape": "RECTANGLE", "boundsMode": "MANUAL", "left": 12, "top": 4, "right": 40, "bottom": 60 }
            }
            """);
        File.WriteAllBytes(Path.Combine(packDirectory, "assets", "ArtRedS", "0.png"), [137, 80, 78, 71, 0]);
        File.WriteAllBytes(Path.Combine(packDirectory, "assets", "ArtRedS", "1.png"), [137, 80, 78, 71, 1]);
        return packDirectory;
    }

    private static void AddSound(string packDirectory, string soundId, string soundPath)
    {
        Directory.CreateDirectory(Path.Combine(packDirectory, "sounds"));
        File.WriteAllText(
            Path.Combine(packDirectory, "sounds", "sound.json"),
            $$"""{ "id": "{{soundId}}", "path": "{{soundPath}}" }""");
        var soundFile = Path.Combine(packDirectory, "assets", "sounds", "hit.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(soundFile)!);
        File.WriteAllBytes(soundFile, [82, 73, 70, 70, 0]);
    }

    // Mirrors PluginHost: a plugin's manifest pack and its folder, owned by the plugin.
    private static GameplayRuntimeRegistry RegisterPack(string packDirectory)
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        using (registry.BeginRegistrationScope(OwnerKey))
        {
            Assert.True(
                registry.TryRegisterModPack(GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory), allowRuntimeClassBindingOverride: false, out var error),
                error);
            registry.RegisterPackDirectory(PackId, packDirectory);
        }

        return registry;
    }

    private static IEnumerable<OpenGarrison.GameplayModding.GameplaySpriteAssetDefinition> GetPackSprites(GameplayRuntimeRegistry registry)
    {
        return registry.ModPacks.Single(pack => pack.Id == PackId).Assets.Sprites.Values;
    }

    // Answers like ServerPluginPackDownloadEndpoint, from the same resolver.
    private sealed class PackServerHandler(ServerPluginPackFileResolver resolver) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string? TamperWith { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount += 1;
            var segments = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath)
                [GameplayPackAssetManifest.RoutePrefix.Length..]
                .Trim('/')
                .Split('/', 3);
            if (segments.Length == 3 && segments[2] == GameplayPackAssetManifest.ManifestFileName
                && resolver.TryGetManifest(segments[0], segments[1], out var manifest, out _))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(manifest.SerializeToUtf8Json()) });
            }

            if (segments.Length == 3 && resolver.TryResolveFile(segments[0], segments[1], segments[2], out var fullPath))
            {
                var bytes = File.ReadAllBytes(fullPath);
                if (segments[2] == TamperWith)
                {
                    bytes[^1] ^= 0xFF;
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
