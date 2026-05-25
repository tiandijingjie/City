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
    public class BasicBloodBurnPotionProp : Prop, AreaEffectIndicatorCb
    {
        private BasicBloodBurnPotionConf _thisConf;
        private SearchArea _areaSearcher;
        private SearchShapeDef _searchShape;
        private Vector2 _center;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _atkSpdUpObj, _armorDownObj;

        public BasicBloodBurnPotionProp()
        {
            _thisConf = new BasicBloodBurnPotionConf();
            _conf = _thisConf;
            _atkSpdUpObj = (SD.StateSoldierEffectType.ATTACKSPEED, _thisConf.p_atkSpeedUp, GD.CalDeltaType.MUL, _thisConf.p_duration, "BasicBloodBurnPotionProp",
                BFD.BuffStrategy.OVERRIDE, (object)this);
            _armorDownObj = (SD.StateSoldierEffectType.PHYARMOR, _thisConf.p_armorDown, GD.CalDeltaType.ADD, _thisConf.p_duration, "BasicBloodBurnPotionProp",
                BFD.BuffStrategy.OVERRIDE, (object)this);

            _areaSearcher = new SearchArea(0, OnAreaTargetsFound, GetSearchShape, null, 0);
            _searchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
            SearchConditionUtil.AddFriendlySoldierCondition(_areaSearcher);
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
                if (sd.CanAddBuff(BFD.SoldierBuffType.STATE) == true)
                {
                    sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _atkSpdUpObj);
                    sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _armorDownObj);
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
