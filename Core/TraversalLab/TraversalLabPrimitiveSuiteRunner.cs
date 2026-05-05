using System.Linq;

namespace OpenGarrison.Core;

public static class TraversalLabPrimitiveSuiteRunner
{
    private readonly record struct CertifiedWindowKey(
        float StartBottomOffset,
        float FacingDirectionX,
        float StartHorizontalSpeedOffset,
        float StartVerticalSpeedOffset,
        bool StartGrounded);

    public static TraversalLabPrimitiveSuiteResult Run(TraversalLabPrimitiveSuite suite)
    {
        ArgumentNullException.ThrowIfNull(suite);
        if (suite.Primitives.Count == 0)
        {
            throw new InvalidOperationException("TraversalLab primitive suite requires at least one primitive.");
        }

        var profiles = suite.PerturbationProfiles
            .ToDictionary(profile => profile.Name, StringComparer.OrdinalIgnoreCase);
        if (!profiles.ContainsKey("default"))
        {
            profiles["default"] = new TraversalLabPerturbationProfile { Name = "default" };
        }

        var results = new List<TraversalLabPrimitiveCertificationResult>(suite.Primitives.Count);
        foreach (var primitive in suite.Primitives)
        {
            if (!profiles.TryGetValue(primitive.PerturbationProfile, out var profile))
            {
                throw new InvalidOperationException($"TraversalLab primitive '{primitive.Name}' references unknown perturbation profile '{primitive.PerturbationProfile}'.");
            }

            var scenario = new TraversalLabScenario
            {
                Name = primitive.Name,
                Fixture = primitive.Fixture,
                LevelName = primitive.LevelName,
                MapAreaIndex = primitive.MapAreaIndex,
                Team = primitive.Team ?? suite.Team,
                ClassId = primitive.ClassId ?? suite.ClassId,
                Start = primitive.Start,
                Steps = primitive.Steps,
                MaxTicks = primitive.MaxTicks,
                TraceEveryTicks = primitive.TraceEveryTicks,
                StartXOffsets = profile.StartXOffsets,
                StartBottomOffsets = profile.StartBottomOffsets,
                FacingDirections = profile.FacingDirections,
                StartHorizontalSpeedOffsets = profile.StartHorizontalSpeedOffsets,
                StartVerticalSpeedOffsets = profile.StartVerticalSpeedOffsets,
                GroundedStates = profile.GroundedStates,
                Expectation = primitive.Expectation,
            };

            var batchResult = TraversalLabRunner.Run(scenario);
            results.Add(new TraversalLabPrimitiveCertificationResult
            {
                PrimitiveName = primitive.Name,
                ArtifactLabel = primitive.ArtifactLabel,
                PerturbationProfileName = profile.Name,
                BatchResult = batchResult,
                Envelope = BuildCertificationEnvelope(batchResult),
            });
        }

        return new TraversalLabPrimitiveSuiteResult
        {
            SuiteName = suite.Name,
            PrimitiveResults = results,
        };
    }

    public static TraversalLabObjectiveSeamArtifact BuildObjectiveSeamArtifact(
        TraversalLabPrimitiveSuite suite,
        TraversalLabPrimitiveSuiteResult result)
    {
        ArgumentNullException.ThrowIfNull(suite);
        ArgumentNullException.ThrowIfNull(result);
        var programs = new List<TraversalLabObjectiveSeamCertification>();
        foreach (var pair in suite.Primitives.Zip(result.PrimitiveResults, static (primitive, certification) => (primitive, certification)))
        {
            if (string.IsNullOrWhiteSpace(pair.primitive.ArtifactLabel)
                || pair.certification.Envelope.CertifiedVariants <= 0)
            {
                continue;
            }

            var startWindows = BuildStartWindowArtifacts(pair.primitive, pair.certification.Envelope.Windows);
            var completionWindows = BuildCompletionWindowArtifacts(pair.certification.Envelope.Windows);
            programs.Add(new TraversalLabObjectiveSeamCertification
            {
                Label = pair.primitive.ArtifactLabel!,
                StartWindows = startWindows,
                CompletionWindows = completionWindows,
            });
        }

        return new TraversalLabObjectiveSeamArtifact
        {
            Programs = programs,
        };
    }

