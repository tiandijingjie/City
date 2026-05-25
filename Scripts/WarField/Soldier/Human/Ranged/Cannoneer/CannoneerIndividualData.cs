using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace Cannoneer
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    //火炮手
    public class CannoneerIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            INTIMIDATE, //震慑
            BLEEDING, //流血
            SIEGE, //攻城
            MAX,
        }

#region Talent
        //天赋：范围攻击,普通攻击会同时攻击目标范围内1.5以内的所有敌人（包括建筑），造成普通攻击60%的伤害
        public class Talent : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                RANGE,
                DAMAGEDOWN,
                MAX,
            }

            public float p_range;
            public float p_damageDown;

            public Talent()
            {
                p_range = 1.5f;
                p_damageDown = 0.6f; //60%
            }

            public Talent(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_range = talent.p_range;
                p_damageDown = talent.p_damageDown;
            }

            public override void ReInit(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_range = talent.p_range;
                p_damageDown = talent.p_damageDown;
            }

            public override bool Improve(object value)
            {
                ValueTuple<ParameterType, float, GD.CalDeltaType> tupple = (ValueTuple<ParameterType, float, GD.CalDeltaType>)value;
                ParameterType target = tupple.Item1;
                float deltaValue = tupple.Item2;
                GD.CalDeltaType calType = tupple.Item3;
                switch (target)
                {
                    case ParameterType.RANGE:
                        p_range = (int)Utils.CalDeltaValue(p_range, deltaValue, calType);
                        break;
                    case ParameterType.DAMAGEDOWN:
                        p_damageDown = Utils.CalDeltaValue(p_damageDown, deltaValue, calType);
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
                    _description.p_name = "Cannoneer Talent";
                }

                _description.p_levelDescription = $"范围攻击,普通攻击会同时攻击目标范围内{p_range}以内的所有敌人(包括建筑)，\n造成普通攻击{p_damageDown}%的伤害";
                return _description;
            }

            public override bool IsEnabled()
            {
                return true;
            }
        }

#endregion

#region Skill
        //- 震慑：普通攻击一定的概率对攻击范围内的所有敌人造成移动速度，攻击速度的降低，不可作用于英雄单位
        public class SkillIntimidate : IndividualItem
        {
            public int p_chance;
            public float p_moveDown;
            public float p_atkSpeedDown;
            public float p_duration;
            public int p_level;

            public SkillIntimidate()
            {
                p_level = 0;
            }

            public SkillIntimidate(IndividualItem item)
            {
                SkillIntimidate skill = item as SkillIntimidate;
                p_chance = skill.p_chance;
                p_moveDown = skill.p_moveDown;
                p_atkSpeedDown = skill.p_atkSpeedDown;
                p_duration = skill.p_duration;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillIntimidate skill = item as SkillIntimidate;
                p_chance = skill.p_chance;
                p_moveDown = skill.p_moveDown;
                p_atkSpeedDown = skill.p_atkSpeedDown;
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
                        p_chance = (int)(100 * 0.2f); //20%
                        p_moveDown = 1 - 0.15f; //15%
                        p_atkSpeedDown = 1 - 0.1f; //10%
                        p_duration = 4.5f; //4.5s
                        break;
                    case 2:
                        p_moveDown = 1 - 0.25f; //25%
                        break;
                    case 3:
                        p_atkSpeedDown = 1 - 0.18f; //18%
                        break;
                    case 4:
                        p_chance = (int)(100 * 0.35f); //35%
                        p_moveDown = 1 - 0.35f; //35%
                        p_atkSpeedDown = 1 - 0.25f; //25%
                        p_duration = 8f; //8s
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
                    _description.p_name = "Intimidate";
                }

                _description.p_levelDescription = $"普通攻击有{p_chance}%概率对攻击范围内的所有敌人降低{1-p_moveDown}%移动速度，\n" +
                                                  $"{1 - p_atkSpeedDown}%攻击速度的降低，持续{p_duration}秒";
                return _description;
            }
        }

        //流血：攻击范围内的敌人有一定概率受到持续伤害，不可叠加，不可作用于英雄单位
        public class SkillBleeding : IndividualItem
        {
            public int p_chance;
            public float p_damage; //damage per second
            public float p_duration;
            public int p_level;

            public SkillBleeding()
            {
                p_level = 0;
            }

            public SkillBleeding(IndividualItem item)
            {
                SkillBleeding skill = item as SkillBleeding;
                p_chance = skill.p_chance;
                p_damage = skill.p_damage;
                p_duration = skill.p_duration;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillBleeding skill = item as SkillBleeding;
                p_chance = skill.p_chance;
                p_damage = skill.p_damage;
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
                        p_chance = (int)(100 * 0.12f); //12%
                        p_damage = 20;
                        p_duration = 5; //5s
                        break;
                    case 2:
                        p_damage = 25;
                        break;
                    case 3:
                        p_chance = (int)(100 * 0.18f); //18%
                        break;
                    case 4:
                        p_chance = (int)(100 * 0.21f); //21%
                        p_damage = 35;
                        p_duration = 11; //11s
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
                    _description.p_name = "Bleeding";
                }

                _description.p_levelDescription = $"攻击范围内的敌人有{p_chance}%概率受到持续{p_duration}伤害,每秒伤害{p_damage}";
                return _description;
            }
        }

        //- 攻城：如果主目标是建筑的时候普通攻击带有一定额外伤害
        public class SkillSiege : IndividualItem
        {
            public float p_damageUp;
            public int p_level;

            public SkillSiege()
            {
                p_level = 0;
            }

            public SkillSiege(IndividualItem item)
            {
                SkillSiege skill = item as SkillSiege;
                p_damageUp = skill.p_damageUp;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillSiege skill = item as SkillSiege;
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
                        p_damageUp = 1 + 0.1f; //10%
                        break;
                    case 2:
                        p_damageUp = 1 + 0.2f; //20%
                        break;
                    case 3:
                        p_damageUp = 1 + 0.3f; //30%
                        break;
                    case 4:
                        p_damageUp = 1 + 0.6f; //60%
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
                    _description.p_name = "Siege";
                }

                _description.p_levelDescription = $"如果主目标是建筑的时候普通攻击造成{p_damageUp - 1}%额外伤害";
                return _description;
            }
        }
#endregion

        public CannoneerIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.INTIMIDATE] = new SkillIntimidate();
            _individualItems[(int)IndividualDataType.BLEEDING] = new SkillBleeding();
            _individualItems[(int)IndividualDataType.SIEGE] = new SkillSiege();
        }

        public CannoneerIndividualData(IndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.INTIMIDATE] = new SkillIntimidate(data.gs_individualItems[(int)IndividualDataType.INTIMIDATE]);
            _individualItems[(int)IndividualDataType.BLEEDING] = new SkillBleeding(data.gs_individualItems[(int)IndividualDataType.BLEEDING]);
            _individualItems[(int)IndividualDataType.SIEGE] = new SkillSiege(data.gs_individualItems[(int)IndividualDataType.SIEGE]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.INTIMIDATE].ReInit(data.gs_individualItems[(int)IndividualDataType.INTIMIDATE]);
            _individualItems[(int)IndividualDataType.BLEEDING].ReInit(data.gs_individualItems[(int)IndividualDataType.BLEEDING]);
            _individualItems[(int)IndividualDataType.SIEGE].ReInit(data.gs_individualItems[(int)IndividualDataType.SIEGE]);
        }

        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.TALENT:
                    case IndividualDataType.INTIMIDATE:
                    case IndividualDataType.BLEEDING:
                    case IndividualDataType.SIEGE:
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

