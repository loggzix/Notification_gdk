using System;
using System.Collections;
using System.Collections.Generic;
using DSDK.Notifications;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

/// <summary>
/// Script demo toàn diện thể hiện tất cả các tính năng của NotificationServices
/// </summary>
public class NotificationServicesDemo : MonoBehaviour
{
    [Header("UI Tham chiếu")]
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

    private void Awake(){
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        SetupButtons();
        SubscribeToEvents();
        
        // Hiển thị trạng thái hiện tại
        UpdateStatus();
        ShowMetrics();
        Debug.Log("[NotificationDemo] Demo đã bắt đầu. Tất cả tính năng đã sẵn sàng.");
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
            clearBadgeBtn.gameObject.SetActive(false); // Chỉ dành cho iOS
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
    /// Demo: Lập lịch thông báo cơ bản
    /// </summary>
    public void TestBasicNotification()
    {
        try
        {
            notificationCounter++;
            string identifier = $"basic_notif_{notificationCounter}";
            string title = "Thông báo cơ bản";
            string body = $"Đây là thông báo số #{notificationCounter}";
            int delaySeconds = 5; // Hiển thị sau 5 giây

            bool success = NotificationServices.Instance.SendNotification(
                title, 
                body, 
                delaySeconds, 
                identifier
            );

            if (success)
            {
                ShowStatus($"Đã lập lịch thông báo cơ bản! (ID: {identifier}, sẽ hiển thị sau {delaySeconds}s)");
            }
            else
            {
                ShowStatus("Không thể lập lịch thông báo");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Fluent Builder API - cấu hình thông báo nâng cao
    /// </summary>
    public void TestFluentBuilder()
    {
        try
        {
            notificationCounter++;
            string identifier = $"fluent_notif_{notificationCounter}";

            bool success = NotificationServices.Instance.CreateNotification()
                .WithTitle("Fluent Builder API")
                .WithBody($"Cấu hình qua chuỗi fluent - #{notificationCounter}")
                .WithSubtitle("Cấu hình nâng cao")
                .WithIdentifier(identifier)
                .In(TimeSpan.FromSeconds(10))
                .WithSound("default")
                .WithGroup("demo_group")
                .Schedule();

            if (success)
            {
                ShowStatus($" Đã lập lịch thông báo fluent builder! (ID: {identifier})");
            }
            else
            {
                ShowStatus(" Fluent builder thất bại");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($" Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Async API với hỗ trợ hủy bỏ
    /// </summary>
    public async void TestAsyncAPI()
    {
        try
        {
            notificationCounter++;
            string identifier = $"async_notif_{notificationCounter}";

            ShowStatus("Đang lập lịch thông báo async...");

            using var cts = new System.Threading.CancellationTokenSource();
            
            // Lập lịch thông báo async
            bool success = await NotificationServices.Instance.SendNotificationAsync(
                "Thông báo Async",
                $"Được tạo bằng async/await - #{notificationCounter}",
                15,
                identifier,
                cts.Token
            );

            if (success)
            {
                ShowStatus($"Đã lập lịch thông báo async! (ID: {identifier})");
            }
            else
            {
                ShowStatus("Thông báo async thất bại");
            }

            // Kiểm tra hủy async
            await NotificationServices.Instance.CancelNotificationAsync(identifier, cts.Token);
            ShowStatus($"Đã hoàn thành hủy bỏ async!");
        }
        catch (OperationCanceledException)
        {
            ShowStatus("Thao tác đã bị hủy");
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Thao tác hàng loạt - gửi nhiều thông báo cùng lúc
    /// </summary>
    public void TestBatchOperations()
    {
        try
        {
            var notifications = new List<NotificationServices.NotificationData>();

            // Tạo lô 5 thông báo
            for (int i = 0; i < 5; i++)
            {
                var data = new NotificationServices.NotificationData
                {
                    title = $"Thông báo hàng loạt #{i + 1}",
                    body = $"Được tạo qua thao tác hàng loạt",
                    fireTimeInSeconds = 20 + (i * 5), // Thời gian lệch nhau
                    identifier = $"batch_notif_{DateTime.Now:yyyyMMdd}_{i}",
                    groupKey = "batch_group",
                    smallIcon = "icon_small",
                    largeIcon = "icon_large"
                };
                notifications.Add(data);
            }

            NotificationServices.Instance.SendNotificationBatch(notifications);
            ShowStatus($"Đã lập lịch {notifications.Count} thông báo hàng loạt!");
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Thông báo trở lại - được lập lịch khi ứng dụng bị đưa vào nền
    /// </summary>
    public void TestReturnNotification()
    {
        try
        {
            var config = new NotificationServices.ReturnNotificationConfig
            {
                enabled = true,
                title = "Chúng tôi nhớ bạn!",
                body = "Quay lại và nhận phần thưởng hàng ngày của bạn!",
                hoursBeforeNotification = 24,
                repeating = false,
                repeatInterval = NotificationServices.RepeatInterval.None,
                identifier = "return_notification"
            };

            NotificationServices.Instance.ConfigureReturnNotification(config);
            ShowStatus("Đã cấu hình thông báo trở lại! (Sẽ kích hoạt khi app bị đưa vào nền 24h)");
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Quản lý nhóm - hủy thông báo theo nhóm
    /// </summary>
    public void TestGroupManagement()
    {
        try
        {
            // Lập lịch một số thông báo với cùng nhóm
            for (int i = 0; i < 3; i++)
            {
                NotificationServices.Instance.SendNotification(
                    $"Thông báo nhóm {i + 1}",
                    "Một phần của nhóm kiểm tra",
                    30,
                    $"group_test_{i}"
                );
            }

            ShowStatus("Đã lập lịch thông báo nhóm! (3 thông báo trong 'test_group')");
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Thông báo lặp lại
    /// </summary>
    public void TestRepeatingNotifications()
    {
        try
        {
            NotificationServices.Instance.SendRepeatingNotification(
                "Nhắc nhở hàng ngày",
                "Kiểm tra tiến độ của bạn!",
                3600, // Cách 1 giờ từ bây giờ
                NotificationServices.RepeatInterval.Daily,
                "daily_reminder"
            );

            ShowStatus("Đã lập lịch thông báo lặp lại hàng ngày!");
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Quản lý Badge iOS
    /// </summary>
    public void TestBadgeManagement()
    {
        try
        {
            #if UNITY_IOS
            // Đặt số badge
            int count = 5;
            NotificationServices.Instance.SetBadgeCount(count);
            ShowStatus($"iOS Badge được đặt thành {count}");

            // Bật tự động tăng
            NotificationServices.Instance.AutoIncrementBadge = true;
            ShowStatus("Đã bật tự động tăng badge");
            #else
            ShowStatus("Quản lý Badge chỉ dành cho iOS");
            #endif
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    /// <summary>
    /// Demo: Cấu hình Android Channel
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
                Description = "Kênh thông báo demo"
            };

            NotificationServices.Instance.SetAndroidChannelConfig(config);
            ShowStatus("Đã cấu hình kênh Android!");
            #else
            ShowStatus("Cấu hình kênh chỉ dành cho Android");
            #endif
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    #endregion

    #region Utility Actions

    /// <summary>
    /// Hủy tất cả thông báo đã lập lịch
    /// </summary>
    public void CancelAllNotifications()
    {
        try
        {
            NotificationServices.Instance.CancelAllScheduledNotifications();
            ShowStatus("Đã hủy tất cả thông báo đã lập lịch");
            UpdateStatus();
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Yêu cầu quyền thông báo
    /// </summary>
    public async void RequestPermission()
    {
        try
        {
            ShowStatus("Đang yêu cầu quyền...");
            
            bool granted = await NotificationServices.Instance.RequestPermissionAsync();
            
            if (granted)
            {
                ShowStatus("Đã cấp quyền!");
            }
            else
            {
                ShowStatus("Quyền bị từ chối");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Xóa badge iOS
    /// </summary>
    public void ClearBadge()
    {
        try
        {
            #if UNITY_IOS
            NotificationServices.Instance.SetBadgeCount(0);
            ShowStatus("Đã xóa badge");
            #endif
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Hiển thị các thông số hiệu năng
    /// </summary>
    public void ShowMetrics()
    {
        try
        {
            var metrics = NotificationServices.Instance.GetPerformanceMetrics();
            
            string metricsStr = $@"Thông số hiệu năng:
Tổng đã lập lịch: {metrics.TotalScheduled}
Tổng đã hủy: {metrics.TotalCancelled}
Tổng lỗi: {metrics.TotalErrors}
Pool Hits: {metrics.PoolHits}
Pool Misses: {metrics.PoolMisses}
Tỷ lệ pool hit: {(metrics.PoolHits + metrics.PoolMisses > 0 ? (metrics.PoolHits * 100f / (metrics.PoolHits + metrics.PoolMisses)): 0):F1}%
Main Thread Drops: {metrics.MainThreadDrops}
Thời gian lưu trung bình: {metrics.AverageSaveTimeMs:F2}ms
Bộ nhớ hiện tại: {metrics.CurrentMemoryUsage / 1024}KB
Bộ nhớ đỉnh: {metrics.PeakMemoryUsage / 1024}KB";
            
            if (metricsText != null)
                metricsText.text = metricsStr;
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}");
        }
    }

    /// <summary>
    /// Cập nhật hiển thị trạng thái với số lượng thông báo hiện tại
    /// </summary>
    private void UpdateStatus()
    {
        try
        {
            int count = NotificationServices.Instance.GetScheduledNotificationCount();
            bool hasPermission = NotificationServices.Instance.HasNotificationPermission();
            
            string status = $"Thông báo: {count} đã lập lịch | " +
                          $"Quyền: {(hasPermission ? "Có" : "Không")}";
            
            if (statusText != null)
                statusText.text = status;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Không thể cập nhật trạng thái: {ex.Message}");
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
        string message = $"Lỗi trong {operation}: {ex.Message}";
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
        // TẮT: Sử dụng Input System thay vì lớp Input cũ
        // Phím tắt để kiểm tra nhanh - đã loại bỏ để tránh xung đột Input System
        // Sử dụng nút UI thay thế để kiểm tra
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
        // TẮT: Phím tắt bị loại bỏ do xung đột Input System
        // Sử dụng nút UI thay thế để kiểm tra
        /*
        // Hiển thị phím tắt
        GUI.Label(new Rect(10, 10, 500, 200), 
            "Phím tắt:\n" +
            "B - Thông báo cơ bản\n" +
            "F - Fluent Builder\n" +
            "A - Async API\n" +
            "R - Thông báo trở lại\n" +
            "G - Quản lý nhóm\n" +
            "P - Yêu cầu quyền\n" +
            "M - Hiển thị thông số\n" +
            "C - Hủy tất cả");
        */
    }

    #endregion
}

