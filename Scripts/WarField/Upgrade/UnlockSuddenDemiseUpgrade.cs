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

    //解锁暴毙
    public class UnlockSuddenDemiseUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.UNLOCKSUDDENDEMISE; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            object obj = (RangedHeroIndividualData.SkillSuddenDemise.ParameterType.LEVEL, 1.0f, GD.CalDeltaType.MIN);
            return SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.SKILL, WE.RaceType.Human, SD.TroopType
                .Ranged, (int)HumanDefines.HeroType.RANGEDHERO, (RangedHeroIndividualData.IndividualDataType.SUDDENDEMISE, obj), true);
        }

#endregion
    }

}
