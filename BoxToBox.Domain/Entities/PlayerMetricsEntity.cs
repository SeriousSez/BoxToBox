namespace BoxToBox.Domain.Entities;

/// <summary>
/// Extended player metrics including distance and speed
/// </summary>
public class PlayerMetricsEntity : Base
{
    public Guid VideoAnalysisId { get; set; }
    public int? JerseyNumber { get; set; }
    public string? PlayerName { get; set; }
    public string Team { get; set; } = string.Empty;
    
    /// <summary>
    /// Total distance covered in meters
    /// </summary>
    public double DistanceCoveredMeters { get; set; }
    
    /// <summary>
    /// Maximum speed achieved in km/h
    /// </summary>
    public double MaxSpeedKmh { get; set; }
    
    /// <summary>
    /// Average speed in km/h (excluding stationary time)
    /// </summary>
    public double AverageSpeedKmh { get; set; }
    
    /// <summary>
    /// Distance covered at different speed thresholds
    /// </summary>
    public double WalkingDistanceMeters { get; set; }    // 0-7 km/h
    public double JoggingDistanceMeters { get; set; }    // 7-15 km/h
    public double RunningDistanceMeters { get; set; }    // 15-20 km/h
    public double SprintingDistanceMeters { get; set; }  // >20 km/h
    
    /// <summary>
    /// Number of sprint efforts (>20 km/h for >1 second)
    /// </summary>
    public int SprintCount { get; set; }
    
    /// <summary>
    /// Minutes played (approximate based on detection)
    /// </summary>
    public double MinutesPlayed { get; set; }
}
