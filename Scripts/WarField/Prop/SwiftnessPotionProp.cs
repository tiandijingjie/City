using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //迅捷药水: 增加英雄30%的移速,持续10s
    public class SwiftnessPotionProp : Prop
    {
        private SwiftnessPotionConf _thisConf;

        public SwiftnessPotionProp()
        {
            _thisConf = new SwiftnessPotionConf();
            _conf = _thisConf;
        }

        public unsafe override bool UseProp()
        {
            //需要计算当前gs_propCureChange gs_propDurationChange
            object _hpIncObj = (SD.StateSoldierEffectType.MOVESPEED, _thisConf.p_moveUp, GD.CalDeltaType.MUL,
                _thisConf.p_duration * PropCtrl.Instance.gs_propDurationChange, "SwiftnessPotion", BFD.BuffStrategy.APPEND, (object)this);
            if (SoldierCtrl.Instance.gs_curHero.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _hpIncObj, PropFinish) == false)
            {
                GameLogger.LogError($"Use Prop failed");
                return false;
            }
            return base.UseProp();
        }

        public unsafe void PropFinish(BFD.BuffCallBackEventType eventType, void* obj)
        {
            Debug.Log("SwiftnessPotion Finish");
        }
    }
}
