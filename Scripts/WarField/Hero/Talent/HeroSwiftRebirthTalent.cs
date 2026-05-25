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

    //快速重生  减少英雄的复活时间  复活时间减少:25%
    public class HeroSwiftRebirthTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentSwiftRebirth _curTalent = null;
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
                _curTalent = (HeroGenericIndividualData.TalentSwiftRebirth)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.SWIFTREBIRTH];
            _name = _curTalent.GetDescription().p_name;

            _soldier.AddStateChange(SD.StateSoldierEffectType.SPAWNSPEED, _curTalent.p_rebirthReduce, GD.CalDeltaType.MUL, out float orivalue);
            return true;
        }
#endregion

#region private functions

#endregion
    }
}
