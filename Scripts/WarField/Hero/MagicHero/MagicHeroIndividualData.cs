using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarField;

namespace MagicHero
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class MagicHeroIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            METEORSTRIKE, //陨石
            STORMFURY, //风雨交加
            FROZENSEAL, //冰封
            MAX,
        }

#region Skill

        //陨石
        // 从天降下一颗陨石,造成范围伤害,并给击中的士兵造成持续的燃烧伤害
        // 技能距离:无限
        // 技能半径:4.5
        // 伤害:105
        // 燃烧伤害:8.5/s
        // 燃烧持续时间:8s
        // 技能冷却:65s
        public class SkillMeteorStrike : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                RADIUS,
                DAMAGE,
                BURNDAMAGEPERSEC, //燃烧每秒伤害
                BURNDURATION,
                LEVEL, //解锁技能
                INTERVAL,
            }

            public float p_distance; //不可以修改
            public float p_radius;
            public float p_damage;
            public float p_burnDamagePerSec;
            public float p_burnDuration;
            public float p_interval;
            public int p_intervalCycle;
            public int p_level;

            public SkillMeteorStrike()
            {
                p_distance = -1; //无限
                p_radius = 4.5f;
                p_damage = 105;
                p_burnDamagePerSec = 8.5f;
                p_burnDuration = 8f;
                p_interval = 65;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillMeteorStrike(IndividualItem item)
            {
                SkillMeteorStrike skill = item as SkillMeteorStrike;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_damage = skill.p_damage;
                p_burnDamagePerSec = skill.p_burnDamagePerSec;
                p_burnDuration = skill.p_burnDuration;
                p_interval = skill.p_interval;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillMeteorStrike skill = item as SkillMeteorStrike;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_damage = skill.p_damage;
                p_burnDamagePerSec = skill.p_burnDamagePerSec;
                p_burnDuration = skill.p_burnDuration;
                p_interval = skill.p_interval;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
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
                    case ParameterType.RADIUS:
                        p_radius = Utils.CalDeltaValue(p_radius, deltaValue, calType);
                        break;
                    case ParameterType.DAMAGE:
                        p_damage = Utils.CalDeltaValue(p_damage, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                        break;
                    case ParameterType.BURNDURATION:
                        p_burnDuration = Utils.CalDeltaValue(p_burnDuration, deltaValue, calType);
                        break;
                    case ParameterType.BURNDAMAGEPERSEC:
                        p_burnDamagePerSec = Utils.CalDeltaValue(p_burnDamagePerSec, deltaValue, calType);
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
                    _description.p_name = "Meteor Strike";
                }

                _description.p_levelDescription = "从天降下一颗陨石,造成范围伤害,并给击中的士兵造成持续的燃烧伤害";
                return _description;
            }
        }

        //在一个区域释放狂风,造成范围伤害并永久降低移动速度,移动速度降低不可叠加,对英雄无效
        // 技能距离:无限
        // 技能半径:4
        // 伤害:210
        // 移速降低:20%
        // 技能冷却:105s
        public class SkillStormFury : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                RADIUS,
                DAMAGE,
                MOVEDOWN,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_distance; //不可以修改
            public float p_radius;
            public float p_damage;
            public float p_moveDown;
            public float p_interval;
            public int p_intervalCycle;
            public int p_level;

            public SkillStormFury()
            {
                p_distance = -1; //无限
                p_radius = 4;
                p_damage = 210;
                p_moveDown = 1 - 0.2f;//20%
                p_interval = 105;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillStormFury(IndividualItem item)
            {
                SkillStormFury skill = item as SkillStormFury;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_damage = skill.p_damage;
                p_moveDown = skill.p_moveDown;
                p_interval = skill.p_interval;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillStormFury skill = item as SkillStormFury;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_damage = skill.p_damage;
                p_moveDown = skill.p_moveDown;
                p_interval = skill.p_interval;
                p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
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
                    case ParameterType.RADIUS:
                        p_radius = Utils.CalDeltaValue(p_radius, deltaValue, calType);
                        break;
                    case ParameterType.DAMAGE:
                        p_damage = Utils.CalDeltaValue(p_damage, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = (int)Utils.CountOfFixUpdate(p_interval);
                        break;
                    case ParameterType.MOVEDOWN:
                        p_moveDown = Utils.CalDeltaValue(p_moveDown, deltaValue, calType);
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
                    _description.p_name = "Storm Fury";
                }

                _description.p_levelDescription = "在一个区域释放狂风,造成范围伤害并永久降低移动速度,移动速度降低不可叠加,对英雄无效";
                return _description;
            }
        }

        //冰冻住一个区域的敌人包括敌人的建筑
        // 技能距离:40
        // 技能半径:3
        // 冻结时间:敌人4s,建筑10s
        // 技能冷却:60s
        public class SKillFrozenSeal : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                RADIUS,
                FROZESD,
                FROZEBD,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_distance; //不能improve
            public float p_radius;
            public float p_frozeSd, p_frozeBd;
            public float p_interval;
            public int p_intervalCycle;
            public int p_level;

            public SKillFrozenSeal()
            {
                p_distance = 100; //40
                p_radius = 3;
                p_frozeSd = 4;
                p_frozeBd = 10;
                p_interval = 5; //60s
                p_level = 0;
            }

            public SKillFrozenSeal(IndividualItem item)
            {
                SKillFrozenSeal skill = item as SKillFrozenSeal;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_frozeSd = skill.p_frozeSd;
                p_frozeBd = skill.p_frozeBd;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SKillFrozenSeal skill = item as SKillFrozenSeal;
                p_distance = skill.p_distance;
                p_radius = skill.p_radius;
                p_frozeSd = skill.p_frozeSd;
                p_frozeBd = skill.p_frozeBd;
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
                    case ParameterType.RADIUS:
                        p_radius = Utils.CalDeltaValue(p_radius, deltaValue, calType);
                        break;
                    case ParameterType.FROZESD:
                        p_frozeSd = Utils.CalDeltaValue(p_frozeSd, deltaValue, calType);
                        break;
                    case ParameterType.FROZEBD:
                        p_frozeBd = Utils.CalDeltaValue(p_frozeBd, deltaValue, calType);
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
                    _description.p_name = "Frozen Seal";
                }

                _description.p_levelDescription = "冰冻住一个区域的敌人包括敌人的建筑";
                return _description;
            }
        }
