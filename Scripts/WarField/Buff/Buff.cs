using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SKD = SkillDefines;
    using BFD = BuffDefines;
    using WE = WarFieldElements;

    public class Buff
    {
        protected WE.WarEleType _hostType; //building or soldier
        protected object _hostScript; //Soldier or WarBulding
        protected bool _isActive; //buff is active or not
        protected List<BFD.BuffTriggerType> _triggerType = null;

        public bool gs_isActive
        {
            get { return _isActive; }
        }

        public Buff(object target)
        {
            _hostScript = target;
            _isActive = false;
        }

        //some buff's effect affect soldier in this function, but some in Solider.BeAffectedByBuff()
        //callback: when buff be triggered call the callback
        public virtual bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            _isActive = true;
            return true;
        }
        //重载
        public virtual bool ActiveBuff<TValue, TRet>(in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            _isActive = true;
            return true;
        }

        //can not called by self
        public virtual void DeactiveBuff()
        {
            _isActive = false;
        }
        public virtual void UpdateBuff(){ }
        //before be attack
        //damage: the hit from the rival
        public virtual float BuffBeAttackPre(float damage, object rival, WE.WarEleType rivalType)
        {
            return damage;
        }

        //after be attack
        //damage: the hp the self actually dropped
        //isDead: is self dead
        public virtual void BuffBeAttackPost(float damage, bool isDead, object rival, WE.WarEleType rivalType) { }

        //before do attack
        //damage: the hit try to send to rival
        //type: the target type (soldier or building)
        public virtual float BuffDoAttackPre(float hit, object rival, WE.WarEleType rivalType)
        {
            return hit;
        }

        //after do attack
        //damage: the hp the rival actually dropped
        //isDead: is rival dead
        //type: the target type (soldier or building)
        public virtual void BuffDoAttackPost(float hit, bool isDead, object rival, WE.WarEleType rivalType) { }
        //dieCOunt: 范围性攻击死亡个数
        //rivalSdList: 受到攻击的Soldiers
        //rivalBdList: 受到攻击的buildings
        public virtual void BuffDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList) { }

        //when solider die , the Buff mya only effect other solders
        //rivalList parterList: the rival or parter in the Buff collider range
        public virtual void BuffDie(){ }

        //check whither can add this kind of buff
        public virtual bool CanAddBuff(object value = null)
        {
            return true;
        }

        public virtual bool HasBuff(object value)
        {
            return _isActive;
        }

        //only stop part of the buff, can not run UnregisterBuff in it
        public virtual void StopPartOfBuff<TValue>(in TValue value){ }
    }

    public class SoldierBuff : Buff
    {
        public BFD.SoldierBuffType p_sdBuffType;
        protected Soldier _hostSoldier;

        public SoldierBuff(object target) : base(target)
        {
            _hostType = WE.WarEleType.SOLDIER;
            _hostSoldier = (Soldier)target;
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            for (int i = 0; i < _triggerType.Count; i++)
                _hostSoldier.RegisterBuff(this, _triggerType[i], p_sdBuffType);
            return base.ActiveBuff(in value);
        }

        public override bool ActiveBuff<TValue, TRet>(in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            for (int i = 0; i < _triggerType.Count; i++)
                _hostSoldier.RegisterBuff(this, _triggerType[i], p_sdBuffType);
            return base.ActiveBuff(in value, ref buffRet, callback);
        }

        public override void DeactiveBuff()
        {
            if(_isActive == false)
                return;

            if (_triggerType != null)
            {
                for (int i = 0; i < _triggerType.Count; i++)
                    _hostSoldier.UnregisterBuff(this, _triggerType[i]);
            }

            base.DeactiveBuff();
        }
    }

    public class WarBuildingBuff : Buff
    {
        public BFD.WarBuildingBuffType p_bdBuffType;
        protected WarBuilding _hostBuilding;

        public WarBuildingBuff(object target) : base(target)
        {
            _hostType = WE.WarEleType.BUILDING;
            _hostBuilding = (WarBuilding)target;
        }

        public override bool ActiveBuff<TValue>(in TValue value, BuffUnsafeCallback callback = null)
        {
            for (int i = 0; i < _triggerType.Count; i++)
                _hostBuilding.RegisterBuff(this, _triggerType[i], p_bdBuffType);
            return base.ActiveBuff(in value);
        }

        public override bool ActiveBuff<TValue, TRet>(in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            for (int i = 0; i < _triggerType.Count; i++)
                _hostBuilding.RegisterBuff(this, _triggerType[i], p_bdBuffType);
            return base.ActiveBuff(in value, ref buffRet, callback);
        }

        public override void DeactiveBuff()
        {
            if(_isActive == false)
                return;

            if (_triggerType != null)
            {
                for (int i = 0; i < _triggerType.Count; i++)
                    _hostBuilding.UnregisterBuff(this, _triggerType[i]);
            }

            base.DeactiveBuff();
        }
    }
}

