using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sniper;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //专注：在攻击同一个敌人时会随着攻击次数的增加会缩短攻击间隔
    public class SniperSkillFocus : Skill
    {
#region public parameters

#endregion

#region private parameters
        private SniperIndividualData.SkillFocus _oriAttribute;
        private object _target;
        private float _totalReduce;
        private Sniper _self;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SniperIndividualData.IndividualDataType.FOCUS; }
        }
#endregion

#region Unity callbacks

        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.ATTACKTRIGGER };
            _self = (Sniper)_soldier;
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new SniperIndividualData.SkillFocus((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.FOCUS]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.FOCUS]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _target = null;
            return true;
        }

        //攻击间隔是在AttackPre之前就已经被复位了
        protected override bool OnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            if (rivalScript == _target)
            {
                if (_totalReduce < _oriAttribute.p_atkGapMax)
                {
                    _totalReduce += _oriAttribute.p_atkGapDown;
                    if (_totalReduce >= _oriAttribute.p_atkGapMax)
                        _totalReduce = _oriAttribute.p_atkGapMax + 0.0001f; //make sure bigger then p_atkGapMax
                }
                _self.ReduceAttackGap(_totalReduce);
            }
            else
            {
                _target = rivalScript;
                _totalReduce = 0;
            }

            damage = hit;
            return true;
        }

#endregion
    }
}


