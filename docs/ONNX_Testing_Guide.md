# ONNX Testing Guide - FIXED VERSION

## ✅ What Was Fixed

The original `OnnxTest.cs` had these issues:
1. ❌ Wrong async Main pattern for .NET 10
2. ❌ Hardcoded relative path that only worked from specific directories
3. ❌ No proper project file
4. ❌ Couldn't inject the service for testing

### Fixed Solutions:

1. **✅ Updated OnnxTest.cs** - Now uses top-level statements and tries multiple paths
2. **✅ Created ZaDataStudio.Tests.csproj** - Proper project configuration
3. **✅ Created TestOnnx.razor** - Blazor test page (EASIEST!)
4. **✅ Created test-onnx.ps1** - PowerShell script for quick testing
5. **✅ Fixed DI registration** - LocalOnnxEmbeddingService now injectable

---

## 🚀 How to Test ONNX (3 Options)

### Option 1: Blazor Test Page (RECOMMENDED - Easiest!)

1. **Run your Blazor app:**
   ```powershell
   cd src\ZaDataStudio.Web
   dotnet run
   ```

2. **Navigate to test page:**
   ```
   https://localhost:5001/test-onnx
   ```

3. **Click "Run ONNX Test" button**

4. **Check results on screen:**
   - ✅ GREEN = Success
   - ❌ RED = Error with details

**Advantages:**
- No command line needed
- Visual output
- Shows embeddings and similarity scores
- Real DI container (tests actual configuration)

---

### Option 2: PowerShell Script (Quick)

1. **Navigate to Web directory:**
   ```powershell
   cd src\ZaDataStudio.Web
   ```

2. **Run the test script:**
   ```powershell
   .\test-onnx.ps1
   ```

3. **Check output:**
   ```
   === ONNX Embedding Service Test ===
   ✓ Model file found
   Loading ONNX model...
   ✓ Model loaded successfully!
   ✓ Generated 2 embeddings (dimension: 384)
   ✓ Best match: 'Sport' (similarity: 85%)
   ✅ SUCCESS: ONNX is working correctly!
   ```

**Advantages:**
- Fast
- Standalone (doesn't require app to be running)
- Good for CI/CD

---

### Option 3: Console Test Project (Advanced)

1. **Build the test project:**
   ```powershell
   cd src\ZaDataStudio.Tests
   dotnet build
   ```

2. **Run the test:**
   ```powershell
   dotnet run
   ```

3. **Check output:**
   ```
   === ONNX Embedding Service Test ===
   Loading model from: D:\...\Models\all-MiniLM-L6-v2.onnx
   ✓ Model loaded successfully!
   ...
   ```

**Advantages:**
- Can add to test suite
- Can run in CI/CD
- Reusable

---

## 🐛 Troubleshooting

### Issue: "ONNX model not found"

**Solution 1:** Run from correct directory
```powershell
# For Blazor page or PowerShell script:
cd src\ZaDataStudio.Web

# For console test:
cd src\ZaDataStudio.Tests
```

**Solution 2:** Check model exists
```powershell
Test-Path "src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx"
# Should return: True
```

**Solution 3:** Re-download if corrupted
```powershell
cd src\ZaDataStudio.Web
Remove-Item "Models\all-MiniLM-L6-v2.onnx" -Force
Invoke-WebRequest -Uri "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx" -OutFile "Models\all-MiniLM-L6-v2.onnx"
```

### Issue: "ONNX service not registered" (Blazor page)

**Check appsettings.Development.json:**
```json
{
  "SemanticMatching": {
    "Provider": "Onnx",  // Must be "Onnx"
    "Enabled": true       // Must be true
  }
}
```

**Restart the app after changing config!**

### Issue: Test hangs/takes forever

**Normal timings:**
- First run: 5-10 seconds (model initialization)
- Subsequent: 1-2 seconds

**If >30 seconds:**
1. Model file may be corrupted (re-download)
2. Not enough RAM (need ~1GB free)
3. Antivirus scanning model file (add exception)

### Issue: Build errors

**Solution:**
```powershell
# Clean and rebuild
dotnet clean
dotnet build
```

**Check packages:**
```powershell
dotnet list src\ZaDataStudio.Application package | Select-String "OnnxRuntime"
# Should see: Microsoft.ML.OnnxRuntime 1.24.2
```

---

## 📊 Expected Test Results

### Successful Test Output:

```
=== ONNX Embedding Service Test ===

Loading model from: D:\...\Models\all-MiniLM-L6-v2.onnx
✓ Model loaded successfully!

Generating embeddings...
✓ Generated 4 embeddings
  - Embedding dimension: 384

Testing semantic similarity:
  Query: 'Sports Volunteering'
  Best Match: 'Sport' (similarity: 85%)

✅ SUCCESS: ONNX semantic matching is working correctly!
```

### What Each Line Means:

- **Loading model** - Reading 86MB file from disk
- **Model loaded** - ONNX Runtime initialized successfully
- **Generating embeddings** - Converting text to 384-dim vectors
- **Embedding dimension: 384** - Correct for all-MiniLM-L6-v2
- **Best Match: 'Sport'** - Semantic similarity working
- **similarity: 85%** - High confidence match
- **SUCCESS** - All tests passed!

---

## 🎯 Quick Verification Checklist

Before running tests:

- [ ] Model file exists (86 MB)
- [ ] Vocab file exists (0.22 MB)
- [ ] Configuration: `Provider: "Onnx"`
- [ ] Configuration: `Enabled: true`
- [ ] App builds without errors
- [ ] At least 1GB free RAM

---

## 🔧 File Locations

**Test Files Created/Fixed:**

1. `src\ZaDataStudio.Tests\OnnxTest.cs` - Console test (FIXED)
2. `src\ZaDataStudio.Tests\ZaDataStudio.Tests.csproj` - Project file (NEW)
3. `src\ZaDataStudio.Web\Components\Pages\TestOnnx.razor` - Blazor page (NEW)
4. `src\ZaDataStudio.Web\test-onnx.ps1` - PowerShell script (NEW)
5. `docs\ONNX_Testing_Guide.md` - This file (NEW)

**Model Files:**

1. `src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx` (86 MB)
2. `src\ZaDataStudio.Web\Models\vocab.txt` (0.22 MB)

---

## 💡 Pro Tips

1. **Use Blazor test page for quick checks** - It's visual and uses real DI
2. **Use PowerShell script for automation** - Great for CI/CD pipelines
3. **Use console test for unit testing** - Can be part of test suite
4. **First run is always slower** - 5-10 seconds is normal
5. **Check logs** - Console output shows exactly what's happening

---

## 🎉 Success Indicators

You'll know ONNX is working when:

✅ Console shows: "✓ Model loaded successfully"  
✅ Embeddings generated in < 10 seconds  
✅ Dimension is 384  
✅ "Sport" matches "Sports Volunteering" at >70%  
✅ No errors or timeouts  

---

## 📞 Still Having Issues?

1. **Check troubleshooting guide:** `docs\ONNX_Troubleshooting.md`
2. **Verify setup:** `docs\ONNX_Ready_To_Run.md`
3. **Check console logs** for specific error messages
4. **Try all 3 test methods** to isolate the issue

---

**Recommended:** Start with the **Blazor test page** (`/test-onnx`) - it's the easiest and most visual way to verify ONNX is working! 🚀
