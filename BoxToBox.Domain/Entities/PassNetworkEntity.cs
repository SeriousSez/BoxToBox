namespace BoxToBox.Domain.Entities;

/// <summary>
/// Represents a passing connection between two players
/// </summary>
public class PassNetworkEntity : Base
{
    public Guid VideoAnalysisId { get; set; }
    public string Team { get; set; } = string.Empty;
    
    public int? FromJerseyNumber { get; set; }
    public string? FromPlayerName { get; set; }
    
    public int? ToJerseyNumber { get; set; }
    public string? ToPlayerName { get; set; }
    
    public int PassCount { get; set; }
    public int SuccessfulPasses { get; set; }
    public double PassAccuracy => PassCount > 0 ? (double)SuccessfulPasses / PassCount * 100 : 0;
    
    /// <summary>
    /// Average position where passes originated: {"x": 0.5, "y": 0.3}
    /// </summary>
    public string? AveragePassLocation { get; set; }
}
