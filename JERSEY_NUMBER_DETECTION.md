# Jersey Number Detection Setup

The system now includes OCR-based jersey number detection using Tesseract.

## Setup Instructions

### 1. Install Tesseract Training Data

Download the English language training data file:

- Go to: https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
- Save the file

### 2. Create tessdata folder

Create the following folder structure in your API project:

```
BoxToBox.API/
  tessdata/
    eng.traineddata
```

### 3. Update Project File

Add this to `BoxToBox.API.csproj`:

```xml
<ItemGroup>
  <None Update="tessdata\eng.traineddata">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 4. Restore NuGet Packages

Run:

```bash
dotnet restore
```

## How It Works

1. **Player Detection**: YOLOv8 detects players in each frame
2. **Jersey Region Extraction**: Extracts the torso region where numbers typically appear
3. **OCR Processing**: Tesseract reads numbers from the jersey (0-99)
4. **Player Matching**: Matches detected numbers to your roster
5. **Event Attribution**: Goals, passes, etc. are attributed to the correct player

## Limitations

- **Accuracy**: ~60-70% in ideal conditions
- **Requirements**:
  - Clear visibility of jersey numbers
  - Good resolution (HD or better recommended)
  - Players facing or partially facing camera
- **Performance**: Adds ~100-200ms per player detection
- **Won't work if**:
  - Player's back is turned away
  - Number is obscured
  - Very low resolution
  - Extreme camera angles

## Fallback Behavior

If OCR fails to detect a number:

1. System uses roster cycling (assigns players round-robin)
2. User-provided rosters are prioritized
3. Manual goal entry is still respected

## Tips for Best Results

1. **Upload HD videos** (1080p or higher)
2. **Provide complete rosters** with all jersey numbers
3. **Manually add goals** for critical moments
4. **Use Sideline camera angle** for better jersey visibility
5. **Process shorter clips** for faster OCR processing
