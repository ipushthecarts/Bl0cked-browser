#!/bin/bash

echo "Building Bl0cked Windows Service..."
echo ""

cd windows-service

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "✗ Error: dotnet not found!"
    echo "Install with: sudo pacman -S dotnet-sdk-6.0"
    exit 1
fi

dotnet clean
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ Build successful!"
    echo "EXE location: Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/Bl0ckedService.exe"
    echo ""
    echo "Copy the entire publish/ folder to your Windows PC"
else
    echo ""
    echo "✗ Build failed!"
    exit 1
fi
