namespace BoxToBox.Domain.Entities;

/// <summary>
/// Set piece event (corner, free kick, throw-in)
/// </summary>
public class SetPieceEventEntity : Base
{
    public string VideoAnalysisId { get; set; } = string.Empty;
    public int TimestampSeconds { get; set; }
    
    /// <summary>
    /// Type: "Corner", "FreeKick", "ThrowIn", "GoalKick", "Penalty"
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    public string Team { get; set; } = string.Empty;
    
    /// <summary>
    /// Ball location when set piece taken: {"x": 0.95, "y": 0.05} for corner
    /// </summary>
    public string Location { get; set; } = "{}";
    
    /// <summary>
    /// Player taking the set piece
    /// </summary>
    public int? TakerJerseyNumber { get; set; }
    public string? TakerName { get; set; }
    
    /// <summary>
    /// Outcome: "Goal", "Shot", "Pass", "Cleared", "Caught"
    /// </summary>
    public string? Outcome { get; set; }
    
    /// <summary>
    /// JSON array of player positions during set piece: [{"jersey": 10, "x": 0.8, "y": 0.5, "team": "Home"}, ...]
    /// </summary>
    public string? PlayerFormation { get; set; }
    
    /// <summary>
    /// Whether this resulted in a goal within 10 seconds
    /// </summary>
    public bool ResultedInGoal { get; set; }
}
