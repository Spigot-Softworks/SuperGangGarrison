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
        private DeferredContentBootstrapStage _deferredContentBootstrapStage;
        private bool _deferredContentBootstrapStarted;
        private bool _deferredContentBootstrapCompleted;
        private bool _initialized;
        private bool _contentLoaded;
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
            _context._menuImageFrame = _context._visualRandom.Next(2);
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
                        if (OperatingSystem.IsBrowser() && !_context._browserBootstrapAssetsApplied)
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

            if (_context._gameplayModAssets is not null && _context._runtimeComposition is not null)
            {
                return;
            }

            _context._gameplayModAssets ??= new GameplayModAssetCache(_context.GraphicsDevice);
            var gameplayModPacks = CharacterClassCatalog.RuntimeRegistry.ModPacks.ToArray();
            _context._runtimeComposition = new ClientRuntimeComposition(
                gameplayModPacks,
                GameplayPackSpriteAssetServiceRegistry.Create(gameplayModPacks));
            _context._gameplayModAssets.LoadRegisteredPacks(_context._runtimeComposition);
            _context._spriteFontOpaqueBoundsCache.Clear();
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
            _context._menuMusicInstance?.Dispose();
            _context._menuMusic?.Dispose();
            _context._lastToDieMenuMusicInstance?.Dispose();
            _context._lastToDieMenuMusic?.Dispose();
            _context._faucetMusicInstance?.Dispose();
            _context._faucetMusic?.Dispose();
            _context._ingameMusicInstance?.Dispose();
            _context._ingameMusic?.Dispose();
            _context._lastToDieIngameMusicInstance?.Dispose();
            _context._lastToDieIngameMusic?.Dispose();
            _context.StopHostedServer();
            _context._networkClient.Dispose();
            _context.LeavePeerRoom();
            _context.StopEmbeddedSession();
            _context.StopLocalJukebox();
            _context._gameplayModAssets?.Dispose();
            _context._runtimeAssets?.Dispose();
            _context._rotatedWeaponSprites?.Dispose();
            _context._rotatedWeaponSprites = null;
            foreach (var frame in _context._neutralSpriteFrameCache.Values)
            {
                frame.Dispose();
            }
            _context._neutralSpriteFrameCache.Clear();
            _context._browserAtlasTextureCache?.Dispose();
            _context._browserAtlasTextureCache = null;
            _context._browserBootstrapAtlasResolver = null;
            _context._runtimeComposition = null;
            _context._spriteFontOpaqueBoundsCache.Clear();
            _context._levelBackgroundFileTexture?.Dispose();
            _context._menuBackgroundTexture?.Dispose();
            _context._menuBitmapFontTexture?.Dispose();
            _context._menuPlaqueTexture?.Dispose();
            _context._menuPlaqueTallTexture?.Dispose();
            _context._menuTextBoxTopTexture?.Dispose();
            _context._menuTextBoxMiddleTexture?.Dispose();
            _context._menuTextBoxBottomTexture?.Dispose();
            _context._menuTextBoxSoloTexture?.Dispose();
            _context._lastToDieMenuPlaqueTexture?.Dispose();
            _context._lastToDieMenuTextBoxSoloTexture?.Dispose();
            _context._gameplayLoadoutClassStripTexture?.Dispose();
            _context._gameplayLoadoutClassSelectionTexture?.Dispose();
            _context._gameplayLoadoutBackgroundBarTexture?.Dispose();
            _context._gameplayLoadoutDescriptionBoardTexture?.Dispose();
            _context._gameplayLoadoutSelectionAtlasTexture?.Dispose();
            foreach (var chunk in _context._gameplayLoadoutSelectionAtlasChunks)
            {
                chunk.Dispose();
            }
            _context._gameplayLoadoutSelectionAtlasChunks.Clear();
            _context._gameplayLoadoutSelectionTexture?.Dispose();
            _context._gameplayLoadoutScrollerTexture?.Dispose();
            _context._gameplayLoadoutPageTexture?.Dispose();
            _context._gameplayLoadoutBackButtonTexture?.Dispose();
            _context._gameplayLoadoutHelmetTexture?.Dispose();
            _context._gameplayLoadoutDogTagsTexture?.Dispose();
            _context._lastToDieLogoTexture?.Dispose();
            _context.DisposeBrandLogoAssets();
            _context.DisposeLastToDieSurvivorCarouselAssets();
            _context.DisposeReplayPlaybackControlAssets();
            _context.DisposeLastToDieBuffIconFrame();
            _context.DisposeGameplayMissPopupFrame();
            _context.DisposeGarrisonBuilderEditorAssets();
            _context.UnloadCrtPresentation();
            _context._gameRenderTarget?.Dispose();
            _context._gameRenderTarget = null;
            _context._hudRenderTarget?.Dispose();
            _context._hudRenderTarget = null;
            _context.DisposeDamageVignetteTextures();
            _context._deathCamCaptureTarget?.Dispose();
            _context._deathCamCaptureTarget = null;
            _context._levelBackgroundFileTexture = null;
            _context._levelBackgroundFileTexturePath = null;
            _context._levelBackgroundFileFailedPath = null;
            _context._levelBackgroundFileTextureLevel = null;
            _context._menuBackgroundTexture = null;
            _context._menuBackgroundTexturePath = null;
            _context._menuBitmapFontTexture = null;
            _context._menuBitmapFontGlyphs.Clear();
            _context._menuBitmapFontLineHeight = 0;
            _context._menuPlaqueTexture = null;
            _context._menuPlaqueTallTexture = null;
            _context._menuTextBoxTopTexture = null;
            _context._menuTextBoxMiddleTexture = null;
            _context._menuTextBoxBottomTexture = null;
            _context._menuTextBoxSoloTexture = null;
            _context._lastToDieMenuPlaqueTexture = null;
            _context._lastToDieMenuTextBoxSoloTexture = null;
            _context._gameplayLoadoutClassStripTexture = null;
            _context._gameplayLoadoutClassSelectionTexture = null;
            _context._gameplayLoadoutBackgroundBarTexture = null;
            _context._gameplayLoadoutDescriptionBoardTexture = null;
            _context._gameplayLoadoutSelectionAtlasTexture = null;
            _context._gameplayLoadoutSelectionTexture = null;
            _context._gameplayLoadoutScrollerTexture = null;
            _context._gameplayLoadoutPageTexture = null;
            _context._gameplayLoadoutBackButtonTexture = null;
            _context._gameplayLoadoutHelmetTexture = null;
            _context._gameplayLoadoutDogTagsTexture = null;
            _context.PersistClientSettings();
            _context.PersistInputBindings();
            _initialized = false;
            _contentLoaded = false;
            _deferredContentBootstrapStarted = false;
            _deferredContentBootstrapCompleted = false;
            _deferredContentBootstrapStage = DeferredContentBootstrapStage.None;
        }
}
