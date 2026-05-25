using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //盾兵
    //受到攻击时有5%概率格挡10点物理伤害
    public class ShieldSoldierTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private ShieldSoldierIndividualData.Talent _orAttribute = null;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();

            _triggerType = new[] { SKD.SkillTriggerType.BEATTACKTRIGGER};
            for (int i = 0; i < _triggerType.Length; i++)
                _soldier.RegisterTalent(this, _triggerType[i]);
        }
#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnActiveTalent()
        {
            if(_orAttribute == null)
                _orAttribute = new ShieldSoldierIndividualData.Talent((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.TALENT]);
            else
                _orAttribute.ReInit((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.TALENT]);
            return true;
        }

        protected override float OnTalentBeAttackPre(float damage, object rival, WarFieldElements.WarEleType rivalType, bool isByPass)
        {
            if(isByPass == true)//不触发技能的伤害  不会触发格挡
                return damage;

            if (damage > 0)
            {
                int chance = Utils.GetRandomInt();
                if (chance < _orAttribute.p_blockChance)
                {
                    float ret = damage - _orAttribute.p_blockValue;
                    return 0 > ret ? 0 : ret;
                }
                return damage;
            }
            else
                return 0;
        }

#endregion
    }
}

