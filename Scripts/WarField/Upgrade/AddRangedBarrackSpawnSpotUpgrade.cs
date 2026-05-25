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

    //更多远程士兵
    public class AddRangedBarrackSpawnSpotUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.ADDRANGEDBARRACKSPAWNSPOT; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("moreSpot");
            if(value < 0)
                return false;
            value *= p_node.p_curLevel;
            return WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)HumanDefines
                .BarrackType.RANGED, "spawnSpotNum", value, GD.CalDeltaType.ADD);
        }

#endregion
    }
}
