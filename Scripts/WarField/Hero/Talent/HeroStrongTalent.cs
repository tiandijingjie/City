using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //强壮  增加护甲和血上限  增加护甲:3  增加血上限:300
    public class HeroStrongTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentStrong _curTalent = null;
        protected object _armorChangeIndex = null, _hpChangeIndex = null;

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
                _curTalent = (HeroGenericIndividualData.TalentStrong)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.STRONG];
            _name = _curTalent.GetDescription().p_name;

            _armorChangeIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.PHYARMOR, _curTalent.p_armor, GD.CalDeltaType.ADD, out
                float oriValue);
            _hpChangeIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.HEALTH, _curTalent.p_hpAdd, GD.CalDeltaType.ADD, out oriValue);
            return true;
        }
#endregion
    }
}

