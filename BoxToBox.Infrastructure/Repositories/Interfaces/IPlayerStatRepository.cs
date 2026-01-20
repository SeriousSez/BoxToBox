using BoxToBox.Domain.Entities;

namespace BoxToBox.Infrastructure.Repositories.Interfaces;

public interface IPlayerStatRepository
{
    Task<PlayerStatEntity?> GetByIdAsync(Guid id);
    Task<IEnumerable<PlayerStatEntity>> GetByAnalysisIdAsync(Guid analysisId);
    Task<IEnumerable<PlayerStatEntity>> GetByPlayerIdAsync(Guid playerId);
    Task AddAsync(PlayerStatEntity entity);
    Task UpdateAsync(PlayerStatEntity entity);
    Task DeleteAsync(Guid id);
    Task DeleteByAnalysisIdAsync(Guid analysisId);
}
