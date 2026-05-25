using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    //攻城塔
    public class SiegeTower : DefenceBuilding
    {
#region public parameters

#endregion

#region private parameters

        private float _bdDamage;
        private GD.DirDef _attackDir;//攻击的方向,
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _attackDir = GD.DirDef.RDir;
            _bdAnimator.transform.localScale = new Vector3(Mathf.Abs(_bdAnimator.transform.localScale.x), _bdAnimator.transform.localScale.y,
                _bdAnimator.transform.localScale.z);
            base.InitBuilding(conf, mapId);
            _canAttackBuilding = true;
            _bdDamage = (_defenceConf.gs_specConfs["siegeDamageUp"] + 1) * _defenceConf.gs_damage;
            return true;
        }

#endregion

#region private functions

        protected override void Attack()
        {
            if (_rival.transform.position.x >= _transform.position.x)
            {
                if (_attackDir != GD.DirDef.RDir)
                {
                    _attackDir = GD.DirDef.RDir;
                    _bdAnimator.transform.localScale = new Vector3(-_bdAnimator.transform.localScale.x, _bdAnimator.transform.localScale.y,
                        _bdAnimator.transform.localScale.z);
                }
            }
            else
            {
                if (_attackDir != GD.DirDef.LDir)
                {
                    _attackDir = GD.DirDef.LDir;
                    _bdAnimator.transform.localScale = new Vector3(-_bdAnimator.transform.localScale.x, _bdAnimator.transform.localScale.y,
                        _bdAnimator.transform.localScale.z);
                }
            }

            Projectile bt = WeaponCtrl.Instance.GetProjectile(_defenceConf.gs_race, _weaponId, _weaponPfb);
            Vector3 startPos = _transform.position + _shootOffset;
            bt.gs_transform.position = startPos;
            Vector2 targetPos = Vector2.zero;
            float damage = 0;
            if (_rivalType == WE.WarEleType.SOLDIER)
            {
                targetPos = ((Soldier)_rivalScript).gs_bullectTargetPos;
                damage = _defenceConf.gs_damage;
            }
            else if (_rivalType == WE.WarEleType.BUILDING)
            {
                targetPos = ((WarBuilding)_rivalScript).gs_bullectTargetPos;
                damage = _bdDamage;
            }

            bt.InitProjectile(gameObject, WE.WarEleType.BUILDING, this, startPos, _rival, _rivalType, _rivalScript, targetPos, 20f,
                _defenceConf.gs_faction, damage, _mapId);
        }

        protected override bool ChooseRival()
        {
            bool isFound = false;
            if(_rivalInRange[(int)WE.WarEleType.BUILDING].Count > 0) //优先选择建筑
            {
                isFound = _rivalInRange[(int)WE.WarEleType.BUILDING].FindFirst(static (rival, self) =>
                {
                    self._rival = rival;
                    self._rivalScript = rival.GetComponent<WarBuilding>();
                    self._rivalType = WE.WarEleType.BUILDING;
                    self._hasRival = true;
                    return true;
                }, this, out var result);
            }
            if(isFound == true)
                return true;

            if (_rivalInRange[(int)WE.WarEleType.SOLDIER].Count > 0)
            {
                isFound = _rivalInRange[(int)WE.WarEleType.SOLDIER].FindFirst(static (rival, self) =>
                {
                    self._rival = rival;
                    self._rivalScript = rival.GetComponent<Soldier>();
                    self._rivalType = WE.WarEleType.SOLDIER;
                    self._hasRival = true;
                    return true;
                }, this, out var result);
            }
            return isFound;
        }

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            switch (changeName)
            {
                case "siegeDamageUp":
                    _bdDamage = (_defenceConf.gs_specConfs["siegeDamageUp"] + 1) * _defenceConf.gs_damage;
                    break;
                default:
                    break;
            }
        }

#endregion
    }
}

