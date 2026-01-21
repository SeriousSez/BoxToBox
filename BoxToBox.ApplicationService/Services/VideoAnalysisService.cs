using BoxToBox.ApplicationService.Dtos;
using BoxToBox.Domain;
using BoxToBox.Domain.Entities;
using BoxToBox.Domain.Models;
using BoxToBox.Domain.Services;
using BoxToBox.Infrastructure.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;

namespace BoxToBox.ApplicationService.Services;

public class VideoAnalysisService : IVideoAnalysisService
{
    private readonly IVideoAnalysisRepository _videoAnalysisRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IPlayerStatRepository _playerStatRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IVideoProcessor _videoProcessor;
    private readonly IVideoAnalysisNotificationService _notificationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _uploadDirectory;

    public VideoAnalysisService(
        IVideoAnalysisRepository videoAnalysisRepository,
        IMatchRepository matchRepository,
        IPlayerRepository playerRepository,
        IPlayerStatRepository playerStatRepository,
        IEventRepository eventRepository,
        IVideoProcessor videoProcessor,
        IVideoAnalysisNotificationService notificationService,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _videoAnalysisRepository = videoAnalysisRepository;
        _matchRepository = matchRepository;
        _playerRepository = playerRepository;
        _playerStatRepository = playerStatRepository;
        _eventRepository = eventRepository;
        _videoProcessor = videoProcessor;
        _notificationService = notificationService;
        _scopeFactory = scopeFactory;
        _uploadDirectory = configuration.GetValue<string>("VideoUploadPath") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads", "Videos");
    }

    public async Task<VideoAnalysisModel> UploadVideoAsync(string title, Stream videoStream, string fileName)
    {
            // Create a new match with the identifier as the name
        var newMatch = new MatchEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            HomeTeam = title,
            AwayTeam = "TBD",
            MatchDate = DateTime.UtcNow,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
        await _matchRepository.AddAsync(newMatch);

        return await UploadVideoAsync(newMatch.Id, title, videoStream, fileName);
    }

    public async Task<VideoAnalysisModel> UploadVideoAsync(Guid matchId, string title, Stream videoStream, string fileName)
    {
        // Verify match exists
        var match = await _matchRepository.GetByIdAsync(matchId);
        if (match == null)
            throw new InvalidOperationException($"Match with ID {matchId} not found");

        // Create upload directory if it doesn't exist
        Directory.CreateDirectory(_uploadDirectory);

        // Generate unique filename
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(_uploadDirectory, uniqueFileName);

        // Save video file
        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await videoStream.CopyToAsync(fileStream);
        }

        var fileInfo = new FileInfo(filePath);

        // Create VideoAnalysis record
        var analysis = new VideoAnalysisEntity
        {
            Id = matchId,
            Title = title,
            VideoFileName = fileName,
            VideoPath = filePath,
            FileSizeBytes = fileInfo.Length,
            Status = AnalysisStatus.Pending,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };

        await _videoAnalysisRepository.AddAsync(analysis);

