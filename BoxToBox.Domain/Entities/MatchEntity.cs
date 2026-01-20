namespace BoxToBox.Domain.Entities;

public class MatchEntity : Base
{
    public required string Title { get; set; }
    public required string HomeTeam { get; set; }
    public required string AwayTeam { get; set; }
    public DateTime MatchDate { get; set; }
    public int? Duration { get; set; } // Duration in seconds
    public string? Location { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public ICollection<VideoAnalysisEntity> VideoAnalyses { get; set; } = new List<VideoAnalysisEntity>();
    public ICollection<PlayerEntity> Players { get; set; } = new List<PlayerEntity>();
}
