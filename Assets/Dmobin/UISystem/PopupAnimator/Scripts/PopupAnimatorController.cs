using System;
using System.Collections;
using UnityEngine;

#if DOTWEEN
using DG.Tweening;
#endif

namespace DSDK.UISystem
{
    /// <summary>
    /// Điều khiển hoạt ảnh cho popup trong UI, bao gồm các hiệu ứng mở/đóng và rung lắc
    /// Yêu cầu component Animator và CanvasGroup để hoạt động
    /// </summary>
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public class PopupAnimatorController : MonoBehaviour
    {
        [Header("COMPONENT")]
        [SerializeField] protected Animator animator; // Component animator để điều khiển animation
        [SerializeField] protected CanvasGroup canvasGroup; // Component canvasGroup để điều khiển alpha
        [SerializeField] protected GameObject blockClickObj; // Đối tượng để chặn click khi đang chạy animation
        [SerializeField] protected RectTransform mainContentRect; // Transform chính của nội dung popup để áp dụng hiệu ứng

        // Các callback được gọi tại các thời điểm khác nhau trong quá trình animation
        protected Action eventStartAnimOpenPopup; // Sự kiện được gọi khi bắt đầu animation mở popup
        protected Action eventEndAnimOpenPopup; // Sự kiện được gọi khi kết thúc animation mở popup
        protected Action eventStartAnimClosePopup; // Sự kiện được gọi khi bắt đầu animation đóng popup
        protected Action eventEndAnimClosePopup; // Sự kiện được gọi khi kết thúc animation đóng popup

        // Tên các tham số điều khiển trong Animator
        protected string openBool = "open"; // Tham số boolean để kích hoạt animation mở
        protected string closeBool = "close"; // Tham số boolean để kích hoạt animation đóng

        [Header("SETTINGS")]
        public bool changeIdleEndOpen = false; // Xác định có reset các animation về trạng thái idle sau khi mở không
        public bool changeIdleEndClose = false; // Xác định có reset các animation về trạng thái idle sau khi đóng không

#if UNITY_EDITOR
        /// <summary>
        /// Tự động cài đặt tham chiếu và thiết lập mode cho animator trong Editor
        /// </summary>
        private void OnValidate()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }
#endif

        /// <summary>
        /// Đảm bảo đóng popup khi component bị vô hiệu hóa
        /// </summary>
        void OnDisable()
        {
            if (!animator.GetBool(closeBool) && animator.GetBool(openBool))
            {
                CloseAnimator();
            }
        }

        #region Animator
        /// <summary>
        /// Đặt callback sẽ được gọi khi bắt đầu animation mở popup
        /// </summary>
        /// <param name="_callback">Hàm callback cần gọi</param>
        public void SetEventStartAnimOpenPopup(Action _callback = null)
        {
            eventStartAnimOpenPopup = _callback;
        }

        /// <summary>
        /// Đặt callback sẽ được gọi khi kết thúc animation mở popup
        /// </summary>
        /// <param name="_callback">Hàm callback cần gọi</param>
        public void SetEventEndAnimOpenPopup(Action _callback = null)
        {
            eventEndAnimOpenPopup = _callback;
        }

        /// <summary>
        /// Đặt callback sẽ được gọi khi bắt đầu animation đóng popup
        /// </summary>
        /// <param name="_callback">Hàm callback cần gọi</param>
        public void SetEventStartAnimClosePopup(Action _callback = null)
        {
            eventStartAnimClosePopup = _callback;
        }

        /// <summary>
        /// Đặt callback sẽ được gọi khi kết thúc animation đóng popup
        /// </summary>
        /// <param name="_callback">Hàm callback cần gọi</param>
        public void SetEventEndAnimClosePopup(Action _callback = null)
        {
            eventEndAnimClosePopup = _callback;
        }

        /// <summary>
        /// Kích hoạt animation mở popup
        /// </summary>
        /// <param name="isRebind">Có reset lại animator về trạng thái ban đầu không</param>
        public void OpenAnimator(bool isRebind = true)
        {
            if (isRebind)
            {
                animator.Rebind();
                animator.Update(0);
            }

            animator.SetBool(openBool, true);
            animator.SetBool(closeBool, false);
        }

