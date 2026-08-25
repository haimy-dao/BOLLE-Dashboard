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
    if (msg.HasAttachments)
    {
        Console.WriteLine("    -> hat Anhänge (Inhalt wird noch nicht ausgelesen, Schritt 2)");
    }
}
