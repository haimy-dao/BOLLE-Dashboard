using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace EmailSearch;

public enum ExtractionStatus
{
    Ok,
    Unsupported,
    Failed,
}

public record ExtractionResult(ExtractionStatus Status, string Text);

public static class TextExtractor
{
    private static readonly HashSet<string> PlainTextExtensions = new() { ".xml", ".txt", ".csv", ".json", ".html", ".htm" };

    public static ExtractionResult ExtractText(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".pdf" => new ExtractionResult(ExtractionStatus.Ok, ExtractPdf(content)),
                ".docx" => new ExtractionResult(ExtractionStatus.Ok, ExtractDocx(content)),
                ".xlsx" => new ExtractionResult(ExtractionStatus.Ok, ExtractXlsx(content)),
                _ when PlainTextExtensions.Contains(extension) => new ExtractionResult(ExtractionStatus.Ok, Encoding.UTF8.GetString(content)),
                _ => new ExtractionResult(ExtractionStatus.Unsupported, string.Empty),
            };
        }
        catch (Exception ex)
        {
            return new ExtractionResult(ExtractionStatus.Failed, ex.Message);
        }
    }

    private static string ExtractPdf(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    private static string ExtractDocx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(stream, false);
        return doc.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }

    private static string ExtractXlsx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sb = new StringBuilder();
        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var cell in worksheet.CellsUsed())
            {
                sb.Append(cell.GetString()).Append(' ');
            }
        }
        return sb.ToString();
    }
}
