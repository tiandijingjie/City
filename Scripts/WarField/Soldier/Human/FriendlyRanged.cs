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
        [SerializeField] protected SD.RemoteAttackStartPosition _shootPos; //出手射击的位置

        protected Vector3 _shootOffset; //bullet start positon offset according to the transform.position
        protected long _weaponId;

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
            if (_shootPos == SD.RemoteAttackStartPosition.OVERHEAD)
                _shootOffset = _halfBodySize * 2;
            else if (_shootPos == SD.RemoteAttackStartPosition.OVERSHOULDER)
            {
                var value = _halfBodySize * 2;
                _shootOffset = new Vector2(value.x, value.y * 2 / 3); //从2/3高度位置出手
            }

            if (_weaponPfb != null)
                _weaponId = WeaponCtrl.Instance.GetWeaponID(_race, (long)_troopType, (long)_sdType, 1, WE.WarEleType.SOLDIER);
        }

#endregion

#region public functions

        public override void RemoteRangedAttack(float damage, MonoBehaviour rivalScript, WE.WarEleType rivalType)
        {
            Projectile bt = WeaponCtrl.Instance.GetProjectile(_race, _weaponId, _weaponPfb);
            Vector3 startPos = _transform.position + _shootOffset;
            bt.gs_transform.position = startPos;
            Vector2 targetPos = Vector2.zero;
            if (rivalType == WE.WarEleType.SOLDIER) //shell
                targetPos = ((Soldier)rivalScript).gs_bullectTargetPos;
            else if (rivalType == WE.WarEleType.BUILDING)
                targetPos = ((WarBuilding)rivalScript).gs_bullectTargetPos;

            //default for ProjectileTypes.BULLET only, for SHELL  solider need inplemente it self
            bt.InitProjectile(gameObject, WE.WarEleType.SOLDIER, this, startPos, _rival, rivalType, rivalScript, targetPos, 20f, _faction, damage, _mapId);
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
