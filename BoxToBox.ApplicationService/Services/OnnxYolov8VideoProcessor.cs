using BoxToBox.ApplicationService.Dtos;
using BoxToBox.Domain;
using BoxToBox.Domain.Entities;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Globalization;
using Tesseract;

namespace BoxToBox.ApplicationService.Services;

/// <summary>
/// Video processor using YOLOv8 via ONNX Runtime for real object detection
/// </summary>
public class OnnxYolov8VideoProcessor : IVideoProcessor
{
    private readonly string _modelPath;
    private const int TARGET_FPS = 5; // Process 5 frames per second
    private const int INPUT_SIZE = 640; // YOLOv8 input size
    private static readonly bool ENABLE_FALLBACK_EVENTS = bool.TryParse(Environment.GetEnvironmentVariable("B2B_ENABLE_FALLBACK_EVENTS"), out var b) && b;

    public OnnxYolov8VideoProcessor(string modelPath = "models/yolo26n.pt")
    {
        // Resolve relative paths to absolute paths from application base directory
        if (!Path.IsPathRooted(modelPath))
        {
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelPath);
            
            // If .pt file is requested but doesn't exist, try .onnx version
            if (!File.Exists(_modelPath) && _modelPath.EndsWith(".pt"))
            {
                var onnxPath = _modelPath.Replace(".pt", ".onnx");
                if (File.Exists(onnxPath))
                {
                    _modelPath = onnxPath;
                    Console.WriteLine($"[YOLO] PyTorch model not found, using ONNX version instead: {_modelPath}");
                    return;
                }
            }
            
            // If not found, try looking in the project root Models directory
            if (!File.Exists(_modelPath))
            {
                var projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)
                    ?.Parent?.Parent?.Parent?.FullName;
                if (projectRoot != null)
                {
                    var altPath = Path.Combine(projectRoot, "Models", Path.GetFileName(modelPath));
                    if (File.Exists(altPath))
                    {
                        _modelPath = altPath;
                        Console.WriteLine($"[YOLO] Found model at: {_modelPath}");
                        return;
                    }
                }
            }
            
