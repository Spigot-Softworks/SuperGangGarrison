using OpenGarrison.Core;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class TraversalLabCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new(JsonOptions)
    {
        WriteIndented = true,
    };

    public static int Run(NavBuildOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.TraversalLabSuitePath))
        {
            return RunSuite(options.TraversalLabSuitePath, options.TraversalLabObjectiveSeamArtifactExportPath);
        }

        var scenarioPath = options.TraversalLabScenarioPath;
        if (string.IsNullOrWhiteSpace(scenarioPath))
        {
            Console.Error.WriteLine("TraversalLab requires --traversal-lab-scenario <path>.");
            return 2;
        }

        TraversalLabScenario? scenario;
        try
        {
            scenario = JsonSerializer.Deserialize<TraversalLabScenario>(File.ReadAllText(scenarioPath), JsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"traversal-lab failed_to_read_scenario path={scenarioPath} reason={ex.Message}");
            return 2;
        }

        if (scenario is null)
        {
            Console.Error.WriteLine($"traversal-lab failed_to_read_scenario path={scenarioPath} reason=deserialized_null");
            return 2;
        }

        TraversalLabBatchResult result;
        try
        {
            result = TraversalLabRunner.Run(scenario);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"traversal-lab failed_to_run scenario={scenario.Name} reason={ex.Message}");
            return 2;
        }

        Console.WriteLine(
            $"traversal-lab scenario={scenario.Name} level={scenario.LevelName ?? scenario.OverrideLevel?.Name ?? "custom"} " +
            $"team={scenario.Team} class={scenario.ClassId} variants={result.Cases.Count} passed={result.PassedCount} failed={result.FailedCount}");
        foreach (var caseResult in result.Cases)
        {
            Console.WriteLine(
                $"variant dx={FormatFloat(caseResult.Variant.StartXOffset)} db={FormatFloat(caseResult.Variant.StartBottomOffset)} " +
                $"face={FormatFloat(caseResult.Variant.FacingDirectionX)} status={(caseResult.Passed ? "pass" : "fail")} " +
                $"start=({FormatFloat(caseResult.StartX)},{FormatFloat(caseResult.StartBottom)}) startGrounded={caseResult.StartGrounded} " +
                $"ticks={caseResult.ExecutedTicks} final=({FormatFloat(caseResult.FinalX)},{FormatFloat(caseResult.FinalBottom)}) " +
                $"rangeX={FormatFloat(caseResult.MinX)}..{FormatFloat(caseResult.MaxX)} " +
                $"rangeBottom={FormatFloat(caseResult.MinBottom)}..{FormatFloat(caseResult.MaxBottom)} " +
                $"travelX={FormatFloat(caseResult.HorizontalTravel)} travelBottom={FormatFloat(caseResult.BottomTravel)} " +
                $"leftGround={FormatOptionalTick(caseResult.FirstLeaveGroundTick)} regrounded={FormatOptionalTick(caseResult.FirstRegroundTick)} " +
                $"carryTick={FormatOptionalTick(caseResult.FirstCarryIntelTick)} grounded={caseResult.FinalGrounded} carry={caseResult.FinalCarryingIntel} " +
                $"enemyMarker={caseResult.FinalOverlapsEnemyIntelMarker} gate={caseResult.FinalInsideBlockingTeamGate} " +
                $"reason={(string.IsNullOrWhiteSpace(caseResult.FailureReason) ? "clear" : caseResult.FailureReason)}");
            foreach (var sample in caseResult.Samples)
            {
                Console.WriteLine(
                    $"  sample t={sample.Tick} step={sample.StepLabel} pos=({FormatFloat(sample.X)},{FormatFloat(sample.Bottom)}) " +
                    $"vel=({FormatFloat(sample.HorizontalSpeed)},{FormatFloat(sample.VerticalSpeed)}) grounded={sample.IsGrounded} face={FormatFloat(sample.FacingDirectionX)} " +
                    $"support={sample.SupportedBelow} blockedL={sample.BlockedLeft} blockedR={sample.BlockedRight} " +
                    $"carry={sample.IsCarryingIntel} enemyMarker={sample.OverlapsEnemyIntelMarker} ownMarker={sample.OverlapsOwnIntelMarker} gate={sample.IsInsideBlockingTeamGate}");
            }
        }

        return result.Passed ? 0 : 3;
    }

    private static int RunSuite(string suitePath, string? exportObjectiveSeamArtifactPath)
    {
        TraversalLabPrimitiveSuite? suite;
        try
        {
            suite = JsonSerializer.Deserialize<TraversalLabPrimitiveSuite>(File.ReadAllText(suitePath), JsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"traversal-lab failed_to_read_suite path={suitePath} reason={ex.Message}");
            return 2;
        }

        if (suite is null)
        {
            Console.Error.WriteLine($"traversal-lab failed_to_read_suite path={suitePath} reason=deserialized_null");
            return 2;
        }

        TraversalLabPrimitiveSuiteResult result;
        try
        {
            result = TraversalLabPrimitiveSuiteRunner.Run(suite);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"traversal-lab failed_to_run_suite suite={suite.Name} reason={ex.Message}");
            return 2;
        }

        Console.WriteLine(
            $"traversal-lab suite={result.SuiteName} primitives={result.PrimitiveResults.Count} passed={result.PassedCount} failed={result.FailedCount}");
        foreach (var primitiveResult in result.PrimitiveResults)
        {
            var envelope = primitiveResult.Envelope;
            Console.WriteLine(
                $"primitive name={primitiveResult.PrimitiveName} profile={primitiveResult.PerturbationProfileName} " +
                $"status={(primitiveResult.Passed ? "pass" : "fail")} certified={envelope.CertifiedVariants}/{envelope.TotalVariants} " +
                $"startDx={FormatFloat(envelope.StartXOffsetMin)}..{FormatFloat(envelope.StartXOffsetMax)} " +
                $"startDb={FormatFloat(envelope.StartBottomOffsetMin)}..{FormatFloat(envelope.StartBottomOffsetMax)} " +
                $"startHSpeed={FormatFloat(envelope.StartHorizontalSpeedOffsetMin)}..{FormatFloat(envelope.StartHorizontalSpeedOffsetMax)} " +
                $"startVSpeed={FormatFloat(envelope.StartVerticalSpeedOffsetMin)}..{FormatFloat(envelope.StartVerticalSpeedOffsetMax)} " +
                $"finalX={FormatFloat(envelope.FinalXMin)}..{FormatFloat(envelope.FinalXMax)} " +
                $"finalBottom={FormatFloat(envelope.FinalBottomMin)}..{FormatFloat(envelope.FinalBottomMax)} " +
                $"faces={(envelope.SupportsFacingLeft ? "L" : string.Empty)}{(envelope.SupportsFacingRight ? "R" : string.Empty)} " +
                $"groundedStart={envelope.SupportsGroundedStart} airborneStart={envelope.SupportsAirborneStart}");
            foreach (var window in envelope.Windows)
            {
                Console.WriteLine(
                    $"  window dx={FormatFloat(window.StartXOffsetMin)}..{FormatFloat(window.StartXOffsetMax)} " +
                    $"db={FormatFloat(window.StartBottomOffset)} face={FormatFloat(window.FacingDirectionX)} " +
                    $"hs={FormatFloat(window.StartHorizontalSpeedOffset)} vs={FormatFloat(window.StartVerticalSpeedOffset)} " +
                    $"grounded={window.StartGrounded} variants={window.CertifiedVariantCount} " +
                    $"finalX={FormatFloat(window.FinalXMin)}..{FormatFloat(window.FinalXMax)} " +
                    $"finalBottom={FormatFloat(window.FinalBottomMin)}..{FormatFloat(window.FinalBottomMax)}");
            }
            foreach (var failedCase in primitiveResult.BatchResult.Cases.Where(static c => !c.Passed))
            {
                Console.WriteLine(
                    $"  fail dx={FormatFloat(failedCase.Variant.StartXOffset)} db={FormatFloat(failedCase.Variant.StartBottomOffset)} " +
                    $"face={FormatFloat(failedCase.Variant.FacingDirectionX)} hs={FormatFloat(failedCase.Variant.StartHorizontalSpeedOffset)} " +
                    $"vs={FormatFloat(failedCase.Variant.StartVerticalSpeedOffset)} grounded={failedCase.Variant.StartGrounded} " +
                    $"reason={failedCase.FailureReason}");
            }
        }

        if (!string.IsNullOrWhiteSpace(exportObjectiveSeamArtifactPath))
        {
            try
            {
                var artifact = TraversalLabPrimitiveSuiteRunner.BuildObjectiveSeamArtifact(suite, result);
                if (File.Exists(exportObjectiveSeamArtifactPath))
                {
                    var existing = JsonSerializer.Deserialize<TraversalLabObjectiveSeamArtifact>(
                        File.ReadAllText(exportObjectiveSeamArtifactPath),
                        JsonOptions);
                    if (existing is not null)
                    {
                        foreach (var certification in artifact.Programs)
                        {
                            existing.Programs.RemoveAll(program => program.Label.Equals(certification.Label, StringComparison.OrdinalIgnoreCase));
                            existing.Programs.Add(certification);
                        }

                        artifact = existing;
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(exportObjectiveSeamArtifactPath)!);
                File.WriteAllText(
                    exportObjectiveSeamArtifactPath,
                    JsonSerializer.Serialize(artifact, IndentedJsonOptions));
                Console.WriteLine(
                    $"objective-seam-artifact path={exportObjectiveSeamArtifactPath} programs={artifact.Programs.Count}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"traversal-lab failed_to_export_objective_seam_artifact path={exportObjectiveSeamArtifactPath} reason={ex.Message}");
                return 2;
            }
        }

        return result.Passed ? 0 : 3;
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatOptionalTick(int? tick)
    {
        return tick.HasValue && tick.Value > 0
            ? tick.Value.ToString(CultureInfo.InvariantCulture)
            : "n/a";
    }
}
