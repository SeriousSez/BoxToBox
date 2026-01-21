namespace BoxToBox.Domain.Entities;

/// <summary>
/// Ball possession statistics for a match
/// </summary>
public class PossessionDataEntity : Base
{
    public Guid VideoAnalysisId { get; set; }
    
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    
    public int HomePossessionSeconds { get; set; }
    public int AwayPossessionSeconds { get; set; }
    
    public double HomePossessionPercentage => TotalPossessionSeconds > 0 
        ? (double)HomePossessionSeconds / TotalPossessionSeconds * 100 : 0;
    
    public double AwayPossessionPercentage => TotalPossessionSeconds > 0 
        ? (double)AwayPossessionSeconds / TotalPossessionSeconds * 100 : 0;
    
    private int TotalPossessionSeconds => HomePossessionSeconds + AwayPossessionSeconds;
    
    /// <summary>
    /// JSON array of possession sequences: [{"team": "Home", "start": 10, "end": 45, "duration": 35}, ...]
    /// </summary>
    public string PossessionSequences { get; set; } = "[]";
    
    /// <summary>
    /// Average possession duration per sequence (seconds)
    /// </summary>
    public double AverageHomePossessionDuration { get; set; }
    public double AverageAwayPossessionDuration { get; set; }
}
