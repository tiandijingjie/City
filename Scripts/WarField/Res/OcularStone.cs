using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;
    using WE = WarFieldElements;

    //曈石 用于抽卡
    public class OcularStone : PickableResBase
    {
#region public parameters
        public int p_timeWheelLap; //用来记录WarResCtrl中时间轮的圈数
#endregion

#region private parameters

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public function
        public override void InitPickableResBase(WRD.ResContainLevel level, Vector2 pos, int mapId, int amount)
        {
            base.InitPickableResBase(level, pos, mapId, amount);
            _renderer.sprite = WarResCtrl.Instance.gs_stoneSprites[(int)_level];
            _renderer.color = Color.white;
        }

        // 农民走近拾取时调用
        public override void PickUp()
        {
            if (_isTimeout == true || gs_isValid == false)
                return;

            gs_isValid = false;

            WarResCtrl.Instance.ReleasePickableRes(gs_entityIndex, this);
        }

        //超时消失
        public override void TimeOut()
        {
            if (_isTimeout == true)
                return;

            _isTimeout = true;
            gs_isValid = false;

            if (gameObject.activeInHierarchy == false)
            {
                WarResCtrl.Instance.ReleasePickableRes(gs_entityIndex, null);
                return;
            }

            WarResCtrl.Instance.ReleasePickableRes(gs_entityIndex, this);
        }

#endregion

#region private functions

#endregion
    }
}

