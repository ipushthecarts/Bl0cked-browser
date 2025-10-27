# Bl0cked - Parental Control Lockdown System

A toggle-able computer lockdown system that enforces chore completion before allowing normal computer use. Designed for parental oversight and responsibility training.

## Overview

Bl0cked is a two-component system:
1. **Windows Service** - Runs on the target PC, displays full-screen lockdown overlay
2. **Android Control App** - Remote control from parent's phone to lock/unlock and manage chores

The system ties computer access to responsibility, creating a digital boundary similar to school management systems but for home use.

## Core Features

### Windows Service (PC Lockdown)
- Full-screen overlay on all monitors when locked
- Blocks keyboard shortcuts (Alt+F4, Ctrl+Alt+Del, Task Manager, etc.)
- Process whitelist - terminates unauthorized applications
- Persistent across reboots - lock state survives PC restarts
- Emergency unlock code for situations when phone unavailable
- Dark theme UI with chore list and timer display

### Android Control App
- **Control Screen:** Lock/unlock toggle with status indicator and timer
- **Chore Manager Screen:** Add, edit, delete chores with real-time sync
- View chore completion status (brother can check off items on PC)
- Dark theme UI matching PC overlay aesthetic
- Dual-mode communication: Local (fast) and Internet (remote)

### Communication System
- **Local Mode (Primary):** Direct HTTP connection when on same WiFi (< 100ms response)
- **Internet Mode (Secondary):** Firebase Realtime Database for remote control from anywhere
- Automatic fallback between modes
- Secure pairing with 6-digit codes and shared secrets
- Real-time synchronization of lock status and chore completions

## Project Structure

```
Bl0cked-browser/
├── windows-service/          # C#/.NET Windows Service
│   ├── Bl0ckedService/
│   │   ├── Program.cs        # Service entry point
│   │   ├── LockdownService.cs # Main orchestration logic
│   │   ├── OverlayWindow.cs   # Full-screen lockdown UI
│   │   ├── StateManager.cs    # Persistent state management
│   │   ├── PairingManager.cs  # Device pairing logic
│   │   ├── KeyboardHook.cs    # Global keyboard hook
│   │   ├── ProcessMonitor.cs  # Process whitelist enforcement
│   │   ├── HttpServer.cs      # Local HTTP REST API
│   │   └── FirebaseClient.cs  # Firebase integration
│   ├── Bl0ckedService.csproj
│   └── Bl0ckedService.sln
│
├── android-app/               # Android control app (Kotlin)
│   ├── app/
│   │   ├── src/main/java/com/bl0cked/
│   │   │   ├── MainActivity.kt        # App entry + navigation
│   │   │   ├── PairingActivity.kt     # Initial pairing screen
│   │   │   ├── ControlFragment.kt     # Lock/unlock control
│   │   │   ├── ChoresFragment.kt      # Chore management
│   │   │   ├── ConnectionManager.kt   # Dual-mode communication
│   │   │   ├── FirebaseService.kt     # Firebase integration
│   │   │   └── models/                # Data models
│   │   └── src/main/res/              # UI layouts and resources
│   ├── build.gradle           # App-level Gradle config
│   └── build.gradle           # Project-level Gradle config
│
└── shared/
    ├── firebase-config/       # Firebase setup documentation
    │   └── FIREBASE_SETUP.md
    └── docs/                  # User documentation
        └── USER_SETUP_GUIDE.md
```

## Technology Stack

