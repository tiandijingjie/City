using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WarField
{
    using BFD = BuffDefines;
    using WE = WarFieldElements;

    //晕眩
    public class StunBuff : SoldierBuff
    {
        private float _lastCycle; //the time has past

        public StunBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.STUN;
        }

        //value: last of seconds
        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            float lastTime = Unsafe.As<TValue, float>(ref Unsafe.AsRef(in value));
            float cycle = Utils.CountOfFixUpdate(lastTime);
            if (_isActive == false)
                _lastCycle = cycle;
            else if (_lastCycle < cycle)
                _lastCycle = cycle;

            if (_isActive == false)
            {
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
                return base.ActiveBuff(in value);
            }

            return true;
        }

        public override void UpdateBuff()
        {
            if (_isActive == true)
            {
                _lastCycle--;
                if (_lastCycle <= 0)
                {
                    _lastCycle = 0;
                    ((Soldier)_hostScript).StopAffectedByBuff(p_sdBuffType);
                }
            }
        }

        public override void DeactiveBuff()
        {
            base.DeactiveBuff();
            _lastCycle = 0;
        }
    }
}

