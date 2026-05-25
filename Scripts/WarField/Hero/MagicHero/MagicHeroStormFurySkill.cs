using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using MagicHero;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using ED = EffectDefines;
    using BFD = BuffDefines;
    using GD = GlobalDefines;

    //在一个区域释放狂风,造成范围伤害并永久降低移动速度,移动速度降低不可叠加,对英雄无效
    // 技能距离:无限
    // 技能半径:4
    // 伤害:210
    // 移速降低:20%
    // 技能冷却:105s
    public class MagicHeroStormFurySkill : Skill, MainEffectFinishCb, AreaEffectIndicatorCb, IndividualItemModifyNotifyInf
    {
#region public parameters

#endregion

#region private parameters

        private MagicHeroIndividualData.SkillStormFury _curAttribute = null;
        private float _intervalCycle;
        private bool _canTrigger; //记录触发但是动画并没有完成的这段时间

        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
        private Vector2 _releasePos;

        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _moveObj;

#endregion

#region private parameters' get set

        public override uint gs_skillType
        {
            get { return (uint)MagicHeroIndividualData.IndividualDataType.STORMFURY; }
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
            _canTrigger = false;
            _skillSearcher.p_mapId = _soldier.gs_mapId;
            SearchManager.Instance.RegisterSearch(_skillSearcher);
            _intervalCycle = _curAttribute.p_intervalCycle;
        }

        //indicator callback
        public void GiveUpEffect()
        {
            //do nothing
        }

        //indicator callback
        public void TriggerEffect(Vector2 center)
        {
            _releasePos = center;
            OnStartSkill(); //播放英雄动画
            _canTrigger = true;
        }

        public void CheckPosition(Vector2 position)
        {
            //do nothing
        }

        //SkillFrostArrow Improve被调用时的通知
        public void IndividualItemModifyNotify(int changeId)
        {
            switch ((MagicHeroIndividualData.SkillStormFury.ParameterType)changeId)
            {
                case MagicHeroIndividualData.SkillStormFury.ParameterType.MOVEDOWN:
                    _moveObj = (SD.StateSoldierEffectType.MOVESPEED, _curAttribute.p_moveDown, GD.CalDeltaType.MUL, -1.0f, "MagicHeroFrozenSealSkill",
                        BFD.BuffStrategy.IGNORE, (object)this);
                    break;
                default:
                    break;
            }
        }
#endregion

#region private functions
protected override bool OnActiveSkill()
        {
            if (_curAttribute == null)
                _curAttribute =
                    (MagicHeroIndividualData.SkillStormFury)(_soldier.gs_oriIndividualData as MagicHeroIndividualData).gs_individualItems[
                        (int)MagicHeroIndividualData.IndividualDataType.STORMFURY];
            _name = _curAttribute.GetDescription().p_name;

            _moveObj = (SD.StateSoldierEffectType.MOVESPEED, _curAttribute.p_moveDown, GD.CalDeltaType.MUL, -1.0f, "MagicHeroFrozenSealSkill", BFD
                .BuffStrategy.IGNORE, (object)this);
            _intervalCycle = 0;
            _canTrigger = false;
            _curAttribute.RegisterNotify(this);

            if (_skillSearcher == null)
            {
                _skillSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, _soldier, _soldier.gs_mapId);
                _skillSearchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
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
                sd.BeAttacked(null, _soldier, WE.WarEleType.SOLDIER, _curAttribute.p_damage, false, false, out float damage);
                sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _moveObj);
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            float radius = _curAttribute.p_radius;
            _skillSearchShape.p_centerOrStartPos = new float2(_releasePos.x, _releasePos.y);
            _skillSearchShape.p_radius = radius;
            _skillSearchShape.p_radiusSq = radius * radius;
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
                EffectIndicator indicator = EffectCtrl.Instance.GetEffectIndicator(ED.SkillIndicatorType.AREA);
                ((AreaEffectIndicator)indicator).SetAreaEffectIndicator(new Color(32 / 255f, 85 / 255f, 191 / 255f), 1, ((Hero)_soldier).gs_transform,
                    new Vector2(_curAttribute.p_distance, _curAttribute.p_radius));
                indicator.ActiveEffectIndicator(this);
            }
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            //播放技能特效
            EffectBase ef = EffectCtrl.Instance.AddEffectAt(_releasePos, EffectDefines.EffectType.HEROSTORMFURY, _soldier.gs_mapId,
                _curAttribute.p_radius);
            ef.AddMainEffectFinishCb(this);
        }

        protected override void OnSkillAnimInterrupted(SD.SoldierAnimType interruptAnim)
        {
            _canTrigger = false;
        }
#endregion
    }
}

