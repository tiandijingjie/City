using System.Collections.Generic;

namespace WarField
{
    using UnityEngine;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    public class SoldierDefines
    {
        public enum TroopType//每个元素对应了文件夹名字，所以不能用全大写的元素名
        {
            MIN = 0,
            Melee,
            Ranged,
            Magic,
            MAX,
        }

        public enum StateSoldierEffectType //skill effect soldier state
        {
            MIN = 0,
            HEALTH,  //最大生命值
            DAMAGE,
            ATTACKRANGE,
            SEARCHRANGE,
            ATTACKSPEED,
            MOVESPEED,
            PHYARMOR,
            SPAWNSPEED,
            BODYHIDE,  //隐身  >0:解除隐身   0<:隐身
            SKILLCOLLIDER, //set skill collider size, ==0 mean disable collider
            HPINC,//回血
            SKILLTIMESTEP, //技能冷却记时步进
            DIEAWARD, //被击杀奖励
            DIEAWARDCHANCE, //被击杀时产生奖励的概率
            MAX,
        }

        //远程攻击出手的位置 （远程，魔法）
        public enum RemoteAttackStartPosition
        {
            MIN = 0,
            OVERHEAD,  //从头顶向斜上方射击
            OVERSHOULDER, //从肩膀平射
            MAX,
        }

        //如果要增加status，一定要检查soldier中调用的IsEnumInRange的代码，是否仍然满足判断条件 ！！！
        public enum SoldierStatus
        {
            MIN = 0,
            INIT,
            BORN,
            IDLE,
            MOVE,
            ATTACKTATGET,
            RELEASESKILL, //在播放释放技能的动画过程中
            INTERRUPT,  //interrupt the actions is doing now. it introduced by buff (stun)
            DIE,
            REBORN, //复活,只有hero有这个状态
            ERROR,
            MAX,
        }

        //soldier 在move状态下说执行的命令
        //enemy和neutral的士兵,只会有NORMAL的cmd
        public enum MoveCmd
        {
            MIN=0,
            NORMAL, //soldier 默认移动命令, hero不会收到这个命令
            MOVETORIVAL, //在normal移动过程中发现敌人
            MANMOVETOPOS, //向着特定的位置移动,途中不会攻击任何目标  玩家控制行为
            MANMOVEATTACKTOPOS, //向着特定的位置移动,途中会攻击发现的目标   玩家控制行为
            MAX
        }

        //animation type
        public enum SoldierAnimType
        {
            MIN = 0,
            IDLE,
            MOVE,
            ATTACK,
            SKILL,
            STUN,
            DIE,
            BORN,
            MAX,
        }

        //高级，低级士兵
        public enum SoldierLevel
        {
            MIN = 0,
            BASICLEVEL,
            HIGHLEVEL,
            RARELEVEL, //稀有级
            BOSSLEVEL, //boss or hero
            MAX,
        }

        //how soldier choose the current target, determine whether the target can be override
        public enum TargetDetectType
        {
            MIN = 0,
            INPROTECTRANGE, //when soldier in PROTECT behavior mode, and rival in the protect target range
            INDETECTRANGE,  //in detect range
            BEATTACK,      //be attacked
            INATTACKRANGE,  //in attack range
            MAX,
        }

        public enum SoldierImproveTarget
        {
            MIN = 0,
            STATE,
            TALENT,
            SKILL,
            GENERALTALENT, //general talent 仅英雄有
            GENERALSKILL, //general skill 仅英雄有
            MAX,
        }

        //士兵的行为模式
        public enum BehaviorMode
        {
            MIN = 0,
            NORMAL,
            PROTECT, //守护模式,守护一个建筑
            MAX,
        }

        //士兵开始传送的阶段
        public enum TransportStage
        {
            MIN = 0, //没有进入传送状态
            IN,  //播放进入传送的动画
            OUT, //播放传送完成的动画
            MAX,
        }

        static public float GetPhysicsResistance(float phyArmor)
        {
            float ret = (0.06f * phyArmor) / (1.0f + 0.06f * Mathf.Abs(phyArmor));
            if(ret > 0.75f)
                return 0.75f;
            else if(ret < -0.75f)
                return -0.75f;
            return ret;
        }

        //计算魔抗
        static public float GetMagicResistance(float magicRs)
        {
            float ret = magicRs;// 1 - (1 - magicRs);
            if(ret > 0.85f)
                return 0.85f;
            else if (ret < -0.85f)
                return -0.85f;
            return ret;
        }

        //the attack time inverval compare to the fix update time
        static public float GetAttackInterval(float attackSpeed)
        {
            return Utils.CountOfFixUpdate(1 / attackSpeed) ;
        }
    }

