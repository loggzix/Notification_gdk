/****************************************************
* Script quản lý hiệu ứng khi nhận/tiêu hao item trong game
* Chức năng chính:
* - Hiển thị hiệu ứng khi nhận item (bay từ điểm A đến điểm B)
* - Hiển thị hiệu ứng khi tiêu hao item
* - Quản lý object pooling cho các hiệu ứng
****************************************************/

using UnityEngine;
using System;
using System.Collections;

using DSDK.Core;
using DSDK.Extensions;
using DSDK.Logger;

namespace DSDK.UISystem
{
    public class AddItemEffectManager : SingletonMonoBehaviour<AddItemEffectManager>
    {
        [Header("COMPONENTS")]
        // Data chứa thông tin resource của các item (icon, sprite,...)
        public AddItemResourceData addItemResourceData;

        [Space]
        [Header("UI REFERENCES")]
        [SerializeField] private Canvas canvas; // Canvas chính để hiển thị UI
        [SerializeField] private RectTransform canvasUIRect; // RectTransform của canvas
        [SerializeField] private RectTransform posRewardUIRect; // Vị trí hiển thị UI phần thưởng
        [SerializeField] private RectTransform posItemShowUIRect; // Vị trí hiển thị UI của item
        [SerializeField] private RectTransform posEffectItemUIRect; // Vị trí hiển thị hiệu ứng UI của item
        [SerializeField] private Transform posItemShowSpriteTrans; // Vị trí hiển thị sprite của item
        [SerializeField] private ShowItemEffectInfo[] listShowItemEffectInfo; // Danh sách thông tin hiệu ứng cho từng loại item
        [SerializeField] private ShowItemSpendEffectInfo[] listShowItemSpendEffectInfo; // Danh sách thông tin hiệu ứng tiêu hao item
        private int countShowUI = 0; // Đếm số lượng UI đang hiển thị

        // Event khi hiển thị UI tiêu hao item
        /// <summary>
        /// Action khi hiển thị UI tiêu hao item, trả về loại item và true nếu là tiêu hao, false nếu là nhận
        /// </summary>
        public static Action<AddItemResourceType, bool> OnShowItemSpendUI = null;

        // Events cho hiệu ứng hiển thị sprite
        /// <summary>
        /// Action khi bắt đầu hiệu ứng hiển thị Item Sprite, trả về loại item
        /// </summary>
        public static Action<AddItemResourceType> OnStartShowItemSprite = null;
        /// <summary>
        /// Action khi kết thúc hiệu ứng hiển thị Item Sprite, trả về loại item
        /// </summary>
        public static Action<AddItemResourceType> OnEndShowItemSprite = null;

        // Events cho hiệu ứng hiển thị UI
        /// <summary>
        /// Action khi bắt đầu gọi hiệu ứng hiển thị UI item, trả về loại item và giá trị tổng item nhận được
        /// </summary>
        public static Action<AddItemResourceType, int> OnShowItemUI = null;

        /// <summary>
        /// Action khi bắt đầu hiệu ứng hiển thị hình ảnh UI item chính, trả về loại item
        /// </summary>
        public static Action<AddItemResourceType> OnStartShowRewardItemUI = null;

        /// <summary>
        /// Action khi kết thúc hiệu ứng hiển thị hình ảnh UI item chính, trả về loại item
        /// </summary>
        public static Action<AddItemResourceType> OnEndShowRewardItemUI = null;

        /// <summary>
        /// Action khi bắt đầu hiệu ứng hiển thị hình ảnh một UI item, trả về loại item
        /// </summary>
        public static Action<AddItemResourceType> OnStartShowOneItemUI = null;

        /// <summary>
        /// Action khi kết thúc hiệu ứng hiển thị hình ảnh một UI item, trả về loại item, giá trị riêng lẻ của item đó và có set data không
        /// </summary>
        public static Action<AddItemResourceType, int, bool> OnEndShowOneItemUI = null;

        [Header("CONFIG")]
        [SerializeField] private bool isCustomConfigCanvas = false; // Có custom config canvas không
        [SerializeField] private string customCanvasSortingLayerName = "UI"; // Tên sorting layer custom
        [SerializeField] private int customCanvasSortingOrder = 0; // Order custom

        [Space]
        [Header("ANIMATION CURVE")]
        [SerializeField] private AnimationCurve[] animationCurveUI; // Đường cong animation cho UI
        [SerializeField] private AnimationCurve[] animationCurveSprite; // Đường cong animation cho sprite

