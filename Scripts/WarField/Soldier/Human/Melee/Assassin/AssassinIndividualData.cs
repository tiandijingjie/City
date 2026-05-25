using System;
using System.Collections;
using System.Collections.Generic;
using WarField;

namespace Assassin
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    public class AssassinIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            CRIT, //暴击
            SUNDERDEFENSES, //破甲
            ASSASSINATE, //暗杀
            MAX,
        }

#region Talent
        //talent
        //5s没有攻击或者被攻击进入隐身，无法主动成为敌人目标，破隐一击造成2.5倍普通伤害的额外伤害
        public class Talent : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                HIDEINTERVAL, //隐身时间
                STRIKETIMES, //破隐一击攻击增强
                MAX,
            }

            public float p_hideInterval;  //渐隐时间
            public float p_hideIntervalCycle;
            public float p_hideStrikeTimes; //破隐一击

            public Talent()
            {
                p_hideInterval = 5f;//5s
                p_hideIntervalCycle = Utils.CountOfFixUpdate(p_hideInterval);
                p_hideStrikeTimes = 1 + 2.5f;
            }

            public Talent(IndividualItem item)
            {
                Talent talent = (Talent)item;
                p_hideInterval = talent.p_hideInterval;
                p_hideIntervalCycle = Utils.CountOfFixUpdate(p_hideInterval);
                p_hideStrikeTimes = talent.p_hideStrikeTimes;
            }

            public override void ReInit(IndividualItem item)
            {
                Talent talent = (Talent)item;
                p_hideInterval = talent.p_hideInterval;
                p_hideIntervalCycle = Utils.CountOfFixUpdate(p_hideInterval);
                p_hideStrikeTimes = talent.p_hideStrikeTimes;
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
                    case ParameterType.HIDEINTERVAL:
                        p_hideInterval = Utils.CalDeltaValue(p_hideInterval, deltaValue, calType);
                        p_hideIntervalCycle = Utils.CountOfFixUpdate(p_hideInterval);
                        break;
                    case ParameterType.STRIKETIMES:
                        p_hideStrikeTimes = Utils.CalDeltaValue(p_hideStrikeTimes - 1, deltaValue, calType) + 1; //不影响普通伤害的部分
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
                    _description.p_name = "Assassin Talent";
                }

                _description.p_levelDescription = $"{p_hideInterval}s没有攻击或者被攻击进入隐身，" +
                                                     $"无法主动成为敌人目标，破隐一击造成{p_hideStrikeTimes}倍普通伤害";
                return _description;
            }
        }
#endregion