    private static List<TraversalLabObjectiveSeamStartWindowArtifact> BuildStartWindowArtifacts(
        TraversalLabPrimitiveDefinition primitive,
        IReadOnlyList<TraversalLabCertifiedWindow> windows)
    {
        return windows
            .GroupBy(
                static window => new
                {
                    window.StartXOffsetMin,
                    window.StartXOffsetMax,
                    window.StartBottomOffset,
                    window.FacingDirectionX,
                    window.StartGrounded,
                })
            .Select(group =>
            {
                var minHorizontalSpeed = group.Min(window => primitive.Start.HorizontalSpeed + window.StartHorizontalSpeedOffset);
                var maxHorizontalSpeed = group.Max(window => primitive.Start.HorizontalSpeed + window.StartHorizontalSpeedOffset);
                var minVerticalSpeed = group.Min(window => primitive.Start.VerticalSpeed + window.StartVerticalSpeedOffset);
                var maxVerticalSpeed = group.Max(window => primitive.Start.VerticalSpeed + window.StartVerticalSpeedOffset);
                return new TraversalLabObjectiveSeamStartWindowArtifact
                {
                    StartXMin = primitive.Start.X + group.Key.StartXOffsetMin,
                    StartXMax = primitive.Start.X + group.Key.StartXOffsetMax,
                    StartBottom = primitive.Start.Bottom + group.Key.StartBottomOffset,
                    StartBottomTolerance = 0f,
                    FacingDirectionX = group.Key.FacingDirectionX,
                    HorizontalSpeedCenter = (minHorizontalSpeed + maxHorizontalSpeed) * 0.5f,
                    HorizontalSpeedTolerance = MathF.Abs(maxHorizontalSpeed - minHorizontalSpeed) * 0.5f,
                    VerticalSpeedCenter = (minVerticalSpeed + maxVerticalSpeed) * 0.5f,
                    VerticalSpeedTolerance = MathF.Abs(maxVerticalSpeed - minVerticalSpeed) * 0.5f,
                    RequireGrounded = group.Key.StartGrounded,
                };
            })
            .OrderBy(static window => window.RequireGrounded ? 0 : 1)
            .ThenBy(static window => window.FacingDirectionX)
            .ThenBy(static window => window.StartBottom)
            .ThenBy(static window => window.StartXMin)
            .ToList();
    }

    private static List<TraversalLabObjectiveSeamCompletionWindowArtifact> BuildCompletionWindowArtifacts(
        IReadOnlyList<TraversalLabCertifiedWindow> windows)
    {
        return windows
            .GroupBy(
                static window => new
                {
                    window.FinalGrounded,
                })
            .Select(group => new TraversalLabObjectiveSeamCompletionWindowArtifact
            {
                XMin = group.Min(static window => window.FinalXMin),
                XMax = group.Max(static window => window.FinalXMax),
                Bottom = (group.Min(static window => window.FinalBottomMin) + group.Max(static window => window.FinalBottomMax)) * 0.5f,
                BottomTolerance = MathF.Abs(group.Max(static window => window.FinalBottomMax) - group.Min(static window => window.FinalBottomMin)) * 0.5f,
                RequireGrounded = group.Key.FinalGrounded,
            })
            .OrderBy(static window => window.RequireGrounded ? 0 : 1)
            .ThenBy(static window => window.XMin)
            .ToList();
    }

