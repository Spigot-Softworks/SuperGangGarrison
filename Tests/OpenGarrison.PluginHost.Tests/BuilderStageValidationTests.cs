using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuilderStageValidationTests
{
    [Theory]
    [InlineData("NextAreaO", false)]
    [InlineData("NextAreaO", true)]
    [InlineData("PreviousAreaO", false)]
    public void ControlPointLimitsApplyToEachStage(string transitionType, bool modernPoints)
    {
        var document = CreateStages(transitionType, modernPoints);
        var result = CustomMapBuilderValidator.Validate(document);
        Assert.Equal(CustomMapBuilderGameMode.AttackDefenseControlPoint, result.Mode);
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Message)));
    }

    [Fact]
    public void ExtraPointsInOneStageStillPreventPlayableSave()
    {
        var document = CreateStages();
        document = document with { Entities = [..document.Entities,
            CustomMapBuilderEntity.Create("controlPoint3", 30, 150),
            CustomMapBuilderEntity.Create("controlPoint4", 40, 150),
            CustomMapBuilderEntity.Create("controlPoint5", 50, 150)] };
        var issue = Assert.Single(CustomMapBuilderValidator.Validate(document).Issues);
        Assert.Equal("adcp_points", issue.Code);
        Assert.StartsWith("Stage 2:", issue.Message);
    }

    [Theory]
    [InlineData("controlPoint1", "adcp_points")]
    [InlineData("CapturePoint", "adcp_capture_zone")]
    [InlineData("SetupGate", "adcp_setup_gate")]
    public void MissingStageObjectivesAreNotSatisfiedByAnotherStage(string removedType, string expectedCode)
    {
        var document = CreateStages();
        document = document with { Entities = document.Entities.Where(entity => entity.Y != 150
            || !(removedType == "controlPoint1" ? entity.Type.StartsWith("controlPoint", StringComparison.OrdinalIgnoreCase)
                : entity.Type.Equals(removedType, StringComparison.OrdinalIgnoreCase))).ToArray() };
        var issue = Assert.Single(CustomMapBuilderValidator.Validate(document).Issues);
        Assert.Equal(expectedCode, issue.Code);
        Assert.StartsWith("Stage 2:", issue.Message);
    }

    [Fact]
    public void RepeatedMarkersAndPreviousMarkersFollowRuntimeBoundaries()
    {
        var document = CreateStages();
        document = document with { Entities = [..document.Entities,
            CustomMapBuilderEntity.Create("NextAreaO", 60, 100),
            CustomMapBuilderEntity.Create("PreviousAreaO", 60, 75)] };
        Assert.True(CustomMapBuilderValidator.Validate(document).IsValid);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(-1, 3, true)]
    [InlineData(100, 1, true)]
    [InlineData(100, 2, true)]
    [InlineData(100, 3, false)]
    [InlineData(150, 1, false)]
    [InlineData(150, 2, true)]
    [InlineData(150, 3, false)]
    [InlineData(200, 2, true)]
    [InlineData(200, 3, true)]
    [InlineData(250, 3, true)]
    public void SharedAreaFilterKeepsBoundaryAndGlobalEntityBehavior(float y, int area, bool expected)
    {
        Assert.Equal(expected, AreaTransitionMetadata.IsInArea(y, area, [100, 200]));
    }

    private static CustomMapBuilderDocument CreateStages(string transitionType = "NextAreaO", bool modernPoints = false)
    {
        var entities = new List<CustomMapBuilderEntity>
        {
            CustomMapBuilderEntity.Create(transitionType, 10, 100),
            CustomMapBuilderEntity.Create(transitionType, 10, 200),
        };
        for (var stage = 0; stage < 3; stage++)
        {
            var y = stage * 100 + 50;
            entities.Add(CustomMapBuilderEntity.Create("redspawn", 10, y));
            entities.Add(CustomMapBuilderEntity.Create("bluespawn", 90, y));
            entities.Add(CustomMapBuilderEntity.Create("SetupGate", 20, y));
            entities.Add(CustomMapBuilderEntity.Create("CapturePoint", 50, y));
            for (var point = 1; point <= 2; point++)
                entities.Add(modernPoints
                    ? CustomMapBuilderEntity.Create("controlPoint", point * 30, y, new Dictionary<string, string> { ["index"] = point.ToString() })
                    : CustomMapBuilderEntity.Create($"controlPoint{point}", point * 30, y));
        }
        return CustomMapBuilderDocument.CreateEmpty("staged") with { EmbeddedWalkmaskSection = "2\n1\n@", Entities = entities };
    }
}
