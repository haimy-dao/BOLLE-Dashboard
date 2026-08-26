using FastBertTokenizer;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace EmailSearch;

public sealed class EmbeddingService : IDisposable
{
    private readonly BertTokenizer _tokenizer;
    private readonly InferenceSession _session;

    private EmbeddingService(BertTokenizer tokenizer, InferenceSession session)
    {
        _tokenizer = tokenizer;
        _session = session;
    }

    public static async Task<EmbeddingService> CreateAsync(string modelDir)
    {
        var tokenizer = new BertTokenizer();
        await tokenizer.LoadVocabularyAsync(Path.Combine(modelDir, "vocab.txt"), convertInputToLowercase: false);
        var session = new InferenceSession(Path.Combine(modelDir, "model.onnx"));
        return new EmbeddingService(tokenizer, session);
    }

    public float[] Embed(string text)
    {
        var (inputIds, attentionMask, _) = _tokenizer.Encode(text, maximumTokens: 256);

        var idsArray = inputIds.ToArray();
        var maskArray = attentionMask.ToArray();
        var length = idsArray.Length;

        var inputIdsTensor = new DenseTensor<long>(idsArray, new[] { 1, length });
        var attentionMaskTensor = new DenseTensor<long>(maskArray, new[] { 1, length });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
        };

        using var results = _session.Run(inputs);
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        var hiddenSize = lastHiddenState.Dimensions[2];

        // Mean-Pooling über alle nicht-gepaddeten Tokens (Standardverfahren bei sentence-transformers-Modellen)
        var pooled = new float[hiddenSize];
        var validTokens = 0;
        for (var t = 0; t < length; t++)
        {
            if (maskArray[t] == 0)
            {
                continue;
            }

            validTokens++;
            for (var h = 0; h < hiddenSize; h++)
            {
                pooled[h] += lastHiddenState[0, t, h];
            }
        }

        var divisor = Math.Max(validTokens, 1);
        for (var h = 0; h < hiddenSize; h++)
        {
            pooled[h] /= divisor;
        }

        Normalize(pooled);
        return pooled;
    }

    public static float CosineSimilarity(float[] a, float[] b)
    {
        var dot = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return dot; // Vektoren sind bereits L2-normalisiert -> Skalarprodukt = Kosinus-Ähnlichkeit
    }

    private static void Normalize(float[] vector)
    {
        var normSquared = 0.0;
        foreach (var v in vector)
        {
            normSquared += (double)v * v;
        }

        var norm = Math.Sqrt(normSquared);
        if (norm <= 0)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
