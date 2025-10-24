using UnityEngine;
using DSDK.Core;
using DSDK.Analytics;
using DSDK.Data;
using static TrackingParamCustom;

namespace Dmobin.Manager
{
    /// <summary>
    /// Core game manager that handles gameplay flow, level progression, and game state management.
    /// This singleton manages game states (playing, victory, lose, revive), level data, resource collection, and analytics tracking.
    /// </summary>
    public class GameManager : SingletonMonoBehaviour<GameManager>
    {
        [Header("SETTINGS")]
        /// <summary>
        /// Whether to automatically start the game when the scene loads
        /// </summary>
        public bool playOnStart = true;

        /// <summary>
        /// Delay time in seconds before showing the revive popup after game failure
        /// </summary>
        [SerializeField] protected float delayTimeShowRevive = 1f;

        /// <summary>
        /// Delay time in seconds before showing the lose popup after game failure
        /// </summary>
        [SerializeField] protected float delayTimeShowLose = 1f;

        /// <summary>
        /// Delay time in seconds before showing the victory popup after level completion
        /// </summary>
        [SerializeField] protected float delayTimeShowVictory = 1f;

        // Game status
        [Header("INFO")]
        /// <summary>
        /// Game mode override - if set to 'none', will use the static gameMode value instead.
        /// Use this when you don't want to retrieve game mode from user data.
        /// </summary>
        [Tooltip("Nếu giá trị này khác NONE thì sẽ lấy giá trị này, nếu bằng NONE thì sẽ lấy giá trị từ GAME MODE STATIC, sử dụng cho trường hợp không muốn lấy game mode từ user data")]
        public GameModeName gameModeName = GameModeName.none;

        /// <summary>
        /// The primary game mode key used throughout the game system
        /// </summary>
        [Tooltip("Giá trị này sử dụng làm KEY CHÍNH")]
        public static GameModeName gameMode = GameModeName.none;

        /// <summary>
        /// Current level number the player is on
        /// </summary>
        public int currentLevel = 1; // Current level

        /// <summary>
        /// Level-specific data containing play statistics and configuration
        /// </summary>
        public LevelPlayInfoData levelPlayData; // Data for current level

        [Header("GAMEPLAY STATUS")]
        /// <summary>
        /// Current state of the gameplay (none, playing, victory, lose, revive)
        /// </summary>
        public eGamePlayState gameplayStatus = eGamePlayState.none;

        /// <summary>
        /// Reason why the level failed (used for analytics tracking)
        /// </summary>
        public LevelFailedReason levelFailedReason = LevelFailedReason.none;

        /// <summary>
        /// Checks if gameplay state can be changed (not in victory or lose state)
        /// </summary>
        public virtual bool CanCheckGameplay => gameplayStatus != eGamePlayState.victory && gameplayStatus != eGamePlayState.lose;

        /// <summary>
        /// Checks if current gameplay status matches the specified state
        /// </summary>
        /// <param name="gamePlayState">State to check against</param>
        /// <returns>True if states match</returns>
        public bool IsGamePlayStatus(eGamePlayState gamePlayState) => gameplayStatus == gamePlayState;

        [Header("REVIVE")]
        /// <summary>
        /// Whether revive attempts are limited or unlimited
        /// </summary>
        protected bool limitRevive = true; // Whether revive is limited or not

        /// <summary>
        /// Maximum number of times the revive popup can be shown per level
        /// </summary>
        protected int maxCountShowRevive = 1; // Maximum number of times to show revive popup

        /// <summary>
        /// Current count of how many times revive popup has been shown this level
        /// </summary>
        protected int currentCountShowRevive = 0; // Current number of times that revive popup has been shown


        [Header("RESOURCES")]
        /// <summary>
        /// Current score accumulated during gameplay
        /// </summary>
        public int Score = 0; // Current score

        /// <summary>
        /// Current number of stars collected
        /// </summary>
        public int Star = 0; // Current star

        /// <summary>
        /// Total number of coins collected during current level/session
        /// </summary>
        public int CoinCollected // Current number of coins collected
        {
            get;
            private set;
        }

        /// <summary>
        /// Total number of diamonds collected during current level/session
        /// </summary>
        public int DiamondCollected // Current number of diamonds collected
        {
            get;
            private set;
        }

        /// <summary>
        /// Initialize event listeners and setup game state when the scene starts
        /// </summary>
        protected virtual void Start()
        {
            this.AddEventListener(EventConstants.Victory, VictoryEventRegisterListener);
            this.AddEventListener(EventConstants.Lose, LoseEventRegisterListener);
            this.AddEventListener(EventConstants.PauseGame, PauseGameEventRegisterListener);
            this.AddEventListener(EventConstants.ResumeGame, ResumeGameEventRegisterListener);

            SetUpStart();
        }

