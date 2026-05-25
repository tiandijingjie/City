using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;

    //宝石 用于在游戏外的升级
    public class GemInStore : WarResInStoreBase
    {
        public GemInStore() : base(0)
        {
            _resTypes = WRD.ResTypes.GEM;
        }
    }
}
