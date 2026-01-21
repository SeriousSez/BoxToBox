namespace BoxToBox.Domain.Models;

public class PossessionDataModel
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int HomePossessionSeconds { get; set; }
    public int AwayPossessionSeconds { get; set; }
    public double? HomePossessionPercentage { get; set; }
    public double? AwayPossessionPercentage { get; set; }
}
