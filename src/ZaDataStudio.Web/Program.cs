using ZaDataStudio.Web.Components;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Infrastructure.Persistence.Repositories;
using ZaDataStudio.Application.Mapping;
using ZaDataStudio.Application.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Infrastructure Services (Clean Architecture)
builder.Services.AddScoped<ISessionRepository, SessionRepository>();

// Register SQL Server services with connection manager (Singleton pattern per scope)
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerConnectionManager>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerDatabaseService>();
builder.Services.AddScoped<IDatabaseService>(sp => sp.GetRequiredService<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerDatabaseService>());

// Register Application and Infrastructure services
builder.Services.AddScoped<ILookupColumnAnalyzer, LookupColumnAnalyzer>();
builder.Services.AddScoped<IMappingComparisonService, MappingComparisonService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.SqlServerComparisonService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Excel.ExcelMappingService>();
builder.Services.AddScoped<ZaDataStudio.Infrastructure.Persistence.SqlServer.DataComparisonService>();

// Register Semantic Matching with OpenAI, Azure OpenAI, or ONNX (if enabled)
var semanticConfig = builder.Configuration.GetSection("SemanticMatching");
var provider = semanticConfig.GetValue<string>("Provider", "OpenAI")?.ToLowerInvariant();

if (semanticConfig.GetValue<bool>("Enabled", false))
{
    var threshold = semanticConfig.GetValue<double>("SimilarityThreshold", 0.75);

    switch (provider)
    {
        case "onnx":
            var onnxConfig = builder.Configuration.GetSection("Onnx");
            var modelPath = onnxConfig["ModelPath"];
            var maxTokens = onnxConfig.GetValue<int>("MaxTokens", 128);

            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                // Resolve relative path to absolute
                if (!Path.IsPathRooted(modelPath))
                {
                    modelPath = Path.Combine(builder.Environment.ContentRootPath, modelPath);
                }

                if (File.Exists(modelPath))
                {
                    builder.Services.AddSemanticMatchingWithOnnx(modelPath, maxTokens, threshold);
                    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Information);
                    Console.WriteLine($"Semantic matching enabled with ONNX model: {modelPath}");
                }
                else
                {
                    Console.WriteLine($"Warning: ONNX model not found at {modelPath}. Semantic matching disabled.");
                    Console.WriteLine($"Download the model from: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2");
                }
            }
            break;

        case "azureopenai":
        case "azure":
            var azureConfig = builder.Configuration.GetSection("AzureOpenAI");
            var endpoint = azureConfig["Endpoint"];
            var azureApiKey = azureConfig["ApiKey"];
            var deploymentName = azureConfig["DeploymentName"] ?? "text-embedding-ada-002";

            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(azureApiKey))
            {
                builder.Services.AddSemanticMatchingWithAzureOpenAI(endpoint, azureApiKey, deploymentName, threshold);
                Console.WriteLine($"Semantic matching enabled with Azure OpenAI: {deploymentName}");
            }
            break;

        case "openai":
        default:
            var openAiConfig = builder.Configuration.GetSection("OpenAI");
            var apiKey = openAiConfig["ApiKey"];
            var model = openAiConfig["Model"] ?? "text-embedding-3-small";

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                builder.Services.AddSemanticMatchingWithOpenAI(apiKey, model, threshold);
                Console.WriteLine($"Semantic matching enabled with OpenAI: {model}");
            }
            break;
    }
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


