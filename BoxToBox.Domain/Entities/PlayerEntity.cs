namespace BoxToBox.Domain.Entities;

public class PlayerEntity : Base
{
    public required string Name { get; set; }
    public int? JerseyNumber { get; set; }
    public string? Position { get; set; } // GK, DF, MF, ST, etc.
    public string? Team { get; set; }
    public int? Height { get; set; } // in cm
    public int? Weight { get; set; } // in kg

    // Navigation properties
    public ICollection<PlayerStatEntity> PlayerStats { get; set; } = new List<PlayerStatEntity>();
    public ICollection<MatchEntity> Matches { get; set; } = new List<MatchEntity>();
}
