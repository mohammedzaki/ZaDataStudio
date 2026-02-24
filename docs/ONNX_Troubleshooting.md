# ONNX Troubleshooting Guide

## ✅ Setup Checklist

Run these commands to verify your ONNX setup:

```powershell
# 1. Verify model file exists
Test-Path "src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx"  # Should return: True

# 2. Verify vocab file exists
Test-Path "src\ZaDataStudio.Web\Models\vocab.txt"  # Should return: True

# 3. Check file sizes
Get-ChildItem "src\ZaDataStudio.Web\Models" | Select-Object Name, @{Name="Size(MB)";Expression={[math]::Round($_.Length/1MB, 2)}}
# Expected output:
# all-MiniLM-L6-v2.onnx: ~86 MB
# vocab.txt: ~0.22 MB

# 4. Verify configuration
Get-Content "src\ZaDataStudio.Web\appsettings.Development.json" | Select-String -Pattern "Provider|ModelPath|MaxTokens"
# Expected:
# "Provider": "Onnx"
# "ModelPath": "Models/all-MiniLM-L6-v2.onnx"
# "MaxTokens": 128
```

## 🐛 Common Issues & Solutions

### Issue 1: App Hangs at "Loading ONNX model"

**Symptoms:**
- Console shows "Loading ONNX model from: ..."
- App freezes/never proceeds
- No error messages

**Causes & Solutions:**

#### A. Model file corrupted
```powershell
# Check file size
(Get-Item "src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx").Length / 1MB
# Should be ~86 MB. If less, download was incomplete.

# Re-download
Remove-Item "src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx" -Force
Invoke-WebRequest -Uri "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx" -OutFile "src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx"
```

#### B. Wrong model format
The model MUST be from the `/onnx/` folder on Hugging Face.
- ❌ Wrong: `model.onnx` from root
- ✅ Correct: `onnx/model.onnx` (optimized version)

#### C. ONNX Runtime version mismatch
```powershell
# Check installed version
dotnet list src\ZaDataStudio.Application\ZaDataStudio.Application.csproj package | Select-String "OnnxRuntime"
# Should be: Microsoft.ML.OnnxRuntime 1.24.2 or higher

# Update if needed
dotnet add src\ZaDataStudio.Application package Microsoft.ML.OnnxRuntime --version 1.24.2
```

### Issue 2: App Hangs During First Embedding Generation

**Symptoms:**
- Model loads successfully
- Hangs at first `GenerateEmbeddingAsync()` call
- Console shows: "Generating embeddings..."

**Causes & Solutions:**

#### A. Insufficient memory
ONNX model needs ~500MB RAM for inference.

```powershell
# Check available memory
Get-CimInstance Win32_OperatingSystem | Select-Object FreePhysicalMemory
# Should have at least 1GB free (1048576 KB)
```

**Solution:** Close other applications or increase RAM.

#### B. First inference is slow
First embedding generation is slower (warm-up).

**Expected times:**
- First call: 2-5 seconds (model initialization)
- Subsequent calls: 10-50ms

**Solution:** Wait 10 seconds. If still hanging, there's another issue.

#### C. Tensor dimension mismatch
Check console for errors like: "Tensor dimensions don't match"

```powershell
# Verify vocab.txt is correct
(Get-Content "src\ZaDataStudio.Web\Models\vocab.txt" | Measure-Object -Line).Lines
# Should be ~30,522 lines (BERT vocabulary size)
```

### Issue 3: "Model not found" Error

**Symptoms:**
- Error: "ONNX model not found at: Models/all-MiniLM-L6-v2.onnx"
- Even though file exists

**Cause:** Path resolution issue (relative vs absolute)

**Solution:**

#### Check working directory:
```csharp
// In Program.cs, add this debug line:
Console.WriteLine($"Working directory: {Directory.GetCurrentDirectory()}");
Console.WriteLine($"Content root: {builder.Environment.ContentRootPath}");
```

#### Fix path in appsettings:
```json
// Try absolute path instead:
{
  "Onnx": {
    "ModelPath": "D:/Me/Work/Applications/ZaDataStudio/02-Code/ZaDataStudio/src/ZaDataStudio.Web/Models/all-MiniLM-L6-v2.onnx"
  }
}
```

### Issue 4: Out of Memory Exception

**Symptoms:**
- `OutOfMemoryException` during inference
- App crashes after a few embeddings

**Cause:** Memory leak or not disposing resources

**Solution:**

Already fixed in the code with `using var results = _session.Run(inputs);`

If still occurring:
1. Reduce `MaxTokens` from 128 to 64
2. Process in smaller batches
3. Add explicit GC collection:

```csharp
// After batch processing
GC.Collect();
GC.WaitForPendingFinalizers();
```

### Issue 5: Poor Match Quality

**Symptoms:**
- Semantic matches seem wrong
- Similarity scores all very low (<0.5)

**Causes & Solutions:**

