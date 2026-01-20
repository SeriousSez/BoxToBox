using BoxToBox.Domain.Entities;

namespace BoxToBox.Infrastructure.Repositories.Interfaces;

public interface IMatchRepository
{
    Task<MatchEntity?> GetByIdAsync(Guid id);
    Task<IEnumerable<MatchEntity>> GetAllAsync();
    Task AddAsync(MatchEntity entity);
    Task UpdateAsync(MatchEntity entity);
    Task DeleteAsync(Guid id);
}
