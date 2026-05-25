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

    //攻击落空buff，攻击但是不造成伤害
    public class AttackLostBuff : SoldierBuff
    {
#region public parameters

#endregion

#region private parameters
        //使用struct避免GC
        private class AttackLost
        {
            public int p_chance; //攻击落空的概率
            public List<int> p_durationList; //<0 mean infinite, cound in second not in fixupdate cycle
            public string p_ownerName;
            public List<object> p_ownerList; //who cause this statechange create
            public object p_lock; //为p_ownerList加锁
        }

        private DataPool<AttackLost> _lostList;
        private int _cycleInSec = 0;
        private int _curListChance = 0; //当前丢失的概率，_lostList中p_chance的最大值

        private List<AttackLost> _deadBuffs = new List<AttackLost>(8);
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public AttackLostBuff(object target) : base(target)
        {
            p_sdBuffType = BFD.SoldierBuffType.ATTACKLOST;
            _lostList = new DataPool<AttackLost>(true, 8);
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            AttackLost dd = OnActiveBuff(in value);
            if (dd == null)
                return false;

            if (_isActive == false)
            {
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER, BFD.BuffTriggerType.ATTACKTRIGGER };
                return base.ActiveBuff(in value, callback);
            }
            return true;
        }

        public override bool ActiveBuff<TValue, TRet>(in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            AttackLost dd = OnActiveBuff(in value);
            if (dd == null)
                return false;
            Unsafe.As<TRet, AttackLost>(ref buffRet) = dd; //对与class 也可以 buffRet = (TRet)(object)dd    但是目前的写法适用于class和struct
            if (_isActive == false)
            {
                _triggerType = new List<BFD.BuffTriggerType> { BFD.BuffTriggerType.TIMETRIGGER };
                return base.ActiveBuff(in value, ref buffRet, callback);
            }

            return true;
        }

        public override void UpdateBuff()
        {
            if (_isActive == false)
                return;

            if (_cycleInSec > 0) //每秒受到一次伤害
            {
                _cycleInSec--;
                return;
            }

            _cycleInSec = Utils.CountOfFixUpdateInSecond();
            _lostList.ForList(static (readOnlyList, self) =>
            {
                int cnt = readOnlyList.Count;
                for (int i = 0; i < cnt; i++)
                {
                    var buff = readOnlyList[i];
                    var durationList = buff.p_durationList;
                    var ownerList = buff.p_ownerList;

                    // 必须加锁保护内部的 List 修改
                    lock (buff.p_lock)
                    {
                        // 倒序遍历内部的 List，安全删除且无需额外的临时 List
                        for (int j = durationList.Count - 1; j >= 0; j--)
                        {
                            if (durationList[j] > 0)
                            {
                                durationList[j]--;
                            }
                            else if (durationList[j] == 0)
                            {
                                durationList.RemoveAt(j);
                                ownerList.RemoveAt(j);
                            }
                        }

                        // 如果该 buff 的所有人均已失效，加入待删除列表（存对象）
                        if (durationList.Count == 0)
                            self._deadBuffs.Add(buff);
                    }
                }
            }, this);

            //统一清理失效的 Buff 并重新计算最高概率
            if (_deadBuffs.Count > 0)
            {
                for (int i = 0; i < _deadBuffs.Count; i++)
                {
                    // 传入对象进行删除，无惧 Swap-And-Pop 的索引变化
                    _lostList.RemoveItem(_deadBuffs[i]);
                }
                _deadBuffs.Clear();

                RecalculateMaxChance();
            }
        }

        public override void StopPartOfBuff<TValue>(in TValue value)
        {
            if (_isActive == false)
                return;

            ValueTuple<object, object> tupple = Unsafe.As<TValue, ValueTuple<object, object>>(ref Unsafe.AsRef(in value));
            AttackLost dd = tupple.Item1 as AttackLost; //is the buffRet returned by ActiveBuff
            object owner = tupple.Item2;
            if (dd == null || _lostList.Contains(dd) == false)
                return;

            lock (dd.p_lock)
            {
                int oIndex = dd.p_ownerList.IndexOf(owner);
                if (oIndex == -1)
                    return;

                dd.p_durationList.RemoveAt(oIndex);
                dd.p_ownerList.RemoveAt(oIndex);

                if (dd.p_ownerList.Count == 0)
                {
                    // 立刻删除，无需延迟
                    _lostList.RemoveItem(dd);
                    RecalculateMaxChance(); //recalculate change
                }
            }
        }

        public override float BuffDoAttackPre(float hit, object rival, WarFieldElements.WarEleType rivalType)
        {
            if (Utils.GetRandomInt() < _curListChance) //攻击丢失
                return 0;
            return hit;
        }

#endregion

#region private functions

        private AttackLost OnActiveBuff<TValue>(in TValue value)
        {
            //类型强转
            ValueTuple<int, float, string, object, BFD.BuffStrategy> tupple = Unsafe.As<TValue, ValueTuple<int, float, string, object, BFD.BuffStrategy>>(ref Unsafe.AsRef(in
                value));
            int chance = tupple.Item1;
            float time = tupple.Item2;
            string ownerName = tupple.Item3;
            object owner = tupple.Item4;
            BFD.BuffStrategy strategy = tupple.Item5;

            if(chance > _curListChance)
                _curListChance = chance;

            AttackLost dd = default;
            bool isFound = false;
            if (strategy != BFD.BuffStrategy.CREATENEW)
                isFound = _lostList.FindFirst(static (item, name) => item.p_ownerName == name, ownerName, out dd);

            if (isFound == false)
            {
                dd = new AttackLost();
                dd.p_chance = chance;
                dd.p_durationList = new List<int>();
                if (time > 0)
                    dd.p_durationList.Add((int)Mathf.Ceil(time));
                else
                    dd.p_durationList.Add(-1);
                dd.p_ownerName = ownerName;
                dd.p_ownerList = new List<object>();
                dd.p_ownerList.Add(owner);
                dd.p_lock = new object();
                _lostList.AddItem(dd);
            }
            else
            {
                if (strategy == BFD.BuffStrategy.UNIQUE)
                    return dd; //active false

                lock (dd.p_lock)
                {
                    if (strategy == BFD.BuffStrategy.OVERRIDE)
                    {
                        dd.p_chance = chance;
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
                }
            }

            _cycleInSec = Utils.CountOfFixUpdateInSecond();
            return dd;
        }

        private void RecalculateMaxChance()
        {
            _curListChance = 0;

            // 传入 this 作为 TState (参数 self 就是 this)
            _lostList.ForList(static (readOnlyList, self) =>
            {
                int count = readOnlyList.Count;
                for (int i = 0; i < count; i++)
                {
                    // 直接通过 self 访问并更新 _curListChance
                    if (self._curListChance < readOnlyList[i].p_chance)
                    {
                        self._curListChance = readOnlyList[i].p_chance;
                    }
                }
            }, this);
        }
#endregion
    }
}
