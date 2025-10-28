using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace DSDK.Notifications
{
    /// <summary>
    /// Public API for NotificationServices
    /// </summary>
    /// <remarks>
    /// This partial class contains all public methods:
    /// - Fluent Builder API
    /// - Standard API (SendNotification, CancelNotification, etc.)
    /// - Async API
    /// - App Lifecycle methods
    /// - Batch Operations
    /// - Debug helpers
    /// - Extensions
    /// </remarks>
    public partial class NotificationServices
    {
        #region Public API - Fluent Builder

        /// <summary>
        /// Creates a fluent notification builder for advanced configuration
        /// </summary>
        /// <returns>NotificationBuilder instance for method chaining</returns>
        /// <example>
        /// <code>
        /// NotificationServices.Instance.CreateNotification()
        ///     .WithTitle("Hello")
        ///     .WithBody("World")
        ///     .In(TimeSpan.FromHours(1))
        ///     .Repeating(RepeatInterval.Daily)
        ///     .Schedule();
        /// </code>
        /// </example>
        public NotificationBuilder CreateNotification() => new NotificationBuilder(this);

        #endregion

        #region Public API - Standard

        bool INotificationService.
            SendNotification(string title, string body, int fireTimeInSeconds, string identifier) =>
            SendNotification(title, body, fireTimeInSeconds, identifier);

        /// <summary>
        /// Schedules a local notification to be delivered after the specified delay
        /// </summary>
        /// <param name="title">Notification title (required, cannot be null or empty)</param>
        /// <param name="body">Notification body/message (required, cannot be null or empty)</param>
        /// <param name="fireTimeInSeconds">Delay in seconds before notification fires (must be >= 0)</param>
        /// <param name="identifier">Unique identifier for the notification (auto-generated if null). Can be used to cancel later.</param>
        /// <returns>True if notification was scheduled successfully, false if validation failed or limit reached</returns>
        /// <exception cref="ArgumentException">Thrown if title or body is null/empty</exception>
        /// <example>
        /// <code>
        /// // Schedule notification in 1 hour
        /// bool success = NotificationServices.Instance.SendNotification(
        ///     "Reminder", 
        ///     "Don't forget to check your progress!", 
        ///     3600,
        ///     "daily_reminder"
        /// );
        /// </code>
        /// </example>
        public bool SendNotification(string title, string body, int fireTimeInSeconds, string identifier = null)
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body) || !ValidateFireTime(fireTimeInSeconds) ||
                !CanScheduleMore() || IsCircuitBreakerOpen())
                return false;

            var data = GetPooledData();
            data.title = title;
            data.body = body;
            data.fireTimeInSeconds = fireTimeInSeconds;
            data.identifier = identifier ?? Guid.NewGuid().ToString();
            bool result = SendNotificationInternal(data);
            ReturnToPool(data);
            return result;
        }

        /// <summary>
        /// Schedules a repeating local notification with specified interval
        /// </summary>
        /// <param name="title">Notification title (required, cannot be null or empty)</param>
        /// <param name="body">Notification body/message (required, cannot be null or empty)</param>
        /// <param name="fireTimeInSeconds">Initial delay in seconds before first notification fires (must be >= 0)</param>
        /// <param name="interval">Repeat interval (Daily, Weekly, etc.)</param>
        /// <param name="identifier">Unique identifier for the notification (auto-generated if null)</param>
        /// <remarks>
        /// Creates a notification that repeats at regular intervals. The first notification fires after fireTimeInSeconds,
        /// then repeats according to the specified interval. Useful for daily reminders, weekly updates, etc.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Schedule daily notification at 9 AM
        /// NotificationServices.Instance.SendRepeatingNotification(
        ///     "Daily Reminder",
        ///     "Check your progress!",
        ///     3600,  // 1 hour from now
        ///     RepeatInterval.Daily,
        ///     "daily_check"
        /// );
        /// </code>
        /// </example>
        public void SendRepeatingNotification(string title, string body, int fireTimeInSeconds, RepeatInterval interval,
            string identifier = null)
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            if (!ValidateFireTime(fireTimeInSeconds) || !CanScheduleMore()) return;
            var data = GetPooledData();
            data.title = title;
            data.body = body;
            data.fireTimeInSeconds = fireTimeInSeconds;
            data.repeats = true;
            data.repeatInterval = interval;
            data.identifier = identifier ?? Guid.NewGuid().ToString();
            SendNotificationInternal(data);
            ReturnToPool(data);
        }

        /// <summary>
        /// Schedules a local notification using NotificationData object (for advanced configuration)
        /// </summary>
        /// <param name="data">NotificationData object containing all notification properties</param>
        /// <remarks>
        /// Allows full control over notification properties including icons, sounds, groups, and badges.
        /// The identifier will be auto-generated if not provided in the data object.
        /// </remarks>
        /// <example>
        /// <code>
        /// var data = new NotificationData
        /// {
        ///     title = "Level Up!",
        ///     body = "You've reached a new milestone",
        ///     fireTimeInSeconds = 3600,
        ///     identifier = "level_up",
        ///     smallIcon = "icon_level",
        ///     groupKey = "achievements"
        /// };
        /// NotificationServices.Instance.SendNotification(data);
        /// </code>
        /// </example>
        public void SendNotification(NotificationData data)
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            if (!data.IsValid() || !ValidateFireTime(data.fireTimeInSeconds) || !CanScheduleMore()) return;
            if (string.IsNullOrEmpty(data.identifier)) data.identifier = Guid.NewGuid().ToString();

            var pooledData = GetPooledData();
            pooledData.CopyFrom(data);
            SendNotificationInternal(pooledData);
            ReturnToPool(pooledData);
        }

        private bool SendNotificationInternal(NotificationData data)
        {
            if (!isInitialized) Initialize();

            // Use platform abstraction (allows mocking for tests)
            if (platform != null)
            {
                if (platform.IsAndroid)
                {
                    int id = platform.SendAndroidNotification(data, ANDROID_CHANNEL_ID);
                    return id >= 0;
                }
                else if (platform.IsIOS)
                {
#if UNITY_IOS
                platform.SendIOSNotification(data, currentBadgeCount);
#endif
                    return true;
                }
                else
                {
                    LogWarning("Platform not supported");
                    return false;
                }
            }

            // Fallback to direct implementation (should rarely happen)
#if UNITY_ANDROID
        return SendAndroidNotification(data) >= 0;
#elif UNITY_IOS
        SendiOSNotification(data);
        return true;
#else
            LogWarning("Platform not supported and no platform injected");
            return false;
#endif
        }

        public void SendNotification(string title, string body, int days, int hours, int minutes, int seconds,
            string identifier = null)
        {
            int totalSeconds = days * TimeConstants.SecondsPerDay + hours * TimeConstants.SecondsPerHour +
                               minutes * TimeConstants.SecondsPerMinute + seconds;
            SendNotification(title, body, totalSeconds, identifier);
        }

        void INotificationService.CancelNotification(string identifier) => CancelNotification(identifier);

        /// <summary>
        /// Cancels a previously scheduled notification by its identifier
        /// </summary>
        /// <param name="identifier">The unique identifier of the notification to cancel</param>
        /// <remarks>
        /// This method is safe to call even if the notification doesn't exist or has already been delivered.
        /// It will remove the notification from both scheduled and displayed notifications.
        /// </remarks>
        /// <example>
        /// <code>
        /// NotificationServices.Instance.CancelNotification("daily_reminder");
        /// </code>
        /// </example>
        public void CancelNotification(string identifier)
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            if (string.IsNullOrEmpty(identifier)) return;

            int id = 0;
            bool found;
            dictLock.EnterWriteLock();
            try
            {
                found = scheduledNotificationIds.TryGetValue(identifier, out var value);
                if (found)
                {
                    id = value.id;
                    insertionOrder.Remove(value.node);
                    scheduledNotificationIds.Remove(identifier);
                }
            }
            finally
            {
                dictLock.ExitWriteLock();
            }

            if (!found) return;

            try
            {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelScheduledNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(identifier);
            InvalidateIOSNotificationCache(); // Invalidate cache after canceling
#endif
                RemoveFromGroup(identifier);
                MarkDirty();
                Interlocked.Increment(ref _ctrTotalCancelled); // Atomic counter, no lock!
            }
            catch (Exception e)
            {
                RecordError("CancelNotification", e);
                LogError("Failed to cancel notification", e.Message);
            }
        }

        public void CancelAllScheduledNotifications()
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            try
            {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllScheduledNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            InvalidateIOSNotificationCache(); // Invalidate cache after canceling all
#endif
                dictLock.EnterWriteLock();
                try
                {
                    scheduledNotificationIds.Clear();
                    insertionOrder.Clear();
                }
                finally
                {
                    dictLock.ExitWriteLock();
                }

                lock (groupsLock)
                {
                    notificationGroups.Clear();
                }

                MarkDirty();
            }
            catch (Exception e)
            {
                RecordError("CancelAllScheduledNotifications", e);
                LogError("Failed to cancel all notifications", e.Message);
            }
        }

        public void CancelAllDisplayedNotifications()
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            try
            {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            iOSNotificationCenter.ApplicationBadge = 0;
            currentBadgeCount = 0;
#endif
            }
            catch (Exception e)
            {
                RecordError("CancelAllDisplayedNotifications", e);
                LogError("Failed to cancel displayed notifications", e.Message);
            }
        }

        public void CancelAllNotifications()
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            CancelAllScheduledNotifications();
            CancelAllDisplayedNotifications();
        }

        bool INotificationService.HasNotificationPermission() => HasNotificationPermission();

        /// <summary>
        /// Checks if the app has permission to send notifications
        /// </summary>
        /// <returns>True if permission is granted, false otherwise</returns>
        /// <remarks>
        /// On Android 13+, this requires POST_NOTIFICATIONS permission.
        /// On iOS, this checks authorization status.
        /// </remarks>
        public bool HasNotificationPermission()
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            // Use cached permission state as source of truth
            // These flags are updated when permission is granted/denied
