namespace BoxToBox.Domain.Entities;

/// <summary>
/// Detected offside situation
/// </summary>
public class OffsideEventEntity : Base
{
    public string VideoAnalysisId { get; set; } = string.Empty;
    public int TimestampSeconds { get; set; }
    
    public string AttackingTeam { get; set; } = string.Empty;
    public int? AttackerJerseyNumber { get; set; }
    public string? AttackerName { get; set; }
    
    /// <summary>
    /// Position of the offside player: {"x": 0.8, "y": 0.4}
    /// </summary>
    public string OffsidePlayerPosition { get; set; } = "{}";
    
    /// <summary>
    /// Position of second-to-last defender: {"x": 0.75, "y": 0.5}
    /// </summary>
    public string DefensiveLinePosition { get; set; } = "{}";
    
    /// <summary>
    /// Distance ahead of defensive line in meters
    /// </summary>
    public double OffsideDistanceMeters { get; set; }
    
    /// <summary>
    /// Confidence in detection (0-1)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Whether this offside was actually called (if known)
    /// </summary>
    public bool? WasCalled { get; set; }
}
