using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarField;

namespace ShieldSoldier
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    public class ShieldSoldierIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            REFLECT, //反击
            SHIELD, //护盾
            SANCTUARY, //守护领域
            MAX,
        }

#region Talent
        //talent
        //受到攻击时有5%概率格挡10点物理伤害
        public class Talent : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                BLOCKCHANCE,
                BLOCKVALUE,
                MAX,
            }

            public Talent()
            {
                p_blockChance = (int)(0.05f * 100);//5%
                p_blockValue = 10f;
            }

            public Talent(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_blockChance = talent.p_blockChance;
                p_blockValue = talent.p_blockValue;
            }

            public override void ReInit(IndividualItem item)
            {
                Talent talent = item as Talent;
                p_blockChance = talent.p_blockChance;
                p_blockValue = talent.p_blockValue;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public int p_blockChance;
            public float p_blockValue;

            public override bool Improve(object value)
            {
                ValueTuple<ParameterType, float, GD.CalDeltaType> tupple = (ValueTuple<ParameterType, float, GD.CalDeltaType>)value;
                ParameterType target = tupple.Item1;
                float deltaValue = tupple.Item2;
                GD.CalDeltaType calType = tupple.Item3;

                switch (target)
                {
                    case ParameterType.BLOCKCHANCE:
                        p_blockChance = (int)Utils.CalDeltaValue(p_blockChance, deltaValue, calType);
                        break;
                    case ParameterType.BLOCKVALUE:
                        p_blockValue = Utils.CalDeltaValue(p_blockValue, deltaValue, calType);
                        break;
                    default:
                        GameLogger.LogError($"Unknown improve target: {target}");
                        return false;
                }

                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Shield Soldier Talent";
                }

                _description.p_levelDescription = $"受到攻击时有{p_blockChance}%概率格挡{p_blockValue}点物理伤害";
                return _description;
            }
        }
#endregion

