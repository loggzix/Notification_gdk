
public static class TrackingParamCustom
{
    #region GAME LEVEL
    public enum GameModeName
    {
        none,
        normal
    }

    public enum LocationStartType
    {
        none,
        home,
        gameplay
    }

    public enum LevelFailedReason
    {
        none,
        lose,
        replay,
        game_back_home
    }
    #endregion

    #region RESOURCE
    public enum ItemResourceType
    {
        none,
        coin,
        diamond,
        skin
    }

    public enum ResourceState
    {
        None,
        Earn,
        Spend
    }

    public enum TypeGetResource
    {
        none,
        buy_iap,
        reward_end_level,
        reward,
        spin,
        buy_song,
        unlock_skin,
        x2_reward,
        victory
    }

    public enum TypeItem
    {
        none,
        reward,
        continuity,
        purchase
    }

    public enum Booster
    {
        none
    }
    #endregion

    #region GAME SCREEN
    public enum ButtonName
    {
        none,
        btn_setting_home,
        btn_close,
        btn_music_setting,
        btn_sound_setting,
        btn_vibration_setting,
        btn_back_home,
        btn_retry_no_internet,
        btn_one_to_four_star,
        btn_five_star
    }

    public enum ScreenName
    {
        none,
        menu_home,
        menu_gameplay,
        popup_setting,
        popup_lose,
        popup_victory,
        popup_rate
    }

    public enum ButtonState
    {
        none,
        click,
        show
    }

    public enum Location
    {
        none,
        home,
        gameplay
    }
    #endregion

    #region SKIN
    public enum SkinType
    {
        none,
        skin
    }

    public enum SkinState
    {
        none,
        selected,
        preview,
        get
    }
    #endregion

    #region FEATURE
    public enum FeatureName
    {
        none,
        loading
    }

    public enum FeatureState
    {
        none
    }
    #endregion

    #region USER PROPERTIES
    public enum UserProperties
    {
        none,
        current_level,
        current_play_mode
    }
    #endregion
}