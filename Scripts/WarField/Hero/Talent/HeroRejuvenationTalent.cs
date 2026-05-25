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

    //恢复 增加恢复速度 回血增加:2.5/s
    public class HeroRejuvenationTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentRejuvenation _curTalent = null;
        private object _hpIncIndexer = null;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            // _triggerType = new[] { SKD.SkillTriggerType.BEATTACKTRIGGER };
            // for (int i = 0; i < _triggerType.Length; i++)
            //     _soldier.RegisterTalent(this, _triggerType[i]);  //talent need to do register during awake by itself
        }

#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentRejuvenation)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.REJUVENATION];
            _name = _curTalent.GetDescription().p_name;
            _hpIncIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.HPINC, _curTalent.p_hpIncAdd, GD.CalDeltaType.ADD, out
                float oriValue);
            return true;
        }

#endregion
    }
}
