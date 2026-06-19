using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    //多重箭塔: 攻击多个敌人,但是无法攻击建筑
    public class VolleyTower : DefenceBuilding
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private int _targetNum;

        private DataList<GameObject> _rivalList;
        private DataList<MonoBehaviour> _rivalScriptList;
        private object _listLock = new object();

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _rivalList = new DataList<GameObject>(false, true); //不加锁，使用独立锁_listLock
            _rivalScriptList = new DataList<MonoBehaviour>(false, false); //allowDuplicate==false 是因为_rivalList已经保证了不会重复
        }

#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            if(base.InitBuilding(conf, mapId) == false)
                return false;
            _canAttackBuilding = false;
            _targetNum = (int)_defenceConf.gs_specConfs["targetNum"];
            return true;
        }

        public override void TargetRemove(WarFieldElements.WarEleType targetType, GameObject target)
        {
            if(_beInited == false)
                return;

            lock (_listLock)
            {
                int index = _rivalList.GetIndex(target);
                if (index == -1)
                    return;
                _rivalList.RemoveItemAt(index);
                _rivalScriptList.RemoveItemAt(index);
            }

            if (ChooseRivals() == 0) //没有攻击目标
                _hasRival = false;
        }

        public override void RivalInRange(GameObject colTarget, WarFieldElements.WarEleType colType, WE.FactionType faction)
        {
            base.RivalInRange(colTarget, colType, faction);
            if (ChooseRivals() > 0)
                _hasRival = true;
        }

        public override void RivalOutRange(GameObject colTarget, WarFieldElements.WarEleType colType, WarFieldElements.FactionType faction)
        {
            base.RivalOutRange(colTarget, colType, faction);
            TargetRemove(colType, colTarget);
        }

#endregion

#region private functions

        protected override void Attack()
        {
            _nextAttackInterval += _defenceConf.gs_atkSpeedCycle;

            lock (_listLock)
            {
                int count = _rivalList.Count;
                for (int i = 0; i < count; i++)
                {
                    Vector2 startPos = (Vector2)(_transform.position + _shootOffset);
                    Soldier rivalSd = (Soldier)_rivalScriptList.GetByIndex(i);
                    Vector2 targetPos = rivalSd.gs_bullectTargetPos;
                    int targetGridIndex = rivalSd.gs_gridIndex;
                    WeaponCtrl.Instance.FireBezierBullet(
                        _weaponId, _defenceConf.gs_faction, _defenceConf.gs_damage,
                        _mapId, (int)WE.WarEleType.BUILDING, gs_gridIndex,
                        (int)WE.WarEleType.SOLDIER, targetGridIndex, true,
                        startPos, targetPos, 20f, 20f, _weaponPfb);
                }
            }
        }

        protected int ChooseRivals()
        {
            lock (_listLock)
            {
                if(_rivalList.Count == _targetNum)
                    return _targetNum;

                var list = _rivalInRange[(int)WE.WarEleType.SOLDIER];
                //Lamda
                list.ForList(static (loopList, self) =>
                {
                    for (int i = 0; i < loopList.Count; i++)
                    {
                        if (self._rivalList.Contains(loopList[i]) == true) //已经在攻击列表里了
                            continue;

                        Soldier soldier = loopList[i].GetComponent<Soldier>();
                        if (soldier == null)
                        {
                            GameLogger.LogError($"Can not get Soldier script from gameobject {loopList[i].name}");
                            continue;
                        }

                        self._rivalList.AddItem(loopList[i]);
                        self._rivalScriptList.AddItem(soldier);
                        if (self._rivalList.Count == self._targetNum)
                            return;
                    }
                    return;
                }, this);//Lamda
                return _rivalList.Count;
            }
        }

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            if (changeName == "targetNum") //攻击数目改变，只支持增加不支持减少
            {
                _targetNum = (int)_defenceConf.gs_specConfs["targetNum"];
                ChooseRivals();
            }
        }

#endregion
    }
}

