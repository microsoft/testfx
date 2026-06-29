// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VideoRecorder;

internal sealed partial class FfmpegVideoRecorder
{
    // Resolves the screen rectangle of the window to capture so gdigrab can record just that
    // region. Candidates are tried in order: the process main window (a GUI app under test owns
    // it), then the foreground window (the terminal you launched from — this is what makes
    // Windows Terminal work, since its window isn't owned by the test process), then the console
    // window (classic conhost). Returns false when none is a usable visible window, in which case
    // the caller falls back to full-screen capture.
    private static bool TryGetCurrentProcessWindowRegion(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;

#if NETCOREAPP
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
#endif

        // gdigrab captures physical pixels, so we must read window rectangles in physical
        // coordinates too. Make this thread Per-Monitor-V2 DPI aware while querying; otherwise a
        // DPI-unaware process gets virtualized (logical) coordinates and the region is wrong on
        // any scaled display. Restore the previous context afterwards.
        IntPtr previousDpiContext = TrySetPerMonitorDpiAwareThread();
        try
        {
            int screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            int screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

            foreach (IntPtr handle in EnumerateCandidateWindows())
            {
                if (handle == IntPtr.Zero
                    || !NativeMethods.IsWindowVisible(handle)
                    || !NativeMethods.GetWindowRect(handle, out NativeMethods.RECT rect))
                {
                    continue;
                }

                int left = Math.Max(0, rect.Left);
                int top = Math.Max(0, rect.Top);
                int right = screenWidth > 0 ? Math.Min(rect.Right, screenWidth) : rect.Right;
                int bottom = screenHeight > 0 ? Math.Min(rect.Bottom, screenHeight) : rect.Bottom;

                // gdigrab requires even dimensions for the yuv420p pixel format.
                int candidateWidth = (right - left) & ~1;
                int candidateHeight = (bottom - top) & ~1;
                if (candidateWidth <= 0 || candidateHeight <= 0)
                {
                    continue;
                }

                x = left;
                y = top;
                width = candidateWidth;
                height = candidateHeight;
                return true;
            }
        }
        finally
        {
            if (previousDpiContext != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.SetThreadDpiAwarenessContext(previousDpiContext);
                }
                catch (Exception)
                {
                    // Best effort; the override is per-thread and short-lived.
                }
            }
        }

        return false;
    }

    private static IntPtr TrySetPerMonitorDpiAwareThread()
    {
        try
        {
            return NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch (Exception)
        {
            // SetThreadDpiAwarenessContext is unavailable before Windows 10 1607; proceed without it.
            return IntPtr.Zero;
        }
    }

    private static IEnumerable<IntPtr> EnumerateCandidateWindows()
    {
        // A GUI app under test owns its main window.
        using (var current = Process.GetCurrentProcess())
        {
            yield return current.MainWindowHandle;
        }

        // The window you launched from (e.g. Windows Terminal), whose window is not owned by the
        // test process. Captured at record time.
        yield return NativeMethods.GetForegroundWindow();

        // A classic console (conhost) window owned by the console host.
        yield return NativeMethods.GetConsoleWindow();
    }

    private static class NativeMethods
    {
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
