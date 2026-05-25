using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace RangedHero
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class RangedHeroIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            FROSTARROW, //寒冰箭
            ARROWRAIN, //箭雨
            SUDDENDEMISE, //暴毙
            MAX,
        }

#region Skill
        //大量降低前方敌人的移动和攻击速度,不能叠加,并造成伤害
        //覆盖范围: 宽2.5长9的区域
        // 属性降低: 降低50%移动速度,35%攻击速度
        // 技能持续时间: 20s
        // 伤害:30
        // 冷却: 40s
        public class SkillFrostArrow : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                LENGTH,
                DAMAGE,
                MOVEDOWN,
                ATTACKSPEEDDOWN,
                DURATION,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_length;
            public float p_width; //不能升级
            public float p_damage;
            public float p_moveDown;
            public float p_attackSpeedDown;
            public float p_duration;
            public float p_interval;
            public int p_intervalCycle;
            public int p_level;

            public SkillFrostArrow()
            {
                p_length = 9f;
                p_width = 3f;
                p_damage = 30;
                p_moveDown = 1 - 0.5f; //50%
                p_attackSpeedDown = 1 - 0.37f;//35%
                p_duration = 20f;
                p_interval = 40f;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillFrostArrow(IndividualItem item)
            {
                SkillFrostArrow skill = item as SkillFrostArrow;
                p_length = skill.p_length;
                p_width = skill.p_width;
                p_damage = skill.p_damage;
                p_moveDown = skill.p_moveDown;
                p_duration = skill.p_duration;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillFrostArrow skill = item as SkillFrostArrow;
                p_length = skill.p_length;
                p_width = skill.p_width;
                p_damage = skill.p_damage;
                p_moveDown = skill.p_moveDown;
                p_duration = skill.p_duration;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }

            public override bool Improve(object value)
            {
                ValueTuple<ParameterType, float, GD.CalDeltaType> tupple = (ValueTuple<ParameterType, float, GD.CalDeltaType>)value;
                ParameterType target = tupple.Item1;
                float deltaValue = tupple.Item2;
                GD.CalDeltaType calType = tupple.Item3;
                switch (target)
                {
                    case ParameterType.LENGTH:
                        p_length = Utils.CalDeltaValue(p_length, deltaValue, calType);
                        break;
                    case ParameterType.MOVEDOWN:
                        p_moveDown = Utils.CalDeltaValue(p_moveDown, deltaValue, calType);
                        break;
                    case ParameterType.ATTACKSPEEDDOWN:
                        p_attackSpeedDown = Utils.CalDeltaValue(p_attackSpeedDown, deltaValue, calType);
                        break;
                    case ParameterType.DAMAGE:
                        p_damage = Utils.CalDeltaValue(p_damage, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                        break;
                    case ParameterType.DURATION:
                        p_duration = Utils.CalDeltaValue(p_duration, deltaValue, calType);
                        break;
                    case ParameterType.LEVEL://解锁skill
                        p_level++;
                        if (p_level > 1)
                            return false;
                        break;
                    default:
                        GameLogger.LogError($"Unknown improve target: {target}");
                        return false;
                }
                GameLogger.LogInfo($"Skill {this.GetType().FullName.Replace('+', '.')} upgrade parameter {target}");
                InfoObserver((int)target);
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Frost Arrow";
                }

                _description.p_levelDescription = "大量降低前方敌人的移动和攻击速度,不能叠加,并造成伤害";
                return _description;
            }
        }

        //攻击前方圆形区域内的所有敌人
        // 技能距离:20
        // 覆盖半径:4
        // 伤害:100
        // 冷却:60s
        public class SkillArrowRain : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                DISTANCE,
                RANGE,
                DAMAGE,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_distance; //最远释放范围
            public float p_radius;
            public float p_damage;
            public float p_interval;
            public int p_intervalCycle;
            public int p_level;

            public SkillArrowRain()
            {
                p_distance = 20f;
                p_radius = 4f;
                p_damage = 100;
                p_interval = 60f;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillArrowRain(IndividualItem item)
            {
                SkillArrowRain skill = item as SkillArrowRain;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_damage = skill.p_damage;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillArrowRain skill = item as SkillArrowRain;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_damage = skill.p_damage;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }

            public override bool Improve(object value)
            {
                ValueTuple<ParameterType, float, GD.CalDeltaType> tupple = (ValueTuple<ParameterType, float, GD.CalDeltaType>)value;
                ParameterType target = tupple.Item1;
                float deltaValue = tupple.Item2;
                GD.CalDeltaType calType = tupple.Item3;
                switch (target)
                {
                    case ParameterType.DISTANCE:
                        p_distance = Utils.CalDeltaValue(p_distance, deltaValue, calType);
                        break;
                    case ParameterType.RANGE:
                        p_radius = Utils.CalDeltaValue(p_radius, deltaValue, calType);
                        break;
                    case ParameterType.DAMAGE:
                        p_damage = Utils.CalDeltaValue(p_damage, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                        break;
                    case ParameterType.LEVEL://解锁skill
                        p_level++;
                        if (p_level > 1)
                            return false;
                        break;
                    default:
                        GameLogger.LogError($"Unknown improve target: {target}");
                        return false;
                }
                GameLogger.LogInfo($"Skill {this.GetType().FullName.Replace('+', '.')} upgrade parameter {target}");
                InfoObserver((int)target);
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Rain Arrow";
                }

                _description.p_levelDescription = "攻击前方圆形区域内的所有敌人";
                return _description;
            }
        }

        //随机击杀技能范围内多个血量低于阈值的敌人,不包括英雄
        // 技能距离:20
        // 覆盖半径: 5
        // 击杀数量:7
        // 血量阈值:50%
        // 冷却:40s
        public class SkillSuddenDemise : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                DISTANCE,
                RANGE,
                KILLNUM,
                HPTHRESHOLD,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_distance;
            public float p_radius;
            public int p_killNum;
            public float p_hpThreshold;
            public float p_interval;
            public int p_intervalCycle;
            public int p_level;

            public SkillSuddenDemise()
            {
                p_distance = 20f;
                p_radius = 5f;  //半径为5
                p_killNum = 7;
                p_hpThreshold = 0.5f; //50%
                p_interval = 50;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillSuddenDemise(IndividualItem item)
            {
                SkillSuddenDemise skill = item as SkillSuddenDemise;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_killNum = skill.p_killNum;
                p_hpThreshold = skill.p_hpThreshold;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillSuddenDemise skill = item as SkillSuddenDemise;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_killNum = skill.p_killNum;
                p_hpThreshold = skill.p_hpThreshold;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }

            public override bool Improve(object value)
            {
                ValueTuple<ParameterType, float, GD.CalDeltaType> tupple = (ValueTuple<ParameterType, float, GD.CalDeltaType>)value;
                ParameterType target = tupple.Item1;
                float deltaValue = tupple.Item2;
                GD.CalDeltaType calType = tupple.Item3;
                switch (target)
                {
                    case ParameterType.DISTANCE:
                        p_distance = Utils.CalDeltaValue(p_distance, deltaValue, calType);
                        break;
                    case ParameterType.RANGE:
                        p_radius = Utils.CalDeltaValue(p_radius, deltaValue, calType);
                        break;
                    case ParameterType.KILLNUM:
                        p_killNum = (int)Utils.CalDeltaValue(p_killNum, deltaValue, calType);
                        break;
                    case ParameterType.HPTHRESHOLD:
                        p_hpThreshold = Utils.CalDeltaValue(p_hpThreshold, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                        break;
                    case ParameterType.LEVEL://解锁skill
                        p_level++;
                        if (p_level > 1)
                            return false;
                        break;
                    default:
                        GameLogger.LogError($"Unknown improve target: {target}");
                        return false;
                }
                GameLogger.LogInfo($"Skill {this.GetType().FullName.Replace('+', '.')} upgrade parameter {target}");
                InfoObserver((int)target);
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Sudden Demise";
                }

                _description.p_levelDescription = "随机击杀技能范围内多个血量阈值的敌人,不包括英雄";
                return _description;
            }
        }
