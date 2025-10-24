using System.Collections.Generic;
using UnityEngine;
using DSDK.Core;
using DSDK.Logger;

namespace DSDK.ToastNotification
{
    /// <summary>
    /// Quản lý và hiển thị các thông báo dạng toast trong giao diện người dùng.
    /// Hỗ trợ nhiều loại toast khác nhau (Info, Success, Error) và hai chế độ hiển thị (Normal, Queue).
    /// </summary>
    public class ToastManager : SingletonMonoBehaviour<ToastManager>
    {
        [Header("COMPONENT")]
        [SerializeField] private RectTransform posSpawnToast; // Vị trí sẽ sinh ra các toast
        [SerializeField] private ToastSettingInfo[] listToastSettingInfo; // Danh sách cấu hình cho từng loại toast

        [Header("TESTING")]
        [SerializeField] private string[] messTextTest = new string[] { "Hello", "World" };
        [SerializeField] private ToastItemType itemTypeTest = ToastItemType.Info;
        [SerializeField] private ToastMode modeTest = ToastMode.Normal;
        /// <summary>
        /// Đăng ký lắng nghe sự kiện hiển thị toast khi component được kích hoạt
        /// </summary>
        private void OnEnable()
        {
            this.AddEventListener<string[], ToastItemType, ToastMode>(ToastEvent.ShowToast, ShowToastEventRegisterListener);
        }

        /// <summary>
        /// Hủy đăng ký lắng nghe sự kiện khi component bị vô hiệu hóa
        /// </summary>
        private void OnDisable()
        {
            this.RemoveEventListener<string[], ToastItemType, ToastMode>(ToastEvent.ShowToast, ShowToastEventRegisterListener);
        }

        /// <summary>
        /// Xử lý sự kiện hiển thị toast được kích hoạt từ hệ thống event
        /// </summary>
        /// <param name="text">Mảng các chuỗi văn bản sẽ hiển thị</param>
        private void ShowToastEventRegisterListener(string[] text = null)
        {
            if (text != null && text.Length > 0)
            {
                string[] listString = new string[text.Length];

                for (int i = 0; i < text.Length; i++)
                {
                    listString[i] = (string)text[i];
                }

                Show(listString, ToastItemType.Info, ToastMode.Normal);
            }
            else
            {
                DLogger.LogInfo($"[DSDK] ToastManager: ShowToastEventRegisterListener: text is null");
            }
        }

        /// <summary>
        /// Xử lý sự kiện hiển thị toast được kích hoạt từ hệ thống event
        /// </summary>
        /// <param name="text">Mảng các chuỗi văn bản sẽ hiển thị</param>
        /// <param name="itemType">Loại toast (Info, Success, Error)</param>
        /// <param name="mode">Chế độ hiển thị (Normal, Queue)</param>
        private void ShowToastEventRegisterListener(string[] text = null, ToastItemType itemType = ToastItemType.Info, ToastMode mode = ToastMode.Normal)
        {
            if (text != null && text.Length > 0)
            {
                string[] listString = new string[text.Length];

                for (int i = 0; i < text.Length; i++)
                {
                    listString[i] = (string)text[i];
                }

                Show(listString, itemType, mode);
            }
            else
            {
                DLogger.LogInfo($"[DSDK] ToastManager: ShowToastEventRegisterListener: text is null");
            }
        }

        /// <summary>
        /// Hiển thị toast với nội dung, loại và chế độ được chỉ định
        /// </summary>
        /// <param name="mess">Mảng các chuỗi văn bản sẽ hiển thị</param>
        /// <param name="itemType">Loại toast (Info, Success, Error)</param>
        /// <param name="mode">Chế độ hiển thị (Normal, Queue)</param>
        public void Show(string[] mess, ToastItemType itemType, ToastMode mode)
        {
            if (mode == ToastMode.Normal)
            {
                // Hiển thị toast ngay lập tức
                ShowToastImmediately(mess, itemType);
            }
            else
            {
                // Thêm toast vào hàng đợi để hiển thị tuần tự
                ToastSettingInfo settingInfo = GetToastSettingInfo(itemType);
                if (settingInfo == null)
                {
                    DLogger.LogInfo($"[DSDK] ToastManager: Show: settingInfo is null");
                    return;
                }

                settingInfo.toastQueue.Enqueue(mess);
                if (!settingInfo.isProcessingQueue)
                {
                    ProcessNextToast(settingInfo);
                }
            }
        }

