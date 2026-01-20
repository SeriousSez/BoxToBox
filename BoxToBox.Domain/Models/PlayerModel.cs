namespace BoxToBox.Domain.Models;

public class PlayerModel : Base
{
    public required string Name { get; set; }
    public int? JerseyNumber { get; set; }
    public string? Position { get; set; }
    public string? Team { get; set; }
    public int? Height { get; set; }
    public int? Weight { get; set; }
}
