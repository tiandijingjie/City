using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using MeleeHero;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    public class MeleeHeroWhirlwindSlashSkill : Skill, MainEffectFinishCb
    {
#region public parameters

#endregion

#region private parameters

        private MeleeHeroIndividualData.SkillWhirlwindSlash _curAttribute = null;
        private float _intervalCycle;
        private bool _canTrigger; //记录触发但是动画并没有完成的这段时间

        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;

#endregion

#region private parameters' get set

        public override uint gs_skillType
        {
            get { return (uint)MeleeHeroIndividualData.IndividualDataType.WHIRLWINDSLASH; }
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

        //HeroSkillEffectWhirlwindSlash callback
        public void MainEffectFinish(string effInfo = null)
        {
            _skillSearcher.p_mapId = _soldier.gs_mapId;
            SearchManager.Instance.RegisterSearch(_skillSearcher);
            _canTrigger = false;
            _intervalCycle = _curAttribute.p_intervalCycle;
        }

#endregion

#region private functions

        protected override bool OnActiveSkill()
        {
            if (_curAttribute == null)
                _curAttribute =
                    (MeleeHeroIndividualData.SkillWhirlwindSlash)(_soldier.gs_oriIndividualData as MeleeHeroIndividualData).gs_individualItems[
                        (int)MeleeHeroIndividualData.IndividualDataType.WHIRLWINDSLASH];

            _name = _curAttribute.GetDescription().p_name;
            _intervalCycle = 0;
            _canTrigger = false;

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
                sd.BeAttacked(_soldier.gameObject, _soldier, WE.WarEleType.SOLDIER, _curAttribute.p_damage, false, false,
                    out float damage);
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            float radius = _curAttribute.p_radius + 0.3f;
            Vector2 center = (Vector2)_soldier.gs_transform.position;
            _skillSearchShape.p_centerOrStartPos = new float2(center.x, center.y);
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
                OnStartSkill();
                _canTrigger = true;
            }
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            EffectBase ef = EffectCtrl.Instance.AddEffectAt(_soldier.gs_transform.position, EffectDefines.EffectType.HEROWHIRLWINDSLASH,
                _soldier.gs_mapId, _curAttribute.p_radius + 0.3f); //0.3f 是英雄本身的大小
            ef.AddMainEffectFinishCb(this);
        }

        protected override void OnSkillAnimInterrupted(SD.SoldierAnimType interruptAnim)
        {
            _canTrigger = false;
        }

#endregion
    }
}
