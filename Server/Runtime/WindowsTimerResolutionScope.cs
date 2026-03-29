using System;
using System.Runtime.InteropServices;

internal sealed class WindowsTimerResolutionScope : IDisposable
{
    private const uint TimerResolutionMilliseconds = 1;
    private const uint TimeNoError = 0;
    private readonly bool _isActive;

    private WindowsTimerResolutionScope(bool isActive)
    {
        _isActive = isActive;
    }

    public bool IsActive => _isActive;

    public static WindowsTimerResolutionScope Create1Millisecond()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WindowsTimerResolutionScope(false);
        }

        try
        {
            return new WindowsTimerResolutionScope(timeBeginPeriod(TimerResolutionMilliseconds) == TimeNoError);
        }
        catch (DllNotFoundException)
        {
            return new WindowsTimerResolutionScope(false);
        }
        catch (EntryPointNotFoundException)
        {
            return new WindowsTimerResolutionScope(false);
        }
    }

    public void Dispose()
    {
        if (_isActive)
        {
            _ = timeEndPeriod(TimerResolutionMilliseconds);
        }
    }

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeBeginPeriod(uint periodMilliseconds);

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeEndPeriod(uint periodMilliseconds);
}
