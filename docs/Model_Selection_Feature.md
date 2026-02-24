# Model Selection Feature for Semantic Matching

## Overview
Added the ability to select different embedding models for each AI provider directly in the UI, giving users fine-grained control over cost, performance, and accuracy trade-offs.

## What Changed

### 1. Settings Service Updated
**File**: `src\ZaDataStudio.Application\Mapping\SemanticMatchingSettingsService.cs`

Added new properties and methods:
- `Model` property - Stores selected model name
- `GetAvailableModels()` - Returns available models for current provider
- `GetModelDescription()` - Returns description with dimensions and cost info
- Auto-selects appropriate default model when provider changes

### 2. UI Component Enhanced
**File**: `src\ZaDataStudio.Web\Components\SemanticMatchingSettings.razor`

Added model selection dropdown:
- Shows available models for selected provider
- Displays model description (dimensions, cost, speed)
- Shows restart warning when model changes
- Updates automatically when provider changes

### 3. Configuration Loading
**File**: `src\ZaDataStudio.Web\Program.cs`

Loads model from appsettings.json based on provider:
- OpenAI: Reads from `OpenAI:Model`
- Azure OpenAI: Reads from `AzureOpenAI:DeploymentName`
- ONNX: Uses filename (all-MiniLM-L6-v2)

## Available Models

### OpenAI Models
| Model | Dimensions | Cost | Best For |
|-------|-----------|------|----------|
| **text-embedding-3-small** | 1536 | $0.02/1M tokens | General purpose (default) |
| **text-embedding-3-large** | 3072 | $0.13/1M tokens | High accuracy tasks |
| **text-embedding-ada-002** | 1536 | $0.10/1M tokens | Legacy compatibility |

### ONNX Models (Local)
| Model | Dimensions | Cost | Best For |
|-------|-----------|------|----------|
| **all-MiniLM-L6-v2** | 384 | Free | Fast, general purpose (default) |
| **all-mpnet-base-v2** | 768 | Free | Higher quality, slower |
| **paraphrase-multilingual-MiniLM-L12-v2** | 384 | Free | Multiple languages |

### Azure OpenAI Models
| Model | Dimensions | Cost | Best For |
|-------|-----------|------|----------|
| **text-embedding-ada-002** | 1536 | Varies | Standard (default) |
| **text-embedding-3-small** | 1536 | Varies | Cost-effective |
| **text-embedding-3-large** | 3072 | Varies | High accuracy |

## How to Use

### In the UI

1. **Navigate to Schema Comparison**
   - Go to `/schema-comparison`
   - Scroll to "AI Semantic Matching Settings" card

2. **Select Provider**
   - Choose OpenAI, ONNX, or Azure OpenAI

3. **Select Model**
   - Dropdown automatically shows models for chosen provider
   - See description with dimensions and cost
   - Default model is pre-selected

4. **Apply Changes**
   - ⚠️ **Restart required** to apply model changes
   - Shows warning message when model is changed

### Via Configuration

**For OpenAI**:
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "text-embedding-3-small"  // ← Change this
  },
  "SemanticMatching": {
    "Provider": "OpenAI"
  }
}
```

**For ONNX**:
```json
{
  "Onnx": {
    "ModelPath": "Models/all-MiniLM-L6-v2.onnx",  // ← File determines model
    "MaxTokens": 128
  },
  "SemanticMatching": {
    "Provider": "Onnx"
  }
}
```

**For Azure OpenAI**:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://...",
    "ApiKey": "...",
    "DeploymentName": "text-embedding-ada-002"  // ← Change this
  },
  "SemanticMatching": {
    "Provider": "AzureOpenAI"
  }
}
```

## Model Selection Strategy

### When to Use text-embedding-3-small (OpenAI)
✅ Most common use case  
✅ Good balance of cost and quality  
✅ Fast inference  
✅ 1536 dimensions (same as ada-002)  
✅ **5x cheaper** than ada-002

**Use for**: General lookup matching, daily operations

### When to Use text-embedding-3-large (OpenAI)
✅ Need highest accuracy  
✅ Critical matching tasks  
✅ 3072 dimensions (2x more than small)  
✅ Budget allows for higher cost

**Use for**: Important migrations, final production runs

### When to Use text-embedding-ada-002 (OpenAI/Azure)
✅ Legacy compatibility  
✅ Already using in other systems  
✅ Azure OpenAI default

**Use for**: Consistency with existing systems

### When to Use all-MiniLM-L6-v2 (ONNX)
✅ No internet required  
✅ No API costs  
✅ Fast inference  
✅ Good general purpose quality

**Use for**: Development, offline work, cost savings

### When to Use all-mpnet-base-v2 (ONNX)
✅ Higher quality than MiniLM  
✅ Still free and offline  
✅ Acceptable slower speed  
✅ 768 dimensions (2x MiniLM)

**Use for**: When accuracy matters more than speed

### When to Use paraphrase-multilingual (ONNX)
✅ Multi-language support  
✅ Arabic + English lookup values  
✅ Free and offline

