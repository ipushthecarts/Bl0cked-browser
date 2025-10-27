# Add project specific ProGuard rules here.
# You can control the set of applied configuration files using the
# proguardFiles setting in build.gradle.

# Keep Bl0cked classes
-keep class com.bl0cked.** { *; }

# Keep Firebase classes
-keep class com.google.firebase.** { *; }

# Keep Gson classes
-keep class com.google.gson.** { *; }

# Keep OkHttp classes
-keep class okhttp3.** { *; }

# Keep model classes
-keep class com.bl0cked.models.** { *; }
