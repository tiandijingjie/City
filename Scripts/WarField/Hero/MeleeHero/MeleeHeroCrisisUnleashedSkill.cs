using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using MeleeHero;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using ED = EffectDefines;
    using GD = GlobalDefines;

    //技能激活时的敌人数量,之后数量改变也不影响技能效果
    //危机降临  根据技能激活时周围敌人的数量增加闪避和血上限
    //范围：3.5
    //闪避概率:0.8%*敌人数量 (不超过30%)
    //增加血上限:30*敌人数量 （不超过600）
    //持续时间:15s
    //冷却:45s
    public class MeleeHeroCrisisUnleashedSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private MeleeHeroIndividualData.SkillCrisisUnleashed _curAttribute = null;

        private int _intervalCycle, _durationCycle;
        private int _dodgeChance;
        public object _stateChangeObj;
        private int _enemyCnt; //范围内敌人的数量
        private bool _isTriggered;
        private Vector2 _effectOffset;
#endregion

#region private parameters' get set

        public override uint gs_skillType
        {
            get { return (uint)MeleeHeroIndividualData.IndividualDataType.CRISISUNLEASHED; }
        }

#endregion

#region Unity callbacks

        private new void Awake()
        {
            base.Awake();
            _triggerType = new[]
            {
                SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.ACTIVETRIGGER, SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER,
                SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER, SkillDefines.SkillTriggerType.BEATTACKTRIGGER
            };
            _effectOffset = new Vector2(0, 0.6f);
        }

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnActiveSkill()
        {
            if (_curAttribute == null)
                _curAttribute =
                    (MeleeHeroIndividualData.SkillCrisisUnleashed)(_soldier.gs_oriIndividualData as MeleeHeroIndividualData).gs_individualItems[
                        (int)MeleeHeroIndividualData.IndividualDataType.CRISISUNLEASHED];

            _name = _curAttribute.GetDescription().p_name;
            _intervalCycle = 0;
            _isTriggered = false;
            _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _curAttribute.p_radius, GD.CalDeltaType.EQUAL, out float ori);
            return true;
        }

        protected override void OnSkillRivalEnter(GameObject rival, WarFieldElements.WarEleType type)
        {
            _enemyCnt++;
        }

        protected override void OnSkillRivalLeave(GameObject rival, WarFieldElements.WarEleType type)
        {
            _enemyCnt--;
            if (_enemyCnt < 0) //just precaution
                _enemyCnt = 0;
        }

        protected override void OnSkillActivatedTrigger()
        {
            if (_isTriggered == true)
                return;
            if (_intervalCycle > 0)
                return;


            EffectBase ef = EffectCtrl.Instance.AddEffectAt((Vector2)_soldier.gs_transform.position + _effectOffset,
                ED.EffectType.HEROCRISISUNLEASHED, _soldier.gs_mapId, null);
            if (ef != null)
                ef.EffectFollow(_soldier.gs_transform, _effectOffset);

            _durationCycle = (int)_curAttribute.p_durationCycle;
            _intervalCycle = (int)_curAttribute.p_intervalCycle;
            float hpMax = _curAttribute.p_hpMaxPerSD * _enemyCnt;
            hpMax = hpMax < _curAttribute.p_hpMax ? hpMax : _curAttribute.p_hpMax;
            _dodgeChance = (int)(_curAttribute.p_dodgePerSd * _enemyCnt);
            _stateChangeObj = _soldier.AddStateChange(SD.StateSoldierEffectType.HEALTH, hpMax, GD.CalDeltaType.ADD, out float oriValue);
            _isTriggered = true;
        }

        protected override void OnSkillUpdate()
        {
            if (_intervalCycle > 0)
                _intervalCycle--;
            if (_isTriggered == true)
            {
                _durationCycle--;
                if (_durationCycle <= 0)
                {
                    _isTriggered = false;
                    _soldier.ModifyStateChange(SD.StateSoldierEffectType.HEALTH, _stateChangeObj, true, 0, GD.CalDeltaType.MIN, true);
                }
            }
        }

        //不考虑isByPass,可以闪避所有来源的伤害
        protected override float OnSkillBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            if (_isTriggered == true)
            {
                int chance = Utils.GetRandomInt();
                if (chance < _dodgeChance)
                    return 0;
            }

            return damage;
        }

#endregion
    }
}

