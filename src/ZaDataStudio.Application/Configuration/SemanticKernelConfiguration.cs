using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using ZaDataStudio.Application.Mapping;

namespace ZaDataStudio.Application.Configuration;

/// <summary>
/// Configuration extensions for semantic matching with OpenAI or Azure OpenAI
/// </summary>
public static class SemanticKernelConfiguration
{
    /// <summary>
    /// Configure semantic matching with OpenAI (text-embedding-3-small)
    /// Recommended for most users: simpler setup and 5x cheaper than Azure OpenAI
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="apiKey">OpenAI API key (starts with sk-)</param>
    /// <param name="modelId">Embedding model to use</param>
    /// <param name="similarityThreshold">Minimum similarity score (0-1) to consider a match</param>
    public static IServiceCollection AddSemanticMatchingWithOpenAI(
        this IServiceCollection services,
        string apiKey,
        string modelId = "text-embedding-3-small",
        double similarityThreshold = 0.75)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key is required", nameof(apiKey));

        // Register Semantic Kernel with OpenAI
        var kernelBuilder = services.AddKernel();
        
        kernelBuilder.AddOpenAITextEmbeddingGeneration(
            modelId: modelId,
            apiKey: apiKey);

        // Register the semantic matcher with threshold
        services.AddScoped(sp => 
        {
            var embeddingService = sp.GetRequiredService<ITextEmbeddingGenerationService>();
            return new SemanticLookupMatcher(embeddingService, similarityThreshold);
        });

        return services;
    }

    /// <summary>
    /// Configure semantic matching with Azure OpenAI (text-embedding-ada-002)
    /// For enterprise scenarios requiring Azure compliance and private networking
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="endpoint">Azure OpenAI endpoint URL</param>
    /// <param name="apiKey">Azure OpenAI API key</param>
    /// <param name="deploymentName">Deployment name in Azure</param>
    /// <param name="similarityThreshold">Minimum similarity score (0-1) to consider a match</param>
    public static IServiceCollection AddSemanticMatchingWithAzureOpenAI(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName = "text-embedding-ada-002",
        double similarityThreshold = 0.75)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Azure OpenAI endpoint is required", nameof(endpoint));
        
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Azure OpenAI API key is required", nameof(apiKey));

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

    /// <summary>
    /// Configure semantic matching with local ONNX model (all-MiniLM-L6-v2)
    /// For offline use, zero cost, and privacy (data stays local)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="modelPath">Path to ONNX model file (.onnx)</param>
    /// <param name="maxTokens">Maximum tokens for input (default: 128)</param>
    /// <param name="similarityThreshold">Minimum similarity score (0-1) to consider a match</param>
    public static IServiceCollection AddSemanticMatchingWithOnnx(
        this IServiceCollection services,
        string modelPath,
        int maxTokens = 128,
        double similarityThreshold = 0.75)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("ONNX model path is required", nameof(modelPath));

        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"ONNX model not found at: {modelPath}");

        // Register local ONNX embedding service as both interface and concrete type
        services.AddSingleton<LocalOnnxEmbeddingService>(sp => 
            new LocalOnnxEmbeddingService(modelPath, maxTokens));

        services.AddSingleton<ITextEmbeddingGenerationService>(sp => 
            sp.GetRequiredService<LocalOnnxEmbeddingService>());

        // Register the semantic matcher with threshold
        services.AddScoped(sp => 
        {
            var embeddingService = sp.GetRequiredService<ITextEmbeddingGenerationService>();
            return new SemanticLookupMatcher(embeddingService, similarityThreshold);
        });

        return services;
    }
}
