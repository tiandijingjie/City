using System;
using System.Collections;
using System.Collections.Generic;
using Priest;
using UnityEngine;

using WarField;

namespace Shaman
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    //萨满祭司
    public class ShamanIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            POLYMORPH, //变羊术
            THRONSNARE, //荆棘术
            SUMMONGOLEM, //召唤石人
            MAX,
        }

#region Talent
        //天赋：无
        public class Talent : IndividualItem
        {
            public Talent()
            {

            }

            public Talent(IndividualItem item)
            {

            }

            public override void ReInit(IndividualItem item)
            {

            }

            public override bool Improve(object value)
            {
                GameLogger.LogInfo($"Priest not has talent, can not improve");
                return true;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Shaman Talent";
                }

                _description.p_levelDescription = $"无";
                return _description;
            }

            public override bool IsEnabled()
            {
                return false;
            }
        }

#endregion

#region Skill
        //- 变形术：将敌人变成无法攻击的羊，无法作用于英雄单位
        public class SkillPolymorph : IndividualItem
        {
            public float p_interval;
            public float p_intervalCycle;
            public float p_duration;
            public float p_range;
            public int p_level;

            public SkillPolymorph()
            {
                p_interval = 0;
            }

            public SkillPolymorph(IndividualItem item)
            {
                SkillPolymorph skill = item as SkillPolymorph;
                p_range = skill.p_range;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_duration = skill.p_duration;
                p_level = skill.p_level;
            }

            override public void ReInit(IndividualItem item)
            {
                SkillPolymorph skill = item as SkillPolymorph;
                p_range = skill.p_range;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_duration = skill.p_duration;
                p_level = skill.p_level;
            }

            public override bool Improve(object value)
            {
                if(p_level >= SKD.MAXSOLDIERSKILLLEVEL)
                    return false;
                p_level++;
                switch (p_level)
                {
                    case 1:
                        p_range = 5.5f;
                        p_interval = 100; //100s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_duration = 5; //5s
                        break;
                    case 2:
                        p_interval = 90; //90s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        break;
                    case 3:
                        p_duration = 6; //6s
                        break;
                    case 4:
                        p_interval = 75; //75
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_duration = 9; //9s
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
                    _description.p_name = "Polymorph";
                }

                _description.p_levelDescription = $"将一个敌人变成无法攻击的羊持续{p_duration},\n" +
                                                  $"冷却时间{p_interval}，无法作用于英雄单位";
                return _description;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }
        }

        //- 荆棘术：召唤荆棘困住一个敌人，敌人困住期间无法攻击，同时受到持续伤害，无法作用于英雄单位
        public class SkillThronSnare : IndividualItem
        {
            public float p_interval;
            public float p_intervalCycle;
            public float p_duration;
            public float p_damagePerSecond;
            public float p_range;
            public int p_level;

            public SkillThronSnare()
            {
                p_level = 0;
            }

            public SkillThronSnare(IndividualItem item)
            {
                SkillThronSnare skill = item as SkillThronSnare;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_duration = skill.p_duration;
                p_damagePerSecond = skill.p_damagePerSecond;
                p_range = skill.p_range;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillThronSnare skill = item as SkillThronSnare;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_duration = skill.p_duration;
                p_damagePerSecond = skill.p_damagePerSecond;
                p_range = skill.p_range;
                p_level = skill.p_level;
            }

            public override bool Improve(object value)
            {
                if(p_level >= SKD.MAXSOLDIERSKILLLEVEL)
                    return false;
                p_level++;
                switch (p_level)
                {
                    case 1:
                        p_interval = 120; //120s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_duration = 5; //5s
                        p_damagePerSecond = 15;
                        p_range = 5.5f;
                        break;
                    case 2:
                        p_interval = 110; //110s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        break;
                    case 3:
                        p_damagePerSecond = 20;
                        break;
                    case 4:
                        p_interval = 90; //90s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_duration = 7; //9s
                        p_damagePerSecond = 25;
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
                    _description.p_name = "Thron Snare";
                }

                _description.p_levelDescription = $"召唤荆棘困住一个敌人，敌人困住期间无法攻击,\n" +
                                                  $"持续{p_duration},冷却时间{p_interval},\n" +
                                                  $"受困期间受到{p_damagePerSecond}点/s伤害";
                return _description;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }
        }

        //- 召唤石人：战斗中召唤石人
        public class SkillSummonGolem : IndividualItem
        {
            public float p_interval;
            public float p_intervalCycle;
            public float p_duration;
            public int p_doubleSummonChance;
            public int p_level;

            public SkillSummonGolem()
            {
                p_level = 0;
            }

            public SkillSummonGolem(IndividualItem item)
            {
                SkillSummonGolem skill = item as SkillSummonGolem;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_duration = skill.p_duration;
                p_doubleSummonChance = skill.p_doubleSummonChance;
                p_level = skill.p_level;
            }

            override public void ReInit(IndividualItem item)
            {
                SkillSummonGolem skill = item as SkillSummonGolem;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_duration = skill.p_duration;
                p_doubleSummonChance = skill.p_doubleSummonChance;
                p_level = skill.p_level;
            }

            public override bool Improve(object value)
            {
                if(p_level >= SKD.MAXSOLDIERSKILLLEVEL)
                    return false;
                p_level++;
                switch (p_level)
                {
                    case 1:
                        p_interval = 150; //150s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_duration = 20; //20s
                        p_doubleSummonChance = 0;
                        break;
                    case 2:
                        p_duration = 30;
                        break;
                    case 3:
                        p_interval = 120; //120s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        break;
                    case 4:
                        p_doubleSummonChance = (int)(0.3f * 100); //30%
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
                    _description.p_name = "Summon Golem";
                }

                _description.p_levelDescription = $"战斗中召唤石人,冷却时间{p_interval}s,石人持续时间{p_duration}s\n";
                if (p_doubleSummonChance > 0)
                    _description.p_levelDescription += $"有{p_doubleSummonChance}%概率一次召唤两个石人";
                return _description;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }
        }
#endregion

        public ShamanIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.POLYMORPH] = new SkillPolymorph();
            _individualItems[(int)IndividualDataType.THRONSNARE] = new SkillThronSnare();
            _individualItems[(int)IndividualDataType.SUMMONGOLEM] = new SkillSummonGolem();
        }

        public ShamanIndividualData(IndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.POLYMORPH] = new SkillPolymorph(data.gs_individualItems[(int)IndividualDataType.POLYMORPH]);
            _individualItems[(int)IndividualDataType.THRONSNARE] = new SkillThronSnare(data.gs_individualItems[(int)IndividualDataType.THRONSNARE]);
            _individualItems[(int)IndividualDataType.SUMMONGOLEM] = new SkillSummonGolem(data.gs_individualItems[(int)IndividualDataType.SUMMONGOLEM]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.POLYMORPH].ReInit(data.gs_individualItems[(int)IndividualDataType.POLYMORPH]);
            _individualItems[(int)IndividualDataType.THRONSNARE].ReInit(data.gs_individualItems[(int)IndividualDataType.THRONSNARE]);
            _individualItems[(int)IndividualDataType.SUMMONGOLEM].ReInit(data.gs_individualItems[(int)IndividualDataType.SUMMONGOLEM]);
        }

        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.TALENT:
                    case IndividualDataType.POLYMORPH:
                    case IndividualDataType.THRONSNARE:
                    case IndividualDataType.SUMMONGOLEM:
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

