using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarField;

namespace HeroGeneral
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class HeroGenericIndividualData : IndividualData
    {
        public enum IndividualDataType
        {
            MIN = 0,
            //通用技能
            INVINCIBLE, //无敌
            HASTE, //加速
            //通用天赋
            QUICKATTACK, //快速攻击  天赋
            HEAVYATTACK, //重击 天赋
            DOUBLEDAMAGE, //双倍伤害  天赋
            ALLIEDATTACKBOOST, //友军攻击增强
            ALLIEDATTACKSPEEDBOOST, //友军攻速增强
            ALLIEDSKILLBOOST, //友军技能增强
            SUICIDESQUAD, //自爆小队
            SELFDESTRUCT, //自爆

            STRONG, //强壮
            COUNTERATTACK, //反击
            REVERSAL, //反转
            REJUVENATION, //恢复
            REBIRTH, //新生
            GUARDIANINSTINCT, //护主
            HOSTILEINSTINCT, //敌对
            NEWCHANCE, //新的机会
            FLEE, //逃跑
            LASTBREATH, //锁定
            FLEETFOOT, //急速

            PROSPERITY, //富饶
            BOUNTY, //奖励
            ALCHEMICALMASTERY, //炼金大师   TBD
            COLLECTOR, //收集者
            EXPERIENCE, //经验   TBD
            SWIFTREBIRTH, //快速重生
            GAMBLER, //赌徒   TBD
            DECEPTION, //欺骗   TBD
            MAX,
        }

#region Skill
        public class SkillInvincible : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                DURATION,
                INTERVAL,
                MAX,
            }

            public float p_duration;
            public float p_interval;
            public float p_intervalCycle;

            public SkillInvincible()
            {
                p_duration = 10f;
                p_interval = 20f;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
            }
            public SkillInvincible(IndividualItem item)
            {
                SkillInvincible skill = item as SkillInvincible;
                p_duration = skill.p_duration;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
            }

            public override void ReInit(IndividualItem item)
            {
                SkillInvincible skill = item as SkillInvincible;
                p_duration = skill.p_duration;
                p_interval = skill.p_interval;
                p_intervalCycle = skill.p_intervalCycle;
            }

            public override bool Improve(object value)
            {
                ValueTuple<ParameterType, float, GD.CalDeltaType> tupple = (ValueTuple<ParameterType, float, GD.CalDeltaType>)value;
                ParameterType target = tupple.Item1;
                float deltaValue = tupple.Item2;
                GD.CalDeltaType calType = tupple.Item3;
                switch (target)
                {
                    case ParameterType.DURATION:
                        p_duration = Utils.CalDeltaValue(p_duration, deltaValue, calType);
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
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
                    _description.p_name = "Invincible";
                }

                _description.p_levelDescription =
                    $"进入无敌状态 {p_duration} 秒，免疫所有伤害和控制效果。\n" +
                    $"技能冷却时间：{p_interval} 秒。";
                return _description;
            }

            public override bool IsEnabled()
            {
                return true;
            }
        }

        public class SkillHaste : IndividualItem
        {
            public enum ParameterType
            {
                MIN = 0,
                DURATION,
                SPEEDPERCENTAGE,
                INTERVAL,
                MAX,
            }

            public float p_duration;
            public float p_moveUp;
            public float p_speedPercent;
            public float p_interval;
            public float p_intervalCycle;

            public SkillHaste()
            {
                p_duration = 10f;
                p_speedPercent = 50f; //50%
                p_moveUp = 1 + p_speedPercent / 100;
                p_interval = 20f;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
            }

            public SkillHaste(IndividualItem item)
            {
                SkillHaste skill = item as SkillHaste;
                p_duration = skill.p_duration;
                p_speedPercent = skill.p_speedPercent;
                p_moveUp = 1 + p_speedPercent / 100;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
            }

            public override void ReInit(IndividualItem item)
            {
                SkillHaste skill = item as SkillHaste;
                p_duration = skill.p_duration;
                p_speedPercent = skill.p_speedPercent;
                p_moveUp = 1 + p_speedPercent / 100;
                p_interval = skill.p_interval;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
            }

            public override bool Improve(object value)
            {
                ValueTuple<ParameterType, float, GD.CalDeltaType> tupple = (ValueTuple<ParameterType, float, GD.CalDeltaType>)value;
                ParameterType target = tupple.Item1;
                float deltaValue = tupple.Item2;
                GD.CalDeltaType calType = tupple.Item3;
                switch (target)
                {
                    case ParameterType.DURATION:
                        p_duration = Utils.CalDeltaValue(p_duration, deltaValue, calType);
                        break;
                    case ParameterType.SPEEDPERCENTAGE:
                        p_speedPercent = Utils.CalDeltaValue(p_speedPercent, deltaValue, calType);
                        p_moveUp = 1 + p_speedPercent / 100;
                        break;
                    case ParameterType.INTERVAL:
                        p_interval = Utils.CalDeltaValue(p_interval, deltaValue, calType);
                        p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
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
                    _description.p_name = "Haste";
                }

                _description.p_levelDescription =
                    $"提升移动速度 {p_speedPercent}% ，持续 {p_duration} 秒。\n" +
                    $"技能冷却时间：{p_interval} 秒。";

                return _description;
            }

            public override bool IsEnabled()
            {
                return true;
            }
        }
#endregion

