#!/usr/bin/env python3
"""
Converts YOLOv8 PyTorch model to ONNX format for use with ONNX Runtime.
Run this script to convert yolo26n.pt to yolo26n.onnx
"""

import os
import sys

def main():
    try:
        from ultralytics import YOLO
    except ImportError:
        print("ERROR: ultralytics not installed")
        print("Please run: pip install ultralytics")
        sys.exit(1)

    # Model paths
    model_dir = os.path.join(os.path.dirname(__file__), "BoxToBox.API", "Models")
    pt_model = os.path.join(model_dir, "yolo26n.pt")
    
    if not os.path.exists(pt_model):
        print(f"ERROR: Model file not found at {pt_model}")
        sys.exit(1)
    
    print(f"Converting PyTorch model: {pt_model}")
    print("This may take a few minutes...")
    
    try:
        # Load the PyTorch model
        model = YOLO(pt_model)
        
        # Export to ONNX format
        export_path = model.export(format="onnx", imgsz=640)
        
        print(f"\n✓ Conversion successful!")
        print(f"✓ ONNX model saved to: {export_path}")
        print(f"\nThe application can now use the ONNX model for real-time video analysis.")
        
    except Exception as e:
        print(f"\nERROR during conversion: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
