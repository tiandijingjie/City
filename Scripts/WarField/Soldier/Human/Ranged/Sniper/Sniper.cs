using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sniper;

namespace WarField
{
    //狙击手
    public class Sniper : FriendlyRanged
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
            _isRemote = false; //sniper子弹用户看不见，所以直接当成近战进行处理
        }

#endregion

#region public functions

        public void ReduceAttackGap(float percent)
        {
            _nextAttackInterval -= _oriAttackInterval * percent;
            if (_nextAttackInterval < 0)
                _nextAttackInterval = 1;
        }

#endregion

#region private functions
        protected override bool InitSoldier(int spawnIndex, byte mapId)
        {
            var data = SoldierCtrl.Instance.GetSdIndividualData(_race, _troopType, (int)_sdType);
            if (data != null)
            {
                if (_sdConfBeInit == false)
                {
                    _oriIndividualData = new SniperIndividualData((SniperIndividualData)data);
                    _curIndividualData = new SniperIndividualData((SniperIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((SniperIndividualData)data);
                    _curIndividualData.ReInitIndividualData((SniperIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get sniper individual data");
            }

            return base.InitSoldier(spawnIndex, mapId);
        }
#endregion
    }
}

