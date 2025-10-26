# Notification Services Demo

## Giới Thiệu
Script `NotificationServices` cung cấp một giải pháp toàn diện và mạnh mẽ để quản lý thông báo trong Unity, hỗ trợ đa nền tảng (Android, iOS).

## Tính Năng Chính
- Gửi thông báo đơn giản
- Thông báo lặp lại
- Quản lý nhóm thông báo
- Hỗ trợ builder pattern
- Xử lý sự kiện thông báo
- Kiểm tra và quản lý trạng thái thông báo

## Yêu Cầu
- Unity 2019.4 hoặc mới hơn
- Unity Notification Package
- Hỗ trợ Android/iOS

## Cài Đặt
1. Đảm bảo bạn đã cài đặt Unity Notification Package
2. Import các script:
   - `NotificationServices.cs`
   - `NotificationDemo.cs`

## Sử Dụng Cơ Bản

### Gửi Thông Báo Đơn Giản
```csharp
// Gửi thông báo sau 10 giây
NotificationServices.Instance.SendNotification(
    "Tiêu Đề", 
    "Nội Dung Thông Báo", 
    10
);
```

### Thông Báo Lặp Lại
```csharp
NotificationServices.Instance.SendRepeatingNotification(
    "Thông Báo Lặp", 
    "Nội dung lặp lại", 
    86400, // 1 ngày
    NotificationServices.RepeatInterval.Daily
);
```

### Sử Dụng Builder
```csharp
NotificationServices.Instance.CreateNotification()
    .WithTitle("Tiêu Đề")
    .WithBody("Nội Dung")
    .WithGroup("nhom_thong_bao")
    .In(TimeSpan.FromSeconds(30))
    .Schedule();
```

## Xử Lý Sự Kiện
```csharp
// Đăng ký sự kiện
NotificationServices.Instance.OnNotificationEvent += HandleNotificationEvent;

void HandleNotificationEvent(NotificationServices.NotificationEvent evt)
{
    switch (evt.Type)
    {
        case NotificationServices.NotificationEvent.EventType.Received:
            Debug.Log($"Nhận: {evt.Title}");
            break;
        case NotificationServices.NotificationEvent.EventType.Tapped:
            Debug.Log($"Nhấn: {evt.Title}");
            break;
    }
}
```

## Các Phương Thức Hữu Ích
- `CancelAllNotifications()`: Hủy tất cả thông báo
- `GetNotificationStatus(identifier)`: Kiểm tra trạng thái thông báo
- `LogDebugInfo()`: Ghi nhật ký thông tin debug

## Cấu Hình Thông Báo Quay Lại
```csharp
var returnConfig = new NotificationServices.ReturnNotificationConfig
{
    enabled = true,
    title = "Chúng tôi nhớ bạn!",
    body = "Quay lại và nhận phần thưởng",
    hoursBeforeNotification = 24
};

NotificationServices.Instance.ConfigureReturnNotification(returnConfig);
```

## Lưu Ý
- Kiểm tra quyền thông báo trước khi sử dụng
- Đảm bảo tuân thủ chính sách thông báo của từng nền tảng
- Giới hạn số lượng thông báo:
  - iOS: 64 thông báo
  - Android: 500 thông báo

## Khắc Phục Sự Cố
- Kiểm tra quyền thông báo
- Xác nhận cấu hình đúng cho từng nền tảng
- Sử dụng `LogDebugInfo()` để kiểm tra trạng thái

## Giấy Phép
[Thêm thông tin giấy phép của bạn]

## Liên Hệ
[Thêm thông tin liên hệ hỗ trợ]
