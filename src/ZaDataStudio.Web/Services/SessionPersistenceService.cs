using Microsoft.JSInterop;
using System.Text.Json;

namespace ZaDataStudio.Web.Services;

public class SessionPersistenceService
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "schemaComparisonSessions";

    public SessionPersistenceService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<List<ComparisonSession>> GetAllSessionsAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            
            if (string.IsNullOrEmpty(json))
                return new List<ComparisonSession>();

            return JsonSerializer.Deserialize<List<ComparisonSession>>(json) ?? new List<ComparisonSession>();
        }
        catch
        {
            return new List<ComparisonSession>();
        }
    }

    public async Task SaveSessionAsync(ComparisonSession session)
    {
        var sessions = await GetAllSessionsAsync();
        
        var existing = sessions.FirstOrDefault(s => s.Id == session.Id);
        if (existing != null)
        {
            sessions.Remove(existing);
        }

        session.LastModified = DateTime.UtcNow;
        sessions.Insert(0, session);

        // Keep only the last 20 sessions
        if (sessions.Count > 20)
        {
            sessions = sessions.Take(20).ToList();
        }

        var json = JsonSerializer.Serialize(sessions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var sessions = await GetAllSessionsAsync();
        sessions.RemoveAll(s => s.Id == sessionId);
        
        var json = JsonSerializer.Serialize(sessions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task<ComparisonSession?> GetSessionAsync(string sessionId)
    {
        var sessions = await GetAllSessionsAsync();
        return sessions.FirstOrDefault(s => s.Id == sessionId);
    }
}

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
