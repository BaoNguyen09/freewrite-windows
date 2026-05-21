using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace FreewriteWindows;

internal static class BorderlessChrome
{
    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmncrpDisabled = 1;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// WindowChrome is defined in MainWindow.xaml. Only apply DWM tweaks here to avoid double HWND hooks.
    /// </summary>
    public static void Apply(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyDwmChrome(window);
            return;
        }

        window.SourceInitialized += OnSourceInitialized;
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.SourceInitialized -= OnSourceInitialized;
            ApplyDwmChrome(window);
        }
    }

    private static void ApplyDwmChrome(Window window)
    {
        DisableNonClientRendering(window);
        TrySetRoundedCorners(window);
    }

    private static void DisableNonClientRendering(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var policy = DwmncrpDisabled;
            _ = DwmSetWindowAttribute(handle, DwmwaNcRenderingPolicy, ref policy, sizeof(int));
        }
        catch
        {
            // DWM is optional; ignore if unavailable.
        }
    }

    private static void TrySetRoundedCorners(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var preference = DwmwcpRound;
            _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 and older builds may not support rounded window corners.
        }
    }
}
