#nullable enable

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client.Rendering.Crt;

/// <summary>
/// Applies the optional CRT presentation pass after the logical frame has been
/// composed. The ContentManager owns the effects; this renderer owns its targets.
/// </summary>
public sealed class CrtPresentationRenderer : IDisposable
{
#if BROWSER_KNI
    public CrtPresentationRenderer(GraphicsDevice device, ContentManager content, Action<string>? diagnostic = null)
    {
    }

    public bool IsActive => false;

    public bool CurvatureActive => false;

    public CrtQualityKind EffectiveQuality => CrtQualityKind.Balanced;

    public CrtSignalModeKind EffectiveSignalMode => CrtSignalModeKind.Preset;

    public string? FallbackReason => null;

    public bool Prepare(CrtPresetKind preset, CrtQualityKind quality, CrtSignalModeKind signalMode, bool curvatureEnabled,
        int brightnessPercent, int sourceWidth, int sourceHeight, Rectangle destination) => false;

    public bool Present(Texture2D source, Rectangle destination) => false;

    public Vector2 MapOutputToSource(Vector2 uv) => uv;

    public void Invalidate(bool releaseResources = true)
    {
    }

    public void Dispose()
    {
    }
#else
    private const long HighMemoryBudgetBytes = 96L * 1024L * 1024L;
    private const int MaximumHighTargetDimension = 4096;
    private const float HighlightKnee = 0.82f;
    private const float HighlightKneeRange = 0.18f;

    private static readonly RasterizerState CrtRasterizer = RasterizerState.CullNone;

    private readonly GraphicsDevice _device;
    private readonly ContentManager _content;
    private readonly Action<string>? _diagnostic;
    private PrepareKey? _cachedKey;
    private bool _prepared;
    private bool _disposed;
    private SpriteBatch? _crtSpriteBatch;
    private bool _balancedLoadAttempted;
    private bool _highLoadAttempted;
    private string? _balancedLoadFailure;
    private string? _highLoadFailure;
    private Effect? _balancedEffect;
    private Effect? _highEffect;
    private BalancedParameters? _balancedParameters;
    private HighParameters? _highParameters;
    private RenderTarget2D? _signalTarget;
    private RenderTarget2D? _emissionTarget;
    private RenderTarget2D? _glowTargetA;
    private RenderTarget2D? _glowTargetB;
    private CrtPresetDefinition _definition;
    private CrtPresentationMapping _mapping;
    private int _sourceWidth;
    private int _sourceHeight;
    private Rectangle _destination;
    private float _brightness;
    private float _maskStripeWidth;
    private float _maskResolution;
    private float _maskStrength;
    private CrtPresetKind _currentPreset;
    private CrtQualityKind _requestedQuality;
    private CrtSignalModeKind _requestedSignalMode;
    private CrtSignalModeKind _effectiveSignalMode;

    public CrtPresentationRenderer(GraphicsDevice device, ContentManager content, Action<string>? diagnostic = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _diagnostic = diagnostic;
    }

    public bool IsActive { get; private set; }

    public bool CurvatureActive { get; private set; }

    public CrtQualityKind EffectiveQuality { get; private set; } = CrtQualityKind.Balanced;

    public CrtSignalModeKind EffectiveSignalMode => _effectiveSignalMode;

    public string? FallbackReason { get; private set; }

    /// <summary>
    /// Converts a normalized point within the presentation rectangle to source
    /// coordinates using the exact geometry used by both CRT shaders.
    /// </summary>
    public Vector2 MapOutputToSource(Vector2 uv) => _mapping.MapOutputToSource(uv);

