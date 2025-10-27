# Firebase Configuration Guide

This document explains how to set up Firebase for the Bl0cked Lockdown System.

## Overview

Firebase provides the remote communication layer when the Android app is not on the same local network as the Windows PC. This enables control from anywhere (school, work, etc.).

## Prerequisites

- Google account
- Access to Firebase Console (https://console.firebase.google.com)

## Setup Steps

### 1. Create Firebase Project

1. Go to https://console.firebase.google.com
2. Click "Add project"
3. Enter project name: `Bl0cked-Lockdown`
4. Follow prompts to create project
5. Disable Google Analytics (optional)

### 2. Enable Firebase Realtime Database

1. In Firebase Console, select your project
2. Navigate to **Build** → **Realtime Database**
3. Click **Create Database**
4. Select location (choose closest to your region)
5. Start in **Test mode** initially (we'll configure security rules later)

### 3. Configure Security Rules

Replace the default rules with these secure rules:

```json
{
  "rules": {
    "bl0cked": {
      "devices": {
        "$pcId": {
          ".read": "auth != null && (auth.uid == $pcId || root.child('bl0cked/devices/' + $pcId + '/pairedDevice').val() == auth.uid)",
          ".write": "auth != null && (auth.uid == $pcId || root.child('bl0cked/devices/' + $pcId + '/pairedDevice').val() == auth.uid)"
        }
      }
    }
  }
}
```

### 4. Enable Firebase Cloud Messaging (FCM)

1. Navigate to **Build** → **Cloud Messaging**
2. Enable Cloud Messaging API
3. Note your **Server Key** (needed for Windows Service)

### 5. Set Up Android App

1. In Firebase Console, click **Add app** → **Android**
2. Enter package name: `com.bl0cked.app`
3. Download `google-services.json`
4. Place `google-services.json` in `android-app/app/` directory
5. **Important:** Do NOT commit `google-services.json` to version control

### 6. Set Up Windows Service

1. In Firebase Console, go to **Project Settings** → **Service Accounts**
2. Click **Generate new private key**
3. Save the JSON file as `firebase-credentials.json`
4. Place in `C:\ProgramData\Bl0cked\firebase-credentials.json` on the Windows PC
5. **Important:** Do NOT commit this file to version control

### 7. Enable Firebase Authentication (Anonymous)

1. Navigate to **Build** → **Authentication**
2. Click **Get Started**
3. Enable **Anonymous** authentication
4. This allows the PC and Android app to authenticate with Firebase

## Database Structure

The Firebase Realtime Database will have this structure:

```
bl0cked/
  devices/
    {pcId}/
      status/
        locked: true/false
        lockStartTime: timestamp
        lockDuration: seconds
        lastUpdate: timestamp
      chores/
        {choreId}/
          id: number
          text: string
          checked: boolean
      commands/
        {commandId}/
          type: "lock" / "unlock" / "updateChores"
          payload: {...}
          timestamp: timestamp
          fromDevice: androidDeviceId
          processed: boolean
      pairedDevice: androidDeviceId
```

## Security Best Practices

1. **Never commit Firebase credentials to version control**
   - `google-services.json` is in `.gitignore`
   - `firebase-credentials.json` is in `.gitignore`

2. **Use secure rules**
   - Only paired devices can communicate
   - Timestamp-based replay attack prevention

3. **Limit access**
   - Only install service account credentials on trusted PCs
   - Only share pairing codes temporarily

## Testing Connection

### Test Android App Connection:
1. Install Android app with `google-services.json`
2. Pair with PC
3. Try sending lock/unlock commands
4. Verify commands appear in Firebase Console → Realtime Database

### Test Windows Service Connection:
1. Place `firebase-credentials.json` in `C:\ProgramData\Bl0cked\`
2. Start Windows Service
3. Check service logs for "Firebase initialized successfully"
4. Verify status updates appear in Firebase Console

## Troubleshooting

**Problem:** Android app can't connect to Firebase
- Verify `google-services.json` is in correct location
- Check package name matches in Firebase Console
- Ensure internet permission in AndroidManifest.xml

**Problem:** Windows service can't connect to Firebase
- Verify `firebase-credentials.json` exists and is valid
- Check Windows service has internet connectivity
- Review service logs for error messages

**Problem:** Commands not syncing
- Check Firebase security rules are configured
- Verify both devices are authenticated
- Check pairing is established correctly

## Cost

Firebase free tier (Spark plan) is sufficient for single-user use:
- **Realtime Database:** 1 GB storage, 10 GB/month transfer
- **Cloud Messaging:** Unlimited
- **Authentication:** 10K verifications/month

For this use case (1 PC + 1 Android device), you will stay well within free tier limits.

## Support

For Firebase-specific issues, refer to:
- Firebase Documentation: https://firebase.google.com/docs
- Firebase Support: https://firebase.google.com/support
