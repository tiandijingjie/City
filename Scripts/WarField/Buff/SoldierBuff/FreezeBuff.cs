using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using WE = WarFieldElements;
    using ED = EffectDefines;

    //冰冻
    public class FreezeBuff : SoldierBuff
    {
        private float _duration; //<0 mean infinite, cound in second not in fixupdate cycle
        private float _damage; //damage per second
        private int _cycleInSec = 0;
        private EffectBase _effect;

        public FreezeBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.FREEZE;
        }

        public override bool CanAddBuff(object value = null)
        {
            if (_isActive == true)
                return false;
            return true;
        }

        //value: last of seconds
        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            if (_isActive == true)
                return false;

            //类型强转
            ValueTuple<float, float> tupple = Unsafe.As<TValue, ValueTuple<float, float>>(ref Unsafe.AsRef(in value));
            _duration = tupple.Item1;
            _damage = tupple.Item2;
            if (_damage <= 0)
                _damage = 0;

            _cycleInSec = Utils.CountOfFixUpdateInSecond();
            _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
            _effect = EffectCtrl.Instance.AddEffectAt(_hostSoldier.gs_transform.position, ED.EffectType.FROZEN, _hostSoldier.gs_mapId, 1f);
            return base.ActiveBuff(in value);
        }

        public override void UpdateBuff()
        {
            if(_isActive == false)
                return;
            if (_cycleInSec > 0)
            {
                _cycleInSec--;
                return;
            }

            _cycleInSec = Utils.CountOfFixUpdateInSecond();

            if(_damage > 0)  //每秒受到一次伤害
                _hostSoldier.BeAttacked(null, null, WE.WarEleType.SOLDIER, _damage, false, false, out float hitValue);

            if (_duration > 0)
                _duration--;

            if (_duration == 0)
            {
                EffectCtrl.Instance.ReleaseEffect(_effect, ED.EffectType.FROZEN, true);
                _hostSoldier.StopAffectedByBuff(p_sdBuffType);
                return;
            }
        }

        public override void DeactiveBuff()
        {
            base.DeactiveBuff();
            _duration = 0;
        }
    }
}

