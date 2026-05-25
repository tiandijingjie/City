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

    // 冰冻住一个区域的敌人包括敌人的建筑
    // 技能距离:40
    // 技能半径:3
    // 冻结时间:敌人4s,建筑10s
    // 技能冷却:60s
    public class MagicHeroFrozenSealSkill : Skill, AreaEffectIndicatorCb, IndividualItemModifyNotifyInf
    {
#region public parameters

#endregion

#region private parameters

        private MagicHeroIndividualData.SKillFrozenSeal _curAttribute = null;
        private float _intervalCycle;
        private bool _canTrigger; //记录触发但是动画并没有完成的这段时间

        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
        private Vector2 _releasePos;

        private (float, float) _freezeSdObj, _freezeBdObj;
#endregion

#region private parameters' get set

        public override uint gs_skillType
        {
            get { return (uint)MagicHeroIndividualData.IndividualDataType.FROZENSEAL; }
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

        //SKillFrozenSeal Improve被调用时的通知
        public void IndividualItemModifyNotify(int changeId)
        {
            switch ((MagicHeroIndividualData.SKillFrozenSeal.ParameterType)changeId)
            {
                case MagicHeroIndividualData.SKillFrozenSeal.ParameterType.FROZESD:
                    _freezeSdObj = (_curAttribute.p_frozeSd, 0);
                    break;
                case MagicHeroIndividualData.SKillFrozenSeal.ParameterType.FROZEBD:
                    _freezeBdObj = (_curAttribute.p_frozeBd, 0);
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
                    (MagicHeroIndividualData.SKillFrozenSeal)(_soldier.gs_oriIndividualData as MagicHeroIndividualData).gs_individualItems[
                        (int)MagicHeroIndividualData.IndividualDataType.FROZENSEAL];
            _name = _curAttribute.GetDescription().p_name;

            _intervalCycle = 0;
            _canTrigger = false;
            _freezeSdObj = (_curAttribute.p_frozeSd, 0.0f);
            _freezeBdObj = (_curAttribute.p_frozeBd, 0.0f);
            _curAttribute.RegisterNotify(this);

            if (_skillSearcher == null)
            {
                _skillSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, _soldier, _soldier.gs_mapId);
                _skillSearchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
                SearchConditionUtil.AddEnemySoldierAndBuildingConditions(_skillSearcher);
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
                if (targets[i] is Soldier sd)
                {
                    if (sd.CanAddBuff(BFD.SoldierBuffType.FREEZE) == true)
                        sd.BeAffectedByBuff(BFD.SoldierBuffType.FREEZE, in _freezeSdObj);
                }
                else if (targets[i] is WarBuilding bd)
                {
                    if (bd.CanAddBuff(BFD.WarBuildingBuffType.FREEZE) == true)
                        bd.BeAffectedByBuff(BFD.WarBuildingBuffType.FREEZE, in _freezeBdObj);
                }
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
            EffectBase ef = EffectCtrl.Instance.AddEffectAt(_releasePos, EffectDefines.EffectType.HEROFROZENSEAL, _soldier.gs_mapId,
                _curAttribute.p_radius);

            _canTrigger = false;
            _skillSearcher.p_mapId = _soldier.gs_mapId;
            SearchManager.Instance.RegisterSearch(_skillSearcher);
            _intervalCycle = _curAttribute.p_intervalCycle;
        }

        protected override void OnSkillAnimInterrupted(SD.SoldierAnimType interruptAnim)
        {
            _canTrigger = false;
        }
#endregion
    }
}

