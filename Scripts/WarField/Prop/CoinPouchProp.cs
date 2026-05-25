using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    using WRD = WarResDefine;

    public class CoinPouchProp : Prop
    {
        private CoinPouchConf _thisConf;

        public CoinPouchProp()
        {
            _thisConf = new CoinPouchConf();
            _conf = _thisConf;
        }

        public override bool UseProp()
        {
            WarResCtrl.Instance.AddRes(WRD.ResTypes.GOLDCOIN, _thisConf.p_coin);
            return base.UseProp();
        }
    }
}
