# Bl0cked - User Setup Guide

Complete guide for setting up the Bl0cked Lockdown System.

## System Requirements

### Windows PC (Target Computer)
- **OS:** Windows 10 or Windows 11
- **RAM:** 4GB minimum
- **.NET Runtime:** .NET 6.0 or later
- **Admin Rights:** Required for installation
- **Network:** WiFi or Ethernet connection

### Android Device (Admin Phone)
- **OS:** Android 8.0 (API 26) or later
- **Storage:** 50MB free space
- **Network:** WiFi or cellular data

## Installation Steps

### Part 1: Set Up Firebase (One-Time)

Follow the guide at `shared/firebase-config/FIREBASE_SETUP.md` to:
1. Create Firebase project
2. Configure Realtime Database
3. Download `google-services.json` for Android
4. Download `firebase-credentials.json` for Windows

### Part 2: Build the Windows Service

1. **Open Visual Studio 2022**
   - Open `windows-service/Bl0ckedService.sln`

2. **Restore NuGet Packages**
   - Right-click solution → **Restore NuGet Packages**

3. **Build in Release Mode**
   - Set configuration to **Release**
   - Build → **Build Solution**
   - Executable will be at: `windows-service/Bl0ckedService/bin/Release/net6.0-windows/Bl0ckedService.exe`

4. **Copy to Installation Location**
   ```
   mkdir "C:\Program Files\Bl0cked"
   copy "windows-service\Bl0ckedService\bin\Release\net6.0-windows\*" "C:\Program Files\Bl0cked\"
   ```

5. **Create Data Directory**
   ```
   mkdir "C:\ProgramData\Bl0cked"
   ```

6. **Place Firebase Credentials**
   - Copy `firebase-credentials.json` to `C:\ProgramData\Bl0cked\firebase-credentials.json`

7. **Install as Windows Service**
   - Open **Command Prompt as Administrator**
   ```
   sc create Bl0ckedService binPath="C:\Program Files\Bl0cked\Bl0ckedService.exe" start=auto
   sc description Bl0ckedService "Bl0cked Lockdown Service for parental control"
   sc start Bl0ckedService
   ```

8. **Verify Service is Running**
   - Open **Services** (services.msc)
   - Find "Bl0ckedService"
   - Status should be "Running"

### Part 3: Build the Android App

1. **Open Android Studio**
   - Open `android-app/` directory

2. **Place Firebase Config**
   - Copy `google-services.json` to `android-app/app/google-services.json`

3. **Build APK**
   - Build → **Build Bundle(s) / APK(s)** → **Build APK(s)**
   - APK will be at: `android-app/app/build/outputs/apk/release/app-release.apk`

4. **Install on Android Device**
   - Transfer APK to phone
   - Enable **Unknown Sources** in Android settings
   - Install APK

## Initial Pairing

### Step 1: Start Windows Service

1. On Windows PC, ensure Bl0ckedService is running
2. Service will generate a 6-digit pairing code
3. **To view pairing code:**
   - Check console logs (if running in debug mode), OR
   - The pairing code will be displayed in a window on first run

### Step 2: Pair Android App

1. Open Bl0cked app on Android
2. You'll see pairing screen
3. Enter:
   - **6-digit code** from Windows PC
   - **PC IP address** (e.g., 192.168.1.100)
     - Find PC IP: Open Command Prompt, type `ipconfig`, look for IPv4 Address
4. Tap **Pair**
5. Wait for confirmation

### Step 3: Set Emergency Unlock Code

1. On Windows PC, navigate to `C:\ProgramData\Bl0cked\state.json`
2. Edit the file (requires admin rights)
3. Add your emergency unlock code:
   ```json
   {
     "locked": false,
     "emergencyUnlockCode": "12345678"
   }
   ```
4. Save the file
5. Restart the service for changes to take effect

## Daily Usage

### Locking the Computer

1. Open Bl0cked app
2. Go to **Control** tab
3. Tap **ACTIVATE LOCK** button
4. PC will immediately show lockdown overlay
5. Timer starts counting

### Monitoring Progress

1. View **Control** tab to see:
   - Lock status (LOCKED/UNLOCKED)
   - Lock duration timer
