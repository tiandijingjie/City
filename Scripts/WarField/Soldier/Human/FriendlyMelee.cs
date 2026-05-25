using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    public class FriendlyMelee : FriendlySoldier
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected HumanDefines.MeleeType _sdType;

#endregion

#region private parameters' get set3

        public override int gs_sdType
        {
            get { return (int)_sdType; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _isRemote = false;
        }

#endregion

#region public functions

#endregion

#region private functions
        protected override bool InitSoldier(int spawnIndex, byte mapId)
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
            return base.InitSoldier(spawnIndex, mapId);
        }

        protected override void OnSoldierDie()
        {
            SoldierCtrl.Instance.RemoveSoldier(_race, _troopType, (int)_sdType, this, _sdConf.p_level, _mapId);
            base.OnSoldierDie();
        }

#endregion
    }
}

