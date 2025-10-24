using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if DOTWEEN
using DG.Tweening;
#endif

namespace DSDK.UISystem
{
    public class ItemSpendShowUIController : MonoBehaviour
    {
        [Header("COMPONENT")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image iconImg;
        [SerializeField] private TextMeshProUGUI valueTxt;
        [SerializeField] private RectTransform childRect;
        [SerializeField] private Color32 colorTextSpend = Color.red;
        [SerializeField] private Color32 colorTextReceive = Color.green;

        public void SetData(Sprite icon, string text, bool isSpend = true)
        {
            iconImg.sprite = icon;
            iconImg.SetNativeSize();

            valueTxt.text = text;

            if (isSpend)
            {
                valueTxt.color = colorTextSpend;
            }
            else
            {
                valueTxt.color = colorTextReceive;
            }
        }

        public void ShowEffect(float anchorXMoveValue = 0f, float anchorYMoveValue = -100f)
        {
#if DOTWEEN
            canvasGroup.DOKill();
            childRect.DOKill();
            childRect.anchoredPosition = Vector2.zero;

            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.Linear).SetUpdate(true);

            childRect.DOAnchorPos(new Vector2(anchorXMoveValue, anchorYMoveValue), 0.5f).SetEase(Ease.OutBack).SetUpdate(true).OnComplete(() =>
            {
                canvasGroup.DOFade(0f, 0.5f).SetDelay(1f).SetEase(Ease.Linear).SetUpdate(true).OnComplete(() =>
                {
                    Disable();
                });
            });
#else
            childRect.anchoredPosition = new Vector2(anchorXMoveValue, anchorYMoveValue);
            canvasGroup.alpha = 1f;

            Invoke(nameof(Disable), 1f);
#endif
        }

        private void Disable()
        {
#if DOTWEEN
            canvasGroup.DOKill();
            childRect.DOKill();
#endif
            childRect.anchoredPosition = Vector2.zero;
            canvasGroup.alpha = 0f;
        }
    }
}