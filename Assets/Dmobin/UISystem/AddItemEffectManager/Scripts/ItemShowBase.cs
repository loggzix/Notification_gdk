using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSDK.UISystem
{
    /// <summary>
    /// Lớp cơ sở cho hiệu ứng hiển thị vật phẩm trong UI
    /// Cung cấp các phương thức ảo để các lớp con có thể ghi đè và tùy chỉnh hiệu ứng
    /// </summary>
    public class ItemShowBase : MonoBehaviour
    {
        [Header("COMPONENTS")]
        // Loại tài nguyên của vật phẩm (Coin, Gem, Energy, v.v.)
        [SerializeField] protected AddItemResourceType itemResourceType = AddItemResourceType.Coin;

        // Sự kiện được gọi khi hiệu ứng vật phẩm kết thúc
        public Action OnEndItemEffect = null;

        /// <summary>
        /// Thiết lập loại vật phẩm và biểu tượng hiển thị
        /// </summary>
        /// <param name="itemResourceType">Loại tài nguyên</param>
        /// <param name="icon">Biểu tượng hình ảnh của vật phẩm</param>
        public void SetItemType(AddItemResourceType itemResourceType, Sprite icon)
        {
            this.itemResourceType = itemResourceType;

            SetImageDisplay(icon);
        }

        /// <summary>
        /// Cấu hình các giá trị ban đầu cho transform của vật phẩm
        /// </summary>
        /// <param name="startPos">Vị trí bắt đầu</param>
        /// <param name="startScale">Tỷ lệ kích thước ban đầu</param>
        /// <param name="startAngle">Góc xoay ban đầu</param>
        public void ConfigTransformStartValue(Vector3 startPos, float startScale, float startAngle)
        {
            transform.position = startPos;
            transform.localScale = Vector3.one * startScale;
            transform.rotation = Quaternion.Euler(0f, 0f, startAngle);
        }

        #region Các phương thức ảo để các lớp con ghi đè

        /// <summary>
        /// Thiết lập hình ảnh hiển thị cho vật phẩm
        /// </summary>
        /// <param name="icon">Biểu tượng hình ảnh của vật phẩm</param>
        public virtual void SetImageDisplay(Sprite icon) { }

        /// <summary>
        /// Bật/tắt hiển thị hình ảnh của vật phẩm
        /// </summary>
        /// <param name="value">Trạng thái bật/tắt</param>
        public virtual void EnableDisplayImage(bool value) { }

        /// <summary>
        /// Thiết lập giá trị text hiển thị
        /// </summary>
        /// <param name="value">Giá trị cần hiển thị</param>
        public virtual void SetValueText(string value) { }

        /// <summary>
        /// Vô hiệu hóa hiển thị vật phẩm
        /// </summary>
        public virtual void Disable() { }

        /// <summary>
        /// Hiệu ứng di chuyển vật phẩm
        /// </summary>
        /// <param name="endScale">Tỷ lệ kích thước kết thúc</param>
        /// <param name="timeScale">Thời gian thay đổi kích thước</param>
        /// <param name="endAngle">Góc xoay kết thúc</param>
        /// <param name="rotateSpeed">Tốc độ xoay</param>
        /// <param name="movePath">Đường dẫn di chuyển (các điểm trên đường đi)</param>
        /// <param name="timeMove">Thời gian di chuyển</param>
        /// <param name="startFadeIn">Có sử dụng hiệu ứng fade in khi bắt đầu hay không</param>
        /// <param name="timeFade">Thời gian fade</param>
        /// <param name="effectMoveDone">Có chạy hiệu ứng hoàn thành di chuyển hay không</param>
        /// <param name="effectEndItem">Có chạy hiệu ứng kết thúc vật phẩm hay không</param>
        /// <param name="callback">Hàm callback khi hoàn thành</param>
        /// <param name="animationCurve">Đường cong animation</param>
        /// <param name="typeEffect">Loại hiệu ứng</param>
        public virtual void MoveEffect(float endScale = 1f, float timeScale = 1f, float endAngle = 0f, float rotateSpeed = 100f, Vector3[] movePath = null, float timeMove = 10f, bool startFadeIn = false, float timeFade = 0.1f, bool effectMoveDone = true, bool effectEndItem = true, Action callback = null, AnimationCurve animationCurve = null, AddItemEffectManager.TypeEffect typeEffect = AddItemEffectManager.TypeEffect.Type1) { }

        /// <summary>
        /// Hiệu ứng hoàn thành di chuyển
        /// </summary>
        /// <param name="endScale">Tỷ lệ kích thước kết thúc</param>
        /// <param name="plusValue">Giá trị cộng thêm</param>
        /// <param name="timeScale">Thời gian thay đổi kích thước</param>
        /// <param name="effectMoveDone">Có chạy hiệu ứng hoàn thành di chuyển hay không</param>
        /// <param name="effectEndItem">Có chạy hiệu ứng kết thúc vật phẩm hay không</param>
        /// <param name="callback">Hàm callback khi hoàn thành</param>
        public virtual void MoveDoneEffect(float endScale = 1f, float plusValue = 0f, float timeScale = 0.5f, bool effectMoveDone = true, bool effectEndItem = true, Action callback = null) { }

        /// <summary>
        /// Kết thúc hiệu ứng vật phẩm và gọi sự kiện OnEndItemEffect
        /// </summary>
        public virtual void EndItemEffect()
        {
            OnEndItemEffect?.Invoke();
        }

        /// <summary>
        /// Hiệu ứng bật vật phẩm
        /// </summary>
        /// <param name="scaleValue">Giá trị tỷ lệ kích thước</param>
        /// <param name="time">Thời gian hiệu ứng</param>
        /// <param name="callback">Hàm callback khi hoàn thành</param>
        public virtual void EnableEffect(float scaleValue = 1f, float time = 0.2f, Action callback = null) { }

        /// <summary>
        /// Hiệu ứng tắt vật phẩm
        /// </summary>
        /// <param name="time">Thời gian hiệu ứng</param>
        /// <param name="callback">Hàm callback khi hoàn thành</param>
        public virtual void DisableEffect(float time = 0.2f, Action callback = null) { }

        /// <summary>
        /// Hiệu ứng hiện dần (Fade In)
        /// </summary>
        /// <param name="time">Thời gian hiệu ứng</param>
        public virtual void FadeInEffect(float time = 0.5f) { }

        /// <summary>
        /// Hiệu ứng mờ dần (Fade Out)
        /// </summary>
        /// <param name="time">Thời gian hiệu ứng</param>
        public virtual void FadeOutEffect(float time = 0.5f) { }

        #endregion
    }
}