using System.Runtime.InteropServices;

using System.Windows;

using System.Windows.Interop;

using System.Windows.Media;



namespace FreewriteWindows;



internal static class WindowFullscreen

{

    private const uint MonitorDefaultToNearest = 2;

    private const uint SwpShowWindow = 0x0040;

    private const int SwMinimize = 6;

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



    public static void Minimize(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero && ShowWindow(hwnd, SwMinimize))
        {
            return;
        }

        window.WindowState = WindowState.Minimized;
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