    public static TraversalLabCertificationEnvelope BuildCertificationEnvelope(TraversalLabBatchResult batchResult)
    {
        var passedCases = batchResult.Cases.Where(static result => result.Passed).ToArray();
        if (passedCases.Length == 0)
        {
            return new TraversalLabCertificationEnvelope
            {
                TotalVariants = batchResult.Cases.Count,
                CertifiedVariants = 0,
                Windows = [],
            };
        }

        return new TraversalLabCertificationEnvelope
        {
            TotalVariants = batchResult.Cases.Count,
            CertifiedVariants = passedCases.Length,
            StartXOffsetMin = passedCases.Min(static result => result.Variant.StartXOffset),
            StartXOffsetMax = passedCases.Max(static result => result.Variant.StartXOffset),
            StartBottomOffsetMin = passedCases.Min(static result => result.Variant.StartBottomOffset),
            StartBottomOffsetMax = passedCases.Max(static result => result.Variant.StartBottomOffset),
            StartHorizontalSpeedOffsetMin = passedCases.Min(static result => result.Variant.StartHorizontalSpeedOffset),
            StartHorizontalSpeedOffsetMax = passedCases.Max(static result => result.Variant.StartHorizontalSpeedOffset),
            StartVerticalSpeedOffsetMin = passedCases.Min(static result => result.Variant.StartVerticalSpeedOffset),
            StartVerticalSpeedOffsetMax = passedCases.Max(static result => result.Variant.StartVerticalSpeedOffset),
            FinalXMin = passedCases.Min(static result => result.FinalX),
            FinalXMax = passedCases.Max(static result => result.FinalX),
            FinalBottomMin = passedCases.Min(static result => result.FinalBottom),
            FinalBottomMax = passedCases.Max(static result => result.FinalBottom),
            SupportsFacingLeft = passedCases.Any(static result => result.Variant.FacingDirectionX < 0f),
            SupportsFacingRight = passedCases.Any(static result => result.Variant.FacingDirectionX > 0f),
            SupportsGroundedStart = passedCases.Any(static result => result.Variant.StartGrounded),
            SupportsAirborneStart = passedCases.Any(static result => !result.Variant.StartGrounded),
            Windows = BuildCertifiedWindows(batchResult.Cases),
        };
    }

    private static TraversalLabCertifiedWindow[] BuildCertifiedWindows(IReadOnlyList<TraversalLabCaseResult> cases)
    {
        return cases
            .GroupBy(static result => new CertifiedWindowKey(
                result.Variant.StartBottomOffset,
                result.Variant.FacingDirectionX,
                result.Variant.StartHorizontalSpeedOffset,
                result.Variant.StartVerticalSpeedOffset,
                result.Variant.StartGrounded))
            .SelectMany(static group =>
            {
                var ordered = group
                    .OrderBy(static result => result.Variant.StartXOffset)
                    .ToArray();
                var windows = new List<TraversalLabCertifiedWindow>();
                List<TraversalLabCaseResult>? activeCases = null;
                foreach (var caseResult in ordered)
                {
                    if (caseResult.Passed)
                    {
                        activeCases ??= [];
                        activeCases.Add(caseResult);
                        continue;
                    }

                    if (activeCases is { Count: > 0 })
                    {
                        windows.Add(BuildWindow(group.Key, activeCases));
                        activeCases = null;
                    }
                }

                if (activeCases is { Count: > 0 })
                {
                    windows.Add(BuildWindow(group.Key, activeCases));
                }

                return windows;
            })
            .OrderBy(static window => window.StartGrounded ? 0 : 1)
            .ThenBy(static window => window.FacingDirectionX)
            .ThenBy(static window => window.StartBottomOffset)
            .ThenBy(static window => window.StartHorizontalSpeedOffset)
            .ThenBy(static window => window.StartVerticalSpeedOffset)
            .ThenBy(static window => window.StartXOffsetMin)
            .ToArray();
    }

    private static TraversalLabCertifiedWindow BuildWindow(
        CertifiedWindowKey key,
        List<TraversalLabCaseResult> cases)
    {
        return new TraversalLabCertifiedWindow
        {
            StartXOffsetMin = cases.Min(static result => result.Variant.StartXOffset),
            StartXOffsetMax = cases.Max(static result => result.Variant.StartXOffset),
            StartBottomOffset = key.StartBottomOffset,
            FacingDirectionX = key.FacingDirectionX,
            StartHorizontalSpeedOffset = key.StartHorizontalSpeedOffset,
            StartVerticalSpeedOffset = key.StartVerticalSpeedOffset,
            StartGrounded = key.StartGrounded,
            CertifiedVariantCount = cases.Count,
            FinalXMin = cases.Min(static result => result.FinalX),
            FinalXMax = cases.Max(static result => result.FinalX),
            FinalBottomMin = cases.Min(static result => result.FinalBottom),
            FinalBottomMax = cases.Max(static result => result.FinalBottom),
            FinalGrounded = cases.All(static result => result.FinalGrounded),
        };
    }
}
