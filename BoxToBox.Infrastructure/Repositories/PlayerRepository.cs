using BoxToBox.Domain.Entities;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BoxToBox.Infrastructure.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly BoxToBoxDbContext _context;

    public PlayerRepository(BoxToBoxDbContext context)
    {
        _context = context;
    }

    public async Task<PlayerEntity?> GetByIdAsync(Guid id)
    {
        return await _context.Players
            .Include(p => p.PlayerStats)
            .Include(p => p.Matches)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<PlayerEntity>> GetAllAsync()
    {
        return await _context.Players
            .Include(p => p.PlayerStats)
            .Include(p => p.Matches)
            .ToListAsync();
    }

    public async Task AddAsync(PlayerEntity entity)
    {
        await _context.Players.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PlayerEntity entity)
    {
        _context.Players.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Players.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
