@echo off
REM Convert YOLOv8 PyTorch model to ONNX format
REM Run this script to convert yolo26n.pt to yolo26n.onnx

echo Checking for Python...
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python is not installed or not in PATH
    echo Please install Python from https://www.python.org/
    pause
    exit /b 1
)

echo Checking for ultralytics package...
python -c "import ultralytics" >nul 2>&1
if errorlevel 1 (
    echo Installing ultralytics...
    pip install ultralytics
    if errorlevel 1 (
        echo ERROR: Failed to install ultralytics
        pause
        exit /b 1
    )
)

echo.
echo Starting model conversion...
echo This may take a few minutes...
echo.

python "%~dp0convert_model.py"

if errorlevel 1 (
    echo.
    echo Conversion failed. Please check the error messages above.
    pause
    exit /b 1
)

echo.
echo Conversion complete! You can now run your application.
pause
