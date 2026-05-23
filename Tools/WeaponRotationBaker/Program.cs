#nullable enable

// WeaponRotationBaker
// ───────────────────
// For every weapon sprite found under Core/Content/Sprites/Weapons/ this tool
// pre-bakes 24 rotated variants at 0.5× scale using nearest-neighbour sampling.
//
// Angles span –90° to +82.5° in 7.5° steps (24 steps total).  These cover
// all right-facing weapon orientations.  Left-facing is handled at runtime
// with a horizontal flip — the same pre-baked angle set is reused.
//
// Output structure:
//   Core/Content/Sprites/WeaponsRotated/
//     {SpriteName}/
//       frame{F:D2}.png   (F = frame index; a horizontal strip of AngleCount × D columns)
//       manifest.json     (canvasSize + origin positions per angle)
//     _master-manifest.json  (list of all baked sprite names)
//
// Excluded sprites (backstab, handled as plain left/right flip at runtime):
//   BackstabLegsS, SpyBlueBackstabTorsoS
//
// Usage:
//   dotnet run                                       (auto-detects project root)
//   dotnet run -- --input <weapons-dir> --output <output-dir>

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

// ── Configuration ────────────────────────────────────────────────────────────
const int AngleCount = 24;
const float AngleStartDeg = -90f;
const float AngleStepDeg = 180f / AngleCount; // = 7.5°

var JsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

// ── Argument parsing / root discovery ────────────────────────────────────────
var (inputWeaponsRoot, outputRoot) = ParseArgs(args);

if (inputWeaponsRoot is null)
    inputWeaponsRoot = FindWeaponsSpritesRoot();

if (inputWeaponsRoot is null || !Directory.Exists(inputWeaponsRoot))
{
    Console.Error.WriteLine("ERROR: Could not find the Sprites/Weapons directory.");
    Console.Error.WriteLine("Usage: WeaponRotationBaker [--input <weapons-dir>] [--output <output-dir>]");
    return 1;
}

if (outputRoot is null)
    outputRoot = Path.Combine(Path.GetDirectoryName(inputWeaponsRoot)!, "WeaponsRotated");

Console.WriteLine($"Input:  {inputWeaponsRoot}");
Console.WriteLine($"Output: {outputRoot}");
Console.WriteLine($"Angles: {AngleCount} × {AngleStepDeg}° ({AngleStartDeg}° … {AngleStartDeg + (AngleCount - 1) * AngleStepDeg}°)");
Console.WriteLine();

Directory.CreateDirectory(outputRoot);

// ── Discover sprite XML files ─────────────────────────────────────────────────
var xmlFiles = Directory
    .GetFiles(inputWeaponsRoot, "*.xml", SearchOption.AllDirectories)
    .Where(static p => !Path.GetFileName(p).StartsWith("_resources", StringComparison.OrdinalIgnoreCase))
    .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
    .ToArray();

Console.WriteLine($"Found {xmlFiles.Length} sprite definition(s).");
Console.WriteLine();

// ── Sprites that must not be baked (backstab uses plain flip at runtime) ─────
var ExcludedSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "BackstabLegsS",
    "SpyBlueBackstabTorsoS",
};

var spriteNames = new List<string>();