    //data from configuration file
    public class SoldierConf
    {
        public string p_name;
        public WE.FactionType p_faction;
        public SoldierDefines.TroopType p_troop;
        public SoldierDefines.SoldierLevel p_level; //士兵的等级,敌方士兵的等级主要是影响爆出的宝石, 和是不是boss， 己方士兵BOSSLEVEL就是hero
        public int p_soldierType; //for hero p_soldierType==HumanDefines.HeroType
        public float p_health;
        public float p_damage;
        public float p_attackRange;
        public float p_searchRange;
        public float p_attackSpeed;  //how many times attack in a second
        public float p_moveSpeed;
        public WE.RaceType p_race;  //race : human orculars   not used for now
        public float p_phyArmor;
        public float p_spawnTime; //spawn time in second
        public float p_spawnTimeInCycle; //count of fix update, must be float because x.9 compare to x.1 is big differente,
        public int p_price; //生产价格
        public float p_hpInc; //回血 per second
        public string p_description;
        public float p_skillTimeStep; //技能的冷却记时的步进，默认1，如果技能冷却缩短p_skillTimeStep增大
        public Dictionary<string, float> p_specConfs; //一些特有的属性 e.g. 英雄的一些特性
        public float p_mass; //重量用于计算移动过程中的相互推挤

        //被击杀奖励,SoldierState不需要这两个值
        public float p_dieReward; //定义的float,为了计算精确
        public int p_dieRewardChance;

        public SoldierConf()
        {
            p_race = WE.RaceType.MIN;
            p_level = SoldierDefines.SoldierLevel.BASICLEVEL;
            p_description = "";
            p_skillTimeStep = 1f;
            p_specConfs = null;
            p_dieReward = 0;
            p_dieRewardChance = 0;
            p_mass = 10;
        }

        public SoldierConf(SoldierConf src)
        {
            p_name = src.p_name;
            p_faction = src.p_faction;
            p_troop = src.p_troop;
            p_level = src.p_level;
            p_soldierType = src.p_soldierType;
            p_health = src.p_health;
            p_damage = src.p_damage;
            p_attackRange = src.p_attackRange;
            p_searchRange = src.p_searchRange;
            p_attackSpeed = src.p_attackSpeed;
            p_moveSpeed = src.p_moveSpeed;
            p_race = src.p_race;
            p_phyArmor = src.p_phyArmor;
            p_spawnTime = src.p_spawnTime;
            p_spawnTimeInCycle = Utils.CountOfFixUpdate(p_spawnTime);
            p_price = src.p_price;
            p_hpInc = src.p_hpInc;
            p_description = src.p_description;
            p_skillTimeStep = src.p_skillTimeStep;
            if (src.p_specConfs != null)
                p_specConfs = new Dictionary<string, float>(src.p_specConfs);
            else
                p_specConfs = null;
            p_dieReward = src.p_dieReward;
            p_dieRewardChance = src.p_dieRewardChance;
            p_mass = src.p_mass;
        }

