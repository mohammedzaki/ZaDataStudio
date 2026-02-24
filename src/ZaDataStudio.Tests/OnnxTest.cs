using ZaDataStudio.Application.Mapping;

Console.WriteLine("=== ONNX Embedding Service Test ===\n");

try
{
    // Find the model path (works from any run location)
    string modelPath;
    var currentDir = Directory.GetCurrentDirectory();

    // Try multiple possible locations
    var possiblePaths = new[]
    {
        Path.Combine(currentDir, "Models", "all-MiniLM-L6-v2.onnx"),
        Path.Combine(currentDir, "..", "ZaDataStudio.Web", "Models", "all-MiniLM-L6-v2.onnx"),
        Path.Combine(currentDir, "..", "..", "ZaDataStudio.Web", "Models", "all-MiniLM-L6-v2.onnx"),
        Path.Combine(currentDir, "..", "..", "..", "ZaDataStudio.Web", "Models", "all-MiniLM-L6-v2.onnx"),
        Path.Combine(currentDir, "..", "..", "..", "..", "ZaDataStudio.Web", "Models", "all-MiniLM-L6-v2.onnx"),
        Path.Combine(currentDir, "..", "..", "..", "..", "..", "src", "ZaDataStudio.Web", "Models", "all-MiniLM-L6-v2.onnx")
    };

    modelPath = possiblePaths.FirstOrDefault(File.Exists) 
        ?? throw new FileNotFoundException(
            "ONNX model not found. Tried:\n" + 
            string.Join("\n", possiblePaths.Select(p => $"  - {Path.GetFullPath(p)}")));

    Console.WriteLine($"Loading model from: {Path.GetFullPath(modelPath)}");
    var service = new LocalOnnxEmbeddingService(modelPath, maxTokens: 128);

    Console.WriteLine("✓ Model loaded successfully!\n");

    // Test embedding generation
    var testTexts = new[] { "Sport", "Sports Volunteering", "Education", "Technology" };

    Console.WriteLine("Generating embeddings...");
    var embeddings = await service.GenerateEmbeddingsAsync(testTexts);

    Console.WriteLine($"✓ Generated {embeddings.Count} embeddings");
    Console.WriteLine($"  - Embedding dimension: {embeddings[0].Length}\n");

    // Test semantic similarity
    Console.WriteLine("Testing semantic similarity:");
    var matcher = new SemanticLookupMatcher(service, similarityThreshold: 0.70);

    var (match, similarity) = await matcher.FindBestMatchAsync(
        "Sports Volunteering", 
        new[] { "Sport", "Education", "Technology" });

    Console.WriteLine($"  Query: 'Sports Volunteering'");
    Console.WriteLine($"  Best Match: '{match}' (similarity: {similarity:P0})");

    if (match == "Sport" && similarity > 0.70)
    {
        Console.WriteLine("\n✅ SUCCESS: ONNX semantic matching is working correctly!");
    }
    else
    {
        Console.WriteLine("\n⚠️ WARNING: Unexpected result. Check model configuration.");
    }

    // Test batch semantic similarity
    Console.WriteLine("Testing batch semantic similarity:");
    var matchResult = await matcher.BatchMatchAsync(
        new[] { "Sports Volunteering", "Education" },
        new[] { "Sport", "Education", "Technology" });

    Console.WriteLine($"  Query Array: 'Sports Volunteering', 'Education'");
    // Console.WriteLine($"  Best Match: '{match}' (similarity: {similarity:P0})");

    foreach (var query in matchResult)
    {
        Console.WriteLine($"  Query: '{query.Key}' => Best Match: '{query.Value.Match}' (similarity: {query.Value.Similarity:P0})");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ ERROR: {ex.Message}");
    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
    Environment.Exit(1);
}

Console.WriteLine("\nTest completed successfully!");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

