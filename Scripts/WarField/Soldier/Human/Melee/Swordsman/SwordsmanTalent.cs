using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Swordsman;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    
    //普通攻击时15%概率具有30%的吸血效果
    public class SwordsmanTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private SwordsmanIndividualData.Talent _oriAttribute = null;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            
            _triggerType = new[] { SKD.SkillTriggerType.ATTACKTRIGGER};
            for (int i = 0; i < _triggerType.Length; i++)
                _soldier.RegisterTalent(this, _triggerType[i]);
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_oriAttribute == null)
                _oriAttribute = new SwordsmanIndividualData.Talent((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.TALENT]);
            else
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.TALENT]);
            
            return true;
        }

        protected override void OnTalentDoAttackPost(float hit, bool isDead, object rivalScript, WarFieldElements.WarEleType rivalType)
        {
            if (hit > 0)
            {
                int chance = Utils.GetRandomInt();
                if (chance < _oriAttribute.p_bloodStealChance)
                {
                    float cure = hit * _oriAttribute.p_bloodStealPercent;
                    _soldier.BeCure(null, cure, GD.CalDeltaType.ADD);
                }
            }
        }

#endregion
    }
}

