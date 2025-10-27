# Bl0cked - Build Guide for Arch Linux (CachyOS)

Complete guide for building both the Windows Service (.exe) and Android App (.apk) on Arch Linux, specifically tested on CachyOS with Cosmic Desktop.

## System Requirements

- **OS:** Arch Linux / CachyOS
- **RAM:** 8GB minimum (16GB recommended for Android build)
- **Storage:** 20GB free space (Android SDK is large)
- **Internet:** Required for downloading dependencies

---

## Part 1: System Setup

### Update System

```bash
# Update system packages
sudo pacman -Syu

# If using CachyOS, ensure cachyos-keyring is updated
sudo pacman -S cachyos-keyring
```

### Install Base Development Tools

```bash
# Install base development packages
sudo pacman -S base-devel git wget curl unzip

# Install AUR helper (yay) if not already installed
sudo pacman -S --needed git base-devel
git clone https://aur.archlinux.org/yay.git
cd yay
makepkg -si
cd ..
rm -rf yay
```

---

## Part 2: Build Windows Service (.exe)

### Step 1: Install .NET SDK

```bash
# Install .NET 6.0 SDK from official repos
sudo pacman -S dotnet-sdk-6.0 dotnet-runtime-6.0 aspnet-runtime-6.0

# Verify installation
dotnet --version
# Should show: 6.0.x or higher
```

### Step 2: Clone/Navigate to Project

```bash
# Navigate to the Bl0cked-browser directory
cd ~/path/to/Bl0cked-browser/windows-service
```

### Step 3: Restore Dependencies

```bash
# Restore NuGet packages
dotnet restore

# If you get SSL/certificate errors:
dotnet nuget locals all --clear
dotnet restore --no-cache
```

### Step 4: Build the Service

```bash
# Build for Windows x64 target
dotnet publish -c Release -r win-x64 --self-contained

# Alternative: Build for Linux (if you want to test locally, though it won't work fully)
# dotnet publish -c Release -r linux-x64 --self-contained
```

### Step 5: Locate Output

```bash
# The compiled Windows executable will be at:
# windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/Bl0ckedService.exe

# List the output directory
ls -lh windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/

# You'll see:
# - Bl0ckedService.exe (main executable)
# - Multiple .dll files (dependencies)
# - All required runtime files
```

### Common Issues on Arch:

**Issue: "dotnet: command not found"**
```bash
# Ensure dotnet is in PATH
echo 'export DOTNET_ROOT=/usr/share/dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$DOTNET_ROOT' >> ~/.bashrc
source ~/.bashrc
```

**Issue: "NuGet package source not found"**
```bash
# Add NuGet sources
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org

# Clear and restore
dotnet nuget locals all --clear
dotnet restore
```

**Issue: "Target framework not found"**
```bash
# Install additional SDK versions if needed
sudo pacman -S dotnet-sdk dotnet-runtime
```

---

## Part 3: Build Android App (.apk)

### Step 1: Install Java Development Kit

```bash
# Install OpenJDK 17 (required for latest Android Studio)
sudo pacman -S jdk17-openjdk

# Set Java version
sudo archlinux-java set java-17-openjdk

# Verify Java installation
java -version
# Should show: openjdk version "17.x.x"
```

### Step 2: Install Android Studio

#### Option A: Using yay (AUR - Recommended)

```bash
# Install Android Studio from AUR
yay -S android-studio

# This will take 10-20 minutes on first install
```

#### Option B: Manual Installation

```bash
# Download Android Studio
cd ~/Downloads
wget https://redirector.gvt1.com/edgedl/android/studio/ide-zips/2023.1.1.28/android-studio-2023.1.1.28-linux.tar.gz

# Extract to /opt
sudo tar -xzf android-studio-*.tar.gz -C /opt/

# Create desktop entry
cat > ~/.local/share/applications/android-studio.desktop << 'EOF'
[Desktop Entry]
Version=1.0
Type=Application
Name=Android Studio
Icon=/opt/android-studio/bin/studio.png
Exec=/opt/android-studio/bin/studio.sh
Categories=Development;IDE;
Terminal=false
EOF

# Make executable
chmod +x ~/.local/share/applications/android-studio.desktop
```

### Step 3: Initial Android Studio Setup

```bash
# Launch Android Studio
android-studio
# OR if manual install:
/opt/android-studio/bin/studio.sh
```

**In Android Studio:**
1. Complete first-run wizard
2. Choose "Standard" installation
3. Accept all licenses
4. Wait for SDK download (this takes 15-30 minutes)
5. Recommended SDK components:
   - Android SDK Platform 34
   - Android SDK Build-Tools 34.0.0
   - Android SDK Platform-Tools
   - Android Emulator (optional)

### Step 4: Set Up Environment Variables

