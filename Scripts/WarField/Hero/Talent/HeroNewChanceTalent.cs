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

    //新的机会 生命值降低到10%时会立刻恢复到70%,如果英雄在血量高于10%时直接被击毙不会触发此效果  冷却: 180s
    public class HeroNewChanceTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentNewChance _curTalent = null;
        private int _intervalCycle;
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
                _curTalent = (HeroGenericIndividualData.TalentNewChance)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.NEWCHANCE];
            _name = _curTalent.GetDescription().p_name;

            _intervalCycle = 0;

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
                    _soldier.BeCure(null, _soldier.gs_curState.p_maxHealth * _curTalent.p_hpRecover, GD.CalDeltaType.EQUAL);
                    //_soldier.gs_curState.p_health = _soldier.gs_curState.p_maxHealth * _curTalent.p_hpRecover;
                    _intervalCycle = (int)_curTalent.p_intervalCycle;
                }
            }
        }

#endregion
    }
}
