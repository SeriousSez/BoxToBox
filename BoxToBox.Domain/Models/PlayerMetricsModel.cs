namespace BoxToBox.Domain.Models;

public class PlayerMetricsModel
{
    public string? JerseyNumber { get; set; }
    public string? PlayerName { get; set; }
    public string Team { get; set; } = string.Empty;
    public double DistanceCoveredMeters { get; set; }
    public double MaxSpeedKmh { get; set; }
    public double AverageSpeedKmh { get; set; }
    public int SprintCount { get; set; }
    public double MinutesPlayed { get; set; }
}