**Use for**: International data with mixed languages

## Cost Comparison

### Example: 100,000 tokens processed

| Provider | Model | Cost | Speed |
|----------|-------|------|-------|
| OpenAI | text-embedding-3-small | $0.002 | ⚡⚡⚡ Fast |
| OpenAI | text-embedding-3-large | $0.013 | ⚡⚡ Medium |
| OpenAI | text-embedding-ada-002 | $0.010 | ⚡⚡⚡ Fast |
| ONNX | all-MiniLM-L6-v2 | **Free** | ⚡ Slow |
| ONNX | all-mpnet-base-v2 | **Free** | 🐌 Slower |
| Azure | (varies by region) | $0.XXX | ⚡⚡ Medium |

## UI Features

### Model Dropdown
- Automatically populated based on selected provider
- Shows current selection
- Auto-updates when provider changes

### Model Description Box
Shows for each model:
- **Dimensions**: Vector size (affects quality)
- **Cost**: Price per 1M tokens (OpenAI models only)
- **Recommendation**: Best use case

### Restart Warning
Appears when model is changed:
> ⚠️ **Note:** Changing model requires application restart to take effect.

### Configuration Summary
Shows at bottom of card:
```
✓ Provider: OpenAI (Cloud, Paid)
✓ Model: text-embedding-3-small
✓ Threshold: 75%
✓ Status: Ready
```

## Technical Notes

### Provider-Model Relationship
- Each provider has its own set of available models
- Changing provider auto-selects default model for that provider
- Model dropdown is dynamically populated

### Auto-Selection Logic
```csharp
Provider Changed → Auto-select default model:
- OpenAI → text-embedding-3-small
- ONNX → all-MiniLM-L6-v2
- AzureOpenAI → text-embedding-ada-002
```

### ONNX Model Notes
For ONNX, the dropdown shows model names, but the actual model used is determined by:
1. The ONNX file in `Models/` directory
2. Currently only `all-MiniLM-L6-v2.onnx` is downloaded

To use other ONNX models:
1. Download from HuggingFace
2. Place in `Models/` directory
3. Update `ModelPath` in appsettings.json

## Testing Different Models

### Test Scenario 1: Cost vs Quality
```
1. Use text-embedding-3-small at 75% threshold
2. Note: Match count, accuracy, cost
3. Use text-embedding-3-large at 75% threshold
4. Compare: Did quality improve? Worth the 6.5x cost?
```

### Test Scenario 2: Free vs Paid
```
1. Use ONNX all-MiniLM-L6-v2 at 75%
2. Note: Match count, processing time
3. Use OpenAI text-embedding-3-small at 75%
4. Compare: Speed difference, accuracy difference
```

### Test Scenario 3: Model Size Impact
```
1. Use all-MiniLM-L6-v2 (384 dim) at 75%
2. Use all-mpnet-base-v2 (768 dim) at 75%
3. Compare: Does 2x dimensions improve matching?
```

## Recommendations

### For Development
🔧 **Use**: ONNX all-MiniLM-L6-v2
- Free, fast enough for testing
- No API key management

### For Production (Budget-Conscious)
💰 **Use**: OpenAI text-embedding-3-small
- Best cost/quality ratio
- Fast and reliable
- Only $0.02 per 1M tokens

### For Production (Quality-First)
⭐ **Use**: OpenAI text-embedding-3-large
- Highest accuracy
- Worth the cost for critical migrations

### For Offline/Air-Gapped
🔒 **Use**: ONNX all-mpnet-base-v2
- Best quality without internet
- Acceptable speed for batch processing

### For Multilingual Data
🌍 **Use**: ONNX paraphrase-multilingual-MiniLM-L12-v2
- Handles Arabic + English
- Good for international applications

## Troubleshooting

### "Model not found" Error
**Cause**: Selected ONNX model file doesn't exist  
**Solution**: 
1. Check `Models/` directory
2. Verify filename matches selected model
3. Download missing model from HuggingFace

### "Invalid deployment name" (Azure)
**Cause**: Deployment name in UI doesn't match Azure portal  
**Solution**:
1. Go to Azure OpenAI Studio
2. Check actual deployment name
3. Update in UI or appsettings.json

### Different Results with Same Threshold
**Cause**: Different models have different vector spaces  
**Expected**: Normal behavior  
**Action**: Adjust threshold for each model independently

## Future Enhancements

1. **Download ONNX models from UI**
   - One-click download of additional models
   - Progress indicator during download

2. **Model performance metrics**
   - Show actual processing time
   - Compare cost per analysis

3. **A/B testing**
   - Run same data through different models
   - Side-by-side comparison

4. **Custom ONNX models**
   - Upload your own fine-tuned models
   - Support for domain-specific embeddings

## Summary

Model selection adds powerful flexibility:
- ✅ Choose based on cost, speed, or quality
- ✅ Test different models easily
- ✅ Optimize for your specific use case
- ✅ Visual feedback and recommendations
- ✅ Seamless integration with existing UI

**Current settings visible in UI at all times!**
