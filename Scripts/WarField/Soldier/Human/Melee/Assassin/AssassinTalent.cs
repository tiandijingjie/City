using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Assassin;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //刺客
    //5s没有攻击或者被攻击进入隐身，无法主动成为敌人目标，破隐一击造成2.5倍普通伤害的额外伤害
    public class AssassinTalent : Talent
    {
#region public parameters

#endregion

#region private parameters
        private AssassinIndividualData.Talent _oriAttribute = null, _curAttribute = null;
        private bool _inHide;
        private object _hideStateIndex;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();

            _triggerType = new[] { SKD.SkillTriggerType.ATTACKTRIGGER, SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.BEATTACKTRIGGER};
            for (int i = 0; i < _triggerType.Length; i++)
                _soldier.RegisterTalent(this, _triggerType[i]);
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveTalent()
        {
            _inHide = false;
            if (_oriAttribute == null)
            {
                _oriAttribute = new AssassinIndividualData.Talent((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.TALENT]);
                _curAttribute = new AssassinIndividualData.Talent(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as AssassinIndividualData)
                    .gs_individualItems[(int)AssassinIndividualData.IndividualDataType.TALENT]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _curAttribute.p_hideIntervalCycle = 0; //hide on born
            _hideStateIndex = null;
            return true;
        }

        protected override void OnTalentUpdate()
        {
            if (_inHide == false && _soldier.gs_curStatus == SD.SoldierStatus.MOVE)
            {
                _curAttribute.p_hideIntervalCycle -= _timeStep;
                if (_curAttribute.p_hideIntervalCycle <= 0)
                {
                    _inHide = true;
                    if (_hideStateIndex == null)
                        _hideStateIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.BODYHIDE, -1, GD.CalDeltaType.EQUAL, out float ori); //hide
                    else
                        _soldier.ModifyStateChange(SD.StateSoldierEffectType.BODYHIDE, _hideStateIndex, false, -1, GD.CalDeltaType.EQUAL, true);
                }
            }
        }

        protected override float OnTalentBeAttackPre(float damage, object rival, WarFieldElements.WarEleType rivalType, bool isByPass)
        {
            if(isByPass == true) //不触发技能的伤害不会打断隐身
                return damage;

            if (_inHide == true)
            {
                _inHide = false;
                if (_hideStateIndex == null)
                    _hideStateIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.BODYHIDE, 1, GD.CalDeltaType.EQUAL, out float ori); //show
                else
                    _soldier.ModifyStateChange(SD.StateSoldierEffectType.BODYHIDE, _hideStateIndex, false, 1, GD.CalDeltaType.EQUAL, true);
                _curAttribute.p_hideIntervalCycle = _oriAttribute.p_hideIntervalCycle;
            }
            return damage;
        }

        protected override bool OnTalentDoAttackPre(float hit, object rivalScript, WarFieldElements.WarEleType rivalType, out float damage)
        {
            if (rivalType == WE.WarEleType.SOLDIER)
            {
                if (_inHide == true) //破隐一击
                {
                    _inHide = false;
                    if (_hideStateIndex == null)
                        _hideStateIndex = _soldier.AddStateChange(SD.StateSoldierEffectType.BODYHIDE, 1, GD.CalDeltaType.EQUAL, out float ori); //show
                    else
                        _soldier.ModifyStateChange(SD.StateSoldierEffectType.BODYHIDE, _hideStateIndex, false, 1, GD.CalDeltaType.EQUAL, true);//show
                    _curAttribute.p_hideIntervalCycle = _oriAttribute.p_hideIntervalCycle;
                    hit = hit * _curAttribute.p_hideStrikeTimes;
                }
            }

            damage = hit;
            return true;
        }
#endregion
    }
}