**Windows Service:**
- .NET 6.0 (C#)
- Windows Forms for overlay UI
- HttpListener for local REST API
- Firebase Admin SDK for internet mode
- Native Windows APIs for hooks and process control

**Android App:**
- Kotlin
- Material Design Components
- Firebase Realtime Database
- Firebase Cloud Messaging
- OkHttp for local HTTP
- Coroutines for async operations

**Communication:**
- Local: HTTP REST + WebSocket
- Internet: Firebase Realtime Database
- Authentication: Shared secret tokens

## Security Features

- **Device Pairing:** Only paired Android device can control PC
- **Bypass Prevention:** Blocks Task Manager, keyboard shortcuts, unauthorized processes
- **Persistence:** Lock state survives reboots, Safe Mode, user switching
- **Emergency Access:** 8-digit emergency unlock code with attempt limiting
- **Encrypted Storage:** State files encrypted using Windows DPAPI
- **Rate Limiting:** Protection against brute-force attempts
- **Replay Attack Protection:** Timestamp-based command validation

## Quick Start

### Prerequisites

1. **Windows PC:**
   - Windows 10 or 11
   - .NET 6.0 Runtime
   - Admin rights for installation

2. **Android Device:**
   - Android 8.0 (API 26) or later

3. **Firebase Account:**
   - Free tier sufficient
   - See `shared/firebase-config/FIREBASE_SETUP.md`

### Installation

1. **Set up Firebase** (one-time):
   - Follow `shared/firebase-config/FIREBASE_SETUP.md`
   - Download `google-services.json` and `firebase-credentials.json`

2. **Build Windows Service:**
   ```bash
   cd windows-service
   dotnet restore
   dotnet build -c Release
   ```

3. **Install Windows Service:**
   ```cmd
   # Run as Administrator
   sc create Bl0ckedService binPath="C:\Path\To\Bl0ckedService.exe" start=auto
   sc start Bl0ckedService
   ```

4. **Build Android App:**
   ```bash
   cd android-app
   ./gradlew assembleRelease
   # APK at: app/build/outputs/apk/release/app-release.apk
   ```

5. **Pair Devices:**
   - Open Android app
   - Enter 6-digit code shown on PC
   - Enter PC IP address
   - Tap "Pair"

### Usage

**To Lock PC:**
1. Open Android app → Control tab
2. Tap "ACTIVATE LOCK"
3. PC shows lockdown overlay with chore list

**To Unlock PC:**
1. Verify chores completed (Chores tab)
2. Control tab → Tap "DEACTIVATE LOCK"
3. PC overlay disappears

**To Manage Chores:**
1. Chores tab → Tap "+ NEW CHORE"
2. Enter chore description → Add
3. Edit/delete using buttons on each chore

## Documentation

- **Planning Document:** `planning.md` - Complete technical specification
- **Research Notes:** `research.md` - Codebase analysis and findings
- **Firebase Setup:** `shared/firebase-config/FIREBASE_SETUP.md`
- **User Guide:** `shared/docs/USER_SETUP_GUIDE.md`

## Known Limitations

1. **USB Boot Bypass:** Child could boot from USB drive
   - **Mitigation:** Set BIOS password, disable USB boot

2. **Physical Access:** Child could physically damage PC
   - **Mitigation:** Discuss consequences beforehand

3. **Requires Admin:** Service can be uninstalled with admin password
   - **Mitigation:** Don't share admin credentials

4. **Network Dependency:** Internet mode requires active internet on both devices
   - **Mitigation:** Emergency unlock code available

## Development

### Windows Service Development

```bash
cd windows-service
dotnet restore
dotnet build
dotnet run  # For testing
```

### Android App Development

```bash
cd android-app
# Open in Android Studio
# Or use command line:
./gradlew build
./gradlew installDebug
```

## Testing

See `planning.md` section "Testing & Verification" for comprehensive manual testing checklist covering:
- Lock/unlock functionality
- Chore management and sync
- Bypass prevention mechanisms
- Multi-monitor support
- Persistence across reboots
- Emergency unlock
- Communication modes (local/internet)

## Contributing

This project was designed as a parental control system. When contributing:
- Follow existing code patterns
- Maintain security features
- Test bypass prevention thoroughly
- Document any configuration changes
- Respect privacy and ethical considerations

## License

This project is provided as-is for personal use. Ensure compliance with local laws regarding parental control software.

## Disclaimer

This software is designed for responsible parental oversight. Users must:
- Discuss expectations with children beforehand
- Use as a tool for responsibility training, not punishment
- Ensure emergency access methods are available
- Respect privacy and reasonable boundaries
- Comply with local laws and regulations

## Support

For detailed setup instructions, troubleshooting, and usage guidelines, refer to:
- `shared/docs/USER_SETUP_GUIDE.md` - Complete user manual
- `planning.md` - Technical specifications
- `shared/firebase-config/FIREBASE_SETUP.md` - Firebase configuration

---

**Built with responsibility and accountability in mind.**
