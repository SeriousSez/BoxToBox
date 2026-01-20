using BoxToBox.ApplicationService.Dtos;
using BoxToBox.Domain;
using BoxToBox.Domain.Models;

namespace BoxToBox.ApplicationService.Services;

public interface IVideoAnalysisService
{
    /// <summary>
    /// Upload a video file - accepts title or creates new match with given name
    /// </summary>
    Task<VideoAnalysisModel> UploadVideoAsync(string title, Stream videoStream, string fileName);

    /// <summary>
    /// Upload a video file and create a VideoAnalysis record for processing
    /// </summary>
    Task<VideoAnalysisModel> UploadVideoAsync(Guid matchId, string title, Stream videoStream, string fileName);

    /// <summary>
    /// Get video analysis by ID with all statistics
    /// </summary>
    Task<VideoAnalysisModel?> GetAnalysisAsync(Guid analysisId);

    /// <summary>
    /// Get all video analyses
    /// </summary>
    Task<IEnumerable<VideoAnalysisModel>> GetAllAnalysesAsync();

    /// <summary>
    /// Get player statistics for a specific video analysis
    /// </summary>
    Task<IEnumerable<PlayerStatModel>> GetPlayerStatsAsync(Guid analysisId);

    /// <summary>
    /// Get timeline events for a specific video analysis
    /// </summary>
    Task<IEnumerable<EventModel>> GetEventsAsync(Guid analysisId);

    /// <summary>
    /// Start processing a video for analysis (runs in background)
    /// </summary>
    Task<bool> StartAnalysisAsync(Guid analysisId, TeamRosterRequest? homeTeam = null, TeamRosterRequest? awayTeam = null, List<GoalInfo>? goals = null, string cameraAngle = "Overhead");

    /// <summary>
    /// Generate a short clip around a specific event
    /// </summary>
    Task<string> GenerateEventClipAsync(Guid analysisId, Guid eventId, int preSeconds = 2, int postSeconds = 4);

    /// <summary>
    /// Get analysis progress/status
    /// </summary>
    Task<(AnalysisStatus status, float? progress, string? error)> GetAnalysisStatusAsync(Guid analysisId);

    /// <summary>
    /// Cancel an ongoing analysis
    /// </summary>
    Task<bool> CancelAnalysisAsync(Guid analysisId);

    /// <summary>
    /// Delete an analysis and its video file
    /// </summary>
    Task<bool> DeleteAnalysisAsync(Guid analysisId);
}
