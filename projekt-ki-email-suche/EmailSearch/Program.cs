using EmailSearch;

Console.WriteLine($"Suche nach '{Config.SearchTerm}' im Postfach {Config.TargetMailbox} ...");

var accessToken = await GraphClient.GetAccessTokenAsync();

using var httpClient = new HttpClient();
var messages = await EmailSearchService.SearchMessagesAsync(
    httpClient, accessToken, Config.TargetMailbox, Config.SearchTerm);

if (messages.Count == 0)
{
    Console.WriteLine("Keine Treffer gefunden.");
    return;
}

Console.WriteLine($"{messages.Count} Treffer:\n");
foreach (var msg in messages)
{
    var sender = msg.From?.EmailAddress?.Address ?? "?";
    Console.WriteLine($"- {msg.ReceivedDateTime} | von {sender} | {msg.Subject}");

    if (!msg.HasAttachments)
    {
        continue;
    }

    var attachments = await AttachmentClient.GetAttachmentsAsync(httpClient, accessToken, Config.TargetMailbox, msg.Id);
    foreach (var attachment in attachments.Where(a => a.IsFileAttachment && a.ContentBytes is not null))
    {
        var bytes = Convert.FromBase64String(attachment.ContentBytes!);
        var result = TextExtractor.ExtractText(attachment.Name, bytes);
        var marker = result.Status switch
        {
            ExtractionStatus.Unsupported => "Dateiformat wird noch nicht unterstützt (z. B. Bild ohne OCR)",
            ExtractionStatus.Failed => $"Extraktion fehlgeschlagen: {result.Text}",
            _ => result.Text.Contains(Config.SearchTerm, StringComparison.OrdinalIgnoreCase)
                ? $"enthält '{Config.SearchTerm}'"
                : "kein Treffer im extrahierten Text",
        };
        Console.WriteLine($"    -> Anhang '{attachment.Name}': {marker}");
    }
}
