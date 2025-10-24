using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSDK.Data
{
    //Dữ liệu dùng chung cho tất cả các game
    [Serializable]
    public partial class GameData : Data<GameData>
    {
        [SerializeField] private bool firstOpen = false;
        [SerializeField] private bool firstPlay = false;

        public bool FirstOpen
        {
            get => firstOpen;
            set
            {
                firstOpen = value;
                Save();
            }
        }

        public bool FirstPlay
        {
            get => firstPlay;
            set
            {
                firstPlay = value;
                Save();
            }
        }
    }
}