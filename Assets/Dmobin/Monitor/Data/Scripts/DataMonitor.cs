using UnityEngine;

namespace DSDK.Data
{
    public class DataMonitor : MonoBehaviour
    {
        #region DONT TOUCH

        private static DataMonitor _instance;

        public static DataMonitor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DataMonitor>();
                }

                return _instance;
            }
        }

        public static bool IsInstanceValid()
        {
            return _instance;
        }

        #region Fields

        /// <summary>
        /// Lấy dữ liệu default từ Monitor thay vì lấy từ constructor
        /// </summary>
        [SerializeField] private bool _getDefaultFromMonitor = false;

        [SerializeField] private string _profileId = "main";
        [SerializeField] private bool _debug = false;
        [SerializeField] private DataSaveType _saveType = DataSaveType.PlayerPrefs;
        [HideInInspector] public DataMonitorInfo MonitorInfo;

        public static bool AllDataLoaded = false;

        #endregion

        #region Main Method

        protected void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            FileDataHandler.Instance.Setup(_profileId, Application.persistentDataPath, _debug, _saveType);
            MonitorInfo = DataMonitorInfo.Instance;
            GetInstance();
        }

        #endregion

        #endregion

        // for monitor only ,not use in runtime

        #region Monitor

        #region Monitor Fields
        [SerializeField] private GameData _gameData;
        #endregion

        public void GetInstance()
        {
            if (_getDefaultFromMonitor)
            {
                #region Get Default

                if (!FileDataHandler.Instance.IsExist(_gameData.Key))
                {
                    GameData.SetInstance(_gameData);

                    // Save the default data immediately to ensure it persists
                    _gameData.Save();
                }

                #endregion
            }

            RefreshAllInstances();

            AllDataLoaded = true;
        }

        public void LoadAllData()
        {
            #region Load All Data
            GameData.Instance.Load();
            #endregion

            RefreshAllInstances();
        }

        /// <summary>
        /// Refresh all data instances to update inspector display
        /// </summary>
        public void RefreshAllInstances()
        {
            #region Refresh Instances
            _gameData = GameData.Instance;
            #endregion
        }

        public void SaveAllData()
        {
            #region Save All Data
            _gameData.Save();
            #endregion
        }

        public void DeleteAllData()
        {
            FileDataHandler.Instance.DeleteProfile(_profileId);
        }

        #endregion
    }
}