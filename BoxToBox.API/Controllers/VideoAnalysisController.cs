using BoxToBox.ApplicationService.Dtos;
using BoxToBox.ApplicationService.Services;
using BoxToBox.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace BoxToBox.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoAnalysisController : ControllerBase
{
    private readonly IVideoAnalysisService _videoAnalysisService;
    private readonly ILogger<VideoAnalysisController> _logger;

    public VideoAnalysisController(
        IVideoAnalysisService videoAnalysisService,
        ILogger<VideoAnalysisController> logger)
    {
        _videoAnalysisService = videoAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a video file for analysis - accepts match name or GUID
    /// </summary>
    [HttpPost("upload/{matchIdentifier}")]
    [RequestSizeLimit(5_368_709_120)] // 5 GB
    [RequestFormLimits(MultipartBodyLengthLimit = 5_368_709_120)]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<VideoAnalysisModel>> UploadVideo(string matchIdentifier, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        try
        {
            using (var stream = file.OpenReadStream())
            {
                var analysis = await _videoAnalysisService.UploadVideoAsync(matchIdentifier, stream, file.FileName);
                return CreatedAtAction(nameof(GetAnalysis), new { id = analysis.Id }, analysis);
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading video");
            return StatusCode(500, "An error occurred while uploading the video");
        }
    }

    /// <summary>
    /// Get video analysis details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<VideoAnalysisModel>> GetAnalysis(Guid id)
    {
        var analysis = await _videoAnalysisService.GetAnalysisAsync(id);
        if (analysis == null)
            return NotFound();

        _logger.LogInformation($"GetAnalysis called for ID: {id}, Status: {analysis.Status}, Progress: {analysis.ProcessingProgress}%");
        return Ok(analysis);
    }

    /// <summary>
    /// Get all video analyses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<VideoAnalysisModel>>> GetAnalyses()
    {
        var analyses = await _videoAnalysisService.GetAllAnalysesAsync();
        return Ok(analyses);
    }

    /// <summary>
    /// Get player statistics for an analysis
    /// </summary>
    [HttpGet("{analysisId}/player-stats")]
    public async Task<ActionResult<IEnumerable<PlayerStatModel>>> GetPlayerStats(Guid analysisId)
    {
        var stats = await _videoAnalysisService.GetPlayerStatsAsync(analysisId);
        _logger.LogInformation("GetPlayerStats called for {AnalysisId}. Count: {Count}", analysisId, stats?.Count() ?? 0);
        return Ok(stats);
    }

    /// <summary>
    /// Get timeline events for an analysis
    /// </summary>
    [HttpGet("{analysisId}/events")]
    public async Task<ActionResult<IEnumerable<EventModel>>> GetEvents(Guid analysisId)
    {
        var events = await _videoAnalysisService.GetEventsAsync(analysisId);
        return Ok(events);
    }

    /// <summary>
    /// Start analysis processing for a video with optional team roster and colors
    /// </summary>
    [HttpPost("{analysisId}/start")]
    public async Task<ActionResult<bool>> StartAnalysis(Guid analysisId, [FromBody] AnalysisRequest? analysisRequest = null)
    {
        try
        {
            var cameraAngle = analysisRequest?.CameraAngle ?? "Overhead";
            var result = await _videoAnalysisService.StartAnalysisAsync(analysisId, analysisRequest?.HomeTeam, analysisRequest?.AwayTeam, analysisRequest?.Goals, cameraAngle);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Generate a short clip for a specific event
    /// </summary>
    [HttpPost("{analysisId}/events/{eventId}/clip")]
    public async Task<ActionResult<object>> GenerateEventClip(Guid analysisId, Guid eventId, [FromQuery] int preSeconds = 2, [FromQuery] int postSeconds = 4)
    {
        try
        {
            var clipUrl = await _videoAnalysisService.GenerateEventClipAsync(analysisId, eventId, preSeconds, postSeconds);
            return Ok(new { clipUrl });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Download an already-generated clip for an event
    /// </summary>
    [HttpGet("{analysisId}/events/{eventId}/clip")]
    public IActionResult DownloadEventClip(Guid analysisId, Guid eventId)
    {
        var clipRelative = Path.Combine("clips", analysisId.ToString(), $"{eventId}.mp4");
        var clipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", clipRelative);

        if (!System.IO.File.Exists(clipPath))
        {
            return NotFound("Clip not found. Generate it first.");
        }

        var stream = new FileStream(clipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "video/mp4", $"clip-{eventId}.mp4");
    }

    /// <summary>
    /// Get analysis status and progress
    /// </summary>
    [HttpGet("{analysisId}/status")]
    public async Task<ActionResult<object>> GetAnalysisStatus(Guid analysisId)
    {
        var (status, progress, error) = await _videoAnalysisService.GetAnalysisStatusAsync(analysisId);
        return Ok(new { status = status.ToString(), progress, error });
    }

    /// <summary>
    /// Cancel an ongoing analysis
    /// </summary>
    [HttpPost("{analysisId}/cancel")]
    public async Task<ActionResult<bool>> CancelAnalysis(Guid analysisId)
    {
        var result = await _videoAnalysisService.CancelAnalysisAsync(analysisId);
        return Ok(result);
    }

    /// <summary>
    /// Delete an analysis
    /// </summary>
    [HttpDelete("{analysisId}")]
    public async Task<IActionResult> DeleteAnalysis(Guid analysisId)
    {
        var deleted = await _videoAnalysisService.DeleteAnalysisAsync(analysisId);
        return deleted ? NoContent() : NotFound();
    }
}
