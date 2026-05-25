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

    //抢救遗迹
    public class DestroyGetFeeBack : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.DESTROYGETFEEBACK; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("goldBack");
            if(value < 0)
                return false;

            for (int i = 1; i < (int)HumanDefines.DefenceType.MAX; i++)
            {
                BuildingConf conf = WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i);
                if(conf == null)
                    continue;
                WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i, "destroyFee",
                    value * conf.gs_price, GD.CalDeltaType.ADD);
            }
            return true;
        }

#endregion
    }
}
