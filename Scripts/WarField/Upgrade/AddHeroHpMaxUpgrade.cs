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

    //增加英雄{addHp}点生命值上限
    public class AddHeroHpMaxUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.ADDHEROHPMAX; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("addHp");
            if(value < 0)
                return false;
            value *= p_node.p_curLevel;
            object improveObj = (SD.StateSoldierEffectType.HEALTH, value, GD.CalDeltaType.ADD);
            for (int i = 1; i < (int)HumanDefines.HeroType.MAX; i++)
            {
                SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.STATE, WE.RaceType.Human, SD.TroopType.MIN,
                    i, improveObj, true);
            }

            return true;
        }

#endregion
    }
}
