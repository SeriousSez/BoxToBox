using Microsoft.AspNetCore.SignalR;

namespace BoxToBox.API.Hubs;

public class VideoAnalysisHub : Hub
{
    public async Task SubscribeToAnalysis(string analysisId)
    {
        Console.WriteLine($"[SignalR Hub] Connection {Context.ConnectionId} subscribing to group 'analysis-{analysisId}'");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"analysis-{analysisId}");
        Console.WriteLine($"[SignalR Hub] Subscription complete");
    }

    public async Task UnsubscribeFromAnalysis(string analysisId)
    {
        Console.WriteLine($"[SignalR Hub] Connection {Context.ConnectionId} unsubscribing from group 'analysis-{analysisId}'");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"analysis-{analysisId}");
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"[SignalR Hub] New connection: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"[SignalR Hub] Disconnected: {Context.ConnectionId}, Exception: {exception?.Message}");
        await base.OnDisconnectedAsync(exception);
    }
}
