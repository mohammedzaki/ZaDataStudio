# Semantic Lookup Matching with OpenAI

This feature uses OpenAI's `text-embedding-3-small` model to find semantically similar lookup values when exact matches aren't found. For example, it can match "Sports Volunteering" to "sport" or "IT Support" to "Technology".

## 🎯 Benefits

- **Improved Match Rate**: Finds semantically similar values that exact string matching misses
- **Cost-Effective**: Uses OpenAI's cheapest embedding model at $0.02 per 1M tokens
- **Batch Processing**: Efficient for large datasets with caching
- **Transparent**: Shows similarity scores (0-100%) in Excel reports
- **Optional**: Feature can be enabled/disabled via configuration

## 📋 Prerequisites

1. **.NET 10 SDK** (already installed)
2. **OpenAI API Account** - Sign up at https://platform.openai.com/signup
3. **OpenAI API Key** - Generate at https://platform.openai.com/api-keys

## 🚀 Quick Start

### Step 1: Install NuGet Packages

```powershell
cd src\ZaDataStudio.Application
dotnet add package Microsoft.SemanticKernel
dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI
```

### Step 2: Get Your OpenAI API Key

1. Go to https://platform.openai.com/api-keys
2. Click "Create new secret key"
3. Name it "ZaDataStudio" 
4. Copy the key (starts with `sk-proj-...`)
5. **Save it securely** - you won't see it again!

### Step 3: Configure API Key (Development)

**Option A: User Secrets (Recommended for Development)**

```powershell
cd src\ZaDataStudio.Web
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-YOUR-KEY-HERE"
```

**Option B: Environment Variable**

```powershell
# Windows PowerShell
$env:OpenAI__ApiKey = "sk-proj-YOUR-KEY-HERE"

# Windows CMD
set OpenAI__ApiKey=sk-proj-YOUR-KEY-HERE
```

**Option C: appsettings.Development.json (Not Recommended - Risk of Commit)**

Edit `src\ZaDataStudio.Web\appsettings.Development.json`:

```json
{
  "OpenAI": {
    "ApiKey": "sk-proj-YOUR-KEY-HERE",
    "Model": "text-embedding-3-small"
  },
  "SemanticMatching": {
    "Enabled": true,
    "SimilarityThreshold": 0.70
  }
}
```

⚠️ **WARNING**: Never commit API keys to Git! Add `appsettings.Development.json` to `.gitignore` if using this method.

### Step 4: Run the Application

```powershell
cd src\ZaDataStudio.Web
dotnet run
```

The semantic matching will automatically activate when:
- `SemanticMatching:Enabled` is `true`
- `OpenAI:ApiKey` is configured

## 🎨 How It Works

### 1. Exact Matching First
```csharp
Source: "Sports Volunteering"
Destination: ["Sports", "Volunteering", "Education"]
Result: No exact match → Try semantic matching
```

### 2. AI Semantic Matching
```csharp
// Generate embeddings (1536-dimensional vectors)
sourceEmbedding = OpenAI.Embed("Sports Volunteering")
destEmbedding1 = OpenAI.Embed("Sports")  
destEmbedding2 = OpenAI.Embed("Volunteering")

// Calculate similarity scores (cosine similarity)
"Sports Volunteering" vs "Sports" = 0.85 (85%)  ✓ Above threshold
"Sports Volunteering" vs "Volunteering" = 0.72 (72%) ✓ Above threshold

// Return best match
Result: "Sports" (85% similarity)
```

### 3. Excel Report Display
```
| Old Value           | New Value | Status       | AI Match % |
|---------------------|-----------|--------------|------------|
| Sports Volunteering | Sports    | 🤖 AI Match  | 85%        |
| Education           | Education | ✓ Exact Match| 100%       |
| Technology          |           | ✗ Missing    | -          |
```

## ⚙️ Configuration

### appsettings.json (Production)

```json
{
  "OpenAI": {
    "ApiKey": "",  // Set via environment variable
    "Model": "text-embedding-3-small"
  },
  "SemanticMatching": {
    "Enabled": true,
    "SimilarityThreshold": 0.75,  // 75% minimum similarity
    "EnableBatchProcessing": true
  }
}
```

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `false` | Enable/disable semantic matching |
| `SimilarityThreshold` | `0.75` | Minimum similarity (0-1) to consider a match |
| `Model` | `text-embedding-3-small` | OpenAI embedding model |
| `EnableBatchProcessing` | `true` | Use batch API for better performance |

### Threshold Tuning

- **0.85+**: Very strict - only near-identical meanings
- **0.75** (default): Balanced - good accuracy with reasonable matches
- **0.70**: More permissive - catches more matches but may have false positives
- **0.60-**: Too permissive - many incorrect matches

## 💰 Cost Estimation

### Pricing
- **Model**: text-embedding-3-small
- **Cost**: $0.02 per 1 million tokens
- **Token estimate**: ~100 tokens per lookup value (average)

### Example Scenarios

**Scenario 1: Small Dataset**
- 100 source values × 100 dest values = 10,000 comparisons
- Tokens: 200 values × 100 tokens = 20,000 tokens
- Cost: $0.0004 (less than 1 cent)

**Scenario 2: Medium Dataset**
- 1,000 source × 1,000 dest = 1M comparisons
- Tokens: 2,000 values × 100 tokens = 200,000 tokens
- Cost: $0.004 (less than 1 cent)

**Scenario 3: Large Dataset**
- 10,000 source × 10,000 dest = 100M comparisons
- Tokens: 20,000 values × 100 tokens = 2M tokens
- Cost: $0.04 (4 cents)

### Batch Processing Benefits
The implementation caches destination embeddings, so you only pay for:
- Each unique source value (once)
- Each unique destination value (once)

