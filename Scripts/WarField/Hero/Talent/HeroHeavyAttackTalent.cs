using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //重击 增加攻击伤害 伤害增加15%
    public class HeroHeavyAttackTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentHeavyAttack _curTalent = null;
        protected object _stateChangeIndex = null;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            //_triggerType = new[];
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentHeavyAttack)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.HEAVYATTACK];
            _name = _curTalent.GetDescription().p_name;

            _stateChangeIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.DAMAGE, _curTalent.p_damageUp, GD.CalDeltaType.MUL, out
                float oriValue);
            return true;
        }
#endregion
    }
}

