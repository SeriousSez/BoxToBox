using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BoxToBox.ApplicationService.Services
{
    /// <summary>
    /// Jersey number recognition using custom trained ONNX model.
    /// Replaces Tesseract OCR with a more accurate CNN-based approach.
    /// </summary>
    public class JerseyNumberRecognizer : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _modelPath;
        private bool _disposed = false;

        public JerseyNumberRecognizer(string modelPath)
        {
            _modelPath = modelPath;
            
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Jersey number model not found at {modelPath}");
            }

            try
            {
                _session = new InferenceSession(modelPath);
                Console.WriteLine($"[JerseyRecognizer] Loaded model from {modelPath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load jersey number model: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Detect jersey number from a cropped player region.
        /// </summary>
        /// <param name="framePath">Path to the video frame</param>
        /// <param name="playerBox">Bounding box of detected player</param>
        /// <param name="confidenceThreshold">Minimum confidence to return prediction (0-1)</param>
        /// <returns>Detected jersey number (0-99) or null if not confident</returns>
        public async Task<int?> DetectJerseyNumberAsync(string framePath, (float X, float Y, float Width, float Height) playerBox, float confidenceThreshold = 0.7f)
        {
            try
            {
                using var image = await Image.LoadAsync<Rgb24>(framePath);
                
                // Extract jersey region from player bounding box
                var jerseyRegion = ExtractJerseyRegion(image, playerBox);
                if (jerseyRegion == null)
                {
                    return null;
                }

                using (jerseyRegion)
                {
                    // Preprocess for model input
                    var inputTensor = PreprocessImage(jerseyRegion);

                    // Run inference
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("input", inputTensor)
                    };

                    using var results = _session.Run(inputs);
                    var output = results.First().AsEnumerable<float>().ToArray();

                    // Get prediction with confidence
                    var (predictedNumber, confidence) = GetTopPrediction(output);

                    if (confidence >= confidenceThreshold)
                    {
                        Console.WriteLine($"[JerseyRecognizer] Detected #{predictedNumber} (confidence: {confidence:F2})");
                        return predictedNumber;
                    }
                    else
                    {
                        Console.WriteLine($"[JerseyRecognizer] Low confidence: {confidence:F2} for #{predictedNumber}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JerseyRecognizer] Detection failed: {ex.Message}");
                return null;
            }
        }

        private Image<Rgb24>? ExtractJerseyRegion(Image<Rgb24> image, (float X, float Y, float Width, float Height) playerBox)
        {
            try
            {
                // Calculate jersey region (torso area where number is visible)
                int x = Math.Max(0, (int)playerBox.X);
                int y = Math.Max(0, (int)playerBox.Y);
                int width = Math.Min((int)playerBox.Width, image.Width - x);
                int height = Math.Min((int)playerBox.Height, image.Height - y);

                // Focus on torso center (where jersey numbers typically appear)
                int jerseyWidth = (int)(width * 0.5);   // Center 50%
                int jerseyHeight = (int)(height * 0.4); // Upper-middle 40%
                int jerseyX = x + (width - jerseyWidth) / 2;
                int jerseyY = y + (int)(height * 0.15); // Below head

                // Validate bounds
                if (jerseyX < 0 || jerseyY < 0 ||
                    jerseyX + jerseyWidth > image.Width ||
                    jerseyY + jerseyHeight > image.Height ||
                    jerseyWidth < 20 || jerseyHeight < 20)
                {
                    return null;
                }

                // Extract and enhance the region
                var jerseyRegion = image.Clone(ctx => ctx.Crop(new Rectangle(jerseyX, jerseyY, jerseyWidth, jerseyHeight)));
                
                // Apply CLAHE-like enhancement
                jerseyRegion.Mutate(x => x
                    .Resize(128, 192)  // Model input size: 128x192
                    .GaussianBlur(0.5f) // Slight blur to reduce noise
                );

                return jerseyRegion;
            }
            catch
            {
                return null;
            }
        }

        private DenseTensor<float> PreprocessImage(Image<Rgb24> image)
        {
            // Resize to model input size (already done in ExtractJerseyRegion, but ensure)
            if (image.Width != 128 || image.Height != 192)
            {
                image.Mutate(x => x.Resize(128, 192));
            }

            // Create tensor: [batch_size, channels, height, width] = [1, 3, 192, 128]
            var tensor = new DenseTensor<float>(new[] { 1, 3, 192, 128 });

            // ImageNet normalization values
            var mean = new[] { 0.485f, 0.456f, 0.406f };
            var std = new[] { 0.229f, 0.224f, 0.225f };

            // Convert image to tensor with normalization
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        var pixel = pixelRow[x];
                        
                        // Normalize each channel
                        tensor[0, 0, y, x] = (pixel.R / 255f - mean[0]) / std[0]; // R
                        tensor[0, 1, y, x] = (pixel.G / 255f - mean[1]) / std[1]; // G
                        tensor[0, 2, y, x] = (pixel.B / 255f - mean[2]) / std[2]; // B
                    }
                }
            });

            return tensor;
        }

        private (int Number, float Confidence) GetTopPrediction(float[] logits)
        {
            // Apply softmax to get probabilities
            var max = logits.Max();
            var exp = logits.Select(x => Math.Exp(x - max)).ToArray();
            var sum = exp.Sum();
            var probabilities = exp.Select(x => (float)(x / sum)).ToArray();

            // Get top prediction
            var maxIndex = 0;
            var maxProb = probabilities[0];
            
            for (int i = 1; i < probabilities.Length; i++)
            {
                if (probabilities[i] > maxProb)
                {
                    maxProb = probabilities[i];
                    maxIndex = i;
                }
            }

            return (maxIndex, maxProb);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
        }
    }
}
