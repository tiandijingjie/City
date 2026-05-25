using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;

    //逃跑  生命低于30%时,增加移动速度,生命值恢复到45%恢复速度  移动速度增加:50%
    public class HeroFleeTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentFlee _curTalent = null;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _buffObj;
        private bool _isTriggered = false;
        private object _buffIndexer = null;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER };
            for (int i = 0; i < _triggerType.Length; i++)
                _soldier.RegisterTalent(this, _triggerType[i]);  //talent need to do register during awake by itself
        }

#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentFlee)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.FLEE];
            _name = _curTalent.GetDescription().p_name;

            _buffObj = (SD.StateSoldierEffectType.MOVESPEED, _curTalent.p_moveUp, GD.CalDeltaType.MUL, -1f, "HeroFleeTalent",
                BFD.BuffStrategy.OVERRIDE, (object)this);
            _isTriggered = false;
            _buffIndexer = null;
            return true;
        }

        protected override void OnTalentUpdate()
        {
            if(_isTriggered == false)
            {
                if (_soldier.gs_curState.p_health < (_soldier.gs_curState.p_maxHealth * _curTalent.p_hpThreshold))
                {
                    if(_soldier.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _buffObj, ref _buffIndexer) == true)
                        _isTriggered = true;
                }
            }
            else
            {
                if (_soldier.gs_curState.p_health > (_soldier.gs_curState.p_maxHealth * _curTalent.p_hpRecover))
                {
                    var value = (_buffIndexer, (object)this);
                    _soldier.StopPartOfBuff(BFD.SoldierBuffType.STATE, in value); //停止buff
                    _isTriggered = false;
                }
            }
        }

#endregion
    }
}
