using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SD = SoldierDefines;

    //意外财富
    public class KillNeutralGetGoldUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.KILLNEUTRALGETGOLD; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("killGold");
            if(value < 0)
                return false;
            value *= p_node.p_curLevel;
            object rewardObj = (SD.StateSoldierEffectType.DIEAWARD, value, GD.CalDeltaType.ADD);
            object rewardChanceObj = (SD.StateSoldierEffectType.DIEAWARDCHANCE, 100.0f, GD.CalDeltaType.ADD); //100% chance
            for (int i = 0; i < (int)SD.TroopType.MAX; i++)
            {
                var list = SoldierCtrl.Instance.GetSoldiers(WE.RaceType.Neutral, (SD.TroopType)i);
                if(list == null || list.Count == 0)
                    continue;
                foreach (var conf in list)
                {
                    SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.STATE, WE.RaceType.Neutral, (SD
                        .TroopType)i, conf.p_soldierType, rewardObj, false);
                    SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.STATE, WE.RaceType.Neutral, (SD
                        .TroopType)i, conf.p_soldierType, rewardChanceObj, false);
                }
            }

            return true;
        }

#endregion
    }
}
