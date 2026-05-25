using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class NeutralDefines : MonoBehaviour
    {
        public enum MeleeType
        {
            MIN = 0,
            GOLEM, //石头人
            LANDMINE, //地雷
            TARGETDUMMY, //稻草人
            MAX,
        }

        public enum RangedType
        {
            MIN = 0,
            MAX,
        }

        public enum MagicType
        {
            MIN = 0,
            MAX,
        }

        public enum PortalType
        {
            MIN = 0,
            PORTAL,
            MAX,
        }

        public enum GoldMineType //资源矿产的丰度分类
        {
            MIN = 0,
            LOWLEVEL,
            MIDLEVEL,
            HIGHLEVL,
            MAX,
        }

        public enum GemMineType //资源矿产的丰度分类
        {
            MIN = 0,
            LOWLEVEL,
            MIDLEVEL,
            HIGHLEVL,
            MAX,
        }

        public enum CaveType
        {
            MIN = 0,
            CAVE,
            MAX,
        }
    }
}

