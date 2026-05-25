using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using WBD = WarBuildingDefines;

    //富饶的洞穴
    public class AddCaveProduceUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.ADDCAVEPRODUCE; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("produceAdd");
            if(value < 0)
                return false;
            value *= p_node.p_curLevel;
            value = 1 + value;
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.LOWLEVEL, "caveProduceAdd", value, GD.CalDeltaType.ADD);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.MIDLEVEL, "caveProduceAdd", value, GD.CalDeltaType.ADD);
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, WE.RaceType.Neutral, WBD.BuildingMode.GOLDMINE, (int)
                NeutralDefines.GoldMineType.HIGHLEVL, "caveProduceAdd", value, GD.CalDeltaType.ADD);
            return true;
        }

#endregion
    }
}
