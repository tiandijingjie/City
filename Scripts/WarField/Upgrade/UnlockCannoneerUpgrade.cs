using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //解锁火炮手
    public class UnlockCannoneerUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.UNLOCKCANNONEER; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            return SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.CANNONEER, false);
        }

#endregion
    }
}
