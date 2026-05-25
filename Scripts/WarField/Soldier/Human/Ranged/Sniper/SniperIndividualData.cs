using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace Sniper
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    public class SniperIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            HEADSHOT, //爆头
            RALLYINGCRY, //凝聚人心
            FOCUS, //专注
            MAX,
        }

#region Talent

        //天赋：普通攻击有7%概率暴击造成1.8倍伤害
        public class Talent : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                CHANCE,
                DAMGEUP,
                MAX,
            }

            public int p_chance;
            public float p_damgeUp;

            public Talent()
            {
                p_chance = (int)(0.07f * 100); //7%
                p_damgeUp = 1 + 0.8f; //0.8倍
            }

            public Talent(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_chance = talent.p_chance;
                p_damgeUp = talent.p_damgeUp;
            }

            public override void ReInit(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_chance = talent.p_chance;
                p_damgeUp = talent.p_damgeUp;
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
                    case ParameterType.CHANCE:
                        p_chance = (int)Utils.CalDeltaValue(p_chance, deltaValue, calType);
                        break;
                    case ParameterType.DAMGEUP:
                        p_damgeUp = Utils.CalDeltaValue(p_damgeUp - 1, deltaValue, calType) + 1; //only affect the increase part
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
                    _description.p_name = "Sniper Talent";
                }

                _description.p_levelDescription = $"普通攻击有{p_chance}%概率暴击造成{p_damgeUp}倍伤害";
                return _description;
            }
        }

#endregion

#region Skill
        //爆头：每击杀一个敌人有概率增加一定攻击力
        public class SkillHeadShot : IndividualItem
        {
            public int p_chance;
            public float p_damageInc;
            public int p_level;

            public SkillHeadShot()
            {
                p_level = 0;
            }

            public SkillHeadShot(IndividualItem item)
            {
                SkillHeadShot skillHeadShot = item as SkillHeadShot;
                p_chance = skillHeadShot.p_chance;
                p_damageInc = skillHeadShot.p_damageInc;
                p_level = skillHeadShot.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillHeadShot skillHeadShot = item as SkillHeadShot;
                p_chance = skillHeadShot.p_chance;
                p_damageInc = skillHeadShot.p_damageInc;
                p_level = skillHeadShot.p_level;
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
                        p_chance = (int)(0.4f * 100); //40%
                        p_damageInc = 1.2f;
                        break;
                    case 2:
                        p_damageInc = 2.5f;
                        break;
                    case 3:
                        p_chance = (int)(0.55f * 100); //55%
                        break;
                    case 4:
                        p_chance = (int)(0.90f * 100); //90%
                        p_damageInc = 4f;
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
                    _description.p_name = "Head Shot";
                }

                _description.p_levelDescription = $"普通攻击有{p_chance}%概率增加{p_damageInc}点攻击力";
                return _description;
            }
        }

        //凝聚人心：周围近战士兵，远程士兵的数量会对攻击速度和攻击力加成
        public class SkillRallyingCry : IndividualItem
        {
            public float p_range;
            public float p_meleeDamgeInc;
            public float p_rangedAtkSpeedUp;
            public int p_level;

            public SkillRallyingCry()
            {
                p_level = 0;
            }

            public SkillRallyingCry(IndividualItem item)
            {
                SkillRallyingCry skill = item as SkillRallyingCry;
                p_range = skill.p_range;
                p_meleeDamgeInc = skill.p_meleeDamgeInc;
                p_rangedAtkSpeedUp = skill.p_rangedAtkSpeedUp;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillRallyingCry skill = item as SkillRallyingCry;
                p_range = skill.p_range;
                p_meleeDamgeInc = skill.p_meleeDamgeInc;
                p_rangedAtkSpeedUp = skill.p_rangedAtkSpeedUp;
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
                        p_range = 4f;
                        p_meleeDamgeInc = 0.7f;
                        p_rangedAtkSpeedUp = 0.6f;
                        break;
                    case 2:
                        p_meleeDamgeInc = 1.2f;
                        break;
                    case 3:
                        p_range = 4.5f;
                        break;
                    case 4:
                        p_range = 6.5f;
                        p_meleeDamgeInc = 2.1f;
                        p_rangedAtkSpeedUp = 1.2f;
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
                    _description.p_name = "Rallying Cry";
                }

                _description.p_levelDescription = $"{p_range}范围内，普通攻击造成(近战士兵的数量*{p_meleeDamgeInc})点额外伤害，" +
                                                  $"提升（远程士兵数量*{p_rangedAtkSpeedUp}）%的攻击速度";
                return _description;
            }
        }

        //专注：在攻击同一个敌人时会随着攻击次数的增加会缩短攻击间隔
        public class SkillFocus : IndividualItem
        {
            public float p_atkGapDown;
            public float p_atkGapMax; //最大间隔时间缩短
            public int p_level;

            public SkillFocus()
            {
                p_level = 0;
            }

            public SkillFocus(IndividualItem item)
            {
                SkillFocus skill = item as SkillFocus;
                p_atkGapDown = skill.p_atkGapDown;
                p_atkGapMax = skill.p_atkGapMax;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillFocus skill = item as SkillFocus;
                p_atkGapDown = skill.p_atkGapDown;
                p_atkGapMax = skill.p_atkGapMax;
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
                        p_atkGapDown = 0.035f; //3.5%
                        p_atkGapMax = 0.5f; //50%
                        break;
                    case 2:
                        p_atkGapDown = 0.05f; //5%
                        break;
                    case 3:
                        p_atkGapMax = 0.6f; //60%
                        break;
                    case 4:
                        p_atkGapDown = 0.08f; //3.5%
                        p_atkGapMax = 0.9f; //50%
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
                    _description.p_name = "Focus";
                }

                _description.p_levelDescription = $"在攻击同一个敌人时每次攻击缩短{p_atkGapDown}%攻击间隔，最多缩短{p_atkGapMax}%间隔";
                return _description;
            }
        }
#endregion

        public SniperIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.HEADSHOT] = new SkillHeadShot();
            _individualItems[(int)IndividualDataType.RALLYINGCRY] = new SkillRallyingCry();
            _individualItems[(int)IndividualDataType.FOCUS] = new SkillFocus();
        }

        public SniperIndividualData(SniperIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.HEADSHOT] = new SkillHeadShot(data.gs_individualItems[(int)IndividualDataType.HEADSHOT]);
            _individualItems[(int)IndividualDataType.RALLYINGCRY] = new SkillRallyingCry(data.gs_individualItems[(int)IndividualDataType.RALLYINGCRY]);
            _individualItems[(int)IndividualDataType.FOCUS] = new SkillFocus(data.gs_individualItems[(int)IndividualDataType.FOCUS]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.HEADSHOT].ReInit(data.gs_individualItems[(int)IndividualDataType.HEADSHOT]);
            _individualItems[(int)IndividualDataType.RALLYINGCRY].ReInit(data.gs_individualItems[(int)IndividualDataType.RALLYINGCRY]);
            _individualItems[(int)IndividualDataType.FOCUS].ReInit(data.gs_individualItems[(int)IndividualDataType.FOCUS]);
        }

        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.TALENT:
                    case IndividualDataType.HEADSHOT:
                    case IndividualDataType.RALLYINGCRY:
                    case IndividualDataType.FOCUS:
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

