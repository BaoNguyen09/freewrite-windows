using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace FreewriteWindows;

public static class PdfExporter
{
    private const double PageWidth = 612;
    private const double PageHeight = 792;
    private const double Margin = 72;
    private const double FontSize = 12;
    private const double LineHeight = 18;

    public static void Export(WindowOwner owner, HumanEntry entry, string content)
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "PDF file (*.pdf)|*.pdf",
            FileName = ExtractTitle(content, entry.Date) + ".pdf",
            AddExtension = true,
            DefaultExt = ".pdf"
        };

        if (saveDialog.ShowDialog(owner.Window) != true)
        {
            return;
        }

        File.WriteAllBytes(saveDialog.FileName, CreatePdf(content));
    }

    public static string ExtractTitle(string content, string date)
    {
        var words = content
            .Trim()
            .ReplaceLineEndings(" ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}', '<', '>').ToLowerInvariant())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Take(4)
            .ToArray();

        return words.Length == 0 ? $"Entry {date}" : string.Join("-", words);
    }

    private static byte[] CreatePdf(string content)
    {
        var pages = WrapLines(NormalizeForPdf(content), 82)
            .Chunk(36)
            .Select(chunk => chunk.ToList())
            .ToList();

        if (pages.Count == 0)
        {
            pages.Add(new List<string> { string.Empty });
        }

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"
        };

        var kids = Enumerable.Range(0, pages.Count)
            .Select(pageIndex => $"{3 + pageIndex * 2} 0 R")
            .ToArray();
        objects.Add($"<< /Type /Pages /Kids [{string.Join(" ", kids)}] /Count {pages.Count} >>");

        for (var i = 0; i < pages.Count; i++)
        {
            var pageObjectNumber = 3 + i * 2;
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth.ToString(CultureInfo.InvariantCulture)} {PageHeight.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {contentObjectNumber} 0 R >>");
            objects.Add(CreateContentObject(pages[i]));
        }

        var builder = new StringBuilder();
        builder.AppendLine("%PDF-1.4");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.AppendLine($"{i + 1} 0 obj");
            builder.AppendLine(objects[i]);
            builder.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.AppendLine("xref");
        builder.AppendLine($"0 {objects.Count + 1}");
        builder.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            builder.AppendLine($"{offset:0000000000} 00000 n ");
        }

        builder.AppendLine("trailer");
        builder.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        builder.AppendLine("startxref");
        builder.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string CreateContentObject(IReadOnlyList<string> lines)
    {
        var stream = new StringBuilder();
        stream.AppendLine("BT");
        stream.AppendLine($"/F1 {FontSize.ToString(CultureInfo.InvariantCulture)} Tf");
        stream.AppendLine($"{Margin.ToString(CultureInfo.InvariantCulture)} {(PageHeight - Margin).ToString(CultureInfo.InvariantCulture)} Td");
        foreach (var line in lines)
        {
            stream.AppendLine($"({EscapePdfText(line)}) Tj");
            stream.AppendLine($"0 -{LineHeight.ToString(CultureInfo.InvariantCulture)} Td");
        }
        stream.AppendLine("ET");

        var text = stream.ToString();
        var length = Encoding.ASCII.GetByteCount(text);
        return $"<< /Length {length} >>\nstream\n{text}endstream";
    }

    private static IEnumerable<string> WrapLines(string content, int maxChars)
    {
        foreach (var paragraph in content.ReplaceLineEndings("\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                yield return string.Empty;
                continue;
            }

            var current = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length == 0)
                {
                    current.Append(word);
                }
                else if (current.Length + word.Length + 1 <= maxChars)
                {
                    current.Append(' ').Append(word);
                }
                else
                {
                    yield return current.ToString();
                    current.Clear();
                    current.Append(word);
                }
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }
    }

    private static string NormalizeForPdf(string content)
    {
        return content
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2026', '.')
            .Select(ch => ch is >= ' ' and <= '~' or '\n' or '\r' or '\t' ? ch : '?')
            .Aggregate(new StringBuilder(), (builder, ch) => builder.Append(ch))
            .ToString();
    }

    private static string EscapePdfText(string text)
    {
        return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}

public sealed class WindowOwner
{
    public WindowOwner(System.Windows.Window window)
    {
        Window = window;
    }

    public System.Windows.Window Window { get; }
}
