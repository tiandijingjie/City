using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;

    //金币
    public class GoldCoinInStore : WarResInStoreBase
    {
        public GoldCoinInStore() : base(0)
        {
            _resTypes = WRD.ResTypes.GOLDCOIN;
        }
    }
}
