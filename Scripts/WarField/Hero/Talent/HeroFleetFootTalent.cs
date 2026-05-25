using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;

    //急速  增加移动速度  增加移速:12%
    public class HeroFleetFootTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentFleetFoot _curTalent = null;
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
                _curTalent = (HeroGenericIndividualData.TalentFleetFoot)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.FLEETFOOT];
            _name = _curTalent.GetDescription().p_name;

            _stateChangeIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.MOVESPEED, _curTalent.p_moveUp, GD.CalDeltaType.MUL, out
                float oriValue);
            return true;
        }
#endregion
    }
}
