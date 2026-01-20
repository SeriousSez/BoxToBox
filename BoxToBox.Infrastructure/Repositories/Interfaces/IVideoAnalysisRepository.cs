using BoxToBox.Domain;
using BoxToBox.Domain.Entities;

namespace BoxToBox.Infrastructure.Repositories.Interfaces;

public interface IVideoAnalysisRepository
{
    Task<IEnumerable<VideoAnalysisEntity>> GetAllAsync();
    Task<VideoAnalysisEntity?> GetByIdAsync(Guid id);
    Task AddAsync(VideoAnalysisEntity entity);
    Task UpdateAsync(VideoAnalysisEntity entity);
    Task DeleteAsync(Guid id);
}
