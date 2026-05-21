using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FreewriteWindows;

internal static class WindowFullscreen
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoActivate = 0x0010;
    private const int SwMinimize = 6;
    private const int WmSysCommand = 0x0112;
    private const int ScMinimize = 0xF020;
    private static readonly IntPtr HwndTop = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RectNative rect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static bool TryGetMonitorPixelBounds(Window window, out Rect pixelBounds)
    {
        pixelBounds = default;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        pixelBounds = new Rect(
            info.rcMonitor.Left,
            info.rcMonitor.Top,
            info.rcMonitor.Right - info.rcMonitor.Left,
            info.rcMonitor.Bottom - info.rcMonitor.Top);
        return true;
    }

    public static bool TryGetWindowPixelBounds(Window window, out Rect pixelBounds)
    {
        pixelBounds = default;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        pixelBounds = new Rect(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);
        return pixelBounds.Width > 0 && pixelBounds.Height > 0;
    }

    public static void ApplyToMonitor(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !TryGetMonitorPixelBounds(window, out var pixelBounds))
        {
            window.WindowState = WindowState.Maximized;
            return;
        }

        window.WindowState = WindowState.Normal;
        if (!SetWindowPos(
                hwnd,
                HwndTop,
                (int)pixelBounds.X,
                (int)pixelBounds.Y,
                (int)pixelBounds.Width,
                (int)pixelBounds.Height,
                SwpShowWindow))
        {
            ApplyBoundsInDips(window, pixelBounds);
        }
    }

    public static void RestoreBounds(Window window, Rect pixelBounds)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        window.WindowState = WindowState.Normal;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(
                hwnd,
                HwndTop,
                (int)pixelBounds.X,
                (int)pixelBounds.Y,
                (int)pixelBounds.Width,
                (int)pixelBounds.Height,
                SwpShowWindow);
        }

        ApplyBoundsInDips(window, pixelBounds);
    }

    public static bool IsMinimized(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return window.WindowState == WindowState.Minimized
            || (hwnd != IntPtr.Zero && IsIconic(hwnd));
    }

    public static bool TryMinimize(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.WindowState = WindowState.Minimized;
            return window.WindowState == WindowState.Minimized;
        }

        ShowWindow(hwnd, SwMinimize);
        if (!IsIconic(hwnd))
        {
            SendMessage(hwnd, WmSysCommand, (IntPtr)ScMinimize, IntPtr.Zero);
        }

        if (!IsIconic(hwnd))
        {
            window.WindowState = WindowState.Minimized;
        }

        var minimized = IsMinimized(window);
        // #region agent log
        DebugSessionLog.Write(
            "WindowFullscreen.cs:TryMinimize",
            "minimize result",
            new
            {
                hwnd = hwnd.ToInt64(),
                isIconic = IsIconic(hwnd),
                windowState = window.WindowState.ToString(),
                minimized,
            },
            "E",
            "verify-6");
        // #endregion
        return minimized;
    }

    public static void ParkMinimizedOffScreen(Window window, double widthDip, double heightDip)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !IsIconic(hwnd))
        {
            return;
        }

        var widthPx = Math.Max(200, (int)Math.Round(widthDip * GetDpiScale(window)));
        var heightPx = Math.Max(150, (int)Math.Round(heightDip * GetDpiScale(window)));
        SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, widthPx, heightPx, SwpNoActivate);
    }

    public static void BringToForeground(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.Activate();
            return;
        }

        ShowWindow(hwnd, 9);
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
        window.Activate();
    }

    private static double GetDpiScale(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            return 1;
        }

        return source.CompositionTarget.TransformToDevice.M11;
    }

    private static void ApplyBoundsInDips(Window window, Rect pixelBounds)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            window.Left = pixelBounds.X;
            window.Top = pixelBounds.Y;
            window.Width = pixelBounds.Width;
            window.Height = pixelBounds.Height;
            return;
        }

        var toDip = source.CompositionTarget.TransformFromDevice;
        var topLeft = toDip.Transform(new Point(pixelBounds.X, pixelBounds.Y));
        var bottomRight = toDip.Transform(new Point(pixelBounds.Right, pixelBounds.Bottom));
        window.Left = topLeft.X;
        window.Top = topLeft.Y;
        window.Width = Math.Max(1, bottomRight.X - topLeft.X);
        window.Height = Math.Max(1, bottomRight.Y - topLeft.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public RectNative rcMonitor;
        public RectNative rcWork;
        public uint dwFlags;
    }
}
