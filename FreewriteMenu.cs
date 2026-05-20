using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FreewriteWindows;

internal static class FreewriteMenu
{
    private const double ItemFontSize = 14;
    private static readonly Thickness ItemPadding = new(12, 8, 12, 8);

    public static ContextMenu Create(UIElement placementTarget, bool isDarkMode, double minWidth = 120, double maxWidth = 320)
    {
        return new ContextMenu
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Top,
            MinWidth = minWidth,
            MaxWidth = maxWidth,
            Padding = new Thickness(4),
            Background = MenuBackground(isDarkMode),
            BorderBrush = MenuBorder(isDarkMode),
            BorderThickness = new Thickness(1),
            Foreground = MenuForeground(isDarkMode, enabled: true),
            ItemContainerStyle = CreateMenuItemStyle(isDarkMode)
        };
    }

    public static MenuItem CreateItem(string label, Action? action, bool isDarkMode, bool enabled = true)
    {
        var item = new MenuItem
        {
            Header = label,
            IsEnabled = enabled && action is not null,
            FontSize = ItemFontSize,
            Padding = ItemPadding,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = MenuForeground(isDarkMode, enabled && action is not null),
            Icon = EmptyIcon(),
            StaysOpenOnClick = false
        };

        if (action is not null)
        {
            item.Click += (_, _) => action();
        }

        return item;
    }

    public static MenuItem CreateDivider(bool isDarkMode)
    {
        var lineColor = isDarkMode
            ? Color.FromRgb(48, 48, 48)
            : Color.FromRgb(230, 230, 230);

        return new MenuItem
        {
            IsEnabled = false,
            Focusable = false,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Icon = EmptyIcon(),
            Header = new Border
            {
                Height = 1,
                Margin = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(lineColor)
            }
        };
    }

    private static Style CreateMenuItemStyle(bool isDarkMode)
    {
        var hoverColor = isDarkMode
            ? Color.FromRgb(48, 48, 48)
            : Color.FromRgb(245, 245, 245);

        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(MenuItem.PaddingProperty, ItemPadding));
        style.Setters.Add(new Setter(MenuItem.IconProperty, EmptyIcon()));
        style.Setters.Add(new Setter(MenuItem.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new Thickness(0)));

        var highlightTrigger = new Trigger
        {
            Property = MenuItem.IsHighlightedProperty,
            Value = true
        };
        highlightTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(hoverColor)));
        style.Triggers.Add(highlightTrigger);
        return style;
    }

    private static Grid EmptyIcon() => new() { Width = 0, Height = 0, Visibility = Visibility.Collapsed };

    private static Brush MenuBackground(bool isDarkMode) =>
        isDarkMode ? new SolidColorBrush(Color.FromRgb(32, 32, 32)) : Brushes.White;

    private static Brush MenuBorder(bool isDarkMode) =>
        isDarkMode
            ? new SolidColorBrush(Color.FromRgb(48, 48, 48))
            : new SolidColorBrush(Color.FromRgb(230, 230, 230));

    private static Brush MenuForeground(bool isDarkMode, bool enabled)
    {
        if (!enabled)
        {
            return isDarkMode
                ? new SolidColorBrush(Color.FromRgb(110, 110, 110))
                : new SolidColorBrush(Color.FromRgb(136, 136, 136));
        }

        return isDarkMode
            ? new SolidColorBrush(Color.FromRgb(204, 204, 204))
            : new SolidColorBrush(Color.FromRgb(34, 34, 34));
    }
}
