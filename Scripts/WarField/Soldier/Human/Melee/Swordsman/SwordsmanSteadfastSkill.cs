using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Swordsman;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //坚固：当血量低于一定比例的时候大幅提高回血速度和防御力
    public class SwordsmanSteadfastSkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private SwordsmanIndividualData.SkillSteadfast _oriAttribute = null;
        private float _startHp, _endHp;
        private bool _inSteadfast = false;
        private object _hpIncUpIndexer, _armorUpIndexer;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SwordsmanIndividualData.IndividualDataType.STEADFAST; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if(_oriAttribute == null)
                _oriAttribute = new SwordsmanIndividualData.SkillSteadfast((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.STEADFAST]);
            else
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SwordsmanIndividualData)
                    .gs_individualItems[(int)SwordsmanIndividualData.IndividualDataType.STEADFAST]);
            _name = _oriAttribute.GetDescription().p_name;
            _startHp = _soldier.gs_curState.p_maxHealth * _oriAttribute.p_hpPercent;
            _endHp = _soldier.gs_curState.p_maxHealth * (_oriAttribute.p_hpPercent + 0.1f);//血量要回升到比触发时高10%才能结束，避免反复触发
            _hpIncUpIndexer = _armorUpIndexer = null;
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_inSteadfast == false)
            {
                if (_soldier.gs_curState.p_health < _startHp)
                {
                    _inSteadfast = true;
                    OnStartSkill();
                }
            }
            else
            {
                if (_soldier.gs_curState.p_health > _endHp)
                {
                    _inSteadfast = false;
                    SkillFinish();
                }
            }
        }

        protected override void OnSkillAnimInterrupted(SoldierDefines.SoldierAnimType interruptAnim)
        {
            _inSteadfast = false;
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            if (_hpIncUpIndexer != null)
            {
                GameLogger.LogWarning("HpIncUp Indexer not null, should not enter skill again");
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.HPINC, _hpIncUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _hpIncUpIndexer = null;
                _inSteadfast = false;
                return;
            }

            if (_armorUpIndexer != null)
            {
                GameLogger.LogWarning("ArmorUp Indexer not null, should not enter skill again");
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.DAMAGE, _armorUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _armorUpIndexer = null;
                _inSteadfast = false;
                return;
            }

            _hpIncUpIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.HPINC, _oriAttribute.p_hpIncUp, GD.CalDeltaType.MUL, out float orivalue);
            _armorUpIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.PHYARMOR, _oriAttribute.p_armorUp, GD.CalDeltaType.ADD, out orivalue);
        }

        protected override void OnSkillFinish()
        {
            if (_hpIncUpIndexer != null)
            {
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.HPINC, _hpIncUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _hpIncUpIndexer = null;
            }

            if (_armorUpIndexer != null)
            {
                _soldier.ModifyStateChange(SD.StateSoldierEffectType.PHYARMOR, _armorUpIndexer, true, 0, GD.CalDeltaType.MIN, true);
                _armorUpIndexer = null;
            }
        }
#endregion
    }
}

