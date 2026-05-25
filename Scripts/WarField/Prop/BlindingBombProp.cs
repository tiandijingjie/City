using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //闪光弹 使范围4内的普通敌人致盲失去99%攻击力，对boss减低20%的攻击力,持续5s，
    public class BlindingBombProp : Prop, AreaEffectIndicatorCb
    {
        private BlendingBombConf _thisConf;
        private SearchArea _areaSearcher;
        private SearchShapeDef _searchShape;
        private Vector2 _center;
        private (int, float, string, object, BFD.BuffStrategy) _lostObj, _lostBossObj;

        public BlindingBombProp()
        {
            _thisConf = new BlendingBombConf();
            _conf = _thisConf;
            _lostObj = (_thisConf.p_dmgMissChance, _thisConf.p_duration, "BlindingBombProp", (object)this, BFD.BuffStrategy.APPEND);
            _lostBossObj = (_thisConf.p_bossDmgMissChance, _thisConf.p_duration, "BlindingBombProp", (object)this, BFD.BuffStrategy.APPEND);

            _areaSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, null, 0);
            _searchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
            SearchConditionUtil.AddEnemySoldierConditions(_areaSearcher);
        }

        public override void ActiveProp()
        {
            EffectIndicator indicator = EffectCtrl.Instance.GetEffectIndicator(EffectDefines.SkillIndicatorType.AREA);

            ((AreaEffectIndicator)indicator).SetAreaEffectIndicator(new Color(1f, 1f, 1f), 1, null, new Vector2(-1, _thisConf.p_range));
            ((AreaEffectIndicator)indicator).SetAreaEffectIndicatorBg("Textures/EffectTex/EffectIndicator/AreaEffect", new Color(0.2f, 0.8f, 0.8f,
                0.4f));
            indicator.ActiveEffectIndicator(this);
        }

        public override bool UseProp()
        {
            _areaSearcher.p_mapId = WarMapCtrl.Instance.gs_curMapId;
            SearchManager.Instance.RegisterSearch(_areaSearcher);
            return base.UseProp();
        }

        private void OnAreaTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Soldier sd = targets[i] as Soldier;
                if (sd == null)
                    continue;
                if (sd.CanAddBuff(BFD.SoldierBuffType.ATTACKLOST) == true)
                {
                    if(sd.gs_sdLevel == SD.SoldierLevel.BOSSLEVEL)
                        sd.BeAffectedByBuff(BFD.SoldierBuffType.ATTACKLOST, in _lostBossObj);
                    else
                        sd.BeAffectedByBuff(BFD.SoldierBuffType.ATTACKLOST, in _lostObj);
                }
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            float radius = _thisConf.p_range;
            _searchShape.p_centerOrStartPos = new float2(_center.x, _center.y);
            _searchShape.p_radius = radius;
            _searchShape.p_radiusSq = radius * radius;
            return _searchShape;
        }

        public void GiveUpEffect()
        {
            base.GiveupProp();
        }

        public void TriggerEffect(Vector2 center)
        {
            _center = center;
            UseProp();
        }

        public void CheckPosition(Vector2 position)
        {
            //do nothing
        }
    }
}
