# Semantic Matching Implementation Plan for Lookup Values

## Overview

This document outlines the implementation plan for adding AI-powered semantic matching to improve lookup value comparison in the ZaDataStudio application. This enhancement will help identify similar values even when they don't match exactly (e.g., "Sports Volunteering" matching with "sport").

## Problem Statement

Current lookup matching uses exact string comparison, which fails to identify semantically similar values:
- "Sports Volunteering" vs "sport"
- "Athletic Programs" vs "athletics"
- "Healthcare Services" vs "medical services"

## Recommended Solution

### Option 1: Azure OpenAI + Semantic Kernel (Production Ready)

**Best for:** Enterprise scenarios with cloud connectivity

**Advantages:**
- ✅ Native .NET integration via Semantic Kernel
- ✅ Supports embeddings for semantic similarity
- ✅ Handles bilingual text (English/Arabic)
- ✅ Microsoft-supported, enterprise-ready
- ✅ Can batch process for performance

**Installation:**
```bash
dotnet add package Microsoft.SemanticKernel
dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI
```

---

## Implementation

### 1. Create Semantic Lookup Matcher Service

**File:** `src\ZaDataStudio.Application\Mapping\SemanticLookupMatcher.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Numerics.Tensors;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// AI-powered semantic matching for lookup values using embeddings
/// </summary>
public class SemanticLookupMatcher
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly double _similarityThreshold;

    public SemanticLookupMatcher(
        ITextEmbeddingGenerationService embeddingService, 
        double similarityThreshold = 0.75)
    {
        _embeddingService = embeddingService;
        _similarityThreshold = similarityThreshold;
    }

    /// <summary>
    /// Find best semantic match for a source value from destination values
    /// </summary>
    public async Task<(string? BestMatch, double Similarity)> FindBestMatchAsync(
        string sourceValue, 
        IEnumerable<string> destinationValues)
    {
        if (string.IsNullOrWhiteSpace(sourceValue) || !destinationValues.Any())
            return (null, 0);

        // Generate embedding for source value
        var sourceEmbedding = await _embeddingService.GenerateEmbeddingAsync(sourceValue);

        // Generate embeddings for all destination values (can be cached)
        var destinationEmbeddings = new Dictionary<string, ReadOnlyMemory<float>>();
        foreach (var destValue in destinationValues)
        {
            destinationEmbeddings[destValue] = await _embeddingService
                .GenerateEmbeddingAsync(destValue);
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

    /// <summary>
    /// Batch match multiple source values to destination values
    /// More efficient for large datasets
    /// </summary>
    public async Task<Dictionary<string, (string? Match, double Similarity)>> BatchMatchAsync(
        IEnumerable<string> sourceValues,
        IEnumerable<string> destinationValues)
    {
        var results = new Dictionary<string, (string?, double)>();

        // Cache destination embeddings (only compute once)
        var destinationEmbeddings = new Dictionary<string, ReadOnlyMemory<float>>();
        foreach (var destValue in destinationValues)
        {
            destinationEmbeddings[destValue] = await _embeddingService
                .GenerateEmbeddingAsync(destValue);
        }

        // Match each source value
        foreach (var sourceValue in sourceValues)
        {
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                results[sourceValue] = (null, 0);
                continue;
            }

            var sourceEmbedding = await _embeddingService
                .GenerateEmbeddingAsync(sourceValue);
            
            string? bestMatch = null;
            double highestSimilarity = 0;

            foreach (var (destValue, destEmbedding) in destinationEmbeddings)
            {
                var similarity = CosineSimilarity(
                    sourceEmbedding.Span, 
                    destEmbedding.Span);
                
                if (similarity > highestSimilarity && similarity >= _similarityThreshold)
                {
                    highestSimilarity = similarity;
                    bestMatch = destValue;
                }
            }

            results[sourceValue] = (bestMatch, highestSimilarity);
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

        float dotProduct = TensorPrimitives.Dot(vector1, vector2);
        float magnitude1 = TensorPrimitives.Norm(vector1);
        float magnitude2 = TensorPrimitives.Norm(vector2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }
}
```

