using System.Windows;
using System.Windows.Controls;

namespace FreewriteWindows;

internal static class EditorLayout
{
    public const double ColumnMinWidth = 650;
    public const double ColumnMaxWidth = 880;
    public const double ColumnWidthRatio = 0.78;
    public const double TextPaddingLeft = 16;
    public const double TextPaddingRight = 48;
    public const double ScrollbarGutter = 28;

    public static double LineHeightForFontSize(double fontSize) => fontSize * 1.5;

    public static double ResolveColumnWidth(double viewportWidth, double fallbackWidth)
    {
        var available = viewportWidth > 200 ? viewportWidth : fallbackWidth;
        return Math.Clamp(available * ColumnWidthRatio, ColumnMinWidth, ColumnMaxWidth);
    }

    public static void ApplyScrollGutter(ScrollViewer? scrollViewer)
    {
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.Padding = new Thickness(0, 0, ScrollbarGutter, 0);
        scrollViewer.Margin = new Thickness(0);
    }
}
