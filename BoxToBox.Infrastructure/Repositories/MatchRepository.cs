using BoxToBox.Domain.Entities;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BoxToBox.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly BoxToBoxDbContext _context;

    public MatchRepository(BoxToBoxDbContext context)
    {
        _context = context;
    }

    public async Task<MatchEntity?> GetByIdAsync(Guid id)
    {
        return await _context.Matches
            .Include(m => m.Players)
            .Include(m => m.VideoAnalyses)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<MatchEntity>> GetAllAsync()
    {
        return await _context.Matches
            .Include(m => m.Players)
            .Include(m => m.VideoAnalyses)
            .ToListAsync();
    }

    public async Task AddAsync(MatchEntity entity)
    {
        await _context.Matches.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(MatchEntity entity)
    {
        _context.Matches.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Matches.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
