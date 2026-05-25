using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //持续性伤害buff
    public class DurativeDamageBuff : SoldierBuff
    {
#region public parameters

#endregion

#region private parameters
        private class DurationDamage
        {
            public float p_damage;
            public List<int> p_durationList; //<0 mean infinite, cound in second not in fixupdate cycle
            public string p_ownerName;
            public List<object> p_ownerList; //who cause this statechange create
        }

        private List<DurationDamage> _damageList;
        private int _cycleInSec = 0;
        private List<int> _rmList; //the item need to remove from _damageList
        private object _lock;
#endregion

#region private parameters' get set

#endregion

#region public functions
        public DurativeDamageBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.DURATIONDAMAGE;
            _damageList = new List<DurationDamage>();
            _rmList = new List<int>();
            _lock = new object();
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            DurationDamage dd = OnActiveBuff(value);
            if (dd == null)
                return false;
            if (_isActive == false)
            {
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
                return base.ActiveBuff(in value, callback);
            }

            return true;
        }

        public override bool ActiveBuff<TValue, TRet>(in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            DurationDamage dd = OnActiveBuff(value);
            if (dd == null)
                return false;
            Unsafe.As<TRet, DurationDamage>(ref buffRet) = dd; //对与class 也可以 buffRet = (TRet)(object)dd    但是目前的写法适用于class和struct
            if (_isActive == false)
            {
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
                return base.ActiveBuff(in value, ref buffRet, callback);
            }

            return true;
        }

        public override void UpdateBuff()
        {
            if(_isActive == false)
                return;

            if (_cycleInSec > 0)  //每秒受到一次伤害
            {
                _cycleInSec--;
                return;
            }

            _cycleInSec = Utils.CountOfFixUpdateInSecond();
            int cnt = _damageList.Count;
            for (int i = 0; i < cnt; i++)
            {
                int dCnt = _damageList[i].p_durationList.Count;
                List<int> ownerRmList = null;
                int ownerRmCnt = 0;
                for (int j = 0; j < dCnt; j++)
                {
                    if (_damageList[i].p_durationList[j] > 0)
                        _damageList[i].p_durationList[j]--;
                    else if (_damageList[i].p_durationList[j] == 0)
                    {
                        if(ownerRmList == null)
                            ownerRmList = new List<int>();
                        ownerRmList.Add(j);
                        ownerRmCnt++;
                    }
                }
                _hostSoldier.BeAttacked(null, null, WE.WarEleType.SOLDIER, _damageList[i].p_damage, false, false, out float hitValue);
                for (int k = 0; k < ownerRmCnt; k++)
                {
                    _damageList[i].p_durationList.RemoveAt(ownerRmList[k]);
                    _damageList[i].p_ownerList.RemoveAt(ownerRmList[k]);
                }

                if (ownerRmCnt == dCnt) //remove all of the items in the p_durationList/p_ownerList list
                {
                    lock (_lock)
                    {
                        if(_rmList.Contains(i) == false)
                            _rmList.Add(i);
                    }

                }
            }

            lock (_lock)
            {
                if (_rmList.Count > 0)
                {
                    for (int i = 0; i < _rmList.Count; i++)
                        _damageList.RemoveAt(_rmList[i]);
                    _rmList.Clear();
                }
            }
        }

        public override void StopPartOfBuff<TValue>(in TValue value)
        {
            if (_isActive == false)
                return;

            ValueTuple<object, object> tupple = Unsafe.As<TValue, ValueTuple<object, object>>(ref Unsafe.AsRef(in value));
            DurationDamage dd = tupple.Item1 as DurationDamage; //is the buffRet returned by ActiveBuff
            object owner = tupple.Item2;
            int index = _damageList.IndexOf(dd);
            if(index == -1)
                return;
            int oIndex = _damageList[index].p_ownerList.IndexOf(owner);
            _damageList[index].p_durationList.RemoveAt(oIndex);
            _damageList[index].p_ownerList.RemoveAt(oIndex);
            if (_damageList[index].p_ownerList.Count == 0)
            {
                lock (_lock)
                {
                    if(_rmList.Contains(index) == false)
                        _rmList.Add(index);
                }
            }
        }
#endregion

#region private functions

        private DurationDamage OnActiveBuff(object value)
        {
            ValueTuple<float, float, string, object, BFD.BuffStrategy> tupple = (ValueTuple<float, float, string, object, BFD.BuffStrategy>)value;
            float damage = tupple.Item1;
            float time = tupple.Item2;
            string ownerName = tupple.Item3;
            object owner = tupple.Item4;
            BFD.BuffStrategy strategy = tupple.Item5;

            DurationDamage dd = null;
            if (strategy != BFD.BuffStrategy.CREATENEW)
            {
                int cnt = _damageList.Count;
                for (int i = 0; i < cnt; i++)
                {
                    if (_damageList[i].p_ownerName == ownerName) //同一时间同一种持续性的伤害只能有一个，如果有新的同类伤害过来，只能更新buff的数值、持续时间
                    {
                        dd = _damageList[i];
                        break;
                    }
                }
            }

            if (dd == null)
            {
                dd = new DurationDamage();
                dd.p_damage = damage;
                dd.p_durationList = new List<int>();
                if (time > 0)
                    dd.p_durationList.Add((int)Mathf.Ceil(time));
                else
                    dd.p_durationList.Add(-1);
                dd.p_ownerName = ownerName;
                dd.p_ownerList = new List<object>();
                dd.p_ownerList.Add(owner);
                _damageList.Add(dd);
            }
            else
            {
                if (strategy == BFD.BuffStrategy.UNIQUE)
                    return null; //active false

                if (strategy == BFD.BuffStrategy.OVERRIDE)
                {
                    dd.p_damage = damage;
                    dd.p_durationList[0] = (int)Mathf.Ceil(time);
                    dd.p_ownerList[0] = owner;
                }
                else if (strategy == BFD.BuffStrategy.APPEND)
                {
                    if (dd.p_ownerList.Contains(owner) == false)
                    {
                        dd.p_ownerList.Add(owner);
                        dd.p_durationList.Add((int)Utils.CountOfFixUpdate(time));
                    }
                }
                else //should be BFD.BuffStrategy.Ignore
                {
                    return dd;
                }
            }
            _cycleInSec = Utils.CountOfFixUpdateInSecond();
            return dd;
        }
#endregion


    }
}

