using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if DOTWEEN
using DG.Tweening;
#endif

namespace DSDK.UISystem
{
    public class ItemShowSpriteController : ItemShowBase
    {
        [Header("COMPONENTS")]
        [SerializeField] SpriteRenderer displaySpriteRender;

        public override void SetImageDisplay(Sprite sprite)
        {
            displaySpriteRender.sprite = sprite;

            EnableDisplayImage(true);
        }

        public override void EnableDisplayImage(bool value)
        {
            displaySpriteRender.enabled = value;
        }

        public override void Disable()
        {
#if DOTWEEN
            transform.DOKill();
            displaySpriteRender.DOKill();
#endif

            transform.localScale = Vector3.one;
            displaySpriteRender.color = new Color32(255, 255, 255, 255);

            gameObject.SetActive(false);
        }

        public override void MoveEffect(float endScale = 1f, float timeScale = 1f, float endAngle = 0f, float rotateSpeed = 100f, Vector3[] movePath = null, float timeMove = 10f, bool startFadeIn = false, float timeFade = 0.1f, bool effectMoveDone = true, bool effectEndItem = true, Action callback = null, AnimationCurve animationCurve = null, AddItemEffectManager.TypeEffect typeEffect = AddItemEffectManager.TypeEffect.Type1)
        {
#if DOTWEEN
            transform.DOKill();
            transform.DOScale(endScale, timeScale).SetSpeedBased(true).SetEase(Ease.Linear);
            transform.DORotate(new Vector3(0f, 0f, endAngle), rotateSpeed, RotateMode.FastBeyond360).SetSpeedBased(true).SetEase(Ease.Linear);
            transform.DOPath(movePath, timeMove, PathType.CatmullRom).SetEase(animationCurve).OnComplete(() =>
            {
                MoveDoneEffect(endScale, callback: callback);
            });
#else
            MoveDoneEffect(endScale, 0.2f, 0.5f, effectMoveDone, effectEndItem, callback);
#endif
        }

        public override void MoveDoneEffect(float endScale = 1f, float plusValue = 0.5f, float timeScale = 0.5f, bool effectMoveDone = true, bool effectEndItem = true, Action callback = null)
        {
            if (effectMoveDone)
            {
                if (displaySpriteRender.gameObject.activeInHierarchy)
                {
#if DOTWEEN
                    transform.DOScale(endScale + plusValue, timeScale).SetEase(Ease.Linear).SetUpdate(true);
                    displaySpriteRender.DOFade(0f, timeScale).SetEase(Ease.Linear).SetUpdate(true).OnComplete(() =>
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
    }
}