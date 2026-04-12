#nullable enable

using System.Diagnostics;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly BrowserPerformanceAccumulator _browserPerformance = new();

    public BrowserPerformanceSnapshot GetBrowserPerformanceSnapshot()
    {
        return _browserPerformance.GetSnapshot();
    }

    private void RecordBrowserUpdateDuration(long startTimestamp)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserPerformance.RecordUpdate(GetElapsedMilliseconds(startTimestamp));
    }

    private void RecordBrowserDrawDuration(long startTimestamp)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserPerformance.RecordDraw(GetElapsedMilliseconds(startTimestamp));
    }

    private void RecordBrowserSimulationDuration(long startTimestamp, int tickCount)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserPerformance.RecordSimulation(GetElapsedMilliseconds(startTimestamp), tickCount);
    }

    private void RecordBrowserPresentationDuration(long startTimestamp)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserPerformance.RecordPresentation(GetElapsedMilliseconds(startTimestamp));
    }

    private void RecordBrowserWorldDrawDuration(long startTimestamp)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserPerformance.RecordWorldDraw(GetElapsedMilliseconds(startTimestamp));
    }

    private void RecordBrowserHudDrawDuration(long startTimestamp)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserPerformance.RecordHudDraw(GetElapsedMilliseconds(startTimestamp));
    }

    private void RecordBrowserModalDrawDuration(long startTimestamp)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        _browserPerformance.RecordModalDraw(GetElapsedMilliseconds(startTimestamp));
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return startTimestamp <= 0
            ? 0d
            : (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }

    public readonly record struct BrowserPerformanceSnapshot(
        long Samples,
        double LastUpdateMs,
        double AverageUpdateMs,
        double LastDrawMs,
        double AverageDrawMs,
        double LastSimulationMs,
        double AverageSimulationMs,
        double AverageSimulationTicks,
        double LastPresentationMs,
        double AveragePresentationMs,
        double LastWorldDrawMs,
        double AverageWorldDrawMs,
        double LastHudDrawMs,
        double AverageHudDrawMs,
        double LastModalDrawMs,
        double AverageModalDrawMs)
    {
        public string ToLogLine()
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Browser perf samples={Samples} update(last/avg)={LastUpdateMs:0.0}/{AverageUpdateMs:0.0}ms " +
                $"draw(last/avg)={LastDrawMs:0.0}/{AverageDrawMs:0.0}ms " +
                $"sim(last/avg)={LastSimulationMs:0.0}/{AverageSimulationMs:0.0}ms ticks(avg)={AverageSimulationTicks:0.0} " +
                $"present(last/avg)={LastPresentationMs:0.0}/{AveragePresentationMs:0.0}ms " +
                $"world(last/avg)={LastWorldDrawMs:0.0}/{AverageWorldDrawMs:0.0}ms " +
                $"hud(last/avg)={LastHudDrawMs:0.0}/{AverageHudDrawMs:0.0}ms " +
                $"modal(last/avg)={LastModalDrawMs:0.0}/{AverageModalDrawMs:0.0}ms");
        }
    }

    private sealed class BrowserPerformanceAccumulator
    {
        private long _samples;
        private double _updateSumMs;
        private double _drawSumMs;
        private double _simulationSumMs;
        private double _simulationTickSum;
        private double _presentationSumMs;
        private double _worldDrawSumMs;
        private double _hudDrawSumMs;
        private double _modalDrawSumMs;

        public double LastUpdateMs { get; private set; }

        public double LastDrawMs { get; private set; }

        public double LastSimulationMs { get; private set; }

        public int LastSimulationTicks { get; private set; }

        public double LastPresentationMs { get; private set; }

        public double LastWorldDrawMs { get; private set; }

        public double LastHudDrawMs { get; private set; }

        public double LastModalDrawMs { get; private set; }

        public void RecordUpdate(double milliseconds)
        {
            LastUpdateMs = milliseconds;
            _updateSumMs += milliseconds;
            _samples += 1;
        }

        public void RecordDraw(double milliseconds)
        {
            LastDrawMs = milliseconds;
            _drawSumMs += milliseconds;
        }

        public void RecordSimulation(double milliseconds, int tickCount)
        {
            LastSimulationMs = milliseconds;
            LastSimulationTicks = tickCount;
            _simulationSumMs += milliseconds;
            _simulationTickSum += tickCount;
        }

        public void RecordPresentation(double milliseconds)
        {
            LastPresentationMs = milliseconds;
            _presentationSumMs += milliseconds;
        }

        public void RecordWorldDraw(double milliseconds)
        {
            LastWorldDrawMs = milliseconds;
            _worldDrawSumMs += milliseconds;
        }

        public void RecordHudDraw(double milliseconds)
        {
            LastHudDrawMs = milliseconds;
            _hudDrawSumMs += milliseconds;
        }

        public void RecordModalDraw(double milliseconds)
        {
            LastModalDrawMs = milliseconds;
            _modalDrawSumMs += milliseconds;
        }

        public BrowserPerformanceSnapshot GetSnapshot()
        {
            var sampleCount = _samples <= 0 ? 1 : _samples;
            return new BrowserPerformanceSnapshot(
                _samples,
                LastUpdateMs,
                _updateSumMs / sampleCount,
                LastDrawMs,
                _drawSumMs / sampleCount,
                LastSimulationMs,
                _simulationSumMs / sampleCount,
                _simulationTickSum / sampleCount,
                LastPresentationMs,
                _presentationSumMs / sampleCount,
                LastWorldDrawMs,
                _worldDrawSumMs / sampleCount,
                LastHudDrawMs,
                _hudDrawSumMs / sampleCount,
                LastModalDrawMs,
                _modalDrawSumMs / sampleCount);
        }
    }
}
