namespace BoxToBox.Domain.Entities;

/// <summary>
/// Pressing intensity metrics for a team
/// </summary>
public class PressingDataEntity : Base
{
    public string VideoAnalysisId { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    
    /// <summary>
    /// Passes Per Defensive Action - lower = more intense press
    /// </summary>
    public double PPDA { get; set; }
    
    /// <summary>
    /// Percentage of time team applied high press (opposition half)
    /// </summary>
    public double HighPressPercentage { get; set; }
    
    /// <summary>
    /// Percentage of time team applied mid press
    /// </summary>
    public double MidPressPercentage { get; set; }
    
    /// <summary>
    /// Percentage of time team had low block
    /// </summary>
    public double LowBlockPercentage { get; set; }
    
    /// <summary>
    /// Number of successful press actions (won ball within 3 seconds)
    /// </summary>
    public int SuccessfulPresses { get; set; }
    
    /// <summary>
    /// Total press attempts
    /// </summary>
    public int TotalPressAttempts { get; set; }
    
    /// <summary>
    /// Average number of players within 10m of ball carrier
    /// </summary>
    public double AveragePlayersNearBall { get; set; }
    
    /// <summary>
    /// JSON array of press zones with success rate: [{"zone": "attacking_third", "attempts": 20, "successful": 12}, ...]
    /// </summary>
    public string PressZones { get; set; } = "[]";
}
