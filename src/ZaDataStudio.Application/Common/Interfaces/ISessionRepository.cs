using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Common.Interfaces;

public interface ISessionRepository
{
    Task<List<ComparisonSession>> GetAllSessionsAsync();
    Task<ComparisonSession?> GetSessionAsync(string id);
    Task SaveSessionAsync(ComparisonSession session);
    Task DeleteSessionAsync(string id);
}

