#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int BrandLogoSourceWidth = 526;
    private const int BrandLogoSourceHeight = 166;
    private const float BrandLogoLegacyMenuWidth = 507f;
    private const float BrandLogoMenuPadding = 20f;

    private LoadedSpriteFrame? _brandLogoStaticFrame;
    private LoadedSpriteFrame? _brandLogoFlameRegionsFrame;
    private LoadedSpriteFrame? _brandLogoFlameOverlayFrame;
    private Effect? _brandLogoFlameEffect;
    private bool _brandLogoEffectLoadAttempted;
    private string _brandLogoAssetError = string.Empty;
    private float _brandLogoFlameTimeSeconds;
    private bool _brandIntroActive;
    private float _brandIntroElapsedSeconds;
    private float _brandIntroExitElapsedSeconds = -1f;
    private bool _brandIntroBurstSoundPlayed;
    private bool _brandIntroMenuBackgroundInitialized;
    private AnimatedMenuBackgroundController? _brandIntroMapBackgroundController;

    internal static Rectangle GetPermanentBrandLogoBounds(int viewportWidth, int viewportHeight)
    {
        var maximumWidth = MathF.Max(1f, viewportWidth - (BrandLogoMenuPadding * 2f));
        var targetWidth = MathF.Min(BrandLogoLegacyMenuWidth, viewportWidth * 0.38f);
        targetWidth = Math.Clamp(targetWidth, 1f, maximumWidth);
        var targetHeight = targetWidth * BrandLogoSourceHeight / BrandLogoSourceWidth;
        if (targetHeight > viewportHeight - (BrandLogoMenuPadding * 2f))
        {
            targetHeight = MathF.Max(1f, viewportHeight - (BrandLogoMenuPadding * 2f));
            targetWidth = targetHeight * BrandLogoSourceWidth / BrandLogoSourceHeight;
        }

        return new Rectangle(
            (int)MathF.Round(viewportWidth - targetWidth - BrandLogoMenuPadding),
            (int)MathF.Round(BrandLogoMenuPadding),
            Math.Max(1, (int)MathF.Round(targetWidth)),
            Math.Max(1, (int)MathF.Round(targetHeight)));
    }

    internal static Rectangle GetCenteredBrandLogoBounds(int viewportWidth, int viewportHeight)
    {
        var targetWidth = MathF.Min(940f, viewportWidth * 0.70f);
        var heightLimitedWidth = viewportHeight * 0.55f * BrandLogoSourceWidth / BrandLogoSourceHeight;
        targetWidth = MathF.Min(targetWidth, heightLimitedWidth);
        targetWidth = MathF.Max(1f, targetWidth);
        var targetHeight = targetWidth * BrandLogoSourceHeight / BrandLogoSourceWidth;
        return new Rectangle(
            (int)MathF.Round((viewportWidth - targetWidth) * 0.5f),
            (int)MathF.Round((viewportHeight - targetHeight) * 0.5f),
            Math.Max(1, (int)MathF.Round(targetWidth)),
            Math.Max(1, (int)MathF.Round(targetHeight)));
    }

    internal static Rectangle InterpolateBrandLogoBounds(Rectangle from, Rectangle to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new Rectangle(
            (int)MathF.Round(MathHelper.Lerp(from.X, to.X, amount)),
            (int)MathF.Round(MathHelper.Lerp(from.Y, to.Y, amount)),
            Math.Max(1, (int)MathF.Round(MathHelper.Lerp(from.Width, to.Width, amount))),
            Math.Max(1, (int)MathF.Round(MathHelper.Lerp(from.Height, to.Height, amount))));
    }

    private static Rectangle ScaleBrandLogoBoundsAroundCenter(Rectangle bounds, float scale)
    {
        scale = MathF.Max(0.01f, scale);
        var width = Math.Max(1, (int)MathF.Round(bounds.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(bounds.Height * scale));
        return new Rectangle(
            bounds.Center.X - (width / 2),
            bounds.Center.Y - (height / 2),
            width,
            height);
    }

    private void AdvanceBrandLogoFlame(float elapsedSeconds)
    {
        if (float.IsFinite(elapsedSeconds) && elapsedSeconds > 0f)
        {
            _brandLogoFlameTimeSeconds = (_brandLogoFlameTimeSeconds + elapsedSeconds) % 4096f;
        }
    }

    private bool DrawFlamingBrandLogo(Rectangle destination, float flameBlend = 1f, float opacity = 1f, float flashAmount = 0f)
    {
        EnsureBrandLogoAssets();
        if (_brandLogoStaticFrame is null || destination.Width <= 0 || destination.Height <= 0 || opacity <= 0f)
        {
            return false;
        }

        flameBlend = Math.Clamp(flameBlend, 0f, 1f);
        opacity = Math.Clamp(opacity, 0f, 1f);
        if (_brandLogoFlameEffect is null
            || _brandLogoFlameRegionsFrame is null
            || _brandLogoFlameOverlayFrame is null
            || _brandLogoFlameRegionsFrame.Width != _brandLogoStaticFrame.Width
            || _brandLogoFlameRegionsFrame.Height != _brandLogoStaticFrame.Height
            || _brandLogoFlameOverlayFrame.Width != _brandLogoStaticFrame.Width
            || _brandLogoFlameOverlayFrame.Height != _brandLogoStaticFrame.Height)
        {
            DrawLoadedSpriteFrame(_brandLogoStaticFrame, destination, Color.White * opacity);
            return true;
        }

        ConfigureBrandLogoEffect(flameBlend, flashAmount);
        _spriteBatch.End();
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            effect: _brandLogoFlameEffect,
            rasterizerState: RasterizerState.CullNone);
        DrawLoadedSpriteFrame(_brandLogoStaticFrame, destination, Color.White * opacity);
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
        return true;
    }

    private void ConfigureBrandLogoEffect(float flameBlend, float flashAmount)
    {
        var staticFrame = _brandLogoStaticFrame!;
        var regionsFrame = _brandLogoFlameRegionsFrame!;
        var overlayFrame = _brandLogoFlameOverlayFrame!;
        var effect = _brandLogoFlameEffect!;
        effect.Parameters["FlameRegionTexture"]?.SetValue(regionsFrame.Texture);
        effect.Parameters["FlameOverlayTexture"]?.SetValue(overlayFrame.Texture);
        effect.Parameters["FlameTime"]?.SetValue(_brandLogoFlameTimeSeconds);
        effect.Parameters["FlameBlend"]?.SetValue(flameBlend);
        effect.Parameters["LogoFlashAmount"]?.SetValue(Math.Clamp(flashAmount, 0f, 1f));
        effect.Parameters["LogoTextureSize"]?.SetValue(new Vector2(staticFrame.Width, staticFrame.Height));
        SetBrandLogoUvTransform(effect, "Static", staticFrame);
        SetBrandLogoUvTransform(effect, "Region", regionsFrame);
        SetBrandLogoUvTransform(effect, "Overlay", overlayFrame);
    }

    private static void SetBrandLogoUvTransform(Effect effect, string prefix, LoadedSpriteFrame frame)
    {
        var source = frame.SourceRectangle ?? new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height);
        effect.Parameters[$"{prefix}UvOffset"]?.SetValue(new Vector2(
            source.X / (float)Math.Max(1, frame.Texture.Width),
            source.Y / (float)Math.Max(1, frame.Texture.Height)));
        effect.Parameters[$"{prefix}UvScale"]?.SetValue(new Vector2(
            source.Width / (float)Math.Max(1, frame.Texture.Width),
            source.Height / (float)Math.Max(1, frame.Texture.Height)));
    }

    private void EnsureBrandLogoAssets()
    {
        var directory = ContentRoot.GetPath("Sprites", "Menu", "Title", "OpenGarrisonLogo");
        if (_brandLogoStaticFrame is null)
        {
            _brandLogoStaticFrame = TryLoadBrandLogoFrame(directory, "logo-static.png");
        }
        if (_brandLogoFlameRegionsFrame is null)
        {
            _brandLogoFlameRegionsFrame = TryLoadBrandLogoFrame(directory, "logo-flame-regions.png");
        }
        if (_brandLogoFlameOverlayFrame is null)
        {
            _brandLogoFlameOverlayFrame = TryLoadBrandLogoFrame(directory, "logo-flame-overlay.png");
        }

        if (_brandLogoFlameEffect is null && !_brandLogoEffectLoadAttempted)
        {
            _brandLogoEffectLoadAttempted = true;
            try
            {
                _brandLogoFlameEffect = Content.Load<Effect>("FlamingLogo");
            }
            catch (Exception ex)
            {
                RecordBrandLogoAssetError($"flame effect unavailable ({ex.GetType().Name}: {ex.Message})");
            }
        }
    }

    private LoadedSpriteFrame? TryLoadBrandLogoFrame(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!CanLoadSpriteFrameFromPath(path))
        {
            return null;
        }

        try
        {
            var frame = LoadSpriteFrameFromPath(path);
            if (frame is null || frame.SourceRectangle is null)
            {
                return frame;
            }

            // The flaming-logo effect samples the three logo layers as separate
            // textures. Atlas-backed frames all share one large page, so passing
            // the page texture plus source rectangles through a multi-texture
            // effect lets a driver/shader path sample neighboring atlas regions.
            // Detach these small layers before binding them to the effect so all
            // shader UVs are local 0..1 coordinates.
            var sourceRectangle = frame.SourceRectangle.Value;
            var pixels = new Color[sourceRectangle.Width * sourceRectangle.Height];
            if (!frame.TryCopyPixelData(pixels))
            {
                return frame;
            }

            var texture = new Texture2D(GraphicsDevice, sourceRectangle.Width, sourceRectangle.Height);
            texture.SetData(pixels);
            var detachedFrame = new LoadedSpriteFrame(
                texture,
                OpaqueBounds: frame.OpaqueBounds,
                PixelSource: new LoadedSpriteFramePixelSource(
                    pixels,
                    sourceRectangle.Width,
                    sourceRectangle.Height));
            frame.Dispose();
            return detachedFrame;
        }
        catch (Exception ex)
        {
            RecordBrandLogoAssetError($"{fileName} unavailable ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }

    private void RecordBrandLogoAssetError(string message)
    {
        if (string.Equals(_brandLogoAssetError, message, StringComparison.Ordinal))
        {
            return;
        }

        _brandLogoAssetError = message;
        AddConsoleLine($"brand logo fallback: {message}");
    }

    private void DisposeBrandLogoAssets()
    {
        _brandLogoStaticFrame?.Dispose();
        _brandLogoFlameRegionsFrame?.Dispose();
        _brandLogoFlameOverlayFrame?.Dispose();
        _brandLogoStaticFrame = null;
        _brandLogoFlameRegionsFrame = null;
        _brandLogoFlameOverlayFrame = null;
        _brandLogoFlameEffect = null;
        _brandLogoEffectLoadAttempted = false;
        _brandLogoAssetError = string.Empty;
        _brandIntroActive = false;
        _brandIntroElapsedSeconds = 0f;
        _brandIntroExitElapsedSeconds = -1f;
        _brandIntroBurstSoundPlayed = false;
        _brandIntroMenuBackgroundInitialized = false;
        _brandIntroMapBackgroundController?.Reset();
        _brandIntroMapBackgroundController = null;
        _brandLogoFlameTimeSeconds = 0f;
    }
}
