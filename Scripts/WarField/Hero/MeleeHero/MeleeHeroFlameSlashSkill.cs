using System;
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
    using ED = EffectDefines;

    //火焰斩 攻击前方一条直线上的敌人 攻击距离:8.5  攻击宽度:2 攻击伤害:120  冷却:55s
    public class MeleeHeroFlameSlashSkill : Skill, MainEffectFinishCb, DirectionEffectIndicatorCb
    {
#region public parameters

#endregion

#region private parameters

        private MeleeHeroIndividualData.SkillFlameSlash _curAttribute = null;
        private float _intervalCycle;
        private bool _canTrigger; //记录触发但是动画并没有完成的这段时间

        private float _angle; //技能释放的角度
        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
#endregion

#region private parameters' get set

        public override uint gs_skillType
        {
            get { return (uint)MeleeHeroIndividualData.IndividualDataType.FLAMESLASH; }
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
                    (MeleeHeroIndividualData.SkillFlameSlash)(_soldier.gs_oriIndividualData as MeleeHeroIndividualData).gs_individualItems[
                        (int)MeleeHeroIndividualData.IndividualDataType.FLAMESLASH];

            _name = _curAttribute.GetDescription().p_name;
            _intervalCycle = 0;
            _canTrigger = false;

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
                ((DirectionEffectIndicator)indicator).SetDirectionEffectIndicator(new Color(191 / 255f, 55 / 255f, 23 / 255f), 1,
                    ((Hero)_soldier).gs_transform, new Vector2(_curAttribute.p_length, _curAttribute.p_width));
                indicator.ActiveEffectIndicator(this);
            }
        }

        protected override void OnSkillAnimTakeEffect(string value)
        {
            //播放技能特效
            EffectBase ef = EffectCtrl.Instance.AddEffectAt(_soldier.gs_transform.position, EffectDefines.EffectType.HEROFLAMESLASH, _soldier.gs_mapId,
                _curAttribute.p_length);
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


