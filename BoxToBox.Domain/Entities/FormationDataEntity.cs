namespace BoxToBox.Domain.Entities;

/// <summary>
/// Detected formation at a specific time in the match
/// </summary>
public class FormationDataEntity : Base
{
    public Guid VideoAnalysisId { get; set; }
    public string Team { get; set; } = string.Empty;
    
    public int TimestampSeconds { get; set; }
    
    /// <summary>
    /// Formation string: "4-4-2", "4-3-3", "3-5-2", etc.
    /// </summary>
    public string Formation { get; set; } = string.Empty;
    
    public double Confidence { get; set; }
    
    /// <summary>
    /// JSON array of player positions with roles: [{"jersey": 10, "x": 0.5, "y": 0.3, "role": "CM"}, ...]
    /// Roles: GK, LB, CB, RB, LM, CM, RM, LW, ST, RW
    /// </summary>
    public string PlayerPositions { get; set; } = "[]";
    
    /// <summary>
    /// Team shape metrics
    /// </summary>
    public double? TeamWidth { get; set; }
    public double? TeamDepth { get; set; }
    public double? Compactness { get; set; }
}
