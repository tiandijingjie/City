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

    //新生  英雄死亡后有3次立刻复活的机会
    public class HeroRebirthTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentRebirth _curTalent = null;
        private int _rebornTime = 0;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.REBORNTRIGGER };
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
                _curTalent = (HeroGenericIndividualData.TalentRebirth)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.REBIRTH];
            _name = _curTalent.GetDescription().p_name;
            _rebornTime = _curTalent.p_rebirthTime;
            return true;
        }

        protected override float OnTalentRebornTrigger(float rebornTime)
        {
            if (_rebornTime > 0)
            {
                _rebornTime--;
                return 1; //1s 复活
            }
            return rebornTime;
        }

#endregion
    }
}
