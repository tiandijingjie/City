using System;
using System.Collections;
using System.Collections.Generic;
using WarField;

namespace MeleeHero
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class MeleeHeroIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            FLAMESLASH, //火焰斩
            WHIRLWINDSLASH, //旋风斩
            CRISISUNLEASHED, //危机降临
            MAX,
        }

#region Skill
        //火焰斩 攻击前方一条直线上的敌人 攻击距离:8.5  攻击宽度:2 攻击伤害:120  冷却:55s
        public class SkillFlameSlash : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                LENGTH,
                DAMAGE,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_length;
            public float p_width; //不能升级
            public float p_damage;
            public float p_interval;
            public float p_intervalCycle;
            public int p_level;

            public SkillFlameSlash()
            {
                p_length = 8.5f;
                p_width = 2f;
                p_damage = 120;
                p_interval = 55;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillFlameSlash(IndividualItem item)
            {
                SkillFlameSlash skill = item as SkillFlameSlash;
                p_length = skill.p_length;
                p_width = skill.p_width;
                p_damage = skill.p_damage;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillFlameSlash skill = item as SkillFlameSlash;
                p_length = skill.p_length;
                p_width = skill.p_width;
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
                    case ParameterType.LENGTH:
                        p_length = Utils.CalDeltaValue(p_length, deltaValue, calType);
                        break;
                    case ParameterType.DAMAGE:
                        p_damage = Utils.CalDeltaValue(p_damage, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
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
                    _description.p_name = "Flame Slash";
                }

                _description.p_levelDescription = "攻击前方一条直线上的敌人";
                return _description;
            }
        }

        //旋风斩 攻击周围所有的敌人 攻击范围:4.5 攻击伤害:100每个敌人  冷却:45s
        public class SkillWhirlwindSlash : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                RADIUS,
                DAMAGE,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_radius;
            public float p_damage;
            public float p_interval;
            public float p_intervalCycle;
            public int p_level;

            public SkillWhirlwindSlash()
            {
                p_radius = 4.5f;
                p_damage = 100;
                p_interval = 45;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillWhirlwindSlash(IndividualItem item)
            {
                SkillWhirlwindSlash skill = item as SkillWhirlwindSlash;
                p_radius = skill.p_radius;
                p_damage = skill.p_damage;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillWhirlwindSlash skill = item as SkillWhirlwindSlash;
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
                    case ParameterType.RADIUS:
                        p_radius = Utils.CalDeltaValue(p_radius, deltaValue, calType);
                        break;
                    case ParameterType.DAMAGE:
                        p_damage = Utils.CalDeltaValue(p_damage, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
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
                    _description.p_name = "Whirlwind Slash";
                }

                _description.p_levelDescription = "攻击周围所有的敌人";
                return _description;
            }
        }

        //危机降临  根据技能激活时周围敌人的数量增加闪避和血上限
        //范围：3.5
        //闪避概率:0.8%*敌人数量 (不超过30%)
        //增加血上限:30*敌人数量 （不超过600）
        //持续时间:15s
        //冷却:45s
        public class SkillCrisisUnleashed : IndividualItemWithNotify
        {
            public enum ParameterType
            {
                MIN = 0,
                RADIUS,
                DODGEPERSD, //闪避
                HPMAXPERSD, //血量上限
                DURATION,
                INTERVAL,
                LEVEL, //解锁技能
                MAX,
            }

            public float p_radius;
            public float p_dodgePerSd, p_dodgeMax;
            public float p_hpMaxPerSD, p_hpMax;
            public float p_duration, p_durationCycle;
            public float p_interval, p_intervalCycle;
            public int p_level;

            public SkillCrisisUnleashed()
            {
                p_radius = 3.5f;
                p_dodgePerSd = 0.008f * 100; //0.8%
                p_dodgeMax = 0.3f * 100; //30%
                p_hpMaxPerSD = 15;
                p_hpMax = 600;
                p_duration = 15;
                p_durationCycle = Utils.CountOfFixUpdate(p_duration);
                p_interval = 45;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_level = 0;
            }

            public SkillCrisisUnleashed(IndividualItem item)
            {
                SkillCrisisUnleashed skill = item as SkillCrisisUnleashed;
                p_radius = skill.p_radius;
                p_dodgePerSd = skill.p_dodgePerSd;
                p_dodgeMax = skill.p_dodgeMax;
                p_hpMaxPerSD = skill.p_hpMaxPerSD;
                p_hpMax = skill.p_hpMax;
                p_duration = skill.p_duration;
                p_durationCycle = skill.p_durationCycle;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
                p_level = skill.p_level;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillCrisisUnleashed skill = item as SkillCrisisUnleashed;
                p_radius = skill.p_radius;
                p_dodgePerSd = skill.p_dodgePerSd;
                p_dodgeMax = skill.p_dodgeMax;
                p_hpMaxPerSD = skill.p_hpMaxPerSD;
                p_hpMax = skill.p_hpMax;
                p_duration = skill.p_duration;
                p_durationCycle = skill.p_durationCycle;
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
                    case ParameterType.DODGEPERSD:
                        p_dodgePerSd = Utils.CalDeltaValue(p_dodgePerSd, deltaValue, calType);
                        break;
                    case ParameterType.HPMAXPERSD:
                        p_hpMaxPerSD = Utils.CalDeltaValue(p_hpMaxPerSD, deltaValue, calType);
                        break;
                    case ParameterType.DURATION:
                        p_duration = Utils.CalDeltaValue(p_duration, deltaValue, calType);
                        p_durationCycle = Utils.CountOfFixUpdate(p_duration);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                        break;
                    case ParameterType.LEVEL: //解锁skill
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
                    _description.p_name = "Crisis Unleashed";
                }

                _description.p_levelDescription = "根据周围敌人的数量增加闪避和血上限";
                return _description;
            }
        }
#endregion

        public MeleeHeroIndividualData(WE.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.FLAMESLASH] = new SkillFlameSlash();
            _individualItems[(int)IndividualDataType.WHIRLWINDSLASH] = new SkillWhirlwindSlash();
            _individualItems[(int)IndividualDataType.CRISISUNLEASHED] = new SkillCrisisUnleashed();
        }

        public MeleeHeroIndividualData(MeleeHeroIndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            _individualItems[(int)IndividualDataType.FLAMESLASH] = new SkillFlameSlash(data.gs_individualItems[(int)IndividualDataType.FLAMESLASH]);
            _individualItems[(int)IndividualDataType.WHIRLWINDSLASH] = new SkillWhirlwindSlash(data.gs_individualItems[(int)IndividualDataType.WHIRLWINDSLASH]);
            _individualItems[(int)IndividualDataType.CRISISUNLEASHED] = new SkillCrisisUnleashed(data.gs_individualItems[(int)IndividualDataType.CRISISUNLEASHED]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.FLAMESLASH].ReInit(data.gs_individualItems[(int)IndividualDataType.FLAMESLASH]);
            _individualItems[(int)IndividualDataType.WHIRLWINDSLASH].ReInit(data.gs_individualItems[(int)IndividualDataType.WHIRLWINDSLASH]);
            _individualItems[(int)IndividualDataType.CRISISUNLEASHED].ReInit(data.gs_individualItems[(int)IndividualDataType.CRISISUNLEASHED]);
        }

        //can not apply in base class, because ImproveTarget define in the child class
        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.FLAMESLASH:
                    case IndividualDataType.WHIRLWINDSLASH:
                    case IndividualDataType.CRISISUNLEASHED:
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

