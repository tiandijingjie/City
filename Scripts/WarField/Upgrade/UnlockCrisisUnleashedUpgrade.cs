using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;
using MeleeHero;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //解锁危机降临
    public class UnlockCrisisUnleashedUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.UNLOCKCRISISUNLEASHED; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            object obj = (MeleeHeroIndividualData.SkillCrisisUnleashed.ParameterType.LEVEL, 1.0f, GD.CalDeltaType.MIN);
            return SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.SKILL, WE.RaceType.Human, SD.TroopType
                .Melee, (int)HumanDefines.HeroType.MELEEHERO, (MeleeHeroIndividualData.IndividualDataType.CRISISUNLEASHED, obj), true);
        }

#endregion
    }
}