        /// <summary>
        /// Clean up event listeners when the GameManager is destroyed
        /// </summary>
        protected virtual void OnDestroy()
        {
            this.RemoveEventListener(EventConstants.Victory, VictoryEventRegisterListener);
            this.RemoveEventListener(EventConstants.Lose, LoseEventRegisterListener);
            this.RemoveEventListener(EventConstants.PauseGame, PauseGameEventRegisterListener);
            this.RemoveEventListener(EventConstants.ResumeGame, ResumeGameEventRegisterListener);
        }

        #region REGISTER LISTENER EVENT

        /// <summary>
        /// Event listener callback for victory events
        /// </summary>
        protected virtual void VictoryEventRegisterListener()
        {
            Victory();
        }

        /// <summary>
        /// Event listener callback for lose events
        /// </summary>
        protected virtual void LoseEventRegisterListener()
        {
            Lose();
        }

        /// <summary>
        /// Event listener callback for pause game events
        /// </summary>
        protected virtual void PauseGameEventRegisterListener()
        {
            PauseGame();
        }

        /// <summary>
        /// Event listener callback for resume game events
        /// </summary>
        protected virtual void ResumeGameEventRegisterListener()
        {
            ResumeGame();
        }

        #endregion

        /// <summary>
        /// Initialize game configuration, load level data, and optionally start gameplay
        /// </summary>
        protected virtual void SetUpStart()
        {
            if (gameModeName != GameModeName.none)
            {
                gameMode = gameModeName;
            }
            else
            {
                gameModeName = gameMode;
            }

            currentLevel = GameLevelData.I.CurrentLevel;
            levelPlayData = GameLevelData.I.GetLevelPlayInfoData(currentLevel);

            TrackingManager.I.Level = currentLevel;
            TrackingManager.I.PlayMode = gameMode.ToString();

            if (playOnStart)
            {
                PlayGame();
            }
        }

        /// <summary>
        /// Set the game mode for both static and instance variables
        /// </summary>
        /// <param name="value">Game mode to set</param>
        public void SetGameMode(GameModeName value)
        {
            gameMode = value;
            gameModeName = value;
        }

        #region STATUS INGAME

        /// <summary>
        /// Start the gameplay and change state to playing. Tracks gameplay start analytics.
        /// </summary>
        public virtual void PlayGame()
        {
            if (gameplayStatus == eGamePlayState.playing)
            {
                return;
            }

            gameplayStatus = eGamePlayState.playing;

            TrackingPlaygame();
        }

        /// <summary>
        /// Handle level victory - advance to next level and show victory popup after delay
        /// </summary>
        public virtual void Victory()
        {
            if (!CanCheckGameplay) return;
            gameplayStatus = eGamePlayState.victory;
            GameLevelData.I.CurrentLevel++;

            Invoke(nameof(ShowVictoryPopup), delayTimeShowVictory);

            TrackingVictory();
        }

        /// <summary>
        /// Show the victory popup UI (to be implemented by derived classes)
        /// </summary>
        protected virtual void ShowVictoryPopup()
        {
            // TODO: Show victory popup
        }

        /// <summary>
        /// Handle level failure and show lose popup after delay
        /// </summary>
        public virtual void Lose()
        {
            if (!CanCheckGameplay) return;
            gameplayStatus = eGamePlayState.lose;
            Invoke(nameof(ShowLosePopup), delayTimeShowLose);

            TrackingLose();
        }

        /// <summary>
        /// Show the lose popup UI (to be implemented by derived classes)
        /// </summary>
        protected virtual void ShowLosePopup()
        {
            // TODO: Show lose popup
        }

        /// <summary>
        /// Pause the game
        /// </summary>
        protected virtual void PauseGame()
        {
            if (!CanCheckGameplay) return;
            gameplayStatus = eGamePlayState.pause;
        }

        /// <summary>
        /// Resume the game
        /// </summary>
        protected virtual void ResumeGame()
        {
            if (!CanCheckGameplay) return;
            gameplayStatus = eGamePlayState.playing;
        }

        /// <summary>
        /// Check if revive is available and either show revive popup or trigger lose state
        /// </summary>
        public virtual void CheckRevive()
        {
            if (gameplayStatus == eGamePlayState.revive) return;
            gameplayStatus = eGamePlayState.revive;
            if (limitRevive)
            {
                if (currentCountShowRevive >= maxCountShowRevive)
                {
                    Lose();
                }
                else
                {
                    currentCountShowRevive++;
                    Invoke(nameof(ShowRevivePopup), delayTimeShowRevive);
                }
            }
            else
            {
                Invoke(nameof(ShowRevivePopup), delayTimeShowRevive);
            }
        }