// ── Process each sprite ───────────────────────────────────────────────────────
foreach (var xmlPath in xmlFiles)
{
    var spriteName = Path.GetFileNameWithoutExtension(xmlPath);

    if (ExcludedSprites.Contains(spriteName))
    {
        Console.WriteLine($"  {spriteName} — skipped (backstab, no rotation needed)");
        continue;
    }

    var imagesDir  = Path.Combine(Path.GetDirectoryName(xmlPath)!, $"{spriteName}.images");

    if (!Directory.Exists(imagesDir))
        continue;

    var framePaths = Directory
        .GetFiles(imagesDir, "*.png", SearchOption.TopDirectoryOnly)
        .OrderBy(static p => ExtractTrailingNumber(Path.GetFileNameWithoutExtension(p)))
        .ThenBy(static p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (framePaths.Length == 0)
        continue;

    // Read origin from XML metadata
    var doc = XDocument.Load(xmlPath);
    var originEl   = doc.Root?.Element("origin");
    var fullOriginX = int.TryParse(originEl?.Attribute("x")?.Value, out var ox) ? ox : 0;
    var fullOriginY = int.TryParse(originEl?.Attribute("y")?.Value, out var oy) ? oy : 0;

    var spriteOutDir = Path.Combine(outputRoot, spriteName);
    Directory.CreateDirectory(spriteOutDir);

    Console.Write($"  {spriteName} ({framePaths.Length} frame(s))...");

    var frameManifests = new List<FrameManifest>(framePaths.Length);

    for (var frameIndex = 0; frameIndex < framePaths.Length; frameIndex++)
    {
        using var srcImage = Image.Load<Rgba32>(framePaths[frameIndex]);

        var fullW = srcImage.Width;
        var fullH = srcImage.Height;
        var halfW = Math.Max(1, fullW / 2);
        var halfH = Math.Max(1, fullH / 2);

        // ── 50% nearest-neighbour downscale (sample every other pixel) ────────
        using var halfImage = new Image<Rgba32>(halfW, halfH);
        for (var y = 0; y < halfH; y++)
        for (var x = 0; x < halfW; x++)
            halfImage[x, y] = srcImage[x * 2, y * 2];

        // ── Square canvas large enough to contain any rotation without clipping
        var diagonal = (int)Math.Ceiling(Math.Sqrt((double)halfW * halfW + (double)halfH * halfH));
        var D = diagonal % 2 == 0 ? diagonal : diagonal + 1; // keep even for symmetry
        if (D < 2) D = 2;

        var offsetX = (D - halfW) / 2;
        var offsetY = (D - halfH) / 2;

        using var canvas = new Image<Rgba32>(D, D); // all pixels transparent by default
        for (var y = 0; y < halfH; y++)
        for (var x = 0; x < halfW; x++)
            canvas[offsetX + x, offsetY + y] = halfImage[x, y];

        // ── Origin in canvas space (half-pixel units) ─────────────────────────
        var canvasOriginX = offsetX + fullOriginX / 2f;
        var canvasOriginY = offsetY + fullOriginY / 2f;
        var cx = D / 2f;
        var cy = D / 2f;

        var angleOrigins = new List<AngleOrigin>(AngleCount);

        // ── Horizontal strip: AngleCount columns of width D, all in one PNG ──
        using var strip = new Image<Rgba32>(D * AngleCount, D);

        for (var i = 0; i < AngleCount; i++)
        {
            var angleDeg = AngleStartDeg + i * AngleStepDeg;
            var angleRad = angleDeg * MathF.PI / 180f;
            var cosA     = MathF.Cos(angleRad);
            var sinA     = MathF.Sin(angleRad);

            // ── Nearest-neighbour inverse rotation into strip column i ────────
            // Convention: positive angle = clockwise on screen (Y-down, XNA/MonoGame).
            // Forward CW rotation:   x' = cx + dx*cos – dy*sin
            //                        y' = cy + dx*sin + dy*cos
            // Inverse (source lookup from output pixel):
            //                        sx = cx + rx*cos + ry*sin
            //                        sy = cy – rx*sin + ry*cos
            var stripX = i * D;
            for (var py = 0; py < D; py++)
            {
                for (var px = 0; px < D; px++)
                {
                    var rx = px - cx;
                    var ry = py - cy;
                    var sx = cx + rx * cosA + ry * sinA;
                    var sy = cy - rx * sinA + ry * cosA;
                    var ix = (int)MathF.Round(sx);
                    var iy = (int)MathF.Round(sy);
                    strip[stripX + px, py] = (ix >= 0 && ix < D && iy >= 0 && iy < D)
                        ? canvas[ix, iy]
                        : default; // transparent
                }
            }

            // ── Track where the origin lands after forward CW rotation ────────
            var dx = canvasOriginX - cx;
            var dy = canvasOriginY - cy;
            angleOrigins.Add(new AngleOrigin(
                OriginX: cx + dx * cosA - dy * sinA,
                OriginY: cy + dx * sinA + dy * cosA));
        }

        strip.SaveAsPng(Path.Combine(spriteOutDir, $"frame{frameIndex:D2}.png"));
        frameManifests.Add(new FrameManifest(CanvasSize: D, Angles: angleOrigins));
    }

    // ── Write per-sprite manifest ─────────────────────────────────────────────
    var manifest = new SpriteManifest(
        SpriteName:       spriteName,
        FrameCount:       framePaths.Length,
        AngleCount:       AngleCount,
        AngleStartDegrees: AngleStartDeg,
        AngleStepDegrees:  AngleStepDeg,
        Frames:            frameManifests);

    File.WriteAllText(
        Path.Combine(spriteOutDir, "manifest.json"),
        JsonSerializer.Serialize(manifest, JsonOptions));

    spriteNames.Add(spriteName);
    Console.WriteLine(" done.");
}

// ── Master manifest ───────────────────────────────────────────────────────────
File.WriteAllText(
    Path.Combine(outputRoot, "_master-manifest.json"),
    JsonSerializer.Serialize(
        new MasterManifest(AngleCount, AngleStartDeg, AngleStepDeg, spriteNames),
        JsonOptions));

Console.WriteLine();
Console.WriteLine($"Done. Baked {spriteNames.Count} sprite(s) → {outputRoot}");
return 0;

// ─── Helpers ──────────────────────────────────────────────────────────────────

static (string? input, string? output) ParseArgs(string[] args)
{
    string? input = null, output = null;
    for (var i = 0; i + 1 < args.Length; i++)
    {
        if (args[i] is "--input")  input  = args[i + 1];
        if (args[i] is "--output") output = args[i + 1];
    }
    return (input, output);
}

static string? FindWeaponsSpritesRoot()
{
    var current = AppContext.BaseDirectory;
    for (var depth = 0; depth < 12; depth++)
    {
        var candidate = Path.Combine(current, "Core", "Content", "Sprites", "Weapons");
        if (Directory.Exists(candidate)) return candidate;
        var parent = Path.GetDirectoryName(current);
        if (parent is null || parent == current) break;
        current = parent;
    }
    return null;
}

/// <summary>
/// Returns the trailing integer embedded in a filename (e.g. "image 3" → 3).
/// Files without a trailing number sort last.
/// </summary>
static int ExtractTrailingNumber(string name)
{
    var digits = new string(name.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
    return int.TryParse(digits, out var n) ? n : int.MaxValue;
}

// ─── Manifest record types ────────────────────────────────────────────────────

/// <summary>
/// The (x, y) position of the weapon origin within the baked D×D canvas,
/// in half-pixel units.  At runtime, pass this as the SpriteBatch origin
/// when drawing at 2× scale so the origin maps to the correct screen pixel.
/// </summary>
record AngleOrigin(
    [property: JsonPropertyName("originX")] float OriginX,
    [property: JsonPropertyName("originY")] float OriginY);

/// <summary>
/// Manifest data for one animation frame across all 24 baked angles.
/// </summary>
record FrameManifest(
    [property: JsonPropertyName("canvasSize")] int CanvasSize,
    [property: JsonPropertyName("angles")]     List<AngleOrigin> Angles);

/// <summary>
/// Full manifest for one sprite.  Lives at WeaponsRotated/{SpriteName}/manifest.json.
/// </summary>
record SpriteManifest(
    [property: JsonPropertyName("spriteName")]        string SpriteName,
    [property: JsonPropertyName("frameCount")]        int FrameCount,
    [property: JsonPropertyName("angleCount")]        int AngleCount,
    [property: JsonPropertyName("angleStartDegrees")] float AngleStartDegrees,
    [property: JsonPropertyName("angleStepDegrees")]  float AngleStepDegrees,
    [property: JsonPropertyName("frames")]            List<FrameManifest> Frames);

/// <summary>
/// Top-level manifest listing every baked sprite name.
/// Lives at WeaponsRotated/_master-manifest.json.
/// </summary>
record MasterManifest(
    [property: JsonPropertyName("angleCount")]        int AngleCount,
    [property: JsonPropertyName("angleStartDegrees")] float AngleStartDegrees,
    [property: JsonPropertyName("angleStepDegrees")]  float AngleStepDegrees,
    [property: JsonPropertyName("spriteNames")]       List<string> SpriteNames);
