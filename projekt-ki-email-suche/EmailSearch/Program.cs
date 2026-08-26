using EmailSearch;

if (args.Contains("--test-embeddings"))
{
    await RunEmbeddingSelfTestAsync();
    return;
}

if (args.Contains("--test-summary"))
{
    RunSummarySelfTest();
    return;
}

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

var useSemantic = args.Contains("--semantic");
EmbeddingService? embeddings = null;
float[]? queryEmbedding = null;
if (useSemantic)
{
    Console.WriteLine("Lade Embedding-Modell (einmalig, kann beim ersten Mal etwas dauern) ...");
    var modelDir = Path.Combine(AppContext.BaseDirectory, "Models");
    embeddings = await EmbeddingService.CreateAsync(modelDir);
    queryEmbedding = embeddings.Embed(Config.SemanticQuery);
    Console.WriteLine($"Semantische Vergleichsanfrage: \"{Config.SemanticQuery}\"\n");
}

var useSummarize = args.Contains("--summarize");
SummaryService? summaries = null;
if (useSummarize)
{
    Console.WriteLine("Lade Zusammenfassungs-Modell (Phi-3.5-mini, einmalig, kann dauern) ...");
    var phiDir = Path.Combine(AppContext.BaseDirectory, "Models", "phi-3.5-mini");
    summaries = new SummaryService(phiDir);
}

var threads = messages
    .GroupBy(m => m.ConversationId)
    .Select(g => g.OrderBy(m => m.ReceivedDateTime).ToList())
    .OrderByDescending(thread => thread.Max(m => m.ReceivedDateTime))
    .ToList();

var limitArg = args.FirstOrDefault(a => a.StartsWith("--limit="));
if (limitArg is not null && int.TryParse(limitArg["--limit=".Length..], out var limit))
{
    threads = threads.Take(limit).ToList();
    Console.WriteLine($"(Begrenzt auf {limit} Konversation(en) zum Testen)\n");
}

Console.WriteLine($"{messages.Count} Treffer in {threads.Count} Konversation(en):\n");
foreach (var thread in threads)
{
    var first = thread[0];
    Console.WriteLine($"=== Konversation ({thread.Count} Mail(s)) – \"{first.Subject}\" ===");

    var threadTextBuilder = new System.Text.StringBuilder();

    foreach (var msg in thread)
    {
        var sender = msg.From?.EmailAddress?.Address ?? "?";
        var scorePrefix = "";
        if (embeddings is not null && queryEmbedding is not null)
        {
            var messageText = $"{msg.Subject} {msg.BodyPreview}".Trim();
            var similarity = EmbeddingService.CosineSimilarity(embeddings.Embed(messageText), queryEmbedding);
            scorePrefix = $"[{similarity:F2}] ";
        }

        Console.WriteLine($"  - {scorePrefix}{msg.ReceivedDateTime:yyyy-MM-dd HH:mm} | von {sender} | {msg.Subject}");

        if (useSummarize)
        {
            var cleanBody = ThreadPreprocessor.StripQuotedText(msg.BodyPreview ?? string.Empty);
            threadTextBuilder.AppendLine($"[{msg.ReceivedDateTime:yyyy-MM-dd}] {sender}: {msg.Subject}\n{cleanBody}");
        }

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
            Console.WriteLine($"      -> Anhang '{attachment.Name}': {marker}");

            if (useSummarize && result.Status == ExtractionStatus.Ok)
            {
                var trimmed = result.Text.Length > 1500 ? result.Text[..1500] : result.Text;
                threadTextBuilder.AppendLine($"[Anhang: {attachment.Name}]\n{trimmed}");
            }
        }
    }

    if (useSummarize && summaries is not null)
    {
        var threadText = threadTextBuilder.ToString();
        var dates = ThreadPreprocessor.ExtractDates(threadText);
        var amounts = ThreadPreprocessor.ExtractAmounts(threadText);

        Console.WriteLine("\n  --- KI-Zusammenfassung ---");
        if (dates.Count > 0)
        {
            Console.WriteLine($"  Gefundene Daten: {string.Join(", ", dates)}");
        }

        if (amounts.Count > 0)
        {
            Console.WriteLine($"  Gefundene Beträge: {string.Join(", ", amounts)}");
        }

        Console.WriteLine("  " + summaries.Summarize(threadText).Replace("\n", "\n  "));
    }

    Console.WriteLine();
}

summaries?.Dispose();

embeddings?.Dispose();

static void RunSummarySelfTest()
{
    Console.WriteLine("Offline-Selbsttest der Zusammenfassungs-Pipeline (ohne Graph API) ...\n");
    var phiDir = Path.Combine(AppContext.BaseDirectory, "Models", "phi-3.5-mini");
    using var summaries = new SummaryService(phiDir);

    var sampleThread =
        "[2026-08-10] d.fiege@bolle.net: 12774 Hünxe, Interimscontainer - Anfrage Nachtragsangebot\n" +
        "Hallo Herr Praast, bitte senden Sie uns ein Nachtragsangebot für die Deckendurchführung zwischen den Modulen, Bauvorhaben 12774 Hünxe Waldschule. Termin ist der 20.08.2026.\n\n" +
        "[2026-08-12] tino.praast@pwk-tischler.de: AW: 12774 Hünxe, Interimscontainer - Anfrage Nachtragsangebot\n" +
        "Guten Tag, anbei das Angebot über 3.450,00 € netto für die Deckendurchführung. Ausführung ist bis zum 18.08.2026 möglich.\n\n" +
        "[2026-08-13] d.fiege@bolle.net: AW: 12774 Hünxe, Interimscontainer - Anfrage Nachtragsangebot\n" +
        "Vielen Dank, das Angebot ist freigegeben. Bitte mit der Ausführung wie vorgeschlagen beginnen.";

    Console.WriteLine("Beispiel-Thread:\n" + sampleThread + "\n");
    Console.WriteLine("Zusammenfassung:");
    Console.WriteLine(summaries.Summarize(sampleThread));
}

static async Task RunEmbeddingSelfTestAsync()
{
    Console.WriteLine("Offline-Selbsttest der Embedding-Pipeline (ohne Graph API) ...\n");
    var modelDir = Path.Combine(AppContext.BaseDirectory, "Models");
    using var embeddings = await EmbeddingService.CreateAsync(modelDir);

    var query = "Projekt 12774 Hünxe Waldschule";
    var candidates = new[]
    {
        "AW: 12774 Hünxe, Interimcontaineranlage Waldschule - Rechnungsversand",
        "Bauprojekt Interimscontainer für die Förderschule Waldschule in Hünxe, Bauvorhaben 12774",
        "Einladung zum Firmen-Sommerfest am Freitag",
        "Ich hätte gerne eine Pizza mit extra Käse",
    };

    var queryEmbedding = embeddings.Embed(query);
    Console.WriteLine($"Anfrage: \"{query}\"\n");
    foreach (var candidate in candidates)
    {
        var score = EmbeddingService.CosineSimilarity(embeddings.Embed(candidate), queryEmbedding);
        Console.WriteLine($"[{score:F3}] {candidate}");
    }
}