#region Skill
        //暴击：普通攻击有概率对士兵造成额外伤害
        public class SkillCrit : IndividualItem
        {
            public float p_criticalChance;
            public float p_critTimes;
            public int p_level;//当前的技能等级

            public SkillCrit()
            {
                p_level = 0;
            }

            public SkillCrit(IndividualItem item)
            {
                SkillCrit skill = (SkillCrit)item;
                p_criticalChance = skill.p_criticalChance;
                p_critTimes = skill.p_critTimes;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillCrit skill = (SkillCrit)item;
                p_criticalChance = skill.p_criticalChance;
                p_critTimes = skill.p_critTimes;
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
                        p_criticalChance = 0.05f * 100; //5%
                        p_critTimes = 1 + 0.5f;
                        break;
                    case 2:
                        p_criticalChance = 0.085f * 100; //8.5%
                        break;
                    case 3:
                        p_critTimes = 1 + 0.95f;
                        break;
                    case 4:
                        p_criticalChance = 0.18f * 100;
                        p_critTimes = 1 + 1.75f;
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
                    _description.p_name = "Crit";
                }

                _description.p_levelDescription = $"普通攻击有{p_criticalChance}%概率造成额外{p_critTimes}倍伤害";
                return _description;
            }
        }

        //破甲：普通攻击可以永久降低敌人护甲，不可叠加
        public class SkillSunderDefenses : IndividualItem
        {
            public float p_armorDown; //护甲降低
            public int p_level;

            public SkillSunderDefenses()
            {
                p_level = 0;
            }

            public SkillSunderDefenses(IndividualItem item)
            {
                SkillSunderDefenses skill = (SkillSunderDefenses)item;
                p_armorDown = skill.p_armorDown;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillSunderDefenses skill = (SkillSunderDefenses)item;
                p_armorDown = skill.p_armorDown;
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
                        p_armorDown = 3f;
                        break;
                    case 2:
                        p_armorDown = 4.8f;
                        break;
                    case 3:
                        p_armorDown = 6f;
                        break;
                    case 4:
                        p_armorDown = 8;
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
                    _description.p_name = "SunderDefenses";
                }

                _description.p_levelDescription = $"普通攻击可以永久降低敌人{p_armorDown}点护甲,效果不可叠加";
                return _description;
            }
        }

        //暗杀：每隔一段时间，对目标敌人造成纯粹伤害
        public class SkillAssassinate : IndividualItem
        {
            public float p_interval;
            public float p_intervalCycle;
            public float p_damage;
            public float p_executionPercent; //斩杀线
            public float p_executionChance; //斩杀概率
            public int p_level = 0;

            public SkillAssassinate()
            {
                p_level = 0;
            }

            public SkillAssassinate(IndividualItem item)
            {
                SkillAssassinate skill = (SkillAssassinate)item;
                p_level = skill.p_level;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_damage = skill.p_damage;
                p_executionPercent = skill.p_executionPercent;
                p_executionChance = skill.p_executionChance;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillAssassinate skill = (SkillAssassinate)item;
                p_level = skill.p_level;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_damage = skill.p_damage;
                p_executionPercent = skill.p_executionPercent;
                p_executionChance = skill.p_executionChance;
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
                        p_interval = 25;
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_damage = 90;
                        p_executionPercent = 0.15f; //15%
                        p_executionChance = 0.05f; //5%
                        break;
                    case 2:
                        p_interval = 23;
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        break;
                    case 3:
                        p_damage = 110;
                        break;
                    case 4:
                        p_interval = 20;
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_executionPercent = 0.2f;
                        p_executionChance = 0.07f;
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
                    _description.p_name = "Assassinate";
                }

                _description.p_levelDescription = $"每隔{p_interval}，对目标敌人造成{p_damage}点纯粹伤害，如果非英雄单位的敌人的血量低于{p_executionPercent}%时有{p_executionChance}%概率斩杀";
                return _description;
            }
        }
#endregion

        public AssassinIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.CRIT] = new SkillCrit();
            _individualItems[(int)IndividualDataType.SUNDERDEFENSES] = new SkillSunderDefenses();
            _individualItems[(int)IndividualDataType.ASSASSINATE] = new SkillAssassinate();
        }

        public AssassinIndividualData(AssassinIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.CRIT] = new SkillCrit(data.gs_individualItems[(int)IndividualDataType.CRIT]);
            _individualItems[(int)IndividualDataType.SUNDERDEFENSES] = new SkillSunderDefenses(data.gs_individualItems[(int)IndividualDataType.SUNDERDEFENSES]);
            _individualItems[(int)IndividualDataType.ASSASSINATE] = new SkillAssassinate(data.gs_individualItems[(int)IndividualDataType.ASSASSINATE]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.CRIT].ReInit(data.gs_individualItems[(int)IndividualDataType.CRIT]);
            _individualItems[(int)IndividualDataType.SUNDERDEFENSES].ReInit(data.gs_individualItems[(int)IndividualDataType.SUNDERDEFENSES]);
            _individualItems[(int)IndividualDataType.ASSASSINATE].ReInit(data.gs_individualItems[(int)IndividualDataType.ASSASSINATE]);
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
                    case IndividualDataType.CRIT:
                    case IndividualDataType.SUNDERDEFENSES:
                    case IndividualDataType.ASSASSINATE:
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

