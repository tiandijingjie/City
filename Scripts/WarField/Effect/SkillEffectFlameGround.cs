using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;

    //燃烧地面
    public class SkillEffectFlameGround : SkillEffect
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private SpriteRenderer _renderer;

        private float _damagePerSec; //秒伤
        private DataPool<Soldier> _soldierInRange;
        private Dictionary<Soldier, object> _sdBuffDic; //solider -> duration damage buff indexer
        private object _lock;
        private (float, float, string, object, BFD.BuffStrategy) _damageObj;
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.FLAMEFROUND; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.FLAMEFROUND;
            base.Awake();
            _soldierInRange = new DataPool<Soldier>(false); //没有加锁
            _sdBuffDic = new Dictionary<Soldier, object>();
            _renderer.enabled = false;
            _lock = new object();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if(_isActive == false)
                return;

            string tag = collision.gameObject.tag;
            WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
            if (colType == WE.WarEleType.SOLDIER)
            {
                Soldier soldier = collision.GetComponent<Soldier>();
                lock (_lock)
                {
                    if (_soldierInRange.AddItem(soldier) == true)
                    {
                        object indexer = null;
                        soldier.BeAffectedByBuff(BFD.SoldierBuffType.DURATIONDAMAGE, in _damageObj, ref indexer);
                        _sdBuffDic.Add(soldier, indexer);
                    }
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if(_isActive == false)
                return;

            string tag = collision.gameObject.tag;
            WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
            if (colType == WE.WarEleType.SOLDIER)
            {
                Soldier soldier = collision.GetComponent<Soldier>();
                var value = (_sdBuffDic[soldier], (object)this);
                soldier.StopPartOfBuff(BFD.SoldierBuffType.DURATIONDAMAGE, in value);
                lock (_lock)
                {
                    _sdBuffDic.Remove(soldier);
                    _soldierInRange.RemoveItem(soldier);
                }
            }
        }

#endregion

#region public functions

#endregion

#region private functions
        protected override void OnActiveEffect(object value)
        {
            ValueTuple<float, float> valueTuple = (ValueTuple<float, float>)value;
            _damagePerSec = valueTuple.Item1;
            _duration = valueTuple.Item2;
            _renderer.enabled = true;
            if(_duration > 0)
                Invoke("OnTimeOut", _duration);
            _damageObj = (_damagePerSec, -1.0f, "SkillEffectFlameGround", (object)this, BFD.BuffStrategy.APPEND);
        }

        protected override void OnDeactiveEffect()
        {
            _renderer.enabled = false;

            lock (_lock)
            {
                int count = _soldierInRange.Count;

                for (int i = 0; i < count; i++) //_soldierInRange没有加锁，才能这么遍历
                {
                    var value = (_sdBuffDic[_soldierInRange.GetByIndex(i)], (object)this);
                    _soldierInRange.GetByIndex(i).StopPartOfBuff(BFD.SoldierBuffType.DURATIONDAMAGE, in value);
                }

                _sdBuffDic.Clear();
                _soldierInRange.Clear();
            }
        }

        private void OnTimeOut()
        {
            EffectCtrl.Instance.ReleaseEffect(this, _effectType);
        }
#endregion
    }
}

