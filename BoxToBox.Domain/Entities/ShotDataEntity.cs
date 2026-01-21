namespace BoxToBox.Domain.Entities;

/// <summary>
/// Shot event with location and outcome
/// </summary>
public class ShotDataEntity : Base
{
    public string VideoAnalysisId { get; set; } = string.Empty;
    public int TimestampSeconds { get; set; }
    
    public string Team { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }
    public string? PlayerName { get; set; }
    
    /// <summary>
    /// Shot location (normalized 0-1 field coordinates): {"x": 0.85, "y": 0.5}
    /// </summary>
    public string Location { get; set; } = "{}";
    
    /// <summary>
    /// Distance from goal in meters
    /// </summary>
    public double DistanceToGoalMeters { get; set; }
    
    /// <summary>
    /// Angle to goal in degrees
    /// </summary>
    public double AngleToGoalDegrees { get; set; }
    
    /// <summary>
    /// Shot outcome: "Goal", "Saved", "Blocked", "Wide", "Post"
    /// </summary>
    public string Outcome { get; set; } = "Unknown";
    
    /// <summary>
    /// Expected Goals (xG) value 0-1
    /// Based on historical data of shot conversion from similar positions
    /// </summary>
    public double ExpectedGoals { get; set; }
    
    /// <summary>
    /// Body part: "Foot", "Head", "Other"
    /// </summary>
    public string? BodyPart { get; set; }
    
    /// <summary>
    /// Shot type: "Open Play", "Free Kick", "Penalty", "Corner"
    /// </summary>
    public string? ShotType { get; set; }
}