        [Space]
        [Header("PREFABS")]
        // Prefab và pool cho UI phần thưởng
        [SerializeField] private ItemShowUIController itemShowRewardUIPrefab;
        private ItemShowUIController[] poolItemRewardUI = new ItemShowUIController[0];

        // Prefab và pool cho UI hiển thị item
        [SerializeField] private ItemShowUIController itemShowUIPrefab;
        private ItemShowUIController[] poolItemShowUI = new ItemShowUIController[0];

        // Prefab và pool cho sprite hiển thị item
        [SerializeField] private ItemShowSpriteController itemShowSpritePrefab;
        private ItemShowSpriteController[] poolItemShowSprite = new ItemShowSpriteController[0];

        #region Unity Event Functions

        private void Start()
        {
            SetupFollowScene();
        }

        /// <summary>
        /// Đăng ký các event listener khi script được enable
        /// </summary>
        private void OnEnable()
        {
            this.AddEventListener(AddItemEffectEvent.UpdateCanvas, UpdateCanvasEventRegisterListener);
            this.AddEventListener<AddItemResourceType, string, bool>(AddItemEffectEvent.ShowSpendItemUI, ShowSpendItemUIEventRegisterListener);
        }

        /// <summary>
        /// Hủy đăng ký các event listener khi script bị disable
        /// </summary>
        private void OnDisable()
        {
            this.RemoveEventListener(AddItemEffectEvent.UpdateCanvas, UpdateCanvasEventRegisterListener);
            this.RemoveEventListener<AddItemResourceType, string, bool>(AddItemEffectEvent.ShowSpendItemUI, ShowSpendItemUIEventRegisterListener);
        }

        #endregion

        #region Event Listeners & Setup

        /// <summary>
        /// Cập nhật canvas theo scene hiện tại
        /// </summary>
        private void UpdateCanvasEventRegisterListener()
        {
            SetupFollowScene();
        }

        /// <summary>
        /// Xử lý event hiển thị UI tiêu hao item
        /// </summary>
        private void ShowSpendItemUIEventRegisterListener(AddItemResourceType itemType = AddItemResourceType.None, string value = "", bool isSpend = true)
        {
            ShowSpendItemUI(itemType, value, isSpend);
        }

        /// <summary>
        /// Thiết lập canvas theo scene hiện tại
        /// </summary>
        public void SetupFollowScene()
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            if (UISystem.IsInstanceValid())
            {
                Canvas sceneCanvas = UISystem.I.mainCanvas;
                canvas.renderMode = sceneCanvas.renderMode;
                canvas.worldCamera = sceneCanvas.worldCamera;
                canvas.sortingLayerName = isCustomConfigCanvas ? customCanvasSortingLayerName : sceneCanvas.sortingLayerName;

                // Lấy ra order in layer lớn nhất từ canvas chính và các canvas con
                int maxSortingOrder = sceneCanvas.sortingOrder;

                // Tìm tất cả các canvas con
                Canvas[] childCanvases = sceneCanvas.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas childCanvas in childCanvases)
                {
                    if (childCanvas != sceneCanvas && childCanvas.sortingOrder > maxSortingOrder)
                    {
                        maxSortingOrder = childCanvas.sortingOrder;
                    }
                }

                // Đặt sorting order cao hơn giá trị lớn nhất tìm được
                canvas.sortingOrder = isCustomConfigCanvas ? customCanvasSortingOrder : maxSortingOrder + 1;
            }
            else
            {
                if (Camera.main)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = Camera.main;
                }

