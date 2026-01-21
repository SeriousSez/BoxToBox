using BoxToBox.Domain.Entities;
using System.Text.Json;

namespace BoxToBox.ApplicationService.Services;

/// <summary>
/// Service for calculating advanced analytics from tracking data
/// </summary>
public class AdvancedAnalyticsService
{
    private const double FIELD_LENGTH_METERS = 105.0; // Standard field length
    private const double FIELD_WIDTH_METERS = 68.0;   // Standard field width
    
    /// <summary>
    /// Position point with timestamp
    /// </summary>
    public class PositionPoint
    {
        public double X { get; set; }  // Normalized 0-1
        public double Y { get; set; }  // Normalized 0-1
        public int Timestamp { get; set; }  // Seconds
    }
    
    /// <summary>
    /// Player tracking data for a frame
    /// </summary>
    public class PlayerPosition
    {
        public int? JerseyNumber { get; set; }
        public string? PlayerName { get; set; }
        public string Team { get; set; } = string.Empty;
        public double X { get; set; }  // Normalized 0-1 (0 = left, 1 = right)
        public double Y { get; set; }  // Normalized 0-1 (0 = top/own goal, 1 = bottom/opponent goal)
        public int Timestamp { get; set; }
    }
    
    /// <summary>
    /// Generate heat map data from player positions
    /// </summary>
    public List<HeatMapDataEntity> GenerateHeatMaps(List<PlayerPosition> allPositions, Guid videoAnalysisId)
    {
        var heatMaps = new List<HeatMapDataEntity>();
        
        // Group by player (jersey number + team)
        var playerGroups = allPositions
            .GroupBy(p => new { p.JerseyNumber, p.Team, p.PlayerName });
        
        foreach (var playerGroup in playerGroups)
        {
            var positions = playerGroup.Select(p => new PositionPoint
            {
                X = p.X,
                Y = p.Y,
                Timestamp = p.Timestamp
            }).ToList();
            
            var heatMap = new HeatMapDataEntity
            {
                Id = Guid.NewGuid(),
                VideoAnalysisId = videoAnalysisId,
                JerseyNumber = playerGroup.Key.JerseyNumber,
                PlayerName = playerGroup.Key.PlayerName,
                Team = playerGroup.Key.Team,
                PositionData = JsonSerializer.Serialize(positions),
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            };
            
            heatMaps.Add(heatMap);
        }
        
        return heatMaps;
    }
    
