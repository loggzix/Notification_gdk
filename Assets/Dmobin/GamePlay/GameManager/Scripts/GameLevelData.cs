using UnityEngine;
using System;
using System.Collections.Generic;
using DSDK.Extensions;

namespace DSDK.Data
{
    [Serializable]
    public partial class GameLevelData : Data<GameLevelData>
    {
        [SerializeField] private int _currentLevel = 1;

        [SerializeField] private int _levelUnlocked = 1;

        [SerializeField] private Dictionary<int, LevelPlayInfoData> _dictLevelPlayInfoData = new Dictionary<int, LevelPlayInfoData>();

#if UNITY_EDITOR
        [SerializeField] private Dict<int, LevelPlayInfoData> _dictLevelPlayInfoDataEditor = new Dict<int, LevelPlayInfoData>();
#endif

        // Property to ensure dictionary is always initialized
        #region METHODS
        public int CurrentLevel
        {
            get => _currentLevel;
            set
            {
                _currentLevel = value;

                if (value > _levelUnlocked)
                {
                    _levelUnlocked = value;
                }

                Save();
            }
        }

        public int LevelUnlocked
        {
            get => _levelUnlocked;
            set
            {
                _levelUnlocked = value;

                Save();
            }
        }

        public Dictionary<int, LevelPlayInfoData> DictLevelPlayInfoData
        {
            get
            {
                return _dictLevelPlayInfoData;
            }
            set
            {
                _dictLevelPlayInfoData = value;

#if UNITY_EDITOR
                _dictLevelPlayInfoDataEditor.Clear();
                foreach (var item in value)
                {
                    _dictLevelPlayInfoDataEditor.Add(item.Key, item.Value);
                }
#endif

                Save();
            }
        }

        public bool IsHasLevelPlayInfoData(int levelId)
        {
            return DictLevelPlayInfoData.ContainsKey(levelId);
        }

        public LevelPlayInfoData GetLevelPlayInfoData(int levelId, bool isCreateIfNotExists = true)
        {
            if (DictLevelPlayInfoData.ContainsKey(levelId))
            {
                return DictLevelPlayInfoData[levelId];
            }

            if (isCreateIfNotExists)
            {
                LevelPlayInfoData levelPlayInfoData = new LevelPlayInfoData(levelId);
                DictLevelPlayInfoData.Add(levelId, levelPlayInfoData);

#if UNITY_EDITOR
                _dictLevelPlayInfoDataEditor.Add(levelId, levelPlayInfoData);
#endif

                Save();

                return levelPlayInfoData;
            }

            return null;
        }

        public void SetLevelPlayInfoData(int levelId, LevelPlayInfoData levelPlayInfoData)
        {
            if (DictLevelPlayInfoData.ContainsKey(levelId))
            {
                DictLevelPlayInfoData[levelId] = levelPlayInfoData;
            }
            else
            {
                DictLevelPlayInfoData.Add(levelId, levelPlayInfoData);
            }

#if UNITY_EDITOR
            if (_dictLevelPlayInfoDataEditor.ContainsKey(levelId))
            {
                _dictLevelPlayInfoDataEditor[levelId] = levelPlayInfoData;
            }
            else
            {
                _dictLevelPlayInfoDataEditor.Add(levelId, levelPlayInfoData);
            }
#endif

            Save();
        }

        public void RemoveLevelPlayInfoData(int levelId)
        {
#if UNITY_EDITOR
            if (_dictLevelPlayInfoDataEditor.ContainsKey(levelId))
            {
                _dictLevelPlayInfoDataEditor.Remove(levelId);
            }
#endif

            if (DictLevelPlayInfoData.ContainsKey(levelId))
            {
                DictLevelPlayInfoData.Remove(levelId);
                Save();
            }
        }

        public void ClearLevelPlayInfoData()
        {
            DictLevelPlayInfoData.Clear();

#if UNITY_EDITOR
            _dictLevelPlayInfoDataEditor.Clear();
#endif

            Save();
        }
        #endregion
    }

    [Serializable]
    public class LevelPlayInfoData
    {
        public int levelId = 0;
        public int playCount = 0;
        public int victoryCount = 0;
        public int loseCount = 0;
        public int deadCount = 0;
        public int reviveCount = 0;
        public int skipCount = 0;
        public int replayCount = 0;
        public int score = 0;
        public int bestScore = 0;
        public int star = 0;

        public LevelPlayInfoData(int levelId = 0, int playCount = 0, int victoryCount = 0, int loseCount = 0, int deadCount = 0, int reviveCount = 0, int skipCount = 0, int replayCount = 0, int score = 0, int bestScore = 0, int star = 0)
        {
            this.levelId = levelId;
            this.playCount = playCount;
            this.victoryCount = victoryCount;
            this.loseCount = loseCount;
            this.deadCount = deadCount;
            this.reviveCount = reviveCount;
            this.skipCount = skipCount;
            this.replayCount = replayCount;
            this.score = score;
            this.bestScore = bestScore;
            this.star = star;
        }
    }
}