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

    //解锁远程英雄
    public class UnlockRangedHeroUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.UNLOCKRANGEDHERO; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            return SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.HeroType.RANGEDHERO, true);
        }

#endregion
    }
}
