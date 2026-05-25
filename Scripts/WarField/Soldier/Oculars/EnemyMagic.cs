using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    public class EnemyMagic : EnemySoldier
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected OcularsDefines.MagicType _sdType;

#endregion

#region private parameters' get set

        public override int gs_sdType
        {
            get { return (int)_sdType; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions

        public override bool InitSoldier(byte mapId)
        {
            if (_sdConfBeInit == false)
            {
                _sdConf = new SoldierConf(SoldierCtrl.Instance.GetSdConf(_race, _troopType, (int)_sdType));
                _sdConfBeInit = true;
            }
            else
            {
                _sdConf.ReInitSoldierConf(SoldierCtrl.Instance.GetSdConf(_race, _troopType, (int)_sdType));
            }
            _entitySubType = WE.EncodeEntitySubType((byte)_race, (byte)_troopType, (byte)_sdType, 0);
            return base.InitSoldier(mapId);
        }

#endregion

#region private functions

        protected override void OnSoldierDie()
        {
            base.OnSoldierDie();

            SoldierCtrl.Instance.RemoveSoldier(_race, _troopType, (int)_sdType, this, _sdConf.p_level, _mapId);
        }

#endregion
    }
}