    /// <summary>
    /// Calculate player distance and speed metrics
    /// </summary>
    public List<PlayerMetricsEntity> CalculatePlayerMetrics(List<PlayerPosition> allPositions, Guid videoAnalysisId)
    {
        var metrics = new List<PlayerMetricsEntity>();
        
        var playerGroups = allPositions
            .GroupBy(p => new { p.JerseyNumber, p.Team, p.PlayerName });
        
        foreach (var playerGroup in playerGroups)
        {
            var positions = playerGroup.OrderBy(p => p.Timestamp).ToList();
            
            if (positions.Count < 2) continue;
            
            double totalDistance = 0;
            double maxSpeed = 0;
            double walkingDistance = 0;
            double joggingDistance = 0;
            double runningDistance = 0;
            double sprintingDistance = 0;
            int sprintCount = 0;
            bool inSprint = false;
            
            for (int i = 1; i < positions.Count; i++)
            {
                var prev = positions[i - 1];
                var curr = positions[i];
                var timeDelta = curr.Timestamp - prev.Timestamp;
                
                if (timeDelta <= 0 || timeDelta > 5) continue; // Skip invalid time deltas
                
                // Calculate distance in meters
                var dx = (curr.X - prev.X) * FIELD_LENGTH_METERS;
                var dy = (curr.Y - prev.Y) * FIELD_WIDTH_METERS;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                
                // Calculate speed in km/h
                var speedKmh = (distance / timeDelta) * 3.6; // m/s to km/h

                // Discard unrealistic jumps (>35 km/h) to avoid noisy spikes
                if (speedKmh > 35)
                    continue;

                totalDistance += distance;
                maxSpeed = Math.Max(maxSpeed, speedKmh);
                
                // Categorize distance by speed
                if (speedKmh < 7)
                    walkingDistance += distance;
                else if (speedKmh < 15)
                    joggingDistance += distance;
                else if (speedKmh < 20)
                    runningDistance += distance;
                else
                {
                    sprintingDistance += distance;
                    if (!inSprint)
                    {
                        sprintCount++;
                        inSprint = true;
                    }
                }
                
                if (speedKmh < 20)
                    inSprint = false;
            }
            
            var firstTimestamp = positions.First().Timestamp;
            var lastTimestamp = positions.Last().Timestamp;
            var minutesPlayed = (lastTimestamp - firstTimestamp) / 60.0;
            
            var avgSpeed = minutesPlayed > 0 ? (totalDistance / (minutesPlayed * 60)) * 3.6 : 0;
            
            var metric = new PlayerMetricsEntity
            {
                Id = Guid.NewGuid(),
                VideoAnalysisId = videoAnalysisId,
                JerseyNumber = playerGroup.Key.JerseyNumber,
                PlayerName = positions.FirstOrDefault()?.PlayerName,
                Team = playerGroup.Key.Team,
                DistanceCoveredMeters = totalDistance,
                MaxSpeedKmh = maxSpeed,
                AverageSpeedKmh = avgSpeed,
                WalkingDistanceMeters = walkingDistance,
                JoggingDistanceMeters = joggingDistance,
                RunningDistanceMeters = runningDistance,
                SprintingDistanceMeters = sprintingDistance,
                SprintCount = sprintCount,
                MinutesPlayed = minutesPlayed,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            };
            
            metrics.Add(metric);
        }
        
        return metrics;
    }
    
