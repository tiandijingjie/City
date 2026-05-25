using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Swordsman;

namespace WarField
{
    using WE = WarFieldElements;
    using SKD = SkillDefines;
    using BFD = BuffDefines;

    public class SwordsmanStunSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private SwordsmanIndividualData.SkillStun _oriAttribute = null;
        private bool _triggerStun = false;
        private bool _targetHero = false;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SwordsmanIndividualData.IndividualDataType.STUN; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.ATTACKTRIGGER };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if(_oriAttribute == null)
                _oriAttribute = new SwordsmanIndividualData.SkillStun((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.STUN]);
            else
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.STUN]);
            _name = _oriAttribute.GetDescription().p_name;
            _triggerStun = false;
            _targetHero = false;
            return true;
        }

        protected override bool OnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            damage = hit;
            if (rivalType == WE.WarEleType.SOLDIER)
            {
                Soldier soldier = (Soldier)rivalScript;
                if (soldier.gs_sdLevel != SoldierDefines.SoldierLevel.BOSSLEVEL)
                {
                    int chance = Utils.GetRandomInt();
                    if (chance < _oriAttribute.p_chance)
                    {
                        _triggerStun = true;
                        damage += _oriAttribute.p_stunDamage;
                    }
                }
                else
                {
                    if (_oriAttribute.p_heroEnable == true)
                    {
                        int chance = Utils.GetRandomInt();
                        if (chance < _oriAttribute.p_heroChance)
                        {
                            _triggerStun = true;
                            _targetHero = true;
                            damage += _oriAttribute.p_heroStunDamage;
                        }
                    }
                }
            }
            return true;
        }

        protected override void OnSkillDoAttackPost(float hit, bool isDead, object rivalScript, WarFieldElements.WarEleType rivalType)
        {
            if(isDead == true || rivalType != WE.WarEleType.SOLDIER)
                return;

            if (_triggerStun == true)
            {
                _triggerStun = false;
                if(_targetHero == false)
                    ((Soldier)rivalScript).BeAffectedByBuff(BFD.SoldierBuffType.STUN, in _oriAttribute.p_stunDuration);
                else
                {
                    ((Soldier)rivalScript).BeAffectedByBuff(BFD.SoldierBuffType.STUN, in _oriAttribute.p_heroStunDuration);
                    _targetHero = false;
                }
            }
        }

#endregion
    }
}

