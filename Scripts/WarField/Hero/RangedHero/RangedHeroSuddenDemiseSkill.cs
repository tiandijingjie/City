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

    //随机击杀技能范围内多个血量低于阈值的敌人,不包括英雄
    // 技能距离:20
    // 覆盖半径: 5
    // 击杀数量:7
    // 血量阈值:50%
    // 冷却:40s
    public class RangedHeroSuddenDemiseSkill : Skill, AreaEffectIndicatorCb
    {
#region public parameters

#endregion

#region private parameters
        private RangedHeroIndividualData.SkillSuddenDemise _curAttribute = null;
        private float _intervalCycle;
        private bool _canTrigger; //记录触发但是动画并没有完成的这段时间

        private SearchArea _skillSearcher;
        private SearchShapeDef _skillSearchShape;
        private Vector2 _releasePos;
        private int _killNum;

        private RangedHero _rangedHero;

        private GameObject _arrowPfb;
        [SerializeField] private uint _arrowId;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.TIMETRIGGER, SKD.SkillTriggerType.ACTIVETRIGGER };
            _arrowPfb = Resources.Load<GameObject>("Prefabs/Weapon/Bullet/SuddenDemiseArrow");
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

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_curAttribute == null)
                _curAttribute =
                    (RangedHeroIndividualData.SkillSuddenDemise)(_soldier.gs_oriIndividualData as RangedHeroIndividualData).gs_individualItems[
                        (int)RangedHeroIndividualData.IndividualDataType.SUDDENDEMISE];
            _name = _curAttribute.GetDescription().p_name;

            if (_arrowPfb == null)
                GameLogger.LogError($"RangedHeroSuddenDemiseSkill failed to get arrow prefab");

            _rangedHero = _soldier as RangedHero;
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
                if (sd.gs_curState.p_health < sd.gs_curState.p_maxHealth * 0.5f)
                {
                    _rangedHero.ShootSpecialWeaponTo(_arrowPfb, _arrowId, new Vector2(0.3f, 0.5f), sd.gameObject, WE.WarEleType.SOLDIER, sd, 100000f);
                    _killNum++;
                    if (_killNum >= _curAttribute.p_killNum)
                        break;
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
            _canTrigger = false;
            _killNum = 0;
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