        /// <summary>
        /// Hiển thị toast ngay lập tức không qua hàng đợi
        /// </summary>
        /// <param name="mess">Mảng các chuỗi văn bản sẽ hiển thị</param>
        /// <param name="itemType">Loại toast (Info, Success, Error)</param>
        private void ShowToastImmediately(string[] mess, ToastItemType itemType)
        {
            ToastDisplayItemController item = GetToastDisplayItem(itemType);
            item.gameObject.SetActive(true);
            item.ShowToast(mess);
        }

        /// <summary>
        /// Xóa tất cả các toast trong hàng đợi của một loại toast cụ thể
        /// </summary>
        /// <param name="itemType">Loại toast cần xóa hàng đợi</param>
        public void ClearQueue(ToastItemType itemType)
        {
            ToastSettingInfo settingInfo = GetToastSettingInfo(itemType);
            if (settingInfo == null)
            {
                DLogger.LogInfo($"[DSDK] ToastManager: ClearQueue: settingInfo is null");
                return;
            }

            settingInfo.toastQueue.Clear();
            settingInfo.isProcessingQueue = false;
        }

        /// <summary>
        /// Xóa tất cả các toast trong hàng đợi của tất cả các loại toast
        /// </summary>
        public void ClearAllQueue()
        {
            foreach (var item in listToastSettingInfo)
            {
                item.toastQueue.Clear();
                item.isProcessingQueue = false;
            }
        }

