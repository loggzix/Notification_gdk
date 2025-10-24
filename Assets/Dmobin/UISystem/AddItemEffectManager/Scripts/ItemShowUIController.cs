using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

#if DOTWEEN
using DG.Tweening;
#endif

namespace DSDK.UISystem
{
    public class ItemShowUIController : ItemShowBase
    {
        [Header("COMPONENTS")]
        [SerializeField] private RectTransform rectTrans;
        public RectTransform rect => rectTrans;
        [SerializeField] private Image displayImage;
        [SerializeField] private TextMeshProUGUI valueText;

        public override void SetImageDisplay(Sprite sprite)
        {
            displayImage.sprite = sprite;

            EnableDisplayImage(true);
        }

        public override void EnableDisplayImage(bool value)
        {
            displayImage.gameObject.SetActive(value);
        }

        public override void SetValueText(string value)
        {
            valueText.text = value;
        }

        public override void Disable()
        {
#if DOTWEEN
            transform.DOKill();
            displayImage.DOKill();
#endif

            transform.localScale = Vector3.one;
            displayImage.color = new Color32(255, 255, 255, 255);

            gameObject.SetActive(false);
        }

        public override void EnableEffect(float scaleValue = 1f, float time = 0.2f, Action callback = null)
        {
#if DOTWEEN
            transform.DOScale(scaleValue, time).SetUpdate(true).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                callback?.Invoke();
                transform.DOKill();
            });
#else
            callback?.Invoke();
#endif
        }

        public override void DisableEffect(float time = 0.2f, Action callback = null)
        {
#if DOTWEEN
            transform.DOScale(0f, time).SetEase(Ease.InOutBack).SetUpdate(true).OnComplete(() =>
            {
                callback?.Invoke();
                Disable();
            });
#else
            callback?.Invoke();
            Disable();
#endif
        }

        public override void MoveEffect(float endScale = 1f, float timeScale = 1f, float endAngle = 0f, float rotateSpeed = 100f, Vector3[] movePath = null, float timeMove = 10f, bool startFadeIn = false, float timeStartFade = 0.2f, bool effectMoveDone = true, bool effectEndItem = true, Action callback = null, AnimationCurve animationCurve = null, AddItemEffectManager.TypeEffect typeEffect = AddItemEffectManager.TypeEffect.Type1)
        {
            if (startFadeIn)
            {
                FadeInEffect(timeStartFade);
            }

#if DOTWEEN
            transform.DOScale(endScale, timeScale).SetEase(Ease.Linear).SetUpdate(true);
            transform.DORotate(new Vector3(0f, 0f, endAngle), rotateSpeed, RotateMode.FastBeyond360).SetUpdate(true).SetSpeedBased(true).SetEase(Ease.Linear);

            if (typeEffect == AddItemEffectManager.TypeEffect.Type1)
            {
                transform.DOPath(movePath, timeMove, PathType.CatmullRom).SetUpdate(true).SetEase(animationCurve).OnComplete(() =>
                {
                    MoveDoneEffect(endScale, 0.2f, 0.5f, effectMoveDone, effectEndItem, callback);
                });
            }
            else if (typeEffect == AddItemEffectManager.TypeEffect.Type2)
            {
                transform.DOPath(movePath, timeMove, PathType.CatmullRom).SetUpdate(true).SetEase(animationCurve).OnComplete(() =>
                {
                    MoveDoneEffect(endScale, 0.2f, 0.5f, effectMoveDone, effectEndItem, callback);
                });
            }
#else
            MoveDoneEffect(endScale, 0.2f, 0.5f, effectMoveDone, effectEndItem, callback);
#endif
        }

        public override void MoveDoneEffect(float endScale = 1f, float plusValue = 0.5f, float timeScale = 0.5f, bool effectMoveDone = true, bool effectEndItem = true, Action callback = null)
        {
            if (effectMoveDone)
            {
                if (displayImage.gameObject.activeInHierarchy)
                {
#if DOTWEEN
                    transform.DOScale(endScale + plusValue, timeScale).SetEase(Ease.Linear).SetUpdate(true);

                    displayImage.DOFade(0f, timeScale).SetEase(Ease.Linear).SetUpdate(true).OnComplete(() =>
                    {
                        Disable();
                    });
#else
                    Disable();
#endif
                }
                else
                {
                    Disable();
                }
            }
            else
            {
                Disable();
            }

            if (effectEndItem)
            {
                EndItemEffect();
            }

            callback?.Invoke();
        }

        public override void FadeInEffect(float time = 0.5f)
        {
            if (displayImage.gameObject.activeInHierarchy)
            {
#if DOTWEEN
                displayImage.DOKill();
                displayImage.DOFade(1f, time).ChangeStartValue(new Color(1, 1, 1, 0)).SetUpdate(true).SetEase(Ease.Linear).SetUpdate(true);
#else
                displayImage.color = new Color32(255, 255, 255, 255);
#endif
            }
        }

        public override void FadeOutEffect(float time = 0.5f)
        {
            if (displayImage.gameObject.activeInHierarchy)
            {
#if DOTWEEN
                displayImage.DOKill();
                displayImage.DOFade(0f, time).SetEase(Ease.Linear).SetUpdate(true);
#else
                displayImage.color = new Color32(255, 255, 255, 0);
#endif
            }
        }

        public Vector3 AnchorPos3D
        {
            get
            {
                return rectTrans.anchoredPosition3D;
            }
            set
            {
                rectTrans.anchoredPosition3D = value;
            }
        }

        public Vector2 AnchorPos
        {
            get
            {
                return rectTrans.anchoredPosition;
            }
            set
            {
                rectTrans.anchoredPosition = value;
            }
        }
    }
}