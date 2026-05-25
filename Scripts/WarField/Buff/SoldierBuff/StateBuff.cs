using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    public class StateBuff : SoldierBuff
    {
#region public parameters

#endregion

#region private parameters

        private class StateChange
        {
            public SD.StateSoldierEffectType p_type; //what state will be effected
            public float p_value;
            public GD.CalDeltaType p_calType;
            public List<int> p_timeCycle; //according to p_ownerList, evenry owner take effect time
            public float p_oriState; //原始的state值
            public string p_ownerName; //where the buff come from, the class name
            public List<object> p_ownerList; //who cause this statechange create
            public object p_stateChangeObj; //the index return from AddStateChange
            public List<BuffUnsafeCallback> p_callbacks;

            public bool TryChangeState(Soldier sd)
            {
                p_stateChangeObj = sd.AddStateChange(p_type, p_value, p_calType, out p_oriState);
                if(p_stateChangeObj != null)
                    return true;
                return false;
            }

            //not change state anymore, remove state change
            public unsafe void RestoreState(Soldier sd)
            {
                sd.ModifyStateChange(p_type, p_stateChangeObj, true, 0, GD.CalDeltaType.MIN, true);
                if (p_callbacks.Count > 0)
                {
                    int count = p_callbacks.Count;
                    for (int i = 0; i < count; i++)
                    {
                        p_callbacks[i](BFD.BuffCallBackEventType.FINISH, (void*)null);
                    }
                }
            }

            public bool ModifyState(Soldier sd)
            {
                return sd.ModifyStateChange(p_type, p_stateChangeObj, false, p_value, p_calType, true); //覆盖之前的state变化
            }
        }

        private List<StateChange>[] _stateChangeList;
#endregion

#region public functions
        public StateBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.STATE;
            _stateChangeList = new List<StateChange>[(int)SD.StateSoldierEffectType.MAX];
            for (int i = 1; i < (int)SD.StateSoldierEffectType.MAX; i++)
            {
                _stateChangeList[i] = new List<StateChange>();
            }
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            StateChange sc = OnActiveBuff(in value, callback);
            if (sc == null)
                return false;
            if (_isActive == false)
            {
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
                return base.ActiveBuff(in value, callback);
            }

            return true;
        }

        //return a statechange struct as buffRet, used by StopPartOfBuff()
        public override bool ActiveBuff<TValue, TRet>(in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            StateChange sc = OnActiveBuff(value, callback);
            if (sc == null)
                return false;
            Unsafe.As<TRet, StateChange>(ref buffRet) = sc;
            if (_isActive == false)
            {
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
                return base.ActiveBuff(in value, ref buffRet, callback);
            }

            return true;
        }


        public override void UpdateBuff()
        {
            base.UpdateBuff();
            for (int i = 1; i < (int)SD.StateSoldierEffectType.MAX; i++)
            {
                List<StateChange> rmList = new List<StateChange>();
                List<StateChange> list = _stateChangeList[i];
                int cnt = list.Count;
                for (int j = 0; j < cnt; j++)
                {
                    StateChange change = list[j];
                    int stCnt = change.p_ownerList.Count;
                    List<int> ownerRmList = null;
                    int ownerRmCnt = 0;
                    for (int z = 0; z < stCnt; z++)
                    {
                        if(change.p_timeCycle[z] > 0)
                            change.p_timeCycle[z]--;
                        if (change.p_timeCycle[z] == 0) //<0 means infinite
                        {
                            if(ownerRmList == null)
                                ownerRmList = new List<int>();
                            ownerRmList.Add(z);
                            ownerRmCnt++;
                        }
                    }
                    for (int z = 0; z < ownerRmCnt; z++)
                    {
                        change.p_ownerList.RemoveAt(ownerRmList[z]);
                        change.p_timeCycle.RemoveAt(ownerRmList[z]);
                    }

                    if (ownerRmCnt == stCnt)//remove all of the items in the p_timeCycle/p_ownerList list
                    {
                        change.RestoreState(_hostSoldier);
                        rmList.Add(change);
                    }
                }

                int rmCnt = rmList.Count;
                for (int z = 0; z < rmCnt; z++)
                {
                    list.Remove(rmList[z]);
                }
            }
        }

        public override void StopPartOfBuff<TValue>(in TValue value)
        {
            if (_isActive == false)
                return;

            ValueTuple<object, object> tupple = Unsafe.As<TValue, ValueTuple<object, object>>(ref Unsafe.AsRef(in value));
            StateChange statechange = tupple.Item1 as StateChange; //is the buffRet returned by ActiveBuff
            object owner = tupple.Item2;
            if(statechange == null)
                return;
            List<StateChange> list = _stateChangeList[(int)statechange.p_type];
            int index = list.IndexOf(statechange);
            if (index >= 0)
            {
                statechange.p_ownerList.Remove(owner);
                if (statechange.p_ownerList.Count == 0)
                {
                    statechange.RestoreState(_hostSoldier);
                    list.Remove(statechange);
                }
            }
        }

        public override void DeactiveBuff()
        {
            for (int i = 1; i < (int)SD.StateSoldierEffectType.MAX; i++) //not change state, soldier it self will restore
            {
                _stateChangeList[i].Clear();
            }
            base.DeactiveBuff();
        }