```bash
# Add Android SDK to PATH
echo 'export ANDROID_HOME=$HOME/Android/Sdk' >> ~/.bashrc
echo 'export PATH=$PATH:$ANDROID_HOME/tools' >> ~/.bashrc
echo 'export PATH=$PATH:$ANDROID_HOME/platform-tools' >> ~/.bashrc
echo 'export PATH=$PATH:$ANDROID_HOME/cmdline-tools/latest/bin' >> ~/.bashrc
source ~/.bashrc

# Verify adb is accessible
adb --version
```

### Step 5: Set Up Firebase (REQUIRED)

```bash
# Before building, you MUST have google-services.json
# Follow the Firebase setup guide:
# See: shared/firebase-config/FIREBASE_SETUP.md

# Once you have google-services.json, place it at:
cp /path/to/google-services.json android-app/app/google-services.json

# Verify it's in the right place
ls -l android-app/app/google-services.json
```

### Step 6: Build Using Android Studio GUI

```bash
# Navigate to project and open in Android Studio
cd ~/path/to/Bl0cked-browser/android-app
android-studio . &
```

**In Android Studio:**
1. Wait for Gradle sync to complete (10-15 minutes first time)
2. If prompted about SDK versions, click "Install"
3. Accept any license agreements
4. Once sync complete: **Build → Build Bundle(s) / APK(s) → Build APK(s)**
5. Wait 3-5 minutes for build
6. Click "locate" in the notification popup

**APK Location:**
```
android-app/app/build/outputs/apk/debug/app-debug.apk
```

### Step 7: Build Using Command Line (Alternative)

```bash
# Navigate to android app directory
cd ~/path/to/Bl0cked-browser/android-app

# Make gradlew executable
chmod +x gradlew

# Clean previous builds
./gradlew clean

# Build debug APK
./gradlew assembleDebug

# APK will be at: app/build/outputs/apk/debug/app-debug.apk

# For release APK (unsigned):
./gradlew assembleRelease
# Output: app/build/outputs/apk/release/app-release-unsigned.apk
```

### Common Issues on Arch:

**Issue: "SDK location not found"**
```bash
# Create local.properties file
echo "sdk.dir=$HOME/Android/Sdk" > android-app/local.properties
```

**Issue: "Gradle sync failed"**
```bash
# Clear Gradle cache
rm -rf ~/.gradle/caches/
rm -rf android-app/.gradle/

# Sync again
./gradlew --refresh-dependencies
```

**Issue: "AAPT2 not found"**
```bash
# Install 32-bit libraries (needed for some Android tools)
sudo pacman -S lib32-gcc-libs lib32-glibc
```

**Issue: "License not accepted"**
```bash
# Accept all licenses
yes | $ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager --licenses
```

**Issue: "Out of memory" during build**
```bash
# Increase Gradle memory
echo "org.gradle.jvmargs=-Xmx4096m -XX:MaxMetaspaceSize=512m" >> android-app/gradle.properties

# Also increase swap if needed
sudo dd if=/dev/zero of=/swapfile bs=1M count=4096
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
```

**Issue: "ADB can't detect device"**
```bash
# Add udev rules for Android devices
sudo pacman -S android-udev

# Reload udev rules
sudo udevadm control --reload-rules
sudo udevadm trigger

# Add user to plugdev group
sudo usermod -aG plugdev $USER
# Log out and back in for group changes to take effect
```

---

## Part 4: CachyOS-Specific Optimizations

### Use CachyOS Optimized Packages

```bash
# CachyOS has optimized packages for better performance
# Ensure you're using CachyOS repos for dotnet and JDK

# Check repo configuration
cat /etc/pacman.conf | grep cachyos

# Update with CachyOS optimizations
sudo pacman -S cachyos-optimized-kernel
```

### Optimize Build Performance

```bash
# Use all CPU cores for builds
echo "org.gradle.parallel=true" >> android-app/gradle.properties
echo "org.gradle.workers.max=8" >> android-app/gradle.properties

# Enable Gradle daemon
echo "org.gradle.daemon=true" >> android-app/gradle.properties

# Use ccache for faster rebuilds (dotnet)
sudo pacman -S ccache
```

### Cosmic Desktop Specific

```bash
# If Android Studio has rendering issues with Cosmic:
# Set environment variable before launching
export _JAVA_AWT_WM_NONREPARENTING=1
android-studio

# Add to launcher permanently
# Edit: ~/.local/share/applications/android-studio.desktop
# Change Exec line to:
# Exec=env _JAVA_AWT_WM_NONREPARENTING=1 /opt/android-studio/bin/studio.sh
```

---

## Part 5: Automated Build Scripts

### Complete Build Script for Both Components

Create `build-all.sh`:

```bash
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
```