#if UNITY_ANDROID
            return hasAndroidPermission;
#elif UNITY_IOS
            return hasIosPermission;
#else
            return false;
#endif
        }

        /// <summary>
        /// Unified async permission request API (cross-platform)
        /// </summary>
        /// <param name="ct">Cancellation token for async operation</param>
        /// <returns>True if permission granted, false if denied</returns>
        /// <remarks>
        /// This is the recommended way to request permissions. 
        /// Waits for user response with timeout protection.
        /// </remarks>
        public async Task<bool> RequestPermissionAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (HasNotificationPermission()) return true; // Already granted

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Timeouts.IosAuthorization));

            var tcs = new TaskCompletionSource<bool>();

#if UNITY_ANDROID
        RequestAuthorizationAndroid(granted => 
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult(granted);
        });
#elif UNITY_IOS
        pendingIosPermissionCallback = granted => 
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetResult(granted);
        };
        
        if (authCoroutine != null) StopCoroutine(authCoroutine);
        authCoroutine = StartCoroutine(RequestAuthorizationiOS(pendingIosPermissionCallback));
#else
            tcs.TrySetResult(true); // Editor/unsupported platform
#endif

            cts.Token.Register(() =>
            {
                if (!tcs.Task.IsCompleted) tcs.TrySetCanceled();
            });

            try
            {
                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                LogWarning("Permission request timed out");
                throw new TimeoutException("Permission request timed out");
            }
        }

        int INotificationService.GetScheduledNotificationCount() => GetScheduledNotificationCount();

        public int GetScheduledNotificationCount()
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            dictLock.EnterReadLock();
            try
            {
                return scheduledNotificationIds.Count;
            }
            finally
            {
                dictLock.ExitReadLock();
            }
        }

        public IReadOnlyList<string> GetAllScheduledIdentifiers()
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            dictLock.EnterReadLock();
            try
            {
                return scheduledNotificationIds.Keys.ToArray();
            }
            finally
            {
                dictLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Sets the badge count shown on the app icon (iOS only)
        /// </summary>
        /// <param name="count">The badge count to display. Set to 0 to hide the badge</param>
        /// <remarks>
        /// On iOS, this updates the application badge count on the home screen icon.
        /// On other platforms, this method does nothing.
        /// The badge count is automatically synced with the internal currentBadgeCount variable.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Show badge with count 5
        /// NotificationServices.Instance.SetBadgeCount(5);
        /// 
        /// // Hide badge
        /// NotificationServices.Instance.SetBadgeCount(0);
        /// </code>
        /// </example>
        public void SetBadgeCount(int count)
        {
#if UNITY_IOS
        iOSNotificationCenter.ApplicationBadge = count;
        currentBadgeCount = count;
#endif
        }

#if UNITY_IOS
    /// <summary>
    /// Controls whether badge count auto-increments when sending notifications
    /// Default: false (manual badge management)
    /// </summary>
    public bool AutoIncrementBadge
    {
        get => autoIncrementBadge;
        set
        {
            autoIncrementBadge = value;
            if (currentLogLevel >= LogLevel.Info)
                Debug.Log($"[NotificationServices] AutoIncrementBadge set to {value}");
        }
    }
#endif

        /// <summary>
        /// Checks if the notification service has been initialized
        /// </summary>
        /// <returns>True if initialized, false otherwise</returns>
        public bool IsInitialized() => isInitialized;

        /// <summary>
        /// Forces an immediate save of all pending data to persistent storage
        /// </summary>
        /// <returns>Async task that completes when save is done</returns>
        public async Task ForceFlushSaveAsync() => await FlushSaveAsync().ConfigureAwait(false);

        /// <summary>
        /// Gets a snapshot of current performance metrics
        /// </summary>
        /// <returns>PerformanceMetrics object containing statistics about notifications, pooling, and errors</returns>
        /// <remarks>
        /// Useful for monitoring system health and debugging performance issues.
        /// Metrics include total notifications scheduled/cancelled, pool hit rate, and error count.
        /// </remarks>
        /// <example>
        /// <code>
        /// var metrics = NotificationServices.Instance.GetPerformanceMetrics();
        /// float poolHitRate = metrics.PoolHits * 100f / (metrics.PoolHits + metrics.PoolMisses);
        /// Debug.Log($"Pool efficiency: {poolHitRate:F1}%");
        /// </code>
        /// </example>
        public PerformanceMetrics GetPerformanceMetrics()
        {
            lock (metricsLock)
            {
                metrics.UpdateMemory(); // Update memory stats before returning
                return new PerformanceMetrics
                {
                    TotalScheduled = metrics.TotalScheduled,
                    TotalCancelled = metrics.TotalCancelled,
                    TotalErrors = metrics.TotalErrors,
                    PoolHits = metrics.PoolHits,
                    PoolMisses = metrics.PoolMisses,
                    MainThreadDrops = metrics.MainThreadDrops,
                    AverageSaveTimeMs = metrics.AverageSaveTimeMs,
                    StartTime = metrics.StartTime,
                    CurrentMemoryUsage = metrics.CurrentMemoryUsage,
                    PeakMemoryUsage = metrics.PeakMemoryUsage
                };
            }
        }

        public void ResetMetrics()
        {
            lock (metricsLock)
            {
                metrics.Reset();
            }
        }

        /// <summary>
        /// Exports performance metrics to JSON file for analysis
        /// </summary>
        /// <param name="path">Optional file path. If null, uses persistent data path with timestamp</param>
        /// <returns>The full path where metrics were exported</returns>
        /// <remarks>
        /// Useful for debugging performance issues and monitoring system health over time.
        /// Metrics include total notifications scheduled/cancelled, pool efficiency, error rates, and memory usage.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Auto-export on quit
        /// NotificationServices.Instance.DumpMetricsToFile();
        /// 
        /// // Export to specific location
        /// var path = Application.dataPath + "/../logs/metrics.json";
        /// NotificationServices.Instance.DumpMetricsToFile(path);
        /// </code>
        /// </example>
        public string DumpMetricsToFile(string path = null)
        {
            try
            {
                if (path == null)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    path = Path.Combine(Application.persistentDataPath, $"notification_metrics_{timestamp}.json");
                }

                var metrics = GetPerformanceMetrics();
                var json = JsonUtility.ToJson(metrics, true);
                File.WriteAllText(path, json);

                if (currentLogLevel >= LogLevel.Info) LogInfo("Performance metrics exported to", path);

                return path;
            }
            catch (Exception e)
            {
                RecordError("DumpMetricsToFile", e);
                LogError("Failed to export metrics", e.Message);
                return null;
            }
        }

        /// <summary>
        /// Clears all event handlers to prevent memory leaks
        /// </summary>
        /// <remarks>
        /// Call this when you want to ensure no event handlers are holding references.
        /// Useful for testing or when manually managing lifecycle.
        /// </remarks>
        public void ClearAllEventHandlers()
        {
            lock (eventLock)
            {
                _onNotificationEvent = null;
                notificationEventSubscriberCount = 0;
            }

            lock (errorEventLock)
            {
                _onError = null;
                errorEventSubscriberCount = 0;
            }

            LogInfo("Cleared all event handlers", 0);
        }

        /// <summary>
        /// Gets the number of subscribers to notification events (for debugging)
        /// Optimized: Uses counter instead of GetInvocationList() to avoid memory allocation
        /// </summary>
        public int GetEventSubscriberCount()
        {
            // Use volatile counter for zero-allocation subscriber count
            return notificationEventSubscriberCount + errorEventSubscriberCount;
        }

        #endregion

        #region Async API

        public async Task<bool> SendNotificationAsync(string title, string body, int fireTimeInSeconds,
            string identifier = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timeoutCts = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<bool>();
            RunOnMainThread(() =>
            {
                try
                {
                    var result = SendNotification(title, body, fireTimeInSeconds, identifier);
                    tcs.TrySetResult(result);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });

            var registration = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                var timeoutTask = Task.Delay(Timeouts.AsyncOperationTimeoutSeconds * 1000, timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    tcs.TrySetCanceled();
                    LogWarning("SendNotificationAsync timed out after", Timeouts.AsyncOperationTimeoutSeconds);
                    throw new TimeoutException("Operation timed out");
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                timeoutCts.Cancel(); // Stop delay timer immediately
                registration.Dispose();
            }
        }

        public async Task<bool> SendNotificationAsync(NotificationData data, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timeoutCts = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<bool>();
            RunOnMainThread(() =>
            {
                try
                {
                    SendNotification(data);
                    tcs.TrySetResult(true);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });

            var registration = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                var timeoutTask = Task.Delay(Timeouts.AsyncOperationTimeoutSeconds * 1000, timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    tcs.TrySetCanceled();
                    LogWarning("SendNotificationAsync (data) timed out after", Timeouts.AsyncOperationTimeoutSeconds);
                    throw new TimeoutException("Operation timed out");
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                timeoutCts.Cancel();
                registration.Dispose();
            }
        }

        public async Task CancelNotificationAsync(string identifier, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timeoutCts = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<bool>();
            RunOnMainThread(() =>
            {
                try
                {
                    CancelNotification(identifier);
                    tcs.TrySetResult(true);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });

            var registration = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                var timeoutTask = Task.Delay(Timeouts.AsyncOperationTimeoutSeconds * 1000, timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    tcs.TrySetCanceled();
                    LogWarning("CancelNotificationAsync timed out after", Timeouts.AsyncOperationTimeoutSeconds);
                    throw new TimeoutException("Operation timed out");
                }

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                timeoutCts.Cancel();
                registration.Dispose();
            }
        }

        public async Task<int> GetScheduledNotificationCountAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timeoutCts = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<int>();
            RunOnMainThread(() =>
            {
                try
                {
                    var count = GetScheduledNotificationCount();
                    tcs.TrySetResult(count);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });

            var registration = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                var timeoutTask = Task.Delay(Timeouts.AsyncOperationTimeoutSeconds * 1000, timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Cancel pending task to prevent leak
                    tcs.TrySetCanceled();
                    LogWarning("GetScheduledNotificationCountAsync timed out after",
                        Timeouts.AsyncOperationTimeoutSeconds);
                    throw new TimeoutException("Operation timed out");
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                timeoutCts.Cancel();
                registration.Dispose();
            }
        }

        #endregion

        #region App Lifecycle

        private void OnAppBackgrounded()
        {
            SaveLastOpenTime();
            ScheduleReturnNotification();
            _ = FlushSaveAsync(); // ConfigureAwait not needed in Unity - sync context required
        }

        private void OnAppForegrounded()
        {
            CheckInactivityAndSchedule();
            SaveLastOpenTime();
            InvalidateDateTimeCache();

            // CRITICAL: Refresh permission state in case user changed it in Settings
            StartCoroutine(RefreshPermissionIfChanged());

            try
            {
#if UNITY_ANDROID
            // Android notification APIs don't work in Editor, wrap in try-catch
            try
            {
                AndroidNotificationCenter.CancelAllDisplayedNotifications();
            }
            catch (System.Exception)
            {
                // Silently fail in Editor where Android APIs don't work
            }
#elif UNITY_IOS
            // iOS notification center is only available on iOS devices
            // Wrap in try-catch to handle Editor case gracefully
            try
            {
                iOSNotificationCenter.RemoveAllDeliveredNotifications();
                iOSNotificationCenter.ApplicationBadge = 0;
                currentBadgeCount = 0;
            }
            catch (System.Exception)
            {
                // Silently fail in Editor where iOS APIs don't work
            }
#endif
                // Check if returnConfig is configured before attempting to cancel
                if (returnConfig != null)
                {
                    CancelNotification(returnConfig.identifier);
                    CancelNotification(returnConfig.identifier + "_urgent");
                }
            }
            catch (Exception e)
            {
                RecordError("OnAppForegrounded", e);
                LogError("Failed to clear notifications", e.Message);
            }
        }

        /// <summary>
        /// Refreshes permission state to detect changes made in Settings while app was backgrounded
        /// </summary>
        private IEnumerator RefreshPermissionIfChanged()
        {
#if UNITY_ANDROID
        bool oldPermission = hasAndroidPermission;
        hasAndroidPermission = Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS");
        
        if (oldPermission != hasAndroidPermission)
        {
            Debug.Log($"[NotificationServices] Permission changed: {hasAndroidPermission}");
            DispatchEvent(hasAndroidPermission ? NotificationEvent.EventType.PermissionGranted 
                                              : NotificationEvent.EventType.PermissionDenied, "", "");
        }
#elif UNITY_IOS
        // Check authorization status (must be on main thread)
        var authStatus = iOSNotificationCenter.GetAuthorizationStatus();
        bool oldPermission = hasIosPermission;
        hasIosPermission = (authStatus == AuthorizationStatus.Authorized);
        
        if (oldPermission != hasIosPermission)
        {
            Debug.Log($"[NotificationServices] Permission changed: {hasIosPermission}");
            DispatchEvent(hasIosPermission ? NotificationEvent.EventType.PermissionGranted 
                                           : NotificationEvent.EventType.PermissionDenied, "", "");
        }
#endif
            yield return null;
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Schedules multiple notifications in a single batch operation
        /// </summary>
        /// <param name="notifications">List of NotificationData objects to schedule</param>
        /// <remarks>
        /// Efficiently schedules multiple notifications at once. If the batch size exceeds the limit,
        /// it will be automatically processed in chunks. Returns immediately after queueing all valid notifications.
        /// </remarks>
        /// <example>
        /// <code>
        /// var notifications = new List&lt;NotificationData&gt;
        /// {
        ///     new NotificationData { title = "Task 1", body = "Do this", fireTimeInSeconds = 3600 },
        ///     new NotificationData { title = "Task 2", body = "Do that", fireTimeInSeconds = 7200 }
        /// };
        /// NotificationServices.Instance.SendNotificationBatch(notifications);
        /// </code>
        /// </example>
        public void SendNotificationBatch(List<NotificationData> notifications)
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            if (notifications is not { Count: > 0 }) return;

            if (notifications.Count > Limits.MaxBatchSize)
            {
                LogWarning("Batch size exceeds limit, processing in chunks", Limits.MaxBatchSize);
            }

            int successCount = 0;
            int processedCount = 0;

            foreach (var data in notifications)
            {
                if (processedCount >= Limits.MaxBatchSize)
                {
                    LogWarning("Reached batch size limit, processed", processedCount, notifications.Count);
                    break;
                }

                if (data.IsValid() && CanScheduleMore())
                {
                    var pooledData = GetPooledData();
                    pooledData.CopyFrom(data);
                    if (SendNotificationInternal(pooledData)) successCount++;
                    ReturnToPool(pooledData);
                }

                processedCount++;
            }

            LogInfo("Batch scheduled", successCount);
            if (processedCount < notifications.Count) LogWarning("Only processed", processedCount, notifications.Count);

            MarkDirty();
        }

        public void CancelNotificationBatch(List<string> identifiers)
        {
            ThrowIfDisposed(); // Guard against disposed/quitting state
            if (identifiers is not { Count: > 0 }) return;

            if (identifiers.Count > Limits.MaxBatchSize)
            {
                LogWarning("Cancel batch size exceeds limit", Limits.MaxBatchSize);
                identifiers = identifiers.Take(Limits.MaxBatchSize).ToList();
            }

            var idsToCancel = new List<(string identifier, int id)>(identifiers.Count);

            // Batch read/write in single lock
            dictLock.EnterWriteLock();
            try
            {
                foreach (var identifier in identifiers)
                {
                    if (scheduledNotificationIds.TryGetValue(identifier, out var value))
                    {
                        insertionOrder.Remove(value.node);
                        scheduledNotificationIds.Remove(identifier);
                        idsToCancel.Add((identifier, value.id));
                    }
                }
            }
            finally
            {
                dictLock.ExitWriteLock();
            }

            // Cancel outside lock
            foreach (var (identifier, id) in idsToCancel)
            {
                try
                {
#if UNITY_ANDROID
                AndroidNotificationCenter.CancelScheduledNotification(id);
#elif UNITY_IOS
                iOSNotificationCenter.RemoveScheduledNotification(identifier);
#endif
                    RemoveFromGroup(identifier);
                }
                catch (Exception e)
                {
                    RecordError("CancelNotificationBatch", e);
                    LogError("Failed to cancel in batch", e.Message);
                }
            }

            if (idsToCancel.Count > 0)
            {
                MarkDirty();
                LogInfo("Cancelled batch", idsToCancel.Count);
#if UNITY_IOS
            InvalidateIOSNotificationCache(); // Invalidate cache after batch cancel
#endif
            }
        }

        public async Task SendNotificationBatchAsync(List<NotificationData> notifications,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timeoutCts = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<bool>();
            RunOnMainThread(() =>
            {
                try
                {
                    SendNotificationBatch(notifications);
                    tcs.TrySetResult(true);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });

            var registration = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                var timeoutTask = Task.Delay(Timeouts.AsyncOperationTimeoutSeconds * 1000, timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    tcs.TrySetCanceled();
                    LogWarning("SendNotificationBatchAsync timed out after", Timeouts.AsyncOperationTimeoutSeconds);
                    throw new TimeoutException("Operation timed out");
                }

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                timeoutCts.Cancel();
                registration.Dispose();
            }
        }

        public async Task CancelNotificationBatchAsync(List<string> identifiers, CancellationToken ct = default)
        {
            ThrowIfDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timeoutCts = new CancellationTokenSource();

            var tcs = new TaskCompletionSource<bool>();
            RunOnMainThread(() =>
            {
                try
                {
                    CancelNotificationBatch(identifiers);
                    tcs.TrySetResult(true);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            });

            var registration = cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                var timeoutTask = Task.Delay(Timeouts.AsyncOperationTimeoutSeconds * 1000, timeoutCts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    tcs.TrySetCanceled();
                    LogWarning("CancelNotificationBatchAsync timed out after", Timeouts.AsyncOperationTimeoutSeconds);
                    throw new TimeoutException("Operation timed out");
                }

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                timeoutCts.Cancel();
                registration.Dispose();
            }
        }

        #endregion

        #region Debug

        public Dictionary<string, object> GetDebugInfo()
        {
            int scheduledCount, groupCount;
            dictLock.EnterReadLock();
            try
            {
                scheduledCount = scheduledNotificationIds.Count;
            }
            finally
            {
                dictLock.ExitReadLock();
            }

            lock (groupsLock)
            {
                groupCount = notificationGroups.Count;
            }

            var info = new Dictionary<string, object>
            {
                ["Initialized"] = isInitialized,
                ["ScheduledCount"] = scheduledCount,
                ["HasPermission"] = HasNotificationPermission(),
                ["HoursSinceLastOpen"] = GetHoursSinceLastOpen(),
                ["ReturnNotificationEnabled"] = returnConfig?.enabled ?? false,
                ["Platform"] = Application.platform.ToString(),
                ["MaxNotifications"] = IS_IOS ? Limits.IosMaxNotifications : Limits.AndroidMaxNotifications,
                ["CircuitBreakerOpen"] = IsCircuitBreakerOpen(),
                ["ConsecutiveErrors"] = consecutiveErrors,
                ["GroupCount"] = groupCount
            };

#if UNITY_IOS
        info["BadgeCount"] = currentBadgeCount;  // #if already ensures iOS
#endif
            return info;
        }

        public void LogDebugInfo()
        {
            var info = GetDebugInfo();
            var builder = GetThreadLogBuilder(); // Already cleared
            builder.Append("=== NotificationServices Debug ===\n");
            foreach (var kvp in info)
            {
                builder.Append(kvp.Key);
                builder.Append(": ");
                builder.Append(kvp.Value);
                builder.Append('\n');
            }

            builder.Append("===================================");
            Debug.Log(builder.ToString());
        }

        /// <summary>
        /// Logs current pool statistics for debugging object pool efficiency
        /// </summary>
        public void LogPoolStats()
        {
            lock (poolLock)
            {
                var builder = GetThreadLogBuilder(); // Already cleared
                builder.Append("[NotificationServices] Pool Stats:\n");
                builder.Append("  NotificationData: ")
                    .Append(notificationDataPool.Count)
                    .Append("/")
                    .Append(PoolSizes.NotificationData)
                    .Append('\n');
                builder.Append("  Events: ").Append(eventPool.Count).Append("/").Append(PoolSizes.Events);
                Debug.Log(builder.ToString());
            }

            // Also log metrics
            var metrics = GetPerformanceMetrics();
            if (metrics.PoolHits + metrics.PoolMisses > 0)
            {
                float hitRate = metrics.PoolHits * 100f / (metrics.PoolHits + metrics.PoolMisses);
                Debug.Log($"[NotificationServices] Pool hit rate: {hitRate:F1}%");
            }
        }

        #endregion

        #region Extensions

        public void SendNotification(string title, string body, TimeSpan delay, string identifier = null) =>
            SendNotification(title, body, (int)delay.TotalSeconds, identifier);

        public void SendNotificationAt(string title, string body, DateTime scheduledTime, string identifier = null)
        {
            var delay = scheduledTime - DateTime.UtcNow;
            if (delay.TotalSeconds < 0) return;
            SendNotification(title, body, (int)delay.TotalSeconds, identifier);
        }

        public bool IsNotificationScheduled(string identifier)
        {
            ThrowIfDisposed();
            dictLock.EnterReadLock();
            try
            {
                return scheduledNotificationIds.ContainsKey(identifier);
            }
            finally
            {
                dictLock.ExitReadLock();
            }
        }

        public string GetNotificationStatus(string identifier)
        {
            ThrowIfDisposed();
            int id = 0;
            bool found;
            dictLock.EnterReadLock();
            try
            {
                found = scheduledNotificationIds.TryGetValue(identifier, out var value);
                if (found) id = value.id;
            }
            finally
            {
                dictLock.ExitReadLock();
            }

            if (!found) return "Not Found";

#if UNITY_ANDROID
        try { return AndroidNotificationCenter.CheckScheduledNotificationStatus(id).ToString(); }
        catch (Exception e) { LogError("Failed to get status", e.Message); return "Error"; }
#elif UNITY_IOS
        try
        {
            // Use cached notifications to avoid repeated system queries
            GetCachedIOSNotifications(out var scheduled, out var delivered);
            
            // Check if scheduled
            if (scheduled.Any(n => n.Identifier == identifier))
                return "Scheduled";
            
            // Check if delivered
            if (delivered.Any(n => n.Identifier == identifier))
                return "Delivered";
            
            // Not found in either list
            return "Not Found";
        }
        catch (Exception e) { LogError("Failed to get iOS status", e.Message); return "Error"; }
#else
            return "Unknown";
#endif
        }

        #endregion
    }
}