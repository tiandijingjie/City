using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //收集者  增加能量采集石采集范围  采集范围增加:50%
    public class HeroCollectorTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentCollector _curTalent = null;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            // _triggerType = new[] { };
            // for (int i = 0; i < _triggerType.Length; i++)
            //     _soldier.RegisterTalent(this, _triggerType[i]);  //talent need to do register during awake by itself
        }
#endregion

#region public functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentCollector)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.COLLECTOR];
            _name = _curTalent.GetDescription().p_name;

            _soldier.AddSpecificStateChange("stoneSearchRadius", _curTalent.p_collectRangeInc, GD.CalDeltaType.MUL, out float oriValue);
            return true;
        }
#endregion

#region private functions

#endregion
    }
}
