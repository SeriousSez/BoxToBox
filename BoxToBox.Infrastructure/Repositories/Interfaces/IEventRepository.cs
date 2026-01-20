using BoxToBox.Domain.Entities;

namespace BoxToBox.Infrastructure.Repositories.Interfaces;

public interface IEventRepository
{
    Task<EventEntity?> GetByIdAsync(Guid id);
    Task<IEnumerable<EventEntity>> GetByAnalysisIdAsync(Guid analysisId);
    Task AddAsync(EventEntity entity);
    Task UpdateAsync(EventEntity entity);
    Task DeleteAsync(Guid id);
    Task DeleteByAnalysisIdAsync(Guid analysisId);
}
