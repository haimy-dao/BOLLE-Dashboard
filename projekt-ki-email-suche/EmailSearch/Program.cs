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
        var text = TextExtractor.ExtractText(attachment.Name, bytes);
        var found = text.Contains(Config.SearchTerm, StringComparison.OrdinalIgnoreCase);
        var marker = found ? $"enthält '{Config.SearchTerm}'" : "kein Treffer im Text (evtl. gescannt/Bild ohne OCR)";
        Console.WriteLine($"    -> Anhang '{attachment.Name}': {marker}");
    }
}