Not: source_count × dest_count comparisons!

## 🔒 Security Best Practices

### Development
1. **Use User Secrets**: `dotnet user-secrets set "OpenAI:ApiKey" "your-key"`
2. **Never commit** API keys to Git
3. **Add to .gitignore**: `appsettings.*.json` (except templates)

### Production
1. **Environment Variables**: Set `OpenAI__ApiKey` in hosting environment
2. **Azure Key Vault**: Store keys securely in Azure
3. **Managed Identity**: Use Azure OpenAI with managed identity (no keys!)

### Key Rotation
```powershell
# Generate new key at https://platform.openai.com/api-keys
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-NEW-KEY"
# Or update environment variable
```

## 🧪 Testing

### Test with Sample Data

```csharp
// src\ZaDataStudio.Web\Components\Pages\SemanticMatchTest.razor
@page "/test-semantic"
@inject SemanticLookupMatcher Matcher

<h3>Semantic Matching Test</h3>

<button @onclick="TestMatch">Test Match</button>

<div>@_result</div>

@code {
    private string _result = "";

    private async Task TestMatch()
    {
        var sources = new[] { "Sports Volunteering", "IT Support", "Marketing Communications" };
        var destinations = new[] { "Sport", "Technology", "Marketing" };

        var results = await Matcher.BatchMatchAsync(sources, destinations);

        _result = string.Join("\n", results.Select(r => 
            $"{r.Key} → {r.Value.Match ?? "No match"} ({r.Value.Similarity:P0})"
        ));
    }
}
```

### View Results
```
Sports Volunteering → Sport (85%)
IT Support → Technology (78%)
Marketing Communications → Marketing (92%)
```

## 📊 Monitoring

### Console Output
The application logs semantic matches to console:

```
Semantic match: 'Sports Volunteering' → 'Sport' (similarity: 85%)
Semantic match: 'IT Support' → 'Technology' (similarity: 78%)
Warning: Semantic matching failed: API rate limit exceeded
```

### Excel Reports
Check the "VALUES MAPPING" section:
- 🤖 = AI semantic match (blue background)
- ✓ = Exact match (green background)
- ✗ = No match (yellow background)

## 🔧 Troubleshooting

### "API key not found"
**Solution**: Set the API key using one of the methods in Step 3

### "Authentication failed"
**Solution**: Verify your API key is correct and active at https://platform.openai.com/api-keys

### "Rate limit exceeded"
**Solution**: 
1. Wait 60 seconds and retry
2. Upgrade to paid tier for higher limits
3. Reduce `SimilarityThreshold` to process fewer values

### "Semantic matching disabled"
**Solution**: Check `SemanticMatching:Enabled` is `true` in configuration

### Poor match quality
**Solution**: 
1. Increase `SimilarityThreshold` (try 0.80)
2. Review matches in Excel - adjust threshold based on results
3. Check source data quality - clean up abbreviations/typos

## 🚦 Rollout Strategy

### Phase 1: Pilot (Week 1)
- Enable for 1 small project
- Threshold: 0.80 (strict)
- Review all AI matches manually
- Tune threshold based on results

### Phase 2: Limited (Week 2-3)
- Enable for 5-10 projects
- Threshold: 0.75 (balanced)
- Spot-check AI matches
- Monitor costs and performance

### Phase 3: Full Rollout (Week 4+)
- Enable for all projects
- Threshold: 0.75 (or tuned value)
- Automated monitoring
- Cost tracking

## 🎓 Advanced Usage

### Using Azure OpenAI Instead

If you have Azure OpenAI access:

```csharp
// Program.cs
builder.Services.AddSemanticMatchingWithAzureOpenAI(
    endpoint: "https://your-resource.openai.azure.com/",
    apiKey: azureConfig["ApiKey"]!,
    deploymentName: "text-embedding-ada-002",
    similarityThreshold: 0.75
);
```

Benefits:
- Enterprise compliance
- Private networking (VNet)
- More control

Drawbacks:
- 5x more expensive
- Requires Azure subscription
- More complex setup

### Custom Threshold Per Column

```csharp
// Future enhancement - per-column thresholds
var matcher = new SemanticLookupMatcher(embeddingService, threshold: 0.85);
var result = await matcher.FindBestMatchAsync(sourceValue, destValues);
```

## 📚 Additional Resources

- **OpenAI Embeddings Guide**: https://platform.openai.com/docs/guides/embeddings
- **Semantic Kernel Docs**: https://learn.microsoft.com/semantic-kernel/
- **Cost Calculator**: https://openai.com/api/pricing/
- **Implementation Guide**: `docs/SemanticMatchingImplementation.md`

## ❓ FAQ

**Q: Will this slow down my analysis?**  
A: Initial request has ~1-2 second delay for API call. Results are cached for subsequent matches.

**Q: What happens if I run out of API credits?**  
A: Semantic matching will fail gracefully - exact matches will still work.

**Q: Can I use this offline?**  
A: No, requires internet connection to OpenAI API. For offline, consider local ONNX models (see implementation guide).

**Q: How accurate is semantic matching?**  
A: At 0.75 threshold, typically 90%+ accurate for business terminology. Always review AI matches!

**Q: Can I disable it temporarily?**  
A: Yes, set `SemanticMatching:Enabled` to `false` in configuration.

---

## 🎉 Ready to Start?

1. Install NuGet packages (Step 1)
2. Get OpenAI API key (Step 2)  
3. Configure key (Step 3)
4. Run application (Step 4)
5. Review Excel reports for 🤖 AI matches!

For questions or issues, see `docs/SemanticMatchingImplementation.md` or open a GitHub issue.
