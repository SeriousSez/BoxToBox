using BoxToBox.Domain.Services;
using Microsoft.AspNetCore.SignalR;

namespace BoxToBox.API.Services;

public class VideoAnalysisSignalRNotificationService : IVideoAnalysisNotificationService
{
    private readonly IHubContext<Hubs.VideoAnalysisHub> _hubContext;

    public VideoAnalysisSignalRNotificationService(IHubContext<Hubs.VideoAnalysisHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendAnalysisStatusUpdateAsync(Guid analysisId, string status, float? progress, string message)
    {
        Console.WriteLine($"[SignalR] Broadcasting to group 'analysis-{analysisId}': status={status}, progress={progress}, message={message}");
        await _hubContext.Clients
            .Group($"analysis-{analysisId}")
            .SendAsync("AnalysisStatusUpdated", new
            {
                analysisId = analysisId,
                status = status,
                progress = progress,
                message = message
            });
        Console.WriteLine($"[SignalR] Broadcast complete");
    }
}
