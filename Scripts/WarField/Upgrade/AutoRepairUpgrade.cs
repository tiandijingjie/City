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

    //自动修复
    public class AutoRepairUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.AUTOREPAIR; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("hpInc");
            if(value < 0)
                return false;
            value *= p_node.p_curLevel;

            for (int i = 1; i < (int)HumanDefines.FortressType.MAX; i++)
                WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.FORTRESS, i, "hpInc", value, GD
                    .CalDeltaType.ADD);
            for (int i = 1; i < (int)HumanDefines.BarrackType.MAX; i++)
                WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, i, "hpInc", value, GD
                    .CalDeltaType.ADD);
            for (int i = 1; i < (int)HumanDefines.DefenceType.MAX; i++)
                WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i, "hpInc", value, GD
                    .CalDeltaType.ADD);

            return true;
        }

#endregion
    }
}
