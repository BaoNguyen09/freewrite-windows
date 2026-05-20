using System.Windows.Input;

namespace FreewriteWindows;

internal static class FreewriteCursors
{
    // Packaged .cur was ICO data and could crash WPF on load; use system cursor + CaretBrush in dark mode.
    public static Cursor LightIBeam => Cursors.IBeam;
}
