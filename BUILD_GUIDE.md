# Bl0cked - Complete Build Guide

This guide walks you through compiling both the Windows Service (.exe) and Android App (.apk) from source.

## Prerequisites

### For Windows Service:
- **Windows 10/11** (or Windows on VM)
- **Visual Studio 2022** Community Edition (free) OR **.NET 6.0 SDK**
- **Administrator rights**

### For Android App:
- **Android Studio** (latest stable version)
- **JDK 11 or higher**
- At least **8GB RAM** recommended

---

## Part 1: Build Windows Service (.exe)

### Option A: Using Visual Studio (Recommended)

1. **Install Visual Studio 2022 Community**
   - Download from: https://visualstudio.microsoft.com/downloads/
   - During installation, select:
     - ".NET desktop development" workload
     - "Windows Forms" components

2. **Open the Solution**
   ```
   - Open Visual Studio 2022
   - File → Open → Project/Solution
   - Navigate to: Bl0cked-browser/windows-service/Bl0ckedService.sln
   - Click "Open"
   ```

3. **Restore NuGet Packages**
   ```
   - Right-click on solution in Solution Explorer
   - Select "Restore NuGet Packages"
   - Wait for packages to download
   ```

4. **Build the Project**
   ```
   - Set configuration to "Release" (dropdown at top)
   - Build → Build Solution (or press Ctrl+Shift+B)
   - Wait for build to complete
   ```

5. **Locate the Output**
   ```
   The compiled .exe will be at:
   Bl0cked-browser/windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/Bl0ckedService.exe

   Along with required DLLs in the same directory.
   ```

### Option B: Using .NET CLI (Command Line)

1. **Install .NET 6.0 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/6.0
   - Install the SDK (not just runtime)

2. **Open Command Prompt as Administrator**

3. **Navigate to Project Directory**
   ```cmd
   cd C:\path\to\Bl0cked-browser\windows-service
   ```

4. **Restore Dependencies**
   ```cmd
   dotnet restore
   ```

5. **Build Release Version**
   ```cmd
   dotnet publish -c Release -r win-x64 --self-contained
   ```

6. **Locate the Output**
   ```
   The compiled .exe will be at:
   windows-service/Bl0ckedService/bin/Release/net6.0-windows/win-x64/publish/Bl0ckedService.exe

   This folder contains everything needed to run the service.
   ```

### Common Build Errors:

**Error: "SDK not found"**
- Install .NET 6.0 SDK from Microsoft
- Restart Visual Studio

**Error: "NuGet package restore failed"**
- Check internet connection
- Try: Tools → Options → NuGet Package Manager → Clear All NuGet Cache(s)

**Error: "Windows Forms not available"**
- Make sure you selected the right workload in Visual Studio installer
- Modify installation to add ".NET desktop development"

---

## Part 2: Build Android App (.apk)

### Step 1: Install Android Studio

1. **Download Android Studio**
   - Get from: https://developer.android.com/studio
   - Install with default settings

2. **Launch Android Studio**
   - Complete initial setup wizard
   - Install recommended SDK packages

### Step 2: Set Up Firebase (Required Before Build)

**IMPORTANT:** The app REQUIRES `google-services.json` to compile.

1. **Follow Firebase Setup**
   - See: `shared/firebase-config/FIREBASE_SETUP.md`
   - Create Firebase project
   - Add Android app to Firebase project
   - Download `google-services.json`

2. **Place Firebase Config File**
   ```
   Copy google-services.json to:
   Bl0cked-browser/android-app/app/google-services.json

   This file MUST exist before building!
   ```

### Step 3: Open Project in Android Studio

1. **Open Existing Project**
   ```
   - Launch Android Studio
   - Click "Open"
   - Navigate to: Bl0cked-browser/android-app
   - Click "OK"
   ```

2. **Wait for Gradle Sync**
   ```
   - Android Studio will automatically start syncing
   - This can take 5-10 minutes on first run
   - Watch the progress bar at the bottom
   ```

3. **Install Missing SDK Components**
   ```
   - If prompted about missing SDK versions, click "Install"
   - Accept licenses and wait for installation
   ```

### Step 4: Build the APK

#### Option A: Build Unsigned Debug APK (Fastest)

1. **Build Menu**
   ```
   - Build → Build Bundle(s) / APK(s) → Build APK(s)
   - Wait for build to complete (1-5 minutes)
   ```

2. **Locate the APK**
   ```
   Output location:
   android-app/app/build/outputs/apk/debug/app-debug.apk

   This APK can be installed for testing.
   ```

#### Option B: Build Signed Release APK (Production)

1. **Generate Signing Key** (one-time)
   ```
   - Build → Generate Signed Bundle / APK
   - Select "APK" → Next
   - Click "Create new..." for key store
   - Fill in:
     - Key store path: Choose location (e.g., bl0cked-keystore.jks)
     - Password: Create strong password (SAVE THIS!)
     - Alias: bl0cked
     - Validity: 25 years
     - Certificate info: Fill in details
   - Click OK
   ```

