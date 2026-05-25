using System;
using System.Collections;
using System.Collections.Generic;
using SharpShooter;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using WD = WeaponDefines;
    using GD = GlobalDefines;
    using SKD = SkillDefines;

    //神射手
    public class SharpShooter : FriendlyRanged
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private GameObject _skillArrow;

        private long _skillWeaponId; //蓄力击

        private bool _needAttackSecondRival;
        private float _secondAttackDamageTimes;
        private SearchClosest _searchSecondRival;
        private int _excludeId;
        private float _mainDamage; //为了传递给第个箭使用
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _skillWeaponId = WeaponCtrl.Instance.GetWeaponID(_race, (long)_troopType, (long)_sdType, 2, WE.WarEleType.SOLDIER);
            _searchSecondRival = new SearchClosest(0, OnSecondTargetFound, GetSearchShape, this, -1); //因为是同步查找,所以不用注册
            _searchSecondRival.p_getExcludeCall = GetExcludeIndices;
            SearchConditionUtil.AddEnemySoldierConditions(_searchSecondRival);
        }
#endregion

#region public functions



#endregion

#region private functions
        protected override bool InitSoldier(int spawnIndex, byte mapId)
        {
            var data = SoldierCtrl.Instance.GetSdIndividualData(_race, _troopType, (int)_sdType);
            if (data != null)
            {
                if (_sdConfBeInit == false)
                {
                    _oriIndividualData = new SharpShooterIndividualData((SharpShooterIndividualData)data);
                    _curIndividualData = new SharpShooterIndividualData((SharpShooterIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((SharpShooterIndividualData)data);
                    _curIndividualData.ReInitIndividualData((SharpShooterIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get sharpshooter individual data");
            }
            _needAttackSecondRival = false;
            _secondAttackDamageTimes = 0;
            _searchSecondRival.p_mapId = mapId;
            return base.InitSoldier(spawnIndex, mapId);
        }

        protected override void StepIntoMap(int fromMap, int toMap, float targetY)
        {
            base.StepIntoMap(fromMap, toMap, targetY);
            _searchSecondRival.p_mapId = toMap; //因为是同步查找,所以不用注册
        }

        //打击第二个敌人
        //因为机制原因，其实不会在这一次生效，而是会在下一次调用RemoteRangedAttack攻击第二个敌人，也就是这一次做判断，下一次生效
        public void SecondShoot(float damageTimes)
        {
            _needAttackSecondRival = true;
            _secondAttackDamageTimes = damageTimes;
        }

        public override void RemoteRangedAttack(float damage, MonoBehaviour rivalScript, WE.WarEleType rivalType)
        {
            Projectile bt = WeaponCtrl.Instance.GetProjectile(_race, _weaponId, _weaponPfb);
            Vector3 startPos = _transform.position + _shootOffset;
            bt.gs_transform.position = startPos;
            Vector2 targetPos = Vector2.zero;
            if (rivalType == WE.WarEleType.SOLDIER) //shell
                targetPos = ((Soldier)rivalScript).gs_bullectTargetPos;
            else if(rivalType == WE.WarEleType.BUILDING)
                targetPos = ((WarBuilding)rivalScript).gs_bullectTargetPos;

            //default for ProjectileTypes.BULLET only, for SHELL  solider need inplemente it self
            bt.InitProjectile(gameObject, WE.WarEleType.SOLDIER, this, startPos, _rival, rivalType, rivalScript, targetPos, 20f, _faction, damage, _mapId);

            if (_needAttackSecondRival == true)
            {
                // Find a second enemy soldier in attack range (excluding the primary rival)
                _excludeId = (_rivalType == WE.WarEleType.SOLDIER && _rivalScript != null) ? _rivalScript.gs_gridIndex : -1;
                _mainDamage = damage;
                SearchManager.Instance.RegisterSearch(_searchSecondRival);
            }
        }

        private void GetExcludeIndices(ref Unity.Collections.FixedList64Bytes<int> excludeList)
        {
            //不需要clean
            if(_excludeId >= 0)
                excludeList.Add(_excludeId);
        }

        private void OnSecondTargetFound(IGridNode target, float distance)
        {
            if (target != null)
            {
                Vector3 startPos = _transform.position + _shootOffset;
                Soldier secondTarget = (Soldier)target;
                Projectile bt2 = WeaponCtrl.Instance.GetProjectile(_race, _weaponId, _weaponPfb);
                bt2.gs_transform.position = startPos;
                bt2.InitProjectile(gameObject, WE.WarEleType.SOLDIER, this, startPos,
                    secondTarget.gameObject, WE.WarEleType.SOLDIER, secondTarget,
                    secondTarget.gs_bullectTargetPos, 20f, _faction, _mainDamage * _secondAttackDamageTimes, _mapId, false);
                _needAttackSecondRival = false;
            }
        }

        //射出蓄力击
        //TODO 需要每一帧去计算箭头范围内的敌人然后造成伤害
        public void SharpShootShootChargeStrike(float distance, float damage)
        {
            // Projectile bt = WeaponCtrl.Instance.GetProjectile(_race, _skillWeaponId, _skillArrow);
            // Vector3 startPos = _transform.position + _shootOffset;
            // bt.gs_transform.position = startPos;
            //
            // Vector2 targetPos;
            // if (_rivalType == WE.WarEleType.SOLDIER)
            //     targetPos = Utils.CalPosAtDir(startPos, ((Soldier)_rivalScript).gs_bullectTargetPos, distance);
            // else //target is building,so choose a soldier as a target
            // {
            //     Soldier sd = FindClosestEnemySoldierInRange(-1);
            //     if(sd == null)
            //         return;
            //     targetPos = Utils.CalPosAtDir(startPos, sd.gs_bullectTargetPos, distance);
            // }
            // bt.InitProjectile(gameObject, WE.WarEleType.SOLDIER, this, startPos, null, _rivalType, null, targetPos, 20f, _faction, damage, _mapId);
        }
#endregion
    }
}

