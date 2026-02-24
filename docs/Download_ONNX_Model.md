# Download ONNX Model - Quick Script

This file provides quick commands to download the all-MiniLM-L6-v2 ONNX model.

## Option 1: PowerShell Download Script (Recommended)

Copy and run this script in PowerShell:

```powershell
# Navigate to Web project
cd src\ZaDataStudio.Web

# Create Models directory
New-Item -ItemType Directory -Force -Path Models

# Download model (80 MB)
Write-Host "Downloading all-MiniLM-L6-v2.onnx (80 MB)..." -ForegroundColor Green
Invoke-WebRequest `
    -Uri "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx" `
    -OutFile "Models/all-MiniLM-L6-v2.onnx"

# Download vocabulary (231 KB)
Write-Host "Downloading vocab.txt (231 KB)..." -ForegroundColor Green
Invoke-WebRequest `
    -Uri "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt" `
    -OutFile "Models/vocab.txt"

Write-Host "Download complete!" -ForegroundColor Green
Write-Host "Files saved to: Models\" -ForegroundColor Cyan

# Verify files
Write-Host "`nVerifying downloads..." -ForegroundColor Yellow
if (Test-Path "Models/all-MiniLM-L6-v2.onnx") {
    $size = (Get-Item "Models/all-MiniLM-L6-v2.onnx").Length / 1MB
    Write-Host "✓ all-MiniLM-L6-v2.onnx - $([math]::Round($size, 2)) MB" -ForegroundColor Green
} else {
    Write-Host "✗ all-MiniLM-L6-v2.onnx - Missing!" -ForegroundColor Red
}

if (Test-Path "Models/vocab.txt") {
    $size = (Get-Item "Models/vocab.txt").Length / 1KB
    Write-Host "✓ vocab.txt - $([math]::Round($size, 2)) KB" -ForegroundColor Green
} else {
    Write-Host "✗ vocab.txt - Missing!" -ForegroundColor Red
}

Write-Host "`n✅ Setup complete! You can now run the application with ONNX." -ForegroundColor Green
Write-Host "   Run: dotnet run" -ForegroundColor Cyan
```

## Option 2: Manual Download

1. **Download model.onnx**:
   - Visit: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/blob/main/onnx/model.onnx
   - Click "download" button (80 MB)
   - Save to `src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx`

2. **Download vocab.txt**:
   - Visit: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/blob/main/vocab.txt
   - Click "download" button (231 KB)
   - Save to `src\ZaDataStudio.Web\Models\vocab.txt`

## Option 3: Using Curl (Linux/Mac/Git Bash)

```bash
cd src/ZaDataStudio.Web
mkdir -p Models

# Download model
curl -L -o Models/all-MiniLM-L6-v2.onnx \
  "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx"

# Download vocabulary
curl -L -o Models/vocab.txt \
  "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt"

echo "✅ Download complete!"
ls -lh Models/
```

## Option 4: Using wget (Linux)

```bash
cd src/ZaDataStudio.Web
mkdir -p Models

# Download files
wget -O Models/all-MiniLM-L6-v2.onnx \
  "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx"

wget -O Models/vocab.txt \
  "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt"

echo "✅ Download complete!"
ls -lh Models/
```

## Verify Installation

After downloading, run this to verify:

```powershell
cd src\ZaDataStudio.Web

# Check files exist
Test-Path Models\all-MiniLM-L6-v2.onnx  # Should return True
Test-Path Models\vocab.txt              # Should return True

# Check file sizes
(Get-Item Models\all-MiniLM-L6-v2.onnx).Length / 1MB  # Should be ~80 MB
(Get-Item Models\vocab.txt).Length / 1KB              # Should be ~231 KB
```

Expected output:
```
True
True
80.22
231.45
```

## Test the Application

```powershell
dotnet run
```

Look for this message:
```
Semantic matching enabled with ONNX model: D:\...\src\ZaDataStudio.Web\Models\all-MiniLM-L6-v2.onnx
```

If you see:
```
Warning: ONNX model not found at ...
```

Then the download didn't work. Try another option above.

## Troubleshooting

### Download fails with "403 Forbidden"

Hugging Face may require authentication. Use Git LFS instead:

```bash
git lfs install
git clone https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2
cd all-MiniLM-L6-v2
copy onnx/model.onnx ../src/ZaDataStudio.Web/Models/all-MiniLM-L6-v2.onnx
copy vocab.txt ../src/ZaDataStudio.Web/Models/vocab.txt
```

### Download is slow

The model is 80 MB. On slow connections:
1. Download overnight
2. Use a download manager (e.g., Free Download Manager)
3. Download on faster network and copy file

### File corrupted

Verify SHA256 hash:

```powershell
Get-FileHash Models\all-MiniLM-L6-v2.onnx -Algorithm SHA256
```

Compare with official hash from Hugging Face. If different, re-download.

## Alternative: Use Docker

If downloads keep failing, use our Docker image (coming soon):

```bash
docker pull zadatastudio/onnx-models:latest
docker cp zadatastudio-onnx:/models/. src/ZaDataStudio.Web/Models/
```

## Need Help?

- Check `docs\OnnxSemanticMatching_Setup.md` for detailed guide
- Open GitHub issue if download links are broken
- Contact support if you need the model files via other means

---

**Quick Start**: Copy the PowerShell script above and run it. That's it!
