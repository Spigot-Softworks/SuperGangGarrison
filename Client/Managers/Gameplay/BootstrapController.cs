#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class BootstrapController
    {
        private enum DeferredContentBootstrapStage
        {
            None,
            MenuAssets,
            BrowserGameplayWarmup,
            Audio,
            RuntimeAssets,
            GameplayModAssets,
            Finalize,
            Complete,
        }

        private readonly IGameplayContext _context;
        private OpenGarrison.ClientShared.ClientRuntimeComposition? _runtimeComposition;
        private DeferredContentBootstrapStage _deferredContentBootstrapStage;
        private bool _deferredContentBootstrapStarted;
        private bool _deferredContentBootstrapCompleted;
        private bool _initialized;
        private bool _contentLoaded;
        public GameplayLoadoutResources GameplayLoadoutResources { get; } = new();
        public MenuResources MenuResources { get; } = new();
        public LevelBackgroundResources LevelBackgroundResources { get; } = new();
        public LastToDieLogoResources LastToDieLogoResources { get; } = new();
        public BrowserBootstrapResources BrowserBootstrapResources { get; } = new();
        public SpriteFrameCacheResources SpriteFrameCacheResources { get; } = new();
        public RenderTargetResources RenderTargetResources { get; } = new();
        private int _initializeCallCount;
        private int _loadContentCallCount;

        public BootstrapController(IGameplayContext context)
        {
            _context = context;
        }

        public bool IsContentBootstrapComplete => !_deferredContentBootstrapStarted || _deferredContentBootstrapCompleted;

        public string DeferredContentBootstrapStageName => _deferredContentBootstrapStage.ToString();

        public bool IsInitialized => _initialized;

        public bool IsContentLoaded => _contentLoaded;

        public int InitializeCallCount => _initializeCallCount;

        public int LoadContentCallCount => _loadContentCallCount;

        public bool IsMenuBootstrapComplete
        {
            get
            {
                if (!OperatingSystem.IsBrowser())
                {
                    return IsContentBootstrapComplete;
                }

                if (!_deferredContentBootstrapStarted)
                {
                    return false;
                }

                return _deferredContentBootstrapCompleted
                    || _deferredContentBootstrapStage is not DeferredContentBootstrapStage.None
                        and not DeferredContentBootstrapStage.MenuAssets;
            }
        }

        public bool CanEnterGameplaySession(out string? reason)
        {
            if (!OperatingSystem.IsBrowser())
            {
                reason = null;
                return true;
            }

            if (!_context.IsBrowserGameplayWarmupComplete())
            {
                reason = _context.GetBrowserGameplayWarmupStatusMessage();
                return false;
            }

            if (_context._runtimeAssets is null || _context._gameplayModAssets is null)
            {
                reason = "Browser client assets are still loading. Please wait a moment and try again.";
                return false;
            }

            reason = null;
            return true;
        }

        public void Initialize()
        {
            _initializeCallCount += 1;
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (!OperatingSystem.IsBrowser())
            {
                _context.Window.TextInput += _context.OnWindowTextInput;
            }
            _context.Window.Title = WindowTitle;
            _context.MenuResources.ImageFrame = _context._visualRandom.Next(2);
            _context._playerNameEditBuffer = _context._world.LocalPlayer.DisplayName;
            _context.AddConsoleLine("debug console ready (`)");
            _context.InitializeClientPlugins();
            if (_context._startupMode == GameStartupMode.ServerLauncher)
            {
                _context.InitializeServerLauncherMode();
            }
        }

        public void LoadContent()
        {
            _loadContentCallCount += 1;
            if (_contentLoaded)
            {
                return;
            }

            _contentLoaded = true;
            _context._spriteBatch = new SpriteBatch(_context.GraphicsDevice);
            _context._pixel = new Texture2D(_context.GraphicsDevice, 1, 1);
            _context._pixel.SetData(new[] { Color.White });
            _context._consoleFont = _context.LoadInitialSpriteFont("ConsoleFont");
            _context._menuFont = _context.LoadInitialSpriteFont("MenuFont");
            _context._grayscaleEffect = _context.Content.Load<Effect>("Grayscale");
            StartDeferredContentBootstrap();
            if (!OperatingSystem.IsBrowser())
            {
                while (!IsContentBootstrapComplete)
                {
                    AdvanceDeferredContentBootstrap();
                }
            }
        }

        public void AdvanceDeferredContentBootstrap()
        {
            if (!_deferredContentBootstrapStarted || _deferredContentBootstrapCompleted)
            {
                return;
            }

            try
            {
                switch (_deferredContentBootstrapStage)
                {
                    case DeferredContentBootstrapStage.MenuAssets:
                        if (OperatingSystem.IsBrowser() && !BrowserBootstrapResources.AssetsApplied)
                        {
                            return;
                        }

                        _context.LoadMenuPlaqueTextures();
                        _context.LoadMenuBitmapFont();
                        _context.LoadGameplayLoadoutMenuTextures();
                        if (OperatingSystem.IsBrowser())
                        {
                            EnsureGameplayCachesInitialized();
                            _context.BeginBrowserGameplayWarmup();
                            _deferredContentBootstrapStage = DeferredContentBootstrapStage.BrowserGameplayWarmup;
                            break;
                        }

                        _deferredContentBootstrapStage = DeferredContentBootstrapStage.Audio;
                        break;

                    case DeferredContentBootstrapStage.BrowserGameplayWarmup:
                        EnsureGameplayCachesInitialized();
                        if (!_context.AdvanceBrowserGameplayWarmup())
                        {
                            return;
                        }

                        _deferredContentBootstrapStage = DeferredContentBootstrapStage.Finalize;
                        break;

                    case DeferredContentBootstrapStage.Audio:
                        _context.LoadMenuMusic();
                        _context.LoadLastToDieMenuMusic();
                        _context.LoadFaucetMusic();
                        _context.LoadIngameMusic();
                        _context.LoadLastToDieIngameMusic();
                        _deferredContentBootstrapStage = DeferredContentBootstrapStage.RuntimeAssets;
                        break;

                    case DeferredContentBootstrapStage.RuntimeAssets:
                        EnsureGameplayCachesInitialized();
                        _deferredContentBootstrapStage = DeferredContentBootstrapStage.GameplayModAssets;
                        break;

                    case DeferredContentBootstrapStage.GameplayModAssets:
                        EnsureGameplayCachesInitialized();
                        _deferredContentBootstrapStage = DeferredContentBootstrapStage.Finalize;
                        break;

                    case DeferredContentBootstrapStage.Finalize:
                        FinalizeBootstrap();
                        break;
                }
            }
            catch (Exception ex)
            {
                var failedStage = _deferredContentBootstrapStage;
                _deferredContentBootstrapCompleted = true;
                _deferredContentBootstrapStage = DeferredContentBootstrapStage.Complete;
                Console.WriteLine($"Browser/bootstrap failed at stage {failedStage}: {ex}");
                _context.AddConsoleLine($"content bootstrap failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void StartDeferredContentBootstrap()
        {
            if (_deferredContentBootstrapStarted)
            {
                return;
            }

            _deferredContentBootstrapStarted = true;
            _deferredContentBootstrapCompleted = false;
            _deferredContentBootstrapStage = DeferredContentBootstrapStage.MenuAssets;
        }

        private void EnsureGameplayCachesInitialized()
        {
            if (_context._runtimeAssets is null)
            {
                _context._runtimeAssets = new GameMakerRuntimeAssetCache(_context.GraphicsDevice, _context._assetManifest);
            }

            if (_context._rotatedWeaponSprites is null && !OperatingSystem.IsBrowser())
            {
                var rotatedRoot = _context._assetManifest.SourceRootPath is not null
                    ? System.IO.Path.Combine(_context._assetManifest.SourceRootPath, "Sprites", "WeaponsRotated")
                    : ContentRoot.GetPath("Sprites", "WeaponsRotated");
                if (System.IO.Directory.Exists(rotatedRoot))
                {
                    _context._rotatedWeaponSprites = new RotatedWeaponSpriteCache(_context.GraphicsDevice, rotatedRoot);
                }
            }

            if (_context._gameplayModAssets is not null && _runtimeComposition is not null)
            {
                return;
            }

            _context._gameplayModAssets ??= new GameplayModAssetCache(_context.GraphicsDevice);
            var gameplayModPacks = CharacterClassCatalog.RuntimeRegistry.ModPacks.ToArray();
            _runtimeComposition = new ClientRuntimeComposition(
                gameplayModPacks,
                GameplayPackSpriteAssetServiceRegistry.Create(gameplayModPacks));
            _context._gameplayModAssets.LoadRegisteredPacks(_runtimeComposition);
            SpriteFrameCacheResources.SpriteFontOpaqueBoundsCache.Clear();
        }

        private void FinalizeBootstrap()
        {
            _context.ApplyAudioMuteState();
            _context.AddConsoleLine($"gm assets sprites={_context._assetManifest.Sprites.Count} backgrounds={_context._assetManifest.Backgrounds.Count} sounds={_context._assetManifest.Sounds.Count}");
            _context.NotifyClientPluginsStarted();
            _deferredContentBootstrapStage = DeferredContentBootstrapStage.Complete;
            _deferredContentBootstrapCompleted = true;
        }

        public void UnloadContent()
        {
            if (!_initialized && !_contentLoaded)
            {
                return;
            }

            _context.ShutdownClientPlugins();
            _context.MusicResources.MenuMusicInstance?.Dispose();
            _context.MusicResources.MenuMusic?.Dispose();
            _context.MusicResources.LastToDieMenuMusicInstance?.Dispose();
            _context.MusicResources.LastToDieMenuMusic?.Dispose();
            _context.MusicResources.FaucetMusicInstance?.Dispose();
            _context.MusicResources.FaucetMusic?.Dispose();
            _context.MusicResources.IngameMusicInstance?.Dispose();
            _context.MusicResources.IngameMusic?.Dispose();
            _context.MusicResources.LastToDieIngameMusicInstance?.Dispose();
            _context.MusicResources.LastToDieIngameMusic?.Dispose();
            _context.StopHostedServer();
            _context._networkClient.Dispose();
            _context.LeavePeerRoom();
            _context.StopEmbeddedSession();
            _context.StopLocalJukebox();
            _context._gameplayModAssets?.Dispose();
            _context._runtimeAssets?.Dispose();
            _context._rotatedWeaponSprites?.Dispose();
            _context._rotatedWeaponSprites = null;
            foreach (var frame in SpriteFrameCacheResources.NeutralSpriteFrameCache.Values)
            {
                frame.Dispose();
            }
            SpriteFrameCacheResources.NeutralSpriteFrameCache.Clear();
            BrowserBootstrapResources.AtlasTextureCache?.Dispose();
            BrowserBootstrapResources.AtlasTextureCache = null;
            BrowserBootstrapResources.AtlasResolver = null;
            _runtimeComposition = null;
            SpriteFrameCacheResources.SpriteFontOpaqueBoundsCache.Clear();
            LevelBackgroundResources.Texture?.Dispose();
            _context.MenuResources.BackgroundTexture?.Dispose();
            _context.MenuResources.BitmapFontTexture?.Dispose();
            _context.MenuResources.PlaqueTexture?.Dispose();
            _context.MenuResources.PlaqueTallTexture?.Dispose();
            _context.MenuResources.TextBoxTopTexture?.Dispose();
            _context.MenuResources.TextBoxMiddleTexture?.Dispose();
            _context.MenuResources.TextBoxBottomTexture?.Dispose();
            _context.MenuResources.TextBoxSoloTexture?.Dispose();
            _context.MenuResources.LastToDieMenuPlaqueTexture?.Dispose();
            _context.MenuResources.LastToDieMenuTextBoxSoloTexture?.Dispose();
            _context.GameplayLoadoutResources.ClassStripTexture?.Dispose();
            _context.GameplayLoadoutResources.ClassSelectionTexture?.Dispose();
            _context.GameplayLoadoutResources.BackgroundBarTexture?.Dispose();
            _context.GameplayLoadoutResources.DescriptionBoardTexture?.Dispose();
            _context.GameplayLoadoutResources.SelectionAtlasTexture?.Dispose();
            foreach (var chunk in _context.GameplayLoadoutResources.SelectionAtlasChunks)
            {
                chunk.Dispose();
            }
            _context.GameplayLoadoutResources.SelectionAtlasChunks.Clear();
            _context.GameplayLoadoutResources.SelectionTexture?.Dispose();
            _context.GameplayLoadoutResources.ScrollerTexture?.Dispose();
            _context.GameplayLoadoutResources.PageTexture?.Dispose();
            _context.GameplayLoadoutResources.BackButtonTexture?.Dispose();
            _context.GameplayLoadoutResources.HelmetTexture?.Dispose();
            _context.GameplayLoadoutResources.DogTagsTexture?.Dispose();
            LastToDieLogoResources.Texture?.Dispose();
            _context.DisposeBrandLogoAssets();
            _context.DisposeLastToDieSurvivorCarouselAssets();
            _context.DisposeReplayPlaybackControlAssets();
            _context.DisposeLastToDieBuffIconFrame();
            _context.DisposeGameplayMissPopupFrame();
            _context.DisposeGarrisonBuilderEditorAssets();
            _context.UnloadCrtPresentation();
            RenderTargetResources.GameRenderTarget?.Dispose();
            RenderTargetResources.GameRenderTarget = null;
            RenderTargetResources.HudRenderTarget?.Dispose();
            RenderTargetResources.HudRenderTarget = null;
            RenderTargetResources.WorldPresentationTarget?.Dispose();
            RenderTargetResources.WorldPresentationTarget = null;
            _context.DisposeDamageVignetteTextures();
            RenderTargetResources.DeathCamCaptureTarget?.Dispose();
            RenderTargetResources.DeathCamCaptureTarget = null;
            LevelBackgroundResources.Texture = null;
            LevelBackgroundResources.TexturePath = null;
            LevelBackgroundResources.FailedPath = null;
            LevelBackgroundResources.TextureLevel = null;
            _context.MenuResources.BackgroundTexture = null;
            _context.MenuResources.BackgroundTexturePath = null;
            _context.MenuResources.BitmapFontTexture = null;
            _context.MenuResources.BitmapFontGlyphs.Clear();
            _context.MenuResources.BitmapFontLineHeight = 0;
            _context.MenuResources.PlaqueTexture = null;
            _context.MenuResources.PlaqueTallTexture = null;
            _context.MenuResources.TextBoxTopTexture = null;
            _context.MenuResources.TextBoxMiddleTexture = null;
            _context.MenuResources.TextBoxBottomTexture = null;
            _context.MenuResources.TextBoxSoloTexture = null;
            _context.MenuResources.LastToDieMenuPlaqueTexture = null;
            _context.MenuResources.LastToDieMenuTextBoxSoloTexture = null;
            _context.GameplayLoadoutResources.ClassStripTexture = null;
            _context.GameplayLoadoutResources.ClassSelectionTexture = null;
            _context.GameplayLoadoutResources.BackgroundBarTexture = null;
            _context.GameplayLoadoutResources.DescriptionBoardTexture = null;
            _context.GameplayLoadoutResources.SelectionAtlasTexture = null;
            _context.GameplayLoadoutResources.SelectionTexture = null;
            _context.GameplayLoadoutResources.ScrollerTexture = null;
            _context.GameplayLoadoutResources.PageTexture = null;
            _context.GameplayLoadoutResources.BackButtonTexture = null;
            _context.GameplayLoadoutResources.HelmetTexture = null;
            _context.GameplayLoadoutResources.DogTagsTexture = null;
            _context.PersistClientSettings();
            _context.PersistInputBindings();
            _initialized = false;
            _contentLoaded = false;
            _deferredContentBootstrapStarted = false;
            _deferredContentBootstrapCompleted = false;
            _deferredContentBootstrapStage = DeferredContentBootstrapStage.None;
        }
}
