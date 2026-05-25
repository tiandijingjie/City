using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Cannoneer;

namespace WarField
{
    using SKD = SkillDefines;
    
    public class CannoneerTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private CannoneerIndividualData.Talent _oriAttribute;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            
            _triggerType = new[] { SKD.SkillTriggerType.ACTIVETRIGGER};
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
                _oriAttribute = new CannoneerIndividualData.Talent((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.TALENT]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as CannoneerIndividualData)
                    .gs_individualItems[(int)CannoneerIndividualData.IndividualDataType.TALENT]);
            }
            ((Cannoneer)_soldier).SetShellAttackRange(_oriAttribute.p_range);
            ((Cannoneer)_soldier).SetDamageDown(_oriAttribute.p_damageDown);
            return true;
        }
#endregion
    }
}

