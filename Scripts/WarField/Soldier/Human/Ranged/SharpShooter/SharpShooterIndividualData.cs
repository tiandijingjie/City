using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace SharpShooter
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    public class SharpShooterIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            BARRAGE, //连击
            VITALSTRIKE, //要害打击
            CHARGEDSTRIKE, //蓄力击
            MAX,
        }

#region Talent
        //天赋：普通攻击时有10%概率攻击第二个敌人，第二击的攻击力是普通攻击力的120%
        public class Talent : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                SECONDCHANCE, //第二击的概率
                SECONDDAMAGETIMES, //第二击的攻击倍数
                MAX,
            }

            public int p_secondChance;
            public float p_damageTImes;

            public Talent()
            {
                p_secondChance = (int)(0.1f * 100); //10%
                p_damageTImes = 1.2f; //120%
            }

            public Talent(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_secondChance = talent.p_secondChance;
                p_damageTImes = talent.p_damageTImes;
            }

            public override void ReInit(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_secondChance = talent.p_secondChance;
                p_damageTImes = talent.p_damageTImes;
            }

            public override bool IsEnabled()
            {
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
                    case ParameterType.SECONDCHANCE:
                        p_secondChance = (int)Utils.CalDeltaValue(p_secondChance, deltaValue, calType);
                        break;
                    case ParameterType.SECONDDAMAGETIMES:
                        p_damageTImes = Utils.CalDeltaValue(p_damageTImes, deltaValue, calType);
                        break;
                    default:
                        GameLogger.LogError($"Unknown improve target: {target}");
                        return false;
                }
                GameLogger.LogInfo($"Skill {this.GetType().FullName.Replace('+', '.')} upgrade parameter {target}");
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Sharp Shooter Talent";
                }

                _description.p_levelDescription = $"普通攻击有{p_secondChance}%概率攻击第二个敌人，第二击的攻击力是普通攻击力的{p_damageTImes}%";
                return _description;
            }
        }
#endregion

