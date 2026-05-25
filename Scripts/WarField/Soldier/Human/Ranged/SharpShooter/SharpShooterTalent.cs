using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SharpShooter;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    
    //普通攻击时有8%概率攻击第二个敌人，第二击的攻击力是普通攻击力的50%
    public class SharpShooterTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private SharpShooterIndividualData.Talent _oriAttribute;
        private SharpShooter _sharpShooter;

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
            _sharpShooter = _soldier as SharpShooter;
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new SharpShooterIndividualData.Talent((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.TALENT]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SharpShooterIndividualData)
                    .gs_individualItems[(int)SharpShooterIndividualData.IndividualDataType.TALENT]);
            }
            
            return true;
        }

        protected override void OnTalentDoAttackPost(float hit, bool isDead, object rivalScript, WarFieldElements.WarEleType rivalType)
        {
            int chance = Utils.GetRandomInt();
            if (chance < _oriAttribute.p_secondChance)
            {
                _sharpShooter.SecondShoot(_oriAttribute.p_damageTImes);
            }
        }

#endregion
    }
}

