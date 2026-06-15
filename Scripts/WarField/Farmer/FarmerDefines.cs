using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class FarmerDefines
    {
        public enum FarmerLevel //农民的等级
        {
            MIN = 0,
            LOW,
            MID,
            HIGH,
            MAX,
        }

        public enum FarmerStatus
        {
            MIN = 0,
            INIT,
            INHOME, //在驻地内
            TORES, //搜集资源的路上
            COLLECTRES, //采集资源
            WAITRES, //在野外等待查询资源结果
            GOBACK, //返回最近的驻地
            DIGHIDE, //挖洞躲藏
            HIDE, //躲藏地下,直到敌人离开
            HIDEDETECT,  //敌人已经离开,侦查周围环境
            MAX,
        }

        public enum FarmAnimType
        {
            MIN = 0,
            IDLE,
            ENMTYMOVE, //还没有采集到任何东西
            PACKEDMOVE,  //采集到东西了
            COLLECT,
            DIGHIDE, //挖洞躲藏
            HIDE,
            HIDEDETECT,
            MAX,
        }
    }

    public class FarmerConf
    {
        public FarmerDefines.FarmerLevel p_level;
        public WarFieldElements.GenderType p_gender;
        public float p_moveSpeed;
        public int p_carryCapacity; //背负资源的能力
        public float p_enemyDetectRange;
        public float p_hideMinTime; //最短躲藏时间
        public float p_hideDetectTime; //敌人离开之后还需躲藏的时间
        public float p_mass = 2;
    }
}
