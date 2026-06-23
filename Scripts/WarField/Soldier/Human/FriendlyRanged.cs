using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using WD = WeaponDefines;
    using GD = GlobalDefines;

    public class FriendlyRanged : FriendlySoldier
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected HumanDefines.RangedType _sdType;
        [SerializeField] protected GameObject _weaponPfb = null;

        protected WD.WeaponId _weaponId = WD.WeaponId.MIN;

#endregion

#region private parameters' get set

        public override int gs_sdType
        {
            get { return (int)_sdType; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _enableSearchCollider = false;
            _isRemote = true;

            if (_weaponPfb == null)
                GameLogger.LogError($"{_sdName}, not set weapon prefab");
        }

#endregion

#region public functions

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
        }

#endregion

#region private functions
        protected override bool InitSoldier(int spawnIndex, byte mapId)
        {
            if (_sdConfBeInit == false)
            {
                _sdConf = new SoldierConf(SoldierCtrl.Instance.GetSdConf(_race, _troopType, (int)_sdType));
                _sdConfBeInit = true;
            }
            else
            {
                _sdConf.ReInitSoldierConf(SoldierCtrl.Instance.GetSdConf(_race, _troopType, (int)_sdType));
            }
            _entitySubType = WE.EncodeEntitySubType((byte)_race, (byte)_troopType, (byte)_sdType, 0);
            return base.InitSoldier(spawnIndex, mapId);
        }

        protected override void OnSoldierDie()
        {
            base.OnSoldierDie();

            SoldierCtrl.Instance.RemoveSoldier(_race, _troopType, (int)_sdType, this, _sdConf.p_level, _mapId);
        }

#endregion
    }
}