#region Skill

        //连击：普通攻击时有概率缩短接下来多次攻击的攻击间隔
        public class SkillBarrage : IndividualItem
        {
            public int p_chance;
            public float p_timeDown;
            public float p_duration;
            public int p_level;

            public SkillBarrage()
            {
                p_level = 0;
            }

            public SkillBarrage(IndividualItem item)
            {
                SkillBarrage skill = item as SkillBarrage;
                p_chance = skill.p_chance;
                p_timeDown = skill.p_timeDown;
                p_duration = skill.p_duration;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillBarrage skill = item as SkillBarrage;
                p_chance = skill.p_chance;
                p_timeDown = skill.p_timeDown;
                p_duration = skill.p_duration;
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
                if(p_level >= SKD.MAXSOLDIERSKILLLEVEL)
                    return false;
                p_level++;
                switch (p_level)
                {
                    case 1:
                        p_chance = (int)(0.1f * 100); //10%
                        p_timeDown = 1 + 0.4f; //40%  攻击速度增加40%，实际应该是除以0.6，但是除法开销比较打用140%近似计算
                        p_duration = 5; //5s
                        break;
                    case 2:
                        p_chance = (int)(0.15f * 100); //15%
                        break;
                    case 3:
                        p_timeDown = 1 + 0.5f; //50%
                        break;
                    case 4:
                        p_timeDown = 1 + 0.7f; //70%
                        p_duration = 6; //6s
                        break;
                    default:
                        GameLogger.LogError($"Fail to Improve skill");
                        return false;
                }
                GameLogger.LogInfo($"Skill {this.GetType().FullName.Replace('+', '.')} upgrade to level {p_level}");
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_level = p_level;
                    _description.p_name = "Barrage";
                }

                _description.p_levelDescription = $"普通攻击时有{p_chance}%概率缩短接下来{p_duration}s内{(1 - p_timeDown) * 100}%攻击的攻击间隔";
                return _description;
            }
        }

        //要害打击：普通攻击有概率造成攻速相关倍数的伤害
        public class SkillVitalStrike: IndividualItem
        {
            public int p_atkGap;
            public float p_damageUp;
            public int p_level;

            public SkillVitalStrike()
            {
                p_level = 0;
            }

            public SkillVitalStrike(IndividualItem item)
            {
                SkillVitalStrike skill = item as SkillVitalStrike;
                p_atkGap = skill.p_atkGap;
                p_damageUp = skill.p_damageUp;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillVitalStrike skill = item as SkillVitalStrike;
                p_atkGap = skill.p_atkGap;
                p_damageUp = skill.p_damageUp;
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
                if(p_level >= SKD.MAXSOLDIERSKILLLEVEL)
                    return false;
                p_level++;
                switch (p_level)
                {
                    case 1:
                        p_atkGap = 5;
                        p_damageUp = 0.4f; //40%
                        break;
                    case 2:
                        p_damageUp = 0.55f; //55%
                        break;
                    case 3:
                        p_atkGap = 4;
                        break;
                    case 4:
                        p_atkGap = 3;
                        p_damageUp = 0.8f; //80%
                        break;
                    default:
                        GameLogger.LogError($"Fail to Improve skill");
                        return false;
                }
                GameLogger.LogInfo($"Skill {this.GetType().FullName.Replace('+', '.')} upgrade to level {p_level}");
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_level = p_level;
                    _description.p_name = "Vital Strike";
                }

                _description.p_levelDescription = $"每隔{p_atkGap}次普通攻击造成一次攻速*{p_damageUp}%倍数的伤害";
                return _description;
            }
        }

        //蓄力击：每隔一段时间蓄力一定时间后射出一箭打击一条直线上的所有敌人
        public class SkillChargeStrike: IndividualItem
        {
            public float p_interval;
            public float p_intervalCycle;
            public float p_flyDistance;
            public float p_damage;
            public int p_level;

            public SkillChargeStrike()
            {
                p_level = 0;
            }

            public SkillChargeStrike(IndividualItem item)
            {
                SkillChargeStrike skill = item as SkillChargeStrike;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_flyDistance = skill.p_flyDistance;
                p_damage = skill.p_damage;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillChargeStrike skill = item as SkillChargeStrike;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_flyDistance = skill.p_flyDistance;
                p_damage = skill.p_damage;
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
                if(p_level >= SKD.MAXSOLDIERSKILLLEVEL)
                    return false;
                p_level++;
                switch (p_level)
                {
                    case 1:
                        p_interval = 5; //50s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_flyDistance = 8f;
                        p_damage = 50f;
                        break;
                    case 2:
                        p_interval = 45; //45
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        break;
                    case 3:
                        p_flyDistance = 9.5f;
                        p_damage = 60f;
                        break;
                    case 4:
                        p_interval = 30; //30s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_flyDistance = 12f;
                        p_damage = 85f;
                        break;
                    default:
                        GameLogger.LogError($"Fail to Improve skill");
                        return false;
                }
                GameLogger.LogInfo($"Skill {this.GetType().FullName.Replace('+', '.')} upgrade to level {p_level}");
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_level = p_level;
                    _description.p_name = "Charge Strike";
                }

                _description.p_levelDescription = $"蓄力射出一箭，飞行距离{p_flyDistance}，对经过的每个敌人造成{p_damage}点伤害，冷却时间{p_interval}s";
                return _description;
            }
        }
#endregion

        public SharpShooterIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.BARRAGE] = new SkillBarrage();
            _individualItems[(int)IndividualDataType.VITALSTRIKE] = new SkillVitalStrike();
            _individualItems[(int)IndividualDataType.CHARGEDSTRIKE] = new SkillChargeStrike();
        }

        public SharpShooterIndividualData(SharpShooterIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.BARRAGE] = new SkillBarrage(data.gs_individualItems[(int)IndividualDataType.BARRAGE]);
            _individualItems[(int)IndividualDataType.VITALSTRIKE] = new SkillVitalStrike(data.gs_individualItems[(int)IndividualDataType.VITALSTRIKE]);
            _individualItems[(int)IndividualDataType.CHARGEDSTRIKE] = new SkillChargeStrike(data.gs_individualItems[(int)IndividualDataType.CHARGEDSTRIKE]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.BARRAGE] = new SkillBarrage(data.gs_individualItems[(int)IndividualDataType.BARRAGE]);
            _individualItems[(int)IndividualDataType.VITALSTRIKE] = new SkillVitalStrike(data.gs_individualItems[(int)IndividualDataType.VITALSTRIKE]);
            _individualItems[(int)IndividualDataType.CHARGEDSTRIKE] = new SkillChargeStrike(data.gs_individualItems[(int)IndividualDataType.CHARGEDSTRIKE]);
        }

        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.TALENT:
                    case IndividualDataType.BARRAGE:
                    case IndividualDataType.VITALSTRIKE:
                    case IndividualDataType.CHARGEDSTRIKE:
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

