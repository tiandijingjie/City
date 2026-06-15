using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class WarFieldElements
    {
        public enum FactionType
        {
            MIN = 0,
            FRIENDLY,
            ENEMY,
            NEUTRAL,
            MAX,
        }

        public enum RaceType //每个元素的名字对应了文件夹名字，所以不能用全大写的元素名
        {
            MIN = 0,
            Human,
            Oculars,
            Neutral,
            MAX,
        }

        public enum GenderType //性别
        {
            MIN = 0,
            MAN,
            WOMAN,
            MAX,
        }

        public enum WarEleType
        {
            MIN = 0,
            BUILDING,
            SOLDIER,
            MAXRIVAL, //标志可以被攻击的类型
            OBSTACLE, //地图中的障碍物
            WEAPON,
            OCULARSTONE,
            FARMER,
            MAX,
        }

        public enum Difficulty
        {
            MIN = 0,
            EASY,
            NORMAL,
            HARD,
            MAX,
        }

        //士兵/英雄/建筑/资源生产的能力/属性提升的来源
        public enum  ImproveSrc
        {
            MIN = 0,
            FROMCARD, //来自于抽卡
            FROMPROP, //来自道具,道具和其他不同之处是有时限
            FROMUPGRADE, //来自于外部的升级
            FROMOTHERSOURCE, //其他来源 (e.g. hero天赋)
            MAX,
        }

        public enum MapType
        {
            MIN = 0,
            ONGROUND, //地面上的地图
            UNDERGROUND, //地面下的地图
            MAX,
        }

        public enum TaskType
        {
            MIN = 0,
            NORMAL, //相当于update
            FIXED, //相当于FixUpdate
            MAX,
        }

        //根据camera的高度 显示不同分辨率的图片
        public enum LodLevel
        {
            MIN = 0,
            HD , // 高清 512
            MD, // 中清 256
            LD,  // 低清 128
            MAX,
        }

        //the max damage of one attack can achieve
        static public int MaxDamage = 0x7FFFFFFF; // The damage make sure to skill every kind of soldier
        static public byte OnGroundMapIndex = 0;
        static public byte InvalidMapIndex = 255;

        //寻路
        public const int FriendlyFlowFieldDefaultId = 0; //默认friendly流场的index
        public const int EnemyFlowFieldDefaultId = 1; //默认enemy流场的index
        public const int HomeFlowFieldId = 2; //默认用于farmer提交资源的多源流场
        public const int LocalFlowFieldStartId = 3; //局部流场开始的index

        //覆盖范围显示的颜色
        static public Dictionary<string, Color> CoverColorDict = new Dictionary<string, Color>
        {
            {"Green", new Color32(77, 212, 100, 255) },
        };

        /* can not change outside */
        static private uint EnemySdIndex = 0;
        static private uint FriendlySdIndex = 0;
        static private uint BuildingIndex = 0;

        static public uint GetSdIndex(FactionType faction)
        {
            if(faction == FactionType.FRIENDLY)
            {
                FriendlySdIndex++;
                return FriendlySdIndex;
            }
            else if(faction == FactionType.ENEMY)
            {
                EnemySdIndex++;
                return EnemySdIndex;
            }
            return 0;
        }

        static public uint GetBdIndex()
        {
            BuildingIndex++;
            return BuildingIndex;
        }

        //生成entity中的subtype
        //soldier:  type1: troop  type2: subtype
        //building: type1: bdMode type2: subtype
        //other: 对于soldier是status
        static public uint EncodeEntitySubType(byte raceType, byte type1, byte type2, byte other)
        {
            return ((uint)other << 24) | ((uint)type2 << 16) | ((uint)type1 << 8) | (uint)raceType;
        }

        //解码entity中的subtype
        static public void DecodeEntitySubType(uint data, out byte raceType, out byte type1, out byte type2, out byte other)
        {
            raceType = (byte)(data & 0xFF);
            type1 = (byte)((data >> 8) & 0xFF);
            type2 = (byte)((data >> 16) & 0xFF);
            other = (byte)((data >> 24) & 0xFF);
        }
    }

    //独特的一些数据，天赋，技能等,每个士兵/建筑/英雄都不一样
    public class IndividualData
    {
        public WarFieldElements.WarEleType p_eleType;
        protected IndividualItem[] _individualItems;

        public IndividualItem[] gs_individualItems
        {
            get { return _individualItems; }
        }

        public IndividualData(WarFieldElements.WarEleType type)
        {
            p_eleType = type;
        }

        public IndividualData(IndividualData data)
        {
            p_eleType = data.p_eleType;
        }

        public virtual void ReInitIndividualData(IndividualData data)
        {
            p_eleType = data.p_eleType;
        }

        //value的第一个item是improve的目标
        public virtual bool Improve(object value)
        {
            return false;
        }
    }

    public abstract class IndividualItem
    {
        protected IndividualItemDescription _description;

        public abstract bool Improve(object value);
        public abstract IndividualItemDescription GetDescription();
        public abstract void ReInit(IndividualItem item);
        public abstract bool IsEnabled();
    }

    public interface IndividualItemModifyNotifyInf
    {
        public void IndividualItemModifyNotify(int changeId);
    }

    //添加通知机制,当调用Improve时会通知注册的observer
    //目前hero的技能都是继承这个class
    public abstract class IndividualItemWithNotify : IndividualItem
    {
        protected List<IndividualItemModifyNotifyInf> _notifyList = new List<IndividualItemModifyNotifyInf>();

        public bool RegisterNotify(IndividualItemModifyNotifyInf notify)
        {
            if(_notifyList.Contains(notify))
                return false;
            _notifyList.Add(notify);
            return true;
        }

        protected void InfoObserver(int changeId)
        {
            int cnt = _notifyList.Count;
            for (int i = 0; i < cnt; i++)
            {
                _notifyList[i].IndividualItemModifyNotify(changeId);
            }
        }
    }

    public class IndividualItemDescription
    {
        public string p_name;
        public int p_level = 0;
        public string p_levelDescription;
    }

    //需要守护的建筑、区域
    public interface INeedBeProtect
    {
        //获取入侵者
        public WarEleParent GetInvader(out WarFieldElements.WarEleType type);
        //守护者死亡
        public void ProtectorDie(IProtector protector);
    }

    //守护者(目前只有soldier支持)
    public interface IProtector
    {
        //成为守护者
        //position：分配的站岗的位置
        public bool BecomeProtector(INeedBeProtect needProtect, WarFieldElements.WarEleType type, Vector2 position);

        //恢复成普通模式
        public void ReleaseProtector();

        //通知Protector已经到了指定位置，
        //Protector到达指定位置有两种情况，
        //1）到达BecomeProtector传递的position
        //2) 调用ArriveProtectPos通知
        public void ReachProtectTarget();
    }

    public interface ITask
    {
        // 纯粹的执行逻辑，不需要任何属性声明
        public void RunNormalTask(float deltaTime);
        public void RunFixTask(float deltaTime);
    }
}

