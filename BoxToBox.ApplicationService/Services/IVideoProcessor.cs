using BoxToBox.ApplicationService.Dtos;
using BoxToBox.Domain.Entities;

namespace BoxToBox.ApplicationService.Services;

public interface IVideoProcessor
{
    Task<VideoAnalysisResult> AnalyzeVideoAsync(
        string videoPath, 
        Guid analysisId, 
        Func<int, Task>? onProgressUpdated = null,
        TeamRosterRequest? homeTeamRoster = null,
        TeamRosterRequest? awayTeamRoster = null,
        List<GoalInfo>? goals = null,
        string cameraAngle = "Overhead");
}

public class VideoAnalysisResult
{
    public int? Duration { get; set; }
    public float? FramesPerSecond { get; set; }
    public int TotalPasses { get; set; }
    public float PassCompletionRate { get; set; }
    public int TotalShots { get; set; }
    public int ShotsOnTarget { get; set; }
    public int TotalTackles { get; set; }
    public int TacklesWon { get; set; }
    public float TotalDistanceCovered { get; set; }
    public float AverageSpeed { get; set; }
    public ICollection<PlayerStatEntity> PlayerStats { get; set; } = new List<PlayerStatEntity>();
    public ICollection<EventEntity> Events { get; set; } = new List<EventEntity>();
}
