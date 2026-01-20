namespace BoxToBox.ApplicationService.Dtos;

public class TeamRosterRequest
{
    public string TeamName { get; set; } = string.Empty;
    public string JerseyColorHome { get; set; } = "#FFFFFF"; // RGB hex
    public string JerseyColorAway { get; set; } = "#000000"; // RGB hex
    public List<PlayerInfo> Players { get; set; } = new();
}

public class PlayerInfo
{
    public int JerseyNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty; // GK, DF, MF, FW
    public string Team { get; set; } = string.Empty; // "Home Team" or "Away Team"
}

public class GoalInfo
{
    public int TimestampSeconds { get; set; }
    public string Team { get; set; } = string.Empty; // "Home Team" or "Away Team"
    public int JerseyNumber { get; set; }
    public string PlayerName { get; set; } = string.Empty;
}

public class AnalysisRequest
{
    public TeamRosterRequest? HomeTeam { get; set; }
    public TeamRosterRequest? AwayTeam { get; set; }
    public List<GoalInfo> Goals { get; set; } = new();
    public string CameraAngle { get; set; } = "Overhead";}