#endregion

        public RangedHeroIndividualData(WE.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.FROSTARROW] = new SkillFrostArrow();
            _individualItems[(int)IndividualDataType.ARROWRAIN] = new SkillArrowRain();
            _individualItems[(int)IndividualDataType.SUDDENDEMISE] = new SkillSuddenDemise();
        }

        public RangedHeroIndividualData(RangedHeroIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.FROSTARROW] = new SkillFrostArrow(data.gs_individualItems[(int)IndividualDataType.FROSTARROW]);
            _individualItems[(int)IndividualDataType.ARROWRAIN] = new SkillArrowRain(data.gs_individualItems[(int)IndividualDataType.ARROWRAIN]);
            _individualItems[(int)IndividualDataType.SUDDENDEMISE] = new SkillSuddenDemise(data.gs_individualItems[(int)IndividualDataType.SUDDENDEMISE]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.FROSTARROW].ReInit(data.gs_individualItems[(int)IndividualDataType.FROSTARROW]);
            _individualItems[(int)IndividualDataType.ARROWRAIN].ReInit(data.gs_individualItems[(int)IndividualDataType.ARROWRAIN]);
            _individualItems[(int)IndividualDataType.SUDDENDEMISE].ReInit(data.gs_individualItems[(int)IndividualDataType.SUDDENDEMISE]);
        }

        //can not apply in base class, because ImproveTarget define in the child class
        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.FROSTARROW:
                    case IndividualDataType.ARROWRAIN:
                    case IndividualDataType.SUDDENDEMISE:
                        return _individualItems[(int)target].Improve(tupple.Item2);
                    default:
                        GameLogger.LogError($"Try to imporve unsupport type {target}");
                        break;
                }
            }

            return false;
        }
    }
}

