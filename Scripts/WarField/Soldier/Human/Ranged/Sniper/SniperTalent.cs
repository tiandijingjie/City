using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sniper;

namespace WarField
{
    using SKD = SkillDefines;
    
    //普通攻击有7%概率暴击造成0.8倍伤害
    public class SniperTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private SniperIndividualData.Talent _oriAttribute;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            
            _triggerType = new[] { SKD.SkillTriggerType.ATTACKTRIGGER};
            for (int i = 0; i < _triggerType.Length; i++)
                _soldier.RegisterTalent(this, _triggerType[i]);
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new SniperIndividualData.Talent((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.TALENT]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.TALENT]);
            }
            
            return true;
        }

        protected override bool OnTalentDoAttackPre(float hit, object rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            int chance = Utils.GetRandomInt();
            if (chance <= _oriAttribute.p_chance)
            {
                damage = hit * _oriAttribute.p_damgeUp;
            }
            else
                damage = hit;
            return true;
        }

#endregion
    }
}

