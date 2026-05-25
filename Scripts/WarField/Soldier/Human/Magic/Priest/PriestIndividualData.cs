using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace Priest
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    //牧师
    public class PriestIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            TALENT,
            REJUVENATION, //回春
            MARTYRDOM,  //献祭
            LIFESHARE,  //分享生命
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
                    _description.p_name = "Priest Talent";
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
        //回春：每隔一段时间为范围内损失生命值最多的友军持续回复一段的生命，不可叠加但可以刷新持续时间
        public class SkillRejuvenation : IndividualItem
        {
            public float p_range;
            public float p_hpIncUp;
            public float p_duration;
            public float p_interval;
            public float p_intervalCycle;
            public int p_level;

            public SkillRejuvenation()
            {
                p_level = 0;
            }

            public SkillRejuvenation(IndividualItem item)
            {
                SkillRejuvenation skill = item as SkillRejuvenation;
                p_range = skill.p_range;
                p_hpIncUp = skill.p_hpIncUp;
                p_duration = skill.p_duration;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillRejuvenation skill = item as SkillRejuvenation;
                p_range = skill.p_range;
                p_hpIncUp = skill.p_hpIncUp;
                p_duration = skill.p_duration;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
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
                        p_range = 5;
                        p_hpIncUp = 6 * Time.fixedDeltaTime;
                        p_duration = 6; //6s
                        p_interval = 20; //20s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        break;
                    case 2:
                        p_duration = 8;
                        break;
                    case 3:
                        p_hpIncUp = 10 * Time.fixedDeltaTime;
                        break;
                    case 4:
                        p_duration = 9; //9s
                        p_interval = 15; //15s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
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
                    _description.p_name = "Rejuvenation";
                }

                _description.p_levelDescription = $"每隔{p_interval}s为范围内损失生命值最多的友军持续{p_duration}s回复生命,每秒回复{p_hpIncUp}";
                return _description;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }
        }

        //献祭：缩短攻击距离，降低当血量极低时献祭自身，在一定时间一定范围内友军按照一定比例回复血量，自身进入无敌状态，技能释放完成之后立刻死亡
        public class SkillMartyrdom : IndividualItem
        {
            public float p_hpLimit; //触发生命值
            public float p_duration;
            public float p_range;
            public float p_hpIncUp;
            public float p_atkRangeDown;
            public int p_level;

            public SkillMartyrdom()
            {
                p_level = 0;
            }

            public SkillMartyrdom(IndividualItem item)
            {
                SkillMartyrdom skill = item as SkillMartyrdom;
                p_hpLimit = skill.p_hpLimit;
                p_duration = skill.p_duration;
                p_range = skill.p_range;
                p_hpIncUp = skill.p_hpIncUp;
                p_atkRangeDown = skill.p_atkRangeDown;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillMartyrdom skill = item as SkillMartyrdom;
                p_hpLimit = skill.p_hpLimit;
                p_duration = skill.p_duration;
                p_range = skill.p_range;
                p_hpIncUp = skill.p_hpIncUp;
                p_atkRangeDown = skill.p_atkRangeDown;
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
                        p_atkRangeDown = 1 - 0.3f;//30%
                        p_hpLimit = 0.1f; //10%
                        p_range = 3;
                        p_hpIncUp = 25 * Time.fixedDeltaTime;
                        p_duration = 4; //4s
                        break;
                    case 2:
                        p_atkRangeDown = 1 - 0.35f; //35%
                        p_duration = 4.5f;
                        break;
                    case 3:
                        p_hpLimit = 0.15f; //15%
                        break;
                    case 4:
                        p_atkRangeDown = 1 - 0.5f;//50%
                        p_hpLimit = 0.25f; //25%
                        p_range = 4f;
                        p_hpIncUp = 30 * Time.fixedDeltaTime;
                        p_duration = 5; //5s
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
                    _description.p_name = "Martyrdom";
                }

                _description.p_levelDescription = $"缩短{p_atkRangeDown}攻击距离\n" +
                                                  $"当血量降低到{p_hpLimit}%时献祭自身，" +
                                                  $"{p_range}范围内的友军每秒回复{p_hpIncUp}的生命持续{p_duration},\n" +
                                                  $"技能释放完成之后牧师立刻死亡";
                return _description;
            }

            public override bool IsEnabled()
            {
                if(p_level == 0)
                    return false;
                return true;
            }
        }

        //分享生命：将提升自身的血量上限以及回血速度，每隔一段时间将自身一定比例的血量分享给范围内损失生命值最多的友军
        public class SkillLifeShare : IndividualItem
        {
            public float p_range;
            public float p_hpMaxUp;
            public float p_hpIncUp;
            public float p_interval;
            public float p_intervalCycle;
            public float p_hpsharePercent;
            public int p_level;

            public SkillLifeShare()
            {
                p_level = 0;
            }

            public SkillLifeShare(IndividualItem item)
            {
                SkillLifeShare skill = item as SkillLifeShare;
                p_range = skill.p_range;
                p_hpMaxUp = skill.p_hpMaxUp;
                p_hpIncUp = skill.p_hpIncUp;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_hpsharePercent = skill.p_hpsharePercent;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillLifeShare skill = item as SkillLifeShare;
                p_range = skill.p_range;
                p_hpMaxUp = skill.p_hpMaxUp;
                p_hpIncUp = skill.p_hpIncUp;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_hpsharePercent = skill.p_hpsharePercent;
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
                        p_range = 4f;
                        p_hpMaxUp = 1 + 0.5f; //50%
                        p_hpIncUp = 3 * Time.fixedDeltaTime;
                        p_interval = 50; //50s
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        p_hpsharePercent = 0.3f; //30%
                        break;
                    case 2:
                        p_hpMaxUp = 1 + 0.6f; //60%
                        break;
                    case 3:
                        p_interval = 45;
                        p_intervalCycle = Utils.CountOfFixUpdate(p_intervalCycle);
                        break;
                    case 4:
                        p_hpMaxUp = 1 + 1; //100%
                        p_hpIncUp = 4 * Time.fixedDeltaTime;
                        p_hpsharePercent = 0.5f; //50%
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
                    _description.p_name = "Life Share";
                }

                _description.p_levelDescription = $"提升牧师{p_hpMaxUp}%血上限和{p_hpIncUp}点/s的回血速度\n" +
                                                  $"恢复{p_range}范围内损失生命最多的己方士兵的生命同时牧师失去相同生命值，" +
                                                  $"恢复最大数量为牧师当前生命的{p_hpsharePercent}%，冷却时间{p_interval}s";
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

        public PriestIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent();
            _individualItems[(int)IndividualDataType.REJUVENATION] = new SkillRejuvenation();
            _individualItems[(int)IndividualDataType.MARTYRDOM] = new SkillMartyrdom();
            _individualItems[(int)IndividualDataType.LIFESHARE] = new SkillLifeShare();
        }

        public PriestIndividualData(IndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.TALENT] = new Talent(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.REJUVENATION] = new SkillRejuvenation(data.gs_individualItems[(int)IndividualDataType.REJUVENATION]);
            _individualItems[(int)IndividualDataType.MARTYRDOM] = new SkillMartyrdom(data.gs_individualItems[(int)IndividualDataType.MARTYRDOM]);
            _individualItems[(int)IndividualDataType.LIFESHARE] = new SkillLifeShare(data.gs_individualItems[(int)IndividualDataType.LIFESHARE]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.TALENT].ReInit(data.gs_individualItems[(int)IndividualDataType.TALENT]);
            _individualItems[(int)IndividualDataType.REJUVENATION].ReInit(data.gs_individualItems[(int)IndividualDataType.REJUVENATION]);
            _individualItems[(int)IndividualDataType.MARTYRDOM].ReInit(data.gs_individualItems[(int)IndividualDataType.MARTYRDOM]);
            _individualItems[(int)IndividualDataType.LIFESHARE].ReInit(data.gs_individualItems[(int)IndividualDataType.LIFESHARE]);
        }

        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.TALENT:
                    case IndividualDataType.REJUVENATION:
                    case IndividualDataType.MARTYRDOM:
                    case IndividualDataType.LIFESHARE:
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
