using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Swordsman;

namespace WarField
{
    //剑士
    public class Swordsman : FriendlyMelee
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
                    _oriIndividualData = new SwordsmanIndividualData((SwordsmanIndividualData)data);
                    _curIndividualData = new SwordsmanIndividualData((SwordsmanIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((SwordsmanIndividualData)data);
                    _curIndividualData.ReInitIndividualData((SwordsmanIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get swordsman individual data");
            }

            return base.InitSoldier(spawnIndex, mapId);
        }
#endregion
    }
}