---

### 2. Configure Semantic Kernel

**File:** `src\ZaDataStudio.Application\Configuration\SemanticKernelConfiguration.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace ZaDataStudio.Application.Configuration;

public static class SemanticKernelConfiguration
{
    public static IServiceCollection AddSemanticMatching(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName = "text-embedding-ada-002",
        double similarityThreshold = 0.75)
    {
        // Register Semantic Kernel with Azure OpenAI
        var kernelBuilder = services.AddKernel();
        
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            deploymentName: deploymentName,
            endpoint: endpoint,
            apiKey: apiKey);

        // Register the semantic matcher with threshold
        services.AddScoped(sp => 
        {
            var embeddingService = sp.GetRequiredService<ITextEmbeddingGenerationService>();
            return new SemanticLookupMatcher(embeddingService, similarityThreshold);
        });

        return services;
    }
}
```

---

### 3. Update Lookup Extensions

**File:** `src\ZaDataStudio.Application\Mapping\LookupExtensions.cs`

```csharp
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

public static class LookupExtensions
{
    /// <summary>
    /// Check if the set contains a value similar to the provided lookup value
    /// Enhanced with semantic matching option
    /// </summary>
    public static bool HasSimilarValue(
        this HashSet<string> set, 
        LookupValue value,
        SemanticLookupMatcher? semanticMatcher = null)
    {
        if (value?.EnValue == null) return false;

        // Try exact match first (fastest)
        if (set.Contains(value.EnValue, StringComparer.OrdinalIgnoreCase))
            return true;

        // Try substring/contains match
        if (set.Any(s => 
            s.Contains(value.EnValue, StringComparison.OrdinalIgnoreCase) ||
            value.EnValue.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return true;

        // If semantic matcher is available, try AI-powered matching
        if (semanticMatcher != null)
        {
            var (bestMatch, similarity) = semanticMatcher
                .FindBestMatchAsync(value.EnValue, set)
                .GetAwaiter()
                .GetResult();

            return bestMatch != null && similarity >= 0.75;
        }

        return false;
    }

    /// <summary>
    /// Async version with semantic matching
    /// </summary>
    public static async Task<bool> HasSimilarValueAsync(
        this HashSet<string> set,
        LookupValue value,
        SemanticLookupMatcher semanticMatcher)
    {
        if (value?.EnValue == null) return false;

        // Quick checks first
        if (set.Contains(value.EnValue, StringComparer.OrdinalIgnoreCase))
            return true;

        // Semantic matching
        var (bestMatch, similarity) = await semanticMatcher
            .FindBestMatchAsync(value.EnValue, set);
        return bestMatch != null && similarity >= 0.75;
    }
}
```

---

### 4. Update LookupColumnAnalyzer

**File:** `src\ZaDataStudio.Application\Mapping\LookupColumnAnalyzer.cs`

Add semantic matching support to the analyzer:

