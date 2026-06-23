using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using WD = WeaponDefines;

    //弓箭手
    public class Archer : FriendlyRanged
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
            _weaponId = WD.WeaponId.ARCHERWEAPON;
        }

#endregion

#region public functions

#endregion

#region private functions

#endregion
    }
}

