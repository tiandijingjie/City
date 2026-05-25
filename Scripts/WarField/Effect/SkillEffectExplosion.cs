using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;

    //爆炸
    public class SkillEffectExplosion : SkillEffect
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private SpriteRenderer _renderer;

        private float _damage;
        private float _range;
        private WE.FactionType _fromFaction;
        private SearchArea _explosionSearcher;
        private SearchShapeDef _explosionShape;
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.EXPLOSION; }
        }
#endregion

#region Unity callbacks

        protected override void Awake()
        {
            _effectType = ED.EffectType.EXPLOSION;
            base.Awake();
            _renderer.enabled = true;
            _explosionSearcher = new SearchArea(0, OnExplosionTargetsFound, GetExplosionSearchShape, null, 0);
            _explosionShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
        }
#endregion

#region public functions

#endregion

#region private functions

        protected override void OnActiveEffect(object value = null)
        {
            ValueTuple<float, float, WE.FactionType> valueTuple = (ValueTuple<float, float, WE.FactionType>)value;
            _damage = valueTuple.Item1;
            _range = valueTuple.Item2;
            _fromFaction = valueTuple.Item3;

            _explosionSearcher.p_conditions.Clear();
            if(_fromFaction == WE.FactionType.ENEMY)
                SearchConditionUtil.AddFriendlySoldierCondition(_explosionSearcher);
            else if(_fromFaction == WE.FactionType.FRIENDLY)
                SearchConditionUtil.AddEnemySoldierConditions(_explosionSearcher);
            else
                return;

            _explosionSearcher.p_mapId = WarMapCtrl.Instance.gs_curMapId;
            SearchManager.Instance.RegisterSearch(_explosionSearcher);
            Invoke("OnTimeOut", 0.1f);
        }

        private void OnExplosionTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Soldier sd = targets[i] as Soldier;
                if (sd == null)
                    continue;
                sd.BeAttacked(null, null, WE.WarEleType.MIN, _damage, false, false, out float hitValue);
            }
        }

        private SearchShapeDef GetExplosionSearchShape()
        {
            Vector2 pos = _transform.position;
            _explosionShape.p_centerOrStartPos = new float2(pos.x, pos.y);
            _explosionShape.p_radius = _range;
            _explosionShape.p_radiusSq = _range * _range;
            return _explosionShape;
        }

        protected override void OnDeactiveEffect()
        {
            _renderer.enabled = false;
        }

        private void OnTimeOut()
        {
            EffectCtrl.Instance.ReleaseEffect(this, _effectType);
        }
#endregion
    }
}
