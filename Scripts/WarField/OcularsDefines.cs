using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class OcularsDefines
    {
        public enum MeleeType
        {
            MIN = 0,
            ZOMBIE,
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

        //building types
        public enum FortressType
        {
            MIN = 0,
            MAX,
        }

        //只有一个成员BARRACK，不管什么兵营都是这个类型，建筑的具体出兵类型是在level的配置文件中定义的
        public enum BarrackType
        {
            MIN = 0,
            BARRACK, //all kinds of enemy barrack are all belong to this type
            MAX,
        }

        public enum DefenceType
        {
            MIN = 0,
            MAX,
        }
    }
}

