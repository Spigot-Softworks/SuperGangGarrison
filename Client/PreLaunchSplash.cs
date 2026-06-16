#nullable enable

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace OpenGarrison.Client;

/// <summary>
/// A lightweight native splash window shown from the game process the instant it starts, before the
/// MonoGame window exists and through the (blocking) content load. It gives the player visual feedback
/// ("Launching Super Gang Garrison...") during the several seconds of cold start when nothing is on
/// screen yet. Visuals mirror the updater / in-game loading window. Windows only; a no-op elsewhere.
/// </summary>
public static class PreLaunchSplash
{
    private static IPreLaunchSplash? _current;
    private static readonly object Sync = new();

    public static void Show(string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (Sync)
        {
            if (_current is not null)
            {
                return;
            }

            try
            {
                var splash = new WindowsPreLaunchSplash(message);
                splash.Show();
                _current = splash;
            }
            catch
            {
                // A splash must never prevent the game from launching.
                _current = null;
            }
        }
    }

    public static void Close()
    {
        IPreLaunchSplash? splash;
        lock (Sync)
        {
            splash = _current;
            _current = null;
        }

        try
        {
            splash?.Dispose();
        }
        catch
        {
        }
    }

    private interface IPreLaunchSplash : IDisposable
    {
        void Show();
    }

    [SupportedOSPlatform("windows")]
    private sealed class WindowsPreLaunchSplash : IPreLaunchSplash
    {
        private const int WindowWidth = 384;
        private const int WindowHeight = 108;
        private const int WmDestroy = 0x0002;
        private const int WmPaint = 0x000F;
        private const int WmTimer = 0x0113;
        private const int WmAppClose = 0x8001;
        private const int SwShow = 5;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsExTopmost = 0x00000008;
        private const int WsExToolWindow = 0x00000080;
        private const int CsHRedraw = 0x0002;
        private const int CsVRedraw = 0x0001;
        private const int DtLeft = 0x00000000;
        private const int DtSingleLine = 0x00000020;
        private const int DtVCenter = 0x00000004;
        private const int ProgressSegments = 20;
        private const uint AnimationTimerId = 1;

        private static readonly int BackgroundColor = ColorRef(54, 51, 50);
        private static readonly int BorderColor = ColorRef(119, 119, 119);
        private static readonly int ProgressColor = ColorRef(69, 108, 140);
        private static readonly int WhiteColor = ColorRef(255, 255, 255);

        private static readonly ConcurrentDictionary<IntPtr, WindowsPreLaunchSplash> WindowsByHandle = new();

        private readonly object _sync = new();
        private readonly ManualResetEventSlim _ready = new();
        private readonly Thread _thread;
        private readonly string _className = $"OpenGarrisonLaunchSplash_{Guid.NewGuid():N}";
        private readonly string _message;
        private readonly WndProc _wndProc;

        private IntPtr _handle;
        private bool _disposed;
        private int _animationStep;

        public WindowsPreLaunchSplash(string message)
        {
            _message = string.IsNullOrWhiteSpace(message) ? "Launching Super Gang Garrison..." : message.Trim();
            _wndProc = WindowProcedure;
            _thread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "OpenGarrison launch splash",
            };
            _thread.SetApartmentState(ApartmentState.STA);
        }

