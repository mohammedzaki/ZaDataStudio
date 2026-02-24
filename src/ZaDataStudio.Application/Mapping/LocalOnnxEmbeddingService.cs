using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Text;
using System.Text.RegularExpressions;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Local ONNX-based text embedding service using all-MiniLM-L6-v2 model
/// Provides offline, cost-free semantic embeddings (384 dimensions)
/// </summary>
public class LocalOnnxEmbeddingService : ITextEmbeddingGenerationService
{
    private readonly InferenceSession _session;
    private readonly string _modelPath;
    private readonly int _maxTokens;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly int _padTokenId = 0;
    private readonly int _clsTokenId = 101;
    private readonly int _sepTokenId = 102;
    private readonly int _unkTokenId = 100;

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public LocalOnnxEmbeddingService(string modelPath, int maxTokens = 128)
    {
        if (!File.Exists(modelPath))
        {
            var absolutePath = Path.GetFullPath(modelPath);
            throw new FileNotFoundException(
                $"ONNX model not found at: {modelPath}\n" +
                $"Absolute path: {absolutePath}\n" +
                $"Please download the model from: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/tree/main/onnx");
        }

        _modelPath = modelPath;
        _maxTokens = maxTokens;

        try
        {
            Console.WriteLine($"Loading ONNX model from: {Path.GetFullPath(modelPath)}");
            _session = new InferenceSession(modelPath);
            Console.WriteLine($"✓ ONNX model loaded successfully. Max tokens: {maxTokens}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load ONNX model from {modelPath}: {ex.Message}", ex);
        }

        _vocabulary = LoadVocabulary();
    }

    /// <summary>
    /// Generate embeddings for multiple texts
    /// </summary>
    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<ReadOnlyMemory<float>>();

        foreach (var text in data)
        {
            var embedding = await GenerateEmbeddingAsync(text, kernel, cancellationToken);
            embeddings.Add(embedding);
        }

        return embeddings;
    }

    /// <summary>
    /// Generate embedding for a single text (convenience method)
    /// </summary>
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return await GenerateEmbeddingAsync(text, kernel: null, cancellationToken);
    }

    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // Add timeout protection
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout

        var embedding = await Task.Run(() =>
        {
            try
            {
                // Tokenize input text
                var tokens = Tokenize(text);

                // Create input tensors
                var inputIds = new DenseTensor<long>(new[] { 1, tokens.Length });
                var attentionMask = new DenseTensor<long>(new[] { 1, tokens.Length });
                var tokenTypeIds = new DenseTensor<long>(new[] { 1, tokens.Length });

                for (int i = 0; i < tokens.Length; i++)
                {
                    inputIds[0, i] = tokens[i];
                    attentionMask[0, i] = 1; // All tokens are valid
                    tokenTypeIds[0, i] = 0; // Single sentence
                }

                // Create input containers
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                    NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
                    NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
                };

                // Run inference
                using var results = _session.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();

                // Mean pooling over token embeddings
                var embeddingSize = outputTensor.Dimensions[2];
                var embedding = new float[embeddingSize];
                var validTokens = tokens.Length;

                for (int i = 0; i < validTokens; i++)
                {
                    for (int j = 0; j < embeddingSize; j++)
                    {
                        embedding[j] += outputTensor[0, i, j];
                    }
                }

                // Average and normalize
                for (int i = 0; i < embeddingSize; i++)
                {
                    embedding[i] /= validTokens;
                }

                // L2 normalization
                var norm = Math.Sqrt(embedding.Sum(x => x * x));
                for (int i = 0; i < embeddingSize; i++)
                {
                    embedding[i] /= (float)norm;
                }

                return new ReadOnlyMemory<float>(embedding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during ONNX inference for text '{text}': {ex.Message}");
                throw;
            }

        }, cts.Token);

        return embedding;
    }

    /// <summary>
    /// Tokenize text using WordPiece tokenization (simplified for all-MiniLM-L6-v2)
    /// </summary>
    private long[] Tokenize(string text)
    {
        // Basic preprocessing
        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " "); // Remove punctuation
        
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<long> { _clsTokenId }; // Start with [CLS]

        foreach (var word in words)
        {
            if (tokens.Count >= _maxTokens - 1) break; // Reserve space for [SEP]

            // Try to find the word in vocabulary
            if (_vocabulary.TryGetValue(word, out var tokenId))
            {
                tokens.Add(tokenId);
            }
            else
            {
                // WordPiece subword tokenization (simplified)
                var subwords = TokenizeSubwords(word);
                tokens.AddRange(subwords);
            }
        }

        tokens.Add(_sepTokenId); // End with [SEP]

        // Pad to max length
        while (tokens.Count < _maxTokens)
        {
            tokens.Add(_padTokenId);
        }

        return tokens.Take(_maxTokens).ToArray();
    }

    /// <summary>
    /// Simple subword tokenization (fallback for unknown words)
    /// </summary>
    private List<long> TokenizeSubwords(string word)
    {
        var tokens = new List<long>();
        var start = 0;

        while (start < word.Length)
        {
            var end = word.Length;
            var found = false;

            // Try progressively shorter substrings
            while (start < end)
            {
                var subword = start > 0 ? "##" + word.Substring(start, end - start) : word.Substring(start, end - start);
                
                if (_vocabulary.TryGetValue(subword, out var tokenId))
                {
                    tokens.Add(tokenId);
                    found = true;
                    break;
                }
                end--;
            }

            if (!found)
            {
                tokens.Add(_unkTokenId); // Unknown token
                start++;
            }
            else
            {
                start = end;
            }
        }

        return tokens;
    }

    /// <summary>
    /// Load BERT vocabulary (simplified - loads from vocab.txt)
    /// </summary>
    private Dictionary<string, int> LoadVocabulary()
    {
        var vocab = new Dictionary<string, int>();
        var vocabPath = Path.Combine(Path.GetDirectoryName(_modelPath)!, "vocab.txt");

        if (!File.Exists(vocabPath))
        {
            // Use basic vocabulary if vocab.txt not found
            Console.WriteLine($"Warning: vocab.txt not found at {vocabPath}. Using basic vocabulary.");
            return CreateBasicVocabulary();
        }

        try
        {
            var lines = File.ReadAllLines(vocabPath);
            for (int i = 0; i < lines.Length; i++)
            {
                vocab[lines[i].Trim()] = i;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load vocabulary: {ex.Message}. Using basic vocabulary.");
            return CreateBasicVocabulary();
        }

        return vocab;
    }

    /// <summary>
    /// Create a basic vocabulary as fallback
    /// </summary>
    private Dictionary<string, int> CreateBasicVocabulary()
    {
        var vocab = new Dictionary<string, int>
        {
            ["[PAD]"] = 0,
            ["[UNK]"] = 100,
            ["[CLS]"] = 101,
            ["[SEP]"] = 102,
            ["[MASK]"] = 103
        };

        // Add common English words
        var commonWords = new[] {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with",
            "is", "are", "was", "were", "be", "been", "being", "have", "has", "had",
            "do", "does", "did", "will", "would", "could", "should", "may", "might", "can",
            "not", "no", "yes", "i", "you", "he", "she", "it", "we", "they",
            "this", "that", "these", "those", "what", "which", "who", "when", "where", "why", "how",
            // Add domain-specific words
            "sport", "sports", "education", "technology", "health", "business", "finance",
            "volunteer", "volunteering", "support", "service", "community", "program",
            "data", "information", "system", "management", "project", "development"
        };

        int id = 104;
        foreach (var word in commonWords)
        {
            vocab[word] = id++;
        }

        return vocab;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
