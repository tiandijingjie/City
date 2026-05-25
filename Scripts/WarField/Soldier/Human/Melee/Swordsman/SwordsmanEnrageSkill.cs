using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Swordsman;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //激怒：当血量低于一定比例的时候大幅提高攻击力和攻击速度
    public class SwordsmanEnrageSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private SwordsmanIndividualData.SkillEnrage _oriAttribute = null;
        private float _startHp, _endHp;
        private bool _inEnrage = false;
        private object _speedUpIndexer, _damageUpIndexer;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SwordsmanIndividualData.IndividualDataType.ENRAGE; }
        }
#endregion

#region Unity callbacks

#endregion

#region public functions
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER };
        }
#endregion

#region private functions

        protected override bool OnActiveSkill()
        {
            if(_oriAttribute == null)
                _oriAttribute = new SwordsmanIndividualData.SkillEnrage((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.ENRAGE]);
            else
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.ENRAGE]);
            _name = _oriAttribute.GetDescription().p_name;
            _startHp = _soldier.gs_curState.p_maxHealth * _oriAttribute.p_hpPercent;
            _endHp = _soldier.gs_curState.p_maxHealth * (_oriAttribute.p_hpPercent + 0.1f);//血量要回升到比触发时高10%才能结束，避免反复触发
            _speedUpIndexer = _damageUpIndexer = null;
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_inEnrage == false)
            {
                if (_soldier.gs_curState.p_health < _startHp)
                {
                    _inEnrage = true;
                    OnStartSkill();
                }
            }
            else
            {
                if (_soldier.gs_curState.p_health > _endHp)
                {
                    _inEnrage = false;
                    SkillFinish();
                }
            }
        }

        protected override void OnSkillAnimInterrupted(SoldierDefines.SoldierAnimType interruptAnim)
        {
            _inEnrage = false;
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            if (_speedUpIndexer != null)
            {
                GameLogger.LogWarning("SpeedUp Indexer not null, should not enter skill again");
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.ATTACKSPEED, _speedUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _speedUpIndexer = null;
                _inEnrage = false;
                return;
            }

            if (_damageUpIndexer != null)
            {
                GameLogger.LogWarning("DamageUp Indexer not null, should not enter skill again");
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.DAMAGE, _damageUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _damageUpIndexer = null;
                _inEnrage = false;
                return;
            }

            _speedUpIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.ATTACKSPEED, _oriAttribute.p_attackSpeedUp, GD.CalDeltaType.MUL, out float orivalue);
            _damageUpIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.DAMAGE, _oriAttribute.p_damageUp, GD.CalDeltaType.MUL, out orivalue);
        }

        protected override void OnSkillFinish()
        {
            if (_speedUpIndexer != null)
            {
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.ATTACKSPEED, _speedUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _speedUpIndexer = null;
            }

            if (_damageUpIndexer != null)
            {
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.DAMAGE, _damageUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _damageUpIndexer = null;
            }
        }

#endregion
    }
}

