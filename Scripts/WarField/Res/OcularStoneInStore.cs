using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;

    //存储的总能量 用于抽卡
    public class OcularStoneInStore : WarResInStoreBase
    {
        public OcularStoneInStore() : base(0)
        {
            _resTypes = WRD.ResTypes.OCULARSTONE;
        }
    }
}