2. **Build Signed APK**
   ```
   - Build → Generate Signed Bundle / APK
   - Select "APK" → Next
   - Choose your key store
   - Enter passwords
   - Select "release" build variant
   - Click Finish
   ```

3. **Locate the APK**
   ```
   Output location:
   android-app/app/build/outputs/apk/release/app-release.apk

   This is the production APK for distribution.
   ```

### Step 5: Build via Command Line (Alternative)

If you prefer command line:

1. **Open Terminal in android-app directory**

2. **Make gradlew executable** (Linux/Mac)
   ```bash
   chmod +x gradlew
   ```

3. **Build Debug APK**
   ```bash
   ./gradlew assembleDebug
   ```

4. **Build Release APK** (unsigned)
   ```bash
   ./gradlew assembleRelease
   ```

5. **Locate Output**
   ```
   Debug: android-app/app/build/outputs/apk/debug/app-debug.apk
   Release: android-app/app/build/outputs/apk/release/app-release-unsigned.apk
   ```

### Common Build Errors:

**Error: "google-services.json missing"**
- You MUST set up Firebase and download google-services.json
- Place it in: android-app/app/google-services.json

**Error: "SDK not found" or "License not accepted"**
- Open Android Studio
- Go to: Tools → SDK Manager
- Install missing SDK versions
- Accept licenses: Tools → SDK Manager → SDK Tools tab

**Error: "Gradle sync failed"**
- Check internet connection
- Try: File → Invalidate Caches / Restart
- Delete .gradle folder and sync again

**Error: "Build tools version not found"**
- Edit android-app/app/build.gradle
- Change compileSdk to available version (check SDK Manager)

**Error: "Out of memory"**
- Increase Gradle memory:
  - Create/edit: android-app/gradle.properties
  - Add: `org.gradle.jvmargs=-Xmx4096m`

---

## Part 3: Post-Build - Creating Distribution Package

### For Windows Service:

1. **Copy all files from output directory:**
   ```
   - Bl0ckedService.exe
   - All .dll files
   - All dependency folders
   ```

2. **Create installation package:**
   ```
   Create a folder structure:
   Bl0cked-Windows-Service/
   ├── Bl0ckedService.exe
   ├── [all DLLs]
   └── INSTALL.txt (installation instructions)
   ```

### For Android App:

1. **The APK is ready to distribute:**
   ```
   - app-debug.apk (for testing)
   - app-release.apk (for production)
   ```

2. **Installation:**
   ```
   - Transfer APK to Android device
   - Enable "Install from Unknown Sources" in Settings
   - Tap APK to install
   ```

---

## Part 4: Quick Build Script

### Windows Service Build Script (build-service.bat)

Create a file `build-service.bat`:
```batch
@echo off
cd windows-service
echo Building Bl0cked Windows Service...
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained
echo.
echo Build complete!
echo Output: windows-service\Bl0ckedService\bin\Release\net6.0-windows\win-x64\publish\
pause
```

### Android App Build Script (build-app.sh)

Create a file `build-app.sh`:
```bash
#!/bin/bash
cd android-app
echo "Building Bl0cked Android App..."
./gradlew clean
./gradlew assembleRelease
echo ""
echo "Build complete!"
echo "Output: android-app/app/build/outputs/apk/release/app-release.apk"
```

Make executable:
```bash
chmod +x build-app.sh
```

---

## Troubleshooting

### General Issues:

**"Build takes forever"**
- First build always takes longest (downloads dependencies)
- Subsequent builds are much faster
- Ensure good internet connection

**"Build fails with cryptic errors"**
- Clean the build:
  - Windows: `dotnet clean`
  - Android: `./gradlew clean`
- Then rebuild

**"Antivirus blocking build"**
- Some antivirus software blocks build tools
- Add project folder to antivirus exceptions

### Getting Help:

If you encounter issues not covered here:
1. Check the error message carefully
2. Google the specific error
3. Check GitHub Issues for similar problems
4. Ensure you followed all steps in order

---

## Verification

### Test Windows Service:
```cmd
# Run as Administrator
Bl0ckedService.exe
# Should start without errors
```

### Test Android App:
```
- Install APK on device
- Open app
- Should show pairing screen
```

---

## Next Steps

After successful build:
1. Follow `shared/docs/USER_SETUP_GUIDE.md` for installation
2. Configure Firebase as per `shared/firebase-config/FIREBASE_SETUP.md`
3. Pair your devices and test the system

---

**Build time estimates:**
- Windows Service: 2-5 minutes
- Android App (first time): 10-15 minutes
- Android App (subsequent): 2-5 minutes