        return MapVideoAnalysisToModel(analysis);
    }

    public async Task<VideoAnalysisModel?> GetAnalysisAsync(Guid analysisId)
    {
        var analysis = await _videoAnalysisRepository.GetByIdAsync(analysisId);
        return analysis != null ? MapVideoAnalysisToModel(analysis) : null;
    }

    public async Task<IEnumerable<VideoAnalysisModel>> GetAllAnalysesAsync()
    {
        var analyses = await _videoAnalysisRepository.GetAllAsync();
        return analyses.Select(MapVideoAnalysisToModel);
    }

    public async Task<IEnumerable<PlayerStatModel>> GetPlayerStatsAsync(Guid analysisId)
    {
        var stats = await _playerStatRepository.GetByAnalysisIdAsync(analysisId);
        return stats.Select(MapPlayerStatToModel);
    }

    public async Task<IEnumerable<EventModel>> GetEventsAsync(Guid analysisId)
    {
        var events = await _eventRepository.GetByAnalysisIdAsync(analysisId);
        return events.Select(MapEventToModel);
    }

    public async Task<string> GenerateEventClipAsync(Guid analysisId, Guid eventId, int preSeconds = 2, int postSeconds = 4)
    {
        if (preSeconds < 0) preSeconds = 0;
        if (postSeconds < 0) postSeconds = 0;

        var analysis = await _videoAnalysisRepository.GetByIdAsync(analysisId)
            ?? throw new InvalidOperationException($"Analysis with ID {analysisId} not found");

        var videoPath = analysis.VideoPath;
        if (!File.Exists(videoPath))
            throw new InvalidOperationException($"Video file not found at {videoPath}");

        var evt = await _eventRepository.GetByIdAsync(eventId)
            ?? throw new InvalidOperationException($"Event with ID {eventId} not found");

        if (evt.VideoAnalysisId != analysisId)
            throw new InvalidOperationException("Event does not belong to this analysis");

        if (!await IsFfmpegAvailableAsync())
            throw new InvalidOperationException("FFmpeg is not available on the host. Please install ffmpeg and ensure it is on PATH.");

        var clipDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "clips", analysisId.ToString());
        Directory.CreateDirectory(clipDir);

        var clipPath = Path.Combine(clipDir, $"{eventId}.mp4");
        var clipUrl = $"/clips/{analysisId}/{eventId}.mp4";

        Console.WriteLine($"[GenerateEventClipAsync] Generating clip for event {eventId} in analysis {analysisId}");
        Console.WriteLine($"[GenerateEventClipAsync] Clip URL: {clipUrl}");

        var start = Math.Max(0, evt.Timestamp - preSeconds);
        var duration = Math.Max(1, preSeconds + postSeconds);
        if (analysis.Duration.HasValue && analysis.Duration.Value > 0)
        {
            var maxDuration = Math.Max(1, analysis.Duration.Value - start);
            duration = Math.Min(duration, maxDuration);
        }

        await TrimClipWithFfmpegAsync(videoPath, clipPath, start, duration);

        evt.ClipUrl = clipUrl;
        await _eventRepository.UpdateAsync(evt);

        Console.WriteLine($"[GenerateEventClipAsync] Clip generation complete. Returning URL: {clipUrl}");

        return clipUrl;
    }

    public async Task<bool> StartAnalysisAsync(Guid analysisId, TeamRosterRequest? homeTeam = null, TeamRosterRequest? awayTeam = null, List<GoalInfo>? goals = null, string cameraAngle = "Overhead")
    {
        var analysis = await _videoAnalysisRepository.GetByIdAsync(analysisId);
        if (analysis == null)
            throw new InvalidOperationException($"Analysis with ID {analysisId} not found");

        // Allow starting from Pending or Completed (regenerate)
        if (analysis.Status != AnalysisStatus.Pending && analysis.Status != AnalysisStatus.Completed)
            throw new InvalidOperationException($"Analysis cannot be started from status: {analysis.Status}");

        // Update status to Processing, reset progress, and save camera angle
        analysis.Status = AnalysisStatus.Processing;
        analysis.ProcessingProgress = 0;
        analysis.AnalysisStartedAt = DateTime.UtcNow;
        analysis.AnalysisCompletedAt = null; // Clear completion time for regeneration
        analysis.CameraAngle = cameraAngle; // Save camera angle immediately
        analysis.Modified = DateTime.UtcNow;
        await _videoAnalysisRepository.UpdateAsync(analysis);

        // Start background processing with a new scope (fire and forget)
        _ = Task.Run(() => ProcessVideoInScopeAsync(analysisId, homeTeam, awayTeam, goals, cameraAngle));

        return true;
    }

    private async Task ProcessVideoInScopeAsync(Guid analysisId, TeamRosterRequest? homeTeam = null, TeamRosterRequest? awayTeam = null, List<GoalInfo>? goals = null, string cameraAngle = "Overhead")
    {
        // Create a new scope for the background task so it has its own DbContext
        using var scope = _scopeFactory.CreateScope();
        var videoAnalysisRepository = scope.ServiceProvider.GetRequiredService<IVideoAnalysisRepository>();
        var matchRepository = scope.ServiceProvider.GetRequiredService<IMatchRepository>();
        var playerStatRepository = scope.ServiceProvider.GetRequiredService<IPlayerStatRepository>();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BoxToBox.Infrastructure.BoxToBoxDbContext>();
        
        await ProcessVideoAsync(analysisId, videoAnalysisRepository, matchRepository, playerStatRepository, eventRepository, dbContext, homeTeam, awayTeam, goals, cameraAngle);
    }

    private async Task ProcessVideoAsync(
        Guid analysisId,
        IVideoAnalysisRepository videoAnalysisRepository,
        IMatchRepository matchRepository,
        IPlayerStatRepository playerStatRepository,
        IEventRepository eventRepository,
        BoxToBox.Infrastructure.BoxToBoxDbContext context,
        TeamRosterRequest? homeTeam = null,
        TeamRosterRequest? awayTeam = null,
        List<GoalInfo>? goals = null,
        string cameraAngle = "Overhead")
    {
        Console.WriteLine($"[ProcessVideoAsync] Starting processing for analysis ID: {analysisId}");
        try
        {
            var analysis = await videoAnalysisRepository.GetByIdAsync(analysisId);
            if (analysis == null)
            {
                Console.WriteLine($"[ProcessVideoAsync] Analysis {analysisId} not found");
                return;
            }

            Console.WriteLine($"[ProcessVideoAsync] Processing video: {analysis.VideoPath}");

            // Store team colors and camera angle from roster requests
            if (homeTeam != null)
            {
                analysis.HomeTeamColorPrimary = homeTeam.JerseyColorHome;
                analysis.HomeTeamColorSecondary = homeTeam.JerseyColorAway;
            }
            if (awayTeam != null)
            {
                analysis.AwayTeamColorPrimary = awayTeam.JerseyColorHome;
                analysis.AwayTeamColorSecondary = awayTeam.JerseyColorAway;
            }
            analysis.CameraAngle = cameraAngle;
            await videoAnalysisRepository.UpdateAsync(analysis);

            // Get match details
            var match = await matchRepository.GetByIdAsync(analysis.Id);

            Console.WriteLine($"[ProcessVideoAsync] Using processor type: {_videoProcessor.GetType().Name}");
            // Process video using video processor with progress callback
            var analysisResult = await _videoProcessor.AnalyzeVideoAsync(
                analysis.VideoPath, 
                analysisId,
                async (progress) =>
                {
                    Console.WriteLine($"[ProcessVideoAsync] Progress callback START: {progress}% for analysis {analysisId}");
                    try
                    {
                        // Update progress in database
                        Console.WriteLine($"[ProcessVideoAsync] Updating DB progress to {progress}%");
                        analysis.ProcessingProgress = progress;
                        await videoAnalysisRepository.UpdateAsync(analysis);
                        Console.WriteLine($"[ProcessVideoAsync] DB update complete");

                        // Send progress update via SignalR
                        Console.WriteLine($"[ProcessVideoAsync] Sending SignalR update for {progress}%");
                        await _notificationService.SendAnalysisStatusUpdateAsync(
                            analysisId,
                            analysis.Status.ToString(),
                            progress,
                            $"Processing: {progress}% complete");
                        Console.WriteLine($"[ProcessVideoAsync] SignalR update complete for {progress}%");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ProcessVideoAsync] ERROR in progress callback: {ex.Message}");
                        throw;
                    }
                    Console.WriteLine($"[ProcessVideoAsync] Progress callback END: {progress}%");
                },
                homeTeam,
                awayTeam,
                goals,
                cameraAngle);

            // Clear old player stats and events if regenerating
            Console.WriteLine($"[ProcessVideoAsync] Deleting old player stats and events for analysis {analysisId}");
            await playerStatRepository.DeleteByAnalysisIdAsync(analysisId);
            await eventRepository.DeleteByAnalysisIdAsync(analysisId);
            Console.WriteLine($"[ProcessVideoAsync] Old data cleared");

            // Ensure no tracked entities remain before inserting new ones
            context.ChangeTracker.Clear();

            // Update analysis with results
            analysis.Duration = analysisResult.Duration;
            analysis.FramesPerSecond = analysisResult.FramesPerSecond;
            analysis.TotalPasses = analysisResult.TotalPasses;
            analysis.PassCompletionRate = analysisResult.PassCompletionRate;
            analysis.TotalShots = analysisResult.TotalShots;
            analysis.ShotsOnTarget = analysisResult.ShotsOnTarget;
            analysis.TotalTackles = analysisResult.TotalTackles;
            analysis.TacklesWon = analysisResult.TacklesWon;
            analysis.TotalDistanceCovered = analysisResult.TotalDistanceCovered;
            analysis.AverageSpeed = analysisResult.AverageSpeed;
            analysis.Status = AnalysisStatus.Completed;
            analysis.AnalysisCompletedAt = DateTime.UtcNow;
            analysis.ProcessingProgress = 100;
            analysis.Modified = DateTime.UtcNow;

            // Save player statistics
            foreach (var playerStat in analysisResult.PlayerStats)
            {
                await playerStatRepository.AddAsync(playerStat);
            }

            Console.WriteLine($"[ProcessVideoAsync] Player stats persisted: {analysisResult.PlayerStats.Count} for analysis {analysisId}");

            // Save events
            foreach (var videoEvent in analysisResult.Events)
            {
                await eventRepository.AddAsync(videoEvent);
            }

            Console.WriteLine($"[ProcessVideoAsync] Events persisted: {analysisResult.Events.Count} for analysis {analysisId}");

            // Save advanced analytics
            Console.WriteLine($"[ProcessVideoAsync] Saving advanced analytics...");
            if (analysisResult.HeatMaps?.Any() == true)
            {
                Console.WriteLine($"[ProcessVideoAsync] Persisting {analysisResult.HeatMaps.Count} heat maps");
                foreach (var heatMap in analysisResult.HeatMaps)
                {
                    await context.HeatMapData.AddAsync(heatMap);
                }
            }
            if (analysisResult.PlayerMetrics?.Any() == true)
            {
                Console.WriteLine($"[ProcessVideoAsync] Persisting {analysisResult.PlayerMetrics.Count} player metrics");
                foreach (var metric in analysisResult.PlayerMetrics)
                {
                    await context.PlayerMetrics.AddAsync(metric);
                }
            }
            if (analysisResult.PossessionData != null)
            {
                Console.WriteLine($"[ProcessVideoAsync] Persisting possession data");
                await context.PossessionData.AddAsync(analysisResult.PossessionData);
            }
            
            await context.SaveChangesAsync();
            Console.WriteLine($"[ProcessVideoAsync] Advanced analytics saved to database");

            await videoAnalysisRepository.UpdateAsync(analysis);

            Console.WriteLine($"[ProcessVideoAsync] Analysis updated to Completed status. AnalysisId: {analysisId}, Status: {analysis.Status}, Progress: {analysis.ProcessingProgress}%");

            // Notify clients via SignalR
            await _notificationService.SendAnalysisStatusUpdateAsync(
                analysisId,
                analysis.Status.ToString(),
                analysis.ProcessingProgress,
                "Analysis completed successfully");

            Console.WriteLine($"[ProcessVideoAsync] SignalR notification sent for completion");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessVideoAsync] ERROR occurred: {ex.Message}");
            Console.WriteLine($"[ProcessVideoAsync] Stack trace: {ex.StackTrace}");
            var analysis = await videoAnalysisRepository.GetByIdAsync(analysisId);
            if (analysis != null)
            {
                analysis.Status = AnalysisStatus.Failed;
                analysis.AnalysisErrorMessage = ex.Message;
                analysis.Modified = DateTime.UtcNow;
                await videoAnalysisRepository.UpdateAsync(analysis);
                Console.WriteLine($"[ProcessVideoAsync] Analysis marked as Failed");
            }
        }
    }

    public async Task<(AnalysisStatus status, float? progress, string? error)> GetAnalysisStatusAsync(Guid analysisId)
    {
        var analysis = await _videoAnalysisRepository.GetByIdAsync(analysisId);
        if (analysis == null)
            throw new InvalidOperationException($"Analysis with ID {analysisId} not found");

        return (analysis.Status, analysis.ProcessingProgress, analysis.AnalysisErrorMessage);
    }

    public async Task<bool> CancelAnalysisAsync(Guid analysisId)
    {
        var analysis = await _videoAnalysisRepository.GetByIdAsync(analysisId);
        if (analysis == null)
            throw new InvalidOperationException($"Analysis with ID {analysisId} not found");

        if (analysis.Status == AnalysisStatus.Processing)
        {
            analysis.Status = AnalysisStatus.Cancelled;
            analysis.Modified = DateTime.UtcNow;
            await _videoAnalysisRepository.UpdateAsync(analysis);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteAnalysisAsync(Guid analysisId)
    {
        var analysis = await _videoAnalysisRepository.GetByIdAsync(analysisId);
        if (analysis == null)
            return false;

        if (!string.IsNullOrWhiteSpace(analysis.VideoPath) && File.Exists(analysis.VideoPath))
        {
            File.Delete(analysis.VideoPath);
        }

        await _videoAnalysisRepository.DeleteAsync(analysisId);
        return true;
    }

    private VideoAnalysisModel MapVideoAnalysisToModel(VideoAnalysisEntity entity)
    {
        return new VideoAnalysisModel
        {
            Id = entity.Id,
            Title = entity.Title,
            VideoFileName = entity.VideoFileName,
            VideoPath = entity.VideoPath,
            FileSizeBytes = entity.FileSizeBytes,
            Duration = entity.Duration,
            FramesPerSecond = entity.FramesPerSecond,
            Status = Enum.Parse<AnalysisStatus>(entity.Status.ToString()),
            AnalysisStartedAt = entity.AnalysisStartedAt,
            AnalysisCompletedAt = entity.AnalysisCompletedAt,
            AnalysisErrorMessage = entity.AnalysisErrorMessage,
            ProcessingProgress = entity.ProcessingProgress,
            TotalPasses = entity.TotalPasses,
            PassCompletionRate = entity.PassCompletionRate,
            TotalShots = entity.TotalShots,
            ShotsOnTarget = entity.ShotsOnTarget,
            TotalTackles = entity.TotalTackles,
            TacklesWon = entity.TacklesWon,
            TotalDistanceCovered = entity.TotalDistanceCovered,
            AverageSpeed = entity.AverageSpeed,
            HomeTeamColorPrimary = entity.HomeTeamColorPrimary,
            HomeTeamColorSecondary = entity.HomeTeamColorSecondary,
            AwayTeamColorPrimary = entity.AwayTeamColorPrimary,
            AwayTeamColorSecondary = entity.AwayTeamColorSecondary,
            CameraAngle = entity.CameraAngle,
            Created = entity.Created,
            Modified = entity.Modified,
            
            // Map advanced analytics
            PossessionData = entity.PossessionData != null ? new PossessionDataModel
            {
                HomeTeam = entity.PossessionData.HomeTeam,
                AwayTeam = entity.PossessionData.AwayTeam,
                HomePossessionSeconds = entity.PossessionData.HomePossessionSeconds,
                AwayPossessionSeconds = entity.PossessionData.AwayPossessionSeconds,
                HomePossessionPercentage = entity.PossessionData.HomePossessionPercentage,
                AwayPossessionPercentage = entity.PossessionData.AwayPossessionPercentage
            } : null,
            
            HeatMaps = entity.HeatMaps?.Select(h => new HeatMapDataModel
            {
                JerseyNumber = h.JerseyNumber?.ToString(),
                PlayerName = h.PlayerName,
                Team = h.Team,
                PositionData = h.PositionData
            }).ToList(),
            
            PlayerMetrics = entity.PlayerMetrics?.Select(m => new PlayerMetricsModel
            {
                JerseyNumber = m.JerseyNumber?.ToString(),
                PlayerName = m.PlayerName,
                Team = m.Team,
                DistanceCoveredMeters = m.DistanceCoveredMeters,
                MaxSpeedKmh = m.MaxSpeedKmh,
                AverageSpeedKmh = m.AverageSpeedKmh,
                SprintCount = m.SprintCount,
                MinutesPlayed = m.MinutesPlayed
            }).ToList()
        };
    }

    private PlayerStatModel MapPlayerStatToModel(PlayerStatEntity entity)
    {
        return new PlayerStatModel
        {
            Id = entity.Id,
            PlayerId = entity.PlayerId,
            VideoAnalysisId = entity.VideoAnalysisId,
            JerseyNumber = entity.JerseyNumber,
            PlayerName = entity.PlayerName,
            Team = entity.Team,
            Position = entity.Position,
            PassesAttempted = entity.PassesAttempted,
            PassesCompleted = entity.PassesCompleted,
            PassCompletionPercentage = entity.PassCompletionPercentage,
            AveragePassLength = entity.AveragePassLength,
            LongPasses = entity.LongPasses,
            LongPassesCompleted = entity.LongPassesCompleted,
            ShotsAttempted = entity.ShotsAttempted,
            ShotsOnTarget = entity.ShotsOnTarget,
            GoalsScored = entity.GoalsScored,
            ShotAccuracy = entity.ShotAccuracy,
            DistanceCovered = entity.DistanceCovered,
            Sprints = entity.Sprints,
            AverageSpeed = entity.AverageSpeed,
            MaxSpeed = entity.MaxSpeed,
            Tackles = entity.Tackles,
            TacklesWon = entity.TacklesWon,
            Interceptions = entity.Interceptions,
            Fouls = entity.Fouls,
            FoulsReceived = entity.FoulsReceived,
            Dribbles = entity.Dribbles,
            DribblesWon = entity.DribblesWon,
            Touches = entity.Touches,
            BallRecoveries = entity.BallRecoveries,
            Clearances = entity.Clearances,
            Created = entity.Created,
            Modified = entity.Modified
        };
    }

    private EventModel MapEventToModel(EventEntity entity)
    {
        return new EventModel
        {
            Id = entity.Id,
            VideoAnalysisId = entity.VideoAnalysisId,
            EventType = Enum.Parse<EventType>(entity.EventType.ToString()),
            Timestamp = entity.Timestamp,
            JerseyNumber = entity.JerseyNumber,
            PlayerName = entity.PlayerName,
            Team = entity.Team,
            Details = entity.Details,
            Successful = entity.Successful,
            XStart = entity.XStart,
            YStart = entity.YStart,
            XEnd = entity.XEnd,
            YEnd = entity.YEnd,
            Distance = entity.Distance,
            ClipUrl = entity.ClipUrl,
            Created = entity.Created,
            Modified = entity.Modified
        };
    }

    private async Task<bool> IsFfmpegAvailableAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task TrimClipWithFfmpegAsync(string sourcePath, string outputPath, int startSeconds, int durationSeconds)
    {
        Console.WriteLine($"[TrimClipWithFfmpegAsync] Starting trim operation");
        Console.WriteLine($"[TrimClipWithFfmpegAsync] Source: {sourcePath}");
        Console.WriteLine($"[TrimClipWithFfmpegAsync] Output: {outputPath}");
        Console.WriteLine($"[TrimClipWithFfmpegAsync] Start: {startSeconds}s, Duration: {durationSeconds}s");

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -nostdin -ss {startSeconds} -i \"{sourcePath}\" -t {durationSeconds} -c copy \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start ffmpeg for clip generation");

        var stderr = await process.StandardError.ReadToEndAsync();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Console.WriteLine($"[TrimClipWithFfmpegAsync] FFmpeg exit code: {process.ExitCode}");
        Console.WriteLine($"[TrimClipWithFfmpegAsync] StdErr: {stderr}");
        Console.WriteLine($"[TrimClipWithFfmpegAsync] File exists after trim: {File.Exists(outputPath)}");

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed to generate clip. ExitCode: {process.ExitCode}. StdErr: {stderr}. StdOut: {stdout}");
        }

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException($"ffmpeg completed but output file was not created at {outputPath}");
        }
    }
}
