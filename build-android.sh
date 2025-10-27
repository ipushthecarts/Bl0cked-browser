#!/bin/bash

echo "Building Bl0cked Android App..."
echo ""

cd android-app

# Check for google-services.json
if [ ! -f "app/google-services.json" ]; then
    echo "⚠ Warning: google-services.json not found!"
    echo "You need to set up Firebase first."
    echo "See: ../shared/firebase-config/FIREBASE_SETUP.md"
    exit 1
fi

chmod +x gradlew
./gradlew clean
./gradlew assembleDebug

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ Build successful!"
    echo "APK location: app/build/outputs/apk/debug/app-debug.apk"
    echo ""
    echo "To install on device:"
    echo "  adb install app/build/outputs/apk/debug/app-debug.apk"
else
    echo ""
    echo "✗ Build failed!"
    exit 1
fi
