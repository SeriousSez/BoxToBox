namespace BoxToBox.Domain.Models;

public class VideoAnalysisModel : Base
{
    public required string Title { get; set; }
    public required string VideoFileName { get; set; }
    public required string VideoPath { get; set; }
    public long FileSizeBytes { get; set; }
    public int? Duration { get; set; }
    public float? FramesPerSecond { get; set; }
    public AnalysisStatus Status { get; set; }
    public DateTime? AnalysisStartedAt { get; set; }
    public DateTime? AnalysisCompletedAt { get; set; }
    public string? AnalysisErrorMessage { get; set; }
    public float? ProcessingProgress { get; set; }
    public int? TotalPasses { get; set; }
    public float? PassCompletionRate { get; set; }
    public int? TotalShots { get; set; }
    public int? ShotsOnTarget { get; set; }
    public int? TotalTackles { get; set; }
    public int? TacklesWon { get; set; }
    public float? TotalDistanceCovered { get; set; }
    public float? AverageSpeed { get; set; }
    public string? HomeTeamColorPrimary { get; set; }
    public string? HomeTeamColorSecondary { get; set; }
    public string? AwayTeamColorPrimary { get; set; }
    public string? AwayTeamColorSecondary { get; set; }
    public string CameraAngle { get; set; } = "Overhead";
}