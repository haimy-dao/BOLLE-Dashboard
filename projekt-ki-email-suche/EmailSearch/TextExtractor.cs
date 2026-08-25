using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace EmailSearch;

public static class TextExtractor
{
    public static string ExtractText(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".pdf" => ExtractPdf(content),
                ".docx" => ExtractDocx(content),
                ".xlsx" => ExtractXlsx(content),
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            return $"[Extraktion fehlgeschlagen: {ex.Message}]";
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