                canvas.sortingLayerName = isCustomConfigCanvas ? customCanvasSortingLayerName : "UI";
                canvas.sortingOrder = isCustomConfigCanvas ? customCanvasSortingOrder : 1;
            }
        }

        /// <summary>
        /// Dừng tất cả các hiệu ứng đang chạy
        /// </summary>
        public void StopAll()
        {
            StopAllCoroutines();

            foreach (ItemShowUIController item in poolItemRewardUI)
            {
                item.Disable();
            }

            foreach (ItemShowUIController item in poolItemShowUI)
            {
                item.Disable();
            }

            foreach (ItemShowSpriteController item in poolItemShowSprite)
            {
                item.Disable();
            }
        }

        #endregion

        #region Spend Effect Functions

        /// <summary>
        /// Hiển thị UI tiêu hao item
        /// </summary>
        /// <param name="type">Loại item</param>
        /// <param name="value">Giá trị tiêu hao</param>
        /// <param name="isSpend">True nếu là tiêu hao, False nếu là nhận</param>
        public void ShowSpendItemUI(AddItemResourceType type, string value, bool isSpend, float anchorXMoveValue = 0f, float anchorYMoveValue = -100f)
        {
            ShowItemSpendEffectInfo item = GetShowItemSpendEffectInfo(type);
            if (item != null)
            {
                item.itemSpendShowUIControl.SetData(addItemResourceData.GetItemResourceInfo(type).listIconUI.Random(), value, isSpend);
                item.itemSpendShowUIControl.ShowEffect(anchorXMoveValue, anchorYMoveValue);

                OnShowItemSpendUI?.Invoke(type, isSpend);
            }
        }

        /// <summary>
        /// Lấy thông tin hiệu ứng tiêu hao item theo loại
        /// </summary>
        private ShowItemSpendEffectInfo GetShowItemSpendEffectInfo(AddItemResourceType type)
        {
            foreach (ShowItemSpendEffectInfo item in listShowItemSpendEffectInfo)
            {
                if (item.itemResourceType == type)
                {
                    return item;
                }
            }

            return null;
        }

        #endregion

        #region Item Sprite Effect Functions

        /// <summary>
        /// Hiển thị hiệu ứng sprite của item
        /// </summary>
        /// <param name="itemType">Loại item</param>
        /// <param name="startPosTrans">Transform vị trí bắt đầu</param>
        /// <param name="endPosTrans">Transform vị trí kết thúc</param>
        /// <param name="amount">Số lượng item</param>
        /// <param name="startScale">Tỷ lệ ban đầu</param>
        /// <param name="endScale">Tỷ lệ kết thúc</param>
        /// <param name="startAngle">Góc ban đầu</param>
        /// <param name="endAngle">Góc kết thúc</param>
        /// <param name="timeBetween">Thời gian giữa các item</param>
        /// <param name="timeMove">Thời gian di chuyển</param>
        /// <param name="startFadeIn">Có fade in khi bắt đầu không</param>
        /// <param name="effectItemDone">Có hiệu ứng khi item hoàn thành không</param>
        /// <param name="effectEndItem">Có hiệu ứng khi kết thúc không</param>
        /// <param name="callbackOne">Callback cho mỗi item</param>
        /// <param name="callback">Callback khi hoàn thành tất cả</param>
        /// <param name="typeEffect">Loại hiệu ứng</param>
        public void ShowItemsSprite(AddItemResourceType itemType, Transform startPosTrans, Transform endPosTrans, int amount, float startScale = 1f, float endScale = 1f, float startAngle = 0f, float endAngle = 0f, float timeBetween = 0.1f, float timeMove = 10f, bool startFadeIn = true, float timeStartFade = 0.1f, bool effectItemDone = true, bool effectEndItem = true, Action callbackOne = null, Action callback = null, TypeEffect typeEffect = TypeEffect.Type1)
        {
            StartCoroutine(WaitEffectItemSprite(itemType, startPosTrans, endPosTrans, amount, startScale, endScale, startAngle, endAngle, timeBetween, timeMove, startFadeIn, timeStartFade, effectItemDone, effectEndItem, callbackOne, callback, typeEffect));
        }

        /// <summary>
        /// Coroutine xử lý hiệu ứng sprite của item
        /// </summary>
        IEnumerator WaitEffectItemSprite(AddItemResourceType itemType, Transform startPosTrans, Transform endPosTrans, int amount, float startScale = 1f, float endScale = 1f, float startAngle = 0f, float endAngle = 0f, float timeBetween = 0.1f, float timeMove = 10f, bool startFadeIn = true, float timeStartFade = 0.1f, bool effectItemDone = true, bool effectEndItem = true, Action callbackOne = null, Action callback = null, TypeEffect typeEffect = TypeEffect.Type1)
        {
            if (amount > 0)
            {
                if (OnEndShowItemSprite != null)
                {
                    callbackOne += () => OnEndShowItemSprite?.Invoke(itemType);
                }

                AddItemResourceInfo itemInfo = ItemInfo(itemType);

                for (int i = 0; i < amount; i++)
                {
                    Vector3 startPos = startPosTrans.position;
                    Vector3 endPos = endPosTrans.position;

                    ItemShowSpriteController itemShowSpriteController = GetItemShowSprite().GetComponent<ItemShowSpriteController>();
                    itemShowSpriteController.ConfigTransformStartValue(startPos, startScale, startAngle);
                    itemShowSpriteController.SetImageDisplay(itemInfo.listIcon.Random());

                    Vector3[] path = CalculateBeautifulCurvePath(startPos, endPos);

                    if (effectEndItem)
                    {
                        itemShowSpriteController.OnEndItemEffect = () =>
                        {
                            GameObject effectEndItemObject = GetEffectEndItemSprite(itemType);

                            if (effectEndItemObject)
                            {
                                effectEndItemObject.transform.position = itemShowSpriteController.transform.position;
                            }
                        };
                    }

                    itemShowSpriteController.MoveEffect(endScale, 1f, endAngle, 100f, path, timeMove, startFadeIn, timeStartFade, effectItemDone, effectEndItem, callbackOne, animationCurveSprite[(int)typeEffect], typeEffect);

                    OnStartShowItemSprite?.Invoke(itemType);

                    yield return new WaitForSeconds(timeBetween);
                }

                callback?.Invoke();
            }
        }

        #endregion

        #region Item UI Effect Functions

        /// <summary>
        /// Hiển thị hiệu ứng UI của item
        /// </summary>
        public void ShowItemsUI(AddItemResourceType itemType, int valueReward, int amountItem = 10, bool isSetData = true, float startScale = 1f, float endScale = 1f, float startAngle = 0f, float endAngle = 0f, float timeBetween = 0.05f, float timeMove = 1f, bool startFadeIn = true, float timeStartFade = 0.2f, bool effectItemDone = true, bool effectEndItem = true, Transform startPosTrans = null, Transform endPosTrans = null, Action callbackOne = null, Action callback = null, TypeEffect typeEffect = TypeEffect.Type1)
        {
            if (valueReward <= 0) return;
            countShowUI++;

            OnShowItemUI?.Invoke(itemType, valueReward);

            AddItemResourceInfo itemInfo = ItemInfo(itemType);

            if (endPosTrans == null)
            {
                endPosTrans = GetEndPosTransWithType(itemType);
            }

            if (startPosTrans == null)
            {
                ItemShowUIController itemRewardUIShow = GetItemShowRewardUI();
                itemRewardUIShow.transform.localPosition = Vector3.zero;
                itemRewardUIShow.transform.localScale = Vector2.zero;
                itemRewardUIShow.transform.SetSiblingIndex(posRewardUIRect.transform.childCount - 1);

                Vector3 anchor = itemRewardUIShow.AnchorPos3D;
                anchor.z = 0;
                itemRewardUIShow.AnchorPos3D = anchor;

                startPosTrans = itemRewardUIShow.transform;

                itemRewardUIShow.SetImageDisplay(itemInfo.listIconUI.Random());
                itemRewardUIShow.EnableDisplayImage(false);
                itemRewardUIShow.SetValueText("");
                itemRewardUIShow.EnableEffect(1f, 0.1f, () =>
                {
                    StartCoroutine(IEAnimateItemUI(itemInfo, itemRewardUIShow, valueReward, amountItem, isSetData, startScale, endScale, startAngle, endAngle, timeBetween, timeMove, startFadeIn, timeStartFade, effectItemDone, effectEndItem, startPosTrans, endPosTrans, callbackOne, callback, typeEffect));

                    OnEndShowRewardItemUI?.Invoke(itemType);
                });

                OnStartShowRewardItemUI?.Invoke(itemType);
            }
            else
            {
                StartCoroutine(IEAnimateItemUI(itemInfo, null, valueReward, amountItem, isSetData, startScale, endScale, startAngle, endAngle, timeBetween, timeMove, startFadeIn, timeStartFade, effectItemDone, effectEndItem, startPosTrans, endPosTrans, callbackOne, callback, typeEffect));
            }
        }

        /// <summary>
        /// Coroutine xử lý animation UI của item
        /// </summary>
        IEnumerator IEAnimateItemUI(AddItemResourceInfo itemInfo, ItemShowUIController itemRewardUIShow = null, int valueReward = 0, int amount = 10, bool isSetData = true, float startScale = 1f, float endScale = 1f, float startAngle = 0f, float endAngle = 0f, float timeBetween = 0.05f, float timeMove = 1f, bool startFadeIn = true, float timeStartFade = 0.2f, bool effectItemDone = true, bool effectEndItem = true, Transform startPosTrans = null, Transform endPosTrans = null, Action callbackOne = null, Action callback = null, TypeEffect typeEffect = TypeEffect.Type1)
        {
            if (amount > valueReward)
            {
                amount = valueReward;
            }

            if (amount > 0)
            {
                int valueOfOneItem = valueReward / amount;
                int surplus = valueReward % amount;

                for (int i = 0; i < amount; i++)
                {
                    if (i == amount - 1)
                    {
                        valueOfOneItem += surplus;
                    }

                    EffectItemUI(itemInfo, startPosTrans.position, endPosTrans.position, valueOfOneItem, isSetData, startScale, endScale, startAngle, endAngle, timeMove, startFadeIn, timeStartFade, effectItemDone, effectEndItem, callbackOne, typeEffect);
                    yield return new WaitForSecondsRealtime(timeBetween);
                }
            }

            yield return new WaitForSecondsRealtime(GetTimeDelayCallback(typeEffect));

            if (itemRewardUIShow)
            {
                itemRewardUIShow.DisableEffect(0.2f, () =>
                {
                    countShowUI--;

                    if (countShowUI <= 0)
                    {

                    }

                    callback?.Invoke();
                });
            }
            else
            {
                countShowUI--;

                if (countShowUI <= 0)
                {

                }

                callback?.Invoke();
            }
        }

        /// <summary>
        /// Xử lý hiệu ứng cho một item UI
        /// </summary>
        private void EffectItemUI(AddItemResourceInfo itemInfo, Vector3 startPos, Vector3 endPos, int valueOfOneItem, bool isSetData = true, float startScale = 1f, float endScale = 1f, float startAngle = 0f, float endAngle = 0f, float timeMove = 1f, bool startFadeIn = true, float timeFade = 0.2f, bool effectItemDone = true, bool effectEndItem = true, Action callbackOne = null, TypeEffect typeEffect = TypeEffect.Type1)
        {
            // Thiết lập các thông số ngẫu nhiên theo loại hiệu ứng
            if (typeEffect == TypeEffect.Type1)
            {
                startPos.x += UnityEngine.Random.Range(-0.5f, 0.5f);
                startPos.y += UnityEngine.Random.Range(-0.5f, 0.5f);
            }
            else if (typeEffect == TypeEffect.Type2)
            {
                startPos.x += UnityEngine.Random.Range(-0.5f, 0.5f);
                startPos.y += UnityEngine.Random.Range(-0.5f, 0.5f);
            }

            // Khởi tạo và cấu hình item UI
            ItemShowUIController itemShowUIController = GetItemShowUI();
            itemShowUIController.ConfigTransformStartValue(startPos, startScale, startAngle);

            Vector3 anchor = itemShowUIController.AnchorPos3D;
            anchor.z = 0;
            itemShowUIController.AnchorPos3D = anchor;
            itemShowUIController.transform.SetSiblingIndex(0);

            itemShowUIController.SetImageDisplay(itemInfo.listIconUI.Random());

            endPos.z = itemShowUIController.rect.position.z;

            // Tính toán đường đi của item
            Vector3 newPosMove = startPos;
            newPosMove.z = itemShowUIController.rect.position.z;

            Vector3[] pathMove = new Vector3[0];

            // Xác định đường đi theo loại hiệu ứng
            if (typeEffect == TypeEffect.Type1)
            {
                pathMove = CalculateBeautifulCurvePath(itemShowUIController.transform.position, endPos);
            }
            else if (typeEffect == TypeEffect.Type2)
            {
                pathMove = CalculateBeautifulCurvePath(itemShowUIController.transform.position, endPos);
            }

            if (OnEndShowOneItemUI != null)
            {
                callbackOne += () => OnEndShowOneItemUI?.Invoke(itemInfo.type, valueOfOneItem, isSetData);
            }

            // Thiết lập hiệu ứng kết thúc
            if (effectEndItem)
            {
                itemShowUIController.OnEndItemEffect = () =>
                {
                    GameObject effectEndItemObject = GetEffectEndItem(itemInfo.type);

                    if (effectEndItemObject)
                    {
                        effectEndItemObject.transform.position = itemShowUIController.transform.position;
                    }
                };
            }

            // Kích hoạt hiệu ứng di chuyển
            itemShowUIController.MoveEffect(endScale, 1f, endAngle, 100f, pathMove, timeMove, startFadeIn, timeFade, effectItemDone, effectEndItem, callbackOne, animationCurveUI[(int)typeEffect], typeEffect);

            OnStartShowOneItemUI?.Invoke(itemInfo.type);
        }

        #endregion

        #region Utility Functions

        /// <summary>
        /// Lấy thông tin resource của item theo loại
        /// </summary>
        public AddItemResourceInfo ItemInfo(AddItemResourceType type = AddItemResourceType.Coin)
        {
            foreach (AddItemResourceInfo info in addItemResourceData.listItemResource)
            {
                if (info.type == type)
                {
                    return info;
                }
            }
            return null;
        }

        /// <summary>
        /// Lấy vị trí kết thúc theo loại item
        /// </summary>
        public Transform GetEndPosTransWithType(AddItemResourceType type = AddItemResourceType.Coin)
        {
            foreach (ShowItemEffectInfo pos in listShowItemEffectInfo)
            {
                if (pos.itemResourceType == type)
                {
                    return pos.recTrans;
                }
            }

            return transform;
        }

        /// <summary>
        /// Lấy thời gian delay callback theo loại hiệu ứng
        /// </summary>
        public float GetTimeDelayCallback(TypeEffect typeEffect)
        {
            switch (typeEffect)
            {
                case TypeEffect.Type1:
                    return 2f;
                case TypeEffect.Type2:
                    return 2f;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Lấy UI phần thưởng từ pool hoặc tạo mới nếu pool rỗng
        /// </summary>
        private ItemShowUIController GetItemShowRewardUI()
        {
            if (poolItemRewardUI.Length > 0)
            {
                foreach (ItemShowUIController obj in poolItemRewardUI)
                {
                    if (obj && !obj.gameObject.activeInHierarchy)
                    {
                        obj.gameObject.SetActive(true);
                        return obj;
                    }
                }
            }

            if (itemShowRewardUIPrefab)
            {
                ItemShowUIController objNew = Instantiate(itemShowRewardUIPrefab, posRewardUIRect);
                poolItemRewardUI = (ItemShowUIController[])ArrayExtension.AddElement(poolItemRewardUI, objNew);
                return objNew;
            }

            return null;
        }

        /// <summary>
        /// Lấy UI item từ pool hoặc tạo mới
        /// </summary>
        private ItemShowUIController GetItemShowUI()
        {
            if (poolItemShowUI.Length > 0)
            {
                foreach (ItemShowUIController obj in poolItemShowUI)
                {
                    if (obj && !obj.gameObject.activeInHierarchy)
                    {
                        obj.gameObject.SetActive(true);
                        return obj;
                    }
                }
            }

            if (itemShowUIPrefab)
            {
                ItemShowUIController objNew = Instantiate(itemShowUIPrefab, posItemShowUIRect);
                poolItemShowUI = (ItemShowUIController[])ArrayExtension.AddElement(poolItemShowUI, objNew);
                return objNew;
            }

            return null;
        }

        /// <summary>
        /// Lấy sprite item từ pool hoặc tạo mới
        /// </summary>
        private ItemShowSpriteController GetItemShowSprite()
        {
            foreach (ItemShowSpriteController obj in poolItemShowSprite)
            {
                if (obj && !obj.gameObject.activeInHierarchy)
                {
                    obj.gameObject.SetActive(true);
                    return obj;
                }
            }

            if (itemShowSpritePrefab)
            {
                ItemShowSpriteController objNew = Instantiate(itemShowSpritePrefab, posItemShowSpriteTrans);
                poolItemShowSprite = (ItemShowSpriteController[])ArrayExtension.AddElement(poolItemShowSprite, objNew);
                return objNew;
            }

            return null;
        }

        /// <summary>
        /// Lấy hiệu ứng kết thúc UI từ pool hoặc tạo mới
        /// </summary>
        private GameObject GetEffectEndItem(AddItemResourceType itemType)
        {
            foreach (ShowItemEffectInfo item in listShowItemEffectInfo)
            {
                if (item.itemResourceType == itemType)
                {
                    foreach (GameObject obj in item.poolEffectEndItemUI)
                    {
                        if (obj && !obj.activeInHierarchy)
                        {
                            obj.SetActive(true);
                            return obj;
                        }
                    }

                    if (item.effectEndItemUIPrefab)
                    {
                        GameObject objNew = Instantiate(item.effectEndItemUIPrefab, posEffectItemUIRect);
                        item.poolEffectEndItemUI = (GameObject[])ArrayExtension.AddElement(item.poolEffectEndItemUI, objNew);
                        return objNew;
                    }

                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Lấy hiệu ứng kết thúc sprite từ pool hoặc tạo mới
        /// </summary>
        private GameObject GetEffectEndItemSprite(AddItemResourceType itemType)
        {
            foreach (ShowItemEffectInfo item in listShowItemEffectInfo)
            {
                if (item.itemResourceType == itemType)
                {
                    foreach (GameObject obj in item.poolEffectEndItemSprite)
                    {
                        if (obj && !obj.activeInHierarchy)
                        {
                            obj.SetActive(true);
                            return obj;
                        }
                    }

                    if (item.effectEndItemSpritePrefab)
                    {
                        GameObject objNew = Instantiate(item.effectEndItemSpritePrefab, posItemShowSpriteTrans);
                        item.poolEffectEndItemSprite = (GameObject[])ArrayExtension.AddElement(item.poolEffectEndItemSprite, objNew);
                        return objNew;
                    }

                    break;
                }
            }

            return null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Tính toán đường cong đẹp mắt cho Type1 effect (tự động tính randomX, randomY dựa trên góc)
        /// </summary>
        protected Vector3[] CalculateBeautifulCurvePath(Vector3 startPos, Vector3 endPos)
        {
            // Tính khoảng cách giữa điểm bắt đầu và kết thúc
            float distance = Vector3.Distance(startPos, endPos);

            // Tính toán điểm control để tạo đường cong tự nhiên
            Vector3 direction = (endPos - startPos).normalized;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f); // Vector vuông góc

            // Tính góc giữa điểm đầu và điểm cuối
            float angle = AngleBetweenTwoPoint(startPos, endPos);

            // Tự động tính toán randomX và randomY dựa trên góc và khoảng cách
            float randomX = Mathf.Sin(angle * Mathf.Deg2Rad) * UnityEngine.Random.Range(-1.5f, 1.5f);
            float randomY = Mathf.Cos(angle * Mathf.Deg2Rad) * UnityEngine.Random.Range(-1.5f, 1.5f);

            // Tạo độ cao đường cong vừa phải
            float curveHeight = Mathf.Clamp(distance * 0.008f, 2f, 8f);

            // Xác định hướng cong dựa trên vị trí tương đối của điểm đầu và cuối
            float curveDirection = 1f; // Mặc định cong sang phải

            // Nếu điểm cuối ở bên trái điểm đầu, cong sang trái
            if (endPos.x < startPos.x)
            {
                curveDirection = 1f;
            }
            // Nếu điểm cuối ở bên phải điểm đầu, cong sang phải
            else if (endPos.x > startPos.x)
            {
                curveDirection = -1f;
            }
            // Nếu cùng tọa độ x, dựa vào randomX để quyết định
            else
            {
                curveDirection = randomX >= 0 ? -1f : 1f;
            }

            // Thêm yếu tố ngẫu nhiên nhỏ
            float randomCurveOffset = UnityEngine.Random.Range(0.95f, 1.05f); // Tăng biên độ: 0.99f-1.01f → 0.95f-1.05f
            curveHeight *= randomCurveOffset;

            // Tính điểm giữa với độ cao đường cong
            Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f);

            // Áp dụng hướng cong và độ lệch vừa phải
            float horizontalOffset = randomX * distance * 0.002f; // Giảm nhẹ: 0.003f → 0.002f
            float verticalOffset = randomY * distance * 0.001f; // Giảm nhẹ: 0.002f → 0.001f

            midPoint += perpendicular * curveDirection * (curveHeight + horizontalOffset);
            midPoint.y += verticalOffset; // Thêm biến thiên dọc

            // Điều chỉnh độ cao dựa trên hướng di chuyển
            if (endPos.y > startPos.y)
            {
                // Nếu di chuyển lên trên, tăng độ cao đường cong vừa phải
                midPoint.y += curveHeight * 0.03f; // Giảm nhẹ: 0.05f → 0.03f
            }
            else
            {
                // Nếu di chuyển xuống dưới, vẫn tạo cung đẹp
                midPoint.y += curveHeight * 0.02f; // Giảm nhẹ: 0.03f → 0.02f
            }

            // Tạo thêm một điểm control phụ để đường cong mượt mà hơn
            Vector3 controlPoint1 = Vector3.Lerp(startPos, midPoint, 0.5f);
            Vector3 controlPoint2 = Vector3.Lerp(midPoint, endPos, 0.5f);

            // Thêm biến thiên vừa phải cho các điểm control
            controlPoint1 += perpendicular * curveDirection * (curveHeight * 0.03f); // Giảm nhẹ: 0.04f → 0.03f
            controlPoint2 += perpendicular * curveDirection * (curveHeight * 0.02f); // Giảm nhẹ: 0.03f → 0.02f

            // Thêm một chút biến thiên dọc vừa phải cho các control point
            controlPoint1.y += randomY * 0.3f; // Giảm nhẹ: 0.5f → 0.3f
            controlPoint2.y += randomY * 0.2f; // Giảm nhẹ: 0.3f → 0.2f

            // Trả về mảng các điểm tạo thành đường cong 4 điểm (tối ưu cho CatmullRom)
            return new Vector3[] { startPos, controlPoint1, controlPoint2, endPos };
        }

        /// <summary>
        /// Tính góc giữa hai điểm
        /// </summary>
        public float AngleBetweenTwoPoint(Vector3 pos1, Vector3 pos2)
        {
            float deltaX = pos2.x - pos1.x;
            float deltaY = pos2.y - pos1.y;
            float angleInRadians = Mathf.Atan2(deltaY, deltaX);
            float angleInDegrees = Mathf.Rad2Deg * angleInRadians;
            return angleInDegrees;
        }
        #endregion

        #region Debug

        /// <summary>
        /// Vẽ gizmos để debug vị trí hiển thị item
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;

            foreach (ShowItemEffectInfo item in listShowItemEffectInfo)
            {
                Gizmos.DrawWireCube(item.recTrans.position, new Vector3(item.recTrans.sizeDelta.x, item.recTrans.sizeDelta.y, 0f));
            }
        }

        #endregion

        #region Data Classes

        /// <summary>
        /// Thông tin về hiệu ứng hiển thị item
        /// </summary>
        [Serializable]
        public class ShowItemEffectInfo
        {
            [Header("COMPONENTS")]
            public AddItemResourceType itemResourceType = AddItemResourceType.None; // Loại item
            public RectTransform recTrans; // Transform cho vị trí hiển thị

            [Header("EFFECT END ITEM UI")]
            public GameObject effectEndItemUIPrefab; // Prefab hiệu ứng kết thúc UI
            [HideInInspector] public GameObject[] poolEffectEndItemUI = new GameObject[0]; // Pool các object hiệu ứng UI

            [Header("EFFECT END ITEM SPRITE")]
            public GameObject effectEndItemSpritePrefab; // Prefab hiệu ứng kết thúc sprite
            [HideInInspector] public GameObject[] poolEffectEndItemSprite = new GameObject[0]; // Pool các object hiệu ứng sprite
        }

        /// <summary>
        /// Thông tin về hiệu ứng tiêu hao item
        /// </summary>
        [Serializable]
        public class ShowItemSpendEffectInfo
        {
            public AddItemResourceType itemResourceType = AddItemResourceType.None; // Loại item
            public ItemSpendShowUIController itemSpendShowUIControl; // Controller hiệu ứng tiêu hao
        }

        /// <summary>
        /// Các loại hiệu ứng di chuyển item
        /// </summary>
        public enum TypeEffect
        {
            Type1, // Hiệu ứng bay theo đường cong (thường sử dụng khi mua item)
            Type2, // Hiệu ứng bay theo đường cong (thường sử dụng khi dùng item vào việc gì đó)
        }

        #endregion

        #region TEST
        [ContextMenu("Test")]
        private void Test()
        {
            ShowItemsUI(AddItemResourceType.Coin, 101, callbackOne: () =>
            {
                DLogger.LogInfo($"[DSDK] AddItemEffectManager: Test - End One Item UI");
            },
            callback: () =>
            {
                DLogger.LogInfo($"[DSDK] AddItemEffectManager: Test - End All Item UI");
            },
            typeEffect: TypeEffect.Type1);
        }
        #endregion
    }
}