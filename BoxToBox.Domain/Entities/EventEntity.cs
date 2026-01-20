namespace BoxToBox.Domain.Entities;

public class EventEntity : Base
{
    public Guid VideoAnalysisId { get; set; }
    public required EventType EventType { get; set; }
    public int Timestamp { get; set; } // Timestamp in seconds
    public int? JerseyNumber { get; set; }
    public required string PlayerName { get; set; }
    public required string Team { get; set; }
    
    // Event-specific details
    public string? Details { get; set; } // e.g., "Pass to X", "Shot on target", "Tackle won"
    public bool? Successful { get; set; } // For pass, tackle, etc.
    public float? XStart { get; set; } // Normalized coordinates (0-1)
    public float? YStart { get; set; }
    public float? XEnd { get; set; }
    public float? YEnd { get; set; }
    public float? Distance { get; set; } // meters, for passes

    public string? ClipUrl { get; set; }

    // Navigation properties
    public VideoAnalysisEntity? VideoAnalysis { get; set; }
}