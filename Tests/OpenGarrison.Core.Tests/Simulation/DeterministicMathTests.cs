using System.Security.Cryptography;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests.Simulation;

public sealed class DeterministicMathTests
{
    // Recorded on Windows x64; CI re-checks it on Linux, which is what proves the
    // functions are bit-identical across platforms. Only change it together with an
    // intentional change to DeterministicMath, and expect replay goldens to move too.
    private const string ExpectedFingerprint = "32D68E9CB63E852F9FE582615424B1B5E5816182AF3024031654BB8E7EC5E724";

    [Fact]
    public void SinAndCosStayWithinOneUlpOfTheCorrectlyRoundedResult()
    {
        foreach (var value in Samples(-200f, 200f))
        {
            AssertWithinOneUlp((float)Math.Sin(value), DeterministicMath.Sin(value), $"Sin({value:R})");
            AssertWithinOneUlp((float)Math.Cos(value), DeterministicMath.Cos(value), $"Cos({value:R})");
        }
    }

    [Fact]
    public void AtanAndAtan2StayWithinOneUlpOfTheCorrectlyRoundedResult()
    {
        foreach (var value in Samples(-5000f, 5000f).Concat(Samples(-3f, 3f)))
        {
            AssertWithinOneUlp((float)Math.Atan(value), DeterministicMath.Atan(value), $"Atan({value:R})");
        }

        var random = new Random(1234);
        for (var index = 0; index < 20000; index++)
        {
            var y = (random.NextSingle() - 0.5f) * 4000f;
            var x = (random.NextSingle() - 0.5f) * 4000f;
            AssertWithinOneUlp((float)Math.Atan2(y, x), DeterministicMath.Atan2(y, x), $"Atan2({y:R}, {x:R})");
        }
    }

    [Fact]
    public void ExpAndPowStayWithinOneUlpOfTheCorrectlyRoundedResult()
    {
        foreach (var value in Samples(-80f, 80f))
        {
            AssertWithinOneUlp((float)Math.Exp(value), DeterministicMath.Exp(value), $"Exp({value:R})");
        }

        var random = new Random(5678);
        for (var index = 0; index < 20000; index++)
        {
            var x = random.NextSingle() * 10f;
            var y = (random.NextSingle() - 0.5f) * 10f;
            AssertWithinOneUlp((float)Math.Pow(x, y), DeterministicMath.Pow(x, y), $"Pow({x:R}, {y:R})");
        }

        AssertWithinOneUlp(-8f, DeterministicMath.Pow(-2f, 3f), "Pow(-2, 3)");
        AssertWithinOneUlp(0.25f, DeterministicMath.Pow(-2f, -2f), "Pow(-2, -2)");
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(-0f, 1f)]
    [InlineData(0f, -1f)]
    [InlineData(-0f, -1f)]
    [InlineData(1f, 0f)]
    [InlineData(-1f, 0f)]
    [InlineData(1f, -0f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(1f, float.NegativeInfinity)]
    [InlineData(float.NegativeInfinity, float.NegativeInfinity)]
    [InlineData(float.NaN, 1f)]
    public void Atan2SpecialCasesMatchMathF(float y, float x)
        => Assert.Equal(BitConverter.SingleToInt32Bits(MathF.Atan2(y, x)), BitConverter.SingleToInt32Bits(DeterministicMath.Atan2(y, x)));

    [Fact]
    public void SpecialValuesMatchMathF()
    {
        foreach (var value in new[] { 0f, -0f, float.PositiveInfinity, float.NegativeInfinity, float.NaN })
        {
            AssertSameSpecial(MathF.Sin(value), DeterministicMath.Sin(value));
            AssertSameSpecial(MathF.Cos(value), DeterministicMath.Cos(value));
            AssertSameSpecial(MathF.Atan(value), DeterministicMath.Atan(value));
            AssertSameSpecial(MathF.Exp(value), DeterministicMath.Exp(value));
        }

        foreach (var (x, y) in new[] { (0f, 2f), (0f, -2f), (-0f, 3f), (2f, 0f), (1f, float.NaN), (-2f, 0.5f), (float.PositiveInfinity, -1f) })
        {
            AssertSameSpecial(MathF.Pow(x, y), DeterministicMath.Pow(x, y));
        }
    }

    [Fact]
    public void OutputsMatchTheCrossPlatformFingerprint()
    {
        var buffer = new List<byte>();
        void Add(float value) => buffer.AddRange(BitConverter.GetBytes(BitConverter.SingleToInt32Bits(value)));

        var random = new Random(42);
        for (var index = 0; index < 50000; index++)
        {
            var a = (random.NextSingle() - 0.5f) * 4000f;
            var b = (random.NextSingle() - 0.5f) * 4000f;
            Add(DeterministicMath.Sin(a * 0.05f));
            Add(DeterministicMath.Cos(a * 0.05f));
            Add(DeterministicMath.Atan(a / 100f));
            Add(DeterministicMath.Atan2(a, b));
            Add(DeterministicMath.Exp(b / 50f));
            Add(DeterministicMath.Pow(MathF.Abs(a) / 400f, b / 800f));
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
        Assert.True(ExpectedFingerprint == fingerprint, $"DeterministicMath fingerprint changed: {fingerprint}");
    }

    [Fact]
    public void CoreSimulationCodeDoesNotCallPlatformTranscendentalMath()
    {
        // Wall-clock presentation smoothing; never feeds simulation state.
        var allowed = new[] { "Simulation/Networking/NetworkInterpolationTimeline.cs" };
        var pattern = new System.Text.RegularExpressions.Regex(
            @"\bMathF?\.(Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Sinh|Cosh|Tanh|SinCos|Pow|Exp|Log|Log2|Log10|Cbrt)\s*\(");
        var coreDirectory = Path.Combine(FindRepositoryRoot(), "Core");
        var violations = Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(coreDirectory, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("bin/", StringComparison.Ordinal) && !path.StartsWith("obj/", StringComparison.Ordinal))
            .Where(path => !allowed.Contains(path))
            .SelectMany(path => File.ReadLines(Path.Combine(coreDirectory, path))
                .Select((line, index) => (path, line, index))
                .Where(entry => pattern.IsMatch(entry.line))
                .Select(entry => $"Core/{entry.path}:{entry.index + 1}: {entry.line.Trim()}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "Use DeterministicMath instead of platform math in Core so simulation results match on every OS:\n"
            + string.Join("\n", violations));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }

    private static IEnumerable<float> Samples(float min, float max)
    {
        const int count = 40000;
        for (var index = 0; index <= count; index++)
        {
            yield return min + ((max - min) * index / count);
        }
    }

    private static void AssertWithinOneUlp(float expected, float actual, string expression)
    {
        if (expected == actual)
        {
            return;
        }

        var distance = Math.Abs((long)BitConverter.SingleToInt32Bits(expected) - BitConverter.SingleToInt32Bits(actual));
        Assert.True(
            MathF.Sign(expected) == MathF.Sign(actual) && distance <= 1,
            $"{expression}: expected {expected:R}, actual {actual:R} ({distance} ulp apart)");
    }

    private static void AssertSameSpecial(float expected, float actual)
    {
        if (float.IsNaN(expected))
        {
            Assert.True(float.IsNaN(actual));
            return;
        }

        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
    }
}
