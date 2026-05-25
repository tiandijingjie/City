using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using RangedHero;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using ED = EffectDefines;
    using GD = GlobalDefines;
    using BFD = BuffDefines;

    //大量降低前方敌人的移动和攻击速度,不能叠加,并造成伤害
    //覆盖范围: 宽2.5长9的区域
    // 属性降低: 降低50%移动速度,35%攻击速度
    // 技能持续时间: 20s
    // 伤害:30
    // 冷却:40s
    public class RangedHeroFrostArrowSkill : Skill, MainEffectFinishCb, DirectionEffectIndicatorCb, IndividualItemModifyNotifyInf
    {
#region public parameters

#endregion

#region private parameters
        private RangedHeroIndividualData.SkillFrostArrow _curAttribute = null;
        private float _intervalCycle;
        private bool _canTrigger; //记录触发但是动画并没有完成的这段时间

        private float _angle; //技能释放的角度
        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;

        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _moveDownObj, _attackSpeedDownObj;
        private RangedHero _rangedHero;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)RangedHeroIndividualData.IndividualDataType.FROSTARROW; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.ACTIVETRIGGER };
        }
#endregion

#region public functions
        public void MainEffectFinish(string effInfo = null)
        {
            _skillSearcher.p_mapId = _soldier.gs_mapId;
            SearchManager.Instance.RegisterSearch(_skillSearcher);
            _canTrigger = false;
            _intervalCycle = _curAttribute.p_intervalCycle;
        }

        //SkillFrostArrow Improve被调用时的通知
        public void IndividualItemModifyNotify(int changeId)
        {
            switch ((RangedHeroIndividualData.SkillFrostArrow.ParameterType)changeId)
            {
                case RangedHeroIndividualData.SkillFrostArrow.ParameterType.MOVEDOWN:
                    _moveDownObj = (SD.StateSoldierEffectType.MOVESPEED, _curAttribute.p_moveDown, GD.CalDeltaType.MUL, _curAttribute.p_duration,
                        "RangedHeroFrostArrowSkill", BFD.BuffStrategy.IGNORE, (object)this);
                    break;
                case RangedHeroIndividualData.SkillFrostArrow.ParameterType.ATTACKSPEEDDOWN:
                    _attackSpeedDownObj = (SD.StateSoldierEffectType.ATTACKSPEED, _curAttribute.p_attackSpeedDown, GD.CalDeltaType.MUL,
                        _curAttribute.p_duration, "RangedHeroFrostArrowSkill", BFD.BuffStrategy.IGNORE, (object)this);
                    break;
                default:
                    break;
            }
        }

        //indicator callback
        public void GiveUpEffect()
        {
            //do nothing
        }

        //indicator callback
        public void TriggerEffect(float angle)
        {
            _angle = angle;
            OnStartSkill(); //播放英雄动画
            _canTrigger = true;
        }
#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_curAttribute == null)
                _curAttribute =
                    (RangedHeroIndividualData.SkillFrostArrow)(_soldier.gs_oriIndividualData as RangedHeroIndividualData).gs_individualItems[
                        (int)RangedHeroIndividualData.IndividualDataType.FROSTARROW];
            _name = _curAttribute.GetDescription().p_name;

            _moveDownObj = (SD.StateSoldierEffectType.MOVESPEED, _curAttribute.p_moveDown, GD.CalDeltaType.MUL, _curAttribute.p_duration,
                "RangedHeroFrostArrowSkill", BFD.BuffStrategy.IGNORE, (object)this);
            _attackSpeedDownObj = (SD.StateSoldierEffectType.ATTACKSPEED, _curAttribute.p_attackSpeedDown, GD.CalDeltaType.MUL,
                _curAttribute.p_duration, "RangedHeroFrostArrowSkill", BFD.BuffStrategy.IGNORE, (object)this);

            _intervalCycle = 0;
            _canTrigger = false;
            _curAttribute.RegisterNotify(this);
            _rangedHero = _soldier as RangedHero;

            if (_skillSearcher == null)
            {
                _skillSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, _soldier, _soldier.gs_mapId);
                _skillSearchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.SEGMENT };
                SearchConditionUtil.AddEnemySoldierConditions(_skillSearcher);
            }
            else
            {
                _skillSearcher.p_mapId = _soldier.gs_mapId;
            }

            return true;
        }

        private void OnAreaTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Soldier sd = targets[i] as Soldier;
                if (sd == null)
                    continue;
                sd.BeAttacked(_soldier.gameObject, _soldier, WE.WarEleType.SOLDIER, _curAttribute.p_damage, false, false, out float damage);
                sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _moveDownObj);
                sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _attackSpeedDownObj);
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            Vector2 size = new Vector2(_curAttribute.p_length + 0.3f, _curAttribute.p_width);
            Vector2 origin = (Vector2)_soldier.gs_transform.position;
            _skillSearchShape.p_centerOrStartPos = new float2(origin.x, origin.y);
            _skillSearchShape.p_endPos = new float2(origin.x + size.x, origin.y);
            _skillSearchShape.p_widthRadius = size.y * 0.5f;
            _skillSearchShape.p_widthRadiusSq = _skillSearchShape.p_widthRadius * _skillSearchShape.p_widthRadius;
            return _skillSearchShape;
        }

        protected override void OnSkillMapChange(int fromMap, int toMap)
        {
            _skillSearcher.p_mapId = toMap;
        }

        protected override void OnSkillUpdate()
        {
            if (_intervalCycle >= 0)
            {
                _intervalCycle -= _timeStep;
            }
        }

        protected override void OnSkillActivatedTrigger()
        {
            if (_intervalCycle > 0)
            {
                GameLogger.LogWarning($"Skill {name} duplicated trigger!");
                return;
            }

            if (_canTrigger == true) //已经播放攻击动画,但是时间还没有复位
                return;

            if (_soldier.CanTriggerActiveSkill() == true)
            {
                EffectIndicator indicator = EffectCtrl.Instance.GetEffectIndicator(ED.SkillIndicatorType.DIRCTION);
                ((DirectionEffectIndicator)indicator).SetDirectionEffectIndicator(new Color(32 / 255f, 85 / 255f, 191 / 255f), 1,
                    ((Hero)_soldier).gs_transform, new Vector2(_curAttribute.p_length, _curAttribute.p_width));
                indicator.ActiveEffectIndicator(this);
            }
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            //播放技能特效
            EffectBase ef = EffectCtrl.Instance.AddEffectAt(_rangedHero.gs_skillPos, EffectDefines.EffectType.HEROFROSTARROW, _soldier.gs_mapId,
                new Vector2(_curAttribute.p_length, _curAttribute.p_width));
            ef.transform.rotation = Quaternion.Euler(0, 0, _angle); //按照选定角度旋转特效
            ef.AddMainEffectFinishCb(this);
        }

        protected override void OnSkillAnimInterrupted(SD.SoldierAnimType interruptAnim)
        {
            _canTrigger = false;
        }
#endregion
    }
}

