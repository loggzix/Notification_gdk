using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif
using UnityEngine;

/// <summary>
/// Core models, constants, and interfaces for NotificationServices
/// </summary>
/// <remarks>
/// This partial class contains:
/// - Interfaces (INotificationService, INotificationPlatform)
/// - Data Structures (NotificationData, NotificationEvent, PerformanceMetrics, etc.)
/// - Constants (TimeConstants, Limits, PoolSizes, Timeouts, etc.)
/// </remarks>
public partial class NotificationServices
{
    #region Interfaces
    /// <summary>
    /// Public API interface for notification operations
    /// </summary>
    public interface INotificationService
    {
        bool SendNotification(string title, string body, int fireTimeInSeconds, string identifier = null);
        void CancelNotification(string identifier);
        bool HasNotificationPermission();
        int GetScheduledNotificationCount();
    }
    
    /// <summary>
    /// Platform abstraction interface for testability and dependency injection
    /// </summary>
    public interface INotificationPlatform
    {
        bool IsAndroid { get; }
        bool IsIOS { get; }
        int SendAndroidNotification(NotificationData data, string channelId);
        void SendIOSNotification(NotificationData data, int currentBadgeCount);
        void CancelNotification(string identifier, int id);
        void CancelAllScheduled();
        void CancelAllDisplayed();
        bool CheckAndroidPermission();
        void RequestAndroidPermission(Action<bool> callback);
        void RequestIOSAuthorization(MonoBehaviour context, Action<bool> callback);
        string GetNotificationStatus(int id);
    }
    
    /// <summary>
    /// Default Unity platform implementation - delegates to Unity Notification APIs
    /// </summary>
    private sealed class UnityNotificationPlatform : INotificationPlatform
    {
        private readonly NotificationServices service;
        
        public UnityNotificationPlatform(NotificationServices service)
        {
            this.service = service;
        }
        
        public bool IsAndroid => IS_ANDROID;
        public bool IsIOS => IS_IOS;
        
        public int SendAndroidNotification(NotificationData data, string channelId)
        {
#if UNITY_ANDROID
            return service.SendAndroidNotification(data);
#else
            return -1;
#endif
        }
        
        public void SendIOSNotification(NotificationData data, int currentBadgeCount)
        {
#if UNITY_IOS
            service.SendiOSNotification(data);
#endif
        }
        
        public void CancelNotification(string identifier, int id)
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelScheduledNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(identifier);
#endif
        }
        
        public void CancelAllScheduled()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllScheduledNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
        }
        
        public void CancelAllDisplayed()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
