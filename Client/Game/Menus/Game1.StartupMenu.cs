#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void UpdateStartupSplash(KeyboardState keyboard, MouseState mouse)
    {
        var skipRequested = keyboard.GetPressedKeys().Any(key => !_previousKeyboard.IsKeyDown(key))
            || IsAnyStartupMouseButtonPressed(mouse)
            || IsAnyStartupControllerButtonPressed();

        if (_brandIntroActive)
        {
            UpdateBrandIntro(skipRequested);
            return;
        }

        StopMenuMusic();
        StopIngameMusic();
        EnsureFaucetMusicPlaying();

        _startupSplashTicks += 1;
        if (_startupSplashTicks >= (int)Math.Round(30f * ClientUpdateTicksPerSecond / LegacyMovementModel.SourceTicksPerSecond)
            && _startupSplashFrame < 21f)
        {
            _startupSplashFrame = MathF.Min(
                21f,
                _startupSplashFrame + (0.2f * (LegacyMovementModel.SourceTicksPerSecond / ClientUpdateTicksPerSecond)));
        }

        var requiredSplashTicks = OperatingSystem.IsBrowser()
            ? 30
            : (int)Math.Round(240f * ClientUpdateTicksPerSecond / LegacyMovementModel.SourceTicksPerSecond);
        if (!_bootstrapController.IsMenuBootstrapComplete)
        {
            return;
        }

        if (skipRequested)
        {
            // Skipping Faucet advances to the SGG brand sequence. The brand
            // sequence has its own skip boundary and may then be skipped with
            // a second input.
            BeginBrandIntro();
            return;
        }

        if (_startupSplashTicks >= requiredSplashTicks)
        {
            BeginBrandIntro();
        }
    }

    private void EnsureStartupMenuBackgroundInitialized()
    {
        if (_brandIntroMenuBackgroundInitialized)
        {
            return;
        }

        _brandIntroMenuBackgroundInitialized = true;
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            _animatedMenuBackgroundController.Initialize(_menuBackgroundMode);
        }
    }

    private void BeginBrandIntro()
    {
        if (ClientDistribution.IsRestricted)
        {
            CompleteStartupIntro();
            return;
        }

        _brandIntroActive = true;
        _brandIntroElapsedSeconds = 0f;
        _brandIntroExitElapsedSeconds = -1f;
        _brandIntroBurstSoundPlayed = false;
        _brandLogoFlameTimeSeconds = 0f;
        StopFaucetMusic();
        StopMenuMusic();
        StopIngameMusic();
        EnsureBrandLogoAssets();
        EnsureStartupMenuBackgroundInitialized();
        _brandIntroMapBackgroundController?.Reset();
        _brandIntroMapBackgroundController = new AnimatedMenuBackgroundController(this);
        _brandIntroMapBackgroundController.InitializeSuperGangGarrisonShowcase();
    }

    private void UpdateBrandIntro(bool skipRequested)
    {
        StopFaucetMusic();
        StopIngameMusic();
        if (skipRequested && _bootstrapController.IsMenuBootstrapComplete)
        {
            BeginBrandIntroExit();
        }

        var deltaSeconds = 1f / MathF.Max(1f, ClientUpdateTicksPerSecond);
        if (_brandIntroExitElapsedSeconds < 0f)
        {
            // The arrival curves all clamp naturally, while the continuing
            // clock keeps the title prompt pulsing for as long as the player
            // chooses to remain on this screen.
            _brandIntroElapsedSeconds += deltaSeconds;
        }
        else
        {
            _brandIntroExitElapsedSeconds = MathF.Min(
                BrandIntroTimeline.ExitDurationSeconds,
                _brandIntroExitElapsedSeconds + deltaSeconds);
        }

        AdvanceBrandLogoFlame(deltaSeconds);
        if (_menuBackgroundMode != MenuBackgroundMode.Static)
        {
            _animatedMenuBackgroundController.Update(deltaSeconds);
        }
        _brandIntroMapBackgroundController?.Update(deltaSeconds);
        _menuBottomBarRunners.Update(deltaSeconds);

        var frame = BrandIntroTimeline.Evaluate(
            _brandIntroElapsedSeconds,
            _brandIntroExitElapsedSeconds,
            _brandIntroBurstSoundPlayed);
        if (frame.ShouldPlayBurstSound)
        {
            _brandIntroBurstSoundPlayed = true;
            PlayBrandIntroBurstSound();
        }

        if (frame.ShowcaseReveal > 0f || frame.IsExiting)
        {
            EnsureMenuMusicPlaying();
        }
        else
        {
            StopMenuMusic();
        }

        if (frame.IsComplete)
        {
            CompleteStartupIntro();
        }
    }

    private void BeginBrandIntroExit()
    {
        if (_brandIntroExitElapsedSeconds >= 0f)
        {
            return;
        }

        // A second input may skip the arrival animation, but it still lands on
        // the complete title composition before the flash and menu handoff.
        _brandIntroElapsedSeconds = MathF.Max(_brandIntroElapsedSeconds, BrandIntroTimeline.TitleReadySeconds);
        // Skipping before the explosion cue must not stack that cue on top of
        // the intentional uber-ready start sound.
        _brandIntroBurstSoundPlayed = true;
        _brandIntroExitElapsedSeconds = 0f;
        if (_runtimeAssets is not null)
        {
            TryPlaySound(_runtimeAssets.GetSound("UberChargedSnd"), 1f, 0f, 0f);
        }
    }

    private void PlayBrandIntroBurstSound()
    {
        if (_runtimeAssets is null)
        {
            return;
        }

        TryPlaySound(_runtimeAssets.GetSound("ShotgunSnd"), 0.95f, -0.05f, 0f);
        TryPlaySound(_runtimeAssets.GetSound("ExplosionSnd"), 1f, -0.08f, 0f);
    }

    private void CompleteStartupIntro()
    {
        EnsureStartupMenuBackgroundInitialized();
        _brandIntroMapBackgroundController?.Reset();
        _brandIntroMapBackgroundController = null;
        _brandIntroActive = false;
        _startupSplashOpen = false;
        _mainMenuOpen = true;
        StopFaucetMusic();
        StopIngameMusic();
        EnsureMenuMusicPlaying();
    }

    private void DrawStartupSplash()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;

        if (_brandIntroActive)
        {
            DrawBrandIntro(viewportWidth, viewportHeight);
            return;
        }

        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black);

        var sprite = GetResolvedSprite("FaucetLogoS");
        if (sprite is null || sprite.Frames.Count == 0)
        {
            DrawBitmapFontText("Faucet Software", new Vector2(viewportWidth / 2f - 120f, viewportHeight / 2f), Color.White, 2f);
            if (!_bootstrapController.IsMenuBootstrapComplete)
            {
                DrawBitmapFontText("Loading client assets...", new Vector2(viewportWidth / 2f - 150f, viewportHeight / 2f + 72f), Color.White * 0.85f, 1.2f);
            }

            return;
        }

        var frameIndex = Math.Clamp((int)MathF.Floor(_startupSplashFrame), 0, sprite.Frames.Count - 1);
        DrawLoadedSpriteFrame(
            sprite.Frames[frameIndex],
            new Vector2(viewportWidth / 2f, viewportHeight / 2f),
            null,
            Color.White,
            0f,
            sprite.Origin.ToVector2(),
            new Vector2(4f, 4f),
            SpriteEffects.None,
            0f);

        if (!_bootstrapController.IsMenuBootstrapComplete)
        {
            DrawBitmapFontText("Loading client assets...", new Vector2(viewportWidth / 2f - 150f, viewportHeight - 96f), Color.White * 0.85f, 1.2f);
        }
    }

    private void DrawBrandIntro(int viewportWidth, int viewportHeight)
    {
        var frame = BrandIntroTimeline.Evaluate(
            _brandIntroElapsedSeconds,
            _brandIntroExitElapsedSeconds,
            _brandIntroBurstSoundPlayed);

        // Render the destination menu first, then reveal it through the black
        // cover. Menu input remains blocked while StartupSplashOpen owns the
        // frame, and MenuController suppresses its own copy of the logo.
        _menuController.Draw();
        var showcaseAvailable = _brandIntroMapBackgroundController?.IsInitialized == true;
        var blackOpacity = frame.IsExiting
            ? showcaseAvailable ? 0f : 1f - frame.MenuReveal
            : 1f;
        if (blackOpacity > 0f)
        {
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(0, 0, viewportWidth, viewportHeight),
                Color.Black * blackOpacity);
        }

        var showcaseOpacity = frame.IsExiting
            ? 1f - frame.MenuReveal
            : frame.ShowcaseReveal;
        _brandIntroMapBackgroundController?.Draw(viewportWidth, viewportHeight, showcaseOpacity);

        DrawBrandIntroExplosion(viewportWidth, viewportHeight, frame);

        var centered = GetCenteredBrandLogoBounds(viewportWidth, viewportHeight);
        var burstBounds = ScaleBrandLogoBoundsAroundCenter(centered, frame.LogoBurstScale);
        var permanent = GetPermanentBrandLogoBounds(viewportWidth, viewportHeight);
        var logoBounds = InterpolateBrandLogoBounds(burstBounds, permanent, frame.CornerTransition);
        DrawFlamingBrandLogo(logoBounds, frame.FlameBlend, frame.LogoOpacity, frame.LogoFlash);

        if (frame.PromptOpacity > 0f)
        {
            var promptPosition = new Vector2(
                viewportWidth * 0.5f,
                MathF.Min(viewportHeight - 72f, centered.Bottom + MathF.Max(34f, viewportHeight * 0.045f)));
            var promptScale = Math.Clamp(viewportWidth / 720f, 1.45f, 2.25f);
            var shadowOffset = new Vector2(MathF.Max(2f, promptScale), MathF.Max(2f, promptScale));
            DrawBitmapFontTextCentered(
                "PRESS TO START",
                promptPosition + shadowOffset,
                Color.Black * (0.9f * frame.PromptOpacity),
                promptScale);
            DrawBitmapFontTextCentered(
                "PRESS TO START",
                promptPosition,
                Color.White * frame.PromptOpacity,
                promptScale);
        }
    }

    private bool IsAnyStartupMouseButtonPressed(MouseState mouse)
    {
        return mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed
            || mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed
            || mouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton != ButtonState.Pressed
            || mouse.XButton1 == ButtonState.Pressed && _previousMouse.XButton1 != ButtonState.Pressed
            || mouse.XButton2 == ButtonState.Pressed && _previousMouse.XButton2 != ButtonState.Pressed;
    }

    private bool IsAnyStartupControllerButtonPressed()
    {
        if (!_currentGamePad.IsConnected)
        {
            return false;
        }

        return ControllerActivityButtons.Any(button =>
                _currentGamePad.IsButtonDown(button) && !_previousGamePad.IsButtonDown(button))
            || _currentGamePad.Triggers.Left >= ControllerTriggerThreshold
                && _previousGamePad.Triggers.Left < ControllerTriggerThreshold
            || _currentGamePad.Triggers.Right >= ControllerTriggerThreshold
                && _previousGamePad.Triggers.Right < ControllerTriggerThreshold;
    }

    private void DrawBrandIntroExplosion(int viewportWidth, int viewportHeight, BrandIntroFrame frame)
    {
        if (frame.ExplosionOpacity <= 0f)
        {
            return;
        }

        var explosion = GetResolvedSprite("ExplosionS");
        if (explosion is null || explosion.Frames.Count == 0)
        {
            return;
        }

        var frameIndex = Math.Clamp(
            (int)MathF.Floor(frame.ExplosionProgress * explosion.Frames.Count),
            0,
            explosion.Frames.Count - 1);
        var diameter = MathF.Min(
            MathF.Min(viewportWidth, viewportHeight) * 0.72f,
            GetCenteredBrandLogoBounds(viewportWidth, viewportHeight).Width * 0.72f);
        diameter *= frame.ExplosionScale;
        var size = Math.Max(1, (int)MathF.Round(diameter));
        var destination = new Rectangle(
            (viewportWidth - size) / 2,
            (viewportHeight - size) / 2,
            size,
            size);
        DrawLoadedSpriteFrame(explosion.Frames[frameIndex], destination, Color.White * frame.ExplosionOpacity);
    }
}
