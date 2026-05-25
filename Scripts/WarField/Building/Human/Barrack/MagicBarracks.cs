using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using WBD = WarBuildingDefines;

    public class MagicBarracks : FriendlyBarrack
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _troopType = SD.TroopType.Magic;
        }

#endregion

#region public functions

#endregion

#region private functions


#endregion
    }
}