```csharp
private readonly SemanticLookupMatcher? _semanticMatcher;

public LookupColumnAnalyzer(
    IDbConnectionProvider connectionProvider,
    SemanticLookupMatcher? semanticMatcher = null)
{
    _connectionProvider = connectionProvider;
    _semanticMatcher = semanticMatcher;
}

// Update CompareAndMapLookupValuesAsync method
private async Task<List<LookupValueMapping>> CompareAndMapLookupValuesAsync(
    Dictionary<string, LookupValue> sourceValues,
    Dictionary<string, LookupValue> destinationValues)
{
    var mappings = new List<LookupValueMapping>();

    // Use semantic matcher if available
    if (_semanticMatcher != null && sourceValues.Any())
    {
        var destValuesSet = destinationValues.Values
            .Select(v => v.EnValue)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        var sourceValuesDict = sourceValues.Values
            .Where(v => !string.IsNullOrEmpty(v.EnValue))
            .ToDictionary(v => v.EnValue!, v => v);

        // Batch semantic matching
        var semanticMatches = await _semanticMatcher.BatchMatchAsync(
            sourceValuesDict.Keys,
            destValuesSet);

        foreach (var sourceValue in sourceValues.Values)
        {
            var destMatch = destinationValues.Values.FirstOrDefault(dv => 
                dv.EnValue?.Equals(sourceValue.EnValue, 
                    StringComparison.OrdinalIgnoreCase) == true);

            // If no exact match, check semantic match
            if (destMatch == null && 
                sourceValue.EnValue != null && 
                semanticMatches.TryGetValue(sourceValue.EnValue, 
                    out var semanticResult) &&
                semanticResult.Match != null)
            {
                destMatch = destinationValues.Values.FirstOrDefault(dv => 
                    dv.EnValue == semanticResult.Match);
            }

            mappings.Add(new LookupValueMapping
            {
                SourceLookupCode = sourceValue.Code,
                SourceLookupEnValue = sourceValue.EnValue,
                SourceLookupArValue = sourceValue.ArValue,
                DestinationLookupCode = destMatch?.Code,
                DestinationLookupEnValue = destMatch?.EnValue,
                DestinationLookupArValue = destMatch?.ArValue,
                DestinationLookupValue = destMatch?.EnValue,
                SemanticSimilarity = semanticResult.Similarity // Optional: track similarity
            });
        }
    }
    else
    {
        // Fallback to original logic
        // ... existing code
    }

    return mappings;
}
```

---

### 5. Update Domain Entity (Optional)

**File:** `src\ZaDataStudio.Domain\Entities\LookupValueMapping.cs`

Add semantic similarity score tracking:

```csharp
public class LookupValueMapping
{
    // ... existing properties ...
    
    /// <summary>
    /// Semantic similarity score (0-1) if matched using AI
    /// </summary>
    public double? SemanticSimilarity { get; set; }
}
```

---

## Configuration

### appsettings.json

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "EmbeddingDeployment": "text-embedding-ada-002"
  },
  "SemanticMatching": {
    "SimilarityThreshold": 0.75,
    "EnableBatchProcessing": true,
    "CacheEmbeddings": true
  }
}
```

### Program.cs

```csharp
// Add Semantic Matching services
builder.Services.AddSemanticMatching(
    endpoint: builder.Configuration["AzureOpenAI:Endpoint"]!,
    apiKey: builder.Configuration["AzureOpenAI:ApiKey"]!,
    deploymentName: builder.Configuration["AzureOpenAI:EmbeddingDeployment"]!,
    similarityThreshold: builder.Configuration
        .GetValue<double>("SemanticMatching:SimilarityThreshold", 0.75)
);
```

---

## Cost & Performance Considerations

| Model | Cost per 1K Tokens | Speed | Accuracy | Best For |
|-------|-------------------|-------|----------|----------|
| Azure OpenAI (text-embedding-ada-002) | ~$0.0001 | Fast (API) | Excellent | Production, cloud |
| OpenAI (text-embedding-3-small) | ~$0.00002 | Very Fast | Excellent | Budget-friendly |
| Local ONNX (all-MiniLM-L6-v2) | Free | Medium | Good | Offline, privacy |

**Estimated Costs:**
- 100 lookups with 50 values each = ~5,000 tokens = $0.50/run
- Consider caching embeddings for frequently accessed values

---

## Alternative: Local/Offline Implementation

For air-gapped or privacy-sensitive environments:

### Option 2: ONNX Runtime with Local Models

```bash
dotnet add package Microsoft.ML
dotnet add package Microsoft.ML.OnnxRuntime
dotnet add package Microsoft.ML.Tokenizers
```

**Implementation:** Use sentence-transformers models like `all-MiniLM-L6-v2` from Hugging Face, converted to ONNX format.

---

## Quick Start Steps

1. **Get Azure OpenAI Access:**
   - Apply for Azure OpenAI Service in Azure Portal
   - Create deployment for `text-embedding-ada-002`
   - Copy endpoint and API key

2. **Install NuGet Packages:**
   ```bash
   dotnet add package Microsoft.SemanticKernel
   dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI
   ```

3. **Add Configuration:**
   - Update `appsettings.json` with your credentials
   - Register services in `Program.cs`

4. **Update Code:**
   - Create `SemanticLookupMatcher.cs`
   - Update `LookupColumnAnalyzer` constructor
   - Modify comparison logic

5. **Test:**
   ```csharp
   var matcher = new SemanticLookupMatcher(embeddingService);
   var result = await matcher.FindBestMatchAsync(
       "Sports Volunteering",
       new[] { "sport", "athletics", "recreation" }
   );
   // Result: ("sport", 0.82) - 82% similarity
   ```

---

## Testing Examples

### Unit Test Example

```csharp
[Fact]
public async Task FindBestMatch_SportsVolunteering_MatchesSport()
{
    // Arrange
    var matcher = new SemanticLookupMatcher(_embeddingService);
    var destinationValues = new[] { "sport", "athletics", "recreation", "fitness" };

    // Act
    var (bestMatch, similarity) = await matcher.FindBestMatchAsync(
        "Sports Volunteering", 
        destinationValues);

    // Assert
    Assert.Equal("sport", bestMatch);
    Assert.True(similarity > 0.75);
}
```

---

## Excel Export Enhancement

Update `ExcelMappingService.cs` to show similarity scores in the VALUES MAPPING section:

```csharp
sheet.Cell(row, 6).Value = "Similarity %";

