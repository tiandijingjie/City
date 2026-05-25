using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using WE = WarFieldElements;

    //伤害反弹
    //不能反弹 反弹过来的伤害
    public class ReflectionBuff : SoldierBuff
    {
        private float _relectionPercent; //0~1
        private float _duration, _durationCycle;
        private bool _canEffectBoss;

        private Soldier _reflectionSoldier;
        private float _relectDamage;

        public ReflectionBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.REFLECTION;
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            if (_isActive == true)
            {
                return false;
            }

            ValueTuple<float, float, bool> tuple = Unsafe.As<TValue, ValueTuple<float, float, bool>>(ref Unsafe.AsRef(in value));
            _duration = tuple.Item1;
            _relectionPercent = tuple.Item2;
            _canEffectBoss = tuple.Item3;
            if (_duration > 0)
            {
                _durationCycle = Utils.CountOfFixUpdate(_duration);
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER, BFD.BuffTriggerType.BEATTACKTRIGGER };
            }
            else //infinite
            {
                _durationCycle = -1;
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.BEATTACKTRIGGER };
            }

            _reflectionSoldier = null;
            _relectDamage = 0;
            return base.ActiveBuff(in value, callback);
        }

        public override bool CanAddBuff(object value = null)
        {
            if (_isActive == true)
                return false;
            return true;
        }

        public override void UpdateBuff()
        {
            if(_isActive == false)
                return;

            if(_durationCycle > 0)
                _durationCycle--;
            if (_durationCycle == 0)
            {
                _hostSoldier.StopAffectedByBuff(p_sdBuffType);
            }
        }

        public override void BuffBeAttackPost(float damage, bool isDead, object rival, WE.WarEleType rivalType)
        {
            Soldier sd = (Soldier)rival;
            if (_reflectionSoldier == sd) //suppose _reflectionSoldier == rival
            {
                //不触发skill和buff，就不会再次反弹 反弹过来的伤害
                _reflectionSoldier.BeAttacked(null, null, WE.WarEleType.SOLDIER, _relectDamage, false, false, out float hitValue);
                _relectDamage = 0;
                _reflectionSoldier = null;
            }
        }

        public override float BuffBeAttackPre(float damage, object rival, WE.WarEleType rivalType)
        {
            if (_isActive == false)
                return damage;

            if (rivalType == WE.WarEleType.SOLDIER)
            {
                Soldier sd = (Soldier)rival;
                if(sd.gs_sdLevel == SoldierDefines.SoldierLevel.BOSSLEVEL && _canEffectBoss == false)
                    return damage;
                float reflection = damage * _relectionPercent;
                if (reflection > damage)
                    reflection = damage;
                _relectDamage = reflection;
                _reflectionSoldier = sd;
                damage -= reflection;
            }

            return damage;
        }
    }
}

