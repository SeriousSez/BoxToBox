namespace BoxToBox.Domain.Entities;

/// <summary>
/// Stores position data for generating player heat maps
/// </summary>
public class HeatMapDataEntity : Base
{
    public Guid VideoAnalysisId { get; set; }
    public int? JerseyNumber { get; set; }
    public string? PlayerName { get; set; }
    public string Team { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON array of position points: [{"x": 0.5, "y": 0.3, "timestamp": 120}, ...]
    /// x,y are normalized (0-1) field coordinates
    /// </summary>
    public string PositionData { get; set; } = "[]";
    
    /// <summary>
    /// Pre-calculated density grid for visualization (optional)
    /// JSON 2D array: [[0.1, 0.2, ...], ...]
    /// </summary>
    public string? DensityGrid { get; set; }
    
    public int GridWidth { get; set; } = 50;
    public int GridHeight { get; set; } = 30;
}
