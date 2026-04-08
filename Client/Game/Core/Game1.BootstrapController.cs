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
        private readonly Game1 _game;

        public BootstrapController(Game1 game)
        {
            _game = game;
        }

        public void Initialize()
        {
            _game.Window.TextInput += _game.OnWindowTextInput;
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
            _game._spriteBatch = new SpriteBatch(_game.GraphicsDevice);
            _game._pixel = new Texture2D(_game.GraphicsDevice, 1, 1);
            _game._pixel.SetData(new[] { Color.White });
            _game._consoleFont = _game.Content.Load<SpriteFont>("ConsoleFont");
            _game._menuFont = _game.Content.Load<SpriteFont>("MenuFont");
            _game._runtimeAssets = new GameMakerRuntimeAssetCache(_game.GraphicsDevice, _game._assetManifest);
            _game._gameplayModAssets = new GameplayModAssetCache(_game.GraphicsDevice);
            _game._gameplayModAssets.LoadRegisteredPacks(CharacterClassCatalog.RuntimeRegistry.ModPacks);
            _game._spriteFontOpaqueBoundsCache.Clear();
            _game.LoadMenuPlaqueTextures();
            _game.LoadGameplayLoadoutMenuTextures();
            _game.LoadMenuBitmapFont();
            _game.LoadMenuMusic();
            _game.LoadLastToDieMenuMusic();
            _game.LoadFaucetMusic();
            _game.LoadIngameMusic();
            _game.LoadLastToDieIngameMusic();
            _game.ApplyAudioMuteState();
            _game.AddConsoleLine($"gm assets sprites={_game._assetManifest.Sprites.Count} backgrounds={_game._assetManifest.Backgrounds.Count} sounds={_game._assetManifest.Sounds.Count}");
            _game.NotifyClientPluginsStarted();
        }

        public void UnloadContent()
        {
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
            _game._spriteFontOpaqueBoundsCache.Clear();
            _game._menuBackgroundTexture?.Dispose();
            _game._menuBitmapFontTexture?.Dispose();
            _game._menuPlaqueTexture?.Dispose();
            _game._menuPlaqueTallTexture?.Dispose();
            _game._menuTextBoxTopTexture?.Dispose();
            _game._menuTextBoxMiddleTexture?.Dispose();
            _game._menuTextBoxBottomTexture?.Dispose();
            _game._menuTextBoxSoloTexture?.Dispose();
            _game._gameplayLoadoutClassStripTexture?.Dispose();
            _game._gameplayLoadoutClassSelectionTexture?.Dispose();
            _game._gameplayLoadoutBackgroundBarTexture?.Dispose();
            _game._gameplayLoadoutDescriptionBoardTexture?.Dispose();
            _game._gameplayLoadoutSelectionAtlasTexture?.Dispose();
            _game._gameplayLoadoutSelectionTexture?.Dispose();
            _game._gameplayLoadoutScrollerTexture?.Dispose();
            _game._gameplayLoadoutPageTexture?.Dispose();
            _game._gameplayLoadoutBackButtonTexture?.Dispose();
            _game._lastToDieLogoTexture?.Dispose();
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
            _game._gameplayLoadoutClassStripTexture = null;
            _game._gameplayLoadoutClassSelectionTexture = null;
            _game._gameplayLoadoutBackgroundBarTexture = null;
            _game._gameplayLoadoutDescriptionBoardTexture = null;
            _game._gameplayLoadoutSelectionAtlasTexture = null;
            _game._gameplayLoadoutSelectionTexture = null;
            _game._gameplayLoadoutScrollerTexture = null;
            _game._gameplayLoadoutPageTexture = null;
            _game._gameplayLoadoutBackButtonTexture = null;
            _game.PersistClientSettings();
            _game.PersistInputBindings();
        }
    }
}
