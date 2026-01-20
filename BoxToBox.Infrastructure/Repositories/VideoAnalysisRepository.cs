using BoxToBox.Domain.Entities;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using BoxToBox.Infrastructure;

namespace BoxToBox.Infrastructure.Repositories;

public class VideoAnalysisRepository : IVideoAnalysisRepository
{
	private readonly BoxToBoxDbContext _context;

	public VideoAnalysisRepository(BoxToBoxDbContext context)
	{
		_context = context;
	}

	public async Task<IEnumerable<VideoAnalysisEntity>> GetAllAsync()
	{
		return await _context.VideoAnalyses
			.AsNoTracking()
			.OrderByDescending(v => v.Created)
			.ToListAsync();
	}

	public async Task<VideoAnalysisEntity?> GetByIdAsync(Guid id)
	{
		return await _context.VideoAnalyses
			.Include(v => v.PlayerStats)
			.Include(v => v.Events)
			.FirstOrDefaultAsync(v => v.Id == id);
	}

	public async Task AddAsync(VideoAnalysisEntity entity)
	{
		await _context.VideoAnalyses.AddAsync(entity);
		await _context.SaveChangesAsync();
	}

	public async Task UpdateAsync(VideoAnalysisEntity entity)
	{
		_context.VideoAnalyses.Update(entity);
		await _context.SaveChangesAsync();
	}

	public async Task DeleteAsync(Guid id)
	{
		var entity = await _context.VideoAnalyses.FindAsync(id);
		if (entity != null)
		{
			_context.VideoAnalyses.Remove(entity);
			await _context.SaveChangesAsync();
		}
	}
}
