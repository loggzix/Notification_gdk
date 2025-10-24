using DSDK.Analytics;
using System;
using UnityEngine;
using static TrackingParamCustom;
using DSDK.Logger;
using DSDK.Core;
using DSDK.Extensions;

#if GDK_USE_ADJUST
using AdjustSdk;
#endif

public static class TrackingManagerCustom
{
    private static LocationStartType _locationStart;
    public static LocationStartType LocationStart
    {
        get
        {
            return _locationStart;
        }
        set
        {
            _locationStart = value;
        }
    }

    #region GAME LEVEL
    // Gọi khi bắt đầu một level
    public static void TrackingLevelStart(GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
            (TrackingParam.play_mode, gameModeName),
            (TrackingParam.level, TrackingManager.I.Level),
            ("location_start", LocationStart),
            ("time_event", (int)Time.unscaledTime)
        };

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent(TrackingEvent.level_start, parameters);


#if GDK_USE_AIR_BRIDGE
        AirbridgeEvent @event = new AirbridgeEvent("level_start");
        @event.SetLabel("level");
        @event.SetValue(TrackingManager.I.Level);
        @event.AddCustomAttribute("play_mode", gameModeName);
        AirbridgeUnity.TrackEvent(@event);
#endif
    }

    //Gọi khi kết thúc một level (thua, thắng, replay...)
    public static void TrackingLevelEnd(GameModeName gameModeName, bool success, LevelFailedReason reason, (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
            (TrackingParam.play_mode, gameModeName),
            (TrackingParam.level, TrackingManager.I.Level),
            (TrackingParam.success, success)
        };

        if (!success)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, new (object, object)[] { (TrackingParam.reason, reason) });
        }

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent(TrackingEvent.level_end, parameters);

#if GDK_USE_AIR_BRIDGE
        AirbridgeEvent @event = new AirbridgeEvent("level_end");
        @event.SetLabel("level");
        @event.SetValue(level);
        @event.AddCustomAttribute("play_mode", playMode);
        @event.AddCustomAttribute("success", success);
        AirbridgeUnity.TrackEvent(@event);
