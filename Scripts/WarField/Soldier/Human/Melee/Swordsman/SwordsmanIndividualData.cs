using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarField;

namespace Swordsman
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    public class SwordsmanIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            ENRAGE, //激怒
            STEADFAST, //坚固
            STUN, //晕眩
            MAX,
        }

#region Talent
        //talent
        //普通攻击时15%概率具有30%的吸血效果
        public class Talent : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                BLOODSTEALCHANCE, //吸血概率
                BLOODSTEALPERCENT, //吸血数量
                MAX,
            }

            public float p_bloodStealChance;
            public float p_bloodStealPercent;

            public Talent()
            {
                p_bloodStealChance = 0.15f * 100; //15%
                p_bloodStealPercent = 0.3f;
            }

            public Talent(IndividualItem item)
            {
                Talent talent = (Talent)item;
                p_bloodStealChance = talent.p_bloodStealChance;
                p_bloodStealPercent = talent.p_bloodStealPercent;
            }

            public override void ReInit(IndividualItem item)
            {
                Talent talent = (Talent)item;
                p_bloodStealChance = talent.p_bloodStealChance;
                p_bloodStealPercent = talent.p_bloodStealPercent;
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
                    case ParameterType.BLOODSTEALCHANCE:
                        p_bloodStealChance = Utils.CalDeltaValue(p_bloodStealChance, deltaValue, calType);
                        break;
                    case ParameterType.BLOODSTEALPERCENT:
                        p_bloodStealPercent = Utils.CalDeltaValue(p_bloodStealPercent, deltaValue, calType);
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
                    _description.p_name = "Swordsman talent";
                }

                _description.p_levelDescription = $"普通攻击时{p_bloodStealChance}%概率具有{p_bloodStealPercent}%的吸血效果";
                return _description;
            }
        }
#endregion

