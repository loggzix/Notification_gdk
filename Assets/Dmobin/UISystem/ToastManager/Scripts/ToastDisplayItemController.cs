using UnityEngine;
using TMPro;

#if USE_I2LOC
using I2.Loc;
#endif

namespace DSDK.ToastNotification
{
    /// <summary>
    /// Điều khiển hiển thị các thông báo Toast trong UI
    /// Thông báo Toast là các thông báo tạm thời hiển thị và tự động biến mất sau một khoảng thời gian
    /// </summary>
    public class ToastDisplayItemController : MonoBehaviour
    {
        [Header("COMPONENT")]
        [SerializeField] Animator animator; // Điều khiển animation của Toast
        [SerializeField] RectTransform rectTransform; // Điều khiển kích thước và vị trí của Toast
        [SerializeField] CanvasGroup canvasGroup; // Điều khiển độ trong suốt của Toast
        [SerializeField] TextMeshProUGUI toastText; // Hiển thị nội dung thông báo

#if USE_I2LOC
        [SerializeField] Localize toastTextLocalize; // Quản lý đa ngôn ngữ cho nội dung thông báo
        [SerializeField] LocalizationParamsManager toastTextLocalizeParam; // Quản lý tham số cho nội dung thông báo đa ngôn ngữ
#endif

        [Header("SETTING")]
        // Tăng thêm size của RectTransform so với text
        [SerializeField] private float offsetWidth = 60f; // Độ lề chiều ngang so với nội dung text
        [SerializeField] private float offsetHeight = 50f; // Độ lề chiều dọc so với nội dung text
        private float maxWidth = 600f; // Chiều rộng tối đa của Toast

        private System.Action onComplete; // Callback được gọi khi Toast biến mất

        /// <summary>
        /// Hiển thị thông báo Toast với nội dung và callback khi hoàn thành
        /// </summary>
        /// <param name="mess">Mảng chứa nội dung thông báo. mess[0] là key của thông báo, mess[1] (nếu có) là giá trị để thay thế vào thông báo</param>
        /// <param name="onComplete">Hàm callback được gọi khi Toast biến mất</param>
        public void ShowToast(string[] mess, System.Action onComplete = null)
        {
            this.onComplete = onComplete;
            SetToastText(mess);
            UpdateRectTransformSize();
            animator.SetTrigger("open"); // Kích hoạt animation hiển thị Toast
        }

        /// <summary>
        /// Cập nhật kích thước của RectTransform dựa trên nội dung thông báo
        /// Đảm bảo Toast có kích thước phù hợp với nội dung và không vượt quá giới hạn màn hình
        /// </summary>
        private void UpdateRectTransformSize()
        {
            // Tính max width dựa trên screen width
            maxWidth = Screen.width - 50f;

            // Đợi text được cập nhật
            Canvas.ForceUpdateCanvases();

            // Lấy preferred width của text
            float preferredWidth = toastText.GetPreferredValues().x + offsetWidth;

            // Giới hạn width tối đa
            float finalWidth = Mathf.Min(preferredWidth, maxWidth);

            // Cập nhật size của rectTransform
            Vector2 sizeDelta = rectTransform.sizeDelta;
            sizeDelta.x = finalWidth;
            rectTransform.sizeDelta = sizeDelta;

            // Lấy preferred height của text
            float finalHeight = toastText.GetPreferredValues().y + offsetHeight;

            // Cập nhật size của rectTransform
            Vector2 sizeDelta2 = rectTransform.sizeDelta;
            sizeDelta2.y = finalHeight;
            rectTransform.sizeDelta = sizeDelta2;
        }

        /// <summary>
        /// Thiết lập nội dung text cho Toast
        /// Hỗ trợ đa ngôn ngữ nếu USE_I2LOC được định nghĩa
        /// </summary>
        /// <param name="mess">Mảng chứa thông tin thông báo
        /// - Nếu độ dài là 1: mess[0] là key của thông báo
        /// - Nếu độ dài lớn hơn 1: mess[0] là key, mess[1] là giá trị thay thế vào tham số VALUE trong thông báo</param>
        public void SetToastText(string[] mess)
        {
            if (mess.Length > 0)
            {
                if (mess.Length == 1)
                {
                    // Trường hợp chỉ có một thông báo không cần tham số
#if USE_I2LOC
                    if (toastTextLocalize != null)
                    {
                        toastTextLocalize.SetTerm(mess[0]);
                    }
#else
                    if (toastText != null)
                    {
                        toastText.text = mess[0];
                    }
#endif
                }
                else
                {
                    // Trường hợp thông báo có tham số (VALUE)
#if USE_I2LOC
                    if (toastTextLocalize != null)
                    {
                        toastTextLocalize.SetTerm(mess[0]);
                    }

                    if (toastTextLocalizeParam != null)
                    {
                        toastTextLocalizeParam.SetParameterValue("VALUE", mess[1]);
                    }

#else
                    if (toastText != null)
                    {
                        toastText.text = mess[0];
                    }
#endif
                }
            }
            else
            {
                // Trường hợp không có nội dung
                if (toastText != null)
                {
                    toastText.text = "";
                }
            }
        }

        /// <summary>
        /// Vô hiệu hóa Toast sau khi đã hiển thị xong
        /// Gọi callback onComplete và ẩn gameObject
        /// </summary>
        public void Disable()
        {
            onComplete?.Invoke();
            onComplete = null;
            gameObject.SetActive(false);
        }
    }
}
