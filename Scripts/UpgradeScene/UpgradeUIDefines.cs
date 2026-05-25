using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UpgradeScene
{
    public class UpgradeUIDefines
    {
        public enum UpgradeTypes
        {
            MIN = 0,
            SOLDIERUPGRAD,
            HEROUPGRADE,
            BUILDINGUPGRADE,
            MINEUPGRADE,
            CARDUPGRADE,
            MAX,
        }

        public enum UpgradeSeq
        {
            PARENTUG = 0, //parent upgrade item
            CHILDUG, //child upgrade item
        }
    }

    public class UpgradeConf
    {
        public string p_name;
        public string p_icon;
        public string p_description;
    }

    public class SoldierUpgradeConf : UpgradeConf
    {
        public int p_troop;   //SoldierDefines.TroopType
        public int p_soldier;
        public string p_attribute;
        public string p_value;
    }
}