#endregion

        public MagicHeroIndividualData(WE.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.METEORSTRIKE] = new SkillMeteorStrike();
            _individualItems[(int)IndividualDataType.STORMFURY] = new SkillStormFury();
            _individualItems[(int)IndividualDataType.FROZENSEAL] = new SKillFrozenSeal();
        }

        public MagicHeroIndividualData(MagicHeroIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.METEORSTRIKE] = new SkillMeteorStrike(data.gs_individualItems[(int)IndividualDataType.METEORSTRIKE]);
            _individualItems[(int)IndividualDataType.STORMFURY] = new SkillStormFury(data.gs_individualItems[(int)IndividualDataType.STORMFURY]);
            _individualItems[(int)IndividualDataType.FROZENSEAL] = new SKillFrozenSeal(data.gs_individualItems[(int)IndividualDataType.FROZENSEAL]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.METEORSTRIKE].ReInit(data.gs_individualItems[(int)IndividualDataType.METEORSTRIKE]);
            _individualItems[(int)IndividualDataType.STORMFURY].ReInit(data.gs_individualItems[(int)IndividualDataType.STORMFURY]);
            _individualItems[(int)IndividualDataType.FROZENSEAL].ReInit(data.gs_individualItems[(int)IndividualDataType.FROZENSEAL]);
        }

        //can not apply in base class, because ImproveTarget define in the child class
        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.METEORSTRIKE:
                    case IndividualDataType.STORMFURY:
                    case IndividualDataType.FROZENSEAL:
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

