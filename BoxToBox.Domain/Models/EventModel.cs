namespace BoxToBox.Domain.Models;

public class EventModel : Base
{
    public Guid VideoAnalysisId { get; set; }
    public required EventType EventType { get; set; }
    public int Timestamp { get; set; }
    public int? JerseyNumber { get; set; }
    public required string PlayerName { get; set; }
    public required string Team { get; set; }
    public string? Details { get; set; }
    public bool? Successful { get; set; }
    public float? XStart { get; set; }
    public float? YStart { get; set; }
    public float? XEnd { get; set; }
    public float? YEnd { get; set; }
    public float? Distance { get; set; }

    public string? ClipUrl { get; set; }
}