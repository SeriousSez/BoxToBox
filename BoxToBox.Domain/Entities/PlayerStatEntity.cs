namespace BoxToBox.Domain.Entities;

public class PlayerStatEntity : Base
{
    public Guid PlayerId { get; set; }
    public Guid VideoAnalysisId { get; set; }
    public int JerseyNumber { get; set; }
    public required string PlayerName { get; set; }
    public required string Team { get; set; }
    public string? Position { get; set; }

    // Passing statistics
    public int PassesAttempted { get; set; }
    public int PassesCompleted { get; set; }
    public float? PassCompletionPercentage { get; set; }
    public float? AveragePassLength { get; set; } // meters
    public int? LongPasses { get; set; } // passes > 15m
    public int? LongPassesCompleted { get; set; }

    // Shooting statistics
    public int ShotsAttempted { get; set; }
    public int ShotsOnTarget { get; set; }
    public int GoalsScored { get; set; }
    public float? ShotAccuracy { get; set; } // percentage

    // Movement statistics
    public float? DistanceCovered { get; set; } // meters
    public int? Sprints { get; set; }
    public float? AverageSpeed { get; set; } // km/h
    public float? MaxSpeed { get; set; } // km/h

    // Defensive statistics
    public int Tackles { get; set; }
    public int TacklesWon { get; set; }
    public int Interceptions { get; set; }
    public int Fouls { get; set; }
    public int FoulsReceived { get; set; }
    public int Dribbles { get; set; }
    public int DribblesWon { get; set; }

    // Possession & Other
    public int Touches { get; set; }
    public int BallRecoveries { get; set; }
    public int Clearances { get; set; }

    // Navigation properties
    public PlayerEntity? Player { get; set; }
    public VideoAnalysisEntity? VideoAnalysis { get; set; }
}
