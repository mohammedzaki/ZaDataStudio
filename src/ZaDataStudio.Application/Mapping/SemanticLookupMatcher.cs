using Microsoft.SemanticKernel.Embeddings;
using System.Numerics.Tensors;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// AI-powered semantic matching for lookup values using OpenAI embeddings
/// Helps identify semantically similar values (e.g., "Sports Volunteering" matches "sport")
/// </summary>
public class SemanticLookupMatcher
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly double _similarityThreshold;

    public SemanticLookupMatcher(
        ITextEmbeddingGenerationService embeddingService, 
        double similarityThreshold = 0.75)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _similarityThreshold = similarityThreshold;
    }

    /// <summary>
    /// Find best semantic match for a source value from destination values
    /// </summary>
    /// <param name="sourceValue">The value to match</param>
    /// <param name="destinationValues">Possible matches</param>
    /// <param name="cancellationToken">Cancellation token for long-running operations</param>
    /// <returns>Best match and its similarity score (0-1)</returns>
    public async Task<(string? BestMatch, double Similarity)> FindBestMatchAsync(
        string sourceValue, 
        IEnumerable<string> destinationValues,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceValue) || !destinationValues.Any())
            return (null, 0);

        try
        {
            // Yield to allow UI updates in web scenarios
            await Task.Yield();

            // Generate embedding for source value
            var sourceEmbedding = await _embeddingService.GenerateEmbeddingAsync(sourceValue, cancellationToken: cancellationToken);

            // Generate embeddings for all destination values
            var destinationEmbeddings = new Dictionary<string, ReadOnlyMemory<float>>();
            foreach (var destValue in destinationValues.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    destinationEmbeddings[destValue] = await _embeddingService.GenerateEmbeddingAsync(destValue, cancellationToken: cancellationToken);

                    // Yield periodically for UI responsiveness
                    if (destinationEmbeddings.Count % 5 == 0)
                        await Task.Yield();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating embedding for '{destValue}': {ex.Message}");
                }
            }

            // Calculate cosine similarity for each destination value
            string? bestMatch = null;
            double highestSimilarity = 0;

            foreach (var (destValue, destEmbedding) in destinationEmbeddings)
            {
                var similarity = CosineSimilarity(sourceEmbedding.Span, destEmbedding.Span);

                if (similarity > highestSimilarity && similarity >= _similarityThreshold)
                {
                    highestSimilarity = similarity;
                    bestMatch = destValue;
                }
            }

            return (bestMatch, highestSimilarity);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Semantic matching cancelled for '{sourceValue}'");
            return (null, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FindBestMatchAsync: {ex.Message}");
            return (null, 0);
        }
    }

    /// <summary>
    /// Batch match multiple source values to destination values with progress reporting
    /// More efficient for large datasets as it caches destination embeddings
    /// </summary>
    /// <param name="sourceValues">Values to match</param>
    /// <param name="destinationValues">Possible matches</param>
    /// <param name="progress">Progress reporter for web UI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of source value -> (best match, similarity score)</returns>
    public async Task<Dictionary<string, (string? Match, double Similarity)>> BatchMatchAsync(
        IEnumerable<string> sourceValues,
        IEnumerable<string> destinationValues,
        IProgress<MatchingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, (string?, double)>();

        var sourceList = sourceValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var destList = destinationValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        if (!sourceList.Any() || !destList.Any())
            return results;

        try
        {
            var totalSteps = destList.Count + sourceList.Count;
            var currentStep = 0;

            // Report initial progress
            progress?.Report(new MatchingProgress
            {
                Stage = "Initializing",
                Current = 0,
                Total = totalSteps,
                Message = "Starting semantic matching..."
            });

            await Task.Yield(); // Allow UI to update

            // Cache destination embeddings (only compute once)
            progress?.Report(new MatchingProgress
            {
                Stage = "Caching Destinations",
                Current = currentStep,
                Total = totalSteps,
                Message = $"Generating embeddings for {destList.Count} destination values..."
            });

            var destinationEmbeddings = new Dictionary<string, ReadOnlyMemory<float>>();
            foreach (var destValue in destList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    destinationEmbeddings[destValue] = await _embeddingService.GenerateEmbeddingAsync(
                        destValue, 
                        cancellationToken: cancellationToken);

                    currentStep++;

                    // Report progress every 5 items or for last item
                    if (currentStep % 5 == 0 || currentStep == destList.Count)
                    {
                        progress?.Report(new MatchingProgress
                        {
                            Stage = "Caching Destinations",
                            Current = currentStep,
                            Total = totalSteps,
                            Message = $"Cached {currentStep}/{destList.Count} destinations"
                        });
                        await Task.Yield(); // Allow UI to update
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating embedding for destination '{destValue}': {ex.Message}");
                }
            }

            // Match each source value
            progress?.Report(new MatchingProgress
            {
                Stage = "Matching Sources",
                Current = currentStep,
                Total = totalSteps,
                Message = $"Matching {sourceList.Count} source values..."
            });

            var sourceIndex = 0;
            foreach (var sourceValue in sourceList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var sourceEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                        sourceValue, 
                        cancellationToken: cancellationToken);

                    string? bestMatch = null;
                    double highestSimilarity = 0;

                    foreach (var (destValue, destEmbedding) in destinationEmbeddings)
                    {
                        var similarity = CosineSimilarity(sourceEmbedding.Span, destEmbedding.Span);

                        if (similarity > highestSimilarity && similarity >= _similarityThreshold)
                        {
                            highestSimilarity = similarity;
                            bestMatch = destValue;
                        }
                    }

                    results[sourceValue] = (bestMatch, highestSimilarity);

                    currentStep++;
                    sourceIndex++;

                    // Report progress every item
                    if (sourceIndex % 3 == 0 || sourceIndex == sourceList.Count)
                    {
                        progress?.Report(new MatchingProgress
                        {
                            Stage = "Matching Sources",
                            Current = currentStep,
                            Total = totalSteps,
                            Message = $"Matched {sourceIndex}/{sourceList.Count} sources"
                        });
                        await Task.Yield(); // Allow UI to update
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing source value '{sourceValue}': {ex.Message}");
                    results[sourceValue] = (null, 0);
                }
            }

            progress?.Report(new MatchingProgress
            {
                Stage = "Complete",
                Current = totalSteps,
                Total = totalSteps,
                Message = $"Completed! Matched {results.Count(r => r.Value.Item1 != null)}/{sourceList.Count} values"
            });
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Batch semantic matching cancelled");
            progress?.Report(new MatchingProgress
            {
                Stage = "Cancelled",
                Message = "Operation cancelled by user"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in BatchMatchAsync: {ex.Message}");
            progress?.Report(new MatchingProgress
            {
                Stage = "Error",
                Message = $"Error: {ex.Message}"
            });
        }

        return results;
    }

    /// <summary>
    /// Calculate cosine similarity between two embedding vectors
    /// Returns value between -1 and 1 (1 = identical, 0 = orthogonal, -1 = opposite)
    /// </summary>
    private static double CosineSimilarity(
        ReadOnlySpan<float> vector1, 
        ReadOnlySpan<float> vector2)
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");

        if (vector1.Length == 0)
            return 0;

        float dotProduct = TensorPrimitives.Dot(vector1, vector2);
        float magnitude1 = TensorPrimitives.Norm(vector1);
        float magnitude2 = TensorPrimitives.Norm(vector2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }
}

/// <summary>
/// Progress information for semantic matching operations
/// </summary>
public class MatchingProgress
{
    public string Stage { get; set; } = "";
    public int Current { get; set; }
    public int Total { get; set; }
    public string Message { get; set; } = "";
    public double PercentComplete => Total > 0 ? (Current * 100.0 / Total) : 0;
}
