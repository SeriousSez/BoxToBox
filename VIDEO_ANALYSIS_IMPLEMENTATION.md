# Video Analysis Implementation for BoxToBox

## Overview

This implementation adds comprehensive video analysis capabilities to the BoxToBox project. Users can upload football (soccer) match videos and receive detailed statistics including passing, shots, movement, positioning, and tackle analytics.

## Features

### 1. **Video Upload**

- Upload videos from local files or YouTube
- Associate videos with specific matches
- Automatic file handling and storage

### 2. **Batch Processing Analysis**

- Asynchronous video processing (background jobs)
- Real-time status monitoring
- Progress tracking for uploads

### 3. **Comprehensive Player Statistics**

- **Passing Metrics**: Completion rate, accuracy, distance, long pass success
- **Shooting Statistics**: Attempts, accuracy, shots on target, goals
- **Movement Tracking**: Distance covered, sprints, average/max speed
- **Defensive Actions**: Tackles, interceptions, tackles won, fouls
- **Possession**: Touches, ball recoveries, dribbles, clearances

### 4. **Match Events Timeline**

- Pass, shot, tackle, interception, dribble events
- Event timestamps with video synchronization
- Success/failure tracking for plays
- Player positioning data (x, y coordinates)

### 5. **Analytics Dashboard**

- Real-time match summary statistics
- Per-player performance cards
- Team filtering
- Event timeline visualization
- Status monitoring and error handling

## Architecture

### Backend Structure

#### Domain Layer (`BoxToBox.Domain`)

- **Entities**:
  - `MatchEntity`: Match details (teams, date, duration)
  - `PlayerEntity`: Player profile information
  - `VideoAnalysisEntity`: Video file and analysis metadata
  - `PlayerStatEntity`: Individual player statistics per match
  - `EventEntity`: Match events (passes, shots, tackles, etc.)

- **DTOs**: Data transfer objects for API responses

#### Application Service Layer (`BoxToBox.ApplicationService`)

- **IVideoAnalysisService**: Main service for video operations
  - `UploadVideoAsync()`: Handle video uploads
  - `StartAnalysisAsync()`: Begin processing
  - `GetAnalysisAsync()`: Retrieve analysis results
  - `GetPlayerStatsAsync()`: Get individual statistics
  - `GetEventsAsync()`: Retrieve timeline events
  - `GetAnalysisStatusAsync()`: Monitor progress

- **IVideoProcessor**: Pluggable video processing interface
  - `MockVideoProcessor`: Simulated analysis (ready for real ML integration)

#### Infrastructure Layer (`BoxToBox.Infrastructure`)

- **Repositories**: Data access for all entities
  - `VideoAnalysisRepository`
  - `MatchRepository`
  - `PlayerRepository`
  - `PlayerStatRepository`
  - `EventRepository`

- **DbContext**: Entity Framework configuration with relationships

#### API Layer (`BoxToBox.API`)

- **VideoAnalysisController**: RESTful endpoints
  - `POST /api/videoanalysis/upload/{matchId}`: Upload video
  - `GET /api/videoanalysis/{id}`: Get analysis details
  - `GET /api/videoanalysis/match/{matchId}`: Get match analyses
  - `GET /api/videoanalysis/{analysisId}/player-stats`: Get player statistics
  - `GET /api/videoanalysis/{analysisId}/events`: Get events
  - `POST /api/videoanalysis/{analysisId}/start`: Start processing
  - `GET /api/videoanalysis/{analysisId}/status`: Check status
  - `POST /api/videoanalysis/{analysisId}/cancel`: Cancel analysis

### Frontend Structure (Angular)

#### Services

- **VideoAnalysisService**: HTTP client for API communication
  - Type-safe DTOs matching backend models
  - Handles all video analysis operations

#### Components

**VideoUploadComponent**

- File selection and validation
- Match ID input
- Progress indication
- Error handling and feedback

**AnalyticsDashboardComponent**

- Real-time status polling (5-second intervals)
- Match summary statistics cards
- Per-player performance cards with team filtering
- Event timeline with filtering
- Responsive design with loading states

**VideoAnalysisComponent** (Container)

- Navigation between upload and analytics
- Route handling for analysis results

## Installation & Setup

### Prerequisites

```bash
# .NET 10+ SDK
# Node.js 18+
# Angular CLI
```

### Configuration