#### A. Threshold too high
```json
{
  "SemanticMatching": {
    "SimilarityThreshold": 0.70  // Try lowering to 0.60
  }
}
```

#### B. Vocabulary issue
```powershell
# Check if vocab.txt is loaded
# Look for console message: "Warning: vocab.txt not found"
# If present, ensure vocab.txt is in same folder as .onnx file
```

#### C. Wrong tokenization
The model expects lowercase, punctuation-free text.

**Debug tokenization:**
```csharp
// Add this to LocalOnnxEmbeddingService.Tokenize()
Console.WriteLine($"Input: '{text}' -> Tokens: {string.Join(",", tokens)}");
```

## 🧪 Testing ONNX Setup

### Quick Test in Browser Developer Console

1. Run your Blazor app
2. Open browser Developer Tools (F12)
3. Navigate to lookup analysis page
4. Watch console output for:
   - ✅ "Loading ONNX model from: ..."
   - ✅ "✓ ONNX model loaded successfully"
   - ✅ "Semantic match: 'X' → 'Y' (similarity: Z%)"

### Command Line Test

```powershell
# Run test program
cd src\ZaDataStudio.Tests
dotnet run --project . OnnxTest.cs

# Expected output:
# Loading model from: ...
# ✓ Model loaded successfully!
# Generating embeddings...
# ✓ Generated 4 embeddings
# - Embedding dimension: 384
# Testing semantic similarity:
#   Query: 'Sports Volunteering'
#   Best Match: 'Sport' (similarity: 85%)
# ✅ SUCCESS: ONNX semantic matching is working correctly!
```

## 📊 Performance Benchmarks

### Normal Performance:
- Model loading: 1-3 seconds
- First embedding: 2-5 seconds (warm-up)
- Subsequent embeddings: 10-50ms each
- Batch of 100: 1-2 seconds total

### Slow Performance (Investigate):
- Model loading: >10 seconds
- First embedding: >10 seconds
- Subsequent embeddings: >200ms each
- Batch of 100: >10 seconds

**If slow, check:**
1. Hard drive speed (SSD vs HDD)
2. Available RAM
3. CPU usage (other processes)
4. Antivirus interference

## 🔧 Advanced Diagnostics

### Enable Verbose ONNX Logging

In `LocalOnnxEmbeddingService.cs` constructor:

```csharp
var sessionOptions = new SessionOptions();
sessionOptions.LogVerbosityLevel = 0; // 0 = Verbose, 1 = Info, 2 = Warning
sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;

_session = new InferenceSession(modelPath, sessionOptions);
```

### Monitor Memory Usage

```powershell
# In another PowerShell window while app is running:
while ($true) {
    Get-Process -Name "ZaDataStudio.Web" | Select-Object Name, @{Name="Memory(MB)";Expression={[math]::Round($_.WorkingSet64/1MB, 2)}}
    Start-Sleep -Seconds 5
}

# Expected: 
# Initial: ~200-300 MB
# After loading model: ~500-700 MB
# During inference: ~700-1000 MB
# ⚠️ If growing continuously: memory leak!
```

### Check ONNX Runtime Dependencies

```powershell
# List all ONNX-related DLLs
Get-ChildItem "src\ZaDataStudio.Web\bin\Debug\net10.0\" -Recurse -Filter "*onnx*" | Select-Object Name, Length

# Should include:
# - Microsoft.ML.OnnxRuntime.dll
# - onnxruntime.dll (native)
# - onnxruntime_providers_shared.dll
```

## 🚑 Emergency Fallback

If ONNX continues to fail and you need to proceed:

### Temporary: Switch to OpenAI

```json
{
  "SemanticMatching": {
    "Provider": "OpenAI"  // Change from "Onnx"
  }
}
```

You already have an API key configured, so this will work immediately.

### Disable Semantic Matching

```json
{
  "SemanticMatching": {
    "Enabled": false
  }
}
```

App will work but only use exact string matching.

## 📝 Reporting Issues

If none of these solutions work, provide:

1. **Console output** (all messages)
2. **File verification**:
   ```powershell
   Get-ChildItem "src\ZaDataStudio.Web\Models" -Recurse | Select-Object FullName, Length
   ```
3. **System info**:
   ```powershell
   Get-ComputerInfo | Select-Object OsName, OsArchitecture, TotalPhysicalMemory
   ```
4. **Package versions**:
   ```powershell
   dotnet list src\ZaDataStudio.Application package
   ```

---

## ✅ Success Checklist

- [ ] Model file exists (86 MB)
- [ ] Vocab file exists (0.22 MB)
- [ ] Configuration has `"Provider": "Onnx"`
- [ ] MaxTokens is 128
- [ ] Console shows "✓ ONNX model loaded successfully"
- [ ] First embedding completes in <10 seconds
- [ ] Semantic matches appear in lookup analysis
- [ ] Similarity scores are reasonable (0.70-0.95)

If all checked ✅, your ONNX setup is working correctly!
