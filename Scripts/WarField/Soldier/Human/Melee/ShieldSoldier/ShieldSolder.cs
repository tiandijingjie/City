using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    //盾兵
    public class ShieldSolder : FriendlyMelee
    {
#region public parameters

#endregion

#region private parameters

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
                    _oriIndividualData = new ShieldSoldierIndividualData((ShieldSoldierIndividualData)data);
                    _curIndividualData = new ShieldSoldierIndividualData((ShieldSoldierIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((ShieldSoldierIndividualData)data);
                    _curIndividualData.ReInitIndividualData((ShieldSoldierIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get shield soldier individual data");
            }

            return base.InitSoldier(spawnIndex, mapId);
        }

#endregion
    }
}

