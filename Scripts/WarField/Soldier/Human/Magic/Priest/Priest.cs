using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Priest;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    //牧师
    public class Priest : FriendlySupport
    {
#region public parameters

#endregion

#region private parameters

        private bool _inMartyrdom;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions


        /* ------- Skill Martyrdom ------- */
        public void EnterMartyrdom()
        {
            _inMartyrdom = true;
        }

        public void ExitMartyrdom()
        {
            _inMartyrdom = false;
            ChangeStatusTo(SD.SoldierStatus.DIE);
        }

        public override bool BeAttacked(GameObject attacker, MonoBehaviour attackScript, WarFieldElements.WarEleType attackerT, float damage,
            bool triggerSkill, bool triggerBuff, out float hitValue)
        {
            if (_inMartyrdom == true) //献祭状态不受伤害
            {
                hitValue = 0;
                return true; //return true, 进入假死状态，不会再被攻击,但是仍可能被后续过来的rival选择成目标
            }

            return base.BeAttacked(attacker, attackScript, attackerT, damage, triggerSkill, triggerBuff, out hitValue);
        }

        public override bool BeAffectedByBuff<TValue, TRet>(BuffDefines.SoldierBuffType type, in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            if (_inMartyrdom == true) //献祭状态不受buff
                return false;
            return base.BeAffectedByBuff(type, in value, ref buffRet, callback);
        }

        public override bool BeAffectedByBuff<TValue>(BuffDefines.SoldierBuffType type, in TValue value, BuffUnsafeCallback callback = null)
        {
            if (_inMartyrdom == true) //献祭状态不受buff
                return false;
            return base.BeAffectedByBuff(type, in value, callback);
        }
        /* ------- Skill Martyrdom ------- */

        /* ------- Skill Life Share ------- */
        public void ShareLife(float value)
        {
            _curState.p_health -= value;
            if (_curState.p_health <= 0) //keep self not die
                _curState.p_health = 1f;
        }

        /* ------- Skill Life Share ------- */

#endregion

#region private functions

        protected override bool InitSoldier(int spawnIndex, byte mapId)
        {
            var data = SoldierCtrl.Instance.GetSdIndividualData(_race, _troopType, (int)_sdType);
            if (data != null)
            {
                if (_sdConfBeInit == false)
                {
                    _oriIndividualData = new PriestIndividualData((PriestIndividualData)data);
                    _curIndividualData = new PriestIndividualData((PriestIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((PriestIndividualData)data);
                    _curIndividualData.ReInitIndividualData((PriestIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get priest individual data");
            }

            _inMartyrdom = false;
            return base.InitSoldier(spawnIndex, mapId);
        }

#endregion
    }
}

