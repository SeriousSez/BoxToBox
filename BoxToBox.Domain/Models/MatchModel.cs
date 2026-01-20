namespace BoxToBox.Domain.Models;

public class MatchModel : Base
{
    public required string HomeTeam { get; set; }
    public required string AwayTeam { get; set; }
    public DateTime MatchDate { get; set; }
    public int? Duration { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
}