1. **Database Connection** (`appsettings.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=BoxToBox;Integrated Security=true;"
  },
  "VideoUploadPath": "Uploads/Videos"
}
```

2. **Backend Setup**:

```bash
cd BoxToBox.API
dotnet restore
dotnet build
```

3. **Frontend Setup**:

```bash
cd BoxToBox
npm install
```

### Database Migration

```bash
cd BoxToBox.API
dotnet ef migrations add InitialVideoAnalysis
dotnet ef database update
```

## Usage

### 1. Upload a Video

**Request**:

```
POST /api/videoanalysis/upload/{matchId}
Content-Type: multipart/form-data

file: <video_file>
```

**Response**:

```json
{
  "id": "guid",
  "matchId": "guid",
  "videoFileName": "match.mp4",
  "status": "Pending",
  "fileSizeBytes": 1000000
}
```

### 2. Start Analysis

```
POST /api/videoanalysis/{analysisId}/start
```

### 3. Check Progress

```
GET /api/videoanalysis/{analysisId}/status
```

**Response**:

```json
{
  "status": "Processing",
  "progress": 45.5,
  "error": null
}
```

### 4. Retrieve Results

```
GET /api/videoanalysis/{analysisId}
GET /api/videoanalysis/{analysisId}/player-stats
GET /api/videoanalysis/{analysisId}/events
```

## Video Processing (Future Integration)

The current implementation uses `MockVideoProcessor` for demonstration. To integrate real video analysis:

### Replace with Real ML Pipeline

```csharp
// Implement IVideoProcessor interface
public class YOLOv8VideoProcessor : IVideoProcessor
{
    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath, Guid analysisId)
    {
        // 1. Use OpenCV to extract frames
        // 2. Run YOLOv8 for player/ball detection
        // 3. Use MediaPipe for pose estimation
        // 4. Track movement and calculate statistics
        // 5. Return VideoAnalysisResult with data
    }
}
```

### Required Libraries

- **OpenCV** (video frame extraction)
- **YOLOv8** (player/ball detection)
- **MediaPipe** (pose estimation)
- **NumSharp** (matrix operations)

## Data Models

### PlayerStatEntity

Stores comprehensive performance metrics for each player per match.

**Key Statistics**:

- Passing: `passesAttempted`, `passesCompleted`, `passCompletionPercentage`, `averagePassLength`
- Shooting: `shotsAttempted`, `shotsOnTarget`, `goalsScored`, `shotAccuracy`
- Movement: `distanceCovered`, `sprints`, `averageSpeed`, `maxSpeed`
- Defense: `tackles`, `tacklesWon`, `interceptions`, `fouls`
- Possession: `touches`, `ballRecoveries`, `dribbles`, `clearances`

### EventEntity

Timeline events during the match.

**Event Types**:

- Pass, Shot, Tackle, Interception, Dribble
- Foul, FoulReceived, Clearance, BallRecovery
- GoalScored, ShotBlocked, Aerial
- Substitution (On/Off), YellowCard, RedCard

## API Response Examples

### Get Analysis Status

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "matchId": "550e8400-e29b-41d4-a716-446655440001",
  "videoFileName": "match.mp4",
  "fileSizeBytes": 1073741824,
  "duration": 2700,
  "framesPerSecond": 30,
  "status": "Completed",
  "analysisCompletedAt": "2026-01-19T14:30:00Z",
  "processingProgress": 100,
  "totalPasses": 450,
  "passCompletionRate": 82.5,
  "totalShots": 15,
  "shotsOnTarget": 6,
  "totalTackles": 35,
  "totalDistanceCovered": 105000
}
```

### Get Player Statistics

```json
[
  {
    "id": "guid",
    "playerName": "John Smith",
    "team": "Home Team",
    "position": "DF",
    "jerseyNumber": 4,
    "passesCompleted": 65,
    "passesAttempted": 78,
    "passCompletionPercentage": 83.3,
    "tacksles": 8,
    "tacklesWon": 7,
    "distanceCovered": 10500,
    "averageSpeed": 6.8
  }
]
```

## Performance Considerations

1. **Video File Size**: Consider implementing chunked uploads for large files
2. **Processing Time**: Batch processing via background jobs (Hangfire, Azure Queue)
3. **Real-time Updates**: WebSocket for live progress (upgrade from polling)
4. **Storage**: Cloud storage (Azure Blob, S3) for video files
5. **Caching**: Cache frequently accessed statistics

## Future Enhancements

### Phase 2

- [ ] Real-time streaming analysis (WebSocket)
- [ ] Multiple ML model support
- [ ] Player tracking across multiple matches
- [ ] Team formation detection
- [ ] Heat maps and positioning analysis
- [ ] Comparison reports (player vs player)

### Phase 3

- [ ] YouTube video integration (download and analyze)
- [ ] Live match analysis (streaming input)
- [ ] Advanced AI metrics (off-ball movement, pressing intensity)
- [ ] Mobile app for on-field recording
- [ ] Export reports (PDF, Excel)

## Testing

### Unit Tests

```bash
dotnet test BoxToBox.ApplicationService.Tests
```

### Integration Tests

```bash
dotnet test BoxToBox.API.Tests
```

### Frontend Tests

```bash
ng test
```

## Troubleshooting

### Common Issues

**"Match not found"**

- Ensure match exists in database before uploading video

**"Analysis failed"**

- Check error message in `analysisErrorMessage` field
- Verify video format is supported
- Check available disk space

**"Status not updating"**

- Verify background processing job started
- Check application logs
- Restart API service

## Security Considerations

1. **File Upload Validation**
   - Validate file type (video extensions)
   - Implement file size limits
   - Store files outside web root

2. **API Security**
   - Add authentication/authorization
   - Implement rate limiting
   - Use HTTPS only

3. **Data Privacy**
   - Encrypt sensitive player data
   - Implement data retention policies
   - Add audit logging

## Support

For issues or questions:

1. Check logs: `Uploads/Logs/` directory
2. Review error messages in API response
3. Verify database connectivity
4. Check video file format compatibility
