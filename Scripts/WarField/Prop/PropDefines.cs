using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using PD = PropDefines;
    public class PropDefines
    {
        public enum PropType
        {
            MIN = 0,
            //basic prop
            MINORFIRSTAIDKIT, //初级急救包
            SWIFTNESSPOTION, //迅捷药水
            REPAIRHAMMER, //修理锤
            BLINDINGBOMB, //闪光弹
            LANDMINE, //地雷
            COINPOUCH, //钱袋
            FIELDRATIONS, //行军干粮
            TARGETDUMMY, //稻草人
            BASICOCULARSTONECOLLECTOR, //初级曈石收集器
            BASICBLOODBURNPOTION, //初级燃血药剂
            STASISTRAP, //晕眩陷阱

            //normal prop
            AMPLIFIER, //扩音器
            MAX,
        }

        //与CardDefines.CardLevel对应
        public enum PropLevel
        {
            MIN = 0,
            BASE,
            NORMAL,
            RARE,
            MAX,
        }
    }

    public class Prop
    {
        protected PropConf _conf;

        public PropConf gs_conf => _conf;
        public virtual void ActiveProp() {} //从bag中get出来道具就激活了，因为有些道具需要手动选择释放目标
        //使用道具
        public virtual bool UseProp()
        {
            WarFieldBagSystem.Instance.InfoConsumeCurrentProp();
            return true;
        }

        //放弃使用道具
        public virtual void GiveupProp()
        {
            WarFieldBagSystem.Instance.InfoRetriveCurrentProp();
        }

        //道具效果结束
        public virtual void PropFinish(){ }
        public virtual void PropUpdate() { }
    }

    //物品基类
    public abstract class PropConf
    {
        public PD.PropType gs_type => _type;
        public PD.PropLevel  gs_level => _level;

        protected PD.PropType _type;
        protected PD.PropLevel _level;

        public IndividualItemDescription p_description; //物品的描述
    }

    //初级急救包
    public class MinorFirstAidKitConf : PropConf
    {
        public float p_duration;
        public float p_cure;
        public float p_curePerSec; //每秒恢复的生命

        public MinorFirstAidKitConf()
        {
            _type =  PD.PropType.MINORFIRSTAIDKIT;
            _level = PD.PropLevel.BASE;

            p_duration = 5.0f;
            p_cure = 150;
            p_curePerSec = p_cure / p_duration;

            p_description = new IndividualItemDescription();
            p_description.p_name = "Minor First Aid Kit";
            p_description.p_levelDescription = "在没有被受到攻击的时候,英雄在5s内恢复150点生命,受到伤害会立刻打断恢复";
        }
    }

    //迅捷药水
    public class SwiftnessPotionConf : PropConf
    {
        public float p_duration;
        public float p_moveUp; //移速增加

        public SwiftnessPotionConf()
        {
            _type =  PD.PropType.SWIFTNESSPOTION;
            _level = PD.PropLevel.BASE;

            p_duration = 10.0f;
            p_moveUp = 1 + 0.3f; //30%
        }
    }

    //修理锤
    public class RepairHammerConf : PropConf
    {
        public float p_repairPercent;

        public RepairHammerConf()
        {
            _type =  PD.PropType.REPAIRHAMMER;
            _level = PD.PropLevel.BASE;

            p_repairPercent = 0.25f; //25%
        }
    }

    //闪光弹
    public class BlendingBombConf : PropConf
    {
        public float p_range;
        public float p_duration; //in second
        public int p_dmgMissChance; //普通士兵攻击时的攻击落空
        public int p_bossDmgMissChance; //boss攻击时的攻击落空

        public BlendingBombConf()
        {
            _type =  PD.PropType.BLINDINGBOMB;
            _level = PD.PropLevel.BASE;

            p_range = 4;
            p_duration = 5.0f;
            p_dmgMissChance = (int)(0.9f * 100); //90%
            p_bossDmgMissChance = (int)(0.3f * 100); //30%
        }
    }

    //地雷
    public class LandmineConf : PropConf
    {
        public float p_range;
        public float p_prepareTime; //准备时间
        public float p_damage;
        public float p_delayTime; //延迟爆炸时间

        public LandmineConf()
        {
            _type =  PD.PropType.LANDMINE;
            _level = PD.PropLevel.BASE;

            //这些值是配置在建筑的配置文件里面的
            p_range = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .LANDMINE)).gs_range;
            p_prepareTime = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .LANDMINE)).gs_specConfs["prepareTime"];
            p_damage = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .LANDMINE)).gs_specConfs["damage"];
            p_delayTime = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .LANDMINE)).gs_specConfs["delayTime"];
        }
    }

    //钱袋
    public class CoinPouchConf : PropConf
    {
        public int p_coin;

        public CoinPouchConf()
        {
            _type =  PD.PropType.COINPOUCH;
            _level = PD.PropLevel.BASE;

            p_coin = 100;
        }
    }

    //行军干粮
    public class FieldRationsConf : PropConf
    {
        public float p_range;
        public float p_duration;
        public float p_hpIncUp;
        public float p_moveDown;

        public FieldRationsConf()
        {
            _type =  PD.PropType.FIELDRATIONS;
            _level = PD.PropLevel.BASE;

            p_range = 4;
            p_duration = 10;
            p_hpIncUp = 20;
            p_moveDown = 1 - 0.8f; //80%
        }
    }

    //稻草人
    public class TargetDummyConf : PropConf
    {
        public float p_range;
        public float p_duration;

        //生命和护甲在稻草人的building配置文件中设置
        public TargetDummyConf()
        {
            _type = PD.PropType.TARGETDUMMY;
            _level = PD.PropLevel.BASE;

            p_range = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .TARGETDUMMY)).gs_range;
            p_duration = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .TARGETDUMMY)).gs_duration;
        }
    }

    //初级曈石收集器
    public class BasicOcularStoneCollectorConf : PropConf
    {
        public float p_range;
        public float p_duration;

        public BasicOcularStoneCollectorConf()
        {
            _type = PD.PropType.BASICOCULARSTONECOLLECTOR;
            _level = PD.PropLevel.BASE;

            p_range = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .BASICOCULARSTONECOLLECTOR)).gs_range;
            p_duration = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .BASICOCULARSTONECOLLECTOR)).gs_duration;
        }
    }

    //初级燃血药剂
    public class BasicBloodBurnPotionConf : PropConf
    {
        public float p_range;
        public float p_duration;
        public float p_atkSpeedUp; //攻击速度提升
        public float p_armorDown; //护甲降低

        public BasicBloodBurnPotionConf()
        {
            _type = PD.PropType.BASICBLOODBURNPOTION;
            _level = PD.PropLevel.BASE;
            p_range = 4;
            p_duration = 10;
            p_atkSpeedUp = 1 + 0.15f; //攻速提升15%
            p_armorDown = -2f; //护甲降低2
        }
    }

    //晕眩陷阱
    public class StasisTrapConf : PropConf
    {
        public float p_range;
        public float p_prepareTime; //准备时间
        public float p_damage;
        public float p_stunTime; //晕眩时间
        public float p_delayTime; //延迟爆炸时间

        public StasisTrapConf()
        {
            _type = PD.PropType.STASISTRAP;
            _level = PD.PropLevel.BASE;

            //这些值是配置在建筑的配置文件里面的
            p_range = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .STASISTRAP)).gs_range;
            p_prepareTime = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .STASISTRAP)).gs_specConfs["prepareTime"];
            p_damage = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .STASISTRAP)).gs_specConfs["damage"];
            p_delayTime = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .STASISTRAP)).gs_specConfs["delayTime"];
            p_delayTime = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .STASISTRAP)).gs_specConfs["stunTime"];
        }
    }

    //扩音器
    public class AmplifierConf : PropConf
    {
        public float p_range;
        public float p_atkUp;

        public AmplifierConf()
        {
            _type = PD.PropType.STASISTRAP;
            _level = PD.PropLevel.BASE;

            //这些值是配置在建筑的配置文件里面的
            p_range = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .AMPLIFIER)).gs_range;
            p_atkUp = ((PropBdConf)WarBuildingCtrl.Instance.GetBdConf(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.PROPBD, (int)HumanDefines.PropBdType
                .AMPLIFIER)).gs_specConfs["atkUp"];
        }
    }
}
