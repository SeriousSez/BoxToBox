namespace BoxToBox.Domain.Models;

public class HeatMapDataModel
{
    public string? JerseyNumber { get; set; }
    public string? PlayerName { get; set; }
    public string Team { get; set; } = string.Empty;
    public string PositionData { get; set; } = "[]";
}