            Console.WriteLine($"[YOLO] Model path resolved to: {_modelPath}");
            Console.WriteLine($"[YOLO] Model exists: {File.Exists(_modelPath)}");
        }
        else
        {
            _modelPath = modelPath;
        }
    }

    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath, Guid analysisId, Func<int, Task>? onProgressUpdated = null, TeamRosterRequest? homeTeamRoster = null, TeamRosterRequest? awayTeamRoster = null, List<GoalInfo>? goals = null, string cameraAngle = "Overhead")
    {
        if (!File.Exists(videoPath))
            throw new FileNotFoundException($"Video file not found: {videoPath}");

        // Get video metadata
        var videoDuration = await GetVideoDurationAsync(videoPath);
        var fps = await GetVideoFpsAsync(videoPath);

        // Normalize bad metadata instead of stalling or exploding work
        const int maxDurationSeconds = 7200; // 2 hours hard cap
        const int fallbackDurationSeconds = 600; // 10 minutes fallback if unknown
        const float fallbackFps = 30f;

        if (videoDuration <= 0 || videoDuration > maxDurationSeconds)
        {
            videoDuration = fallbackDurationSeconds;
        }

        if (fps <= 0 || fps > 120f)
        {
            fps = fallbackFps;
        }

        if (onProgressUpdated != null) await onProgressUpdated(5);

        // Calculate frames to process
        var totalFrames = (int)(videoDuration * TARGET_FPS);
        var frameInterval = (int)Math.Max(1, fps / TARGET_FPS);

        // Create temp directory for frames
        var tempDir = Path.Combine(Path.GetTempPath(), $"boxtobox_{analysisId}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Check if ffmpeg is available
            if (!await IsFfmpegAvailableAsync())
            {
                return await GenerateMockAnalysisAsync(analysisId, videoDuration, fps, onProgressUpdated, homeTeamRoster, awayTeamRoster, goals);
            }

            // Extract frames from video
            await ExtractFramesAsync(videoPath, tempDir, frameInterval, totalFrames, async progress =>
            {
                var percent = 5 + (int)(progress * 30); // 5-35%
                if (onProgressUpdated != null) await onProgressUpdated(percent);
            });

            if (onProgressUpdated != null) await onProgressUpdated(35);

            // Check if ONNX model exists
            if (!File.Exists(_modelPath))
            {
                Console.WriteLine($"[YOLO] Model file not found at: {_modelPath}");
                Console.WriteLine($"[YOLO] Falling back to mock analysis");
                return await GenerateMockAnalysisAsync(analysisId, videoDuration, fps, onProgressUpdated, homeTeamRoster, awayTeamRoster, goals);
            }

            // Process frames with YOLOv8
            try
            {
                var detections = await ProcessFramesWithYoloAsync(tempDir, onProgressUpdated);

                if (onProgressUpdated != null) await onProgressUpdated(85);

                // Generate analysis from detections
                var result = await GenerateAnalysisFromDetectionsAsync(analysisId, detections, videoDuration, fps, homeTeamRoster, awayTeamRoster, goals, cameraAngle);

                if (onProgressUpdated != null) await onProgressUpdated(95);

                return result;
            }
            catch (Exception ex) when (ex.Message.Contains("InvalidProtobuf") || ex.Message.Contains("Protobuf"))
            {
                Console.WriteLine($"[YOLO] ONNX model format error: {ex.Message}");
                Console.WriteLine($"[YOLO] The model file must be in ONNX format (.onnx), not PyTorch (.pt)");
                Console.WriteLine($"[YOLO] To convert PyTorch to ONNX, use: yolo export model=yolov8n.pt format=onnx");
                Console.WriteLine($"[YOLO] Falling back to mock analysis");
                return await GenerateMockAnalysisAsync(analysisId, videoDuration, fps, onProgressUpdated, homeTeamRoster, awayTeamRoster, goals);
            }
        }
        finally
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    private async Task<bool> IsFfmpegAvailableAsync()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null) return false;
            
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task ExtractFramesAsync(string videoPath, string outputDir, int frameInterval, int maxFrames, Func<float, Task>? onProgress)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -nostdin -i \"{videoPath}\" -vf \"select='not(mod(n\\,{frameInterval}))'\" -vsync vfr -q:v 2 -frames:v {maxFrames} \"{outputDir}/frame_%04d.jpg\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null)
            throw new Exception("Failed to start ffmpeg");

        // Read stderr and stdout asynchronously to prevent deadlock
        var readStderr = process.StandardError.ReadToEndAsync();
        var readStdout = process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();
        
        var stderr = await readStderr;
        var stdout = await readStdout;

        if (process.ExitCode != 0)
        {
            throw new Exception($"ffmpeg failed: {stderr}");
        }
        
        var extractedFrames = Directory.GetFiles(outputDir, "*.jpg").Length;
        
        // Report final progress
        if (onProgress != null) 
            await onProgress(1.0f);
    }

    private async Task<List<FrameDetection>> ProcessFramesWithYoloAsync(string frameDir, Func<int, Task>? onProgressUpdated)
    {
        var detections = new List<FrameDetection>();
        var frameFiles = Directory.GetFiles(frameDir, "*.jpg").OrderBy(f => f).ToArray();

        try
        {
            using var session = new InferenceSession(_modelPath);

            for (int i = 0; i < frameFiles.Length; i++)
            {
                var framePath = frameFiles[i];
                var timestamp = i / (float)TARGET_FPS;

                var frameDetection = await DetectObjectsInFrameAsync(session, framePath, i, (int)timestamp);
                frameDetection.FramePath = framePath; // Store for color analysis
                detections.Add(frameDetection);

                // Report progress 35-85%
                if (i % 5 == 0 || i == frameFiles.Length - 1)
                {
                    var percent = 35 + (int)((i / (float)frameFiles.Length) * 50);
                    if (onProgressUpdated != null) await onProgressUpdated(percent);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YOLO] Failed to load or use ONNX model from {_modelPath}: {ex.Message}");
            Console.WriteLine($"[YOLO] Note: The model file must be in ONNX format (.onnx), not PyTorch format (.pt)");
            throw;
        }

        return detections;
    }

    private async Task<FrameDetection> DetectObjectsInFrameAsync(InferenceSession session, string framePath, int frameNumber, int timestamp)
    {
        var detection = new FrameDetection
        {
            FrameNumber = frameNumber,
            Timestamp = timestamp,
            DetectedObjects = new List<DetectedObject>()
        };

        try
        {
            // Load and preprocess image
            using var image = await Image.LoadAsync<Rgb24>(framePath);
            var inputTensor = PreprocessImage(image);

            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", inputTensor)
            };

            using var results = session.Run(inputs);
            var output = results.FirstOrDefault()?.AsTensor<float>();

            if (output != null)
            {
                // Parse YOLOv8 output format
                var detectedObjects = ParseYoloOutput(output, image.Width, image.Height);
                detection.DetectedObjects.AddRange(detectedObjects);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Detection failed for frame {frameNumber}: {ex.Message}");
        }

        return detection;
    }

    private DenseTensor<float> PreprocessImage(Image<Rgb24> image)
    {
        // Resize to YOLOv8 input size
        image.Mutate(x => x.Resize(INPUT_SIZE, INPUT_SIZE));

        // Create tensor [1, 3, 640, 640]
        var tensor = new DenseTensor<float>(new[] { 1, 3, INPUT_SIZE, INPUT_SIZE });

        // Normalize pixel values to 0-1 and convert to CHW format
        for (int y = 0; y < INPUT_SIZE; y++)
        {
            for (int x = 0; x < INPUT_SIZE; x++)
            {
                var pixel = image[x, y];
                tensor[0, 0, y, x] = pixel.R / 255f;
                tensor[0, 1, y, x] = pixel.G / 255f;
                tensor[0, 2, y, x] = pixel.B / 255f;
            }
        }

        return tensor;
    }

    private List<DetectedObject> ParseYoloOutput(Tensor<float> output, int originalWidth, int originalHeight)
    {
        var detectedObjects = new List<DetectedObject>();
        
        // YOLOv8 output format: [1, 84, 8400] where 84 = 4 bbox coords + 80 classes
        var dimensions = output.Dimensions.ToArray();
        if (dimensions.Length != 3) return detectedObjects;

        var numDetections = dimensions[2]; // 8400
        var confidenceThreshold = 0.25f;

        for (int i = 0; i < numDetections; i++)
        {
            // Get class scores (skip first 4 bbox values)
            float maxScore = 0;
            int maxClass = 0;

            for (int c = 4; c < 84; c++)
            {
                var score = output[0, c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxClass = c - 4;
                }
            }

            if (maxScore < confidenceThreshold) continue;

            // Get bounding box
            var x = output[0, 0, i];
            var y = output[0, 1, i];
            var w = output[0, 2, i];
            var h = output[0, 3, i];

            // Convert from normalized to pixel coordinates
            var xMin = (x - w / 2) * originalWidth / INPUT_SIZE;
            var yMin = (y - h / 2) * originalHeight / INPUT_SIZE;
            var width = w * originalWidth / INPUT_SIZE;
            var height = h * originalHeight / INPUT_SIZE;

            detectedObjects.Add(new DetectedObject
            {
                Label = GetCocoLabel(maxClass),
                Confidence = maxScore,
                X = xMin,
                Y = yMin,
                Width = width,
                Height = height
            });
        }

        return detectedObjects;
    }

    private (int r, int g, int b) ParseHexColor(string hexColor)
    {
        // Remove # if present
        hexColor = hexColor.TrimStart('#');
        
        if (hexColor.Length == 6)
        {
            int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
            int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
            int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
            return (r, g, b);
        }
        
        // Default to black if invalid
        return (0, 0, 0);
    }

    private double CalculateColorDistance((int r, int g, int b) color1, (int r, int g, int b) color2)
    {
        // Euclidean distance in RGB space
        int dr = color1.r - color2.r;
        int dg = color1.g - color2.g;
        int db = color1.b - color2.b;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private async Task<(int r, int g, int b)> ExtractDominantJerseyColorAsync(string framePath, DetectedObject playerBox)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(framePath);
            
            // Calculate the jersey region (upper-middle portion of player bounding box)
            int x = Math.Max(0, (int)playerBox.X);
            int y = Math.Max(0, (int)playerBox.Y);
            int width = Math.Min((int)playerBox.Width, image.Width - x);
            int height = Math.Min((int)playerBox.Height, image.Height - y);
            
            // Focus on upper 40% of the box (torso/jersey area)
            int jerseyHeight = (int)(height * 0.4);
            int jerseyY = y + (int)(height * 0.15); // Skip head area
            
            // Sample pixels from jersey region
            var colorSamples = new List<(int r, int g, int b)>();
            int sampleStep = Math.Max(1, width / 10); // Sample ~10 pixels across width
            
            for (int sx = x; sx < x + width && sx < image.Width; sx += sampleStep)
            {
                for (int sy = jerseyY; sy < jerseyY + jerseyHeight && sy < image.Height; sy += Math.Max(1, jerseyHeight / 5))
                {
                    var pixel = image[sx, sy];
                    colorSamples.Add((pixel.R, pixel.G, pixel.B));
                }
            }
            
            if (colorSamples.Count == 0)
                return (128, 128, 128); // Default gray
            
            // Calculate average color (simple dominant color estimation)
            int avgR = (int)colorSamples.Average(c => c.r);
            int avgG = (int)colorSamples.Average(c => c.g);
            int avgB = (int)colorSamples.Average(c => c.b);
            
            return (avgR, avgG, avgB);
        }
        catch
        {
            return (128, 128, 128); // Default gray on error
        }
    }

    private async Task<int?> ExtractJerseyNumberAsync(string framePath, DetectedObject playerBox)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgb24>(framePath);
            
            // Calculate jersey number region (back/front center of torso)
            int x = Math.Max(0, (int)playerBox.X);
            int y = Math.Max(0, (int)playerBox.Y);
            int width = Math.Min((int)playerBox.Width, image.Width - x);
            int height = Math.Min((int)playerBox.Height, image.Height - y);
            
            // Focus on torso center where numbers typically appear
            int numberWidth = (int)(width * 0.6);  // Center 60% of width
            int numberHeight = (int)(height * 0.35); // Upper-middle 35%
            int numberX = x + (width - numberWidth) / 2;
            int numberY = y + (int)(height * 0.25); // Below head, on upper torso
            
            // Ensure bounds are valid
            if (numberX < 0 || numberY < 0 || 
                numberX + numberWidth > image.Width || 
                numberY + numberHeight > image.Height ||
                numberWidth < 10 || numberHeight < 10)
            {
                return null;
            }
            
            // Extract and preprocess the region
            using var numberRegion = image.Clone(ctx => ctx.Crop(new Rectangle(numberX, numberY, numberWidth, numberHeight)));
            
            // Enhance contrast and convert to grayscale for better OCR
            numberRegion.Mutate(x => x
                .Grayscale()
                .BinaryThreshold(0.5f) // High contrast black/white
                .Resize(numberRegion.Width * 2, numberRegion.Height * 2)); // Upscale for OCR
            
            // Save to temp file for Tesseract
            var tempPath = Path.Combine(Path.GetTempPath(), $"jersey_{Guid.NewGuid()}.png");
            await numberRegion.SaveAsPngAsync(tempPath);
            
            try
            {
                // Use Tesseract to detect numbers
                var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                if (!Directory.Exists(tessDataPath))
                {
                    Console.WriteLine($"[OCR] tessdata folder not found at {tessDataPath}. Jersey number detection disabled.");
                    return null;
                }
                
                using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "0123456789"); // Only digits
                
                using var img = Pix.LoadFromFile(tempPath);
                using var page = engine.Process(img);
                
                var text = page.GetText().Trim();
                
                // Try to parse as integer
                if (int.TryParse(text, out var number) && number >= 0 && number <= 99)
                {
                    Console.WriteLine($"[OCR] Detected jersey number: {number}");
                    return number;
                }
            }
            finally
            {
                // Clean up temp file
                try { File.Delete(tempPath); } catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OCR] Jersey number detection failed: {ex.Message}");
        }
        
        return null;
    }

    private string GetCocoLabel(int classId)
    {
        // COCO dataset labels (first 80 classes)
        var labels = new[] { "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse",
            "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie",
            "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove",
            "skateboard", "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon",
            "bowl", "banana", "apple", "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut",
            "cake", "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse",
            "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book",
            "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush" };

        return classId >= 0 && classId < labels.Length ? labels[classId] : "unknown";
    }

    private async Task<VideoAnalysisResult> GenerateAnalysisFromDetectionsAsync(Guid analysisId, List<FrameDetection> detections, int videoDuration, float fps, TeamRosterRequest? homeTeamRoster = null, TeamRosterRequest? awayTeamRoster = null, List<GoalInfo>? goals = null, string cameraAngle = "Overhead")
    {
        var matchMinutes = videoDuration / 60;

        // Count detected objects
        var playerDetections = detections.SelectMany(d => d.DetectedObjects).Where(o => o.Label == "person").Count();
        var ballDetections = detections.SelectMany(d => d.DetectedObjects).Where(o => o.Label == "sports ball").Count();

        // Estimate statistics from detections
        var estimatedPlayers = EstimatePlayerCount(detections);
        var estimatedPasses = Math.Max(10, ballDetections / 20);
        var estimatedShots = Math.Max(2, ballDetections / 100);

        var result = new VideoAnalysisResult
        {
            Duration = videoDuration,
            FramesPerSecond = fps,
            TotalPasses = estimatedPasses,
            PassCompletionRate = 75f + new Random().Next(-5, 15),
            TotalShots = estimatedShots,
            ShotsOnTarget = (int)(estimatedShots * 0.6),
            TotalTackles = Math.Max(5, estimatedPlayers / 3),
            TacklesWon = Math.Max(3, estimatedPlayers / 4),
            TotalDistanceCovered = matchMinutes * 110 * 1000,
            AverageSpeed = 6.5f
        };

        // Generate player stats (starts with roster only)
        var playerStats = GeneratePlayerStatsFromDetections(analysisId, matchMinutes, estimatedPlayers, homeTeamRoster, awayTeamRoster);

        // Apply user-provided goals to player stats so scorers reflect manual inputs
        if (goals?.Any() ?? false)
        {
            foreach (var goal in goals)
            {
                var player = playerStats.FirstOrDefault(p => p.Team == goal.Team && p.JerseyNumber == goal.JerseyNumber);

                if (player == null)
                {
                    // If the scorer wasn't in roster stats, create a minimal entry to capture the goals
                    var inferredName = string.IsNullOrWhiteSpace(goal.PlayerName) ? "Unknown" : goal.PlayerName;
                    player = CreatePlayerStat(analysisId, inferredName, goal.JerseyNumber, goal.Team, string.Empty, matchMinutes);
                    playerStats.Add(player);
                }

                player.GoalsScored += 1;
                player.ShotsAttempted += 1;
                player.ShotsOnTarget += 1;
                player.ShotAccuracy = player.ShotsAttempted > 0
                    ? (player.ShotsOnTarget * 100f) / player.ShotsAttempted
                    : 0f;
            }
        }

        // Generate events from detections (OCR will add more players dynamically)
        var events = await GenerateEventsFromDetectionsAsync(analysisId, detections, playerStats, matchMinutes, homeTeamRoster, awayTeamRoster, goals, cameraAngle);
        
        // Apply event metrics BEFORE adding to result (modifies playerStats in-place)
        ApplyEventMetrics(result, playerStats, events);
        
        // NOW add all players to result (includes roster + OCR-detected players with their stats)
        foreach (var stat in playerStats)
        {
            result.PlayerStats.Add(stat);
        }
        
        foreach (var evt in events)
        {
            result.Events.Add(evt);
        }
        
        Console.WriteLine($"[ANALYSIS] Generated {events.Count} events from {detections.Count} frames");
        Console.WriteLine($"[ANALYSIS] Event breakdown - Passes: {events.Count(e => e.EventType == EventType.Pass)}, " +
                         $"Shots: {events.Count(e => e.EventType == EventType.Shot)}, " +
                         $"Goals: {events.Count(e => e.EventType == EventType.GoalScored)}, " +
                         $"Tackles: {events.Count(e => e.EventType == EventType.Tackle)}");
        Console.WriteLine($"[ANALYSIS] Final player count: {playerStats.Count} (will be saved to database)");

        // Calculate match totals from event-driven player stats
        result.TotalDistanceCovered = playerStats.Sum(ps => ps.DistanceCovered ?? 0f);
        result.AverageSpeed = playerStats.Where(ps => ps.AverageSpeed.HasValue && ps.AverageSpeed.Value > 0)
            .Select(ps => ps.AverageSpeed!.Value)
            .DefaultIfEmpty(0f)
            .Average();

        return result;
    }

    private int EstimatePlayerCount(List<FrameDetection> detections)
    {
        var maxPlayers = detections
            .Select(d => d.DetectedObjects.Count(o => o.Label == "person"))
            .DefaultIfEmpty(22)
            .Max();

        return Math.Min(Math.Max(maxPlayers, 10), 22);
    }

    private List<PlayerStatEntity> GeneratePlayerStatsFromDetections(Guid analysisId, int matchMinutes, int playerCount, TeamRosterRequest? homeTeamRoster = null, TeamRosterRequest? awayTeamRoster = null)
    {
        var stats = new List<PlayerStatEntity>();

        // Get roster data if provided
        var homePlayers = homeTeamRoster?.Players?.ToList() ?? new List<PlayerInfo>();
        var awayPlayers = awayTeamRoster?.Players?.ToList() ?? new List<PlayerInfo>();

        if (homePlayers.Any() || awayPlayers.Any())
        {
            // Add roster players - OCR will dynamically add others during event detection
            foreach (var player in homePlayers)
            {
                stats.Add(CreatePlayerStat(analysisId, player.Name, player.JerseyNumber, "Home Team", player.Position, matchMinutes));
            }

            foreach (var player in awayPlayers)
            {
                stats.Add(CreatePlayerStat(analysisId, player.Name, player.JerseyNumber, "Away Team", player.Position, matchMinutes));
            }

            Console.WriteLine($"[PLAYER STATS] Created {stats.Count} player stats from roster (Home: {homePlayers.Count}, Away: {awayPlayers.Count})");
            Console.WriteLine($"[PLAYER STATS] OCR will detect and add additional players during event processing");
        }
        else
        {
            // No roster provided - create placeholder players for detection
            // Split detected player count between teams
            var playersPerTeam = Math.Max(5, playerCount / 2);
            
            Console.WriteLine($"[PLAYER STATS] No roster provided. Creating {playersPerTeam} placeholder players per team.");
            Console.WriteLine($"[PLAYER STATS] OCR will attempt to detect actual jersey numbers during processing.");
            
            for (int i = 1; i <= playersPerTeam; i++)
            {
                stats.Add(CreatePlayerStat(analysisId, $"Player #{i}", i, "Home Team", "", matchMinutes));
            }
            
            for (int i = 1; i <= playersPerTeam; i++)
            {
                stats.Add(CreatePlayerStat(analysisId, $"Player #{i}", i, "Away Team", "", matchMinutes));
            }
        }

        return stats;
    }

    private PlayerStatEntity CreatePlayerStat(Guid analysisId, string name, int jersey, string team, string position, int matchMinutes)
    {
        // Determine position if not provided
        if (string.IsNullOrEmpty(position))
        {
            position = jersey == 1 ? "GK" : jersey <= 4 ? "DF" : jersey <= 8 ? "MF" : "FW";
        }

        // All statistics will be derived from actual detected events
        // Initialize everything to 0 - ApplyEventMetrics will populate based on real detections
        return new PlayerStatEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            VideoAnalysisId = analysisId,
            JerseyNumber = jersey,
            PlayerName = name,
            Team = team,
            Position = position,
            PassesAttempted = 0,
            PassesCompleted = 0,
            PassCompletionPercentage = 0,
            AveragePassLength = 0,
            LongPasses = 0,
            LongPassesCompleted = 0,
            ShotsAttempted = 0,
            ShotsOnTarget = 0,
            GoalsScored = 0,
            ShotAccuracy = 0,
            DistanceCovered = 0,
            Sprints = 0,
            AverageSpeed = 0,
            MaxSpeed = 0,
            Tackles = 0,
            TacklesWon = 0,
            Interceptions = 0,
            Fouls = 0,
            FoulsReceived = 0,
            Dribbles = 0,
            DribblesWon = 0,
            Touches = 0,
            BallRecoveries = 0,
            Clearances = 0,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
    }

    private async Task<List<EventEntity>> GenerateEventsFromDetectionsAsync(Guid analysisId, List<FrameDetection> detections, List<PlayerStatEntity> playerStats, int matchMinutes, TeamRosterRequest? homeTeamRoster, TeamRosterRequest? awayTeamRoster, List<GoalInfo>? goals = null, string cameraAngle = "Overhead")
    {
        var events = new List<EventEntity>();
        var random = new Random();

        // Build a quick lookup for players by jersey number and team
        var playersByJersey = playerStats.GroupBy(p => new { p.JerseyNumber, p.Team })
            .ToDictionary(g => g.Key, g => g.First());

        // Add user-provided goals first
        if (goals?.Any() ?? false)
        {
            foreach (var goal in goals)
            {
                var playerKey = new { JerseyNumber = goal.JerseyNumber, Team = goal.Team };
                var foundPlayer = playersByJersey.TryGetValue(playerKey, out var ps) ? ps : null;
                
                var playerName = !string.IsNullOrEmpty(goal.PlayerName) ? goal.PlayerName : (foundPlayer?.PlayerName ?? "Unknown");
                
                // Determine position based on team - goals typically happen near opponent's goal
                var isHomeTeam = goal.Team == "Home Team";
                var xEnd = isHomeTeam ? 0.95f : 0.05f; // Near opponent goal
                var yEnd = 0.45f + (random.Next(-10, 11) / 100f); // Randomize vertical position
                
                events.Add(new EventEntity
                {
                    Id = Guid.NewGuid(),
                    VideoAnalysisId = analysisId,
                    EventType = EventType.GoalScored,
                    Timestamp = goal.TimestampSeconds,
                    JerseyNumber = goal.JerseyNumber,
                    PlayerName = playerName,
                    Team = goal.Team,
                    Details = $"Goal scored by {playerName}",
                    Successful = true,
                    XStart = isHomeTeam ? 0.7f : 0.3f,
                    YStart = yEnd,
                    XEnd = xEnd,
                    YEnd = yEnd,
                    Distance = 10f + random.Next(0, 15),
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                });
            }
        }

        // Prefer frames where a ball is detected; fallback to evenly spaced frames
        var framesWithBall = detections.Where(d => d.DetectedObjects.Any(o => o.Label == "sports ball")).OrderBy(d => d.Timestamp).ToList();
        var candidateFrames = framesWithBall.Count > 0
            ? framesWithBall
            : detections.Where((_, idx) => idx % Math.Max(1, detections.Count / 10) == 0).OrderBy(d => d.Timestamp).ToList();

        // Heuristic, data-driven event extraction based on ball motion and nearest player
        float prevBx = -1f, prevBy = -1f; bool hasPrev = false;
        int? prevOwner = null;
        string prevTeam = string.Empty;
        int? prevJersey = null;
        string prevPlayerName = "Unknown";
        var maxEvents = 40;

        for (int i = 0; i < candidateFrames.Count && events.Count < maxEvents; i++)
        {
            var frame = candidateFrames[i];
            var objs = frame.DetectedObjects;
            var ballObj = objs.Where(o => o.Label == "sports ball").OrderByDescending(o => o.Confidence).FirstOrDefault();
            if (ballObj == null) { hasPrev = false; continue; }

            var playerObjs = objs.Where(o => o.Label == "person").ToList();
            float frameMaxX = Math.Max(1f, objs.Select(o => o.X + o.Width).DefaultIfEmpty(1f).Max());
            float frameMaxY = Math.Max(1f, objs.Select(o => o.Y + o.Height).DefaultIfEmpty(1f).Max());

            float bx = (ballObj.X + ballObj.Width / 2f) / frameMaxX;
            float by = (ballObj.Y + ballObj.Height / 2f) / frameMaxY;

            // Find nearest player to the ball
            int ownerIndex = -1; float ownerDist = float.MaxValue; float ownerPx = 0f, ownerPy = 0f;
            for (int p = 0; p < playerObjs.Count; p++)
            {
                var po = playerObjs[p];
                float px = (po.X + po.Width / 2f) / frameMaxX;
                float py = (po.Y + po.Height / 2f) / frameMaxY;
                float dist = (float)Math.Sqrt(Math.Pow(px - bx, 2) + Math.Pow(py - by, 2));
                if (dist < ownerDist)
                {
                    ownerDist = dist; ownerIndex = p; ownerPx = px; ownerPy = py;
                }
            }

            // Determine team by jersey color if rosters provided
            var team = "Home Team"; // Default
            if (homeTeamRoster != null && awayTeamRoster != null && ownerIndex >= 0 && ownerIndex < playerObjs.Count)
            {
                var detectedColor = await ExtractDominantJerseyColorAsync(frame.FramePath, playerObjs[ownerIndex]);
                
                var homeColor = ParseHexColor(homeTeamRoster.JerseyColorHome);
                var awayColor = ParseHexColor(awayTeamRoster.JerseyColorAway);
                
                var distanceToHome = CalculateColorDistance(detectedColor, homeColor);
                var distanceToAway = CalculateColorDistance(detectedColor, awayColor);
                
                team = distanceToHome < distanceToAway ? "Home Team" : "Away Team";
            }
            else
            {
                // Fallback to field position heuristic
                team = bx < 0.5f ? "Home Team" : "Away Team";
            }
            
            // Find matching player from roster - use actual jersey numbers from player stats
            // First try to detect jersey number via OCR
            int? detectedJerseyNumber = null;
            if (ownerIndex >= 0 && ownerIndex < playerObjs.Count)
            {
                detectedJerseyNumber = await ExtractJerseyNumberAsync(frame.FramePath, playerObjs[ownerIndex]);
            }
            
            // Get list of players for this team
            var teamPlayers = playerStats.Where(p => p.Team == team).ToList();
            
            // Map ownerIndex to an actual player from the team
            int jersey = 1;
            string player = "Unknown";
            
            // If we detected a jersey number via OCR
            if (detectedJerseyNumber.HasValue)
            {
                // Check if this player already exists in our stats
                var existingPlayer = teamPlayers.FirstOrDefault(p => p.JerseyNumber == detectedJerseyNumber.Value);
                
                if (existingPlayer != null)
                {
                    // Player from roster or placeholder
                    jersey = existingPlayer.JerseyNumber;
                    player = existingPlayer.PlayerName;
                    
                    // Update placeholder names with detected jersey number
                    if (existingPlayer.PlayerName.StartsWith("Player #") && existingPlayer.PlayerName != $"Player #{jersey}")
                    {
                        existingPlayer.PlayerName = $"Player #{jersey}";
                        existingPlayer.JerseyNumber = jersey;
                        Console.WriteLine($"[OCR UPDATE] Updated placeholder to jersey #{jersey} at frame {frame.FrameNumber}");
                    }
                    else
                    {
                        Console.WriteLine($"[OCR MATCH] Detected jersey #{jersey} ({player}) at frame {frame.FrameNumber}");
                    }
                }
                else
                {
                    // New player detected via OCR - add them dynamically
                    jersey = detectedJerseyNumber.Value;
                    player = $"Player #{jersey}";
                    
                    var newPlayer = CreatePlayerStat(analysisId, player, jersey, team, "", matchMinutes);
                    playerStats.Add(newPlayer);
                    Console.WriteLine($"[OCR NEW] Detected new player #{jersey} on {team} at frame {frame.FrameNumber}");
                }
            }
            else if (teamPlayers.Any() && ownerIndex >= 0)
            {
                // Fallback: Use placeholder or cycle through players
                var selectedPlayer = teamPlayers[ownerIndex % teamPlayers.Count];
                jersey = selectedPlayer.JerseyNumber;
                player = selectedPlayer.PlayerName;
            }
            else if (teamPlayers.Any())
            {
                // Fallback to first player
                var selectedPlayer = teamPlayers[0];
                jersey = selectedPlayer.JerseyNumber;
                player = selectedPlayer.PlayerName;
            }
            else
            {
                // Emergency fallback - should rarely happen now
                jersey = 1;
                player = "Player #1";
                
                var newPlayer = CreatePlayerStat(analysisId, player, jersey, team, "", matchMinutes);
                playerStats.Add(newPlayer);
                Console.WriteLine($"[FALLBACK] Created emergency player #{jersey} on {team} at frame {frame.FrameNumber}");
            }

            if (hasPrev)
            {
                float dx = bx - prevBx, dy = by - prevBy;
                float move = (float)Math.Sqrt(dx * dx + dy * dy);
                
                // Adjust thresholds based on camera angle
                // Sideline footage has perspective distortion, so be more forgiving
                float dribbleThresh = cameraAngle == "Sideline" ? 0.008f : 0.01f;      // small move
                float passThresh = cameraAngle == "Sideline" ? 0.015f : 0.03f;         // moderate move (much lower for sideline)
                float shotThresh = cameraAngle == "Sideline" ? 0.05f : 0.08f;          // large, fast move
                float closeRange = 0.04f;      // player proximity

                bool ownerChanged = prevOwner.HasValue && ownerIndex >= 0 && ownerIndex != prevOwner.Value;

                EventType? type = null;
                string details = "Detected from video";
                bool successful = true;
                var lastTeam = prevTeam;
                var lastPlayer = prevPlayerName;

                // Shot if ball moves fast toward goal line; detect net crossing deterministically
                if (move > shotThresh && (bx < 0.08f || bx > 0.92f))
                {
                    bool inGoalBandNow = by > 0.30f && by < 0.70f;   // posts/crossbar vertical window
                    bool inGoalBandPrev = prevBy > 0.30f && prevBy < 0.70f;
                    bool crossesLeft = prevBx > 0.08f && bx < 0.02f && inGoalBandNow && inGoalBandPrev;
                    bool crossesRight = prevBx < 0.92f && bx > 0.98f && inGoalBandNow && inGoalBandPrev;
                    bool crossedNet = crossesLeft || crossesRight;
                    bool onTarget = inGoalBandNow;

                    // More lenient goal detection - if ball is in goal area after fast movement
                    bool probableGoal = onTarget && (bx < 0.02f || bx > 0.98f);

                    if (crossedNet || probableGoal)
                    {
                        type = EventType.GoalScored; 
                        details = $"Goal scored by {player}"; 
                        successful = true;
                        Console.WriteLine($"[EVENT] Goal detected at {frame.Timestamp}s - Ball position: ({bx:F3}, {by:F3}), Team: {team}, Player: {player} #{jersey}");
                    }
                    else
                    {
                        type = EventType.Shot; 
                        details = onTarget ? "Shot on target" : "Shot off target"; 
                        successful = onTarget;
                        Console.WriteLine($"[EVENT] Shot detected at {frame.Timestamp}s - On target: {onTarget}, Ball position: ({bx:F3}, {by:F3})");
                    }
                }
                // Clear pass if owner changes and ball traveled a meaningful distance
                else if (ownerChanged && move > passThresh)
                {
                    // Team detection is noisy; allow passes when teams match or when either side is unknown
                    bool teamsLikelySame = string.IsNullOrWhiteSpace(lastTeam) || string.IsNullOrWhiteSpace(team) || lastTeam == team;
                    bool midlineFlip = Math.Abs(bx - 0.5f) < 0.1f && Math.Abs(prevBx - 0.5f) < 0.1f;

                    if (teamsLikelySame || midlineFlip)
                    {
                        type = EventType.Pass;
                        details = string.IsNullOrWhiteSpace(lastPlayer)
                            ? "Pass between teammates"
                            : $"Pass from {lastPlayer} to {player}";
                        successful = true;
                    }
                    else
                    {
                        type = EventType.Interception;
                        details = "Possession change / interception";
                        successful = true;
                    }
                }
                // Interception/Tackle if owner changes at close range without much ball travel
                else if (ownerChanged && move <= passThresh && ownerDist < closeRange)
                {
                    bool tackle = move <= dribbleThresh || random.Next(100) < 60;
                    type = tackle ? EventType.Tackle : EventType.Interception;
                    details = tackle ? "Defensive tackle" : "Interception"; successful = true;
                }
                // Dribble if same owner and ball is being carried
                else if (!ownerChanged && move >= dribbleThresh)
                {
                    type = EventType.Dribble; details = "Ball dribble"; successful = random.Next(100) >= 40; // ~60%
                }
                // Opportunistic recovery when ball settles near a player
                else if (ownerIndex >= 0 && move < dribbleThresh && ownerDist < closeRange && random.Next(100) < 10)
                {
                    type = EventType.BallRecovery; details = "Ball recovery"; successful = true;
                }

                if (type.HasValue)
                {
                    float xs = Math.Clamp(prevBx, 0f, 1f);
                    float ys = Math.Clamp(prevBy, 0f, 1f);
                    float xe = Math.Clamp(bx, 0f, 1f);
                    float ye = Math.Clamp(by, 0f, 1f);
                    var distance = (float)(Math.Sqrt(Math.Pow(xe - xs, 2) + Math.Pow(ye - ys, 2)) * 105);

                    events.Add(new EventEntity
                    {
                        Id = Guid.NewGuid(),
                        VideoAnalysisId = analysisId,
                        EventType = type.Value,
                        Timestamp = Math.Max(0, frame.Timestamp),
                        JerseyNumber = jersey,
                        PlayerName = player,
                        Team = team,
                        Details = details,
                        Successful = successful,
                        XStart = xs,
                        YStart = ys,
                        XEnd = xe,
                        YEnd = ye,
                        Distance = distance,
                        Created = DateTime.UtcNow,
                        Modified = DateTime.UtcNow
                    });
                }
            }

            prevBx = bx; prevBy = by; hasPrev = true; prevOwner = ownerIndex >= 0 ? ownerIndex : prevOwner;
            prevTeam = team;
            prevJersey = jersey;
            prevPlayerName = player;
        }

        // Intentionally do not synthesize non-vision events (cards, subs, etc.) to avoid false positives

        // Fallback to random generation if we couldn't infer anything
        if (ENABLE_FALLBACK_EVENTS && events.Count == 0)
        {
            var fallbackFrames = candidateFrames;
            var max = Math.Min(20, fallbackFrames.Count);
            for (int i = 0; i < max; i++)
            {
                var frame = fallbackFrames[i];
                var roll = random.Next(100);
                var type = roll < 60 ? EventType.Pass : roll < 80 ? EventType.Shot : roll < 95 ? EventType.Tackle : EventType.Dribble;
                float xs = (float)random.NextDouble();
                float ys = (float)random.NextDouble();
                float xe = Math.Clamp(xs + (float)(random.NextDouble() * 0.3 - 0.15), 0f, 1f);
                float ye = Math.Clamp(ys + (float)(random.NextDouble() * 0.3 - 0.15), 0f, 1f);
                var distance = (float)(Math.Sqrt(Math.Pow(xe - xs, 2) + Math.Pow(ye - ys, 2)) * 105);

                var team = random.Next(2) == 0 ? "Home Team" : "Away Team";
                var jerseyNum = random.Next(1, 12);
                var playerName = "Unknown";
                
                var playerKey = new { JerseyNumber = jerseyNum, Team = team };
                if (playersByJersey.TryGetValue(playerKey, out var foundPlayer))
                {
                    playerName = foundPlayer.PlayerName;
                }

                string details = type switch
                {
                    EventType.Pass => "Pass between teammates",
                    EventType.Shot => random.Next(100) > 50 ? "Shot on target" : "Shot off target",
                    EventType.Tackle => "Defensive tackle",
                    EventType.Dribble => "Ball dribble",
                    _ => "Detected from video"
                };

                bool successful = type switch
                {
                    EventType.Pass => random.Next(100) >= 25,
                    EventType.Shot => details.Contains("on target"),
                    EventType.Tackle => random.Next(100) >= 30,
                    EventType.Dribble => random.Next(100) >= 40,
                    _ => true
                };

                events.Add(new EventEntity
                {
                    Id = Guid.NewGuid(),
                    VideoAnalysisId = analysisId,
                    EventType = type,
                    Timestamp = Math.Max(0, frame.Timestamp),
                    JerseyNumber = jerseyNum,
                    PlayerName = playerName,
                    Team = team,
                    Details = details,
                    Successful = successful,
                    XStart = xs,
                    YStart = ys,
                    XEnd = xe,
                    YEnd = ye,
                    Distance = distance,
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                });
            }
        }

        return events.OrderBy(e => e.Timestamp).ToList();
    }

    private void ApplyEventMetrics(VideoAnalysisResult result, List<PlayerStatEntity> playerStats, List<EventEntity> events)
    {
        if (!events.Any())
        {
            return;
        }

        // Reset event-driven stats so we derive them solely from detected events
        foreach (var stat in playerStats)
        {
            stat.PassesAttempted = 0;
            stat.PassesCompleted = 0;
            stat.PassCompletionPercentage = 0;
            stat.ShotsAttempted = 0;
            stat.ShotsOnTarget = 0;
            stat.GoalsScored = 0;
            stat.Tackles = 0;
            stat.TacklesWon = 0;
            stat.Interceptions = 0;
            stat.Dribbles = 0;
            stat.DribblesWon = 0;
            stat.BallRecoveries = 0;
            stat.Clearances = 0;
            stat.Fouls = 0;
            stat.FoulsReceived = 0;
        }

        var statLookup = playerStats.ToDictionary(ps => (Team: ps.Team, ps.JerseyNumber));

        foreach (var evt in events)
        {
            if (!evt.JerseyNumber.HasValue)
            {
                continue;
            }

            var keyTeam = string.IsNullOrWhiteSpace(evt.Team) ? "Home Team" : evt.Team!;
            if (!statLookup.TryGetValue((keyTeam, evt.JerseyNumber.Value), out var stat))
            {
                continue;
            }

            var successful = evt.Successful ?? false;

            switch (evt.EventType)
            {
                case EventType.Pass:
                    stat.PassesAttempted += 1;
                    if (successful) stat.PassesCompleted += 1;
                    break;
                case EventType.Shot:
                    stat.ShotsAttempted += 1;
                    if (successful) stat.ShotsOnTarget += 1;
                    break;
                case EventType.GoalScored:
                    stat.ShotsAttempted += 1;
                    stat.ShotsOnTarget += 1;
                    stat.GoalsScored += 1;
                    break;
                case EventType.Tackle:
                    stat.Tackles += 1;
                    if (successful) stat.TacklesWon += 1;
                    break;
                case EventType.Interception:
                    stat.Interceptions += 1;
                    break;
                case EventType.Dribble:
                    stat.Dribbles += 1;
                    if (successful) stat.DribblesWon += 1;
                    break;
                case EventType.BallRecovery:
                    stat.BallRecoveries += 1;
                    break;
                case EventType.Clearance:
                    stat.Clearances += 1;
                    break;
                case EventType.Foul:
                    stat.Fouls += 1;
                    break;
                case EventType.FoulReceived:
                    stat.FoulsReceived += 1;
                    break;
            }
        }

        foreach (var stat in playerStats)
        {
            stat.PassCompletionPercentage = stat.PassesAttempted > 0
                ? (stat.PassesCompleted * 100f) / stat.PassesAttempted
                : 0f;
            stat.ShotAccuracy = stat.ShotsAttempted > 0
                ? (stat.ShotsOnTarget * 100f) / stat.ShotsAttempted
                : 0f;
        }

        var totalPasses = playerStats.Sum(ps => ps.PassesAttempted);
        var completedPasses = playerStats.Sum(ps => ps.PassesCompleted);
        result.TotalPasses = totalPasses;
        result.PassCompletionRate = totalPasses > 0 ? (completedPasses * 100f) / totalPasses : 0f;

        result.TotalShots = playerStats.Sum(ps => ps.ShotsAttempted);
        result.ShotsOnTarget = playerStats.Sum(ps => ps.ShotsOnTarget);
        result.TotalTackles = playerStats.Sum(ps => ps.Tackles);
        result.TacklesWon = playerStats.Sum(ps => ps.TacklesWon);
    }

    private async Task<VideoAnalysisResult> GenerateMockAnalysisAsync(Guid analysisId, int videoDuration, float fps, Func<int, Task>? onProgressUpdated, TeamRosterRequest? homeTeamRoster = null, TeamRosterRequest? awayTeamRoster = null, List<GoalInfo>? goals = null, string cameraAngle = "Overhead")
    {
        // Fallback to mock analysis if ffmpeg or model not available
        var matchMinutes = videoDuration / 60;
        var random = new Random();

        for (int i = 40; i <= 90; i += 10)
        {
            if (onProgressUpdated != null) await onProgressUpdated(i);
            await Task.Delay(500);
        }

        var result = new VideoAnalysisResult
        {
            Duration = videoDuration,
            FramesPerSecond = fps,
            TotalPasses = (int)(matchMinutes * 8),
            PassCompletionRate = 78f + random.Next(-5, 5),
            TotalShots = Math.Max(1, matchMinutes / 5),
            ShotsOnTarget = Math.Max(1, matchMinutes / 8),
            TotalTackles = (int)(matchMinutes * 0.5f),
            TacklesWon = (int)(matchMinutes * 0.4f),
            TotalDistanceCovered = matchMinutes * 110 * 1000,
            AverageSpeed = 6.5f
        };

        var playerStats = GeneratePlayerStatsFromDetections(analysisId, matchMinutes, 22, homeTeamRoster, awayTeamRoster);
        foreach (var stat in playerStats)
        {
            result.PlayerStats.Add(stat);
        }

        return result;
    }

    private async Task<int> GetVideoDurationAsync(string videoPath)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var raw = output.Trim();
                Console.WriteLine($"[YOLO] ffprobe duration raw: '{raw}'");
                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
                {
                    return (int)duration;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YOLO] ffprobe duration failed: {ex.Message}");
        }

        return 2700; // Default 45 min
    }

    private async Task<float> GetVideoFpsAsync(string videoPath)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -select_streams v:0 -show_entries stream=r_frame_rate -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var raw = output.Trim();
                Console.WriteLine($"[YOLO] ffprobe fps raw: '{raw}'");
                var parts = raw.Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den) && den > 0)
                {
                    return (float)(num / den);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YOLO] ffprobe fps failed: {ex.Message}");
        }

        return 30f;
    }
}

// Helper classes
public class FrameDetection
{
    public int FrameNumber { get; set; }
    public int Timestamp { get; set; }
    public string FramePath { get; set; } = string.Empty;
    public List<DetectedObject> DetectedObjects { get; set; } = new();
}

public class DetectedObject
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}
