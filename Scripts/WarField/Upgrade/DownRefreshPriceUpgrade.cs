using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    //再来一次
    public class DownRefreshPriceUpgrade : UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.DOWNREFRESHPRICE; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            return true;
        }

#endregion
    }
}
