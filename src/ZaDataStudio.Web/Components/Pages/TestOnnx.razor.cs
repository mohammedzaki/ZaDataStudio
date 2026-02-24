using System.Net.NetworkInformation;
using Microsoft.AspNetCore.Components;
using ZaDataStudio.Application.Mapping;

namespace ZaDataStudio.Web.Components.Pages;

public partial class TestOnnx : ComponentBase
{
    private bool _testing;
    private bool _tested;
    private bool _success;
    private bool _isMatching;
    private List<string> _logs = new();
    private List<ReadOnlyMemory<float>> _embeddings = new();
    private string[] _testTexts = { "Sport", "Sports Volunteering", "Education", "Technology" };
    private string _matchResult = "";
    private MatchingProgress? _matchingProgress;
    
    [Inject]
    private LocalOnnxEmbeddingService? OnnxService { get; set; }

    private async Task RunOnnxTest()
    {
        _testing = true;
        _logs.Clear();
        _embeddings.Clear();
        _matchResult = "";
        _matchingProgress = null;
        _success = false;

        try
        {
            _logs.Add("🔄 Starting ONNX test...");
            StateHasChanged();
            await Task.Delay(100); // Allow UI to update

            if (OnnxService == null)
            {
                _logs.Add("❌ ERROR: ONNX service not registered!");
                _logs.Add("Check appsettings.json: Provider should be 'Onnx'");
                return;
            }

            _logs.Add("✓ ONNX service found");
            StateHasChanged();

            // Test 1: Generate embeddings
            _logs.Add($"🔄 Generating embeddings for {_testTexts.Length} texts...");
            StateHasChanged();
            await Task.Delay(100);

            var startTime = DateTime.Now;
            _embeddings = (await OnnxService.GenerateEmbeddingsAsync(_testTexts)).ToList();
            var duration = (DateTime.Now - startTime).TotalMilliseconds;

            _logs.Add($"✓ Generated {_embeddings.Count} embeddings in {duration:F0}ms");
            _logs.Add($"  Embedding dimension: {_embeddings[0].Length}");
            StateHasChanged();

            // Test 2: Semantic matching with progress (using batch match to show progress)
            _logs.Add("🔄 Testing semantic similarity with progress reporting...");
            _isMatching = true;
            StateHasChanged();
            await Task.Delay(100);

            var progress = new Progress<MatchingProgress>(p =>
            {
                _matchingProgress = p;
                InvokeAsync(StateHasChanged);
            });

            var matcher = new SemanticLookupMatcher(OnnxService, similarityThreshold: 0.70);

            // Use BatchMatchAsync to demonstrate progress reporting
            var sourceValues = new[] { "Sports Volunteering", "Health Care", "Tech Support", "Education Program" };
            var destValues = new[] { "Sport", "Education", "Technology", "Health", "Business", "Finance" };

            var batchResults = await matcher.BatchMatchAsync(
                sourceValues,
                destValues,
                progress,
                cancellationToken: default);

            _isMatching = false;

            // Display results for "Sports Volunteering"
            if (batchResults.TryGetValue("Sports Volunteering", out var result))
            {
                _matchResult = $"'{result.Match}' with {result.Similarity:P0} similarity";
                _logs.Add($"✓ Best match for 'Sports Volunteering': {_matchResult}");
            }

            // Show other matches
            foreach (var kvp in batchResults)
            {
                _logs.Add($"  '{kvp.Key}' → '{kvp.Value.Match}' ({kvp.Value.Similarity:P0})");
            }

            if (batchResults.ContainsKey("Sports Volunteering") &&
                batchResults["Sports Volunteering"].Match == "Sport" &&
                batchResults["Sports Volunteering"].Similarity > 0.70)
            {
                _logs.Add("✅ SUCCESS: ONNX semantic matching is working correctly!");
                _success = true;
            }
            else
            {
                _logs.Add("⚠️ WARNING: Unexpected result. Expected 'Sport' with >70% similarity.");
            }
        }
        catch (Exception ex)
        {
            _logs.Add($"❌ ERROR: {ex.Message}");
            _logs.Add($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            _testing = false;
            _tested = true;
            _isMatching = false;
            StateHasChanged();
        }
    }
}

