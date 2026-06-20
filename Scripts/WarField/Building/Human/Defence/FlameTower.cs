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
            if (_canWork == false)
                return;

            Vector2 startPos = (Vector2)(_transform.position + _shootOffset);
            Vector2 targetPos = ((Soldier)_rivalScript).gs_bullectTargetPos;
            int targetGridIndex = ((WarEleParent)_rivalScript).gs_gridIndex;

            WeaponCtrl.Instance.FireBezierShell(
                _weaponId, _defenceConf.gs_faction, _defenceConf.gs_damage,
                _mapId, (int)WE.WarEleType.BUILDING, gs_gridIndex,
                (int)WE.WarEleType.SOLDIER, targetGridIndex, true,
                startPos, targetPos,
                _defenceConf.gs_weaponRange, _defenceConf.gs_damage, _weaponPfb, false);
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

