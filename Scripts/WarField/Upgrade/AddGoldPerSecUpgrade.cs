using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    //增加工资
    public class AddGoldPerSecUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.ADDGOLDPERSEC; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            float value = p_node.ConvertSpecificToNum("goldAdd");
            if(value < 0)
                return false;
            value *= p_node.p_curLevel;
            WarResCtrl.Instance.GoldIncomePerSecChange(Mathf.RoundToInt(value), GD.CalDeltaType.ADD);

            return true;
        }

#endregion
    }
}