2. Switch to **Chores** tab to see:
   - Which chores brother has checked off
   - Real-time updates as he checks items

### Unlocking the Computer

1. Verify chores are complete (check Chores tab)
2. Go to **Control** tab
3. Tap **DEACTIVATE LOCK** button
4. PC overlay will disappear
5. Normal computer access restored

### Managing Chores

1. Go to **Chores** tab
2. **Add new chore:**
   - Tap **+ NEW CHORE**
   - Enter chore description
   - Tap **Add**
3. **Edit chore:**
   - Tap **Edit** button on chore
   - Modify text
   - Tap **Save**
4. **Delete chore:**
   - Tap trash icon (🗑)
   - Confirm deletion

## Emergency Situations

### Forgot Phone or Battery Died

1. On locked PC, click **Emergency Unlock** button (bottom-left)
2. Enter 8-digit emergency unlock code
3. PC unlocks immediately
4. **Note:** After 3 wrong attempts, 1-hour lockout activates

### Network is Down

- **Local mode:** If both devices on same WiFi, app automatically uses local connection (faster)
- **Internet mode:** If away from home, app uses Firebase (requires internet on both devices)
- **If both fail:** Use emergency unlock code

### Reinstalling Android App

1. Reinstall app from APK
2. You'll need to re-pair:
   - Reset pairing on PC (requires admin)
   - Generate new pairing code
   - Pair again from app

## Security Best Practices

### For Parents/Admins:

1. **Protect Your Android Device**
   - Use strong phone lock (PIN/fingerprint)
   - Don't leave phone unattended

2. **Keep Emergency Code Secret**
   - Write it down in secure location
   - Don't share with child
   - Change it periodically

3. **Secure the PC**
   - Set BIOS password to prevent boot from USB
   - Enable BitLocker drive encryption (optional but recommended)
   - Don't share admin password

4. **Monitor Usage**
   - Check Firebase Console occasionally for unusual activity
   - Review lock/unlock patterns

### Known Limitations:

1. **USB Boot Bypass**
   - Child could boot from USB drive to access files
   - **Mitigation:** Set BIOS password, disable USB boot

2. **Physical Damage**
   - Child could physically damage PC to avoid lockdown
   - **Mitigation:** Discuss consequences beforehand

3. **Requires Admin Password**
   - Service can be uninstalled with admin password
   - **Mitigation:** Don't share admin password

## Troubleshooting

### PC won't lock

**Symptoms:** Tap ACTIVATE LOCK but nothing happens

**Solutions:**
1. Check Windows Service is running (services.msc)
2. Verify network connection between phone and PC
3. Check Firebase credentials are valid
4. Review service logs for errors

### Overlay doesn't appear

**Symptoms:** PC locked but no overlay visible

**Solutions:**
1. Check multiple monitors (overlay should be on all)
2. Restart Windows Service
3. Verify overlay window process is running

### Can't unlock PC

**Symptoms:** Tap DEACTIVATE LOCK but PC stays locked

**Solutions:**
1. Wait 30 seconds and try again (network delay)
2. Switch between Local and Internet mode
3. Use emergency unlock code
4. Restart service (requires admin)

### Chores not syncing

**Symptoms:** Changes in app don't appear on PC

**Solutions:**
1. Check internet connection on both devices
2. Verify Firebase Realtime Database is accessible
3. Force close and reopen app
4. Check Firebase Console for data

## Uninstallation

### To Remove System:

1. **Unlock PC first** (must be unlocked to uninstall)

2. **Stop and Remove Service:**
   ```
   sc stop Bl0ckedService
   sc delete Bl0ckedService
   ```

3. **Delete Files:**
   ```
   rmdir /s "C:\Program Files\Bl0cked"
   rmdir /s "C:\ProgramData\Bl0cked"
   ```

4. **Uninstall Android App:**
   - Settings → Apps → Bl0cked → Uninstall

## Support

For issues or questions:
- Check this documentation thoroughly
- Review Firebase setup guide
- Check planning.md for technical specifications

**Remember:** This system is designed for parental oversight, not punishment. Use responsibly and discuss expectations with your child beforehand.
