using BoxToBox.Domain.Entities;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BoxToBox.Infrastructure.Repositories;

public class EventRepository : IEventRepository
{
    private readonly BoxToBoxDbContext _context;

    public EventRepository(BoxToBoxDbContext context)
    {
        _context = context;
    }

    public async Task<EventEntity?> GetByIdAsync(Guid id)
    {
        return await _context.Events
            .Include(e => e.VideoAnalysis)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<EventEntity>> GetByAnalysisIdAsync(Guid analysisId)
    {
        return await _context.Events
            .Where(e => e.VideoAnalysisId == analysisId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }

    public async Task AddAsync(EventEntity entity)
    {
        await _context.Events.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EventEntity entity)
    {
        _context.Events.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Events.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteByAnalysisIdAsync(Guid analysisId)
    {
        var events = await GetByAnalysisIdAsync(analysisId);
        foreach (var evt in events)
        {
            _context.Events.Remove(evt);
        }
        await _context.SaveChangesAsync();
        
        // Clear change tracker to prevent tracking conflicts when adding new events
        _context.ChangeTracker.Clear();
    }
}
