namespace BoxToBox.Domain.Entities;

public class VideoAnalysisEntity : Base
{
    public required string Title { get; set; }
    public required string VideoFileName { get; set; }
    public required string VideoPath { get; set; }
    public long FileSizeBytes { get; set; }
    public int? Duration { get; set; } // Duration in seconds
    public float? FramesPerSecond { get; set; }

    // Analysis status
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public DateTime? AnalysisStartedAt { get; set; }
    public DateTime? AnalysisCompletedAt { get; set; }
    public string? AnalysisErrorMessage { get; set; }
    public float? ProcessingProgress { get; set; } // 0-100%

    // Summary statistics
    public int? TotalPasses { get; set; }
    public float? PassCompletionRate { get; set; }
    public int? TotalShots { get; set; }
    public int? ShotsOnTarget { get; set; }
    public int? TotalTackles { get; set; }
    public int? TacklesWon { get; set; }
    public float? TotalDistanceCovered { get; set; } // in meters
    public float? AverageSpeed { get; set; } // in km/h

    // Team colors
    public string? HomeTeamColorPrimary { get; set; }
    public string? HomeTeamColorSecondary { get; set; }
    public string? AwayTeamColorPrimary { get; set; }
    public string? AwayTeamColorSecondary { get; set; }

    // Camera settings
    public string CameraAngle { get; set; } = "Overhead";

    // Navigation properties
    public MatchEntity? Match { get; set; }
    public ICollection<PlayerStatEntity> PlayerStats { get; set; } = new List<PlayerStatEntity>();
    public ICollection<EventEntity> Events { get; set; } = new List<EventEntity>();
}