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

    //解锁诅咒塔
    public class UnlockFrostTowerUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.UNLOCKFROSTTOWER; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            return WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                HumanDefines.DefenceType.FROSTTOWER, "isUlocked", 0, GD.CalDeltaType.MIN);
        }

#endregion
    }
}
