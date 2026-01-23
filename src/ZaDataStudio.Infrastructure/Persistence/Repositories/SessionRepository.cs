using System.Text.Json;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Infrastructure.Persistence.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly string _storageDirectory;
    private const string SessionsFile = "sessions.json";

    public SessionRepository(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZaDataStudio",
            "Sessions");
        
        Directory.CreateDirectory(_storageDirectory);
    }

    private string GetSessionsPath() => Path.Combine(_storageDirectory, SessionsFile);

    public async Task<List<ComparisonSession>> GetAllSessionsAsync()
    {
        try
        {
            var filePath = GetSessionsPath();
            if (!File.Exists(filePath))
                return new List<ComparisonSession>();

            var json = await File.ReadAllTextAsync(filePath);
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

        // Keep only the last 50 sessions
        if (sessions.Count > 50)
        {
            sessions = sessions.Take(50).ToList();
        }

        var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(GetSessionsPath(), json);
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var sessions = await GetAllSessionsAsync();
        sessions.RemoveAll(s => s.Id == sessionId);
        
        var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(GetSessionsPath(), json);
    }

    public async Task<ComparisonSession?> GetSessionAsync(string sessionId)
    {
        var sessions = await GetAllSessionsAsync();
        return sessions.FirstOrDefault(s => s.Id == sessionId);
    }
}


