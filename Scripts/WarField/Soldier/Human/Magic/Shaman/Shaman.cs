using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Shaman;

namespace WarField
{

    //萨满祭司: 高级辅助士兵，有各种不可思议的技能
    public class Shaman : FriendlySupport
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
                    _oriIndividualData = new ShamanIndividualData((ShamanIndividualData)data);
                    _curIndividualData = new ShamanIndividualData((ShamanIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((ShamanIndividualData)data);
                    _curIndividualData.ReInitIndividualData((ShamanIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get shaman individual data");
            }

            return base.InitSoldier(spawnIndex, mapId);
        }

#endregion
    }
}

