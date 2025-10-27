using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

/// <summary>
/// Comprehensive demo script showing all NotificationServices features
/// </summary>
public class NotificationServicesDemo : MonoBehaviour
{
    [Header("UI References")]
    public Button basicNotifyBtn;
    public Button fluentBuilderBtn;
    public Button asyncBtn;
    public Button batchBtn;
    public Button returnNotifyBtn;
    public Button cancelAllBtn;
    public Button permissionBtn;
    public Button metricsBtn;
    public Button clearBadgeBtn;
    public Text statusText;
    public Text metricsText;

    private int notificationCounter = 0;

    private void Start()
    {
        SetupButtons();
        SubscribeToEvents();
        
        // Display current status
        UpdateStatus();
        
        Debug.Log("[NotificationDemo] Demo started. All features available.");
    }

    private void SetupButtons()
    {
        if (basicNotifyBtn != null)
            basicNotifyBtn.onClick.AddListener(TestBasicNotification);
        
        if (fluentBuilderBtn != null)
            fluentBuilderBtn.onClick.AddListener(TestFluentBuilder);
        
        if (asyncBtn != null)
            asyncBtn.onClick.AddListener(TestAsyncAPI);
        
        if (batchBtn != null)
            batchBtn.onClick.AddListener(TestBatchOperations);
        
        if (returnNotifyBtn != null)
            returnNotifyBtn.onClick.AddListener(TestReturnNotification);
        
        if (cancelAllBtn != null)
            cancelAllBtn.onClick.AddListener(CancelAllNotifications);
        
        if (permissionBtn != null)
            permissionBtn.onClick.AddListener(RequestPermission);
        
        if (metricsBtn != null)
            metricsBtn.onClick.AddListener(ShowMetrics);
        
        if (clearBadgeBtn != null)
        {
            clearBadgeBtn.onClick.AddListener(ClearBadge);
            #if !UNITY_IOS
            clearBadgeBtn.gameObject.SetActive(false); // iOS only
            #endif
        }
    }

    private void SubscribeToEvents()
    {
        if (NotificationServices.Instance != null)
        {
            NotificationServices.Instance.OnNotificationEvent += HandleNotificationEvent;
            NotificationServices.Instance.OnError += HandleError;
        }
    }

    private void OnDestroy()
    {
        if (NotificationServices.Instance != null)
        {
            NotificationServices.Instance.OnNotificationEvent -= HandleNotificationEvent;
            NotificationServices.Instance.OnError -= HandleError;
        }
    }

    #region Feature Tests