    public bool Prepare(CrtPresetKind preset, CrtQualityKind quality, CrtSignalModeKind signalMode, bool curvatureEnabled,
        int brightnessPercent, int sourceWidth, int sourceHeight, Rectangle destination)
    {
        ThrowIfDisposed();

        preset = NormalizePreset(preset);
        quality = NormalizeQuality(quality);
        signalMode = NormalizeSignalMode(signalMode);
        _currentPreset = preset;
        _requestedQuality = quality;
        _requestedSignalMode = signalMode;
        var key = new PrepareKey(preset, quality, signalMode, curvatureEnabled, brightnessPercent,
            sourceWidth, sourceHeight, destination);
        if (_prepared && _cachedKey is { } cached && cached.Equals(key))
        {
            return IsActive;
        }

        _prepared = true;
        _cachedKey = key;
        IsActive = false;
        CurvatureActive = false;
        FallbackReason = null;
        EffectiveQuality = CrtQualityKind.Balanced;

        if (preset == CrtPresetKind.Off)
        {
            UnbindOwnedTextures();
            ReleaseAllTargets();
            _sourceWidth = Math.Max(0, sourceWidth);
            _sourceHeight = Math.Max(0, sourceHeight);
            _destination = destination;
            _effectiveSignalMode = CrtSignalModeKind.Native;
            _mapping = new CrtPresentationMapping(Math.Max(1, destination.Width), Math.Max(1, destination.Height), 0f);
            return false;
        }

        if (sourceWidth <= 0 || sourceHeight <= 0 || destination.Width <= 0 || destination.Height <= 0)
        {
            UnbindOwnedTextures();
            ReleaseAllTargets();
            _sourceWidth = Math.Max(0, sourceWidth);
            _sourceHeight = Math.Max(0, sourceHeight);
            _destination = destination;
            _effectiveSignalMode = CrtSignalModeKind.Native;
            _mapping = new CrtPresentationMapping(Math.Max(1, destination.Width), Math.Max(1, destination.Height), 0f);
            FallbackReason = "The source or drawable presentation rectangle has no size.";
            return false;
        }

        _definition = CrtPresetDefinition.For(preset);
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _destination = destination;
        _effectiveSignalMode = ResolveSignalMode(preset, signalMode);
        _brightness = Math.Clamp(brightnessPercent, 75, 125) / 100f;
        _maskStripeWidth = _definition.GetMaskStripeWidth(destination.Width);
        _maskResolution = CrtPresetDefinition.GetMaskResolution(_maskStripeWidth);
        _maskStrength = _definition.MaskStrength;
        var curvature = _definition.GetCurvature(curvatureEnabled);
        _mapping = new CrtPresentationMapping(destination.Width, destination.Height, curvature);
        CurvatureActive = curvature > 0f;

        if (!TryLoadBalancedEffect(out var balancedFailure))
        {
            FallbackReason = balancedFailure ?? "Balanced CRT content is unavailable.";
            UnbindOwnedTextures();
            ReleaseAllTargets();
            ReportFallback(preset, quality, sourceWidth, sourceHeight, destination, FallbackReason);
            return false;
        }

        if (_effectiveSignalMode == CrtSignalModeKind.Classic)
        {
            if (!TryEnsureSignalTarget(sourceWidth, sourceHeight, out var signalFailure))
            {
                FallbackReason = signalFailure ?? "Classic CRT signal preparation is unavailable.";
                UnbindOwnedTextures();
                ReleaseAllTargets();
                ReportFallback(preset, quality, sourceWidth, sourceHeight, destination, FallbackReason);
                return false;
            }
        }
        else
        {
            UnbindOwnedTextures();
            ReleaseSignalTarget();
        }

        var wantsHigh = quality == CrtQualityKind.High
            || (quality == CrtQualityKind.Auto && _device.GraphicsProfile == GraphicsProfile.HiDef);
        if (quality == CrtQualityKind.Auto && !wantsHigh)
        {
            FallbackReason = "Auto selected Balanced for the active graphics profile.";
        }
        else if (wantsHigh)
        {
            if (_device.GraphicsProfile != GraphicsProfile.HiDef)
            {
                FallbackReason = "High quality requires the HiDef graphics profile; using Balanced.";
            }
            else if (!IsHighMemoryBudgetAvailable(
                         destination.Width,
                         destination.Height,
                         sourceWidth,
                         sourceHeight,
                         _effectiveSignalMode == CrtSignalModeKind.Classic))
            {
                FallbackReason = "High quality exceeds the CRT intermediate-target memory budget; using Balanced.";
            }
            else if (!TryLoadHighEffect(out var highFailure))
            {
                FallbackReason = highFailure ?? "High CRT content is unavailable; using Balanced.";
            }
            else if (!TryEnsureHighTargets(destination.Width, destination.Height, out var targetFailure))
            {
                FallbackReason = targetFailure ?? "High precision targets are unavailable; using Balanced.";
            }
            else
            {
                EffectiveQuality = CrtQualityKind.High;
            }
        }

        if (EffectiveQuality != CrtQualityKind.High)
        {
            UnbindOwnedTextures();
            ReleaseHighTargets();
        }

        IsActive = true;
        if (FallbackReason is not null)
        {
            ReportFallback(preset, quality, sourceWidth, sourceHeight, destination, FallbackReason);
        }
        return true;
    }

    /// <summary>
    /// Renders CRT output after the caller has ended its SpriteBatch and bound
    /// the backbuffer. A false return leaves no batch active or draw queued.
    /// </summary>
    public bool Present(Texture2D source, Rectangle destination)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        if (!IsActive || source.Width != _sourceWidth || source.Height != _sourceHeight
            || destination != _destination)
        {
            return false;
        }

