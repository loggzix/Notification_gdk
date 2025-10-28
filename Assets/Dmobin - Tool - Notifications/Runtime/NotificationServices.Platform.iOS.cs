#if UNITY_IOS
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Notifications.iOS;

/// <summary>
/// iOS-specific notification implementation
/// </summary>
public partial class NotificationServices
{
    #region iOS
    // iOS cache fields for performance
    private iOSNotification[] cachedScheduledNotifications;
    private iOSNotification[] cachedDeliveredNotifications;
    private float cacheTimestamp;
    private const float CACHE_TIMEOUT = 2f;
    
    private IEnumerator RequestAuthorizationiOS(Action<bool> callback = null)
    {
        pendingIosPermissionCallback = callback;
        var authOptions = AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;

        using (var req = new AuthorizationRequest(authOptions, true))
        {
            float elapsedTime = 0f;
            while (!req.IsFinished && elapsedTime < Timeouts.IosAuthorization)
            {
                if (applicationQuitting || disposed)
                {
                    // Execute callback for quit/disposed (for DI consistency with Android)
                    var cb = pendingIosPermissionCallback;
                    pendingIosPermissionCallback = null;
                    cb?.Invoke(false);
                    yield break;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (this == null || !isInitialized || applicationQuitting || disposed)
            {
                // Execute callback for quit/disposed (for DI consistency with Android)
                var cb = pendingIosPermissionCallback;
                pendingIosPermissionCallback = null;
                cb?.Invoke(false);
                yield break;
            }

            if (elapsedTime >= Timeouts.IosAuthorization)
            {
                LogWarning("iOS auth timeout");
                
                // Execute callback for timeout (for DI consistency with Android)
                var cb = pendingIosPermissionCallback;
                pendingIosPermissionCallback = null;
                cb?.Invoke(false);
                
                yield break;
            }

            if (req.Granted)
            {
                hasIosPermission = true;
                LoadCurrentBadgeCount();
                DispatchEvent(NotificationEvent.EventType.PermissionGranted, "", "");
                
                // Execute callback if provided (for DI consistency with Android)
                var cb = pendingIosPermissionCallback;
                pendingIosPermissionCallback = null;
                cb?.Invoke(true);
            }
            else
            {
                hasIosPermission = false;
                LogWarning("iOS permission denied", req.Error);
                DispatchEvent(NotificationEvent.EventType.PermissionDenied, "", req.Error);
                
                // Execute callback if provided (for DI consistency with Android)
                var cb = pendingIosPermissionCallback;
                pendingIosPermissionCallback = null;
                cb?.Invoke(false);
            }
        }
        authCoroutine = null;
    }

    private void LoadCurrentBadgeCount()
    {
        currentBadgeCount = iOSNotificationCenter.ApplicationBadge;
    }

    private void CheckiOSNotificationTapped()
    {
        try
        {
            var deliveredNotifications = iOSNotificationCenter.GetDeliveredNotifications();
            if (deliveredNotifications.Length > 0)
            {
                var lastNotification = deliveredNotifications[^1];
                LogInfo("iOS app opened from notification", lastNotification.Title);
                DispatchEvent(NotificationEvent.EventType.Tapped, lastNotification.Title, lastNotification.Body);
            }
        }
        catch (Exception e)
        {
            RecordError("CheckiOSNotificationTapped", e);
            LogError("Failed to check iOS notification", e.Message);
        }
    }

    private void SendiOSNotification(NotificationData data)
    {
        try
        {
            iOSNotificationTrigger trigger;

            if (data.repeats && data.repeatInterval != RepeatInterval.None)
                trigger = GetIOSCalendarTrigger(data.repeatInterval, data.fireTimeInSeconds);
            else
                trigger = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = new TimeSpan(0, 0, data.fireTimeInSeconds),
                    Repeats = false
                };

            var identifier = string.IsNullOrEmpty(data.identifier) ? Guid.NewGuid().ToString() : data.identifier;
            int badgeCount;
            
            if (data.customBadgeCount >= 0)
            {
                // Use explicit badge count
                badgeCount = data.customBadgeCount;
                if (!autoIncrementBadge)
                    currentBadgeCount = data.customBadgeCount; // Sync with explicit count
            }
            else if (autoIncrementBadge)
            {
                // Auto increment enabled
                badgeCount = ++currentBadgeCount;
            }
            else
            {
                // Use current badge without increment (manual management)
                badgeCount = currentBadgeCount;
            }

            var notification = new iOSNotification
            {
                Identifier = identifier,
                Title = data.title,
                Body = data.body,
                Subtitle = data.subtitle,
                ShowInForeground = true,
                ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound | PresentationOption.Badge,
                CategoryIdentifier = "default_category",
                ThreadIdentifier = data.groupKey,
                Trigger = trigger,
                Badge = badgeCount,
            };

            if (!string.IsNullOrEmpty(data.soundName) && data.soundName != "default")
                notification.SoundName = data.soundName;

            iOSNotificationCenter.ScheduleNotification(notification);
            AddNotificationId(identifier, 0, data.groupKey);
            LogNotificationScheduled(data.title, data.fireTimeInSeconds, badgeCount);
            RecordSuccess();
            Interlocked.Increment(ref _ctrTotalScheduled); // Atomic counter, no lock!
            InvalidateIOSNotificationCache(); // Invalidate cache after scheduling
        }
        catch (Exception e)
        {
            RecordError("SendiOSNotification", e);
            LogError("Failed to send iOS notification", e.Message);
        }
    }

    private iOSNotificationCalendarTrigger GetIOSCalendarTrigger(RepeatInterval interval, int initialDelaySeconds)
    {
        var fireDate = DateTime.UtcNow.AddSeconds(initialDelaySeconds);
        var trigger = new iOSNotificationCalendarTrigger { Repeats = true };

        switch (interval)
        {
            case RepeatInterval.Daily:
                trigger.Hour = fireDate.Hour;
                trigger.Minute = fireDate.Minute;
                trigger.Second = fireDate.Second;
                break;
            case RepeatInterval.Weekly:
                // Weekday: 1=Sunday, 2=Monday, ..., 7=Saturday (Apple docs)
                trigger.Weekday = (int)fireDate.DayOfWeek + 1;
                trigger.Hour = fireDate.Hour;
                trigger.Minute = fireDate.Minute;
                break;
        }
        return trigger;
    }
    
    /// <summary>
    /// Gets iOS notifications with caching to reduce repeated system queries
    /// </summary>
    private void GetCachedIOSNotifications(out iOSNotification[] scheduled, out iOSNotification[] delivered)
    {
        bool needsRefresh = cachedScheduledNotifications == null || cachedDeliveredNotifications == null ||
                           (Time.time - cacheTimestamp) > CACHE_TIMEOUT;
        
        if (needsRefresh)
        {
            try
            {
                cachedScheduledNotifications = iOSNotificationCenter.GetScheduledNotifications();
                cachedDeliveredNotifications = iOSNotificationCenter.GetDeliveredNotifications();
                cacheTimestamp = Time.time;
            }
            catch (Exception e)
            {
                LogError("Failed to get iOS notifications", e.Message);
                cachedScheduledNotifications = Array.Empty<iOSNotification>();
                cachedDeliveredNotifications = Array.Empty<iOSNotification>();
                cacheTimestamp = Time.time;
            }
        }
        
        scheduled = cachedScheduledNotifications;
        delivered = cachedDeliveredNotifications;
    }
    
    /// <summary>
    /// Invalidates iOS notification cache (call after schedule/cancel operations)
    /// </summary>
    private void InvalidateIOSNotificationCache()
    {
        cachedScheduledNotifications = null;
        cachedDeliveredNotifications = null;
        cacheTimestamp = 0f;
    }
    #endregion
}
#endif

