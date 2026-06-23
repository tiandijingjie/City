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

        [SerializeField] private WD.WeaponId _skillWeaponId; //蓄力击

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
            Vector2 startPos = _firePos != null ? _firePos.GetFirePos(_transform.position, _currentDirIndex) : (Vector2)_transform.position;
            Vector2 targetPos = Vector2.zero;
            if (rivalType == WE.WarEleType.SOLDIER)
                targetPos = ((Soldier)rivalScript).gs_bullectTargetPos;
            else if (rivalType == WE.WarEleType.BUILDING)
                targetPos = ((WarBuilding)rivalScript).gs_bullectTargetPos;

            int targetGridIndex = ((WarEleParent)rivalScript).gs_gridIndex;
            WeaponCtrl.Instance.FireBullet(
                _weaponId, _faction, damage,
                _mapId, (int)WE.WarEleType.SOLDIER, gs_gridIndex,
                (int)rivalType, targetGridIndex, true,
                startPos, targetPos, _weaponPfb);

            if (_needAttackSecondRival == true)
            {
                // Find a second enemy soldier in attack range (excluding the primary rival)
                _excludeId = (_rivalType == WE.WarEleType.SOLDIER && _rivalScript != null) ? ((WarEleParent)_rivalScript).gs_gridIndex : -1;
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
                Vector2 startPos = _firePos != null ? _firePos.GetFirePos(_transform.position, _currentDirIndex) : (Vector2)_transform.position;
                Soldier secondTarget = (Soldier)target;
                WeaponCtrl.Instance.FireBullet(
                    _weaponId, _faction, _mainDamage * _secondAttackDamageTimes,
                    _mapId, (int)WE.WarEleType.SOLDIER, gs_gridIndex,
                    (int)WE.WarEleType.SOLDIER, secondTarget.gs_gridIndex, false,
                    startPos, secondTarget.gs_bullectTargetPos, _weaponPfb);
                _needAttackSecondRival = false;
            }
        }

        //射出蓄力击 (线性穿透，无特定目标)
        //TODO 需要每一帧去计算箭头范围内的敌人然后造成伤害
        public void SharpShootShootChargeStrike(float distance, float damage)
        {
            Vector2 startPos = _firePos != null ? _firePos.GetFirePos(_transform.position, _currentDirIndex) : (Vector2)_transform.position;
            Vector2 referencePos;

            if (_rivalScript == null)
                return;

            if (_rivalType == WE.WarEleType.SOLDIER)
                referencePos = ((Soldier)_rivalScript).gs_bullectTargetPos;
            else if (_rivalType == WE.WarEleType.BUILDING)
                referencePos = ((WarBuilding)_rivalScript).gs_bullectTargetPos;
            else
                return;

            Vector2 direction = (referencePos - startPos).normalized;

            WeaponCtrl.Instance.FireLinearNoTarget(
                _skillWeaponId, _faction, damage,
                _mapId, (int)WE.WarEleType.SOLDIER, gs_gridIndex,
                startPos, direction, distance,
                0.3f, _skillArrow);
        }
#endregion
    }
}

