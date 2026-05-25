using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;

namespace WarUpgrade
{
    using UD = UpgradeDefine;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using BFD = BuffDefines;

    public class AddSoldierMoveInCaveUpgrade : UpgradeBase, ISoldierProductionNotify
    {
#region public parameters

#endregion

#region private parameters

        private float _moveUp;
        private DataPool<SoldierIntoCaveCallBack> _callbackPool;

#endregion

#region private parameters' get set

        public override UpgradeDefine.UpgradeType gs_type
        {
            get { return UD.UpgradeType.ADDSOLDIERMOVEINCAVE; }
        }

#endregion

#region Unity callbacks

#endregion

#region public functions
        //士兵生产出来之后添加上地图改变时的回调
        public void SoldierProduceIntf(WarFieldElements.FactionType faction, SoldierDefines.TroopType troop, Soldier soldier, Vector2 pos)
        {
            SoldierIntoCaveCallBack callback = GetCallbackFromPool();
            soldier.RegisterMapChangeObserver(callback);
        }

#endregion

#region private functions

        protected override bool OnImplementUpgrade()
        {
            _moveUp = p_node.ConvertSpecificToNum("moveAdd");
            if (_moveUp < 0)
                return false;
            _moveUp += 1;

            _callbackPool = new DataPool<SoldierIntoCaveCallBack>(true);
            SoldierCtrl.Instance.RegisterSoliderProduceNotify(this, WE.FactionType.FRIENDLY, SD.TroopType.Melee);
            SoldierCtrl.Instance.RegisterSoliderProduceNotify(this, WE.FactionType.FRIENDLY, SD.TroopType.Ranged);
            SoldierCtrl.Instance.RegisterSoliderProduceNotify(this, WE.FactionType.FRIENDLY, SD.TroopType.Magic);
            return true;
        }

        private SoldierIntoCaveCallBack GetCallbackFromPool()
        {
            var ret = _callbackPool.PopOut();
            if (ret != null)
                return ret;

            return new SoldierIntoCaveCallBack(_moveUp, this);
        }

        private void ReleaseCallbackToPool(SoldierIntoCaveCallBack callback)
        {
            _callbackPool.AddItem(callback);
        }

#endregion

        private class SoldierIntoCaveCallBack : IOnMapChange
        {
            private AddSoldierMoveInCaveUpgrade _parent;
            private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _moveObj;
            private object _buffRef = null;

            public SoldierIntoCaveCallBack(float moveAdd, AddSoldierMoveInCaveUpgrade parent)
            {
                _parent = parent;
                _moveObj = (SD.StateSoldierEffectType.MOVESPEED, moveAdd, GD.CalDeltaType.MUL, -1.0f, "AddSoldierMoveInCaveUpgrade",
                    BFD.BuffStrategy.OVERRIDE, (object)this);
            }

            public void OnMapChange(WarEleParent obj, int fromMap, int toMap)
            {
                //不判断,在添加SoldierIntoCaveCallBack已经判断过了
                // if(obj.gs_warEleType != WE.WarEleType.SOLDIER)
                //     return;
                //
                // if(obj.gs_faction != WE.FactionType.FRIENDLY)
                //     return;
                Soldier soldier = obj as Soldier;
                if (fromMap == WE.OnGroundMapIndex && toMap != WE.OnGroundMapIndex) //从地面进入洞穴
                    soldier.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _moveObj, ref _buffRef);

                //从洞穴回到地面, 士兵init的时候也会调用,所有需要判断_buffRef
                else if (fromMap != WE.OnGroundMapIndex && toMap == WE.OnGroundMapIndex && _buffRef != null)
                {
                    soldier.StopPartOfBuff(BFD.SoldierBuffType.STATE, in _buffRef);
                    _buffRef = null;
                }
            }

            //soldier die
            public void ReleaseInterface()
            {
                _buffRef = null;
                _parent.ReleaseCallbackToPool(this);
            }
        }
    }
}
