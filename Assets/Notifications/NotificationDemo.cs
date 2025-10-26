// using UnityEngine;
// using UnityEngine.UI;
// using System;
//
// public class NotificationDemo : MonoBehaviour
// {
//     [Header("Notification Buttons")]
//     public Button btnSimpleNotification;
//     public Button btnRepeatingNotification;
//     public Button btnBuilderNotification;
//     public Button btnCancelAllNotifications;
//     public Button btnCheckNotificationStatus;
//     public Button btnConfigureReturnNotification;
//     public Button btnLogDebugInfo;
//
//     [Header("Notification Status Input")] public InputField inputNotificationIdentifier;
//     public Text statusText;
//
//     private NotificationServices notificationService;
//
//     void Start()
//     {
//         // Lấy instance của NotificationServices
//         notificationService = NotificationServices.Instance;
//
//         // Đăng ký sự kiện thông báo
//         notificationService.OnNotificationEvent += HandleNotificationEvent;
//
//         // Kiểm tra quyền thông báo
//         if (!notificationService.HasNotificationPermission())
//         {
//             UpdateStatusText("Notification permission not granted");
//             return;
//         }
//
//         // Gán sự kiện cho các nút
//         btnSimpleNotification.onClick.AddListener(SendSimpleNotification);
//         btnRepeatingNotification.onClick.AddListener(SendRepeatingNotification);
//         btnBuilderNotification.onClick.AddListener(SendNotificationWithBuilder);
//         btnCancelAllNotifications.onClick.AddListener(CancelAllNotifications);
//         btnCheckNotificationStatus.onClick.AddListener(CheckNotificationStatus);
//         btnConfigureReturnNotification.onClick.AddListener(ConfigureReturnNotification);
//         btnLogDebugInfo.onClick.AddListener(LogDebugInfo);
//     }
//
//     void SendSimpleNotification()
//     {
//         // Gửi thông báo sau 10 giây
//         notificationService.SendNotification("Thông Báo Đơn Giản", "Đây là nội dung thông báo", 10);
//         UpdateStatusText("Simple notification scheduled");
//     }
//
//     void SendRepeatingNotification()
//     {
//         // Gửi thông báo lặp lại hàng ngày
//         notificationService.SendRepeatingNotification("Thông Báo Lặp Lại", "Thông báo này sẽ lặp lại hàng ngày",
//             86400, // 1 ngày = 86400 giây
//             NotificationServices.RepeatInterval.Daily);
//         UpdateStatusText("Repeating notification scheduled");
//     }
//
//     void SendNotificationWithBuilder()
//     {
//         // Sử dụng fluent builder để tạo thông báo
//         string identifier = Guid.NewGuid().ToString();
//         notificationService.CreateNotification()
//             .WithTitle("Thông Báo Từ Builder")
//             .WithBody("Được tạo bằng notification builder")
//             .WithGroup("demo_group")
//             .WithIdentifier(identifier)
//             .In(TimeSpan.FromSeconds(30))
//             .Schedule();
//         UpdateStatusText($"Builder notification scheduled (ID: {identifier})");
//     }
//
//     void ConfigureReturnNotification()
//     {
//         // Cấu hình thông báo quay lại ứng dụng
//         var returnConfig = new NotificationServices.ReturnNotificationConfig
//         {
//             enabled = true,
//             title = "Chúng tôi nhớ bạn!",
//             body = "Quay lại và nhận phần thưởng",
//             hoursBeforeNotification = 24
//         };
//
//         notificationService.ConfigureReturnNotification(returnConfig);
//         UpdateStatusText("Return notification configured");
//     }
//
//     void HandleNotificationEvent(NotificationServices.NotificationEvent evt)
//     {
//         // Xử lý các sự kiện thông báo
//         switch (evt.Type)
//         {
//             case NotificationServices.NotificationEvent.EventType.Received:
//                 UpdateStatusText($"Thông báo đã nhận: {evt.Title}");
//                 break;
//             case NotificationServices.NotificationEvent.EventType.Tapped:
//                 UpdateStatusText($"Thông báo được nhấn: {evt.Title}");
//                 break;
//             case NotificationServices.NotificationEvent.EventType.PermissionGranted:
//                 UpdateStatusText("Đã cấp quyền thông báo");
//                 break;
//             case NotificationServices.NotificationEvent.EventType.PermissionDenied:
//                 UpdateStatusText("Quyền thông báo bị từ chối");
//                 break;
//         }
//     }
//
//     void CancelAllNotifications()
//     {
//         notificationService.CancelAllNotifications();
//         UpdateStatusText("Tất cả thông báo đã bị hủy");
//     }
//
//     void CheckNotificationStatus()
//     {
//         string identifier = inputNotificationIdentifier.text;
//         if (string.IsNullOrEmpty(identifier))
//         {
//             UpdateStatusText("Vui lòng nhập mã định danh thông báo");
//             return;
//         }
//
//         // Kiểm tra trạng thái của một thông báo cụ thể
//         string status = notificationService.GetNotificationStatus(identifier);
//         UpdateStatusText($"Trạng thái thông báo {identifier}: {status}");
//     }
//
//     void LogDebugInfo()
//     {
//         // Ghi nhật ký thông tin debug
//         notificationService.LogDebugInfo();
//         UpdateStatusText("Đã ghi nhật ký debug");
//     }
//
//     void UpdateStatusText(string message)
//     {
//         if (statusText != null)
//         {
//             statusText.text = message;
//         }
//
//         Debug.Log($"[NotificationDemo] {message}");
//     }
//
//     void OnApplicationQuit()
//     {
//         // Hủy đăng ký sự kiện khi thoát
//         if (notificationService != null)
//         {
//             notificationService.OnNotificationEvent -= HandleNotificationEvent;
//         }
//     }
// }