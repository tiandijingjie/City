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

    //敌对  英雄的血量降低到阈值时大量增加英雄的回血速度   血量：20%   回血增加:20
    //持续时间:10s  冷却:50s
    public class HeroHostileInstinctTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentHostileInstinct _curTalent = null;
        private int _intervalCycle;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _buffObj;
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
                _curTalent = (HeroGenericIndividualData.TalentHostileInstinct)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.HOSTILEINSTINCT];
            _name = _curTalent.GetDescription().p_name;

            _intervalCycle = 0;
            _buffObj = (SD.StateSoldierEffectType.HPINC, _curTalent.p_hpInc, GD.CalDeltaType.ADD, _curTalent.p_duration,
                "HeroHostileInstinctTalent", BFD.BuffStrategy.OVERRIDE, (object)this);
            return true;
        }

        protected override void OnTalentUpdate()
        {
            if (_intervalCycle > 0)
                _intervalCycle--;
            else
            {
                if (_soldier.gs_curState.p_health < (_soldier.gs_curState.p_maxHealth * _curTalent.p_hpThreshold))
                {
                    _soldier.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _buffObj);
                    _intervalCycle = (int)_curTalent.p_intervalCycle;
                }
            }
        }

#endregion
    }
}
