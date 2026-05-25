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

    //锁定  英雄生命值低于10%时一段时间受到任何伤害不会造成血量减少，持续时间结束后这段时间内受到的伤害的按照一定比率回血,如果英雄在血量高于1%时直接被击毙不会触发此效果
    //持续时间:10s  冷却:200s 回血比例：15%
    public class HeroLastBreathTalent : Talent
    {
#region public parameters

#endregion

#region private parameters

        private HeroGenericIndividualData.TalentLastBreath _curTalent = null;
        private int _intervalCycle;
        private int _durationCycle;
        private bool _isTriggered;
        private float _totalDamage;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.BEATTACKTRIGGER };
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
                _curTalent = (HeroGenericIndividualData.TalentLastBreath)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.LASTBREATH];
            _name = _curTalent.GetDescription().p_name;

            _intervalCycle = 0;
            _isTriggered = false;

            return true;
        }

        protected override void OnTalentUpdate()
        {
            if (_intervalCycle > 0)
                _intervalCycle--;

            if (_intervalCycle <= 0)
            {
                if (_isTriggered == false)
                {
                    if (_soldier.gs_curState.p_health < (_soldier.gs_curState.p_maxHealth * _curTalent.p_hpThreshold) && (_soldier.gs_curStatus !=
                        SD.SoldierStatus.DIE || _soldier.gs_curStatus != SD.SoldierStatus.REBORN))
                    {
                        _durationCycle = (int)_curTalent.p_durationCycle;
                        _totalDamage = 0;
                        _isTriggered = true;
                    }
                }
                else
                {
                    _durationCycle--;
                    if (_durationCycle <= 0)
                    {
                        _soldier.BeCure(null, _totalDamage * _curTalent.p_damageToHpAdd, GD.CalDeltaType.ADD);
                        _isTriggered = false;
                        _intervalCycle = (int)_curTalent.p_intervalCycle;
                    }
                }
            }
        }

        //忽略isByPass
        protected override float OnTalentBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            if (_isTriggered == true)
            {
                _totalDamage += damage;
                return 0;
            }
            return damage;
        }

#endregion
    }
}