// When writing mapping rows:
if (valueMap.SemanticSimilarity.HasValue)
{
    sheet.Cell(row, 6).Value = $"{valueMap.SemanticSimilarity.Value:P0}";
    
    if (valueMap.SemanticSimilarity.Value >= 0.9)
        sheet.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.DarkGreen;
    else if (valueMap.SemanticSimilarity.Value >= 0.75)
        sheet.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.LightGreen;
    else
        sheet.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.Yellow;
}
```

---

## Rollout Plan

### Phase 1: Pilot (Week 1-2)
- Implement core `SemanticLookupMatcher`
- Test with sample data
- Measure accuracy and costs

### Phase 2: Integration (Week 3)
- Update `LookupColumnAnalyzer`
- Add configuration settings
- Update Excel export to show similarity scores

### Phase 3: Production (Week 4)
- Deploy to production
- Monitor API usage and costs
- Gather user feedback

---

## Monitoring & Optimization

1. **Track API Calls:**
   - Log embedding generation count
   - Monitor Azure OpenAI quota

2. **Implement Caching:**
   ```csharp
   // Cache embeddings for frequently used values
   private readonly Dictionary<string, ReadOnlyMemory<float>> _embeddingCache = new();
   ```

3. **Adjust Threshold:**
   - Start with 0.75 (75% similarity)
   - Tune based on false positive/negative rates

---

## Success Metrics

- **Accuracy:** % of semantically similar values correctly matched
- **Cost:** Total API cost per analysis run
- **Performance:** Time to analyze lookup values
- **User Satisfaction:** Reduction in manual mapping corrections

---

## Support & Resources

- [Microsoft Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Azure OpenAI Service](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Embeddings Overview](https://platform.openai.com/docs/guides/embeddings)

---

## Future Enhancements

1. **Multi-language Support:** Better handling of English/Arabic mixed values
2. **Custom Models:** Fine-tune embeddings for domain-specific terminology
3. **Active Learning:** Allow users to approve/reject matches to improve accuracy
4. **Hybrid Matching:** Combine rule-based + semantic for best results

---

*Last Updated: 2024*  
*Author: GitHub Copilot*  
*Project: ZaDataStudio*
