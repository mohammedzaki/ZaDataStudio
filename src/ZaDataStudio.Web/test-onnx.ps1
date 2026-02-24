# ONNX Quick Test Script
# Run this from: src\ZaDataStudio.Web directory
# Usage: .\test-onnx.ps1

Write-Host "`n=== ONNX Embedding Service Test ===" -ForegroundColor Cyan
Write-Host ""

# Check if we're in the right directory
if (-not (Test-Path "Models\all-MiniLM-L6-v2.onnx")) {
    Write-Host "❌ ERROR: ONNX model not found!" -ForegroundColor Red
    Write-Host "Please run this script from: src\ZaDataStudio.Web" -ForegroundColor Yellow
    Write-Host "Or download the model first using Download_ONNX_Model.md" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Model file found" -ForegroundColor Green
Write-Host "  Location: $(Get-Location)\Models\all-MiniLM-L6-v2.onnx" -ForegroundColor Gray
Write-Host "  Size: $([math]::Round((Get-Item 'Models\all-MiniLM-L6-v2.onnx').Length / 1MB, 2)) MB" -ForegroundColor Gray
Write-Host ""

# Create a minimal test program
$testCode = @"
using ZaDataStudio.Application.Mapping;

var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "Models", "all-MiniLM-L6-v2.onnx");
Console.WriteLine("Loading ONNX model...");

try
{
    var service = new LocalOnnxEmbeddingService(modelPath, maxTokens: 128);
    Console.WriteLine("✓ Model loaded successfully!");
    
    Console.WriteLine("\nGenerating test embeddings...");
    var embeddings = await service.GenerateEmbeddingsAsync(new[] { "Sport", "Sports Volunteering" });
    Console.WriteLine($"✓ Generated {embeddings.Count} embeddings (dimension: {embeddings[0].Length})");
    
    Console.WriteLine("\nTesting semantic matching...");
    var matcher = new SemanticLookupMatcher(service, 0.70);
    var (match, similarity) = await matcher.FindBestMatchAsync("Sports Volunteering", new[] { "Sport", "Education" });
    Console.WriteLine($"✓ Best match: '{match}' (similarity: {similarity:P0})");
    
    if (match == "Sport" && similarity > 0.70)
    {
        Console.WriteLine("\n✅ SUCCESS: ONNX is working correctly!");
        return 0;
    }
    else
    {
        Console.WriteLine("\n⚠️ WARNING: Unexpected result");
        return 1;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ ERROR: {ex.Message}");
    return 1;
}
"@

# Save test program
$testFile = "OnnxQuickTest.cs"
$testCode | Out-File -FilePath $testFile -Encoding UTF8

Write-Host "Running test..." -ForegroundColor Yellow
Write-Host ""

# Run the test using dotnet script (if available) or create temp project
try {
    # Try using dotnet-script first
    $result = & dotnet script $testFile 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host $result -ForegroundColor Green
        Remove-Item $testFile -Force
        exit 0
    }
}
catch {
    # dotnet-script not available, use regular dotnet run
}

# Create a temporary project and run it
Write-Host "Creating temporary test project..." -ForegroundColor Gray

$tempDir = "TempOnnxTest"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

# Create project file
$csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ZaDataStudio.Application.csproj" />
  </ItemGroup>
</Project>
"@

$csproj | Out-File -FilePath "$tempDir\TempOnnxTest.csproj" -Encoding UTF8

# Copy test code as Program.cs
$testCode | Out-File -FilePath "$tempDir\Program.cs" -Encoding UTF8

# Run the test
Push-Location $tempDir
try {
    Write-Host ""
    $output = & dotnet run --project . 2>&1
    Write-Host $output
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✅ Test completed successfully!" -ForegroundColor Green
    }
    else {
        Write-Host "`n❌ Test failed!" -ForegroundColor Red
    }
}
finally {
    Pop-Location
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $testFile -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
