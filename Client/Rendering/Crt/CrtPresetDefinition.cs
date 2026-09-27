#nullable enable

using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client.Rendering.Crt;

/// <summary>
/// Provisional display-family starting points. Values are design targets, not
/// measurements or claims about a named tube. Nominal triad counts apply at a
/// wide presentation raster; smaller windows coarsen the pattern to preserve
/// a minimum number of output pixels per triad instead of fading the mask away.
/// </summary>
internal readonly record struct CrtPresetDefinition(
    float BeamSigma,
    float HorizontalFocus,
    float BeamEnergy,
    int MaskKind,
    float MaskStrength,
    float TriadsAcrossScreen,
    float MinimumTriadPitch,
    float MaskCellFillX,
    float MaskCellFillY,
    float MaskDarkTransmission,
    float MaskRowPitchFactor,
    float GlowStrength,
    float TightGlowStrength,
    float Curvature)
{
    /// <summary>Mean white-field mask transmission before strength blending.</summary>
    public float MaskAverageTransmission
    {
        get
        {
            var rowFill = MaskKind == 2 ? 1f : MaskCellFillY;
            return MaskDarkTransmission
                + (1f - MaskDarkTransmission) * (MaskCellFillX / 3f) * rowFill;
        }
    }

    public static CrtPresetDefinition For(CrtPresetKind preset) => preset switch
    {
        // Offset, rectangular shadow-mask cells: visible gaps, focused horizontal
        // reconstruction, and a compact light skirt for small text and pixel art.
        CrtPresetKind.PcMonitor => new(
            0.250f, 0.42f, 1f, 1, 0.78f, 560f, 3.0f,
            0.78f, 0.68f, 0.11f, 1.00f, 0.012f, 0.16f, 0.012f),

        // Continuous aperture-grille columns with a narrow, focused beam.
        CrtPresetKind.StudioRgb => new(
            0.208f, 0.36f, 1f, 2, 0.52f, 440f, 3.5f,
            0.82f, 1.00f, 0.10f, 1.00f, 0.008f, 0.11f, 0.008f),

        // Coarser staggered slots are almost twice as tall as they are wide.
        // The local phosphor spread is strongest; broad diffusion stays restrained.
        CrtPresetKind.ArcadeRgb => new(
            0.283f, 0.52f, 1f, 3, 0.72f, 300f, 4.5f,
            0.88f, 0.72f, 0.08f, 2.40f, 0.035f, 0.22f, 0.020f),

        // Softer large shadow cells use a quarter-triad row offset and lower fill;
        // this is a rectangular-cell approximation rather than a round-dot mask.
        CrtPresetKind.HomeTvRgb => new(
            0.333f, 0.68f, 1f, 4, 0.70f, 300f, 5.5f,
            0.70f, 0.62f, 0.15f, 1.25f, 0.025f, 0.20f, 0.032f),

        _ => default,
    };

    public float GetMaskStripeWidth(int outputWidth)
    {
        if (outputWidth <= 0)
        {
            return 1f;
        }

        var nominalTriads = Math.Max(1f, TriadsAcrossScreen);
        var minimumPitch = Math.Max(1f, MinimumTriadPitch);
        var countAtMinimumPitch = outputWidth / minimumPitch;
        var triadCount = Math.Max(1f, Math.Min(nominalTriads, countAtMinimumPitch));
        return outputWidth / (triadCount * 3f);
    }

    public float GetCurvature(bool enabled) => enabled ? Curvature : 0f;

    public static float GetMaskResolution(float stripeWidth)
    {
        // Keep masks fully present at the configured minimum triad pitches;
        // only unresolved subpixel stripes are attenuated.
        var t = Math.Clamp((stripeWidth - 0.50f) / (0.90f - 0.50f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
