#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

/// <summary>
/// Removes the <c>glFinish</c> that MonoGame DesktopGL issues after every
/// <see cref="Texture2D.SetData{T}(T[])"/> call, and counts those calls.
/// </summary>
/// <remarks>
/// MonoGame 3.8 DesktopGL ends <c>Texture2D.PlatformSetDataBody</c> with
/// <c>GL.Finish()</c> "to make sure that any texture uploads on a thread are completed
/// before the main thread tries to use the texture". On DesktopGL every GL call is
/// already marshalled to the UI thread and runs in the one GL context
/// (<c>Threading.BlockOnUIThread</c>), so GL command ordering alone guarantees the
/// upload is visible to later draws, and <c>glTexSubImage2D</c>/<c>glTexImage2D</c>
/// copy client memory before returning. The finish adds nothing but a full
/// CPU/GPU synchronisation: the CPU waits for every queued command, including frames
/// the driver is holding for VSync. With two or three frames queued, a single
/// upload can block for 30–50 ms, and the block is attributed to whichever stage
/// happened to upload (corpse dissolve rows, atlas pages, HUD textures).
///
/// Set <c>OG2_GL_TEXTURE_UPLOAD_FINISH=keep</c> to restore MonoGame's behaviour
/// for an A/B comparison; the wrapper then times each finish so frame captures show
/// how long the synchronisation cost.
///
/// Readbacks (<c>GetData</c>, which on DesktopGL is <c>glGetTexImage</c> of the
/// whole texture even for a sub-rectangle) must synchronise and are left alone, but
/// they are wrapped too so frame captures count them and time the wait.
/// </remarks>
internal static class DesktopGLTextureUploadSync
{
    public const string ModeEnvironmentVariable = "OG2_GL_TEXTURE_UPLOAD_FINISH";

    private delegate void NativeFinish();

    private static readonly bool KeepFinish = string.Equals(
        Environment.GetEnvironmentVariable(ModeEnvironmentVariable)?.Trim(),
        "keep",
        StringComparison.OrdinalIgnoreCase);

    private static FieldInfo? _finishField;
    private static Delegate? _installedWrapper;
    private static NativeFinish? _nativeFinish;
    private static int _frameFinishCalls;
    private static long _frameFinishTicks;
    private static int _frameReadbacks;
    private static long _frameReadbackTicks;
    private static readonly System.Collections.Generic.Dictionary<string, Delegate> InstalledReadbackWrappers = new(StringComparer.Ordinal);

    /// <summary>Human-readable result of the last <see cref="EnsureInstalled"/> call.</summary>
    public static string Status { get; private set; } = "not installed";

    /// <summary>True when the uploads no longer call <c>glFinish</c>.</summary>
    public static bool FinishSkipped => _installedWrapper is not null && !KeepFinish;

