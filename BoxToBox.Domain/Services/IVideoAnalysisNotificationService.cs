namespace BoxToBox.Domain.Services;

/// <summary>
/// Service for notifying clients about video analysis status updates via SignalR
/// </summary>
public interface IVideoAnalysisNotificationService
{
    /// <summary>
    /// Send analysis status update to subscribed clients
    /// </summary>
    Task SendAnalysisStatusUpdateAsync(Guid analysisId, string status, float? progress, string message);
}
