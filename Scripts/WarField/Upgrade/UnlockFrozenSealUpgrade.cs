using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;
using MagicHero;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //解锁冰封
    public class UnlockFrozenSealUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.UNLOCKFROZENSEAL; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            object obj = (MagicHeroIndividualData.SKillFrozenSeal.ParameterType.LEVEL, 1.0f, GD.CalDeltaType.MIN);
            return SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.SKILL, WE.RaceType.Human, SD.TroopType
                .Magic, (int)HumanDefines.HeroType.MAGICHERO, (MagicHeroIndividualData.IndividualDataType.FROZENSEAL, obj), true);
        }

#endregion
    }
}
