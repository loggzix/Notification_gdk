using UnityEngine;
using UnityEngine.EventSystems;
using System;

#if DOTWEEN
using DG.Tweening;
#endif

/// <summary>
/// Component xử lý hiệu ứng scale khi nhấn button, kèm theo âm thanh và rung
/// Yêu cầu: Cần có DOTween để sử dụng hiệu ứng scale
/// </summary>
public class ButtonScaler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("CONFIG")]
    // Scale ban đầu của button
    public Vector3 startScale = Vector3.zero;

    // Scale khi button được nhấn
    public Vector3 endScale = Vector3.one;

    [SerializeField]
    private bool useScaleEffect = true;    // Bật/tắt hiệu ứng scale

    [SerializeField]
    private bool useAudio = true;          // Bật/tắt âm thanh khi nhấn

    [SerializeField]
    private bool useVibration = true;      // Bật/tắt rung khi nhấn

    // Event được gọi khi cần phát âm thanh
    public static Action OnAudioAction;

    // Event được gọi khi cần rung
    public static Action OnVibrationAction;

    /// <summary>
    /// Được gọi khi có thay đổi trong Inspector
    /// Tự động cập nhật giá trị scale khi thay đổi trong Editor
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (useScaleEffect)
        {
            // Lưu scale hiện tại của button
            startScale = transform.localScale;
            // Tăng scale lên 0.05 khi nhấn
            endScale = new Vector3(startScale.x + 0.05f, startScale.y + 0.05f, startScale.z);
        }
        else
        {
            endScale = transform.localScale;
        }
    }

    /// <summary>
    /// Được gọi khi người dùng bắt đầu nhấn button
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (useScaleEffect)
        {
            // Tạo animation
#if DOTWEEN
            transform.DOScale(endScale, 0.05f).SetEase(Ease.Linear).SetUpdate(true);
#endif
        }

        if (useAudio)
        {
            // Phát âm thanh nếu được bật
            OnAudioAction?.Invoke();
        }

        if (useVibration)
        {
            // Kích hoạt rung nếu được bật
            OnVibrationAction?.Invoke();
        }
    }

    /// <summary>
    /// Được gọi khi người dùng thả button ra
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (useScaleEffect)
        {
            // Tạo animation
#if DOTWEEN
            transform.DOScale(startScale, 0.1f).SetEase(Ease.Linear).SetUpdate(true);
#endif
        }
    }
}