    /// <summary>
    /// Installs the wrapper over MonoGame's <c>GL.Finish</c> entry point. Call after
    /// the graphics device exists, and again if the device is recreated (MonoGame
    /// reloads its GL entry points with each context). Returns true when installed.
    /// </summary>
    public static bool EnsureInstalled()
    {
        if (OperatingSystem.IsBrowser())
        {
            Status = "browser host: not applicable";
            return false;
        }

        try
        {
            _finishField ??= typeof(Texture2D).Assembly
                .GetType("MonoGame.OpenGL.GL", throwOnError: false)?
                .GetField("Finish", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (_finishField is null
                || !typeof(Delegate).IsAssignableFrom(_finishField.FieldType)
                || _finishField.FieldType.GetMethod("Invoke") is not { } invoke
                || invoke.ReturnType != typeof(void)
                || invoke.GetParameters().Length != 0)
            {
                Status = "MonoGame GL.Finish entry point not found; uploads keep glFinish";
                return false;
            }

            var current = _finishField.GetValue(null) as Delegate;
            if (current is null)
            {
                Status = "GL entry points not loaded yet";
                return false;
            }

            if (ReferenceEquals(current, _installedWrapper))
            {
                return true;
            }

            var readbackStatus = InstallReadbackWrapper("GetTexImageInternal") & InstallReadbackWrapper("ReadPixelsInternal")
                ? "readbacks timed"
                : "readbacks not timed";

            // MonoGame builds the delegate from the driver's function pointer, so this
            // recovers the native glFinish for the "keep" mode.
            var nativePointer = Marshal.GetFunctionPointerForDelegate(current);
            _nativeFinish = nativePointer == IntPtr.Zero
                ? null
                : Marshal.GetDelegateForFunctionPointer<NativeFinish>(nativePointer);

            var wrapperMethod = typeof(DesktopGLTextureUploadSync).GetMethod(
                nameof(FinishWrapper),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            _installedWrapper = Delegate.CreateDelegate(_finishField.FieldType, wrapperMethod);
            _finishField.SetValue(null, _installedWrapper);
            Status = KeepFinish
                ? $"glFinish kept after texture uploads ({ModeEnvironmentVariable}=keep); finishes are timed; {readbackStatus}"
                : $"glFinish skipped after texture uploads; uploads are counted; {readbackStatus}";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"could not install texture upload wrapper ({ex.GetType().Name}: {ex.Message}); uploads keep glFinish";
            return false;
        }
    }

    /// <summary>
    /// Returns the texture uploads (MonoGame <c>SetData</c> calls) since the last call,
    /// and the wall time spent in <c>glFinish</c> for them (zero unless kept).
    /// </summary>
    public static void TakeFrameCounters(out int uploads, out double finishMilliseconds)
    {
        uploads = Interlocked.Exchange(ref _frameFinishCalls, 0);
        var ticks = Interlocked.Exchange(ref _frameFinishTicks, 0L);
        finishMilliseconds = ticks * 1000d / Stopwatch.Frequency;
    }

    /// <summary>
    /// Returns the GPU readbacks (<c>glGetTexImage</c>/<c>glReadPixels</c>) since the
    /// last call and the wall time they blocked for.
    /// </summary>
    public static void TakeReadbackCounters(out int readbacks, out double readbackMilliseconds)
    {
        readbacks = Interlocked.Exchange(ref _frameReadbacks, 0);
        var ticks = Interlocked.Exchange(ref _frameReadbackTicks, 0L);
        readbackMilliseconds = ticks * 1000d / Stopwatch.Frequency;
    }

    /// <summary>
    /// Replaces a MonoGame GL entry-point delegate with one that forwards every call to
    /// the original and adds the elapsed time to the readback counters.
    /// </summary>
    private static bool InstallReadbackWrapper(string fieldName)
    {
        try
        {
            var field = _finishField!.DeclaringType!.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is null
                || field.GetValue(null) is not Delegate original
                || field.FieldType.GetMethod("Invoke") is not { } invoke
                || invoke.GetParameters().Any(parameter => parameter.ParameterType.IsByRef))
            {
                return false;
            }

            if (InstalledReadbackWrappers.TryGetValue(fieldName, out var installed) && ReferenceEquals(installed, original))
            {
                return true;
            }

            var parameters = invoke.GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            var start = Expression.Variable(typeof(long), "start");
            var body = Expression.Block(
                invoke.ReturnType,
                new[] { start },
                Expression.Assign(start, Expression.Call(typeof(Stopwatch).GetMethod(nameof(Stopwatch.GetTimestamp))!)),
                Expression.TryFinally(
                    Expression.Invoke(Expression.Constant(original, field.FieldType), parameters),
                    Expression.Call(
                        typeof(DesktopGLTextureUploadSync).GetMethod(nameof(RecordReadback), BindingFlags.Static | BindingFlags.NonPublic)!,
                        start)));
            var wrapper = Expression.Lambda(field.FieldType, body, parameters).Compile();
            field.SetValue(null, wrapper);
            InstalledReadbackWrappers[fieldName] = wrapper;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RecordReadback(long startTimestamp)
    {
        Interlocked.Increment(ref _frameReadbacks);
        Interlocked.Add(ref _frameReadbackTicks, Stopwatch.GetTimestamp() - startTimestamp);
    }

    private static void FinishWrapper()
    {
        Interlocked.Increment(ref _frameFinishCalls);
        if (!KeepFinish || _nativeFinish is not { } finish)
        {
            return;
        }

        var start = Stopwatch.GetTimestamp();
        finish();
        Interlocked.Add(ref _frameFinishTicks, Stopwatch.GetTimestamp() - start);
    }
}