#endregion

#region private functions
        //如果是对hpinc的buff,hpinc是每秒的改变,之后会转成per cycle的值的
        private unsafe StateChange OnActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback)
        {
            ValueTuple<SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object> tupple =
                Unsafe.As<TValue, ValueTuple<SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object>>(ref Unsafe.AsRef(in value));

            SD.StateSoldierEffectType type = tupple.Item1;
            float newValue = tupple.Item2;
            GD.CalDeltaType calType = tupple.Item3;
            float time = tupple.Item4; //by seconds
            string ownerName = tupple.Item5;
            BFD.BuffStrategy strategy = tupple.Item6;
            object owner = tupple.Item7;
            int timeCycle = 0;

            List<StateChange> list = _stateChangeList[(int)type];
            StateChange stateChange = null;
            if (strategy != BFD.BuffStrategy.CREATENEW) //not always add a new buff
            {
                int cnt = list.Count;
                for (int i = 0; i < cnt; i++)
                {
                    if (list[i].p_ownerName == ownerName)
                        stateChange = list[i];
                }
            }

            if (stateChange == null)
            {
                stateChange = new StateChange();
                stateChange.p_type = type;
                stateChange.p_value = newValue;
                stateChange.p_calType = calType;
                stateChange.p_ownerName = ownerName;
                stateChange.p_ownerList = new List<object>();
                stateChange.p_ownerList.Add(owner);
                stateChange.p_timeCycle = new List<int>();
                stateChange.p_timeCycle.Add((int)Utils.CountOfFixUpdate(time));
                stateChange.p_callbacks = new List<BuffUnsafeCallback>();
                if(callback != null)
                    stateChange.p_callbacks.Add(callback);

                //take effect
                if (stateChange.TryChangeState(_hostSoldier) == false)
                    return null;
                _stateChangeList[(int)stateChange.p_type].Add(stateChange);
            }
            else //same buff can only take effect for one time, if a new same buff come, only update duration time,not update value no matter what is the new value
            {
                if (strategy == BFD.BuffStrategy.UNIQUE)
                    return null; //active false

                if (strategy == BFD.BuffStrategy.OVERRIDE)
                {
                    timeCycle = (int)Utils.CountOfFixUpdate(time);
                    stateChange.p_timeCycle[0] = timeCycle;
                    stateChange.p_value = newValue;
                    stateChange.p_ownerList[0] = owner; //owner name can not change
                    stateChange.p_calType = calType;
                    if (stateChange.p_callbacks.Count > 0) //should only has one callback
                        stateChange.p_callbacks[0](BFD.BuffCallBackEventType.OVERRIDE, (void*)null);
                    if(callback != null)
                        stateChange.p_callbacks[0] = callback;
                    if (stateChange.p_value != newValue)
                    {
                        stateChange.p_value = newValue;
                        if (stateChange.ModifyState(_hostSoldier) == false)
                            return null;
                    }
                }
                else if (strategy == BFD.BuffStrategy.APPEND)
                {
                    if (stateChange.p_ownerList.Contains(owner) == false)
                    {
                        stateChange.p_ownerList.Add(owner);
                        stateChange.p_timeCycle.Add((int)Utils.CountOfFixUpdate(time));
                        if(callback != null)
                            stateChange.p_callbacks.Add(callback);
                    }
                }
                else //should be BFD.BuffStrategy.Ignore
                {
                    return stateChange;
                }
            }
            return stateChange;
        }
#endregion
    }
}

