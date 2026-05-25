using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using ED = EffectDefines;

    //变形
    public class ShapeChangeBuff : SoldierBuff
    {
        private float _lastCycle;
        private SkillEffectPolymorph _effect;

        public ShapeChangeBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.SHAPCHANGE;
        }

        public override bool CanAddBuff(object value = null)
        {
            if (_isActive == false)
                return true;
            return false;
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            if(_isActive == true)
                return false;

            //类型强转
            float lastTime = Unsafe.As<TValue, float>(ref Unsafe.AsRef(in value));
            if(lastTime > 0)
                _lastCycle = Utils.CountOfFixUpdate(lastTime);
            else
                _lastCycle = -1; //infinity
            _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
            base.ActiveBuff(in value);

            _hostSoldier.ChangeAnimRender(false);
            _effect = (SkillEffectPolymorph)EffectCtrl.Instance.AddEffectAt(_hostSoldier.gs_transform.position, ED.EffectType.POLYMORPH, _hostSoldier
                .gs_mapId);
            return true;
        }

        public override void UpdateBuff()
        {
            if (_isActive == true)
            {
                if(_lastCycle > 0)
                    _lastCycle--;

                if (_lastCycle == 0)
                {
                    ((Soldier)_hostScript).StopAffectedByBuff(p_sdBuffType);
                }
            }
        }

        public override void DeactiveBuff()
        {
            base.DeactiveBuff();
            _lastCycle = 0;
            if(_effect != null)
                EffectCtrl.Instance.ReleaseEffect(_effect, ED.EffectType.POLYMORPH);
            _hostSoldier.ChangeAnimRender(true);
            _effect = null;
        }
    }
}

