using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;
using RangedHero;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //解锁箭雨
    public class UnlockArrowRainUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.UNLOCKARROWRAIN; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            object obj = (RangedHeroIndividualData.SkillArrowRain.ParameterType.LEVEL, 1.0f, GD.CalDeltaType.MIN);
            return SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.SKILL, WE.RaceType.Human, SD.TroopType
                .Ranged, (int)HumanDefines.HeroType.RANGEDHERO, (RangedHeroIndividualData.IndividualDataType.ARROWRAIN, obj), true);
        }

#endregion
    }
}