Make it executable:
```bash
chmod +x build-all.sh
```

Run it:
```bash
./build-all.sh
```

### Quick Android-Only Build Script

Create `build-android.sh`:

```bash
#!/bin/bash
cd android-app
chmod +x gradlew
./gradlew clean assembleDebug
echo ""
echo "APK built at: app/build/outputs/apk/debug/app-debug.apk"
```

### Quick Windows-Only Build Script

Create `build-windows.sh`:

```bash
#!/bin/bash
cd windows-service
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained
echo ""
echo "EXE built at: Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/"
```

---

## Part 6: Testing Builds

### Test Windows Service (Limited on Linux)

```bash
# You can verify the .exe was created
ls -lh windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/Bl0ckedService.exe

# Check dependencies are included
ls windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/ | grep dll

# Note: You cannot fully run the Windows Service on Linux
# It requires Windows-specific APIs (Windows Forms, Service Controller, etc.)
```

### Test Android APK

```bash
# Connect Android device via USB
# Enable USB debugging on device

# Check device is detected
adb devices

# Install APK
adb install android-app/app/build/outputs/apk/debug/app-debug.apk

# Launch app
adb shell am start -n com.bl0cked.app/com.bl0cked.PairingActivity

# View logs
adb logcat | grep Bl0cked
```

---

## Part 7: Packaging for Distribution

### Create Distribution Archive

```bash
# Create output directory
mkdir -p dist

# Copy Windows Service
cp -r windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish dist/Bl0cked-Windows-Service

# Copy Android APK
cp android-app/app/build/outputs/apk/debug/app-debug.apk dist/Bl0cked-Android.apk

# Copy documentation
cp shared/docs/USER_SETUP_GUIDE.md dist/
cp shared/firebase-config/FIREBASE_SETUP.md dist/
cp README.md dist/

# Create archive
cd dist
tar -czf Bl0cked-v1.0.0-linux-build.tar.gz *
cd ..

echo "Distribution package created: dist/Bl0cked-v1.0.0-linux-build.tar.gz"
```

---

## Part 8: Troubleshooting

### Build Logs

```bash
# Save build logs for debugging
./gradlew assembleDebug --info > build.log 2>&1
./gradlew assembleDebug --debug > build-debug.log 2>&1
```

### Clean Everything

```bash
# Complete clean of all build artifacts
rm -rf windows-service/*/bin windows-service/*/obj
rm -rf android-app/.gradle android-app/app/build
rm -rf ~/.gradle/caches/
dotnet nuget locals all --clear
```

### Performance Tips

```bash
# Use tmpfs for gradle builds (faster builds with SSD wear reduction)
sudo mount -t tmpfs -o size=8G tmpfs /tmp/gradle
export GRADLE_USER_HOME=/tmp/gradle
```

### System Resources

```bash
# Monitor resources during build
# Terminal 1: Start build
./build-all.sh

# Terminal 2: Monitor
htop
# or
watch -n 1 'free -h && df -h'
```

---

## Part 9: Development Environment (Optional)

### Install JetBrains Rider (Alternative to Visual Studio)

For C# development on Linux:

```bash
# Install Rider IDE from AUR
yay -S rider

# Or download from JetBrains:
# https://www.jetbrains.com/rider/download/#section=linux
```

### VS Code Setup

```bash
# Install VS Code
yay -s visual-studio-code-bin

# Install extensions
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.vscode-dotnet-runtime
code --install-extension vscjava.vscode-java-pack
code --install-extension vscjava.vscode-gradle
```

---

## Summary

**Build times on CachyOS (estimated):**
- Windows Service: 2-5 minutes
- Android App (first time): 10-20 minutes
- Android App (subsequent): 3-5 minutes

**Storage usage:**
- .NET SDK: ~500MB
- Android SDK: ~10-15GB
- Build artifacts: ~2GB

**Everything works on Arch/CachyOS!** The build process is actually smoother on Linux in many ways compared to Windows.

---

## Quick Reference Commands

```bash
# Full build
./build-all.sh

# Windows Service only
cd windows-service && dotnet publish -c Release -r win-x64 --self-contained

# Android App only
cd android-app && ./gradlew assembleDebug

# Install APK on device
adb install android-app/app/build/outputs/apk/debug/app-debug.apk

# Clean all
rm -rf windows-service/*/bin windows-service/*/obj android-app/.gradle android-app/app/build
```

---

**Next Steps:**
1. Run the build scripts
2. Transfer Windows Service to Windows PC for installation
3. Install Android APK on your phone
4. Follow `USER_SETUP_GUIDE.md` for pairing and setup

**Need help?** Check the main `BUILD_GUIDE.md` for more detailed troubleshooting, or review error messages carefully - most issues are dependency or path related.
