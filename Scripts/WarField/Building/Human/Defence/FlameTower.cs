using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;
    using WE = WarFieldElements;

    public class FlameTower : DefenceBuilding
    {
#region public parameters

#endregion

#region private parameters

        private float _flameDamagePerSec;
        private float _flameDuration;
        private object _flameObj;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            base.InitBuilding(conf, mapId);
            _canAttackBuilding = false;
            _flameDamagePerSec = _defenceConf.gs_specConfs["flameDamage"];
            _flameDuration = _defenceConf.gs_specConfs["flameDuration"];
            _flameObj = (_flameDamagePerSec, _flameDuration);
            return true;
        }

        public override void ShellHit(float hitValue, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            EffectCtrl.Instance.AddEffectAt(target.transform.position, ED.EffectType.FLAMEFROUND, _mapId, _flameObj);
        }

#endregion

#region private functions
        protected override void Attack()
        {
            if(_canWork == false)
                return;

            Projectile bt = WeaponCtrl.Instance.GetProjectile(_defenceConf.gs_race, _weaponId, _weaponPfb);
            Vector3 startPos = _transform.position + _shootOffset;
            bt.gs_transform.position = startPos;
            Vector2 targetPos = ((Soldier)_rivalScript).gs_bullectTargetPos;

            bt.InitProjectile(gameObject, WE.WarEleType.BUILDING, this, startPos, _rival, _rivalType, _rivalScript, targetPos, 10f,
                _defenceConf.gs_faction, _defenceConf.gs_damage, _defenceConf.gs_weaponRange, _defenceConf.gs_damage, _mapId);
            ((Shell)bt).CanAttackBuilding(false);
        }

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            switch (changeName)
            {
                case "flameDamage":
                    _flameDamagePerSec = _defenceConf.gs_specConfs["flameDamage"];
                    _flameObj = (_flameDamagePerSec, _flameDuration);
                    break;
                case "flameDuration":
                    _flameDuration = _defenceConf.gs_specConfs["flameDuration"];
                    _flameObj = (_flameDamagePerSec, _flameDuration);
                    break;
                default:
                    break;
            }
        }

#endregion
    }
}