        public void Show()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _thread.Start();
            }

            _ready.Wait(TimeSpan.FromSeconds(2));
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            if (_handle != IntPtr.Zero)
            {
                PostMessage(_handle, WmAppClose, IntPtr.Zero, IntPtr.Zero);
            }

            if (_thread.IsAlive)
            {
                _thread.Join(TimeSpan.FromSeconds(2));
            }
        }

        private void RunMessageLoop()
        {
            var instance = GetModuleHandle(null);
            var windowClass = new WndClass
            {
                style = CsHRedraw | CsVRedraw,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = instance,
                hCursor = LoadCursor(IntPtr.Zero, 32512),
                hbrBackground = IntPtr.Zero,
                lpszClassName = _className,
            };

            RegisterClass(ref windowClass);

            var screenWidth = GetSystemMetrics(0);
            var screenHeight = GetSystemMetrics(1);
            var left = Math.Max(0, (screenWidth - WindowWidth) / 2);
            var top = Math.Max(0, (screenHeight - WindowHeight) / 2);

            _handle = CreateWindowEx(
                WsExTopmost | WsExToolWindow,
                _className,
                "Super Gang Garrison",
                WsPopup,
                left,
                top,
                WindowWidth,
                WindowHeight,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);

            if (_handle == IntPtr.Zero)
            {
                _ready.Set();
                return;
            }

            WindowsByHandle[_handle] = this;
            ShowWindow(_handle, SwShow);
            UpdateWindow(_handle);
            SetTimer(_handle, AnimationTimerId, 80, IntPtr.Zero);
            _ready.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }

        private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            switch (message)
            {
                case WmPaint:
                    Paint(hwnd);
                    return IntPtr.Zero;
                case WmTimer:
                    _animationStep += 1;
                    InvalidateRect(hwnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                case WmAppClose:
                    KillTimer(hwnd, AnimationTimerId);
                    DestroyWindow(hwnd);
                    return IntPtr.Zero;
                case WmDestroy:
                    WindowsByHandle.TryRemove(hwnd, out _);
                    PostQuitMessage(0);
                    return IntPtr.Zero;
                default:
                    return DefWindowProc(hwnd, message, wParam, lParam);
            }
        }

        private void Paint(IntPtr hwnd)
        {
            var paintStruct = new PaintStruct
            {
                rgbReserved = new byte[32],
            };
            var hdc = BeginPaint(hwnd, ref paintStruct);
            try
            {
                FillRect(hdc, new Rect(0, 0, WindowWidth, WindowHeight), BackgroundColor);
                FillRect(hdc, new Rect(0, 0, WindowWidth, 1), BorderColor);
                FillRect(hdc, new Rect(0, WindowHeight - 1, WindowWidth, WindowHeight), BorderColor);
                FillRect(hdc, new Rect(0, 0, 1, WindowHeight), BorderColor);
                FillRect(hdc, new Rect(WindowWidth - 1, 0, WindowWidth, WindowHeight), BorderColor);
                DrawTextLine(hdc, "Super Gang Garrison", new Rect(26, 7, 360, 24), WhiteColor, DtLeft | DtSingleLine | DtVCenter);
                DrawTextLine(hdc, _message, new Rect(26, 38, 360, 60), WhiteColor, DtLeft | DtSingleLine | DtVCenter);
                DrawProgressFrame(hdc);
                DrawProgressSegments(hdc);
            }
            finally
            {
                EndPaint(hwnd, ref paintStruct);
            }
        }

        private static void DrawProgressFrame(IntPtr hdc)
        {
            FillRect(hdc, new Rect(26, 61, 358, 62), BorderColor);
            FillRect(hdc, new Rect(26, 82, 358, 83), BorderColor);
            FillRect(hdc, new Rect(26, 61, 27, 83), BorderColor);
            FillRect(hdc, new Rect(357, 61, 358, 83), BorderColor);
            FillRect(hdc, new Rect(27, 62, 357, 82), BackgroundColor);
        }

        private void DrawProgressSegments(IntPtr hdc)
        {
            // Animated indeterminate bar: a small block of lit segments sweeps across the track.
            const int litWindow = 4;
            var offset = _animationStep % ProgressSegments;
            for (var index = 0; index < litWindow; index += 1)
            {
                var segment = (offset + index) % ProgressSegments;
                var left = 30 + (segment * 16);
                if (left + 10 > 356)
                {
                    continue;
                }

                FillRect(hdc, new Rect(left, 65, left + 10, 79), ProgressColor);
            }
        }

        private static void DrawTextLine(IntPtr hdc, string text, Rect rect, int color, int format)
        {
            _ = SetTextColor(hdc, color);
            _ = SetBkMode(hdc, 1);
            _ = DrawText(hdc, text.Length == 0 ? " " : text, text.Length == 0 ? 1 : text.Length, ref rect, format);
        }

        private static void FillRect(IntPtr hdc, Rect rect, int color)
        {
            var brush = CreateSolidBrush(color);
            try
            {
                _ = FillRect(hdc, ref rect, brush);
            }
            finally
            {
                DeleteObject(brush);
            }
        }

        private static int ColorRef(int red, int green, int blue)
        {
            return red | (green << 8) | (blue << 16);
        }

        private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClass(ref WndClass lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetTimer(IntPtr hWnd, uint nIDEvent, uint uElapse, IntPtr lpTimerFunc);

        [DllImport("user32.dll")]
        private static extern bool KillTimer(IntPtr hWnd, uint uIDEvent);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out Message lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref Message lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref Message lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hwnd, ref PaintStruct lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PaintStruct lpPaint);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(int colorRef);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr hdc, ref Rect lprc, IntPtr hbr);

        [DllImport("gdi32.dll")]
        private static extern int SetTextColor(IntPtr hdc, int crColor);

        [DllImport("gdi32.dll")]
        private static extern int SetBkMode(IntPtr hdc, int mode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int DrawText(IntPtr hdc, string lpchText, int cchText, ref Rect lprc, int format);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClass
        {
            public int style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            public IntPtr hdc;
            public bool fErase;
            public Rect rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public Rect(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }
        }
    }
}
