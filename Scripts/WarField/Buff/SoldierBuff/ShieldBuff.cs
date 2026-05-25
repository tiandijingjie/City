using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using WE = WarFieldElements;

    public class ShieldBuff : SoldierBuff
    {
        private struct ChargeableAttribute
        {
            public float p_damageAbsorb;
            public float p_brokenRecoverTime;
            public float p_brokenRecoverTimeCycle;
            public float p_peaceRecoverTime;
            public float p_peaceRecoverTimeCycle;
            public BuffUnsafeCallback p_callback; //护盾破裂时回调

            //复制构造函数
            public ChargeableAttribute(ChargeableAttribute attribute)
            {
                p_damageAbsorb = attribute.p_damageAbsorb;
                p_brokenRecoverTime = attribute.p_brokenRecoverTime;
                p_brokenRecoverTimeCycle = attribute.p_brokenRecoverTimeCycle;
                p_peaceRecoverTime = attribute.p_peaceRecoverTime;
                p_peaceRecoverTimeCycle = attribute.p_peaceRecoverTimeCycle;
                p_callback = attribute.p_callback;
            }
        }

        private struct NormalAttribute
        {
            public float p_duration;
            public float p_durationCycle;
            public float p_damageAbsorb;
            public bool p_isInfinityAbsorb; //无敌，无限吸收伤害
            public BuffUnsafeCallback p_callback; //护盾破裂或者结束时回调
        }

        private NormalAttribute _normalAttribute; //normal shield
        private ChargeableAttribute _oriChargeAttribut, _curChargeAttribute; //chargeable shield
        private bool _beAttacked = false; //for chargeable shield
        private BFD.ShieldBuffType? _shieldType;

        public ShieldBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.SHIELD;
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            if (_isActive == true) //already has shield , can not add a new one
                return false;

            if (value is ITuple tmp)
            {
                _shieldType = (BFD.ShieldBuffType)tmp[0];
            }

            if (_shieldType == BFD.ShieldBuffType.NORMAL)
            {
                ValueTuple<BFD.ShieldBuffType, float, float> tuple = Unsafe.As<TValue, ValueTuple<BFD.ShieldBuffType, float, float>>(ref Unsafe.AsRef(in value));
                _normalAttribute.p_duration = Convert.ToSingle(tuple.Item2);
                _normalAttribute.p_damageAbsorb = Convert.ToSingle(tuple.Item3);
                if (_normalAttribute.p_duration > 0)
                {
                    _normalAttribute.p_durationCycle = Utils.CountOfFixUpdate(_normalAttribute.p_duration);
                    _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER, BFD.BuffTriggerType.BEATTACKTRIGGER };
                }
                else //infinity
                {
                    _normalAttribute.p_durationCycle = -1;
                    _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.BEATTACKTRIGGER };
                }

                if (_normalAttribute.p_damageAbsorb <= 0)
                    _normalAttribute.p_isInfinityAbsorb = true;
                else
                    _normalAttribute.p_isInfinityAbsorb = false;
                _normalAttribute.p_callback = callback;
            }
            else if (_shieldType == BFD.ShieldBuffType.CHARGEABLE) //充能型护盾
            {
                _beAttacked = false;
                ValueTuple<BFD.ShieldBuffType, float, float, float> tuple = Unsafe.As<TValue, ValueTuple<BFD.ShieldBuffType, float, float, float>>(ref Unsafe.AsRef(in value));
                _oriChargeAttribut.p_damageAbsorb = Convert.ToSingle(tuple.Item2);
                _oriChargeAttribut.p_brokenRecoverTime = Convert.ToSingle(tuple.Item3);
                _oriChargeAttribut.p_peaceRecoverTime = Convert.ToSingle(tuple.Item4);
                _oriChargeAttribut.p_brokenRecoverTimeCycle = Utils.CountOfFixUpdate(_oriChargeAttribut.p_brokenRecoverTime);
                _oriChargeAttribut.p_peaceRecoverTimeCycle = Utils.CountOfFixUpdate(_oriChargeAttribut.p_peaceRecoverTime);
                _oriChargeAttribut.p_callback = callback;
                _curChargeAttribute = new ChargeableAttribute(_oriChargeAttribut);

                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER, BFD.BuffTriggerType.BEATTACKTRIGGER};
            }

            return base.ActiveBuff(in value, callback);
        }

        public override bool CanAddBuff(object value = null)
        {
            if (_isActive == true)
                return false;
            return true;
        }

        public override unsafe void UpdateBuff()
        {
            if (_isActive == false)
                return;

            if (_shieldType == BFD.ShieldBuffType.NORMAL)
            {
                _normalAttribute.p_durationCycle--;
                if (_normalAttribute.p_durationCycle <= 0)
                {
                    if(_normalAttribute.p_callback != null)
                        _normalAttribute.p_callback(BFD.BuffCallBackEventType.FINISH, (void*)null); //时间结束
                    _hostSoldier.StopAffectedByBuff(p_sdBuffType);
                }
            }
            else if(_shieldType == BFD.ShieldBuffType.CHARGEABLE)
            {
                if (_curChargeAttribute.p_damageAbsorb <= 0) //护盾被打破
                {
                    _curChargeAttribute.p_brokenRecoverTimeCycle--;
                    if (_curChargeAttribute.p_brokenRecoverTimeCycle <= 0)
                    {
                        _curChargeAttribute.p_damageAbsorb = _oriChargeAttribut.p_damageAbsorb;
                        _curChargeAttribute.p_brokenRecoverTimeCycle = _oriChargeAttribut.p_brokenRecoverTimeCycle;
                    }
                }

                if (_hostSoldier.gs_curStatus != SoldierDefines.SoldierStatus.ATTACKTATGET)
                {
                    if (_curChargeAttribute.p_damageAbsorb != _oriChargeAttribut.p_damageAbsorb)
                    {
                        _curChargeAttribute.p_peaceRecoverTimeCycle--;
                        if (_beAttacked == true) //be attacked, reset the peace time
                        {
                            _beAttacked = false;
                            _curChargeAttribute.p_peaceRecoverTimeCycle = _oriChargeAttribut.p_peaceRecoverTimeCycle;
                        }

                        if (_curChargeAttribute.p_peaceRecoverTimeCycle == 0)
                        {
                            _curChargeAttribute.p_damageAbsorb = _oriChargeAttribut.p_damageAbsorb;
                            _curChargeAttribute.p_peaceRecoverTimeCycle = _oriChargeAttribut.p_peaceRecoverTimeCycle;
                        }
                    }

                }
            }
        }

        public override unsafe float BuffBeAttackPre(float damage, object rival, WarFieldElements.WarEleType rivalType)
        {
            if (_isActive == false)
                return damage;

            if (_shieldType == BFD.ShieldBuffType.NORMAL)
            {
                if (_normalAttribute.p_isInfinityAbsorb == true)
                    return 0;

                if (_normalAttribute.p_damageAbsorb > 0)
                {
                    float remain = _normalAttribute.p_damageAbsorb - damage;
                    if (remain >= 0)
                    {
                        _normalAttribute.p_damageAbsorb = remain;
                        return 0;
                    }

                    _normalAttribute.p_damageAbsorb = 0;
                    _hostSoldier.StopAffectedByBuff(p_sdBuffType);
                    if(_normalAttribute.p_callback != null)
                        _normalAttribute.p_callback(BFD.BuffCallBackEventType.FINISH, (void*)null); //护盾破裂回调
                    return -remain;
                }
            }
            else if (_shieldType == BFD.ShieldBuffType.CHARGEABLE)
            {
                _beAttacked = true;
                if (_curChargeAttribute.p_damageAbsorb > 0)
                {
                    float remain = _curChargeAttribute.p_damageAbsorb - damage;
                    if (remain >= 0)
                    {
                        _curChargeAttribute.p_damageAbsorb = remain;
                        return 0;
                    }

                    _curChargeAttribute.p_damageAbsorb = 0;
                    if (_curChargeAttribute.p_callback != null)
                    {
                        float absorbValue = _oriChargeAttribut.p_damageAbsorb;//将堆里的值拷贝到当前方法的栈内存中
                        _oriChargeAttribut.p_callback(BFD.BuffCallBackEventType.EFFECT, &absorbValue); //护盾破裂回调
                    }

                    return -remain;
                }
            }

            return damage;
        }
    }
}

