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
    using WBD = WarBuildingDefines;

    //奖励  增加占领每座眼球矿的收获  增加占领每座眼球矿的收获  眼球收获增加:35%
    public class HeroBountyTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentBounty _curTalent = null;
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
                _curTalent = (HeroGenericIndividualData.TalentBounty)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.BOUNTY];
            _name = _curTalent.GetDescription().p_name;

            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GEMMINE, (int)
                NeutralDefines.GemMineType.LOWLEVEL, "gemInMine", _curTalent.p_eyeInc, GD.CalDeltaType.MUL);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GEMMINE, (int)
                NeutralDefines.GemMineType.MIDLEVEL, "gemInMine", _curTalent.p_eyeInc, GD.CalDeltaType.MUL);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GEMMINE, (int)
                NeutralDefines.GemMineType.HIGHLEVL, "gemInMine", _curTalent.p_eyeInc, GD.CalDeltaType.MUL);

            return true;
        }


#endregion

#region private functions

#endregion
    }
}
