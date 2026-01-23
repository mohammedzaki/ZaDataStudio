namespace ZaDataStudio.Domain.Entities;

public class ComparisonSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string SourceConnectionString { get; set; } = string.Empty;
    public string DestinationConnectionString { get; set; } = string.Empty;
    public List<TableMapping> TableMappings { get; set; } = new();
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    
    // Additional session data
    public List<string> SourceTables { get; set; } = new();
    public List<string> DestinationTables { get; set; } = new();
    public Dictionary<string, List<string>> TableColumnCache { get; set; } = new();
    public HashSet<int> ExpandedTableIndices { get; set; } = new();
    
    // Connection test results
    public ConnectionTestResult? SourceTestResult { get; set; }
    public ConnectionTestResult? DestinationTestResult { get; set; }

    public string DisplayName => string.IsNullOrEmpty(Name) ? $"Session {Created:yyyy-MM-dd HH:mm}" : Name;
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    
    // Additional properties for detailed connection testing
    public bool IsSuccessful { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public double ResponseTime { get; set; }
}
