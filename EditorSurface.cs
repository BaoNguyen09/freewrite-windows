using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FreewriteWindows;

internal static class EditorSurface
{
    private static readonly Thickness ZeroMargin = new(0);

    public static void EnsureDocument(RichTextBox editor)
    {
        if (editor.Document is not null)
        {
            NormalizeBlockSpacing(editor.Document);
            return;
        }

        editor.Document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
        };
        NormalizeBlockSpacing(editor.Document);
    }

    public static string GetText(RichTextBox editor)
    {
        EnsureDocument(editor);
        var range = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
        var text = range.Text;
        // FlowDocument adds a trailing paragraph break; keep user trailing newlines beyond that.
        if (text.Length >= 2 && text[^1] == '\n' && text[^2] == '\n')
        {
            return text[..^1];
        }

        return text;
    }

    public static void SetText(RichTextBox editor, string text)
    {
        EnsureDocument(editor);
        var range = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
        range.Text = text;
    }

    public static void MoveCaretToEnd(RichTextBox editor)
    {
        EnsureDocument(editor);
        editor.CaretPosition = editor.Document.ContentEnd;
    }

    public static void ApplyDocumentTypography(
        RichTextBox editor,
        FontFamily fontFamily,
        double fontSize,
        Brush? foreground)
    {
        EnsureDocument(editor);
        var document = editor.Document;
        document.FontFamily = fontFamily;
        document.FontSize = fontSize;
        document.LineHeight = EditorLayout.LineHeightForFontSize(fontSize);
        document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        NormalizeBlockSpacing(document);
        if (foreground is not null)
        {
            document.Foreground = foreground;
        }
    }

    public static void ApplyPlaceholderTypography(TextBlock placeholder, double fontSize)
    {
        var lineHeight = EditorLayout.LineHeightForFontSize(fontSize);
        placeholder.SetValue(Block.LineHeightProperty, lineHeight);
        placeholder.SetValue(Block.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight);
    }

    private static void NormalizeBlockSpacing(FlowDocument document)
    {
        foreach (var block in document.Blocks)
        {
            block.Margin = ZeroMargin;
            block.LineHeight = document.LineHeight;
            block.LineStackingStrategy = document.LineStackingStrategy;
        }
    }
}
