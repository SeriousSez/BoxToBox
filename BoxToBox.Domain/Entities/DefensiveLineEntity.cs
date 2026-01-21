namespace BoxToBox.Domain.Entities;

/// <summary>
/// Defensive line tracking data over time
/// </summary>
public class DefensiveLineEntity : Base
{
    public string VideoAnalysisId { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON time series of defensive line positions: [{"timestamp": 10, "yPosition": 0.45, "width": 0.6}, ...]
    /// yPosition: 0 = own goal, 1 = opponent goal
    /// </summary>
    public string LinePositions { get; set; } = "[]";
    
    /// <summary>
    /// Average defensive line height (normalized 0-1)
    /// </summary>
    public double AverageLineHeight { get; set; }
    
    /// <summary>
    /// Average width of defensive line (normalized 0-1)
    /// </summary>
    public double AverageLineWidth { get; set; }
    
    /// <summary>
    /// Compactness metric (lower = more compact)
    /// Average distance between defenders
    /// </summary>
    public double Compactness { get; set; }
    
    /// <summary>
    /// Percentage of time line was high (> 0.6)
    /// </summary>
    public double HighLinePercentage { get; set; }
    
    /// <summary>
    /// Percentage of time line was deep (< 0.4)
    /// </summary>
    public double DeepLinePercentage { get; set; }
    
    /// <summary>
    /// Number of times line dropped >10m in <5 seconds
    /// </summary>
    public int RapidDropbacks { get; set; }
    
    /// <summary>
    /// Number of offside traps attempted (coordinated push forward)
    /// </summary>
    public int OffsideTrapAttempts { get; set; }
}
