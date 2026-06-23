using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Cannoneer;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    //巨炮手
    public class Cannoneer : FriendlyRanged
    {
#region public parameters

#endregion

#region private parameters
        private float _shellAttackRange = 1.5f;
        private float _otherDamage = 0;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions


        public override void RemoteRangedAttack(float damage, MonoBehaviour rivalScript, WarFieldElements.WarEleType rivalType)
        {
            Vector2 startPos = _firePos != null ? _firePos.GetFirePos(_transform.position, _currentDirIndex) : (Vector2)_transform.position;
            Vector2 targetPos = (Vector2)rivalScript.transform.position;
            int targetGridIndex = ((WarEleParent)rivalScript).gs_gridIndex;
            WeaponCtrl.Instance.FireBezierShell(
                _weaponId, _faction, damage,
                _mapId, (int)WE.WarEleType.SOLDIER, gs_gridIndex,
                (int)rivalType, targetGridIndex, true,
                startPos, targetPos,
                _shellAttackRange, _otherDamage, _weaponPfb);
        }

        //Talent设置范围攻击的范围
        public void SetShellAttackRange(float range)
        {
            _shellAttackRange = range;
        }

        //Talent设置范围攻击除主目标外其他目标的攻击衰减
        public void SetDamageDown(float percent)
        {
            _otherDamage = _curState.p_damage * percent;
        }
#endregion

#region private functions
        protected override bool InitSoldier(int spawnIndex, byte mapId)
        {
            var data = SoldierCtrl.Instance.GetSdIndividualData(_race, _troopType, (int)_sdType);
            if (data != null)
            {
                if (_sdConfBeInit == false)
                {
                    _oriIndividualData = new CannoneerIndividualData((CannoneerIndividualData)data);
                    _curIndividualData = new CannoneerIndividualData((CannoneerIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((CannoneerIndividualData)data);
                    _curIndividualData.ReInitIndividualData((CannoneerIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get cannoneer individual data");
            }

            return base.InitSoldier(spawnIndex, mapId);
        }
#endregion
    }
}