#endif
        }
        
        public bool CheckAndroidPermission()
        {
#if UNITY_ANDROID
            return Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS");
#else
            return false;
#endif
        }
        
        public void RequestAndroidPermission(Action<bool> callback)
        {
#if UNITY_ANDROID
            service.RequestAuthorizationAndroid(callback);
#endif
        }
        
        public void RequestIOSAuthorization(MonoBehaviour context, Action<bool> callback)
        {
#if UNITY_IOS
            service.StartCoroutine(service.RequestAuthorizationiOS(callback));
#endif
        }
        
        public string GetNotificationStatus(int id)
        {
#if UNITY_ANDROID
            try { return AndroidNotificationCenter.CheckScheduledNotificationStatus(id).ToString(); }
            catch { return "Error"; }
#elif UNITY_IOS
            return "Scheduled";
#else
            return "Unknown";
#endif
        }
    }
    #endregion

    #region Data Structures
    /// <summary>
    /// Log verbosity levels for runtime control
    /// </summary>
    public enum LogLevel : byte 
    { 
        None = 0,      // No logging
        Error = 1,     // Only errors
        Warning = 2,   // Errors + warnings
        Info = 3,      // Errors + warnings + info
        Verbose = 4    // All logs including debug
    }
    
    public enum RepeatInterval : byte { None, Daily, Weekly, Custom }

    [Serializable]
    public sealed class NotificationData
    {
        public string title;
        public string body;
        public string subtitle;
        public int fireTimeInSeconds;
        public string smallIcon;
        public string largeIcon;
        public string identifier;
        public bool repeats;
        public RepeatInterval repeatInterval;
        public string soundName;
        public string groupKey;
        public int customBadgeCount;

        public NotificationData()
        {
            smallIcon = "icon_0";
            largeIcon = "icon_1";
            soundName = "default";
            groupKey = "default_group";
            customBadgeCount = -1;
            repeatInterval = RepeatInterval.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid() => !string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body) && fireTimeInSeconds >= 0;
        
        public void Reset()
        {
            title = body = subtitle = identifier = soundName = groupKey = null;
            smallIcon = "icon_0";
            largeIcon = "icon_1";
            fireTimeInSeconds = 0;
            repeats = false;
            repeatInterval = RepeatInterval.None;
            customBadgeCount = -1;
        }

        public void CopyFrom(NotificationData source)
        {
            title = source.title;
            body = source.body;
            subtitle = source.subtitle;
            fireTimeInSeconds = source.fireTimeInSeconds;
            smallIcon = source.smallIcon;
            largeIcon = source.largeIcon;
            identifier = source.identifier;
            repeats = source.repeats;
            repeatInterval = source.repeatInterval;
            soundName = source.soundName;
            groupKey = source.groupKey;
            customBadgeCount = source.customBadgeCount;
        }
    }

    [Serializable]
    public sealed class ReturnNotificationConfig
    {
        public bool enabled = true;
        public string title = "We miss you!";
        public string body = "Come back and claim your rewards!";
        public int hoursBeforeNotification = 24;
        public bool repeating = false;
        public RepeatInterval repeatInterval = RepeatInterval.Daily;
        public string identifier = "return_notification";
    }

    [Serializable]
    public sealed class SerializedNotification
    {
        public string Identifier;
        public int PlatformId;
    }

    [Serializable]
    public sealed class NotificationStore
    {
        public List<SerializedNotification> Notifications = new List<SerializedNotification>();
        public uint Crc32;
        public ReturnNotificationConfig ReturnConfig;
        public long LastOpenUnixTime; // Unix timestamp for atomic file persistence
    }

    [Serializable]
    private sealed class ScheduledIdsWrapper
    {
        public List<string> identifiers = new List<string>();
        public List<int> ids = new List<int>();
    }

    public sealed class NotificationEvent
    {
        public enum EventType : byte { Received, Tapped, PermissionGranted, PermissionDenied, Error }
        public EventType Type;
        public string Title;
        public string Body;
        public DateTime Timestamp;
        public Exception Error;

        public void Reset()
        {
            Title = Body = null;
            Timestamp = DateTime.MinValue;
            Error = null;
        }
    }

    public sealed class PerformanceMetrics
    {
        public int TotalScheduled;
        public int TotalCancelled;
        public int TotalErrors;
        public int PoolHits;
        public int PoolMisses;
        public int MainThreadDrops; // Number of dropped main thread actions due to queue full
        public double AverageSaveTimeMs;
        public DateTime StartTime;
        public long CurrentMemoryUsage;
        public long PeakMemoryUsage;
        private int saveCount = 0;
        private double totalSaveTimeMs = 0;

        public PerformanceMetrics()
        {
            StartTime = DateTime.UtcNow;
            UpdateMemory();
        }

        public void UpdateMemory()
        {
            CurrentMemoryUsage = GC.GetTotalMemory(false);
            if (CurrentMemoryUsage > PeakMemoryUsage)
                PeakMemoryUsage = CurrentMemoryUsage;
        }
        
        public void Reset()
        {
            TotalScheduled = TotalCancelled = TotalErrors = PoolHits = PoolMisses = MainThreadDrops = 0;
            AverageSaveTimeMs = 0;
            StartTime = DateTime.UtcNow;
            CurrentMemoryUsage = 0;
            PeakMemoryUsage = 0;
            saveCount = 0;
            totalSaveTimeMs = 0;
            UpdateMemory();
        }
        
        public void RecordSaveTime(double timeMs)
        {
            saveCount++;
            totalSaveTimeMs += timeMs;
            AverageSaveTimeMs = totalSaveTimeMs / saveCount;
        }
    }

    public sealed class NotificationBuilder
    {
        private readonly NotificationData data;
        private readonly NotificationServices service;

        internal NotificationBuilder(NotificationServices service)
        {
            this.service = service;
            this.data = service.GetPooledData();
        }

        public NotificationBuilder WithTitle(string title) { data.title = title; return this; }
        public NotificationBuilder WithBody(string body) { data.body = body; return this; }
        public NotificationBuilder WithSubtitle(string subtitle) { data.subtitle = subtitle; return this; }
        public NotificationBuilder WithIdentifier(string identifier) { data.identifier = identifier; return this; }
        public NotificationBuilder In(TimeSpan delay) { data.fireTimeInSeconds = (int)delay.TotalSeconds; return this; }
        public NotificationBuilder In(int seconds) { data.fireTimeInSeconds = seconds; return this; }
        public NotificationBuilder At(DateTime scheduledTime) 
        { 
            data.fireTimeInSeconds = Mathf.Max(0, (int)(scheduledTime - DateTime.UtcNow).TotalSeconds); 
            return this; 
        }
        public NotificationBuilder Repeating(RepeatInterval interval) 
        { 
            data.repeats = true; 
            data.repeatInterval = interval; 
            return this; 
        }
        public NotificationBuilder WithSound(string soundName) { data.soundName = soundName; return this; }
        public NotificationBuilder WithGroup(string groupKey) { data.groupKey = groupKey; return this; }
        public NotificationBuilder WithBadge(int badgeCount) { data.customBadgeCount = badgeCount; return this; }

        public bool Schedule()
        {
            if (!data.IsValid())
            {
                // Provide detailed error message for debugging
                var errorDetails = new StringBuilder(128);
                errorDetails.Append("Invalid notification data - ");
                
                if (string.IsNullOrEmpty(data.title))
                    errorDetails.Append("missing title");
                    
                if (string.IsNullOrEmpty(data.body))
                {
                    if (errorDetails.Length > 30) errorDetails.Append(", ");
                    errorDetails.Append("missing body");
                }
                    
                if (data.fireTimeInSeconds < 0)
                {
                    if (errorDetails.Length > 30) errorDetails.Append(", ");
                    errorDetails.Append("invalid fireTime: ").Append(data.fireTimeInSeconds);
                }
                
                service.LogWarning(errorDetails.ToString());
                service.ReturnToPool(data);
                return false;
            }
            if (string.IsNullOrEmpty(data.identifier)) 
                data.identifier = Guid.NewGuid().ToString();
            
            bool success = service.SendNotificationInternal(data);
            service.ReturnToPool(data);
            return success;
        }

        public async Task<bool> ScheduleAsync(CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<bool>();
            bool queued = service.TryRunOnMainThread(() =>
            {
                try
                {
                    var result = Schedule();
                    tcs.TrySetResult(result);
                }
                catch (Exception e) { tcs.TrySetException(e); }
            }, dropOldestIfFull: false); // Don't drop - fail fast if queue full
            
            // Protect against dropped action causing infinite wait
            if (!queued)
            {
                tcs.TrySetCanceled();
                throw new InvalidOperationException("Main thread queue full - schedule request dropped.");
            }
            
            // Simple: use CancellationTokenSource with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Timeouts.AsyncOperationTimeoutSeconds));
            
            using var registration = timeoutCts.Token.Register(() => tcs.TrySetCanceled());
            
            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!ct.IsCancellationRequested)
                {
                    service.LogWarning("ScheduleAsync timed out after", Timeouts.AsyncOperationTimeoutSeconds);
                    throw new TimeoutException("Schedule operation timed out");
                }
                throw;
            }
        }
    }
    #endregion

    #region Constants
    private static class TimeConstants
    {
        public const int SecondsPerDay = 86400;
        public const int SecondsPerHour = 3600;
        public const int SecondsPerMinute = 60;
    }

    private static class Limits
    {
        public const int IosMaxNotifications = 64;
        public const int AndroidMaxNotifications = 500;
        public const int MaxTrackedNotifications = 100;
        public const int CleanupBatchSize = 10;
        public const int MaxBatchSize = 50;
        public const int MaxPrefsSize = 16384; // 16KB limit for PlayerPrefs string values
        public const int MainThreadQueueCapacity = 1024; // Prevent unbounded growth
        public const int MaxActionsPerFrame = 128; // Batch process limit
    }
    
    private static class PoolSizes
    {
        public const int NotificationData = 20;
        public const int Events = 10;
        public const int StringBuilderCapacity = 256;
        public const int StringBuilderMaxCapacity = 4096;
    }

    private static class Timeouts
    {
        public const float IosAuthorization = 10f;
        public const float SaveDebounce = 0.5f;
        public const float DateTimeCache = 60f;
        public const float CircuitBreaker = 60f;
        public const int AsyncOperationTimeoutSeconds = 5;  // Timeout for async operations
        public const float MaxProcessBudgetMs = 2.0f; // Time budget per frame for main thread actions
    }

    private static class RetryConfig
    {
        public const int SaveAttempts = 3;
        public const int SaveDelayMs = 100;
        public const int CircuitBreakerThreshold = 5;
    }

    private const string ANDROID_CHANNEL_ID = "default_channel";
    private const string ANDROID_CHANNEL_NAME = "Default Channel";
    
    /// <summary>
    /// Configuration for Android notification channel - can be customized per market/product
    /// </summary>
    public class AndroidChannelConfig
    {
        public Importance Importance = Importance.High;
        public bool EnableVibration = true;
        public bool EnableLights = true;
        public bool EnableShowBadge = true;
        public bool CanBypassDnd = false;
        public string Description = "Default notification channel";
    }
    
    private AndroidChannelConfig channelConfig = new AndroidChannelConfig();
    private const string PREFS_KEY_SCHEDULED_IDS = "ScheduledNotificationIds";
    private const string PREFS_KEY_LAST_OPEN_TIME = "LastAppOpenTime";
    private const string PREFS_KEY_RETURN_CONFIG = "ReturnNotificationConfig";
    private const int BINARY_FORMAT_VERSION = 1;
    
    // Atomic file persistence paths
    private static string GetNotificationStorePath() => 
        Path.Combine(Application.persistentDataPath, "notification_store.dat");
    private static string GetNotificationStoreTempPath() => 
        GetNotificationStorePath() + ".tmp";

#if UNITY_ANDROID
    private const bool IS_ANDROID = true;
    private const bool IS_IOS = false;
#elif UNITY_IOS
    private const bool IS_ANDROID = false;
    private const bool IS_IOS = true;
#else
    private const bool IS_ANDROID = false;
    private const bool IS_IOS = false;
#endif
    #endregion
}
