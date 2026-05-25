using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //初级急救包:英雄在5s内恢复150点生命
    public class MinorFirstAidKitProp : Prop
    {
        private MinorFirstAidKitConf _thisConf;

        public MinorFirstAidKitProp()
        {
            _thisConf = new MinorFirstAidKitConf();
            _conf = _thisConf;
        }

        public unsafe override bool UseProp()
        {
            //需要计算当前gs_propCureChange gs_propDurationChange
            object _hpIncObj = (SD.StateSoldierEffectType.HPINC, _thisConf.p_curePerSec * PropCtrl.Instance.gs_propCureChange, GD.CalDeltaType.ADD,
                _thisConf.p_duration * PropCtrl.Instance.gs_propDurationChange, "MinorFirstAidKit", BFD.BuffStrategy.APPEND, (object)this);
            if (SoldierCtrl.Instance.gs_curHero.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _hpIncObj, PropFinish) == false)
            {
                GameLogger.LogError($"Use Prop failed");
                return false;
            }

            return base.UseProp();
        }

        public unsafe void PropFinish(BFD.BuffCallBackEventType eventType, void* obj)
        {
            Debug.Log("MinorFirstAidKit Finish");
        }
    }
}