#region Talent
        //hero talent
        //快速攻击   增加攻击速度  增加攻速:15%
        public class TalentQuickAttack : IndividualItem
        {
            public float p_attakSpeedUp;

            public TalentQuickAttack()
            {
                p_attakSpeedUp = 1 + 0.15f; //15%
            }

            public TalentQuickAttack(IndividualItem item)
            {
                TalentQuickAttack talent = (TalentQuickAttack)item;
                p_attakSpeedUp = talent.p_attakSpeedUp;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentQuickAttack talent = (TalentQuickAttack)item;
                p_attakSpeedUp = talent.p_attakSpeedUp;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Hero Quick Attack Talent";
                }

                _description.p_levelDescription = $"Increase Hero {(p_attakSpeedUp - 1) * 100}% Attack Speed";
                return _description;
            }
        }

        //重击 增加攻击伤害 伤害增加15%
        public class TalentHeavyAttack : IndividualItem
        {
            public float p_damageUp;

            public TalentHeavyAttack()
            {
                p_damageUp = 1 + 0.15f; //15%
            }

            public TalentHeavyAttack(IndividualItem item)
            {
                TalentHeavyAttack talent = (TalentHeavyAttack)item;
                talent.p_damageUp = talent.p_damageUp;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentHeavyAttack talent = (TalentHeavyAttack)item;
                talent.p_damageUp = talent.p_damageUp;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Hero Heavy Attack Talent";
                }

                _description.p_levelDescription = $"Increase Hero {(p_damageUp - 1) * 100}% Attack Damage";
                return _description;
            }
        }

        //双重伤害 普通攻击有一定概率造成双倍伤害 概率:12%
        public class TalentDoubleDamage : IndividualItem
        {
            public float p_chance;

            public TalentDoubleDamage()
            {
                p_chance = 0.12f * 100; //12%
            }

            public TalentDoubleDamage(IndividualItem item)
            {
                TalentDoubleDamage talent = (TalentDoubleDamage)item;
                p_chance = talent.p_chance;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentDoubleDamage talent = (TalentDoubleDamage)item;
                p_chance = talent.p_chance;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Hero Double Attack Talent";
                }

                _description.p_levelDescription = $"Hero normal attacks have {p_chance}% chance to deal double damage";
                return _description;
            }
        }

        //友军攻击增强  增加全体与英雄同类型士兵的攻击力 攻击力增加:10
        public class TalentAlliedAttackBoost: IndividualItem
        {
            public float p_damageAdd;

            public TalentAlliedAttackBoost()
            {
                p_damageAdd = 10;
            }

            public TalentAlliedAttackBoost(IndividualItem item)
            {
                TalentAlliedAttackBoost talent = (TalentAlliedAttackBoost)item;
                p_damageAdd = talent.p_damageAdd;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentAlliedAttackBoost talent = (TalentAlliedAttackBoost)item;
                p_damageAdd = talent.p_damageAdd;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Allied Attack Boost Talent";
                }

                _description.p_levelDescription =
                    $"All allied soldiers of the same type as the hero gain +{p_damageAdd} attack";
                return _description;
            }
        }

        //友军攻速增强 增加全体与英雄同类型士兵的攻击速度 攻速增加:0.5(每秒多攻击0.5次)
        public class TalentAlliedAttackSpeedBoost : IndividualItem
        {
            public float p_attackSpeedAdd;

            public TalentAlliedAttackSpeedBoost()
            {
                p_attackSpeedAdd = 0.5f;
            }

            public TalentAlliedAttackSpeedBoost(IndividualItem item)
            {
                TalentAlliedAttackSpeedBoost talent = (TalentAlliedAttackSpeedBoost)item;
                p_attackSpeedAdd = talent.p_attackSpeedAdd;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentAlliedAttackSpeedBoost talent = (TalentAlliedAttackSpeedBoost)item;
                p_attackSpeedAdd = talent.p_attackSpeedAdd;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Allied Attack Speed Boost Talent";
                }

                _description.p_levelDescription =
                    $"All allied soldiers of the same type as the hero gain +{p_attackSpeedAdd} attack speed";
                return _description;
            }
        }

        //友军技能增强  缩短全体与英雄同类型士兵的技能冷却速度  冷却时间减少:15%
        public class TalentAlliedSkillBoost : IndividualItem
        {
            public float p_skillTimeStepUp;

            public TalentAlliedSkillBoost()
            {
                p_skillTimeStepUp = 1 / (1 - 0.15f); //15% 冷却时间减少15%,相当于技能的timestep增加
            }

            public TalentAlliedSkillBoost(IndividualItem item)
            {
                TalentAlliedSkillBoost talent = (TalentAlliedSkillBoost)item;
                p_skillTimeStepUp = talent.p_skillTimeStepUp;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentAlliedSkillBoost talent = (TalentAlliedSkillBoost)item;
                p_skillTimeStepUp = talent.p_skillTimeStepUp;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Allied Skill Boost Talent";
                }

                _description.p_levelDescription =
                    $"All allied soldiers of the same type as the hero have their skill colldown reduce by {p_skillTimeStepUp}";
                return _description;
            }
        }

        //自爆小队: 全部己方士兵在死亡时有一定概率会自爆,无法攻击建筑
        //自爆范围：3.5
        // 同类型士兵概率：18%
        // 不同类型士兵概率：8%
        // 伤害：40
        public class TalentSuicideSquad : IndividualItem
        {
            public float p_range;
            public int p_sameTroopChance, p_otherTroopChance;
            public float p_damage;

            public TalentSuicideSquad()
            {
                p_range = 3.5f;
                p_sameTroopChance = (int)(0.18f * 100); //18%
                p_otherTroopChance = (int)(0.08f * 100); //8%
                p_damage = 40;
            }

            public TalentSuicideSquad(IndividualItem item)
            {
                TalentSuicideSquad talent = item as TalentSuicideSquad;
                p_range = talent.p_range;
                p_sameTroopChance = talent.p_sameTroopChance;
                p_otherTroopChance = talent.p_otherTroopChance;
                p_damage = talent.p_damage;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentSuicideSquad talent = item as TalentSuicideSquad;
                p_range = talent.p_range;
                p_sameTroopChance = talent.p_sameTroopChance;
                p_otherTroopChance = talent.p_otherTroopChance;
                p_damage = talent.p_damage;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Suicide Squad Talent";
                }

                _description.p_levelDescription =
                    $"All allied soldiers have a chance to self-destruct upon death";
                return _description;
            }
        }

        //自爆   英雄死亡时会爆炸对周围敌人和建筑造成大量伤害  范围:7.5  伤害:280
        public class TalentSelfDestruct : IndividualItem
        {
            public float p_range;
            public float p_damage;

            public TalentSelfDestruct()
            {
                p_range = 7.5f;
                p_damage = 280;
            }

            public TalentSelfDestruct(IndividualItem item)
            {
                TalentSelfDestruct talent = (TalentSelfDestruct)item;
                p_range = talent.p_range;
                p_damage = talent.p_damage;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentSelfDestruct talent = (TalentSelfDestruct)item;
                p_range = talent.p_range;
                p_damage = talent.p_damage;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Attack Speed Boost Talent";
                }

                _description.p_levelDescription =
                    $"Upon death, the hero explodes, dealing {p_damage} damage to all enemies and structures within {p_range} range";
                return _description;
            }
        }

        //强壮  增加护甲和血上限  增加护甲:3  增加血上限:300
        public class TalentStrong : IndividualItem
        {
            public float p_armor;
            public float p_hpAdd;

            public TalentStrong()
            {
                p_armor = 3;
                p_hpAdd = 300;
            }

            public TalentStrong(IndividualItem item)
            {
                TalentStrong talent = (TalentStrong)item;
                p_armor = talent.p_armor;
                p_hpAdd = talent.p_hpAdd;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentStrong talent = (TalentStrong)item;
                p_armor = talent.p_armor;
                p_hpAdd = talent.p_hpAdd;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Strong";
                }

                _description.p_levelDescription =
                    $"增加护甲和血上限";
                return _description;
            }
        }

        //反击  受到近战攻击有一定概率给伤害者造成伤害,对建筑无效  概率:15%  伤害:45
        public class TalentCounterattack : IndividualItem
        {
            public float p_chance;
            public float p_damage;

            public TalentCounterattack()
            {
                p_chance = 0.15f * 100; //15%
                p_damage = 45;
            }

            public TalentCounterattack(IndividualItem item)
            {
                TalentCounterattack talent = (TalentCounterattack)item;
                p_chance = talent.p_chance;
                p_damage = talent.p_damage;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentCounterattack talent = (TalentCounterattack)item;
                p_chance = talent.p_chance;
                p_damage = talent.p_damage;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Counterattack";
                }

                _description.p_levelDescription =
                    $"受到近战攻击有一定概率给伤害者造成伤害,对建筑无效";
                return _description;
            }
        }

        //反转  有一定概率将受到的伤害转成自身血量  概率:8%
        public class TalentReversal : IndividualItem
        {
            public float p_chance;

            public TalentReversal()
            {
                p_chance = 0.08f * 100; //8%
            }

            public TalentReversal(IndividualItem item)
            {
                TalentReversal talent = (TalentReversal)item;
                p_chance = talent.p_chance;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentReversal talent = (TalentReversal)item;
                p_chance = talent.p_chance;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Reversal";
                }

                _description.p_levelDescription =
                    $"有一定概率将受到的伤害转成自身血量";
                return _description;
            }
        }

        //恢复 增加恢复速度 回血增加:2.5/s
        public class TalentRejuvenation : IndividualItem
        {
            public float p_hpIncAdd;

            public TalentRejuvenation()
            {
                p_hpIncAdd = 2.5f * Time.fixedDeltaTime;
            }

            public TalentRejuvenation(IndividualItem item)
            {
                TalentRejuvenation rejuvenation = (TalentRejuvenation)item;
                p_hpIncAdd = rejuvenation.p_hpIncAdd;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentRejuvenation rejuvenation = (TalentRejuvenation)item;
                p_hpIncAdd = rejuvenation.p_hpIncAdd;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Rejuvenation";
                }

                _description.p_levelDescription =
                    $"增加恢复速度";
                return _description;
            }
        }

        //新生  英雄死亡后有3次立刻复活的机会
        public class TalentRebirth : IndividualItem
        {
            public int p_rebirthTime;

            public TalentRebirth()
            {
                p_rebirthTime = 3;
            }

            public TalentRebirth(IndividualItem item)
            {
                TalentRebirth rebirth = (TalentRebirth)item;
                p_rebirthTime = rebirth.p_rebirthTime;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentRebirth rebirth = (TalentRebirth)item;
                p_rebirthTime = rebirth.p_rebirthTime;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Rebirth";
                }

                _description.p_levelDescription =
                    $"英雄死亡后有3次立刻复活的机会";
                return _description;
            }
        }

        //护主  英雄的血量降低到阈值时大量增加英雄的护甲  血量：20%   护甲增加:10
        //持续时间:20s  冷却:60s
        public class TalentGuardianInstinct : IndividualItem
        {
            public float p_hpThreshold;
            public float p_armor;
            public float p_duration;
            public float p_interval;
            public float p_intervalCycle;

            public TalentGuardianInstinct()
            {
                p_hpThreshold = 0.2f; //20%
                p_armor = 10f;
                p_duration = 20;
                p_interval = 60;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
            }

            public TalentGuardianInstinct(IndividualItem item)
            {
                TalentGuardianInstinct talent = item as TalentGuardianInstinct;
                p_hpThreshold = talent.p_hpThreshold;
                p_armor = talent.p_armor;
                p_duration = talent.p_duration;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentGuardianInstinct talent = item as TalentGuardianInstinct;
                p_hpThreshold = talent.p_hpThreshold;
                p_armor = talent.p_armor;
                p_duration = talent.p_duration;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Guardian's Instinct";
                }

                _description.p_levelDescription =
                    $"英雄的血量降低到阈值时根据场上同类型友军的数量增加英雄的护甲";
                return _description;
            }
        }

        //敌对  英雄的血量降低到阈值时大量增加英雄的回血速度   血量：20%   回血增加:20
        //持续时间:10s  冷却:50s
        public class TalentHostileInstinct : IndividualItem
        {
            public float p_hpThreshold;
            public float p_hpInc;
            public float p_duration;
            public float p_interval;
            public float p_intervalCycle;

            public TalentHostileInstinct()
            {
                p_hpThreshold = 0.2f;
                p_hpInc = 20f * Time.fixedDeltaTime;
                p_duration = 10;
                p_interval = 50;
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
            }

            public TalentHostileInstinct(IndividualItem item)
            {
                TalentHostileInstinct talent = item as TalentHostileInstinct;
                p_hpThreshold = talent.p_hpThreshold;
                p_hpInc = talent.p_hpInc;
                p_duration = talent.p_duration;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentHostileInstinct talent = item as TalentHostileInstinct;
                p_hpThreshold = talent.p_hpThreshold;
                p_hpInc = talent.p_hpInc;
                p_duration = talent.p_duration;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Bloodlust";
                }

                _description.p_levelDescription =
                    $"英雄的血量降低到阈值时根据场上敌人的数量增加英雄的回血速度";
                return _description;
            }
        }

        //新的机会 生命值降低到10%时会立刻恢复到70%,如果英雄在血量高于10%时直接被击毙不会触发此效果  冷却: 180s
        public class TalentNewChance : IndividualItem
        {
            public float p_hpThreshold;
            public float p_hpRecover;
            public float p_interval;
            public float p_intervalCycle;

            public TalentNewChance()
            {
                p_hpThreshold = 0.1f; //10%
                p_hpRecover = 0.7f; //70%
                p_interval = 180; //180s
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
            }

            public TalentNewChance(IndividualItem item)
            {
                TalentNewChance talent = item as TalentNewChance;
                p_hpThreshold = talent.p_hpThreshold;
                p_hpRecover = talent.p_hpRecover;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentNewChance talent = item as TalentNewChance;
                p_hpThreshold = talent.p_hpThreshold;
                p_hpRecover = talent.p_hpRecover;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "New Chance";
                }

                _description.p_levelDescription =
                    $"生命值降低到10%时会立刻恢复到70%,如果英雄在血量高于10%时直接被击毙不会触发此效果";
                return _description;
            }
        }

        //逃跑  生命低于30%时,增加移动速度,生命值恢复到45%恢复速度  移动速度增加:50%
        public class TalentFlee : IndividualItem
        {
            public float p_hpThreshold;
            public float p_hpRecover; //防止hp在p_hpThreshold附近来回变化,设置一个大一点的值
            public float p_moveUp;

            public TalentFlee()
            {
                p_hpThreshold = 0.3f;
                p_hpRecover = 0.45f;//45% recover move speed
                p_moveUp = 1 + 0.5f; //50%
            }

            public TalentFlee(IndividualItem item)
            {
                TalentFlee talent = item as TalentFlee;
                p_hpThreshold = talent.p_hpThreshold;
                p_hpRecover = talent.p_hpRecover;
                p_moveUp = talent.p_moveUp;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentFlee talent = item as TalentFlee;
                p_hpThreshold = talent.p_hpThreshold;
                p_hpRecover = talent.p_hpRecover;
                p_moveUp = talent.p_moveUp;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Flee";
                }

                _description.p_levelDescription =
                    $"生命低于30%时,增加移动速度";
                return _description;
            }
        }

        //锁定  英雄生命值低于10%时一段时间受到任何伤害不会造成血量减少，持续时间结束后这段时间内受到的伤害的按照一定比率回血,如果英雄在血量高于1%时直接被击毙不会触发此效果
        //持续时间:10s  冷却:200s 回血比例：15%
        public class TalentLastBreath : IndividualItem
        {
            public float p_hpThreshold;
            public float p_duration;
            public float p_durationCycle;
            public float p_interval;
            public float p_intervalCycle;
            public float p_damageToHpAdd;

            public TalentLastBreath()
            {
                p_hpThreshold = 0.1f; //10%
                p_duration = 10; //10s
                p_durationCycle = Utils.CountOfFixUpdate(p_duration);
                p_interval = 200; //200s
                p_intervalCycle = Utils.CountOfFixUpdate(p_interval);
                p_damageToHpAdd = 0.15f; //15% damage
            }

            public TalentLastBreath(IndividualItem item)
            {
                TalentLastBreath talent = item as TalentLastBreath;
                p_hpThreshold = talent.p_hpThreshold;
                p_duration = talent.p_duration;
                p_durationCycle = talent.p_durationCycle;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
                p_damageToHpAdd = talent.p_damageToHpAdd;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentLastBreath talent = item as TalentLastBreath;
                p_hpThreshold = talent.p_hpThreshold;
                p_duration = talent.p_duration;
                p_durationCycle = talent.p_durationCycle;
                p_interval = talent.p_interval;
                p_intervalCycle = talent.p_intervalCycle;
                p_damageToHpAdd = talent.p_damageToHpAdd;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Flee";
                }

                _description.p_levelDescription =
                    $"英雄生命值低于1%时一段时间受到任何伤害不会造成血量减少，持续时间结束后这段时间内受到的伤害的按照一定比率回血,如果英雄在血量高于1%时直接被击毙不会触发此效果";
                return _description;
            }
        }

        //急速  增加移动速度  增加移速:12%
        public class TalentFleetFoot : IndividualItem
        {
            public float p_moveUp;

            public TalentFleetFoot()
            {
                p_moveUp = 1 + 0.15f;//15%
            }

            public TalentFleetFoot(IndividualItem item)
            {
                TalentFleetFoot talent = item as TalentFleetFoot;
                p_moveUp = talent.p_moveUp;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentFleetFoot talent = item as TalentFleetFoot;
                p_moveUp = talent.p_moveUp;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Fleet Foot";
                }

                _description.p_levelDescription =
                    $"增加移动速度";
                return _description;
            }
        }

        //富饶 增加金矿的产出和金矿卖出的价格  金矿产出增加:20%  金矿卖出价格增加:50%
        public class TalentProsperity : IndividualItem
        {
            public float p_glodProdInc;
            public float p_glodMineSellInc;

            public TalentProsperity()
            {
                p_glodProdInc = 1 + 0.2f;//20%
                p_glodMineSellInc = 1 + 0.5f;//50%
            }

            public TalentProsperity(IndividualItem item)
            {
                TalentProsperity talent = item as TalentProsperity;
                p_glodProdInc = talent.p_glodProdInc;
                p_glodMineSellInc = talent.p_glodMineSellInc;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentProsperity talent = item as TalentProsperity;
                p_glodProdInc = talent.p_glodProdInc;
                p_glodMineSellInc = talent.p_glodMineSellInc;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Prosperity";
                }

                _description.p_levelDescription =
                    $"增加金矿的产出和金矿卖出的价格";
                return _description;
            }
        }

        //奖励  增加占领每座眼球矿的收获  增加占领每座眼球矿的收获  眼球收获增加:35%
        public class TalentBounty : IndividualItem
        {
            public float p_eyeInc;

            public TalentBounty()
            {
                p_eyeInc = 1 + 0.35f;
            }

            public TalentBounty(IndividualItem item)
            {
                TalentBounty talent = item as TalentBounty;
                p_eyeInc = talent.p_eyeInc;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentBounty talent = item as TalentBounty;
                p_eyeInc = talent.p_eyeInc;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Bounty";
                }

                _description.p_levelDescription =
                    $"增加占领每座眼球矿的收获";
                return _description;
            }
        }

        //炼金大师  增加药水持续时间  增加时间:30%
        public class TalentAlchemicalMastery : IndividualItem
        {
            public float p_potionLastInc;

            public TalentAlchemicalMastery()
            {
                p_potionLastInc = 1 + 0.3f; //30%
            }

            public TalentAlchemicalMastery(IndividualItem item)
            {
                TalentAlchemicalMastery talent = item as TalentAlchemicalMastery;
                p_potionLastInc = talent.p_potionLastInc;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentAlchemicalMastery talent = item as TalentAlchemicalMastery;
                p_potionLastInc = talent.p_potionLastInc;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Alchemical Mastery";
                }

                _description.p_levelDescription =
                    $"增加药水持续时间";
                return _description;
            }
        }

        //收集者  增加能量采集石采集范围  采集范围增加:50%
        public class TalentCollector : IndividualItem
        {
            public float p_collectRangeInc;

            public TalentCollector()
            {
                p_collectRangeInc = 1 + 0.5f; //50
            }

            public TalentCollector(IndividualItem item)
            {
                TalentCollector talent = item as TalentCollector;
                p_collectRangeInc = talent.p_collectRangeInc;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentCollector talent = item as TalentCollector;
                p_collectRangeInc = talent.p_collectRangeInc;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Collector";
                }

                _description.p_levelDescription =
                    $"增加能量采集石采集范围";
                return _description;
            }
        }

        //经验  一定概率从一个能量石获得双倍的能量 概率:15%
        public class TalentExperience : IndividualItem
        {
            public float p_change;

            public TalentExperience()
            {
                p_change = 0.15f; //15%
            }

            public TalentExperience(IndividualItem item)
            {
                TalentExperience talent = item as TalentExperience;
                p_change = talent.p_change;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentExperience talent = item as TalentExperience;
                p_change = talent.p_change;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Experience";
                }

                _description.p_levelDescription =
                    $"一定概率从一个能量石获得双倍的能量";
                return _description;
            }
        }

        //快速重生  减少英雄的复活时间  复活时间减少:25%
        public class TalentSwiftRebirth : IndividualItem
        {
            public float p_rebirthReduce;

            public TalentSwiftRebirth()
            {
                p_rebirthReduce = 1 - 0.25f; //25%
            }

            public TalentSwiftRebirth(IndividualItem item)
            {
                TalentSwiftRebirth talent = item as TalentSwiftRebirth;
                p_rebirthReduce = talent.p_rebirthReduce;
            }

            public override void ReInit(IndividualItem item)
            {

                TalentSwiftRebirth talent = item as TalentSwiftRebirth;
                p_rebirthReduce = talent.p_rebirthReduce;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Swift Rebirth";
                }

                _description.p_levelDescription =
                    $"减少英雄的复活时间";
                return _description;
            }
        }

        //赌徒  可以在抽卡阶段多锁定两张卡牌
        public class TalentGambler: IndividualItem
        {
            public int p_lockCardAdd;

            public TalentGambler()
            {
                p_lockCardAdd = 2;
            }

            public TalentGambler(IndividualItem item)
            {
                TalentGambler talent = item as TalentGambler;
                p_lockCardAdd = talent.p_lockCardAdd;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentGambler talent = item as TalentGambler;
                p_lockCardAdd = talent.p_lockCardAdd;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Gambler";
                }

                _description.p_levelDescription =
                    $"减少英雄的复活时间";
                return _description;
            }
        }

        //欺骗  有一定概率在抽牌阶段多抽一张卡牌  概率:15%
        public class TalentDeception : IndividualItem
        {
            public float p_drawOneMoreChance;

            public TalentDeception()
            {
                p_drawOneMoreChance = 0.15f;
            }

            public TalentDeception(IndividualItem item)
            {
                TalentDeception talent = item as TalentDeception;
                p_drawOneMoreChance = talent.p_drawOneMoreChance;
            }

            public override void ReInit(IndividualItem item)
            {
                TalentDeception talent = item as TalentDeception;
                p_drawOneMoreChance = talent.p_drawOneMoreChance;
            }

            public override bool IsEnabled()
            {
                return true;
            }

            public override bool Improve(object value)
            {
                GameLogger.LogError($"Hero Talent not support improve");
                return false;
            }

            public override IndividualItemDescription GetDescription()
            {
                if (_description == null)
                {
                    _description = new IndividualItemDescription();
                    _description.p_name = "Deception";
                }

                _description.p_levelDescription =
                    $"有一定概率在抽牌阶段多抽一张卡牌";
                return _description;
            }
        }