        /// <summary>
        /// Vô hiệu hóa tất cả các toast đang hiển thị của một loại toast cụ thể
        /// </summary>
        /// <param name="itemType">Loại toast cần vô hiệu hóa</param>
        public void DisableToast(ToastItemType itemType)
        {
            ToastSettingInfo settingInfo = GetToastSettingInfo(itemType);
            if (settingInfo == null)
            {
                DLogger.LogInfo($"[DSDK] ToastManager: DisableToast: settingInfo is null");
                return;
            }

            foreach (var displayItem in settingInfo.listToastDisplayItem)
            {
                displayItem.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Vô hiệu hóa tất cả các toast đang hiển thị và xóa tất cả hàng đợi
        /// </summary>
        public void DisableAllToast()
        {
            ClearAllQueue();
            foreach (var item in listToastSettingInfo)
            {
                foreach (var displayItem in item.listToastDisplayItem)
                {
                    displayItem.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Xử lý toast tiếp theo trong hàng đợi
        /// </summary>
        /// <param name="settingInfo">Cấu hình của loại toast đang xử lý</param>
        private void ProcessNextToast(ToastSettingInfo settingInfo)
        {
            if (settingInfo == null)
            {
                DLogger.LogInfo($"[DSDK] ToastManager: ProcessNextToast: settingInfo is null");
                return;
            }

            if (settingInfo.toastQueue.Count == 0)
            {
                settingInfo.isProcessingQueue = false;
                return;
            }

            settingInfo.isProcessingQueue = true;
            string[] nextToast = settingInfo.toastQueue.Dequeue();
            ToastDisplayItemController item = GetToastDisplayItem(settingInfo);
            item.gameObject.SetActive(true);
            item.ShowToast(nextToast, () => OnToastComplete(settingInfo));
        }

        /// <summary>
        /// Được gọi khi một toast hoàn thành hiển thị để xử lý toast tiếp theo trong hàng đợi
        /// </summary>
        /// <param name="settingInfo">Cấu hình của loại toast đang xử lý</param>
        private void OnToastComplete(ToastSettingInfo settingInfo)
        {
            ProcessNextToast(settingInfo);
        }

        /// <summary>
        /// Lấy thông tin cấu hình của một loại toast cụ thể
        /// </summary>
        /// <param name="itemType">Loại toast cần lấy cấu hình</param>
        /// <returns>Thông tin cấu hình của loại toast</returns>
        public ToastSettingInfo GetToastSettingInfo(ToastItemType itemType)
        {
            foreach (var item in listToastSettingInfo)
            {
                if (item.itemType == itemType)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Lấy một toast item để hiển thị dựa trên loại toast.
        /// Nếu không có toast item nào sẵn có, một toast item mới sẽ được tạo ra
        /// </summary>
        /// <param name="itemType">Loại toast cần lấy item</param>
        /// <returns>Toast display item controller</returns>
        private ToastDisplayItemController GetToastDisplayItem(ToastItemType itemType)
        {
            ToastSettingInfo settingInfo = GetToastSettingInfo(itemType);

            if (settingInfo == null)
            {
                DLogger.LogInfo($"[DSDK] ToastManager: GetToastDisplayItem by itemType: settingInfo is null");
                return null;
            }

            for (int i = 0; i < settingInfo.listToastDisplayItem.Count; i++)
            {
                ToastDisplayItemController item = settingInfo.listToastDisplayItem[i];
                if (!item.gameObject.activeInHierarchy)
                {
                    return item;
                }
            }

            // Tạo một toast item mới nếu tất cả các item hiện có đều đang được sử dụng
            ToastDisplayItemController newItem = Instantiate(settingInfo.toastDisplayItemPrefab, posSpawnToast);
            settingInfo.listToastDisplayItem.Add(newItem);
            return newItem;
        }

        /// <summary>
        /// Lấy một toast item để hiển thị dựa trên thông tin cấu hình
        /// </summary>
        /// <param name="settingInfo">Thông tin cấu hình của loại toast</param>
        /// <returns>Toast display item controller</returns>
        private ToastDisplayItemController GetToastDisplayItem(ToastSettingInfo settingInfo)
        {
            if (settingInfo == null)
            {
                DLogger.LogInfo($"[DSDK] ToastManager: GetToastDisplayItem by settingInfo: settingInfo is null");
                return null;
            }

            for (int i = 0; i < settingInfo.listToastDisplayItem.Count; i++)
            {
                ToastDisplayItemController item = settingInfo.listToastDisplayItem[i];
                if (!item.gameObject.activeInHierarchy)
                {
                    return item;
                }
            }

            // Tạo một toast item mới nếu tất cả các item hiện có đều đang được sử dụng
            ToastDisplayItemController newItem = Instantiate(settingInfo.toastDisplayItemPrefab, posSpawnToast);
            settingInfo.listToastDisplayItem.Add(newItem);
            return newItem;
        }

        #region TESTING
        [ContextMenu("Test Show Toast")]
        public void TestShowToast()
        {
            Show(messTextTest, itemTypeTest, modeTest);
        }
        #endregion

        /// <summary>
        /// Định nghĩa các chế độ hiển thị toast
        /// </summary>
        public enum ToastMode
        {
            Normal,     // Hiển thị toast ngay lập tức, không dùng queue
            Queue       // Hiển thị toast theo queue
        }

        /// <summary>
        /// Định nghĩa các loại toast
        /// </summary>
        public enum ToastItemType
        {
            Info,       // Thông tin thông thường
            Success,    // Thông báo thành công
            Warning,    // Thông báo cảnh báo
            Error       // Thông báo lỗi
        }

        /// <summary>
        /// Lớp chứa thông tin cấu hình cho một loại toast cụ thể
        /// </summary>
        [System.Serializable]
        public class ToastSettingInfo
        {
            public string name = "Info";
            public ToastItemType itemType = ToastItemType.Info;                           // Loại toast
            public ToastDisplayItemController toastDisplayItemPrefab;                     // Prefab của toast item
            public List<ToastDisplayItemController> listToastDisplayItem = new List<ToastDisplayItemController>(); // Danh sách các toast item đã tạo
            public Queue<string[]> toastQueue = new Queue<string[]>();                   // Hàng đợi chứa nội dung các toast cần hiển thị
            public bool isProcessingQueue = false;                                        // Cờ đánh dấu đang xử lý hàng đợi
        }
    }
}