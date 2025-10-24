using System.Collections;
using System.Collections.Generic;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif
#if UNITY_ANDROID
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif
using UnityEngine;

namespace MobileMonetizationPro
{
    public class MobileMonetizationPro_NotificationController : MonoBehaviour
    {
        [System.Serializable]
        public class NotifDesc
        {
            public string NotificationTitle;
            public string NotificationDescription;
            public string NotificationSubTitleForIOS;
        }

        [System.Serializable]
        public class Notiftime
        {
            public int Days;
            public int Hours;
            public int Minutes;
            public int Seconds;
        }

        [System.Serializable]
        public class NotifIcon
        {
            public string SmallIconName = "SmallIcon";
            public string LargeIconName = "LargeIcon";
        }

        public NotifDesc AboutNotification;
        public Notiftime NotificationRecievingTime;
        public NotifIcon NotificationIcons;

        int totalSeconds;
        //public string Timer;

        private void Start()
        {
            totalSeconds = NotificationRecievingTime.Days * 24 * 60 * 60 + NotificationRecievingTime.Hours * 60 * 60 + NotificationRecievingTime.Minutes * 60 + NotificationRecievingTime.Seconds;
            //Timer = totalSeconds.ToString();

#if UNITY_ANDROID
        RequestAuthorization();
        RegisterNotificationChannel();
        //SendNotificationForAndroid(NotificationTitle, NotificationDescription, totalSeconds);

#endif
#if UNITY_IOS
            StartCoroutine(RequestAuthorizationForIOS());
#endif
        }
#if UNITY_ANDROID
    public void RequestAuthorization()
    {
        if (!Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
        {
            Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
            Debug.Log("Permission Granted");
        }
    }
#endif
        private void OnApplicationFocus(bool focus)
        {
            if (focus == false)
            {
#if UNITY_ANDROID
            SendNotificationForAndroid(AboutNotification.NotificationTitle, AboutNotification.NotificationDescription, totalSeconds);
#endif
#if UNITY_IOS
                iOSNotificationCenter.RemoveAllScheduledNotifications();
                SendNotificationIOS(AboutNotification.NotificationTitle, AboutNotification.NotificationDescription, AboutNotification.NotificationSubTitleForIOS, totalSeconds);
#endif
            }
        }
#if UNITY_ANDROID
    public void RegisterNotificationChannel()
    {
        AndroidNotificationCenter.CancelAllDisplayedNotifications();

        var channel = new AndroidNotificationChannel()
        {
            Id = "default_channel",
            Name = "Default Channel",
            Importance = Importance.Default,
            Description = "Generic notification"
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
        Debug.Log("Notification Registered");
    }

    public void SendNotificationForAndroid(string title, string text, int fireTimeInSeconds)
    {
        var notification = new AndroidNotification();
        notification.Title = title;
        notification.Text = text;
        notification.FireTime = System.DateTime.Now.AddSeconds(fireTimeInSeconds);

        notification.SmallIcon = NotificationIcons.SmallIconName;
        notification.LargeIcon = NotificationIcons.LargeIconName;

        var id = AndroidNotificationCenter.SendNotification(notification, "default_channel");

        if (AndroidNotificationCenter.CheckScheduledNotificationStatus(id) == NotificationStatus.Scheduled)
        {
            AndroidNotificationCenter.CancelAllNotifications();
            AndroidNotificationCenter.SendNotification(notification, "default_channel");
            Debug.Log("Notification Sent");
        }
    }
#endif

#if UNITY_IOS
        public IEnumerator RequestAuthorizationForIOS()
        {
            using var req = new AuthorizationRequest(authorizationOption: AuthorizationOption.Alert | AuthorizationOption.Badge,
                registerForRemoteNotifications: true);
            while (!req.IsFinished)
            {
                yield return null;
            }
        }
        public void SendNotificationIOS(string title, string body, string subtitle, int fireTimeInSeconds)
        {
            var timeTrigger = new iOSNotificationTimeIntervalTrigger()
            {
                TimeInterval = new System.TimeSpan(hours: 0, minutes: 0, fireTimeInSeconds),
                Repeats = false
            };

            var notification = new iOSNotification()
            {
                Identifier = "Hello",
                Title = title,
                Body = body,
                Subtitle = subtitle,
                ShowInForeground = true,
                ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
                CategoryIdentifier = "default_category",
                ThreadIdentifier = "thread1",
                Trigger = timeTrigger
            };

            iOSNotificationCenter.ScheduleNotification(notification);
        }
#endif

    }
}