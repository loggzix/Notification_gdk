# Tích hợp và sử dụng Notifications trong GDK

## Mục lục

1. [Giới thiệu về Notifications](#giới-thiệu-về-notifications)
2. [Setup trong Unity](#setup-trong-unity)
3. [API Cơ bản](#api-cơ-bản)
4. [Fluent Builder API](#fluent-builder-api)
5. [Async API](#async-api)
6. [Quản lý Permissions](#quản-lý-permissions)
7. [Return Notifications](#return-notifications)
8. [Events và Callbacks](#events-và-callbacks)
9. [Batch Operations](#batch-operations)
10. [Debug và Performance](#debug-và-performance)
11. [Ví dụ Sử dụng](#ví-dụ-sử-dụng)

---

## 1. Giới thiệu về Notifications

### Local Notifications

**Local Notifications** là thông báo được lên lịch và gửi từ chính thiết bị, không cần server. NotificationServices hỗ trợ:

- ✅ Lên lịch notification ở thời điểm cụ thể
- ✅ Repeat notifications (Daily, Weekly)
- ✅ Custom icons, sounds, badges
- ✅ Group notifications
- ✅ Hỗ trợ iOS và Android
- ✅ Permission handling tự động
- ✅ Thread-safe và high-performance

### Remote Notifications

**Remote Notifications** (Push Notifications) là thông báo được gửi từ server qua Firebase/APNs. 

> **Lưu ý**: NotificationServices hiện tại tập trung vào **Local Notifications**. Để sử dụng Remote Notifications, bạn cần tích hợp Firebase (Android) hoặc APNs (iOS) riêng.

---

## 2. Setup trong Unity

### 2.1. Yêu cầu

- **Unity Version**: 2020.3 LTS trở lên
- **Unity Packages**: 
  - `com.unity.mobile.notifications` (Android & iOS)

### 2.2. Cài đặt Package

1. Mở **Window > Package Manager**
2. Click **+** > **Add package by name**
3. Thêm: `com.unity.mobile.notifications`
4. Click **Add**

### 2.3. Import vào Project

NotificationServices đã được design để tự động khởi tạo khi ứng dụng chạy:

```csharp
// Không cần thêm code gì! 
// Instance được tự động tạo trong BootstrapNotificationServices()
```

### 2.4. Android Configuration

#### Manifest Permissions

Thêm vào `Assets/Plugins/Android/AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
```

#### Notification Channel (Android 8.0+)

NotificationServices tự động tạo channel mặc định. Nếu muốn custom:

```csharp
var config = new AndroidChannelConfig
{
    Importance = Importance.High,
    EnableVibration = true,
    EnableLights = true,
    EnableShowBadge = true,
    Description = "Custom channel description"
};
```

### 2.5. iOS Configuration

#### Capabilities

1. Mở **Edit > Project Settings**
2. Chọn **iOS** tab
3. Enable **Push Notifications**
4. Enable **Remote notifications** trong Background Modes

#### Info.plist

NotificationServices tự động xử lý permissions và authorization.

---

## 3. API Cơ bản

### 3.1. Gửi Notification đơn giản

```csharp
using DSDK.Notifications;

// Gửi notification sau 1 giờ
bool success = NotificationServices.Instance.SendNotification(
    "Reminder",                    // title
    "Don't forget to check!",       // body
    3600,                           // delay (seconds)
    "daily_reminder"               // identifier (optional)
);
```

### 3.2. Gửi Repeating Notification

```csharp
// Thông báo lặp lại mỗi ngày
NotificationServices.Instance.SendRepeatingNotification(
    "Daily Check-in",
    "Time to play!",
    3600,                           // 1 giờ từ bây giờ
    RepeatInterval.Daily,           // lặp lại hàng ngày
    "daily_check"                   // identifier
);
```

### 3.3. Hủy Notification

```csharp
// Hủy một notification cụ thể
NotificationServices.Instance.CancelNotification("daily_reminder");

// Hủy TẤT CẢ notifications
NotificationServices.Instance.CancelAllNotifications();
```

### 3.4. Kiểm tra số lượng Notifications

```csharp
int count = NotificationServices.Instance.GetScheduledNotificationCount();
Debug.Log($"Scheduled: {count}");

// Lấy danh sách identifiers
var identifiers = NotificationServices.Instance.GetAllScheduledIdentifiers();
```

### 3.5. Kiểm tra Notification đã được schedule chưa

```csharp
bool exists = NotificationServices.Instance.IsNotificationScheduled("daily_reminder");
```

---

## 4. Fluent Builder API

Fluent Builder API giúp code dễ đọc và maintain hơn:

### 4.1. Basic Builder

```csharp
NotificationServices.Instance.CreateNotification()
    .WithTitle("Welcome!")
    .WithBody("Thanks for installing!")
    .In(TimeSpan.FromHours(24))
    .Schedule();
```

### 4.2. Advanced Builder

```csharp
NotificationServices.Instance.CreateNotification()
    .WithTitle("New Reward")
    .WithBody("Claim your daily bonus!")
    .WithSubtitle("Daily Bonus")           // iOS only
    .WithGroup("rewards")                   // Group key
    .WithBadge(1)                           // Badge count
    .WithSound("notification_sound")       // Custom sound
    .WithIdentifier("reward_001")
    .In(TimeSpan.FromDays(1))
    .Repeating(RepeatInterval.Daily)
    .Schedule();
```

### 4.3. Time-based Scheduling

```csharp
// Schedule tại thời điểm cụ thể
var tomorrow = DateTime.Now.AddDays(1).Date.AddHours(9);
NotificationServices.Instance.CreateNotification()
    .WithTitle("Morning Reminder")
    .WithBody("Time to check in!")
    .At(tomorrow)
    .Schedule();

// Hoặc delay theo giây
NotificationServices.Instance.CreateNotification()
    .WithTitle("Quick Reminder")
    .WithBody("Don't forget!")
    .In(3600)  // 1 hour
    .Schedule();
```

### 4.4. Async Builder

```csharp
await NotificationServices.Instance.CreateNotification()
    .WithTitle("Async Notification")
    .WithBody("This uses async/await")
    .In(3600)
    .ScheduleAsync();
```

---

## 5. Async API

NotificationServices cung cấp đầy đủ Async API cho async/await pattern:

### 5.1. SendNotificationAsync

```csharp
try
{
    bool success = await NotificationServices.Instance.SendNotificationAsync(
        "Async Title",
        "Async Body",
        3600,
        "async_001"
    );
}
catch (TimeoutException)
{
    Debug.LogError("Operation timed out");
}
```

### 5.2. CancelNotificationAsync

```csharp
await NotificationServices.Instance.CancelNotificationAsync("daily_reminder");
```

### 5.3. GetCountAsync

```csharp
int count = await NotificationServices.Instance.GetScheduledNotificationCountAsync();
```

### 5.4. RequestPermissionAsync

```csharp
bool granted = await NotificationServices.Instance.RequestPermissionAsync();
if (granted)
{
    Debug.Log("Permission granted!");
}
```

---

## 6. Quản lý Permissions

### 6.1. Kiểm tra Permission

```csharp
bool hasPermission = NotificationServices.Instance.HasNotificationPermission();
```

### 6.2. Request Permission (Async)

```csharp
try
{
    bool granted = await NotificationServices.Instance.RequestPermissionAsync();
    
    if (granted)
    {
        Debug.Log("User granted notification permission");
        // Schedule your notifications
    }
    else
    {
        Debug.LogWarning("User denied notification permission");
        // Show explanation or alternative
    }
}
catch (TimeoutException)
{
    Debug.LogError("Permission request timed out");
}
```

### 6.3. Listen for Permission Changes

```csharp
void Start()
{
    NotificationServices.Instance.OnNotificationEvent += OnNotificationEvent;
}

void OnNotificationEvent(NotificationEvent evt)
{
    switch (evt.Type)
    {
        case NotificationEvent.EventType.PermissionGranted:
            Debug.Log("Permission granted!");
            break;
        
        case NotificationEvent.EventType.PermissionDenied:
            Debug.LogWarning("Permission denied!");
            break;
    }
}

void OnDestroy()
{
    NotificationServices.Instance.OnNotificationEvent -= OnNotificationEvent;
}
```

---

## 7. Return Notifications

Return Notifications tự động gửi notification khi người chơi không mở app trong một khoảng thời gian:

### 7.1. Configure Return Notification

```csharp
var config = new ReturnNotificationConfig
{
    enabled = true,
    title = "We miss you!",
    body = "Come back and claim rewards!",
    hoursBeforeNotification = 24,      // Send after 24 hours
    repeating = true,
    repeatInterval = RepeatInterval.Daily,
    identifier = "return_notification"
};

NotificationServices.Instance.ConfigureReturnNotification(config);
```

### 7.2. Enable/Disable Return Notification

```csharp
// Enable
NotificationServices.Instance.SetReturnNotificationEnabled(true);

// Disable
NotificationServices.Instance.SetReturnNotificationEnabled(false);
```

### 7.3. Automatic Scheduling

Return notification tự động được schedule khi:
- App bị background (OnApplicationPause)
- Người chơi không mở app sau khoảng thời gian cấu hình

---

## 8. Events và Callbacks

### 8.1. OnNotificationEvent

Event này được trigger khi có notification-related events:

```csharp
void Start()
{
    NotificationServices.Instance.OnNotificationEvent += HandleNotificationEvent;
}

void HandleNotificationEvent(NotificationEvent evt)
{
    switch (evt.Type)
    {
        case NotificationEvent.EventType.Received:
            Debug.Log($"Received: {evt.Title}");
            break;
        
        case NotificationEvent.EventType.Tapped:
            Debug.Log($"Tapped: {evt.Title}");
            HandleNotificationTapped(evt);
            break;
        
        case NotificationEvent.EventType.PermissionGranted:
            Debug.Log("Permission granted!");
            break;
        
        case NotificationEvent.EventType.PermissionDenied:
            Debug.LogWarning("Permission denied!");
            break;
        
        case NotificationEvent.EventType.Error:
            Debug.LogError($"Error: {evt.Error}");
            break;
    }
}

void HandleNotificationTapped(NotificationEvent evt)
{
    // Handle notification tap
    // e.g., Navigate to specific screen, claim reward, etc.
}

void OnDestroy()
{
    // Quan trọng: Luôn unsubscribe để tránh memory leak!
    NotificationServices.Instance.OnNotificationEvent -= HandleNotificationEvent;
}
```

### 8.2. OnError Event

```csharp
void Start()
{
    NotificationServices.Instance.OnError += OnNotificationError;
}

void OnNotificationError(string operation, Exception ex)
{
    Debug.LogError($"Error in {operation}: {ex.Message}");
}

void OnDestroy()
{
    NotificationServices.Instance.OnError -= OnNotificationError;
}
```

### 8.3. Event Types

| Type | Description |
|------|-------------|
| `Received` | Notification được nhận (Android only) |
| `Tapped` | User tap vào notification |
| `PermissionGranted` | User grant permission |
| `PermissionDenied` | User deny permission |
| `Error` | Có lỗi xảy ra trong operation |

---

## 9. Batch Operations

### 9.1. Send Multiple Notifications

```csharp
var notifications = new List<NotificationData>
{
    new NotificationData
    {
        title = "Task 1",
        body = "Do this",
        fireTimeInSeconds = 3600,
        identifier = "task_1"
    },
    new NotificationData
    {
        title = "Task 2",
        body = "Do that",
        fireTimeInSeconds = 7200,
        identifier = "task_2"
    }
};

NotificationServices.Instance.SendNotificationBatch(notifications);
```

### 9.2. Cancel Multiple Notifications

```csharp
var identifiers = new List<string> { "task_1", "task_2", "task_3" };
NotificationServices.Instance.CancelNotificationBatch(identifiers);
```

### 9.3. Async Batch

```csharp
await NotificationServices.Instance.SendNotificationBatchAsync(notifications);
await NotificationServices.Instance.CancelNotificationBatchAsync(identifiers);
```

---

## 10. Debug và Performance

### 10.1. Debug Info

```csharp
// Lấy debug info
var debugInfo = NotificationServices.Instance.GetDebugInfo();
foreach (var kvp in debugInfo)
{
    Debug.Log($"{kvp.Key}: {kvp.Value}");
}

// Hoặc log trực tiếp
NotificationServices.Instance.LogDebugInfo();
```

### 10.2. Performance Metrics

```csharp
var metrics = NotificationServices.Instance.GetPerformanceMetrics();
Debug.Log($"Total Scheduled: {metrics.TotalScheduled}");
Debug.Log($"Total Cancelled: {metrics.TotalCancelled}");
Debug.Log($"Pool Hits: {metrics.PoolHits}");
Debug.Log($"Pool Misses: {metrics.PoolMisses}");

// Export metrics to file
string filePath = NotificationServices.Instance.DumpMetricsToFile();
Debug.Log($"Metrics exported to: {filePath}");

// Reset metrics
NotificationServices.Instance.ResetMetrics();
```

### 10.3. Log Level Control

```csharp
// Set log level
NotificationServices.Instance.SetLogLevel(LogLevel.Verbose);  // Most verbose
NotificationServices.Instance.SetLogLevel(LogLevel.Info);     // Info and above
NotificationServices.Instance.SetLogLevel(LogLevel.Warning);   // Warning and above
NotificationServices.Instance.SetLogLevel(LogLevel.Error);      // Errors only
NotificationServices.Instance.SetLogLevel(LogLevel.None);       // No logging

// Get current log level
LogLevel currentLevel = NotificationServices.Instance.GetLogLevel();
```

### 10.4. Pool Stats

```csharp
NotificationServices.Instance.LogPoolStats();
```

### 10.5. iOS Badge Management

```csharp
#if UNITY_IOS
// Set manual badge
NotificationServices.Instance.SetBadgeCount(5);

// Enable auto-increment
NotificationServices.Instance.AutoIncrementBadge = true;
#endif
```

---

## 11. Ví dụ Sử dụng

### 11.1. Daily Login Rewards

```csharp
using UnityEngine;
using DSDK.Notifications;
using System;

public class DailyRewardManager : MonoBehaviour
{
    void Start()
    {
        // Schedule daily reminder at 9 AM
        ScheduleDailyReminder();
        
        NotificationServices.Instance.OnNotificationEvent += OnNotificationEvent;
    }
    
    void ScheduleDailyReminder()
    {
        var now = DateTime.Now;
        var targetTime = now.Date.AddHours(9); // 9 AM
        
        if (now > targetTime)
        {
            targetTime = targetTime.AddDays(1); // Tomorrow if past 9 AM
        }
        
        NotificationServices.Instance.CreateNotification()
            .WithTitle("Daily Reward")
            .WithBody("Claim your free reward!")
            .At(targetTime)
            .Repeating(RepeatInterval.Daily)
            .WithGroup("daily_rewards")
            .WithIdentifier("daily_reward_reminder")
            .Schedule();
    }
    
    void OnNotificationEvent(NotificationEvent evt)
    {
        if (evt.Type == NotificationEvent.EventType.Tapped)
        {
            if (evt.Identifier == "daily_reward_reminder")
            {
                ShowRewardPopup();
            }
        }
    }
    
    void ShowRewardPopup()
    {
        Debug.Log("Showing reward popup...");
        // Your reward logic here
    }
    
    void OnDestroy()
    {
        NotificationServices.Instance.OnNotificationEvent -= OnNotificationEvent;
    }
}
```

### 11.2. Energy Refill Reminder

```csharp
using UnityEngine;
using DSDK.Notifications;

public class EnergySystem : MonoBehaviour
{
    [SerializeField] int maxEnergy = 100;
    [SerializeField] int currentEnergy;
    [SerializeField] int energyRefillTime = 300; // 5 minutes per energy
    
    void Start()
    {
        currentEnergy = maxEnergy;
        ScheduleEnergyRefillReminders();
    }
    
    void OnEnergyDrained()
    {
        // Calculate when energy will be full
        int energyNeeded = maxEnergy - currentEnergy;
        int totalSeconds = energyNeeded * energyRefillTime;
        
        NotificationServices.Instance.CreateNotification()
            .WithTitle("Energy Full!")
            .WithBody("Your energy has been refilled!")
            .In(totalSeconds)
            .WithIdentifier("energy_refill")
            .Schedule();
    }
    
    void ScheduleEnergyRefillReminders()
    {
        // Schedule reminder when energy is 50% full
        int energyNeeded = maxEnergy / 2;
        int totalSeconds = energyNeeded * energyRefillTime;
        
        NotificationServices.Instance.CreateNotification()
            .WithTitle("Energy Alert")
            .WithBody("Your energy is 50% refilled!")
            .In(totalSeconds)
            .WithGroup("energy_system")
            .WithIdentifier("energy_50_percent")
            .Schedule();
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // App backgrounded - re-schedule if needed
            OnEnergyDrained();
        }
        else
        {
            // App resumed - cancel pending notifications
            NotificationServices.Instance.CancelNotification("energy_refill");
            NotificationServices.Instance.CancelNotification("energy_50_percent");
        }
    }
}
```

### 11.3. Quest Expiry Reminder

```csharp
using UnityEngine;
using DSDK.Notifications;
using System;

public class QuestManager : MonoBehaviour
{
    void Start()
    {
        NotificationServices.Instance.OnNotificationEvent += OnNotificationEvent;
    }
    
    public void AcceptQuest(QuestData quest)
    {
        // Schedule expiry reminder 1 hour before deadline
        var expiryTime = quest.deadline.AddHours(-1);
        
        NotificationServices.Instance.CreateNotification()
            .WithTitle($"Quest Expiring Soon: {quest.title}")
            .WithBody("Complete before it expires!")
            .At(expiryTime)
            .WithIdentifier($"quest_expiry_{quest.id}")
            .WithGroup("quests")
            .Schedule();
    }
    
    void CompleteQuest(int questId)
    {
        // Cancel reminder when quest is completed
        NotificationServices.Instance.CancelNotification($"quest_expiry_{questId}");
    }
    
    void OnNotificationEvent(NotificationEvent evt)
    {
        if (evt.Type == NotificationEvent.EventType.Tapped)
        {
            if (evt.Identifier.StartsWith("quest_expiry_"))
            {
                var questId = evt.Identifier.Replace("quest_expiry_", "");
                OpenQuestScreen(int.Parse(questId));
            }
        }
    }
    
    void OpenQuestScreen(int questId)
    {
        Debug.Log($"Opening quest: {questId}");
        // Navigate to quest screen
    }
    
    void OnDestroy()
    {
        NotificationServices.Instance.OnNotificationEvent -= OnNotificationEvent;
    }
}
```

### 11.4. Weekly Boss Reminder

```csharp
using UnityEngine;
using DSDK.Notifications;

public class BossScheduler : MonoBehaviour
{
    void Start()
    {
        ScheduleWeeklyBossReminder();
    }
    
    void ScheduleWeeklyBossReminder()
    {
        // Schedule every Monday at 8 PM
        NotificationServices.Instance.CreateNotification()
            .WithTitle("Weekly Boss Available!")
            .WithBody("Defeat the boss for legendary rewards!")
            .In(3600) // Schedule logic to calculate exact time
            .Repeating(RepeatInterval.Weekly)
            .WithIdentifier("weekly_boss_reminder")
            .WithGroup("boss_events")
            .WithSound("boss_sound")
            .Schedule();
    }
}
```

### 11.5. Build Notification Management

```csharp
using UnityEngine;
using DSDK.Notifications;

public class BuildSystem : MonoBehaviour
{
    void OnBuildStarted(string buildingType, int buildTime)
    {
        NotificationServices.Instance.CreateNotification()
            .WithTitle($"{buildingType} Completed!")
            .WithBody("Your building is ready to collect!")
            .In(buildTime)
            .WithIdentifier($"build_complete_{buildingType}")
            .WithGroup("buildings")
            .Schedule();
    }
    
    void CancelBuildNotification(string buildingType)
    {
        NotificationServices.Instance.CancelNotification($"build_complete_{buildingType}");
    }
    
    void OnNotificationEvent(NotificationEvent evt)
    {
        if (evt.Type == NotificationEvent.EventType.Tapped)
        {
            if (evt.Identifier.StartsWith("build_complete_"))
            {
                var buildingType = evt.Identifier.Replace("build_complete_", "");
                OpenBuildingScreen(buildingType);
            }
        }
    }
    
    void OpenBuildingScreen(string buildingType)
    {
        Debug.Log($"Opening building screen: {buildingType}");
        // Navigate to building
    }
}
```

### 11.6. Game Group Management

```csharp
using UnityEngine;
using DSDK.Notifications;

public class NotificationGroupManager : MonoBehaviour
{
    void SetupTournamentNotifications()
    {
        // Group all tournament notifications together
        NotificationServices.Instance.CreateNotification()
            .WithTitle("Tournament Registration")
            .WithBody("Registration opens in 1 hour!")
            .In(3600)
            .WithGroup("tournament")
            .WithIdentifier("tournament_registration")
            .Schedule();
        
        NotificationServices.Instance.CreateNotification()
            .WithTitle("Tournament Started")
            .WithBody("Tournament begins in 30 minutes!")
            .In(5400)
            .WithGroup("tournament")
            .WithIdentifier("tournament_start")
            .Schedule();
    }
    
    void CancelTournamentNotifications()
    {
        // Cancel all notifications in tournament group
        NotificationServices.Instance.CancelNotificationGroup("tournament");
    }
    
    void GetTournamentNotifications()
    {
        int count = NotificationServices.Instance.GetScheduledCountByGroup("tournament");
        var identifiers = NotificationServices.Instance.GetNotificationsByGroup("tournament");
        
        Debug.Log($"Tournament notifications: {count}");
        foreach (var id in identifiers)
        {
            Debug.Log($"  - {id}");
        }
    }
}
```

---

## Best Practices

### 1. Always Unsubscribe Events

```csharp
void OnDestroy()
{
    // CRITICAL: Prevent memory leaks!
    NotificationServices.Instance.OnNotificationEvent -= OnNotificationEvent;
    NotificationServices.Instance.OnError -= OnError;
}
```

### 2. Use Unique Identifiers

```csharp
// Good
NotificationServices.Instance.SendNotification("Title", "Body", 3600, $"daily_reward_{DateTime.Now.Date}");

// Bad - will overwrite previous notification
NotificationServices.Instance.SendNotification("Title", "Body", 3600, "daily_reward");
```

### 3. Handle Permission Denial Gracefully

```csharp
bool hasPermission = NotificationServices.Instance.HasNotificationPermission();
if (!hasPermission)
{
    // Show explanation why notification is important
    ShowPermissionExplanationPopup();
}
```

### 4. Cancel When Not Needed

```csharp
void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus)
    {
        // App backgrounded - might want to cancel some notifications
        NotificationServices.Instance.CancelNotification("outdated_notification");
    }
}
```

### 5. Use Groups for Organization

```csharp
// Group related notifications together
NotificationServices.Instance.CreateNotification()
    .WithGroup("achievements")
    .WithIdentifier("achievement_01")
    .Schedule();
```

### 6. Set Appropriate Log Levels

```csharp
#if DEVELOPMENT_BUILD
    NotificationServices.Instance.SetLogLevel(LogLevel.Verbose);
#else
    NotificationServices.Instance.SetLogLevel(LogLevel.Error);
#endif
```

---

## Platform-Specific Notes

### Android

- Requires `POST_NOTIFICATIONS` permission on Android 13+ (API 33+)
- Auto-creates default notification channel
- Supports custom icons, sounds, vibrations
- Max 500 scheduled notifications

### iOS

- Requires `AuthorizationRequest` before scheduling
- Supports badges, sounds, and custom categories
- Max 64 scheduled notifications
- Badge management available

---

## Troubleshooting

### Permission Not Requested

**Problem**: Permission dialog never appears.

**Solution**: 
```csharp
// Manually request permission
await NotificationServices.Instance.RequestPermissionAsync();
```

### Notifications Not Showing

**Problem**: Notifications scheduled but never appear.

**Solution**:
1. Check permission status
2. Verify notification schedule time is in future
3. Check device "Do Not Disturb" settings
4. Review logs for errors

### iOS Badge Not Working

**Problem**: Badge count incorrect.

**Solution**:
```csharp
#if UNITY_IOS
// Manual badge management
NotificationServices.Instance.AutoIncrementBadge = false;
NotificationServices.Instance.SetBadgeCount(desiredCount);
#endif
```

---

## API Reference Summary

### Public Methods

| Method | Description |
|--------|-------------|
| `SendNotification(title, body, seconds, id?)` | Send simple notification |
| `SendRepeatingNotification(...)` | Send repeating notification |
| `CancelNotification(identifier)` | Cancel specific notification |
| `CancelAllNotifications()` | Cancel all notifications |
| `HasNotificationPermission()` | Check permission status |
| `RequestPermissionAsync()` | Request permission (async) |
| `GetScheduledNotificationCount()` | Get count of scheduled notifications |
| `CreateNotification()` | Fluent builder API |
| `ConfigureReturnNotification(config)` | Setup return notifications |
| `OnNotificationEvent` | Event for notifications |
| `OnError` | Event for errors |

### Data Structures

```csharp
public class NotificationData
{
    public string title;
    public string body;
    public string subtitle;
    public int fireTimeInSeconds;
    public string identifier;
    public bool repeats;
    public RepeatInterval repeatInterval;
    public string soundName;
    public string groupKey;
    public int customBadgeCount;
}

public class NotificationEvent
{
    public EventType Type;
    public string Title;
    public string Body;
    public DateTime Timestamp;
    public Exception Error;
}
```

---

## Support

Nếu gặp vấn đề hoặc có câu hỏi:

1. Kiểm tra logs với `LogLevel.Verbose`
2. Xem PerformanceMetrics để debug
3. Review Unity Console logs
4. Check device notification settings

---

## License

NotificationServices - Được thiết kế bởi DSDK Team
© 2024 GDK - Production Ready Notification System