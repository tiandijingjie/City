using System.Collections;
using System.Collections.Generic;

namespace WarField
{
    public class HumanDefines
    {
        public enum MeleeType
        {
            MIN = 0,
            INFANTRY, //步兵
            //技能系
            ASSASSIN,               //刺客
            //打击系
            SWORDSMAN,              //剑士
            //护盾系
            SHIELDSOLDIER,          //盾兵
            MAX,
        }

        public enum RangedType
        {
            MIN = 0,
            ARCHER,                 //弓箭手
            SNIPER,                 //狙击手
            CANNONEER,              //巨炮手
            SHARPSHOOTER,           //神射手
            MAX,
        }

        public enum MagicType
        {
            MIN = 0,
            APPRENTICE,         //学徒
            PRIEST,             //牧师
            SHAMAN,             //萨满
            MAX,
        }

        //building types
        public enum FortressType
        {
            MIN = 0,
            MAINFORTRESS,
            MAX,
        }

        //just the same as SD.TroopType
        public enum BarrackType
        {
            MIN = 0,
            MELEE,
            RANGED,
            MAGIC,
            MAX,
        }

        public enum DefenceType
        {
            MIN = 0,
            BASICTOWER,   //基础塔
            VOLLEYTOWER,  //多重箭塔
            FROSTTOWER,   //冰霜塔
            FLAMETOWER,   //燃烧塔
            LASERTOWER,   //激光塔
            ARCTOWER,     //闪电塔
            SIEGETOWER,   //攻城塔
            MAX,
        }

        public enum HeroType
        {
            MIN = 0,
            MELEEHERO,
            RANGEDHERO,
            MAGICHERO,
            MAX,
        }

        //道具生成的建筑
        public enum PropBdType
        {
            MIN = 0,
            LANDMINE, //地雷
            TARGETDUMMY, //稻草人
            BASICOCULARSTONECOLLECTOR, //初级曈石收集器
            STASISTRAP, //晕眩陷阱
            AMPLIFIER, //扩音器
            MAX,
        }
    }
}

