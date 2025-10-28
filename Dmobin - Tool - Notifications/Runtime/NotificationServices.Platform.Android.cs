#if UNITY_ANDROID
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Notifications.Android;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// Android-specific notification implementation
/// </summary>
public partial class NotificationServices
{
    #region Android
    /// <summary>
    /// Gets Android SDK level using JNI (robust and cached)
    /// </summary>
    private static int GetAndroidSdkInt()
    {
        if (cachedAndroidSdkInt.HasValue)
            return cachedAndroidSdkInt.Value;

        try
        {
            using (var buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                cachedAndroidSdkInt = buildVersion.GetStatic<int>("SDK_INT");
                return cachedAndroidSdkInt.Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NotificationServices] Could not determine Android SDK via JNI: {e.Message}");
            // Safe fallback: assume 33+ (most conservative approach)
            // This ensures POST_NOTIFICATIONS permission is requested on uncertain platforms
            cachedAndroidSdkInt = 33;
            Debug.LogWarning("[NotificationServices] Using fallback SDK level 33 (assuming Android 13+)");
            return 33;
        }
    }
    
    private void RequestAuthorizationAndroid(Action<bool> callback = null)
    {
        if (isCheckingPermission) return;
        isCheckingPermission = true;
        pendingPermissionCallback = callback;
        
        // Check Android API level - POST_NOTIFICATIONS only needed on Android 13+ (API 33+)
        // On older versions, notifications work without runtime permission
        int sdkInt = GetAndroidSdkInt();
        if (sdkInt < 33)
        {
            // Android < 13 - notifications always allowed
            hasAndroidPermission = true;
            isCheckingPermission = false;
            Debug.Log($"[NotificationServices] Android SDK {sdkInt} < 33, notifications always allowed");
            DispatchEvent(NotificationEvent.EventType.PermissionGranted, "", "");
            
            // Execute callback immediately for SDK < 33
            var cb = pendingPermissionCallback;
            pendingPermissionCallback = null;
            cb?.Invoke(true);
            
            return;
        }
        
        if (Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
        {
            hasAndroidPermission = true;
            isCheckingPermission = false;
            Debug.Log("[NotificationServices] Android permission granted");
            DispatchEvent(NotificationEvent.EventType.PermissionGranted, "", "");
            
            // Execute callback if permission already granted
            var cb = pendingPermissionCallback;
            pendingPermissionCallback = null;
            cb?.Invoke(true);
            
            return;
        }

        // Cleanup old callbacks if exists
        CleanupAndroidCallbacks();

        permissionCallbacks = new PermissionCallbacks();
        permissionCallbacks.PermissionGranted += OnAndroidPermissionGranted;
        permissionCallbacks.PermissionDenied += OnAndroidPermissionDenied;
        
#pragma warning disable CS0618
        permissionCallbacks.PermissionDeniedAndDontAskAgain += OnAndroidPermissionDeniedAndDontAskAgain;
#pragma warning restore CS0618
        
        Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS", permissionCallbacks);
    }

    private void CleanupAndroidCallbacks()
    {
        if (permissionCallbacks != null)
        {
            permissionCallbacks.PermissionGranted -= OnAndroidPermissionGranted;
            permissionCallbacks.PermissionDenied -= OnAndroidPermissionDenied;
            
#pragma warning disable CS0618
            permissionCallbacks.PermissionDeniedAndDontAskAgain -= OnAndroidPermissionDeniedAndDontAskAgain;
#pragma warning restore CS0618
            
            permissionCallbacks = null;
        }
    }

    private void OnAndroidPermissionGranted(string permissionName)
    {
        if (permissionName == "android.permission.POST_NOTIFICATIONS")
        {
            hasAndroidPermission = true;
            isCheckingPermission = false;
            DispatchEvent(NotificationEvent.EventType.PermissionGranted, "", "");
            
            // Execute callback if provided (for DI)
            var cb = pendingPermissionCallback;
            pendingPermissionCallback = null;
            cb?.Invoke(true);
            
            CleanupAndroidCallbacks();
        }
    }

    private void OnAndroidPermissionDenied(string permissionName)
    {
        if (permissionName == "android.permission.POST_NOTIFICATIONS")
        {
            hasAndroidPermission = false;
            isCheckingPermission = false;
            DispatchEvent(NotificationEvent.EventType.PermissionDenied, "", "");
            
            // Execute callback if provided (for DI)
            var cb = pendingPermissionCallback;
            pendingPermissionCallback = null;
            cb?.Invoke(false);
            
            CleanupAndroidCallbacks();
        }
    }

    private void OnAndroidPermissionDeniedAndDontAskAgain(string permissionName)
    {
        if (permissionName == "android.permission.POST_NOTIFICATIONS")
        {
            hasAndroidPermission = false;
            isCheckingPermission = false;
            DispatchEvent(NotificationEvent.EventType.PermissionDenied, "", "Permanent");
            
            // Execute callback if provided (for DI)
            var cb = pendingPermissionCallback;
            pendingPermissionCallback = null;
            cb?.Invoke(false);
            
            CleanupAndroidCallbacks();
        }
    }

    private void RegisterAndroidNotificationChannel()
    {
        cachedChannel = new AndroidNotificationChannel
        {
            Id = ANDROID_CHANNEL_ID,
            Name = ANDROID_CHANNEL_NAME,
            Importance = channelConfig.Importance,
            Description = channelConfig.Description,
            CanBypassDnd = channelConfig.CanBypassDnd,
            CanShowBadge = channelConfig.EnableShowBadge,
            EnableLights = channelConfig.EnableLights,
            EnableVibration = channelConfig.EnableVibration,
        };
        
        AndroidNotificationCenter.RegisterNotificationChannel(cachedChannel);
    }
    
    /// <summary>
    /// Sets the Android notification channel configuration
    /// Must be called before Initialize() or notification sending
    /// </summary>
    public void SetAndroidChannelConfig(AndroidChannelConfig config)
    {
        channelConfig = config ?? new AndroidChannelConfig();
        if (isInitialized)
        {
            Debug.LogWarning("[NotificationServices] Channel config updated, but channel already registered. Restart to apply.");
        }
    }

    private void RegisterNotificationCallback()
    {
        AndroidNotificationCenter.OnNotificationReceived += OnNotificationReceived;
    }

    private void OnNotificationReceived(AndroidNotificationIntentData data)
    {
        var notification = data.Notification;
        LogInfo("Notification received", notification.Title);
        DispatchEvent(NotificationEvent.EventType.Received, notification.Title, notification.Text);
    }

    private void CheckLastNotificationIntent()
    {
        try
        {
            var intent = AndroidNotificationCenter.GetLastNotificationIntent();
            if (intent != null)
            {
                var notification = intent.Notification;
                LogInfo("App opened from notification", notification.Title);
                DispatchEvent(NotificationEvent.EventType.Tapped, notification.Title, notification.Text);
            }
        }
        catch (Exception e)
        {
            RecordError("CheckLastNotificationIntent", e);
            LogError("Failed to check notification intent", e.Message);
        }
    }

    private int SendAndroidNotification(NotificationData data)
    {
        if (!hasAndroidPermission) return -1;

        try
        {
            var notification = new AndroidNotification
            {
                Title = data.title,
                Text = data.body,
                FireTime = DateTime.UtcNow.AddSeconds(data.fireTimeInSeconds),
                SmallIcon = data.smallIcon,
                LargeIcon = data.largeIcon,
                ShowTimestamp = true,
                ShouldAutoCancel = true,
                Group = data.groupKey,
            };

            if (data.repeats && data.repeatInterval != RepeatInterval.None)
            {
                var interval = GetAndroidRepeatInterval(data.repeatInterval);
                if (interval.HasValue) notification.RepeatInterval = interval.Value;
            }

            int id = AndroidNotificationCenter.SendNotification(notification, ANDROID_CHANNEL_ID);
            AddNotificationId(data.identifier, id, data.groupKey);
            LogNotificationScheduled(data.title, data.fireTimeInSeconds, id);
            RecordSuccess();
            Interlocked.Increment(ref _ctrTotalScheduled); // Atomic counter, no lock!
            return id;
        }
        catch (Exception e)
        {
            RecordError("SendAndroidNotification", e);
            LogError("Failed to send Android notification", e.Message);
            return -1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan? GetAndroidRepeatInterval(RepeatInterval interval)
    {
        return interval switch
        {
            RepeatInterval.Daily => TimeSpan.FromDays(1),
            RepeatInterval.Weekly => TimeSpan.FromDays(7),
            _ => null
        };
    }
    #endregion
}
#endif

