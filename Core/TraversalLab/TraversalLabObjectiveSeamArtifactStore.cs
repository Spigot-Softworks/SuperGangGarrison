using System.Text.Json;

namespace OpenGarrison.Core;

public static class TraversalLabObjectiveSeamArtifactStore
{
    private static readonly object Sync = new();

    private static string? _loadedPath;

    private static Dictionary<string, TraversalLabObjectiveSeamCertification>? _certificationsByLabel;

    private static Dictionary<string, string[]>? _successorLabelsByLabel;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static bool TryGetCertification(string label, out TraversalLabObjectiveSeamCertification certification)
    {
        certification = default!;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var path = ContentRoot.GetPath("TraversalLab", "objective-seams.json");
        EnsureLoaded(path);
        return _certificationsByLabel is not null
            && _certificationsByLabel.TryGetValue(label, out certification);
    }

    public static bool TryGetCertifiedSuccessorLabels(string label, out IReadOnlyList<string> successorLabels)
    {
        successorLabels = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var path = ContentRoot.GetPath("TraversalLab", "objective-seams.json");
        EnsureLoaded(path);
        if (_successorLabelsByLabel is null
            || !_successorLabelsByLabel.TryGetValue(label, out var labels))
        {
            return false;
        }

        successorLabels = labels;
        return true;
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _loadedPath = null;
            _certificationsByLabel = null;
            _successorLabelsByLabel = null;
        }
    }

    public static Dictionary<string, string[]> BuildSuccessorMap(TraversalLabObjectiveSeamArtifact artifact)
    {
        var certifications = artifact.Programs
            .Where(static certification => !string.IsNullOrWhiteSpace(certification.Label))
            .ToArray();
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var certification in certifications)
        {
            var successors = certifications
                .Where(candidate => !string.Equals(candidate.Label, certification.Label, StringComparison.OrdinalIgnoreCase))
                .Where(candidate => HasWindowOverlap(certification, candidate))
                .Select(static candidate => candidate.Label)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static label => label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            map[certification.Label] = successors;
        }

        return map;
    }

    private static void EnsureLoaded(string path)
    {
        lock (Sync)
        {
            if (string.Equals(_loadedPath, path, StringComparison.OrdinalIgnoreCase)
                && _certificationsByLabel is not null)
            {
                return;
            }

            _loadedPath = path;
            _certificationsByLabel = new Dictionary<string, TraversalLabObjectiveSeamCertification>(StringComparer.OrdinalIgnoreCase);
            _successorLabelsByLabel = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return;
            }

            TraversalLabObjectiveSeamArtifact? artifact;
            try
            {
                artifact = JsonSerializer.Deserialize<TraversalLabObjectiveSeamArtifact>(File.ReadAllText(path), JsonOptions);
            }
            catch
            {
                return;
            }

            if (artifact is null)
            {
                return;
            }

            _successorLabelsByLabel = BuildSuccessorMap(artifact);

            foreach (var certification in artifact.Programs)
            {
                if (string.IsNullOrWhiteSpace(certification.Label))
                {
                    continue;
                }

                _certificationsByLabel[certification.Label] = certification;
            }
        }
    }

    private static bool HasWindowOverlap(
        TraversalLabObjectiveSeamCertification completionCertification,
        TraversalLabObjectiveSeamCertification startCertification)
    {
        foreach (var completionWindow in completionCertification.CompletionWindows)
        {
            foreach (var startWindow in startCertification.StartWindows)
            {
                if (completionWindow.RequireGrounded && !startWindow.RequireGrounded)
                {
                    continue;
                }

                if (RangesOverlap(
                        completionWindow.XMin,
                        completionWindow.XMax,
                        startWindow.StartXMin,
                        startWindow.StartXMax)
                    && RangesOverlap(
                        completionWindow.Bottom - completionWindow.BottomTolerance,
                        completionWindow.Bottom + completionWindow.BottomTolerance,
                        startWindow.StartBottom - startWindow.StartBottomTolerance,
                        startWindow.StartBottom + startWindow.StartBottomTolerance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RangesOverlap(float minA, float maxA, float minB, float maxB)
        => maxA >= minB && maxB >= minA;
}
