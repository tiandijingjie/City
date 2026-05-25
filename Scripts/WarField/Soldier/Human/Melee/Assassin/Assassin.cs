using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Assassin;

namespace WarField
{
    using SD = SoldierDefines;

    //刺客
    public class Assassin : FriendlyMelee
    {
#region public parameters

#endregion

#region private parameters

        private byte _eneitySpec;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override bool InitSoldier(int spawnIndex, byte mapId)
        {
            var data = SoldierCtrl.Instance.GetSdIndividualData(_race, _troopType, (int)_sdType);
            if (data != null)
            {
                if (_sdConfBeInit == false)
                {
                    _oriIndividualData = new AssassinIndividualData((AssassinIndividualData)data);
                    _curIndividualData = new AssassinIndividualData((AssassinIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((AssassinIndividualData)data);
                    _curIndividualData.ReInitIndividualData((AssassinIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get assassin individual data");
            }

            _eneitySpec |= 1 << (int)SpatialDefines.EntitySpecType.HIDE; //出生就隐身
            return base.InitSoldier(spawnIndex, mapId);
        }

        protected override void OnStateChanged(SD.StateSoldierEffectType stateType, float oriValue, float newValue)
        {
            if (stateType == SD.StateSoldierEffectType.BODYHIDE)
            {
                if(oriValue == newValue)
                    return;
                if (newValue == -1)
                    _eneitySpec |= 1 << (int)SpatialDefines.EntitySpecType.HIDE;
                else
                    _eneitySpec = 0;
            }
        }

        protected override byte GetSpatialEntitySpecData()
        {
            return _eneitySpec;
        }

#endregion
    }
}

