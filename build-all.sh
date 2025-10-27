#!/bin/bash

set -e  # Exit on error

echo "================================"
echo "Bl0cked - Arch Linux Build Script"
echo "================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if running on Arch-based system
if ! command -v pacman &> /dev/null; then
    echo -e "${RED}Error: This script is for Arch Linux systems${NC}"
    exit 1
fi

# Function to check if command exists
check_command() {
    if ! command -v $1 &> /dev/null; then
        echo -e "${RED}Error: $1 is not installed${NC}"
        echo "Install with: sudo pacman -S $2"
        exit 1
    fi
}

# Check prerequisites
echo "Checking prerequisites..."
check_command dotnet dotnet-sdk-6.0
check_command java jdk17-openjdk
echo -e "${GREEN}✓ All prerequisites installed${NC}"
echo ""

# Build Windows Service
echo "================================"
echo "Building Windows Service..."
echo "================================"
cd windows-service
dotnet restore
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Windows Service built successfully${NC}"
    echo "Output: windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/"
else
    echo -e "${RED}✗ Windows Service build failed${NC}"
    exit 1
fi
cd ..
echo ""

# Check for google-services.json
if [ ! -f "android-app/app/google-services.json" ]; then
    echo -e "${YELLOW}Warning: google-services.json not found!${NC}"
    echo "Please add Firebase configuration at: android-app/app/google-services.json"
    echo "See: shared/firebase-config/FIREBASE_SETUP.md"
    read -p "Continue anyway? (y/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Build Android App
echo "================================"
echo "Building Android App..."
echo "================================"
cd android-app
chmod +x gradlew
./gradlew clean
./gradlew assembleDebug

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Android App built successfully${NC}"
    echo "Output: android-app/app/build/outputs/apk/debug/app-debug.apk"
else
    echo -e "${RED}✗ Android App build failed${NC}"
    exit 1
fi
cd ..
echo ""

# Summary
echo "================================"
echo "Build Complete!"
echo "================================"
echo ""
echo "Windows Service (.exe):"
echo "  → windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/Bl0ckedService.exe"
echo ""
echo "Android App (.apk):"
echo "  → android-app/app/build/outputs/apk/debug/app-debug.apk"
echo ""
echo "Next steps:"
echo "1. Copy Bl0ckedService.exe to Windows PC"
echo "2. Install app-debug.apk on Android device"
echo "3. Follow setup guide in: shared/docs/USER_SETUP_GUIDE.md"
