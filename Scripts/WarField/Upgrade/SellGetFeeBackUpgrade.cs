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

    //全部回收
    public class SellGetFeeBackUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.SELLGETFEEBACK; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            for (int i = 1; i < (int)HumanDefines.DefenceType.MAX; i++)
            {
                DefenceConf conf = (DefenceConf)WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i);
                if(conf == null)
                    continue;
                float sellPrice =  conf.gs_price - conf.gs_sellPrice;
                WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i, "sellPrice",
                    sellPrice, GD.CalDeltaType.ADD);
            }
            return true;
        }

#endregion
    }
}
