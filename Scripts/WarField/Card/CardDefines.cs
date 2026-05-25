using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class CardDefines
    {
        public enum CardCategory
        {
            MIN = 0,
            MELEE,
            RANGED,
            MAGIC,
            BUILDING,
            RESOURCE,
            HERO,
            PROP, //道具
            MAX,
        }

        public enum CardLevel //card的等级,决定抽取的概率
        {
            MIN = 0,
            BASE,
            NORMAL,
            RARE,
            MAX,
        }
    }
}

