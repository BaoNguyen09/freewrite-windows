using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FreewriteWindows;

internal static class WindowZOrderDebug
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static bool IsForegroundWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        return GetForegroundWindow() == hwnd;
    }

    public static void BringToForeground(Window window)
    {
        window.Activate();
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetForegroundWindow(hwnd);
        }
    }
}
