using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using BFD = BuffDefines;

    //冰霜塔: 给敌人附加冰霜，受到持续伤害
    public class FrostTower : DefenceBuilding
    {
#region public parameters

#endregion

#region private parameters

        private float _frostDamagePerSec;//冰霜的每秒伤害
        private float _frostMoveSpeedDown; //移动速度降低
        private float _frostDuration; //持续时间
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _moveObj;
        private (float, float, string, object, BFD.BuffStrategy) _damgeObj;

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
            _frostDamagePerSec = _defenceConf.gs_specConfs["frostDamage"];
            _frostMoveSpeedDown = 1 - _defenceConf.gs_specConfs["moveDown"];
            _frostDuration = _defenceConf.gs_specConfs["duration"];
            _moveObj = (SD.StateSoldierEffectType.MOVESPEED, _frostMoveSpeedDown, GD.CalDeltaType.MUL, _frostDuration, "FrostTower",
                BFD.BuffStrategy.OVERRIDE, (object)this);
            _damgeObj = (_frostDamagePerSec, _frostDuration, "FrostTower", (object)this, BFD.BuffStrategy.OVERRIDE);
            return true;
        }

        //施加诅咒
        public override void BullectHit(float hitValue, bool isDie, WarFieldElements.WarEleType type, object rivalScript)
        {
            if(isDie == true)
                return;

            Soldier soldier = (Soldier)rivalScript;
            soldier.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _moveObj);
            soldier.BeAffectedByBuff(BFD.SoldierBuffType.DURATIONDAMAGE, in _damgeObj);
        }

#endregion

#region private functions

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            if (changeName == "moveDown")
            {
                _frostMoveSpeedDown = 1 - _defenceConf.gs_specConfs["moveDown"];
                _moveObj = (SD.StateSoldierEffectType.MOVESPEED, _frostMoveSpeedDown, GD.CalDeltaType.MUL, _frostDuration, "FrostTower",
                    BFD.BuffStrategy.OVERRIDE, (object)this);
            }
        }

#endregion
    }
}