#endif
    }

    #endregion

    #region RESOURCE
    /// <summary>
    /// Gọi khi kiếm hoặc tiêu một Resource
    /// </summary>
    /// <param name="resourceState">Earn hoặc Spend</param>
    /// <param name="resourceName">Tên Resource</param>
    /// <param name="amount">số lượng nhận được hoặc tiêu</param>
    /// <param name="balance">Tổng Resource sau khi nhận hoặc tiêu</param>
    /// <param name="itemTypeGet">phương thức nhận được hoặc tiêu</param>
    /// <param name="typeItem">loại item</param>
    /// <param name="booster">tên của booster sử dụng tại thời điểm nhận/tiêu</param>
    /// <param name="gameModeName">tên của game mode</param>
    /// <param name="moreParameters">thêm các tham số khác</param>
    public static void TrackingResource(ResourceState resourceState, ItemResourceType resourceName, int amount, int balance, TypeGetResource itemTypeGet, TypeItem typeItem, Booster booster, GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        if (resourceState == ResourceState.Earn)
        {
            TrackingResourceSource(resourceName, amount, balance, itemTypeGet, typeItem, booster, gameModeName, moreParameters);
        }
        else
        {
            TrackingResourceSink(resourceName, amount, balance, itemTypeGet, typeItem, booster, gameModeName, moreParameters);
        }
    }

    //hàm private khi kiếm được resource
    static void TrackingResourceSource(ItemResourceType resourceName, int amount, int balance, TypeGetResource itemTypeGet, TypeItem typeItem, object booster, GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
            (TrackingParam.play_mode, gameModeName),
            (TrackingParam.level, TrackingManager.I.Level),
            (TrackingParam.name, resourceName),
            (TrackingParam.amount, amount),
            (TrackingParam.balance, balance),
            (TrackingParam.item, itemTypeGet),
            (TrackingParam.item_type, typeItem),
            (TrackingParam.booster, booster),
            (TrackingParam.location, TrackingManager.I.ResourceLocation),
            ("time_event", (int)Time.unscaledTime)
        };

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent(TrackingEvent.resource_source, parameters);
    }


    //hàm private khi tiêu resource
    static void TrackingResourceSink(ItemResourceType resourceName, int amount, int balance, TypeGetResource itemTypeGet, TypeItem typeItem, object booster, GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
            (TrackingParam.play_mode, gameModeName),
            (TrackingParam.level, TrackingManager.I.Level),
            (TrackingParam.name, resourceName),
            (TrackingParam.amount, amount),
            (TrackingParam.balance, balance),
            (TrackingParam.item, itemTypeGet),
            (TrackingParam.item_type, typeItem),
            (TrackingParam.booster, booster),
            (TrackingParam.location, TrackingManager.I.ResourceLocation),
            ("time_event", (int)Time.unscaledTime)
        };

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent(TrackingEvent.resource_sink, parameters);
    }
    #endregion

    #region GAME SCREEN
    // Gọi khi ấn vào một Button, khi một popup mới tự open mà không ấn vào button nào
    public static void TrackingGameScreen(ButtonName buttonName, ButtonState buttonState, ScreenName sceenNameOpen, ScreenName sceenNameClose, int valueInt = 0, string valueString = "none", float valueFloat = 0f, GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
           (TrackingParam.play_mode, gameModeName),
           (TrackingParam.level, TrackingManager.I.Level),
           (TrackingParam.location, TrackingManager.I.Location),
           (TrackingParam.prelocation, TrackingManager.I.Prelocation),
           (TrackingParam.button, buttonName),
           (TrackingParam.state, buttonState),
           ("screen_open", sceenNameOpen),
           ("screen_close", sceenNameClose),
           ("button_value_int", valueInt),
           ("button_value_string", valueString),
           ("button_value_float", valueFloat),
           ("time_event", (int)Time.unscaledTime)
        };

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent(TrackingEvent.game_screen, parameters);
    }

    public static void TrackingGameScreenAutoOpen(ScreenName screenOpen, ScreenName screenClose = ScreenName.none, int valueInt = 0, string valueString = "none", float valueFloat = 0f, GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        if (screenClose == ScreenName.none)
        {
            screenClose = GetLocation();
        }

        TrackingGameScreen(ButtonName.none, ButtonState.show, screenOpen, screenClose, valueInt, valueString, valueFloat, gameModeName, moreParameters);

        if (screenOpen != ScreenName.none)
        {
            SetLocation(screenOpen);
        }
    }

    public static void TrackingGameScreenButtonClick(ButtonName buttonName, ScreenName sceenOpen = ScreenName.none, ScreenName screenClose = ScreenName.none, int valueInt = 0, string valueString = "none", float valueFloat = 0f, GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        if (screenClose == ScreenName.none && sceenOpen != ScreenName.none)
        {
            screenClose = GetLocation();
        }

        TrackingGameScreen(buttonName, ButtonState.click, sceenOpen, screenClose, valueInt, valueString, valueFloat, gameModeName, moreParameters);

        if (sceenOpen != ScreenName.none)
        {
            // ấn button => mở popup mới
            SetLocation(sceenOpen);
        }
    }
    #endregion

    #region SKIN
    public static void TrackingSkin(int skinId, SkinType skinType, SkinState skinState, GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
            (TrackingParam.play_mode, gameModeName),
            (TrackingParam.level, TrackingManager.I.Level),
            ("skin_id", skinId),
            ("skin_type", skinType),
            ("skin_state", skinState),
            (TrackingParam.location, TrackingManager.I.SkinLocation),
            ("time_event", (int)Time.unscaledTime)
        };

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent(TrackingEvent.skin, parameters);
    }
    #endregion

    #region FEATURE
    public static void TrackingFeature(FeatureName featureName = FeatureName.none, FeatureState featureState = FeatureState.none, int featureValueInt = 0, string featureValueString = "none", GameModeName gameModeName = GameModeName.none, (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
            ("feature_name", featureName),
            ("feature_state", featureState),
            ("feature_value_int", featureValueInt),
            ("feature_value_string", featureValueString),
            ("time_event", (int)Time.unscaledTime)
        };

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent("feature", parameters);
    }
    #endregion

    #region MORE GAME PUBSCALE
    public static void TrackingMoreGamePubScale(string more_game_ad_tag = "none", string more_game_state = "none", string game_target = "none", (object, object)[] moreParameters = null)
    {
        (object, object)[] parameters = new (object, object)[]
        {
            ("more_game_ad_tag", more_game_ad_tag),
            ("more_game_state", more_game_state),
            ("game_target", game_target),
            ("time_event", (int)Time.unscaledTime)
        };

        if (moreParameters != null)
        {
            parameters = ((object, object)[])ArrayExtension.AddElement(parameters, moreParameters);
        }

        TrackingManager.I.LogEvent("more_games", parameters);
    }

    #endregion

    #region USER PROPERTIES
    public static ScreenName GetPreLocation()
    {
        ScreenName screenName;
        bool tryParseType = Enum.TryParse<ScreenName>(TrackingManager.I.Prelocation, out screenName);
        if (tryParseType)
        {

            return screenName;
        }
        return ScreenName.none;
    }

    public static void SetLocation(ScreenName location)
    {
        DLogger.LogColor(Color.cyan, "Tracking: game_screen :" + location);
        TrackingManager.I.Location = location.ToString();
    }

    public static ScreenName GetLocation()
    {
        ScreenName screenName;

        bool tryParseType = Enum.TryParse<ScreenName>(TrackingManager.I.Location, out screenName);
        if (tryParseType)
        {
            return screenName;
        }

        return ScreenName.none;
    }

    //Gọi khi thay đổi giá trị của các item thuộc UserProperties
    public static void SetUserProperties(UserProperties userProperties, object value)
    {
        TrackingManager.I.SetUserProperty(userProperties.ToString(), value.ToString());
    }

    #endregion
}