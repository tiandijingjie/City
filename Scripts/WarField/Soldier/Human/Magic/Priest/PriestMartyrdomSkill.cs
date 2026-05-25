using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Priest;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using BFD = BuffDefines;
    using WE = WarFieldElements;

    //- 献祭：缩短攻击距离，降低当血量极低时献祭自身，在一定时间一定范围内友军按照一定比例回复血量，自身进入无敌状态，技能释放完成之后立刻死亡
    public class PriestMartyrdomSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private PriestIndividualData.SkillMartyrdom _oriAttribute;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _hpIncObj;
        private bool _beTrigger;
        private float _hpTriggerThreshold;
        private Priest _priest;

        private Dictionary<Soldier, object> _partersBeEffect;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)PriestIndividualData.IndividualDataType.MARTYRDOM; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[]
            {
                SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER, SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER,
                SKD.SkillTriggerType.TIMETRIGGER
            };
            _partersBeEffect = new Dictionary<Soldier, object>();
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            _beTrigger = false;
            if (_oriAttribute == null)
            {
                _oriAttribute = new PriestIndividualData.SkillMartyrdom((_soldier.gs_oriIndividualData as PriestIndividualData)
                    .gs_individualItems[(int)PriestIndividualData.IndividualDataType.MARTYRDOM]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as PriestIndividualData)
                    .gs_individualItems[(int)PriestIndividualData.IndividualDataType.MARTYRDOM]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            //缩短攻击距离
            _soldier.AddStateChange(SD.StateSoldierEffectType.ATTACKRANGE, _oriAttribute.p_atkRangeDown, GD.CalDeltaType.MUL, out float value);
            _hpIncObj = (SD.StateSoldierEffectType.HPINC, _oriAttribute.p_hpIncUp, GD.CalDeltaType.ADD, -1f, "PriestRejuvenationSkill",
                BFD.BuffStrategy.APPEND, (object)this);  //duration is -1 means infinite
            _hpTriggerThreshold = _soldier.gs_curState.p_maxHealth * _oriAttribute.p_hpLimit;
            _priest = _soldier as Priest;
            _partersBeEffect.Clear();
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if(_beTrigger == true)
                return;

            if (_soldier.gs_curState.p_health <= _hpTriggerThreshold)
            {
                _beTrigger = true;
                _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _oriAttribute.p_range, GD.CalDeltaType.EQUAL, out float value);
                _priest.EnterMartyrdom();
                OnStartSkill();
                Invoke("Timeout", _oriAttribute.p_duration);
            }
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            //现在暂时没有动画，用Invoke代替
            //_priest.ExitMartyrdom();
        }

        protected override void OnSkillParterEnter(GameObject parter, WarFieldElements.WarEleType type)
        {
            if(_beTrigger == false)
                return;

            if (type == WE.WarEleType.SOLDIER)
            {
                Soldier sd = parter.GetComponent<Soldier>();
                if (sd != null)
                {
                    object buffRet = null;
                    if(sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _hpIncObj, ref buffRet) == true)
                        _partersBeEffect[sd] = buffRet;
                }
            }
        }

        protected override void OnSkillParterLeave(GameObject parter, WarFieldElements.WarEleType type)
        {
            if(_beTrigger == false)
                return;

            if (type == WE.WarEleType.SOLDIER)
            {
                Soldier sd = parter.GetComponent<Soldier>();
                if (sd != null)
                {
                    if (_partersBeEffect.ContainsKey(sd) == true)
                    {
                        var value = (_partersBeEffect[sd], (object)this);
                        sd.StopPartOfBuff(BFD.SoldierBuffType.STATE, in value);
                    }
                }
            }
        }

        protected void Timeout()
        {
            _priest.ExitMartyrdom();
            foreach (var tmpSD in _partersBeEffect)
            {
                var value = (tmpSD.Value, (object)this);
                (tmpSD.Key).StopPartOfBuff(BFD.SoldierBuffType.STATE, in value);
            }
        }

#endregion
    }
}
