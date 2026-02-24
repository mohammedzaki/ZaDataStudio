using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Service for managing semantic matching configuration at runtime
/// Allows users to switch between providers and adjust similarity threshold
/// </summary>
public class SemanticMatchingSettingsService
{
    public event Action? OnSettingsChanged;

    private string _provider = "OpenAI";
    private double _similarityThreshold = 0.75;
    private bool _enabled = true;
    private string _model = "text-embedding-3-small";

    // Configuration values (set once at startup)
    private string? _openAiApiKey;
    private string? _azureEndpoint;
    private string? _azureApiKey;
    private string? _onnxModelPath;
    private int _onnxMaxTokens = 128;

    public string Provider
    {
        get => _provider;
        set
        {
            if (_provider != value)
            {
                _provider = value;
                // Auto-select default model for provider
                _model = GetDefaultModelForProvider(value);
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public string Model
    {
        get => _model;
        set
        {
            if (_model != value)
            {
                _model = value;
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public double SimilarityThreshold
    {
        get => _similarityThreshold;
        set
        {
            if (Math.Abs(_similarityThreshold - value) > 0.01)
            {
                _similarityThreshold = value;
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                OnSettingsChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Initialize configuration from appsettings.json
    /// Called once at application startup
    /// </summary>
    public void InitializeFromConfiguration(
        string? provider,
        double threshold,
        bool enabled,
        string? model,
        string? openAiApiKey,
        string? azureEndpoint,
        string? azureApiKey,
        string? onnxModelPath,
        int onnxMaxTokens)
    {
        _provider = provider ?? "OpenAI";
        _similarityThreshold = threshold;
        _enabled = enabled;
        _model = model ?? GetDefaultModelForProvider(_provider);

        // Store configuration for runtime service creation
        _openAiApiKey = openAiApiKey;
        _azureEndpoint = azureEndpoint;
        _azureApiKey = azureApiKey;
        _onnxModelPath = onnxModelPath;
        _onnxMaxTokens = onnxMaxTokens;
    }

    /// <summary>
    /// Get available models for current provider
    /// </summary>
    public string[] GetAvailableModels()
    {
        return Provider.ToLowerInvariant() switch
        {
            "openai" => new[]
            {
                "text-embedding-3-small",
                "text-embedding-3-large",
                "text-embedding-ada-002"
            },
            "onnx" => new[]
            {
                "all-MiniLM-L6-v2",
                "paraphrase-multilingual-mpnet-base-v2",
                "paraphrase-multilingual-MiniLM-L12-v2"
            },
            "azureopenai" => new[]
            {
                "text-embedding-ada-002",
                "text-embedding-3-small",
                "text-embedding-3-large"
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Get display name for provider
    /// </summary>
    public string GetProviderDisplayName(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "openai" => "OpenAI (Cloud, Paid)",
            "onnx" => "ONNX (Local, Free)",
            "azureopenai" => "Azure OpenAI (Cloud, Paid)",
            _ => provider
        };
    }

    /// <summary>
    /// Get description for provider
    /// </summary>
    public string GetProviderDescription(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "openai" => "Uses OpenAI's text-embedding models. Fast, accurate, but requires API key and internet connection.",
            "onnx" => "Uses local all-MiniLM-L6-v2 model. Free, offline, but slower for large datasets.",
            "azureopenai" => "Uses Azure OpenAI Service. Enterprise security and compliance.",
            _ => ""
        };
    }

    /// <summary>
    /// Get description for a specific model
    /// </summary>
    public string GetModelDescription(string model)
    {
        return model.ToLowerInvariant() switch
        {
            "text-embedding-3-small" => "1536 dimensions, $0.02/1M tokens. Recommended for most use cases.",
            "text-embedding-3-large" => "3072 dimensions, $0.13/1M tokens. Higher accuracy for complex matching.",
            "text-embedding-ada-002" => "1536 dimensions, $0.10/1M tokens. Legacy model, still reliable.",
            "all-minilm-l6-v2" => "384 dimensions, free. Sentence transformers model, good for English.",
            "paraphrase-multilingual-mpnet-base-v2" => "768 dimensions, free. Best for multilingual content.",
            "paraphrase-multilingual-minilm-l12-v2" => "384 dimensions, free. Faster multilingual model.",
            _ => ""
        };
    }

    private string GetDefaultModelForProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "openai" => "text-embedding-3-small",
            "onnx" => "all-MiniLM-L6-v2",
            "azureopenai" => "text-embedding-ada-002",
            _ => ""
        };
    }

    /// <summary>
    /// Create a semantic lookup matcher with current settings
    /// This enables runtime switching without restart
    /// </summary>
    public SemanticLookupMatcher? CreateMatcher()
    {
        if (!_enabled)
            return null;

        try
        {
            var embeddingService = CreateEmbeddingService();
            if (embeddingService == null)
                return null;

            return new SemanticLookupMatcher(embeddingService, _similarityThreshold);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating semantic matcher: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Create embedding service based on current provider and model
    /// </summary>
    private Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService? CreateEmbeddingService()
    {
        return Provider.ToLowerInvariant() switch
        {
            "onnx" => CreateOnnxService(),
            "openai" => CreateOpenAiService(),
            "azureopenai" or "azure" => CreateAzureOpenAiService(),
            _ => null
        };
    }

    private LocalOnnxEmbeddingService? CreateOnnxService()
    {
        if (string.IsNullOrWhiteSpace(_onnxModelPath))
            return null;

        if (!File.Exists(_onnxModelPath))
        {
            Console.WriteLine($"ONNX model not found at: {_onnxModelPath}");
            return null;
        }

        return new LocalOnnxEmbeddingService(_onnxModelPath, _onnxMaxTokens);
    }

    private Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService? CreateOpenAiService()
    {
        if (string.IsNullOrWhiteSpace(_openAiApiKey))
        {
            Console.WriteLine("OpenAI API key not configured");
            return null;
        }

        // Create OpenAI embedding service with current model
#pragma warning disable SKEXP0001, SKEXP0010
        return new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAITextEmbeddingGenerationService(
            _model,
            _openAiApiKey);
#pragma warning restore SKEXP0001, SKEXP0010
    }

    private Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService? CreateAzureOpenAiService()
    {
        if (string.IsNullOrWhiteSpace(_azureEndpoint) || string.IsNullOrWhiteSpace(_azureApiKey))
        {
            Console.WriteLine("Azure OpenAI endpoint or API key not configured");
            return null;
        }

        // Create a kernel with Azure OpenAI embedding service
        var kernel = Microsoft.SemanticKernel.Kernel.CreateBuilder()
            .AddAzureOpenAITextEmbeddingGeneration(
                deploymentName: _model,
                endpoint: _azureEndpoint,
                apiKey: _azureApiKey)
            .Build();

        // Get the embedding service from the kernel
        return kernel.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>();
    }
}
