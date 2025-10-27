using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;

public class NotificationDemo : MonoBehaviour
{
    [Header("Notification Buttons")]
    public Button btnSimpleNotification;
    public Button btnRepeatingNotification;
    public Button btnBuilderNotification;
    public Button btnCancelAllNotifications;
    public Button btnCheckNotificationStatus;
    public Button btnConfigureReturnNotification;
    public Button btnLogDebugInfo;

    [Header("Notification Status Input")] public InputField inputNotificationIdentifier;
    public Text statusText;

    private NotificationServices notificationService;

    private void Awake()
    {
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        // Lấy instance của NotificationServices
        notificationService = NotificationServices.Instance;

        // Đăng ký sự kiện thông báo
        notificationService.OnNotificationEvent += HandleNotificationEvent;

        // Kiểm tra quyền thông báo
        if (!notificationService.HasNotificationPermission())
        {
            UpdateStatusText("Notification permission not granted");
            return;
        }

        // Gán sự kiện cho các nút
        btnSimpleNotification.onClick.AddListener(SendSimpleNotification);
        btnRepeatingNotification.onClick.AddListener(SendRepeatingNotification);
        btnBuilderNotification.onClick.AddListener(SendNotificationWithBuilder);
        btnCancelAllNotifications.onClick.AddListener(CancelAllNotifications);
        btnCheckNotificationStatus.onClick.AddListener(CheckNotificationStatus);
        btnConfigureReturnNotification.onClick.AddListener(ConfigureReturnNotification);
        btnLogDebugInfo.onClick.AddListener(LogDebugInfo);
    }

    async void SendSimpleNotification()
    {
        try
        {
            // Sử dụng async version để có thể cancel nếu cần
            bool success = await notificationService.SendNotificationAsync("Thông Báo Đơn Giản", "Đây là nội dung thông báo", 1);
            UpdateStatusText(success ? "Simple notification scheduled" : "Failed to schedule notification");
            if (success) Debug.Log("[NotificationDemo] Simple notification scheduled successfully");
        }
        catch (System.Exception ex)
        {
            UpdateStatusText($"Error scheduling notification: {ex.Message}");
            Debug.LogError($"[NotificationDemo] Failed to schedule simple notification: {ex.Message}");
        }
    }

    async void SendRepeatingNotification()
    {
        try
        {
            // Sử dụng async version
            var data = new NotificationServices.NotificationData
            {
                title = "Thông Báo Lặp Lại",
                body = "Thông báo này sẽ lặp lại hàng ngày",
                fireTimeInSeconds = 86400, // 1 ngày = 86400 giây
                repeats = true,
                repeatInterval = NotificationServices.RepeatInterval.Daily,
                identifier = Guid.NewGuid().ToString()
            };

            bool success = await notificationService.SendNotificationAsync(data);
            UpdateStatusText(success ? "Repeating notification scheduled" : "Failed to schedule repeating notification");
        }
        catch (System.Exception ex)
        {
            UpdateStatusText($"Error scheduling repeating notification: {ex.Message}");
        }
    }

    async void SendNotificationWithBuilder()
    {
        try
        {
            // Sử dụng fluent builder với async
            string identifier = Guid.NewGuid().ToString();
            bool success = await notificationService.CreateNotification()
                .WithTitle("Thông Báo Từ Builder")
                .WithBody("Được tạo bằng notification builder")
                .WithGroup("demo_group")
                .WithIdentifier(identifier)
                .In(1)  // Đơn giản hơn TimeSpan.FromSeconds(1)
                .ScheduleAsync();

            if (success)
                UpdateStatusText($"Builder notification scheduled (ID: {identifier})");
            else
                UpdateStatusText("Failed to schedule builder notification");
        }
        catch (System.Exception ex)
        {
            UpdateStatusText($"Error scheduling builder notification: {ex.Message}");
        }
    }

    void ConfigureReturnNotification()
    {
        try
        {
            // Cấu hình thông báo quay lại ứng dụng
            var returnConfig = new NotificationServices.ReturnNotificationConfig
            {
                enabled = true,
                title = "Chúng tôi nhớ bạn!",
                body = "Quay lại và nhận phần thưởng",
                hoursBeforeNotification = 24
            };

            notificationService.ConfigureReturnNotification(returnConfig);
            UpdateStatusText("Return notification configured");
        }
        catch (System.Exception ex)
        {
            UpdateStatusText($"Error configuring return notification: {ex.Message}");
        }
    }

    void HandleNotificationEvent(NotificationServices.NotificationEvent evt)
    {
        // Xử lý các sự kiện thông báo
        switch (evt.Type)
        {
            case NotificationServices.NotificationEvent.EventType.Received:
                UpdateStatusText($"Thông báo đã nhận: {evt.Title}");
                break;
            case NotificationServices.NotificationEvent.EventType.Tapped:
                UpdateStatusText($"Thông báo được nhấn: {evt.Title}");
                break;
            case NotificationServices.NotificationEvent.EventType.PermissionGranted:
                UpdateStatusText("Đã cấp quyền thông báo");
                break;
            case NotificationServices.NotificationEvent.EventType.PermissionDenied:
                UpdateStatusText("Quyền thông báo bị từ chối");
                break;
        }
    }

    async void CancelAllNotifications()
    {
        try
        {
            // Cancel all scheduled notifications
            var scheduledIds = notificationService.GetAllScheduledIdentifiers();
            if (scheduledIds.Count > 0)
            {
                await notificationService.CancelNotificationBatchAsync(scheduledIds.ToList());
                Debug.Log($"[NotificationDemo] Cancelled {scheduledIds.Count} scheduled notifications");
            }

            // Cancel all displayed notifications
            notificationService.CancelAllDisplayedNotifications();

            UpdateStatusText($"Tất cả thông báo đã bị hủy ({scheduledIds.Count} scheduled)");
        }
        catch (System.Exception ex)
        {
            UpdateStatusText($"Error canceling notifications: {ex.Message}");
        }
    }

    async void CheckNotificationStatus()
    {
        string identifier = inputNotificationIdentifier.text;
        if (string.IsNullOrEmpty(identifier))
        {
            UpdateStatusText("Vui lòng nhập mã định danh thông báo");
            return;
        }

        try
        {
            // Kiểm tra trạng thái của một thông báo cụ thể
            string status = notificationService.GetNotificationStatus(identifier);
            UpdateStatusText($"Trạng thái thông báo {identifier}: {status}");
        }
        catch (System.Exception ex)
        {
            UpdateStatusText($"Error checking notification status: {ex.Message}");
        }
    }

    void LogDebugInfo()
    {
        // Ghi nhật ký thông tin debug
        notificationService.LogDebugInfo();
        UpdateStatusText("Đã ghi nhật ký debug");
    }

    void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[NotificationDemo] {message}");
    }

    void OnDestroy()
    {
        // Hủy đăng ký sự kiện khi object bị destroy
        if (notificationService != null)
        {
            notificationService.OnNotificationEvent -= HandleNotificationEvent;
        }
    }
}