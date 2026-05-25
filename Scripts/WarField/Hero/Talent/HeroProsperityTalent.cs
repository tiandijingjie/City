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

    //富饶 增加金矿的产出和金矿卖出的价格  金矿产出增加:20%  金矿卖出价格增加:50%
    public class HeroProsperityTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentProsperity _curTalent = null;
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
                _curTalent = (HeroGenericIndividualData.TalentProsperity)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.PROSPERITY];
            _name = _curTalent.GetDescription().p_name;

            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.LOWLEVEL, "goldAddPerSec", _curTalent.p_glodProdInc, GD.CalDeltaType.MUL);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.MIDLEVEL, "goldAddPerSec", _curTalent.p_glodProdInc, GD.CalDeltaType.MUL);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.HIGHLEVL, "goldAddPerSec", _curTalent.p_glodProdInc, GD.CalDeltaType.MUL);

            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.LOWLEVEL, "sellPrice", _curTalent.p_glodMineSellInc, GD.CalDeltaType.MUL);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.MIDLEVEL, "sellPrice", _curTalent.p_glodMineSellInc, GD.CalDeltaType.MUL);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMOTHERSOURCE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.HIGHLEVL, "sellPrice", _curTalent.p_glodMineSellInc, GD.CalDeltaType.MUL);
            return true;
        }


#endregion

#region private functions

#endregion
    }
}
