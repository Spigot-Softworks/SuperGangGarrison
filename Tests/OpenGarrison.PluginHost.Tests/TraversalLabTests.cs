using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class TraversalLabTests
{
    [Fact]
    public void RunnerExecutesAllConfiguredVariantStarts()
    {
        var scenario = new TraversalLabScenario
        {
            Name = "flat_run_right",
            OverrideLevel = CreateFlatGroundLevel(),
            Team = PlayerTeam.Red,
            ClassId = PlayerClass.Scout,
            Start = new TraversalLabStartState
            {
                X = 128f,
                Bottom = 500f,
                IsGrounded = true,
                FacingDirectionX = 1f,
            },
            Steps =
            [
                new TraversalLabInputStep
                {
                    Label = "run_right",
                    DurationTicks = 10,
                    Right = true,
                },
            ],
            MaxTicks = 10,
            TraceEveryTicks = 5,
            StartXOffsets = [0f, -8f, 8f],
            FacingDirections = [1f],
        };

        var result = TraversalLabRunner.Run(scenario);

        Assert.Equal(3, result.Cases.Count);
        Assert.All(
            result.Cases,
            caseResult =>
            {
                var expectedStartX = scenario.Start.X + caseResult.Variant.StartXOffset;
                Assert.True(caseResult.FinalX > expectedStartX + 2f, $"expected horizontal progress from {expectedStartX:0.0}, got {caseResult.FinalX:0.0}");
                Assert.Equal(10, caseResult.ExecutedTicks);
                Assert.NotEmpty(caseResult.Samples);
            });
    }

    [Fact]
    public void RunnerEvaluatesExpectationWindow()
    {
        var scenario = new TraversalLabScenario
        {
            Name = "flat_idle_grounded",
            OverrideLevel = CreateFlatGroundLevel(),
            Team = PlayerTeam.Red,
            ClassId = PlayerClass.Scout,
            Start = new TraversalLabStartState
            {
                X = 128f,
                Bottom = 500f,
                IsGrounded = true,
                FacingDirectionX = 1f,
            },
            Steps = [],
            MaxTicks = 1,
            Expectation = new TraversalLabExpectation
            {
                FinalX = 128f,
                FinalBottom = 500f,
                RadiusX = 4f,
                RadiusBottom = 4f,
                MustBeGrounded = true,
            },
        };

        var result = TraversalLabRunner.Run(scenario);

        Assert.Single(result.Cases);
        Assert.True(result.Passed, result.Cases[0].FailureReason);
    }

    [Fact]
    public void RunnerFailsWhenRequiredHorizontalTravelDoesNotOccur()
    {
        var scenario = new TraversalLabScenario
        {
            Name = "flat_idle_should_fail_horizontal_requirement",
            OverrideLevel = CreateFlatGroundLevel(),
            Team = PlayerTeam.Red,
            ClassId = PlayerClass.Scout,
            Start = new TraversalLabStartState
            {
                X = 128f,
                Bottom = 500f,
                IsGrounded = true,
                FacingDirectionX = 1f,
            },
            Steps = [],
            MaxTicks = 5,
            Expectation = new TraversalLabExpectation
            {
                MinHorizontalTravel = 4f,
                MustBeGrounded = true,
            },
        };

        var result = TraversalLabRunner.Run(scenario);

        Assert.Single(result.Cases);
        Assert.False(result.Passed);
        Assert.Contains("horizontal_travel_below_min", result.Cases[0].FailureReason);
    }

    [Fact]
    public void RunnerSupportsFixtureBackedScenarios()
    {
        var scenario = new TraversalLabScenario
        {
            Name = "fixture_flat_run_right",
            Fixture = TraversalLabFixtureKind.FlatGround,
            Team = PlayerTeam.Red,
            ClassId = PlayerClass.Scout,
            Start = new TraversalLabStartState
            {
                X = 128f,
                Bottom = 500f,
                IsGrounded = true,
                FacingDirectionX = 1f,
            },
            Steps =
            [
                new TraversalLabInputStep
                {
                    Label = "run_right",
                    DurationTicks = 10,
                    Right = true,
                },
            ],
            MaxTicks = 10,
            Expectation = new TraversalLabExpectation
            {
                MinHorizontalTravel = 8f,
                MustBeGrounded = true,
            },
        };

        var result = TraversalLabRunner.Run(scenario);

        Assert.Single(result.Cases);
        Assert.True(result.Passed, result.Cases[0].FailureReason);
    }

    [Fact]
    public void PrimitiveSuiteRunnerProducesCertificationEnvelope()
    {
        var suite = new TraversalLabPrimitiveSuite
        {
            Name = "scout_basics",
            Team = PlayerTeam.Red,
            ClassId = PlayerClass.Scout,
            PerturbationProfiles =
            [
                new TraversalLabPerturbationProfile
                {
                    Name = "spawn_window",
                    StartXOffsets = [-16f, 0f, 16f],
                    FacingDirections = [1f],
                    GroundedStates = [true],
                },
            ],
            Primitives =
            [
                new TraversalLabPrimitiveDefinition
                {
                    Name = "flat_run_right",
                    Fixture = TraversalLabFixtureKind.FlatGround,
                    Start = new TraversalLabStartState
                    {
                        X = 128f,
                        Bottom = 500f,
                        IsGrounded = true,
                        FacingDirectionX = 1f,
                    },
                    Steps =
                    [
                        new TraversalLabInputStep
                        {
                            Label = "run_right",
                            DurationTicks = 12,
                            Right = true,
                        },
                    ],
                    MaxTicks = 12,
                    PerturbationProfile = "spawn_window",
                    Expectation = new TraversalLabExpectation
                    {
                        MinHorizontalTravel = 12f,
                        MustBeGrounded = true,
                    },
                },
            ],
        };

        var result = TraversalLabPrimitiveSuiteRunner.Run(suite);

        Assert.Single(result.PrimitiveResults);
        Assert.True(result.Passed);
        Assert.Equal(3, result.PrimitiveResults[0].Envelope.TotalVariants);
        Assert.Equal(3, result.PrimitiveResults[0].Envelope.CertifiedVariants);
        Assert.True(result.PrimitiveResults[0].Envelope.FinalXMax > result.PrimitiveResults[0].Envelope.FinalXMin);
        Assert.Single(result.PrimitiveResults[0].Envelope.Windows);
    }

    [Fact]
    public void CertificationEnvelopeSplitsDisjointPassedStartWindows()
    {
        var batch = new TraversalLabBatchResult
        {
            ScenarioName = "window_split",
            Cases =
            [
                CreateCase(-16f, passed: true, finalX: 100f),
                CreateCase(-8f, passed: true, finalX: 108f),
                CreateCase(0f, passed: false, finalX: 0f),
                CreateCase(8f, passed: true, finalX: 124f),
                CreateCase(16f, passed: true, finalX: 132f),
            ],
        };

        var envelope = TraversalLabPrimitiveSuiteRunner.BuildCertificationEnvelope(batch);

        Assert.Equal(2, envelope.Windows.Count);
        Assert.Equal(-16f, envelope.Windows[0].StartXOffsetMin);
        Assert.Equal(-8f, envelope.Windows[0].StartXOffsetMax);
        Assert.Equal(8f, envelope.Windows[1].StartXOffsetMin);
        Assert.Equal(16f, envelope.Windows[1].StartXOffsetMax);
    }

    [Fact]
    public void ObjectiveSeamArtifactUsesCertifiedLandingGroundedState()
    {
        var suite = new TraversalLabPrimitiveSuite
        {
            Name = "artifact_suite",
            Primitives =
            [
                new TraversalLabPrimitiveDefinition
                {
                    Name = "seam_a",
                    ArtifactLabel = "seam_a",
                    Start = new TraversalLabStartState
                    {
                        X = 100f,
                        Bottom = 500f,
                        IsGrounded = true,
                        FacingDirectionX = 1f,
                    },
                },
            ],
        };
        var result = new TraversalLabPrimitiveSuiteResult
        {
            SuiteName = "artifact_suite",
            PrimitiveResults =
            [
                new TraversalLabPrimitiveCertificationResult
                {
                    PrimitiveName = "seam_a",
                    ArtifactLabel = "seam_a",
                    PerturbationProfileName = "default",
                    BatchResult = new TraversalLabBatchResult
                    {
                        ScenarioName = "seam_a",
                        Cases = [],
                    },
                    Envelope = new TraversalLabCertificationEnvelope
                    {
                        TotalVariants = 1,
                        CertifiedVariants = 1,
                        Windows =
                        [
                            new TraversalLabCertifiedWindow
                            {
                                StartXOffsetMin = -4f,
                                StartXOffsetMax = 4f,
                                StartBottomOffset = 0f,
                                FacingDirectionX = 1f,
                                StartHorizontalSpeedOffset = 0f,
                                StartVerticalSpeedOffset = 0f,
                                StartGrounded = true,
                                CertifiedVariantCount = 1,
                                FinalXMin = 140f,
                                FinalXMax = 144f,
                                FinalBottomMin = 520f,
                                FinalBottomMax = 520f,
                                FinalGrounded = false,
                            },
                        ],
                    },
                },
            ],
        };

        var artifact = TraversalLabPrimitiveSuiteRunner.BuildObjectiveSeamArtifact(suite, result);

        Assert.Single(artifact.Programs);
        Assert.Single(artifact.Programs[0].CompletionWindows);
        Assert.False(artifact.Programs[0].CompletionWindows[0].RequireGrounded);
    }

    [Fact]
    public void ObjectiveSeamArtifactPreservesStartSpeedBands()
    {
        var suite = new TraversalLabPrimitiveSuite
        {
            Name = "artifact_speed_suite",
            Primitives =
            [
                new TraversalLabPrimitiveDefinition
                {
                    Name = "seam_speed",
                    ArtifactLabel = "seam_speed",
                    Start = new TraversalLabStartState
                    {
                        X = 100f,
                        Bottom = 500f,
                        IsGrounded = true,
                        FacingDirectionX = -1f,
                    },
                },
            ],
        };
        var result = new TraversalLabPrimitiveSuiteResult
        {
            SuiteName = "artifact_speed_suite",
            PrimitiveResults =
            [
                new TraversalLabPrimitiveCertificationResult
                {
                    PrimitiveName = "seam_speed",
                    ArtifactLabel = "seam_speed",
                    PerturbationProfileName = "default",
                    BatchResult = new TraversalLabBatchResult
                    {
                        ScenarioName = "seam_speed",
                        Cases = [],
                    },
                    Envelope = new TraversalLabCertificationEnvelope
                    {
                        TotalVariants = 2,
                        CertifiedVariants = 2,
                        Windows =
                        [
                            new TraversalLabCertifiedWindow
                            {
                                StartXOffsetMin = 0f,
                                StartXOffsetMax = 16f,
                                StartBottomOffset = 0f,
                                FacingDirectionX = -1f,
                                StartHorizontalSpeedOffset = -170f,
                                StartVerticalSpeedOffset = 0f,
                                StartGrounded = true,
                                CertifiedVariantCount = 1,
                                FinalXMin = 50f,
                                FinalXMax = 60f,
                                FinalBottomMin = 500f,
                                FinalBottomMax = 500f,
                                FinalGrounded = true,
                            },
                            new TraversalLabCertifiedWindow
                            {
                                StartXOffsetMin = 0f,
                                StartXOffsetMax = 16f,
                                StartBottomOffset = 0f,
                                FacingDirectionX = -1f,
                                StartHorizontalSpeedOffset = -150f,
                                StartVerticalSpeedOffset = 0f,
                                StartGrounded = true,
                                CertifiedVariantCount = 1,
                                FinalXMin = 55f,
                                FinalXMax = 65f,
                                FinalBottomMin = 500f,
                                FinalBottomMax = 500f,
                                FinalGrounded = true,
                            },
                        ],
                    },
                },
            ],
        };

        var artifact = TraversalLabPrimitiveSuiteRunner.BuildObjectiveSeamArtifact(suite, result);

        Assert.Single(artifact.Programs);
        Assert.Single(artifact.Programs[0].StartWindows);
        Assert.Equal(-160f, artifact.Programs[0].StartWindows[0].HorizontalSpeedCenter);
        Assert.Equal(10f, artifact.Programs[0].StartWindows[0].HorizontalSpeedTolerance);
    }

    [Fact]
    public void ObjectiveSeamSuccessorMapOnlyIncludesCertifiedOverlaps()
    {
        var artifact = new TraversalLabObjectiveSeamArtifact
        {
            Programs =
            [
                new TraversalLabObjectiveSeamCertification
                {
                    Label = "stage_361",
                    StartWindows =
                    [
                        new TraversalLabObjectiveSeamStartWindowArtifact
                        {
                            StartXMin = 361f,
                            StartXMax = 377f,
                            StartBottom = 882f,
                            StartBottomTolerance = 0f,
                            FacingDirectionX = 1f,
                            HorizontalSpeedTolerance = 0f,
                            VerticalSpeedTolerance = 0f,
                            RequireGrounded = true,
                        },
                    ],
                    CompletionWindows =
                    [
                        new TraversalLabObjectiveSeamCompletionWindowArtifact
                        {
                            XMin = 103f,
                            XMax = 138f,
                            Bottom = 696f,
                            BottomTolerance = 0f,
                            RequireGrounded = true,
                        },
                    ],
                },
                new TraversalLabObjectiveSeamCertification
                {
                    Label = "stage_103",
                    StartWindows =
                    [
                        new TraversalLabObjectiveSeamStartWindowArtifact
                        {
                            StartXMin = 114f,
                            StartXMax = 146f,
                            StartBottom = 696f,
                            StartBottomTolerance = 0f,
                            FacingDirectionX = -1f,
                            HorizontalSpeedTolerance = 0f,
                            VerticalSpeedTolerance = 0f,
                            RequireGrounded = true,
                        },
                    ],
                    CompletionWindows =
                    [
                        new TraversalLabObjectiveSeamCompletionWindowArtifact
                        {
                            XMin = 782f,
                            XMax = 814f,
                            Bottom = 912f,
                            BottomTolerance = 0f,
                            RequireGrounded = true,
                        },
                    ],
                },
                new TraversalLabObjectiveSeamCertification
                {
                    Label = "stage_654",
                    StartWindows =
                    [
                        new TraversalLabObjectiveSeamStartWindowArtifact
                        {
                            StartXMin = 677f,
                            StartXMax = 693f,
                            StartBottom = 912f,
                            StartBottomTolerance = 0f,
                            FacingDirectionX = 1f,
                            HorizontalSpeedTolerance = 0f,
                            VerticalSpeedTolerance = 0f,
                            RequireGrounded = true,
                        },
                    ],
                    CompletionWindows = [],
                },
            ],
        };

        var successorMap = TraversalLabObjectiveSeamArtifactStore.BuildSuccessorMap(artifact);

        Assert.Contains("stage_103", successorMap["stage_361"]);
        Assert.DoesNotContain("stage_654", successorMap["stage_103"]);
    }

    private static SimpleLevel CreateFlatGroundLevel()
    {
        return new SimpleLevel(
            name: "traversal_lab_floor",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(2048f, 1024f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(128f, 128f),
            redSpawns: [new SpawnPoint(128f, 128f)],
            blueSpawns: [new SpawnPoint(256f, 128f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 128f, 128f),
                new IntelBaseMarker(PlayerTeam.Blue, 256f, 128f),
            ],
            roomObjects: [],
            floorY: 500f,
            solids: [new LevelSolid(0f, 500f, 2048f, 524f)],
            importedFromSource: false);
    }

    private static TraversalLabCaseResult CreateCase(float startXOffset, bool passed, float finalX)
    {
        return new TraversalLabCaseResult
        {
            Variant = new TraversalLabVariant(startXOffset, 0f, 1f, 0f, 0f, true),
            Passed = passed,
            FailureReason = passed ? string.Empty : "failed",
            StartX = 128f + startXOffset,
            StartY = 476f,
            StartBottom = 500f,
            StartGrounded = true,
            FinalX = finalX,
            FinalY = 476f,
            FinalBottom = 500f,
            MinX = MathF.Min(128f + startXOffset, finalX),
            MaxX = MathF.Max(128f + startXOffset, finalX),
            MinBottom = 500f,
            MaxBottom = 500f,
            HorizontalTravel = MathF.Abs(finalX - (128f + startXOffset)),
            BottomTravel = 0f,
            FinalGrounded = true,
            FinalCarryingIntel = false,
            FinalOverlapsEnemyIntelMarker = false,
            FinalInsideBlockingTeamGate = false,
            FirstLeaveGroundTick = null,
            FirstRegroundTick = null,
            FirstCarryIntelTick = null,
            ExecutedTicks = 1,
            Samples = [],
        };
    }
}
