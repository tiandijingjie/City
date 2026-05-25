using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //快速攻击   增加攻击速度  增加攻速:15%
    public class HeroQuickAttackTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentQuickAttack _curTalent = null;
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
                _curTalent = (HeroGenericIndividualData.TalentQuickAttack)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.QUICKATTACK];
            _name = _curTalent.GetDescription().p_name;

            _stateChangeIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.ATTACKSPEED, _curTalent.p_attakSpeedUp, GD.CalDeltaType.MUL, out
                float oriValue);
            return true;
        }
#endregion
    }
}