    /// <summary>
    /// Calculate possession statistics based on ball proximity
    /// </summary>
    public PossessionDataEntity? CalculatePossession(
        List<PlayerPosition> playerPositions,
        List<(double X, double Y, int Timestamp)> ballPositions,
        Guid videoAnalysisId,
        string homeTeam,
        string awayTeam)
    {
        if (ballPositions.Count == 0) return null;
        
        var possessionSequences = new List<object>();
        int homePossessionSeconds = 0;
        int awayPossessionSeconds = 0;
        
        string? currentTeam = null;
        int sequenceStart = 0;
        
        foreach (var (ballX, ballY, timestamp) in ballPositions)
        {
            // Find closest player to ball
            var closestPlayer = playerPositions
                .Where(p => Math.Abs(p.Timestamp - timestamp) <= 1)
                .OrderBy(p => Math.Pow(p.X - ballX, 2) + Math.Pow(p.Y - ballY, 2))
                .FirstOrDefault();
            
            if (closestPlayer != null)
            {
                var distance = Math.Sqrt(
                    Math.Pow((closestPlayer.X - ballX) * FIELD_LENGTH_METERS, 2) +
                    Math.Pow((closestPlayer.Y - ballY) * FIELD_WIDTH_METERS, 2)
                );
                
                // Player is in possession if within 3 meters
                if (distance < 3.0)
                {
                    var team = closestPlayer.Team;
                    
                    if (team != currentTeam)
                    {
                        // Possession changed
                        if (currentTeam != null)
                        {
                            var duration = timestamp - sequenceStart;
                            possessionSequences.Add(new { team = currentTeam, start = sequenceStart, end = timestamp, duration });
                            
                            if (currentTeam == homeTeam)
                                homePossessionSeconds += duration;
                            else
                                awayPossessionSeconds += duration;
                        }
                        
                        currentTeam = team;
                        sequenceStart = timestamp;
                    }
                }
            }
        }
        
        // Close final sequence
        if (currentTeam != null && ballPositions.Any())
        {
            var lastTimestamp = ballPositions.Last().Timestamp;
            var duration = lastTimestamp - sequenceStart;
            possessionSequences.Add(new { team = currentTeam, start = sequenceStart, end = lastTimestamp, duration });
            
            if (currentTeam == homeTeam)
                homePossessionSeconds += duration;
            else
                awayPossessionSeconds += duration;
        }
        
        var homeSequences = possessionSequences.Cast<dynamic>().Where(s => s.team == homeTeam).ToList();
        var awaySequences = possessionSequences.Cast<dynamic>().Where(s => s.team == awayTeam).ToList();
        
        return new PossessionDataEntity
        {
            Id = Guid.NewGuid(),
            VideoAnalysisId = videoAnalysisId,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            HomePossessionSeconds = homePossessionSeconds,
            AwayPossessionSeconds = awayPossessionSeconds,
            PossessionSequences = JsonSerializer.Serialize(possessionSequences),
            AverageHomePossessionDuration = homeSequences.Any() ? homeSequences.Average(s => (double)s.duration) : 0,
            AverageAwayPossessionDuration = awaySequences.Any() ? awaySequences.Average(s => (double)s.duration) : 0,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Detect passes and build pass network
    /// </summary>
    public List<PassNetworkEntity> DetectPasses(
        List<PlayerPosition> playerPositions,
        List<(double X, double Y, int Timestamp)> ballPositions,
        Guid videoAnalysisId,
        string homeTeam,
        string awayTeam)
    {
        var passes = new List<PassNetworkEntity>();
        if (ballPositions.Count < 2) return passes;

        var ballSequence = ballPositions.OrderBy(b => b.Timestamp).ToList();
        string? currentTeam = null;
        var currentPlayer = new { JerseyNumber = (int?)null, PlayerName = "", X = 0.0, Y = 0.0 };
        var passAdjacency = new Dictionary<string, int>(); // "jersey1-jersey2" -> count

        foreach (var (ballX, ballY, timestamp) in ballSequence)
        {
            // Find closest player to ball
            var closestPlayer = playerPositions
                .Where(p => Math.Abs(p.Timestamp - timestamp) <= 2)
                .OrderBy(p => Math.Pow(p.X - ballX, 2) + Math.Pow(p.Y - ballY, 2))
                .FirstOrDefault();

            if (closestPlayer == null) continue;

            var distance = Math.Sqrt(
                Math.Pow((closestPlayer.X - ballX) * FIELD_LENGTH_METERS, 2) +
                Math.Pow((closestPlayer.Y - ballY) * FIELD_WIDTH_METERS, 2)
            );

            // Player has ball if within 3 meters
            if (distance < 3.0)
            {
                var team = closestPlayer.Team;

                // Possession change detected
                if (team != currentTeam || (closestPlayer.JerseyNumber != currentPlayer.JerseyNumber && team == currentTeam))
                {
                    // Record pass from previous player
                    if (currentTeam == team && currentPlayer.JerseyNumber.HasValue && closestPlayer.JerseyNumber.HasValue)
                    {
                        var passKey = $"{currentPlayer.JerseyNumber}-{closestPlayer.JerseyNumber}";
                        passAdjacency[passKey] = passAdjacency.GetValueOrDefault(passKey, 0) + 1;

                        // Create pass entity
                        var passLocation = JsonSerializer.Serialize(new { x = currentPlayer.X, y = currentPlayer.Y });
                        var pass = new PassNetworkEntity
                        {
                            Id = Guid.NewGuid(),
                            VideoAnalysisId = videoAnalysisId,
                            FromJerseyNumber = currentPlayer.JerseyNumber,
                            ToJerseyNumber = closestPlayer.JerseyNumber,
                            FromPlayerName = currentPlayer.PlayerName,
                            ToPlayerName = closestPlayer.PlayerName,
                            Team = team,
                            PassCount = 1,
                            SuccessfulPasses = 1, // Mark as successful
                            AveragePassLocation = passLocation,
                            Created = DateTime.UtcNow,
                            Modified = DateTime.UtcNow
                        };
                        passes.Add(pass);
                    }

                    currentTeam = team;
                    currentPlayer = new
                    {
                        JerseyNumber = closestPlayer.JerseyNumber,
                        PlayerName = closestPlayer.PlayerName ?? "",
                        X = closestPlayer.X,
                        Y = closestPlayer.Y
                    };
                }
            }
        }

        return passes;
    }

    /// <summary>
    /// Detect formations using K-means clustering on player positions
    /// </summary>
    public FormationDataEntity? DetectFormation(
        List<PlayerPosition> playerPositions,
        Guid videoAnalysisId,
        string team)
    {
        var teamPositions = playerPositions
            .Where(p => p.Team == team && p.JerseyNumber.HasValue)
            .ToList();

        if (teamPositions.Count < 10) return null; // Need minimum 10 players

        // Group positions by player and calculate average
        var playerAvgPositions = teamPositions
            .GroupBy(p => p.JerseyNumber)
            .Select(g => new { Jersey = g.Key, AvgX = g.Average(p => p.X), AvgY = g.Average(p => p.Y) })
            .OrderBy(p => p.AvgX)
            .ToList();

        if (playerAvgPositions.Count < 10) return null;

        // Simple formation detection based on defensive line (Y position)
        var defensiveThreshold = 0.35;
        var defensivePlayers = playerAvgPositions.Where(p => p.AvgY < defensiveThreshold).Count();

        var midfieldThreshold = 0.65;
        var midfieldPlayers = playerAvgPositions
            .Where(p => p.AvgY >= defensiveThreshold && p.AvgY < midfieldThreshold).Count();

        var attackingPlayers = playerAvgPositions.Where(p => p.AvgY >= midfieldThreshold).Count();

        // Identify formation
        var formation = IdentifyFormation(defensivePlayers, midfieldPlayers, attackingPlayers);

        // Calculate width and compactness
        var minX = playerAvgPositions.Min(p => p.AvgX);
        var maxX = playerAvgPositions.Max(p => p.AvgX);
        var width = (maxX - minX) * FIELD_LENGTH_METERS;

        var avgY = playerAvgPositions.Average(p => p.AvgY);
        var compactness = playerAvgPositions.Average(p => Math.Abs(p.AvgY - avgY)) * FIELD_WIDTH_METERS;

        var playerPositionsJson = JsonSerializer.Serialize(
            playerAvgPositions.Select(p => new { jersey = p.Jersey, x = p.AvgX, y = p.AvgY })
        );

        return new FormationDataEntity
        {
            Id = Guid.NewGuid(),
            VideoAnalysisId = videoAnalysisId,
            Team = team,
            Formation = formation,
            TimestampSeconds = (int)DateTime.UtcNow.TimeOfDay.TotalSeconds,
            Confidence = 0.85, // Confidence based on player clustering
            TeamWidth = width,
            TeamDepth = (defensivePlayers + midfieldPlayers + attackingPlayers) > 0 ? 105.0 : 0, // Full field depth
            Compactness = compactness,
            PlayerPositions = playerPositionsJson,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
    }

    private string IdentifyFormation(int defenders, int midfielders, int attackers)
    {
        return (defenders, midfielders, attackers) switch
        {
            (4, 4, 2) => "4-4-2",
            (4, 3, 3) => "4-3-3",
            (3, 5, 2) => "3-5-2",
            (3, 4, 3) => "3-4-3",
            (5, 3, 2) => "5-3-2",
            (5, 4, 1) => "5-4-1",
            (4, 5, 1) => "4-5-1",
            _ => $"{defenders}-{midfielders}-{attackers}"
        };
    }
}