#region Skill
        //反击：反弹一定的伤害给非英雄单位攻击者,受到攻击时激活
        public class SkillReflect : IndividualItem
        {
            public float p_reflectPercent;
            public float p_reflectDuration;
            public float p_reflectInterval;
            public float p_reflectIntervalCycle;
            public int p_level;

            public SkillReflect()
            {
                p_level = 0;
            }

            public SkillReflect(IndividualItem item)
            {
                SkillReflect skill = item as SkillReflect;
                p_reflectPercent = skill.p_reflectPercent;
                p_reflectDuration = skill.p_reflectDuration;
                p_reflectInterval = skill.p_reflectInterval;
                p_reflectIntervalCycle = Utils.CountOfFixUpdate(p_reflectInterval);
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillReflect skill = item as SkillReflect;
                p_reflectPercent = skill.p_reflectPercent;
                p_reflectDuration = skill.p_reflectDuration;
                p_reflectInterval = skill.p_reflectInterval;
                p_reflectIntervalCycle = Utils.CountOfFixUpdate(p_reflectInterval);
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
                        p_reflectPercent = 0.6f; //60%
                        p_reflectDuration = 5; //5s
                        p_reflectInterval = 45; //45s
                        p_reflectIntervalCycle = Utils.CountOfFixUpdate(p_reflectInterval);
                        break;
                    case 2:
                        p_reflectInterval = 40;
                        p_reflectIntervalCycle = Utils.CountOfFixUpdate(p_reflectInterval);
                        break;
                    case 3:
                        p_reflectPercent = 0.68f; //68%
                        break;
                    case 4:
                        p_reflectPercent = 0.75f; //75%
                        p_reflectDuration = 6.5f; //6.5s
                        p_reflectInterval = 30; //30
                        p_reflectIntervalCycle = Utils.CountOfFixUpdate(p_reflectInterval);
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
                    _description.p_name = "Reflect";
                }

                _description.p_levelDescription = $"受到攻击时反弹{p_reflectPercent}的伤害给攻击者，持续{p_reflectDuration}s，冷却时间{p_reflectInterval}s，无法作用于英雄单位";
                return _description;
            }
        }

        //护盾：获取充能型护盾吸收伤害，护盾被打破时对周围造成伤害
        public class SkillShield : IndividualItem
        {
            public float p_shieldAbsorb;
            public float p_shieldBrokenRecoverTime; //护盾被打破后的恢复时间
            public float p_shieldPeaceRecoverTime; //没有被攻击情况下护盾的恢复时间
            public float p_brokenDamage;
            public float p_damageRange;
            public int p_level;

            public SkillShield()
            {
                p_level = 0;
            }

            public SkillShield(IndividualItem item)
            {
                SkillShield skill = item as SkillShield;
                p_shieldAbsorb = skill.p_shieldAbsorb;
                p_shieldBrokenRecoverTime = skill.p_shieldBrokenRecoverTime;
                p_shieldPeaceRecoverTime = skill.p_shieldPeaceRecoverTime;
                p_brokenDamage = skill.p_brokenDamage;
                p_damageRange = skill.p_damageRange;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillShield skill = item as SkillShield;
                p_shieldAbsorb = skill.p_shieldAbsorb;
                p_shieldBrokenRecoverTime = skill.p_shieldBrokenRecoverTime;
                p_shieldPeaceRecoverTime = skill.p_shieldPeaceRecoverTime;
                p_brokenDamage = skill.p_brokenDamage;
                p_damageRange = skill.p_damageRange;
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
                        p_shieldAbsorb = 150;
                        p_shieldPeaceRecoverTime = 10; //10s
                        p_shieldBrokenRecoverTime = 50; //50s
                        p_brokenDamage = 40;
                        p_damageRange = 1;
                        break;
                    case 2:
                        p_shieldAbsorb = 180;
                        p_shieldBrokenRecoverTime = 45; //45s
                        break;
                    case 3:
                        p_shieldPeaceRecoverTime = 8; //8s
                        p_damageRange = 1.5f;
                        break;
                    case 4:
                        p_shieldAbsorb = 250;
                        p_shieldBrokenRecoverTime = 40; //50s
                        p_brokenDamage = 75;
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
                    _description.p_name = "Shield";
                }

                _description.p_levelDescription = $"充能型护盾吸收{p_shieldAbsorb}点伤害，在没有被攻击的情况下{p_shieldPeaceRecoverTime}s恢复，" +
                                                  $"护盾被打破之后{p_shieldBrokenRecoverTime}s恢复，护盾炸裂会对周围范围{p_brokenDamage}以内的所有敌人造成{p_brokenDamage}点伤害";
                return _description;
            }
        }

        //守护领域：降低周围敌人的攻击速度和攻击力，不可叠加
        public class SkillSanctuary : IndividualItem
        {
            public float p_range;
            public float p_attackSpeedDown;
            public float p_damageDown;
            public bool p_heroEnable;
            public float p_heroAttackSpeedDown;
            public float p_heroDamageDown;
            public int p_level;

            public SkillSanctuary()
            {
                p_level = 0;
            }

            public SkillSanctuary(IndividualItem item)
            {
                SkillSanctuary skill = item as SkillSanctuary;
                p_range = skill.p_range;
                p_attackSpeedDown = skill.p_attackSpeedDown;
                p_damageDown = skill.p_damageDown;
                p_heroEnable = skill.p_heroEnable;
                p_heroAttackSpeedDown = skill.p_heroAttackSpeedDown;
                p_heroDamageDown = skill.p_heroDamageDown;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillSanctuary skill = item as SkillSanctuary;
                p_range = skill.p_range;
                p_attackSpeedDown = skill.p_attackSpeedDown;
                p_damageDown = skill.p_damageDown;
                p_heroEnable = skill.p_heroEnable;
                p_heroAttackSpeedDown = skill.p_heroAttackSpeedDown;
                p_heroDamageDown = skill.p_heroDamageDown;
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
                        p_heroEnable = false;
                        p_range = 2.5f;
                        p_attackSpeedDown = 1 - 0.1f; //10%
                        p_damageDown = 1 - 0.05f; //5%
                        break;
                    case 2:
                        p_attackSpeedDown = 1 - 0.12f;
                        break;
                    case 3:
                        p_damageDown = 1 - 0.09f;
                        break;
                    case 4:
                        p_heroEnable = true;
                        p_range = 3.5f;
                        p_attackSpeedDown = 1 - 0.23f; //23%
                        p_damageDown = 1 - 0.15f; //15%
                        p_heroAttackSpeedDown = 1 - 0.08f; //8%
                        p_heroDamageDown = 1 - 0.08f; //8%
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
                    _description.p_name = "Sanctuary";
                }

                _description.p_levelDescription = $"降低范围{p_range}以内的所有普通敌人{100 - p_attackSpeedDown}%的攻击速度，{100 - p_damageDown}%的攻击力，效果不可叠加";
                if (p_heroEnable == true)
                    _description.p_levelDescription += $"\n降低范围{p_range}以内的所有普通敌人{100 - p_heroAttackSpeedDown}%的攻击速度，{100 - p_heroDamageDown}%的攻击力，效果不可叠加";
                return _description;
            }
        }
#endregion

        public ShieldSoldierIndividualData(WE.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.REFLECT] = new SkillReflect();
            _individualItems[(int)IndividualDataType.SHIELD] = new SkillShield();
            _individualItems[(int)IndividualDataType.SANCTUARY] = new SkillSanctuary();
        }

        public ShieldSoldierIndividualData(ShieldSoldierIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.REFLECT] = new SkillReflect(data.gs_individualItems[(int)IndividualDataType.REFLECT]);
            _individualItems[(int)IndividualDataType.SHIELD] = new SkillShield(data.gs_individualItems[(int)IndividualDataType.SHIELD]);
            _individualItems[(int)IndividualDataType.SANCTUARY] = new SkillSanctuary(data.gs_individualItems[(int)IndividualDataType.SANCTUARY]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.REFLECT].ReInit(data.gs_individualItems[(int)IndividualDataType.REFLECT]);
            _individualItems[(int)IndividualDataType.SHIELD].ReInit(data.gs_individualItems[(int)IndividualDataType.SHIELD]);
            _individualItems[(int)IndividualDataType.SANCTUARY].ReInit(data.gs_individualItems[(int)IndividualDataType.SANCTUARY]);
        }

        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.TALENT:
                    case IndividualDataType.REFLECT:
                    case IndividualDataType.SHIELD:
                    case IndividualDataType.SANCTUARY:
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