    /// <summary>
    /// Demo: Basic notification scheduling
    /// </summary>
    public void TestBasicNotification()
    {
        try
        {
            notificationCounter++;
            string identifier = $"basic_notif_{notificationCounter}";
            string title = "Basic Notification";
            string body = $"This is notification #{notificationCounter}";
            int delaySeconds = 5; // Show in 5 seconds

            bool success = NotificationServices.Instance.SendNotification(
                title, 
                body, 
                delaySeconds, 
                identifier
            );

            if (success)
            {
                ShowStatus($"✅ Basic notification scheduled! (ID: {identifier}, shows in {delaySeconds}s)");
            }
            else
            {
                ShowStatus("❌ Failed to schedule notification");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Fluent Builder API - advanced notification configuration
    /// </summary>
    public void TestFluentBuilder()
    {
        try
        {
            notificationCounter++;
            string identifier = $"fluent_notif_{notificationCounter}";

            bool success = NotificationServices.Instance.CreateNotification()
                .WithTitle("🎮 Fluent Builder API")
                .WithBody($"Configured via fluent chain - #{notificationCounter}")
                .WithSubtitle("Advanced Configuration")
                .WithIdentifier(identifier)
                .In(TimeSpan.FromSeconds(10))
                .WithSound("default")
                .WithGroup("demo_group")
                .Schedule();

            if (success)
            {
                ShowStatus($"✅ Fluent builder notification scheduled! (ID: {identifier})");
            }
            else
            {
                ShowStatus("❌ Fluent builder failed");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Async API with cancellation support
    /// </summary>
    public async void TestAsyncAPI()
    {
        try
        {
            notificationCounter++;
            string identifier = $"async_notif_{notificationCounter}";

            ShowStatus("⏳ Scheduling async notification...");

            using var cts = new System.Threading.CancellationTokenSource();
            
            // Schedule async notification
            bool success = await NotificationServices.Instance.SendNotificationAsync(
                "Async Notification",
                $"Created using async/await - #{notificationCounter}",
                15,
                identifier,
                cts.Token
            );

            if (success)
            {
                ShowStatus($"✅ Async notification scheduled! (ID: {identifier})");
            }
            else
            {
                ShowStatus("❌ Async notification failed");
            }

            // Test async cancel
            await NotificationServices.Instance.CancelNotificationAsync(identifier, cts.Token);
            ShowStatus($"✅ Async cancellation completed!");
        }
        catch (OperationCanceledException)
        {
            ShowStatus("⏸️ Operation cancelled");
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Batch operations - send multiple notifications at once
    /// </summary>
    public void TestBatchOperations()
    {
        try
        {
            var notifications = new List<NotificationServices.NotificationData>();

            // Create batch of 5 notifications
            for (int i = 0; i < 5; i++)
            {
                var data = new NotificationServices.NotificationData
                {
                    title = $"Batch Notification #{i + 1}",
                    body = $"Created via batch operation",
                    fireTimeInSeconds = 20 + (i * 5), // Staggered times
                    identifier = $"batch_notif_{DateTime.Now:yyyyMMdd}_{i}",
                    groupKey = "batch_group",
                    smallIcon = "icon_small",
                    largeIcon = "icon_large"
                };
                notifications.Add(data);
            }

            NotificationServices.Instance.SendNotificationBatch(notifications);
            ShowStatus($"✅ Batch of {notifications.Count} notifications scheduled!");
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Return Notification - scheduled when app is backgrounded
    /// </summary>
    public void TestReturnNotification()
    {
        try
        {
            var config = new NotificationServices.ReturnNotificationConfig
            {
                enabled = true,
                title = "We miss you! 🎮",
                body = "Come back and claim your daily rewards!",
                hoursBeforeNotification = 24,
                repeating = false,
                repeatInterval = NotificationServices.RepeatInterval.None,
                identifier = "return_notification"
            };

            NotificationServices.Instance.ConfigureReturnNotification(config);
            ShowStatus("✅ Return notification configured! (Will trigger when app backgrounded for 24h)");
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Group management - cancel notifications by group
    /// </summary>
    public void TestGroupManagement()
    {
        try
        {
            // Schedule several notifications with same group
            for (int i = 0; i < 3; i++)
            {
                NotificationServices.Instance.SendNotification(
                    $"Group Notification {i + 1}",
                    "Part of test group",
                    30,
                    $"group_test_{i}"
                );
            }

            ShowStatus("✅ Group notifications scheduled! (3 notifications in 'test_group')");
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Repeating notifications
    /// </summary>
    public void TestRepeatingNotifications()
    {
        try
        {
            NotificationServices.Instance.SendRepeatingNotification(
                "Daily Reminder",
                "Check your progress!",
                3600, // 1 hour from now
                NotificationServices.RepeatInterval.Daily,
                "daily_reminder"
            );

            ShowStatus("✅ Daily repeating notification scheduled!");
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: iOS Badge management
    /// </summary>
    public void TestBadgeManagement()
    {
        try
        {
            #if UNITY_IOS
            // Set badge count
            int count = 5;
            NotificationServices.Instance.SetBadgeCount(count);
            ShowStatus($"✅ iOS Badge set to {count}");

            // Enable auto-increment
            NotificationServices.Instance.AutoIncrementBadge = true;
            ShowStatus("✅ Auto-increment badge enabled");
            #else
            ShowStatus("⏭️ Badge management is iOS-only");
            #endif
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Android Channel Configuration
    /// </summary>
    public void TestAndroidChannelConfig()
    {
        try
        {
            #if UNITY_ANDROID
            var config = new NotificationServices.AndroidChannelConfig
            {
                Importance = Importance.High,
                EnableVibration = true,
                EnableLights = true,
                EnableShowBadge = true,
                CanBypassDnd = false,
                Description = "Demo notification channel"
            };

            NotificationServices.Instance.SetAndroidChannelConfig(config);
            ShowStatus("✅ Android channel configured!");
            #else
            ShowStatus("⏭️ Channel config is Android-only");
            #endif
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    #endregion

    #region Utility Actions

    /// <summary>
    /// Cancel all scheduled notifications
    /// </summary>
    public void CancelAllNotifications()
    {
        try
        {
            NotificationServices.Instance.CancelAllScheduledNotifications();
            ShowStatus("✅ All scheduled notifications cancelled");
            UpdateStatus();
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Request notification permission
    /// </summary>
    public async void RequestPermission()
    {
        try
        {
            ShowStatus("⏳ Requesting permission...");
            
            bool granted = await NotificationServices.Instance.RequestPermissionAsync();
            
            if (granted)
            {
                ShowStatus("✅ Permission granted!");
            }
            else
            {
                ShowStatus("❌ Permission denied");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear iOS badge
    /// </summary>
    public void ClearBadge()
    {
        try
        {
            #if UNITY_IOS
            NotificationServices.Instance.SetBadgeCount(0);
            ShowStatus("✅ Badge cleared");
            #endif
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Display performance metrics
    /// </summary>
    public void ShowMetrics()
    {
        try
        {
            var metrics = NotificationServices.Instance.GetPerformanceMetrics();
            
            string metricsStr = $@"📊 Performance Metrics:
Total Scheduled: {metrics.TotalScheduled}
Total Cancelled: {metrics.TotalCancelled}
Total Errors: {metrics.TotalErrors}
Pool Hits: {metrics.PoolHits}
Pool Misses: {metrics.PoolMisses}
Pool Hit Rate: {(metrics.PoolHits + metrics.PoolMisses > 0 ? (metrics.PoolHits * 100f / (metrics.PoolHits + metrics.PoolMisses)): 0):F1}%
Main Thread Drops: {metrics.MainThreadDrops}
Avg Save Time: {metrics.AverageSaveTimeMs:F2}ms
Current Memory: {metrics.CurrentMemoryUsage / 1024}KB
Peak Memory: {metrics.PeakMemoryUsage / 1024}KB";

            ShowStatus(metricsStr);
            
            if (metricsText != null)
                metricsText.text = metricsStr;
        }
        catch (Exception ex)
        {
            ShowStatus($"❌ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Update status display with current notification count
    /// </summary>
    private void UpdateStatus()
    {
        try
        {
            int count = NotificationServices.Instance.GetScheduledNotificationCount();
            bool hasPermission = NotificationServices.Instance.HasNotificationPermission();
            
            string status = $"📱 Notifications: {count} scheduled | " +
                          $"Permission: {(hasPermission ? "✅" : "❌")}";
            
            if (statusText != null)
                statusText.text = status;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to update status: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    private void HandleNotificationEvent(NotificationServices.NotificationEvent evt)
    {
        string message = $"[{evt.Type}] {evt.Title}: {evt.Body}";
        Debug.Log(message);
        ShowStatus(message);
    }

    private void HandleError(string operation, Exception ex)
    {
        string message = $"❌ Error in {operation}: {ex.Message}";
        Debug.LogError(message);
        ShowStatus(message);
    }

    private void ShowStatus(string message)
    {
        Debug.Log($"[Demo] {message}");
        
        if (statusText != null)
        {
            statusText.text = message;
            StartCoroutine(ClearStatusAfterDelay());
        }
    }

    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        
        if (statusText != null)
            UpdateStatus();
    }

    #endregion

    #region Keyboard Shortcuts for Testing

    private void Update()
    {
        // DISABLED: Using Input System instead of old Input class
        // Keyboard shortcuts for quick testing - removed to avoid Input System conflict
        // Use UI buttons instead for testing
        /*
        if (Input.GetKeyDown(KeyCode.B))
        {
            TestBasicNotification();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            TestFluentBuilder();
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            TestAsyncAPI();
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            RequestPermission();
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            ShowMetrics();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            CancelAllNotifications();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            TestReturnNotification();
        }
        else if (Input.GetKeyDown(KeyCode.G))
        {
            TestGroupManagement();
        }
        */
    }

    private void OnGUI()
    {
        // DISABLED: Keyboard shortcuts removed due to Input System conflict
        // Use UI buttons instead for testing
        /*
        // Display keyboard shortcuts
        GUI.Label(new Rect(10, 10, 500, 200), 
            "⌨️ Keyboard Shortcuts:\n" +
            "B - Basic Notification\n" +
            "F - Fluent Builder\n" +
            "A - Async API\n" +
            "R - Return Notification\n" +
            "G - Group Management\n" +
            "P - Request Permission\n" +
            "M - Show Metrics\n" +
            "C - Cancel All");
        */
    }

    #endregion
}

