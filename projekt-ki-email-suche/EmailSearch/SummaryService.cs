using System.Text;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace EmailSearch;

public sealed class SummaryService : IDisposable
{
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;

    public SummaryService(string modelDir)
    {
        _model = new Model(modelDir);
        _tokenizer = new Tokenizer(_model);
    }

    private const int MaxThreadChars = 6000; // Sicherheitsgrenze, damit die Inferenz auf CPU nicht ausufert
    private const int MaxNewTokens = 400;

    public string Summarize(string threadText)
    {
        if (threadText.Length > MaxThreadChars)
        {
            threadText = threadText[..MaxThreadChars];
        }

        var prompt =
            "<|system|>\n" +
            "Du bist ein Assistent, der E-Mail-Konversationen aus der Bauprojektabwicklung auf Deutsch knapp zusammenfasst. " +
            "Nenne die wichtigsten Fakten (Entscheidungen, offene Punkte, Termine, Beträge). Maximal 5 Sätze.<|end|>\n" +
            "<|user|>\n" + threadText + "<|end|>\n" +
            "<|assistant|>\n";

        using var sequences = _tokenizer.Encode(prompt);
        var promptTokenCount = sequences[0].Length;

        using var generatorParams = new GeneratorParams(_model);
        generatorParams.SetSearchOption("max_length", promptTokenCount + MaxNewTokens);
        generatorParams.SetSearchOption("temperature", 0.2);

        using var generator = new Generator(_model, generatorParams);
        generator.AppendTokenSequences(sequences);

        var sb = new StringBuilder();
        using var stream = _tokenizer.CreateStream();
        while (!generator.IsDone())
        {
            generator.GenerateNextToken();
            var tokenIds = generator.GetSequence(0);
            var newTokenId = tokenIds[^1];
            sb.Append(stream.Decode(newTokenId));
        }

        return sb.ToString().Trim();
    }

    public void Dispose()
    {
        _tokenizer.Dispose();
        _model.Dispose();
    }
}