        public void ReInitSoldierConf(SoldierConf src)
        {
            p_name = src.p_name;
            p_faction = src.p_faction;
            p_troop = src.p_troop;
            p_level = src.p_level;
            p_soldierType = src.p_soldierType;
            p_health = src.p_health;
            p_damage = src.p_damage;
            p_attackRange = src.p_attackRange;
            p_searchRange = src.p_searchRange;
            p_attackSpeed = src.p_attackSpeed;
            p_moveSpeed = src.p_moveSpeed;
            p_race = src.p_race;
            p_phyArmor = src.p_phyArmor;
            p_spawnTime = src.p_spawnTime;
            p_spawnTimeInCycle = Utils.CountOfFixUpdate(p_spawnTime);
            p_price = src.p_price;
            p_hpInc = src.p_hpInc;
            p_description = src.p_description;
            p_skillTimeStep = src.p_skillTimeStep;
            if (src.p_specConfs != null)
                p_specConfs = new Dictionary<string, float>(src.p_specConfs);
            else
                p_specConfs = null;
            p_dieReward = src.p_dieReward;
            p_dieRewardChance = src.p_dieRewardChance;
            p_mass = src.p_mass;
        }
    }

    [System.Serializable]
    public class SoldierState //current soldier state value base on the SoldierConf
    {
        public float p_health;
        public float p_maxHealth;
        public float p_damage;
        public float p_attackRange;
        public float p_searchRange;
        public float p_attackSpeed; //how many times attack in a second
        public float p_moveSpeed;
        public float p_phyArmor;
        public float p_spawnTime; //just to keep align with the SoldierConf, not actually use this value
        public float p_spawnTimeInCycle;
        public float p_hpInc; //回血  by fixupdate,conf is by seond
        public bool p_bodyHide;
        public float p_skillTimeStep; //技能的冷却记时的步进，默认1，如果技能冷却缩短p_skillTimeStep增大
        public Dictionary<string, float> p_specConfs; //一些特有的属性 e.g. 英雄的一些特性
        public float p_mass; //重量用于计算移动过程中的相互推挤

        public SoldierState(SoldierConf conf)
        {
            p_health = conf.p_health;
            p_maxHealth = conf.p_health;
            p_damage = conf.p_damage;
            p_attackRange = conf.p_attackRange;
            p_searchRange = conf.p_searchRange;
            p_attackSpeed = conf.p_attackSpeed;
            p_moveSpeed = conf.p_moveSpeed;
            p_phyArmor = conf.p_phyArmor;
            p_spawnTime = conf.p_spawnTime;
            p_spawnTimeInCycle = Utils.CountOfFixUpdate(p_spawnTime);
            p_hpInc = conf.p_hpInc * Time.fixedDeltaTime;
            p_bodyHide = false;
            p_skillTimeStep = conf.p_skillTimeStep;
            if(conf.p_specConfs != null)
                p_specConfs = new Dictionary<string, float>(conf.p_specConfs);
            else
                p_specConfs = null;
            p_mass = conf.p_mass;
        }

        public void ReInitSoldierState(SoldierConf conf)
        {
            p_health = conf.p_health;
            p_maxHealth = conf.p_health;
            p_damage = conf.p_damage;
            p_attackRange = conf.p_attackRange;
            p_searchRange = conf.p_searchRange;
            p_attackSpeed = conf.p_attackSpeed;
            p_moveSpeed = conf.p_moveSpeed;
            p_phyArmor = conf.p_phyArmor;
            p_spawnTime = conf.p_spawnTime;
            p_spawnTimeInCycle = Utils.CountOfFixUpdate(conf.p_spawnTime);
            p_hpInc = conf.p_hpInc * Time.fixedDeltaTime;
            p_bodyHide = false;
            p_skillTimeStep = conf.p_skillTimeStep;
            if(conf.p_specConfs != null)
                p_specConfs = new Dictionary<string, float>(conf.p_specConfs);
            else
                p_specConfs = null;
            p_mass = conf.p_mass;
        }
    }

    //士兵生产的通知, 不包括hero
    public interface ISoldierProductionNotify
    {
        public void SoldierProduceIntf(WE.FactionType faction, SoldierDefines.TroopType troop, Soldier soldier, Vector2 pos);
    }

    //士兵死亡通知, 不包括hero
    public interface ISoldierDieNotify
    {
        public void SoldierDieIntf(WE.FactionType faction, SoldierDefines.TroopType troop, Soldier soldier);
    }
}