        /// <summary>
        /// Revive the player and continue gameplay
        /// </summary>
        public virtual void Revive()
        {
            gameplayStatus = eGamePlayState.playing;
            this.DispatchEvent(EventConstants.Revive);
        }

        /// <summary>
        /// Show the revive popup UI (to be implemented by derived classes)
        /// </summary>
        protected virtual void ShowRevivePopup()
        {
            // TODO: Show revive popup
        }

        /// <summary>
        /// Skip the current level and advance to the next one. Tracks skip analytics.
        /// </summary>
        public virtual void SkipLevel()
        {
            GameLevelData.I.CurrentLevel++;

            TrackingSkip();
        }

        /// <summary>
        /// Replay the current level. Tracks replay analytics.
        /// </summary>
        public virtual void ReplayLevel()
        {
            TrackingReplay();
        }

        #endregion

        #region RESOURCES
        /// <summary>
        /// Add collected resources to the total amount collected during this session
        /// </summary>
        /// <param name="type">Type of resource (coin, diamond, etc.)</param>
        /// <param name="value">Amount to add</param>
        public virtual void CollectResources(ItemResourceType type, int value)
        {
            switch (type)
            {
                case ItemResourceType.coin:
                    CoinCollected += value;
                    break;
                case ItemResourceType.diamond:
                    DiamondCollected += value;
                    break;
            }
        }

        /// <summary>
        /// Set the total amount of collected resources to a specific value
        /// </summary>
        /// <param name="type">Type of resource (coin, diamond, etc.)</param>
        /// <param name="value">New total value</param>
        public virtual void SetCollectedResources(ItemResourceType type, int value)
        {
            switch (type)
            {
                case ItemResourceType.coin:
                    CoinCollected = value;
                    break;
                case ItemResourceType.diamond:
                    DiamondCollected = value;
                    break;
            }
        }

        /// <summary>
        /// Add points to the current score
        /// </summary>
        /// <param name="value">Points to add</param>
        public virtual void AddScore(int value)
        {
            Score += value;
        }

        /// <summary>
        /// Set the score to a specific value
        /// </summary>
        /// <param name="value">New score value</param>
        public virtual void SetScore(int value)
        {
            Score = value;
        }

        /// <summary>
        /// Add stars to the current total
        /// </summary>
        /// <param name="value">Number of stars to add</param>
        public virtual void AddStar(int value)
        {
            Star += value;
        }

        /// <summary>
        /// Set the star count to a specific value
        /// </summary>
        /// <param name="value">New star count</param>
        public virtual void SetStar(int value)
        {
            Star = value;
        }
        #endregion

        #region TRACKING

        /// <summary>
        /// Track analytics when gameplay starts
        /// </summary>
        protected virtual void TrackingPlaygame()
        {
            TrackingManagerCustom.LocationStart = LocationStartType.gameplay;

            TrackingManagerCustom.TrackingLevelStart(gameMode);

            levelPlayData.playCount++;

            GameLevelData.I.SetLevelPlayInfoData(currentLevel, levelPlayData);
        }

        /// <summary>
        /// Track analytics when level is completed successfully
        /// </summary>
        protected virtual void TrackingVictory()
        {
            TrackingManagerCustom.TrackingLevelEnd(gameMode, true, LevelFailedReason.none);

            levelPlayData.victoryCount++;

            GameLevelData.I.SetLevelPlayInfoData(currentLevel, levelPlayData);
        }

        /// <summary>
        /// Track analytics when level is failed
        /// </summary>
        protected virtual void TrackingLose()
        {
            TrackingManagerCustom.TrackingLevelEnd(gameMode, false, levelFailedReason);

            levelPlayData.loseCount++;

            GameLevelData.I.SetLevelPlayInfoData(currentLevel, levelPlayData);
        }

        /// <summary>
        /// Track analytics when level is skipped
        /// </summary>
        protected virtual void TrackingSkip()
        {
            TrackingManagerCustom.TrackingLevelEnd(gameMode, true, LevelFailedReason.none);

            levelPlayData.skipCount++;

            GameLevelData.I.SetLevelPlayInfoData(currentLevel, levelPlayData);
        }

        /// <summary>
        /// Track analytics when level is replayed
        /// </summary>
        protected virtual void TrackingReplay()
        {
            TrackingManagerCustom.TrackingLevelEnd(gameMode, false, levelFailedReason);

            levelPlayData.replayCount++;

            GameLevelData.I.SetLevelPlayInfoData(currentLevel, levelPlayData);
        }
        #endregion
    }
}