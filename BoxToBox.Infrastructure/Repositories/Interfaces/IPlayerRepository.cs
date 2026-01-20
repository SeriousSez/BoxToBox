using BoxToBox.Domain.Entities;

namespace BoxToBox.Infrastructure.Repositories.Interfaces;

public interface IPlayerRepository
{
    Task<PlayerEntity?> GetByIdAsync(Guid id);
    Task<IEnumerable<PlayerEntity>> GetAllAsync();
    Task AddAsync(PlayerEntity entity);
    Task UpdateAsync(PlayerEntity entity);
    Task DeleteAsync(Guid id);
}
