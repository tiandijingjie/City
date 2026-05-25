using System.Collections;
using System.Collections.Generic;

using WarField;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //士兵增加{hpInc}HP/s的生命恢复
    public class AddSoldierHpIncUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.ADDSOLDIERHPINC; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("hpInc");
            if(value < 0)
                return false;
            value *= p_node.p_curLevel;

            object improveObj = (SD.StateSoldierEffectType.HPINC, value, GD.CalDeltaType.ADD);
            for (int i = 1; i < (int)HumanDefines.MeleeType.MAX; i++)
            {
                SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.STATE, WE.RaceType.Human, SD.TroopType.Melee,
                    i, improveObj, false);
            }

            for (int i = 1; i < (int)HumanDefines.RangedType.MAX; i++)
            {
                SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.STATE, WE.RaceType.Human,
                    SD.TroopType.Ranged, i, improveObj, false);
            }

            for (int i = 1; i < (int)HumanDefines.MagicType.MAX; i++)
            {
                SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.STATE, WE.RaceType.Human,
                    SD.TroopType.Magic, i, improveObj, false);
            }

            return true;
        }

#endregion
    }
}
