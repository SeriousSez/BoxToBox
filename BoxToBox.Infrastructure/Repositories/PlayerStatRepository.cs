using BoxToBox.Domain.Entities;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BoxToBox.Infrastructure.Repositories;

public class PlayerStatRepository : IPlayerStatRepository
{
    private readonly BoxToBoxDbContext _context;

    public PlayerStatRepository(BoxToBoxDbContext context)
    {
        _context = context;
    }

    public async Task<PlayerStatEntity?> GetByIdAsync(Guid id)
    {
        return await _context.PlayerStats
            .Include(ps => ps.Player)
            .Include(ps => ps.VideoAnalysis)
            .FirstOrDefaultAsync(ps => ps.Id == id);
    }

    public async Task<IEnumerable<PlayerStatEntity>> GetByAnalysisIdAsync(Guid analysisId)
    {
        // Primary query by foreign key
        var stats = await _context.PlayerStats
            .Where(ps => ps.VideoAnalysisId == analysisId)
            .AsNoTracking()
            .ToListAsync();

        // Fallback: if none found, load via VideoAnalysis navigation which includes PlayerStats
        if (stats.Count == 0)
        {
            var analysis = await _context.VideoAnalyses
                .Include(v => v.PlayerStats)
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == analysisId);

            if (analysis?.PlayerStats != null && analysis.PlayerStats.Count > 0)
            {
                return analysis.PlayerStats.ToList();
            }
        }

        return stats;
    }

    public async Task<IEnumerable<PlayerStatEntity>> GetByPlayerIdAsync(Guid playerId)
    {
        return await _context.PlayerStats
            .Where(ps => ps.PlayerId == playerId)
            .Include(ps => ps.VideoAnalysis)
            .ToListAsync();
    }

    public async Task AddAsync(PlayerStatEntity entity)
    {
        await _context.PlayerStats.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PlayerStatEntity entity)
    {
        _context.PlayerStats.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.PlayerStats.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteByAnalysisIdAsync(Guid analysisId)
    {
        var stats = await GetByAnalysisIdAsync(analysisId);
        foreach (var stat in stats)
        {
            _context.PlayerStats.Remove(stat);
        }
        await _context.SaveChangesAsync();
        
        // Clear change tracker to prevent tracking conflicts when adding new stats
        _context.ChangeTracker.Clear();
    }
}
