#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private sealed class BootstrapController
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

        private readonly Game1 _game;
        private DeferredContentBootstrapStage _deferredContentBootstrapStage;
        private bool _deferredContentBootstrapStarted;
        private bool _deferredContentBootstrapCompleted;
        private bool _initialized;
        private bool _contentLoaded;
        private int _initializeCallCount;
        private int _loadContentCallCount;

        public BootstrapController(Game1 game)
        {
            _game = game;
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

            if (!_game.IsBrowserGameplayWarmupComplete())
            {
                reason = _game.GetBrowserGameplayWarmupStatusMessage();
                return false;
            }

            if (_game._runtimeAssets is null || _game._gameplayModAssets is null)
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
                _game.Window.TextInput += _game.OnWindowTextInput;
            }
            _game.Window.Title = _game._startupMode == GameStartupMode.ServerLauncher
                ? $"OG2.ServerLauncher - Proto (Protocol v{ProtocolVersion.Current})"
                : $"OG2 - Proto (Protocol v{ProtocolVersion.Current})";
            _game._menuImageFrame = _game._visualRandom.Next(2);
            _game._playerNameEditBuffer = _game._world.LocalPlayer.DisplayName;
            _game.AddConsoleLine("debug console ready (`)");
            _game.InitializeClientPlugins();
            if (_game._startupMode == GameStartupMode.ServerLauncher)
            {
                _game.InitializeServerLauncherMode();
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
            _game._spriteBatch = new SpriteBatch(_game.GraphicsDevice);
            _game._pixel = new Texture2D(_game.GraphicsDevice, 1, 1);
            _game._pixel.SetData(new[] { Color.White });
            _game._consoleFont = _game.LoadInitialSpriteFont("ConsoleFont");
            _game._menuFont = _game.LoadInitialSpriteFont("MenuFont");
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
                        if (OperatingSystem.IsBrowser() && !_game._browserBootstrapAssetsApplied)
                        {
                            return;
                        }

                        _game.LoadMenuPlaqueTextures();
                        _game.LoadMenuBitmapFont();
                        _game.LoadGameplayLoadoutMenuTextures();
                        if (OperatingSystem.IsBrowser())
                        {
                            EnsureGameplayCachesInitialized();
                            _game.BeginBrowserGameplayWarmup();
                            _deferredContentBootstrapStage = DeferredContentBootstrapStage.BrowserGameplayWarmup;
                            break;
                        }

                        _deferredContentBootstrapStage = DeferredContentBootstrapStage.Audio;
                        break;

                    case DeferredContentBootstrapStage.BrowserGameplayWarmup:
                        EnsureGameplayCachesInitialized();
                        if (!_game.AdvanceBrowserGameplayWarmup())
                        {
                            return;
                        }

                        _deferredContentBootstrapStage = DeferredContentBootstrapStage.Finalize;
                        break;

                    case DeferredContentBootstrapStage.Audio:
                        _game.LoadMenuMusic();
                        _game.LoadLastToDieMenuMusic();
                        _game.LoadFaucetMusic();
                        _game.LoadIngameMusic();
                        _game.LoadLastToDieIngameMusic();
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
                _game.AddConsoleLine($"content bootstrap failed: {ex.GetType().Name}: {ex.Message}");
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
            if (_game._runtimeAssets is null)
            {
                _game._runtimeAssets = new GameMakerRuntimeAssetCache(_game.GraphicsDevice, _game._assetManifest);
            }

            if (_game._gameplayModAssets is not null && _game._runtimeComposition is not null)
            {
                return;
            }

            _game._gameplayModAssets ??= new GameplayModAssetCache(_game.GraphicsDevice);
            var gameplayModPacks = CharacterClassCatalog.RuntimeRegistry.ModPacks.ToArray();
            _game._runtimeComposition = new ClientRuntimeComposition(
                gameplayModPacks,
                GameplayPackSpriteAssetServiceRegistry.Create(gameplayModPacks));
            _game._gameplayModAssets.LoadRegisteredPacks(_game._runtimeComposition);
            _game._spriteFontOpaqueBoundsCache.Clear();
        }

        private void FinalizeBootstrap()
        {
            _game.ApplyAudioMuteState();
            _game.AddConsoleLine($"gm assets sprites={_game._assetManifest.Sprites.Count} backgrounds={_game._assetManifest.Backgrounds.Count} sounds={_game._assetManifest.Sounds.Count}");
            _game.NotifyClientPluginsStarted();
            _deferredContentBootstrapStage = DeferredContentBootstrapStage.Complete;
            _deferredContentBootstrapCompleted = true;
        }

        public void UnloadContent()
        {
            if (!_initialized && !_contentLoaded)
            {
                return;
            }

            _game.ShutdownClientPlugins();
            _game._menuMusicInstance?.Dispose();
            _game._menuMusic?.Dispose();
            _game._lastToDieMenuMusicInstance?.Dispose();
            _game._lastToDieMenuMusic?.Dispose();
            _game._faucetMusicInstance?.Dispose();
            _game._faucetMusic?.Dispose();
            _game._ingameMusicInstance?.Dispose();
            _game._ingameMusic?.Dispose();
            _game._lastToDieIngameMusicInstance?.Dispose();
            _game._lastToDieIngameMusic?.Dispose();
            _game.StopHostedServer();
            _game._networkClient.Dispose();
            _game._gameplayModAssets?.Dispose();
            _game._runtimeAssets?.Dispose();
            _game._browserAtlasTextureCache?.Dispose();
            _game._browserAtlasTextureCache = null;
            _game._browserBootstrapAtlasResolver = null;
            _game._runtimeComposition = null;
            _game._spriteFontOpaqueBoundsCache.Clear();
            _game._menuBackgroundTexture?.Dispose();
            _game._menuBitmapFontTexture?.Dispose();
            _game._menuPlaqueTexture?.Dispose();
            _game._menuPlaqueTallTexture?.Dispose();
            _game._menuTextBoxTopTexture?.Dispose();
            _game._menuTextBoxMiddleTexture?.Dispose();
            _game._menuTextBoxBottomTexture?.Dispose();
            _game._menuTextBoxSoloTexture?.Dispose();
            _game._lastToDieMenuPlaqueTexture?.Dispose();
            _game._lastToDieMenuTextBoxSoloTexture?.Dispose();
            _game._gameplayLoadoutClassStripTexture?.Dispose();
            _game._gameplayLoadoutClassSelectionTexture?.Dispose();
            _game._gameplayLoadoutBackgroundBarTexture?.Dispose();
            _game._gameplayLoadoutDescriptionBoardTexture?.Dispose();
            _game._gameplayLoadoutSelectionAtlasTexture?.Dispose();
            foreach (var chunk in _game._gameplayLoadoutSelectionAtlasChunks)
            {
                chunk.Dispose();
            }
            _game._gameplayLoadoutSelectionAtlasChunks.Clear();
            _game._gameplayLoadoutSelectionTexture?.Dispose();
            _game._gameplayLoadoutScrollerTexture?.Dispose();
            _game._gameplayLoadoutPageTexture?.Dispose();
            _game._gameplayLoadoutBackButtonTexture?.Dispose();
            _game._gameplayLoadoutHelmetTexture?.Dispose();
            _game._gameplayLoadoutDogTagsTexture?.Dispose();
            _game._lastToDieLogoTexture?.Dispose();
            _game.DisposeGarrisonBuilderEditorAssets();
            _game._gameRenderTarget?.Dispose();
            _game._gameRenderTarget = null;
            _game._deathCamCaptureTarget?.Dispose();
            _game._deathCamCaptureTarget = null;
            _game._menuBackgroundTexture = null;
            _game._menuBackgroundTexturePath = null;
            _game._menuBitmapFontTexture = null;
            _game._menuBitmapFontGlyphs.Clear();
            _game._menuBitmapFontLineHeight = 0;
            _game._menuPlaqueTexture = null;
            _game._menuPlaqueTallTexture = null;
            _game._menuTextBoxTopTexture = null;
            _game._menuTextBoxMiddleTexture = null;
            _game._menuTextBoxBottomTexture = null;
            _game._menuTextBoxSoloTexture = null;
            _game._lastToDieMenuPlaqueTexture = null;
            _game._lastToDieMenuTextBoxSoloTexture = null;
            _game._gameplayLoadoutClassStripTexture = null;
            _game._gameplayLoadoutClassSelectionTexture = null;
            _game._gameplayLoadoutBackgroundBarTexture = null;
            _game._gameplayLoadoutDescriptionBoardTexture = null;
            _game._gameplayLoadoutSelectionAtlasTexture = null;
            _game._gameplayLoadoutSelectionTexture = null;
            _game._gameplayLoadoutScrollerTexture = null;
            _game._gameplayLoadoutPageTexture = null;
            _game._gameplayLoadoutBackButtonTexture = null;
            _game._gameplayLoadoutHelmetTexture = null;
            _game._gameplayLoadoutDogTagsTexture = null;
            _game.PersistClientSettings();
            _game.PersistInputBindings();
            _initialized = false;
            _contentLoaded = false;
            _deferredContentBootstrapStarted = false;
            _deferredContentBootstrapCompleted = false;
            _deferredContentBootstrapStage = DeferredContentBootstrapStage.None;
        }
    }
}