        /// <summary>
        /// Được gọi bởi animation event khi bắt đầu animation mở popup
        /// Kích hoạt callback và bật chặn click
        /// </summary>
        public void StartEventAnimOpenPopup()
        {
            eventStartAnimOpenPopup?.Invoke();

            if (blockClickObj)
            {
                blockClickObj.SetActive(true);
            }
        }

        /// <summary>
        /// Được gọi bởi animation event khi kết thúc animation mở popup
        /// Tắt chặn click và kích hoạt callback
        /// </summary>
        public void EndEventAnimOpenPopup()
        {
            if (blockClickObj)
            {
                blockClickObj.SetActive(false);
            }

            eventEndAnimOpenPopup?.Invoke();

            if (changeIdleEndOpen)
            {
                animator.SetBool(openBool, false);
                animator.SetBool(closeBool, false);
            }
        }

        /// <summary>
        /// Kích hoạt animation đóng popup
        /// </summary>
        /// <param name="isRebind">Có reset lại animator về trạng thái ban đầu không</param>
        public void CloseAnimator(bool isRebind = true)
        {
            if (isRebind)
            {
                animator.Rebind();
                animator.Update(0);
            }

            animator.SetBool(closeBool, true);
            animator.SetBool(openBool, false);
        }

        /// <summary>
        /// Được gọi bởi animation event khi bắt đầu animation đóng popup
        /// Kích hoạt callback và bật chặn click
        /// </summary>
        public void StartEventAnimClosePopup()
        {
            eventStartAnimClosePopup?.Invoke();

            if (blockClickObj)
            {
                blockClickObj.SetActive(true);
            }
        }

        /// <summary>
        /// Được gọi bởi animation event khi kết thúc animation đóng popup
        /// Tắt chặn click, kích hoạt callback và reset trạng thái animator (nếu cần)
        /// </summary>
        public void EndEventAnimClosePopup()
        {
            if (blockClickObj)
            {
                blockClickObj.SetActive(false);
            }

            eventEndAnimClosePopup?.Invoke();

            if (changeIdleEndClose)
            {
                animator.SetBool(openBool, false);
                animator.SetBool(closeBool, false);
            }
        }

        /// <summary>
        /// Bật/tắt animator component
        /// </summary>
        /// <param name="value">Trạng thái enable của animator</param>
        public void SetEnableAnimator(bool value)
        {
            animator.enabled = value;
        }

        /// <summary>
        /// Tạm dừng animation bằng cách đặt tốc độ animator = 0
        /// </summary>
        public void PauseAnim()
        {
            animator.speed = 0f;
        }

        /// <summary>
        /// Tiếp tục animation bằng cách đặt lại tốc độ animator = 1
        /// </summary>
        public void ResumeAnim()
        {
            animator.speed = 1f;
        }

        /// <summary>
        /// Đặt trạng thái của một tham số boolean trong animator
        /// </summary>
        /// <param name="boolName">Tên tham số boolean cần đặt</param>
        /// <param name="value">Trạng thái mới (true hoặc false)</param>
        public void SetBool(string boolName, bool value)
        {
            animator.SetBool(boolName, value);
        }

        /// <summary>
        /// Chơi một animation có tên tương ứng
        /// </summary>
        /// <param name="animationName">Tên animation cần chơi</param>
        public void SetAnimation(string animationName)
        {
            animator.Play(animationName, 0, 0);
        }
        #endregion

        #region SHAKE
#if DOTWEEN
        /// <summary>
        /// Tạo hiệu ứng rung lắc cho popup
        /// </summary>
        /// <param name="duration">Thời gian rung lắc (giây)</param>
        /// <param name="strength">Độ mạnh của hiệu ứng rung</param>
        /// <param name="vibratio">Số lần rung trong khoảng thời gian</param>
        /// <param name="randomness">Độ ngẫu nhiên của hiệu ứng (0-180)</param>
        /// <param name="easeMove">Kiểu easing cho chuyển động</param>
        public void Shake(float duration, Vector2 strength, int vibratio = 10, float randomness = 90f, Ease easeMove = Ease.InOutQuad)
        {
            if (mainContentRect)
            {
                mainContentRect.DOKill(); // Dừng tween hiện tại nếu có
                mainContentRect.anchoredPosition = Vector2.zero; // Reset vị trí
                mainContentRect.DOShakeAnchorPos(duration, strength, vibratio, randomness).SetEase(easeMove);
            }
        }
#endif
        #endregion
    }
}