        var savedState = CaptureDeviceState();
        try
        {
            UnbindOwnedTextures();
            var beamSource = source;
            if (_effectiveSignalMode == CrtSignalModeKind.Classic)
            {
                try
                {
                    DrawSignal(source);
                    beamSource = _signalTarget
                        ?? throw new InvalidOperationException("Classic CRT signal target was not prepared.");
                }
                catch (Exception exception) when (IsRecoverableRenderFailure(exception))
                {
                    FallbackReason = $"Classic CRT signal preparation failed ({exception.Message}).";
                    return FailPresentation(ref savedState, destination, FallbackReason!, releaseSignalTarget: true, releaseHighTargets: true);
                }
            }

            if (EffectiveQuality == CrtQualityKind.High)
            {
                try
                {
                    DrawHigh(beamSource, destination, savedState.Viewport);
                    return true;
                }
                catch (Exception exception) when (IsRecoverableRenderFailure(exception))
                {
                    var highFailure = $"High quality failed while rendering ({exception.Message}); using Balanced.";
                    DisposeCrtSpriteBatch();
                    ClearBackBuffer(savedState.Viewport);
                    savedState = RemoveReleasedTargetBindings(savedState, releaseSignalTarget: false, releaseHighTargets: true);
                    UnbindOwnedTextures();
                    ReleaseHighTargets();
                    EffectiveQuality = CrtQualityKind.Balanced;
                    SetFallbackReason(highFailure);
                }
            }

            try
            {
                DrawBalanced(beamSource, destination, savedState.Viewport);
                return true;
            }
            catch (Exception exception) when (IsRecoverableRenderFailure(exception))
            {
                FallbackReason = $"CRT rendering failed ({exception.Message}).";
                return FailPresentation(ref savedState, destination, FallbackReason!, releaseSignalTarget: true, releaseHighTargets: true);
            }
        }
        finally
        {
            RestoreDeviceState(savedState);
        }
    }

    private bool FailPresentation(ref DeviceState savedState, Rectangle destination, string reason,
        bool releaseSignalTarget, bool releaseHighTargets)
    {
        IsActive = false;
        FallbackReason = reason;
        DisposeCrtSpriteBatch();
        ClearBackBuffer(savedState.Viewport);
        savedState = RemoveReleasedTargetBindings(savedState, releaseSignalTarget, releaseHighTargets);
        UnbindOwnedTextures();
        if (releaseSignalTarget)
        {
            ReleaseSignalTarget();
        }

        if (releaseHighTargets)
        {
            ReleaseHighTargets();
        }

        ReportFallback(_currentPreset, _requestedQuality, _sourceWidth,
            _sourceHeight, destination, reason);
        return false;
    }

    private void ClearBackBuffer(Viewport viewport)
    {
        _device.SetRenderTarget(null);
        _device.Viewport = viewport;
        _device.ScissorRectangle = new Rectangle(viewport.X, viewport.Y, viewport.Width, viewport.Height);
        _device.RasterizerState = CrtRasterizer;
        _device.Clear(Color.Black);
    }

    /// <summary>Clears failure and size latches after a settings or device change.</summary>
    public void Invalidate(bool releaseResources = true)
    {
        ThrowIfDisposed();
        _prepared = false;
        _cachedKey = null;
        IsActive = false;
        CurvatureActive = false;
        FallbackReason = null;
        _balancedLoadAttempted = false;
        _highLoadAttempted = false;
        _balancedLoadFailure = null;
        _highLoadFailure = null;
        if (releaseResources)
        {
            UnbindOwnedTextures();
            ReleaseAllTargets();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnbindOwnedTextures();
        ReleaseAllTargets();
        DisposeCrtSpriteBatch();
        _balancedEffect = null;
        _highEffect = null;
        _balancedParameters = null;
        _highParameters = null;
        IsActive = false;
        _disposed = true;
    }

    private static CrtPresetKind NormalizePreset(CrtPresetKind preset) => preset switch
    {
        CrtPresetKind.Off or CrtPresetKind.PcMonitor or CrtPresetKind.StudioRgb
            or CrtPresetKind.ArcadeRgb or CrtPresetKind.HomeTvRgb => preset,
        _ => CrtPresetKind.Off,
    };

    private static CrtQualityKind NormalizeQuality(CrtQualityKind quality) => quality switch
    {
        CrtQualityKind.Auto or CrtQualityKind.Balanced or CrtQualityKind.High => quality,
        _ => CrtQualityKind.Auto,
    };

    private static CrtSignalModeKind NormalizeSignalMode(CrtSignalModeKind signalMode) => signalMode switch
    {
        CrtSignalModeKind.Preset or CrtSignalModeKind.Native or CrtSignalModeKind.Classic => signalMode,
        _ => CrtSignalModeKind.Preset,
    };

    private static CrtSignalModeKind ResolveSignalMode(CrtPresetKind preset, CrtSignalModeKind requestedMode)
    {
        return requestedMode switch
        {
            CrtSignalModeKind.Native => CrtSignalModeKind.Native,
            CrtSignalModeKind.Classic => CrtSignalModeKind.Classic,
            _ => preset == CrtPresetKind.PcMonitor
                ? CrtSignalModeKind.Native
                : CrtSignalModeKind.Classic,
        };
    }

    private bool TryLoadBalancedEffect(out string? failure)
    {
        failure = _balancedLoadFailure;
        if (_balancedEffect is not null)
        {
            return true;
        }

        if (_balancedLoadAttempted)
        {
            return false;
        }

        _balancedLoadAttempted = true;
        try
        {
            var effect = _content.Load<Effect>("CrtBalanced");
            var parameters = new BalancedParameters(effect);
            _balancedEffect = effect;
            _balancedParameters = parameters;
            return true;
        }
        catch (ContentLoadException exception)
        {
            _balancedLoadFailure = $"Balanced CRT content failed to load ({exception.Message}).";
            failure = _balancedLoadFailure;
            return false;
        }
        catch (IOException exception)
        {
            _balancedLoadFailure = $"Balanced CRT content could not be read ({exception.Message}).";
            failure = _balancedLoadFailure;
            return false;
        }
        catch (OutOfMemoryException exception)
        {
            _balancedLoadFailure = $"Balanced CRT content could not be allocated ({exception.Message}).";
            failure = _balancedLoadFailure;
            return false;
        }
        catch (NotSupportedException exception)
        {
            _balancedLoadFailure = $"Balanced CRT content is unsupported ({exception.Message}).";
            failure = _balancedLoadFailure;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            _balancedLoadFailure = $"Balanced CRT effect contract is invalid ({exception.Message}).";
            failure = _balancedLoadFailure;
            return false;
        }
        catch (ArgumentException exception)
        {
            _balancedLoadFailure = $"Balanced CRT effect contract is invalid ({exception.Message}).";
            failure = _balancedLoadFailure;
            return false;
        }
    }

    private bool TryLoadHighEffect(out string? failure)
    {
        failure = _highLoadFailure;
        if (_highEffect is not null)
        {
            return true;
        }

        if (_highLoadAttempted)
        {
            return false;
        }

        _highLoadAttempted = true;
        try
        {
            var effect = _content.Load<Effect>("CrtHigh");
            var parameters = new HighParameters(effect);
            _highEffect = effect;
            _highParameters = parameters;
            return true;
        }
        catch (ContentLoadException exception)
        {
            _highLoadFailure = $"High CRT content failed to load ({exception.Message}).";
            failure = _highLoadFailure;
            return false;
        }
        catch (IOException exception)
        {
            _highLoadFailure = $"High CRT content could not be read ({exception.Message}).";
            failure = _highLoadFailure;
            return false;
        }
        catch (OutOfMemoryException exception)
        {
            _highLoadFailure = $"High CRT content could not be allocated ({exception.Message}).";
            failure = _highLoadFailure;
            return false;
        }
        catch (NotSupportedException exception)
        {
            _highLoadFailure = $"High CRT content is unsupported ({exception.Message}).";
            failure = _highLoadFailure;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            _highLoadFailure = $"High CRT effect contract is invalid ({exception.Message}).";
            failure = _highLoadFailure;
            return false;
        }
        catch (ArgumentException exception)
        {
            _highLoadFailure = $"High CRT effect contract is invalid ({exception.Message}).";
            failure = _highLoadFailure;
            return false;
        }
    }

    private bool TryEnsureHighTargets(int width, int height, out string? failure)
    {
        failure = null;
        if (_emissionTarget is not null && _emissionTarget.Width == width && _emissionTarget.Height == height
            && _glowTargetA is not null && _glowTargetB is not null)
        {
            return true;
        }

        UnbindOwnedTextures();
        ReleaseHighTargets();
        var quarterWidth = Math.Max(1, width / 4);
        var quarterHeight = Math.Max(1, height / 4);
        try
        {
            _emissionTarget = CreateHalfTarget(width, height);
            _glowTargetA = CreateHalfTarget(quarterWidth, quarterHeight);
            _glowTargetB = CreateHalfTarget(quarterWidth, quarterHeight);
        }
        catch (Exception exception) when (IsRecoverableTargetFailure(exception))
        {
            ReleaseHighTargets();
            failure = $"HalfVector4 render targets are unavailable ({exception.Message}).";
            return false;
        }

        if (_emissionTarget.Format != SurfaceFormat.HalfVector4
            || _glowTargetA.Format != SurfaceFormat.HalfVector4
            || _glowTargetB.Format != SurfaceFormat.HalfVector4)
        {
            ReleaseHighTargets();
            failure = "The graphics device substituted a non-HalfVector4 CRT target format.";
            return false;
        }

        return true;
    }

    private bool TryEnsureSignalTarget(int sourceWidth, int sourceHeight, out string? failure)
    {
        failure = null;
        var signalWidth = Math.Max(1, sourceWidth / 2 + sourceWidth % 2);
        var signalHeight = Math.Max(1, sourceHeight / 2 + sourceHeight % 2);
        var signalBytes = (long)signalWidth * signalHeight * 4L;
        if (signalWidth > MaximumHighTargetDimension || signalHeight > MaximumHighTargetDimension
            || signalBytes > HighMemoryBudgetBytes)
        {
            failure = "The Classic half-size signal target exceeds the CRT intermediate-target memory budget.";
            return false;
        }

        if (_signalTarget is not null && _signalTarget.Width == signalWidth
            && _signalTarget.Height == signalHeight && _signalTarget.Format == SurfaceFormat.Color)
        {
            return true;
        }

        UnbindOwnedTextures();
        ReleaseSignalTarget();
        RenderTarget2D? target = null;
        try
        {
            target = CreateColorTarget(signalWidth, signalHeight);
            if (target.Format != SurfaceFormat.Color)
            {
                target.Dispose();
                failure = "The graphics device substituted a non-Color Classic signal target.";
                return false;
            }

            _signalTarget = target;
            return true;
        }
        catch (Exception exception) when (IsRecoverableTargetFailure(exception))
        {
            target?.Dispose();
            failure = $"The Classic half-size signal target is unavailable ({exception.Message}).";
            return false;
        }
    }

    private RenderTarget2D CreateHalfTarget(int width, int height)
    {
        return new RenderTarget2D(
            _device,
            width,
            height,
            mipMap: false,
            preferredFormat: SurfaceFormat.HalfVector4,
            preferredDepthFormat: DepthFormat.None,
            preferredMultiSampleCount: 0,
            usage: RenderTargetUsage.DiscardContents);
    }

    private RenderTarget2D CreateColorTarget(int width, int height)
    {
        return new RenderTarget2D(
            _device,
            width,
            height,
            mipMap: false,
            preferredFormat: SurfaceFormat.Color,
            preferredDepthFormat: DepthFormat.None,
            preferredMultiSampleCount: 0,
            usage: RenderTargetUsage.DiscardContents);
    }

    private static bool IsHighMemoryBudgetAvailable(int width, int height,
        int sourceWidth, int sourceHeight, bool includeSignalTarget)
    {
        if (width <= 0 || height <= 0 || width > MaximumHighTargetDimension || height > MaximumHighTargetDimension)
        {
            return false;
        }

        var fullBytes = (long)width * height * 8L;
        var quarterBytes = (long)Math.Max(1, width / 4) * Math.Max(1, height / 4) * 8L;
        var signalBytes = includeSignalTarget
            ? (long)Math.Max(1, sourceWidth / 2 + sourceWidth % 2)
                * Math.Max(1, sourceHeight / 2 + sourceHeight % 2) * 4L
            : 0L;
        return fullBytes + (2L * quarterBytes) + signalBytes <= HighMemoryBudgetBytes;
    }

    private void DrawSignal(Texture2D source)
    {
        var signalTarget = _signalTarget
            ?? throw new InvalidOperationException("Classic CRT signal target was not prepared.");
        var parameters = _balancedParameters!;
        var effect = _balancedEffect!;

        UnbindOwnedTextures();
        _device.SetRenderTarget(signalTarget);
        _device.Viewport = new Viewport(0, 0, signalTarget.Width, signalTarget.Height);
        _device.Clear(Color.Black);
        effect.CurrentTechnique = parameters.PrepareSignalTechnique;
        parameters.SourceTexture.SetValue(source);
        parameters.SourceSize.SetValue(new Vector2(source.Width, source.Height));
        DrawPass(GetCrtSpriteBatch(), source,
            new Rectangle(0, 0, signalTarget.Width, signalTarget.Height), effect, SamplerState.PointClamp);
    }

    private void DrawBalanced(Texture2D source, Rectangle destination, Viewport backBufferViewport)
    {
        ClearBackBuffer(backBufferViewport);
        var parameters = _balancedParameters!;
        var effect = _balancedEffect!;
        effect.CurrentTechnique = parameters.Technique;
        SetBalancedParameters(parameters, source, destination.Width, destination.Height);
        DrawPass(GetCrtSpriteBatch(), source, destination, effect, SamplerState.PointClamp);
    }

    private void DrawHigh(Texture2D source, Rectangle destination, Viewport backBufferViewport)
    {
        var emission = _emissionTarget!;
        var glowA = _glowTargetA!;
        var glowB = _glowTargetB!;
        var parameters = _highParameters!;
        var effect = _highEffect!;

        UnbindOwnedTextures();
        _device.SetRenderTarget(emission);
        _device.Viewport = new Viewport(0, 0, emission.Width, emission.Height);
        effect.CurrentTechnique = parameters.EmissionTechnique;
        SetHighCommonParameters(parameters, source, glowA, emission.Width, emission.Height);
        DrawPass(GetCrtSpriteBatch(), source, new Rectangle(0, 0, emission.Width, emission.Height), effect, SamplerState.PointClamp);

        UnbindOwnedTextures();
        _device.SetRenderTarget(glowA);
        _device.Viewport = new Viewport(0, 0, glowA.Width, glowA.Height);
        effect.CurrentTechnique = parameters.DownsampleTechnique;
        SetHighCommonParameters(parameters, emission, glowB, glowA.Width, glowA.Height);
        DrawPass(GetCrtSpriteBatch(), emission, new Rectangle(0, 0, glowA.Width, glowA.Height), effect, SamplerState.LinearClamp);

        UnbindOwnedTextures();
        _device.SetRenderTarget(glowB);
        _device.Viewport = new Viewport(0, 0, glowB.Width, glowB.Height);
        effect.CurrentTechnique = parameters.BlurHorizontalTechnique;
        SetHighCommonParameters(parameters, glowA, glowA, glowB.Width, glowB.Height);
        parameters.BlurDirection.SetValue(new Vector2(1f / glowA.Width, 0f));
        DrawPass(GetCrtSpriteBatch(), glowA, new Rectangle(0, 0, glowB.Width, glowB.Height), effect, SamplerState.LinearClamp);

        UnbindOwnedTextures();
        _device.SetRenderTarget(glowA);
        _device.Viewport = new Viewport(0, 0, glowA.Width, glowA.Height);
        effect.CurrentTechnique = parameters.BlurVerticalTechnique;
        SetHighCommonParameters(parameters, glowB, glowB, glowA.Width, glowA.Height);
        parameters.BlurDirection.SetValue(new Vector2(0f, 1f / glowB.Height));
        DrawPass(GetCrtSpriteBatch(), glowB, new Rectangle(0, 0, glowA.Width, glowA.Height), effect, SamplerState.LinearClamp);

        UnbindOwnedTextures();
        ClearBackBuffer(backBufferViewport);
        effect.CurrentTechnique = parameters.CompositeTechnique;
        SetHighCommonParameters(parameters, emission, glowA, destination.Width, destination.Height);
        DrawPass(GetCrtSpriteBatch(), emission, destination, effect, SamplerState.LinearClamp);
    }

    private void SetBalancedParameters(BalancedParameters parameters, Texture2D source, int width, int height)
    {
        parameters.SourceTexture.SetValue(source);
        parameters.SourceSize.SetValue(new Vector2(source.Width, source.Height));
        parameters.OutputSize.SetValue(new Vector2(width, height));
        parameters.AspectScale.SetValue(_mapping.AspectScale);
        parameters.Curvature.SetValue(_mapping.Curvature);
        parameters.Brightness.SetValue(_brightness);
        parameters.BaseBeamSigma.SetValue(_definition.BeamSigma);
        parameters.HorizontalFocus.SetValue(_definition.HorizontalFocus);
        parameters.BeamEnergy.SetValue(_definition.BeamEnergy);
        parameters.MaskKind.SetValue(_definition.MaskKind);
        parameters.MaskStrength.SetValue(_maskStrength);
        parameters.MaskAverageTransmission.SetValue(_definition.MaskAverageTransmission);
        parameters.MaskStripeWidth.SetValue(_maskStripeWidth);
        parameters.MaskResolution.SetValue(_maskResolution);
        parameters.MaskCellFillX.SetValue(_definition.MaskCellFillX);
        parameters.MaskCellFillY.SetValue(_definition.MaskCellFillY);
        parameters.MaskDarkTransmission.SetValue(_definition.MaskDarkTransmission);
        parameters.MaskRowPitchFactor.SetValue(_definition.MaskRowPitchFactor);
        parameters.GlowStrength.SetValue(_definition.GlowStrength);
    }

    private void SetHighCommonParameters(HighParameters parameters, Texture2D source,
        Texture2D bloom, int width, int height)
    {
        parameters.SourceTexture.SetValue(source);
        parameters.BloomTexture.SetValue(bloom);
        parameters.SourceSize.SetValue(new Vector2(source.Width, source.Height));
        parameters.OutputSize.SetValue(new Vector2(width, height));
        parameters.AspectScale.SetValue(_mapping.AspectScale);
        parameters.Curvature.SetValue(_mapping.Curvature);
        parameters.Brightness.SetValue(_brightness);
        parameters.BaseBeamSigma.SetValue(_definition.BeamSigma);
        parameters.HorizontalFocus.SetValue(_definition.HorizontalFocus);
        parameters.BeamEnergy.SetValue(_definition.BeamEnergy);
        parameters.MaskKind.SetValue(_definition.MaskKind);
        parameters.MaskStrength.SetValue(_maskStrength);
        parameters.MaskAverageTransmission.SetValue(_definition.MaskAverageTransmission);
        parameters.MaskStripeWidth.SetValue(_maskStripeWidth);
        parameters.MaskResolution.SetValue(_maskResolution);
        parameters.MaskCellFillX.SetValue(_definition.MaskCellFillX);
        parameters.MaskCellFillY.SetValue(_definition.MaskCellFillY);
        parameters.MaskDarkTransmission.SetValue(_definition.MaskDarkTransmission);
        parameters.MaskRowPitchFactor.SetValue(_definition.MaskRowPitchFactor);
        parameters.BloomStrength.SetValue(_definition.GlowStrength);
        parameters.TightGlowStrength.SetValue(_definition.TightGlowStrength);
        parameters.HighlightKnee.SetValue(HighlightKnee);
        parameters.HighlightKneeRange.SetValue(HighlightKneeRange);
        parameters.BlurDirection.SetValue(Vector2.Zero);
    }

    private static void DrawPass(SpriteBatch spriteBatch, Texture2D texture, Rectangle destination,
        Effect effect, SamplerState samplerState)
    {
        var batchStarted = false;
        try
        {
            spriteBatch.Begin(
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.Opaque,
                samplerState: samplerState,
                depthStencilState: DepthStencilState.None,
                rasterizerState: CrtRasterizer,
                effect: effect);
            batchStarted = true;
            spriteBatch.Draw(texture, destination, Color.White);
        }
        finally
        {
            if (batchStarted)
            {
                spriteBatch.End();
            }
        }
    }

    private DeviceState CaptureDeviceState()
    {
        return new DeviceState(
            _device.Viewport,
            _device.ScissorRectangle,
            _device.BlendState,
            _device.DepthStencilState,
            _device.RasterizerState,
            _device.SamplerStates[0],
            _device.SamplerStates[1],
            _device.SamplerStates[2],
            _device.Textures[0],
            _device.Textures[1],
            _device.Textures[2]);
    }

    private void RestoreDeviceState(DeviceState state)
    {
        _device.SetRenderTarget(null);
        _device.Viewport = state.Viewport;
        _device.ScissorRectangle = state.ScissorRectangle;
        _device.BlendState = state.BlendState;
        _device.DepthStencilState = state.DepthStencilState;
        _device.RasterizerState = state.RasterizerState;
        _device.SamplerStates[0] = state.Sampler0;
        _device.SamplerStates[1] = state.Sampler1;
        _device.SamplerStates[2] = state.Sampler2;
        _device.Textures[0] = state.Texture0;
        _device.Textures[1] = state.Texture1;
        _device.Textures[2] = state.Texture2;
    }

    private void SetFallbackReason(string reason)
    {
        if (FallbackReason == reason)
        {
            return;
        }

        FallbackReason = reason;
        _diagnostic?.Invoke(reason);
    }

    private void ReportFallback(CrtPresetKind preset, CrtQualityKind quality, int sourceWidth,
        int sourceHeight, Rectangle destination, string reason)
    {
        _diagnostic?.Invoke(
            $"CRT requested={preset}/{quality}, effective={(IsActive ? EffectiveQuality.ToString() : "Off")}, "
            + $"signal={_requestedSignalMode}/{_effectiveSignalMode}, "
            + $"source={sourceWidth}x{sourceHeight}, output={destination.Width}x{destination.Height}, "
            + $"profile={_device.GraphicsProfile}, format={_emissionTarget?.Format.ToString() ?? "Color"}; {reason}");
    }

    private static bool IsRecoverableTargetFailure(Exception exception) => exception is
        OutOfMemoryException or NotSupportedException or PlatformNotSupportedException or ArgumentException
        or InvalidOperationException;

    private static bool IsRecoverableRenderFailure(Exception exception) => exception is
        OutOfMemoryException or NotSupportedException or PlatformNotSupportedException
        or ContentLoadException or IOException or InvalidOperationException or ArgumentException;

    private SpriteBatch GetCrtSpriteBatch()
    {
        return _crtSpriteBatch ??= new SpriteBatch(_device);
    }

    private void DisposeCrtSpriteBatch()
    {
        var batch = _crtSpriteBatch;
        _crtSpriteBatch = null;
        batch?.Dispose();
    }

    private void UnbindOwnedTextures()
    {
        for (var slot = 0; slot < 3; slot++)
        {
            var texture = _device.Textures[slot];
            if (ReferenceEquals(texture, _signalTarget)
                || ReferenceEquals(texture, _emissionTarget)
                || ReferenceEquals(texture, _glowTargetA)
                || ReferenceEquals(texture, _glowTargetB))
            {
                _device.Textures[slot] = null;
            }
        }
    }

    private DeviceState RemoveReleasedTargetBindings(DeviceState state,
        bool releaseSignalTarget, bool releaseHighTargets)
    {
        bool IsReleasedTarget(Texture? texture)
        {
            return (releaseSignalTarget && ReferenceEquals(texture, _signalTarget))
                || (releaseHighTargets && (ReferenceEquals(texture, _emissionTarget)
                    || ReferenceEquals(texture, _glowTargetA)
                    || ReferenceEquals(texture, _glowTargetB)));
        }

        return state with
        {
            Texture0 = IsReleasedTarget(state.Texture0) ? null : state.Texture0,
            Texture1 = IsReleasedTarget(state.Texture1) ? null : state.Texture1,
            Texture2 = IsReleasedTarget(state.Texture2) ? null : state.Texture2,
        };
    }

    private void ReleaseSignalTarget()
    {
        UnbindOwnedTextures();
        _signalTarget?.Dispose();
        _signalTarget = null;
    }

    private void ReleaseHighTargets()
    {
        UnbindOwnedTextures();
        _emissionTarget?.Dispose();
        _glowTargetA?.Dispose();
        _glowTargetB?.Dispose();
        _emissionTarget = null;
        _glowTargetA = null;
        _glowTargetB = null;
    }

    private void ReleaseAllTargets()
    {
        ReleaseSignalTarget();
        ReleaseHighTargets();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private readonly record struct PrepareKey(
        CrtPresetKind Preset,
        CrtQualityKind Quality,
        CrtSignalModeKind SignalMode,
        bool CurvatureEnabled,
        int BrightnessPercent,
        int SourceWidth,
        int SourceHeight,
        Rectangle Destination);

    private readonly record struct DeviceState(
        Viewport Viewport,
        Rectangle ScissorRectangle,
        BlendState BlendState,
        DepthStencilState DepthStencilState,
        RasterizerState RasterizerState,
        SamplerState Sampler0,
        SamplerState Sampler1,
        SamplerState Sampler2,
        Texture? Texture0,
        Texture? Texture1,
        Texture? Texture2);

    private sealed class BalancedParameters
    {
        public BalancedParameters(Effect effect)
        {
            SourceTexture = Require(effect, "SourceTexture");
            SourceSize = Require(effect, "SourceSize");
            OutputSize = Require(effect, "OutputSize");
            AspectScale = Require(effect, "AspectScale");
            Curvature = Require(effect, "Curvature");
            Brightness = Require(effect, "Brightness");
            BaseBeamSigma = Require(effect, "BaseBeamSigma");
            HorizontalFocus = Require(effect, "HorizontalFocus");
            BeamEnergy = Require(effect, "BeamEnergy");
            MaskKind = Require(effect, "MaskKind");
            MaskStrength = Require(effect, "MaskStrength");
            MaskAverageTransmission = Require(effect, "MaskAverageTransmission");
            MaskStripeWidth = Require(effect, "MaskStripeWidth");
            MaskResolution = Require(effect, "MaskResolution");
            MaskCellFillX = Require(effect, "MaskCellFillX");
            MaskCellFillY = Require(effect, "MaskCellFillY");
            MaskDarkTransmission = Require(effect, "MaskDarkTransmission");
            MaskRowPitchFactor = Require(effect, "MaskRowPitchFactor");
            GlowStrength = Require(effect, "GlowStrength");
            Technique = RequireTechnique(effect, "Balanced");
            PrepareSignalTechnique = RequireTechnique(effect, "PrepareSignal");
        }

        public EffectParameter SourceTexture { get; }
        public EffectParameter SourceSize { get; }
        public EffectParameter OutputSize { get; }
        public EffectParameter AspectScale { get; }
        public EffectParameter Curvature { get; }
        public EffectParameter Brightness { get; }
        public EffectParameter BaseBeamSigma { get; }
        public EffectParameter HorizontalFocus { get; }
        public EffectParameter BeamEnergy { get; }
        public EffectParameter MaskKind { get; }
        public EffectParameter MaskStrength { get; }
        public EffectParameter MaskAverageTransmission { get; }
        public EffectParameter MaskStripeWidth { get; }
        public EffectParameter MaskResolution { get; }
        public EffectParameter MaskCellFillX { get; }
        public EffectParameter MaskCellFillY { get; }
        public EffectParameter MaskDarkTransmission { get; }
        public EffectParameter MaskRowPitchFactor { get; }
        public EffectParameter GlowStrength { get; }
        public EffectTechnique Technique { get; }
        public EffectTechnique PrepareSignalTechnique { get; }
    }

    private sealed class HighParameters
    {
        public HighParameters(Effect effect)
        {
            SourceTexture = Require(effect, "SourceTexture");
            BloomTexture = Require(effect, "BloomTexture");
            SourceSize = Require(effect, "SourceSize");
            OutputSize = Require(effect, "OutputSize");
            AspectScale = Require(effect, "AspectScale");
            Curvature = Require(effect, "Curvature");
            Brightness = Require(effect, "Brightness");
            BaseBeamSigma = Require(effect, "BaseBeamSigma");
            HorizontalFocus = Require(effect, "HorizontalFocus");
            BeamEnergy = Require(effect, "BeamEnergy");
            MaskKind = Require(effect, "MaskKind");
            MaskStrength = Require(effect, "MaskStrength");
            MaskAverageTransmission = Require(effect, "MaskAverageTransmission");
            MaskStripeWidth = Require(effect, "MaskStripeWidth");
            MaskResolution = Require(effect, "MaskResolution");
            MaskCellFillX = Require(effect, "MaskCellFillX");
            MaskCellFillY = Require(effect, "MaskCellFillY");
            MaskDarkTransmission = Require(effect, "MaskDarkTransmission");
            MaskRowPitchFactor = Require(effect, "MaskRowPitchFactor");
            BlurDirection = Require(effect, "BlurDirection");
            BloomStrength = Require(effect, "BloomStrength");
            TightGlowStrength = Require(effect, "TightGlowStrength");
            HighlightKnee = Require(effect, "HighlightKnee");
            HighlightKneeRange = Require(effect, "HighlightKneeRange");
            EmissionTechnique = RequireTechnique(effect, "Emission");
            DownsampleTechnique = RequireTechnique(effect, "Downsample");
            BlurHorizontalTechnique = RequireTechnique(effect, "BlurHorizontal");
            BlurVerticalTechnique = RequireTechnique(effect, "BlurVertical");
            CompositeTechnique = RequireTechnique(effect, "Composite");
        }

        public EffectParameter SourceTexture { get; }
        public EffectParameter BloomTexture { get; }
        public EffectParameter SourceSize { get; }
        public EffectParameter OutputSize { get; }
        public EffectParameter AspectScale { get; }
        public EffectParameter Curvature { get; }
        public EffectParameter Brightness { get; }
        public EffectParameter BaseBeamSigma { get; }
        public EffectParameter HorizontalFocus { get; }
        public EffectParameter BeamEnergy { get; }
        public EffectParameter MaskKind { get; }
        public EffectParameter MaskStrength { get; }
        public EffectParameter MaskAverageTransmission { get; }
        public EffectParameter MaskStripeWidth { get; }
        public EffectParameter MaskResolution { get; }
        public EffectParameter MaskCellFillX { get; }
        public EffectParameter MaskCellFillY { get; }
        public EffectParameter MaskDarkTransmission { get; }
        public EffectParameter MaskRowPitchFactor { get; }
        public EffectParameter BlurDirection { get; }
        public EffectParameter BloomStrength { get; }
        public EffectParameter TightGlowStrength { get; }
        public EffectParameter HighlightKnee { get; }
        public EffectParameter HighlightKneeRange { get; }
        public EffectTechnique EmissionTechnique { get; }
        public EffectTechnique DownsampleTechnique { get; }
        public EffectTechnique BlurHorizontalTechnique { get; }
        public EffectTechnique BlurVerticalTechnique { get; }
        public EffectTechnique CompositeTechnique { get; }
    }

    private static EffectParameter Require(Effect effect, string name)
    {
        return effect.Parameters[name]
            ?? throw new InvalidOperationException($"CRT effect is missing required parameter '{name}'.");
    }

    private static EffectTechnique RequireTechnique(Effect effect, string name)
    {
        return effect.Techniques[name]
            ?? throw new InvalidOperationException($"CRT effect is missing required technique '{name}'.");
    }
#endif
}
