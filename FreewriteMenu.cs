using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FreewriteWindows;

internal static class FreewriteMenu
{
    private const double ItemFontSize = 14;
    private static readonly Thickness ItemPadding = new(12, 8, 12, 8);

    public static ContextMenu Create(UIElement placementTarget, bool isDarkMode, double minWidth = 120, double maxWidth = 320)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Top,
            MinWidth = minWidth,
            MaxWidth = maxWidth,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            HasDropShadow = false
        };

        menu.Template = CreateMenuTemplate(isDarkMode);
        menu.ItemContainerStyle = CreateItemStyle(isDarkMode);
        return menu;
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
            Foreground = MenuForeground(isDarkMode, enabled && action is not null)
        };

        if (action is not null)
        {
            item.Click += (_, _) => action();
        }

        item.Template = CreateItemTemplate(isDarkMode);
        return item;
    }

    public static Separator CreateDivider(bool isDarkMode)
    {
        return new Separator
        {
            Margin = new Thickness(0),
            Height = 1,
            Background = isDarkMode
                ? new SolidColorBrush(Color.FromRgb(48, 48, 48))
                : new SolidColorBrush(Color.FromRgb(230, 230, 230))
        };
    }

    private static ControlTemplate CreateMenuTemplate(bool isDarkMode)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.PaddingProperty, new Thickness(0));
        border.SetValue(Border.SnapsToDevicePixelsProperty, true);
        border.SetValue(Border.BackgroundProperty, MenuBackground(isDarkMode));
        border.SetValue(Border.BorderBrushProperty, MenuBorder(isDarkMode));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.EffectProperty, new DropShadowEffect
        {
            BlurRadius = 8,
            ShadowDepth = 2,
            Opacity = 0.12,
            Direction = 270,
            Color = Colors.Black
        });

        var itemsHost = new FrameworkElementFactory(typeof(StackPanel));
        itemsHost.SetValue(StackPanel.IsItemsHostProperty, true);
        border.AppendChild(itemsHost);

        return new ControlTemplate(typeof(ContextMenu))
        {
            VisualTree = border
        };
    }

    private static Style CreateItemStyle(bool isDarkMode)
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(Control.FontSizeProperty, ItemFontSize));
        style.Setters.Add(new Setter(Control.PaddingProperty, ItemPadding));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, MenuForeground(isDarkMode, true)));
        style.Setters.Add(new Setter(Control.TemplateProperty, CreateItemTemplate(isDarkMode)));
        return style;
    }

    private static ControlTemplate CreateItemTemplate(bool isDarkMode)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "ItemBorder";
        border.SetValue(Border.PaddingProperty, ItemPadding);
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentSourceProperty, nameof(MenuItem.Header));
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(MenuItem))
        {
            VisualTree = border
        };

        var hover = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, MenuHoverBackground(isDarkMode), "ItemBorder"));
        template.Triggers.Add(hover);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Control.ForegroundProperty, MenuForeground(isDarkMode, false)));
        template.Triggers.Add(disabled);

        return template;
    }

    private static Brush MenuBackground(bool isDarkMode) =>
        isDarkMode ? new SolidColorBrush(Color.FromRgb(32, 32, 32)) : Brushes.White;

    private static Brush MenuBorder(bool isDarkMode) =>
        isDarkMode
            ? new SolidColorBrush(Color.FromRgb(48, 48, 48))
            : new SolidColorBrush(Color.FromRgb(230, 230, 230));

    private static Brush MenuHoverBackground(bool isDarkMode) =>
        isDarkMode
            ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(18, 0, 0, 0));

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