#endregion

        public HeroGenericIndividualData(WarFieldElements.WarEleType type = WE.WarEleType.SOLDIER) : base(type)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            //hero generic skills
            _individualItems[(int)IndividualDataType.INVINCIBLE] = new SkillInvincible();
            _individualItems[(int)IndividualDataType.HASTE] = new SkillHaste();

            //hero talents
            _individualItems[(int)IndividualDataType.QUICKATTACK] = new TalentQuickAttack();
            _individualItems[(int)IndividualDataType.HEAVYATTACK] = new TalentHeavyAttack();
            _individualItems[(int)IndividualDataType.DOUBLEDAMAGE] = new TalentDoubleDamage();
            _individualItems[(int)IndividualDataType.ALLIEDATTACKBOOST] = new TalentAlliedAttackBoost();
            _individualItems[(int)IndividualDataType.ALLIEDATTACKSPEEDBOOST] = new TalentAlliedAttackSpeedBoost();
            _individualItems[(int)IndividualDataType.ALLIEDSKILLBOOST] = new TalentAlliedSkillBoost();
            _individualItems[(int)IndividualDataType.SUICIDESQUAD] = new TalentSuicideSquad();
            _individualItems[(int)IndividualDataType.SELFDESTRUCT] = new TalentSelfDestruct();

            _individualItems[(int)IndividualDataType.STRONG] = new TalentStrong();
            _individualItems[(int)IndividualDataType.COUNTERATTACK] = new TalentCounterattack();
            _individualItems[(int)IndividualDataType.REVERSAL] = new TalentReversal();
            _individualItems[(int)IndividualDataType.REJUVENATION] = new TalentRejuvenation();
            _individualItems[(int)IndividualDataType.REBIRTH] = new TalentRebirth();
            _individualItems[(int)IndividualDataType.GUARDIANINSTINCT] = new TalentGuardianInstinct();
            _individualItems[(int)IndividualDataType.HOSTILEINSTINCT] = new TalentHostileInstinct();
            _individualItems[(int)IndividualDataType.NEWCHANCE] = new TalentNewChance();
            _individualItems[(int)IndividualDataType.FLEE] = new TalentFlee();
            _individualItems[(int)IndividualDataType.LASTBREATH] = new TalentLastBreath();
            _individualItems[(int)IndividualDataType.FLEETFOOT] = new TalentFleetFoot();

            _individualItems[(int)IndividualDataType.PROSPERITY] = new TalentProsperity();
            _individualItems[(int)IndividualDataType.BOUNTY] = new TalentBounty();
            _individualItems[(int)IndividualDataType.ALCHEMICALMASTERY] = new TalentAlchemicalMastery();
            _individualItems[(int)IndividualDataType.COLLECTOR] = new TalentCollector();
            _individualItems[(int)IndividualDataType.EXPERIENCE] = new TalentExperience();
            _individualItems[(int)IndividualDataType.SWIFTREBIRTH] = new TalentSwiftRebirth();
            _individualItems[(int)IndividualDataType.GAMBLER] = new TalentGambler();
            _individualItems[(int)IndividualDataType.DECEPTION] = new TalentDeception();
        }

        public HeroGenericIndividualData(IndividualData data) : base(data)
        {
            _individualItems = new IndividualItem[(int)IndividualDataType.MAX];
            //hero generic skills
            _individualItems[(int)IndividualDataType.INVINCIBLE] = new SkillInvincible(data.gs_individualItems[(int)IndividualDataType.INVINCIBLE]);
            _individualItems[(int)IndividualDataType.HASTE] = new SkillHaste(data.gs_individualItems[(int)IndividualDataType.HASTE]);

            //hero talents
            _individualItems[(int)IndividualDataType.QUICKATTACK] = new TalentQuickAttack(data.gs_individualItems[(int)IndividualDataType.QUICKATTACK]);
            _individualItems[(int)IndividualDataType.HEAVYATTACK] = new TalentHeavyAttack(data.gs_individualItems[(int)IndividualDataType.HEAVYATTACK]);
            _individualItems[(int)IndividualDataType.DOUBLEDAMAGE] = new TalentDoubleDamage(data.gs_individualItems[(int)IndividualDataType.DOUBLEDAMAGE]);
            _individualItems[(int)IndividualDataType.ALLIEDATTACKBOOST] = new TalentAlliedAttackBoost(data.gs_individualItems[(int)IndividualDataType.ALLIEDATTACKBOOST]);
            _individualItems[(int)IndividualDataType.ALLIEDATTACKSPEEDBOOST] = new TalentAlliedAttackSpeedBoost(data.gs_individualItems[(int)IndividualDataType.ALLIEDATTACKSPEEDBOOST]);
            _individualItems[(int)IndividualDataType.ALLIEDSKILLBOOST] = new TalentAlliedSkillBoost(data.gs_individualItems[(int)IndividualDataType.ALLIEDSKILLBOOST]);
            _individualItems[(int)IndividualDataType.SUICIDESQUAD] = new TalentSuicideSquad(data.gs_individualItems[(int)IndividualDataType.SUICIDESQUAD]);
            _individualItems[(int)IndividualDataType.SELFDESTRUCT] = new TalentSelfDestruct(data.gs_individualItems[(int)IndividualDataType.SELFDESTRUCT]);

            _individualItems[(int)IndividualDataType.STRONG] = new TalentStrong(data.gs_individualItems[(int)IndividualDataType.STRONG]);
            _individualItems[(int)IndividualDataType.COUNTERATTACK] = new TalentCounterattack(data.gs_individualItems[(int)IndividualDataType.COUNTERATTACK]);
            _individualItems[(int)IndividualDataType.REVERSAL] = new TalentReversal(data.gs_individualItems[(int)IndividualDataType.REVERSAL]);
            _individualItems[(int)IndividualDataType.REJUVENATION] = new TalentRejuvenation(data.gs_individualItems[(int)IndividualDataType.REJUVENATION]);
            _individualItems[(int)IndividualDataType.REBIRTH] = new TalentRebirth(data.gs_individualItems[(int)IndividualDataType.REBIRTH]);
            _individualItems[(int)IndividualDataType.GUARDIANINSTINCT] = new TalentGuardianInstinct(data.gs_individualItems[(int)IndividualDataType.GUARDIANINSTINCT]);
            _individualItems[(int)IndividualDataType.HOSTILEINSTINCT] = new TalentHostileInstinct(data.gs_individualItems[(int)IndividualDataType.HOSTILEINSTINCT]);
            _individualItems[(int)IndividualDataType.NEWCHANCE] = new TalentNewChance(data.gs_individualItems[(int)IndividualDataType.NEWCHANCE]);
            _individualItems[(int)IndividualDataType.FLEE] = new TalentFlee(data.gs_individualItems[(int)IndividualDataType.FLEE]);
            _individualItems[(int)IndividualDataType.LASTBREATH] = new TalentLastBreath(data.gs_individualItems[(int)IndividualDataType.LASTBREATH]);
            _individualItems[(int)IndividualDataType.FLEETFOOT] = new TalentFleetFoot(data.gs_individualItems[(int)IndividualDataType.FLEETFOOT]);

            _individualItems[(int)IndividualDataType.PROSPERITY] = new TalentProsperity(data.gs_individualItems[(int)IndividualDataType.PROSPERITY]);
            _individualItems[(int)IndividualDataType.BOUNTY] = new TalentBounty(data.gs_individualItems[(int)IndividualDataType.BOUNTY]);
            _individualItems[(int)IndividualDataType.ALCHEMICALMASTERY] = new TalentAlchemicalMastery(data.gs_individualItems[(int)IndividualDataType.ALCHEMICALMASTERY]);
            _individualItems[(int)IndividualDataType.COLLECTOR] = new TalentCollector(data.gs_individualItems[(int)IndividualDataType.COLLECTOR]);
            _individualItems[(int)IndividualDataType.EXPERIENCE] = new TalentExperience(data.gs_individualItems[(int)IndividualDataType.EXPERIENCE]);
            _individualItems[(int)IndividualDataType.SWIFTREBIRTH] = new TalentSwiftRebirth(data.gs_individualItems[(int)IndividualDataType.SWIFTREBIRTH]);
            _individualItems[(int)IndividualDataType.GAMBLER] = new TalentGambler(data.gs_individualItems[(int)IndividualDataType.GAMBLER]);
            _individualItems[(int)IndividualDataType.DECEPTION] = new TalentDeception(data.gs_individualItems[(int)IndividualDataType.DECEPTION]);
        }

        public override void ReInitIndividualData(IndividualData data)
        {
            base.ReInitIndividualData(data);
            _individualItems[(int)IndividualDataType.INVINCIBLE].ReInit(data.gs_individualItems[(int)IndividualDataType.INVINCIBLE]);
            _individualItems[(int)IndividualDataType.HASTE].ReInit(data.gs_individualItems[(int)IndividualDataType.HASTE]);

            //hero talents
            _individualItems[(int)IndividualDataType.QUICKATTACK].ReInit(data.gs_individualItems[(int)IndividualDataType.QUICKATTACK]);
            _individualItems[(int)IndividualDataType.HEAVYATTACK].ReInit(data.gs_individualItems[(int)IndividualDataType.HEAVYATTACK]);
            _individualItems[(int)IndividualDataType.DOUBLEDAMAGE].ReInit(data.gs_individualItems[(int)IndividualDataType.DOUBLEDAMAGE]);
            _individualItems[(int)IndividualDataType.ALLIEDATTACKBOOST].ReInit(data.gs_individualItems[(int)IndividualDataType.ALLIEDATTACKBOOST]);
            _individualItems[(int)IndividualDataType.ALLIEDATTACKSPEEDBOOST].ReInit(data.gs_individualItems[(int)IndividualDataType.ALLIEDATTACKSPEEDBOOST]);
            _individualItems[(int)IndividualDataType.ALLIEDSKILLBOOST].ReInit(data.gs_individualItems[(int)IndividualDataType.ALLIEDSKILLBOOST]);
            _individualItems[(int)IndividualDataType.SUICIDESQUAD].ReInit(data.gs_individualItems[(int)IndividualDataType.SUICIDESQUAD]);
            _individualItems[(int)IndividualDataType.SELFDESTRUCT].ReInit(data.gs_individualItems[(int)IndividualDataType.SELFDESTRUCT]);

            _individualItems[(int)IndividualDataType.STRONG].ReInit(data.gs_individualItems[(int)IndividualDataType.STRONG]);
            _individualItems[(int)IndividualDataType.COUNTERATTACK].ReInit(data.gs_individualItems[(int)IndividualDataType.COUNTERATTACK]);
            _individualItems[(int)IndividualDataType.REVERSAL].ReInit(data.gs_individualItems[(int)IndividualDataType.REVERSAL]);
            _individualItems[(int)IndividualDataType.REJUVENATION].ReInit(data.gs_individualItems[(int)IndividualDataType.REJUVENATION]);
            _individualItems[(int)IndividualDataType.REBIRTH].ReInit(data.gs_individualItems[(int)IndividualDataType.REBIRTH]);
            _individualItems[(int)IndividualDataType.GUARDIANINSTINCT].ReInit(data.gs_individualItems[(int)IndividualDataType.GUARDIANINSTINCT]);
            _individualItems[(int)IndividualDataType.HOSTILEINSTINCT].ReInit(data.gs_individualItems[(int)IndividualDataType.HOSTILEINSTINCT]);
            _individualItems[(int)IndividualDataType.NEWCHANCE].ReInit(data.gs_individualItems[(int)IndividualDataType.NEWCHANCE]);
            _individualItems[(int)IndividualDataType.FLEE].ReInit(data.gs_individualItems[(int)IndividualDataType.FLEE]);
            _individualItems[(int)IndividualDataType.LASTBREATH].ReInit(data.gs_individualItems[(int)IndividualDataType.LASTBREATH]);
            _individualItems[(int)IndividualDataType.FLEETFOOT].ReInit(data.gs_individualItems[(int)IndividualDataType.FLEETFOOT]);

            _individualItems[(int)IndividualDataType.PROSPERITY].ReInit(data.gs_individualItems[(int)IndividualDataType.PROSPERITY]);
            _individualItems[(int)IndividualDataType.BOUNTY].ReInit(data.gs_individualItems[(int)IndividualDataType.BOUNTY]);
            _individualItems[(int)IndividualDataType.ALCHEMICALMASTERY].ReInit(data.gs_individualItems[(int)IndividualDataType.ALCHEMICALMASTERY]);
            _individualItems[(int)IndividualDataType.COLLECTOR].ReInit(data.gs_individualItems[(int)IndividualDataType.COLLECTOR]);
            _individualItems[(int)IndividualDataType.EXPERIENCE].ReInit(data.gs_individualItems[(int)IndividualDataType.EXPERIENCE]);
            _individualItems[(int)IndividualDataType.SWIFTREBIRTH].ReInit(data.gs_individualItems[(int)IndividualDataType.SWIFTREBIRTH]);
            _individualItems[(int)IndividualDataType.GAMBLER].ReInit(data.gs_individualItems[(int)IndividualDataType.GAMBLER]);
            _individualItems[(int)IndividualDataType.DECEPTION].ReInit(data.gs_individualItems[(int)IndividualDataType.DECEPTION]);
        }

        public override bool Improve(object value)
        {
            if (value is ValueTuple<IndividualDataType, object> tupple)
            {
                IndividualDataType target = tupple.Item1;
                switch (target)
                {
                    case IndividualDataType.INVINCIBLE:
                    case IndividualDataType.HASTE:
                        return _individualItems[(int)target].Improve(tupple.Item2);
                    default:  //hero talent not support improve
                        GameLogger.LogError($"Try to imporve unsupport type {target}");
                        break;
                }
            }

            return false;
        }
    }
}

