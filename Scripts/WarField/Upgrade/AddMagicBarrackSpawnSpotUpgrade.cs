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

    //更多魔法士兵
    public class AddMagicBarrackSpawnSpotUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.ADDMAGICBARRACKSPAWNSPOT; }
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
            WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)HumanDefines
                .BarrackType.MAGIC, "spawnSpotNum", value, GD.CalDeltaType.ADD);
            return true;
        }

#endregion
    }
}