#region Skill
        //激怒:当血量低于一定比例的时候大幅提高攻击力和攻击速度
        public class SkillEnrage : IndividualItem
        {
            public float p_hpPercent;
            public float p_attackSpeedUp;
            public float p_damageUp;
            public int p_level;//当前的技能等级

            public SkillEnrage()
            {
                p_level = 0;
            }

            public SkillEnrage(IndividualItem item)
            {
                SkillEnrage skill = item as SkillEnrage;
                p_hpPercent = skill.p_hpPercent;
                p_attackSpeedUp = skill.p_attackSpeedUp;
                p_damageUp = skill.p_damageUp;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillEnrage skill = item as SkillEnrage;
                p_hpPercent = skill.p_hpPercent;
                p_attackSpeedUp = skill.p_attackSpeedUp;
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
                        p_hpPercent = 0.2f; //20%
                        p_attackSpeedUp = 1 + 0.2f;//20%
                        p_damageUp = 1 + 0.15f; //15%
                        break;
                    case 2:
                        p_hpPercent = 0.25f; //25%
                        break;
                    case 3:
                        p_damageUp = 1 + 0.22f; //2%
                        break;
                    case 4:
                        p_hpPercent = 0.4f; //4%
                        p_attackSpeedUp = 1 + 0.35f;//35%
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
                    _description.p_name = "Enrage";
                }

                _description.p_levelDescription = $"当血量低于{p_hpPercent}%的时候提高{p_damageUp - 1}%攻击力和{p_attackSpeedUp - 1}%攻击速度";
                return _description;
            }
        }

        //坚固：当血量低于一定比例的时候大幅提高回血速度和防御力
        public class SkillSteadfast : IndividualItem
        {
            public float p_hpPercent;
            public float p_hpIncUp;
            public float p_armorUp;
            public int p_level;

            public SkillSteadfast()
            {
                p_level = 0;
            }

            public SkillSteadfast(IndividualItem item)
            {
                SkillSteadfast skill = item as SkillSteadfast;
                p_hpPercent = skill.p_hpPercent;
                p_hpIncUp = skill.p_hpIncUp;
                p_armorUp = skill.p_armorUp;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillSteadfast skill = item as SkillSteadfast;
                p_hpPercent = skill.p_hpPercent;
                p_hpIncUp = skill.p_hpIncUp;
                p_armorUp = skill.p_armorUp;
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
                        p_hpPercent = 0.3f;//30%
                        p_hpIncUp = 1 + 1f; //100%
                        p_armorUp = 2;
                        break;
                    case 2:
                        p_armorUp = 3.5f;
                        break;
                    case 3:
                        p_hpPercent = 0.4f;//40%
                        break;
                    case 4:
                        p_hpIncUp = 1 + 2.0f; //200%
                        p_armorUp = 4.5f;
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
                    _description.p_name = "Steadfast";
                }

                _description.p_levelDescription = $"当血量低于{p_hpPercent}%的时候提高{p_hpIncUp - 1}%的回血速度，{p_armorUp}的护甲";
                return _description;
            }
        }

        //晕眩：普通攻击有一定概率击晕普通敌人并造成额外伤害
        public class SkillStun: IndividualItem
        {
            public int p_chance;
            public float p_stunDuration;
            public float p_stunDamage;

            public bool p_heroEnable;
            public float p_heroChance;
            public float p_heroStunDuration;
            public float p_heroStunDamage;
            public int p_level;

            public SkillStun()
            {
                p_level = 0;
            }

            public SkillStun(IndividualItem item)
            {
                SkillStun skill = item as SkillStun;
                p_chance = skill.p_chance;
                p_stunDuration = skill.p_stunDuration;
                p_stunDamage = skill.p_stunDamage;
                p_heroEnable = skill.p_heroEnable;
                p_heroChance = skill.p_heroChance;
                p_heroStunDuration = skill.p_heroStunDuration;
                p_heroStunDamage = skill.p_heroStunDamage;

                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillStun skill = item as SkillStun;
                p_chance = skill.p_chance;
                p_stunDuration = skill.p_stunDuration;
                p_stunDamage = skill.p_stunDamage;

                p_heroEnable = skill.p_heroEnable;
                p_heroChance = skill.p_heroChance;
                p_heroStunDuration = skill.p_heroStunDuration;
                p_heroStunDamage = skill.p_heroStunDamage;
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
                        p_chance = (int)(0.08f * 100); //8%
                        p_stunDuration = 2; //2s
                        p_stunDamage = 20;
                        p_heroEnable = false;
                        break;
                    case 2:
                        p_chance = (int)(0.17f * 100); //17%
                        break;
                    case 3:
                        p_stunDuration = 3.5f; //3.5s
                        break;
                    case 4:
                        p_stunDamage = 80;
                        p_heroEnable = true;
                        p_heroChance = (int)(0.05f * 100); //5%
                        p_heroStunDuration = 1f; //1s
                        p_heroStunDamage = 30;
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
                    _description.p_name = "Stun";
                }

                _description.p_levelDescription = $"普通攻击有{p_heroChance}概率击晕普通敌人{p_stunDuration}并造成{p_stunDamage}额外伤害";
                if(p_heroEnable == true)
                    _description.p_levelDescription += $"\n普通攻击有{p_heroChance}概率击晕boss单位{p_heroStunDuration}并造成{p_heroStunDamage}额外伤害";
                return _description;
            }
        }
#endregion

        public SwordsmanIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.ENRAGE] = new SkillEnrage();
            _individualItems[(int)IndividualDataType.STEADFAST] = new SkillSteadfast();
            _individualItems[(int)IndividualDataType.STUN] = new SkillStun();
        }

        public SwordsmanIndividualData(SwordsmanIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.ENRAGE] = new SkillEnrage(data.gs_individualItems[(int)IndividualDataType.ENRAGE]);
            _individualItems[(int)IndividualDataType.STEADFAST] = new SkillSteadfast(data.gs_individualItems[(int)IndividualDataType.STEADFAST]);
            _individualItems[(int)IndividualDataType.STUN] = new SkillStun(data.gs_individualItems[(int)IndividualDataType.STUN]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.ENRAGE].ReInit(data.gs_individualItems[(int)IndividualDataType.ENRAGE]);
            _individualItems[(int)IndividualDataType.STEADFAST].ReInit(data.gs_individualItems[(int)IndividualDataType.STEADFAST]);
            _individualItems[(int)IndividualDataType.STUN].ReInit(data.gs_individualItems[(int)IndividualDataType.STUN]);
        }

        //can not apply in base class, because ImproveTarget define in the child class
        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.TALENT:
                    case IndividualDataType.ENRAGE:
                    case IndividualDataType.STEADFAST:
                    case IndividualDataType.STUN:
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

