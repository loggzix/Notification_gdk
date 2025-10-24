using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace DSDK.UISystem
{
    [CreateAssetMenu(fileName = "AddItemResourceData", menuName = "ScriptableObject/AddItemResourceData")]
    public class AddItemResourceData : ScriptableObject
    {
        public AddItemResourceInfo[] listItemResource;

        public AddItemResourceInfo GetItemResourceInfo(AddItemResourceType itemResourceType)
        {
            foreach (AddItemResourceInfo info in listItemResource)
            {
                if (info.type == itemResourceType)
                {
                    return info;
                }
            }
            return null;
        }

        public int GetIndexItemByType(AddItemResourceType itemResourceType)
        {
            for (int i = 0; i < listItemResource.Length; i++)
            {
                if (listItemResource[i].type == itemResourceType)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    [Serializable]
    public class AddItemResourceInfo
    {
        public string name = "";
        public AddItemResourceType type = AddItemResourceType.None;
        public Sprite[] listIcon = null;
        public Sprite[] listIconBig = null;

        public Sprite[] listIconUI = null;
        public Sprite[] listIconUIBig = null;
        public string detail = "";
    }

    [Serializable]
    public enum AddItemResourceType
    {
        None,
        Coin,
        Diamond
    }
}