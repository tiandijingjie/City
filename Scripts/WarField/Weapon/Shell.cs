using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using WD = WeaponDefines;
    using ED = EffectDefines;

    public class Shell : Projectile
    {
#region public parameters

#endregion

#region private parameters

        private bool _canAttackBuilding;
        private SearchArea _explosionSearcher;
        private SearchShapeDef _explosionShape;
        private List<Soldier> _sdList;
        private List<WarBuilding> _bdList;
        private float _totalHit;
        private int _dieCount;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _canAttackBuilding = true;
            _explosionSearcher = new SearchArea(0, OnExplosionTargetsFound, GetExplosionSearchShape, null, 0);
            _explosionShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
        }

#endregion

#region public functions

        public void CanAttackBuilding(bool value)
        {
            _canAttackBuilding = value;
        }

#endregion

#region private functions

        protected override bool OnInit()
        {
            _explosionSearcher.p_conditions.Clear();
            if (_faction == WE.FactionType.FRIENDLY)
            {
                SearchConditionUtil.AddEnemySoldierConditions(_explosionSearcher);
                SearchConditionUtil.AddEnemyBuildingCondition(_explosionSearcher);
            }
            else if(_faction == WE.FactionType.ENEMY)
            {
                SearchConditionUtil.AddFriendlySoldierCondition(_explosionSearcher);
                SearchConditionUtil.AddFriendlyBuildingCondition(_explosionSearcher);
            }
            else
            {
                return false;
            }
            return base.OnInit();
        }

        protected override void ReachTarget()
        {
            _sdList = new List<Soldier>();
            _bdList = new List<WarBuilding>();
            _totalHit = 0;
            _dieCount = 0;
            _explosionSearcher.p_mapId = gs_mapId;
            SearchManager.Instance.RegisterSearch(_explosionSearcher);

            if(_fromType == WE.WarEleType.SOLDIER && _triggerSkill == true)
                ((Soldier)_fromScript).ShellHit(_totalHit, _dieCount, _sdList, _bdList, _toScript, _toType);
            else if(_fromType == WE.WarEleType.BUILDING)
                ((DefenceBuilding)_fromScript).ShellHit(_totalHit, _dieCount, _sdList, _bdList, _toScript, _toType);

            base.ReachTarget();
        }

        private void OnExplosionTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is Soldier sd)
                {
                    float hitValue = 0;
                    if (sd == _toScript)
                    {
                        if (sd.BeAttacked(_fromObj, _fromScript, _fromType, _damage, true, true, out hitValue) == true)
                            _dieCount++;
                    }
                    else
                    {
                        if (sd.BeAttacked(_fromObj, _fromScript, _fromType, _otherDamage, true, true, out hitValue) == true)
                            _dieCount++;
                    }
                    _sdList.Add(sd);
                    if(hitValue > 0)
                        _totalHit += hitValue;
                }
                else if (targets[i] is WarBuilding bd && _canAttackBuilding == true)
                {
                    bd.BeAttacked(_fromObj, _fromScript, _fromType, _damage, out float hitValue);
                    _bdList.Add(bd);
                    if(hitValue > 0)
                        _totalHit += hitValue;
                }
            }
        }

        private SearchShapeDef GetExplosionSearchShape()
        {
            float radius = _damageRange;
            _explosionShape.p_centerOrStartPos = new float2(_toPos.x, _toPos.y);
            _explosionShape.p_radius = radius;
            _explosionShape.p_radiusSq = radius * radius;
            return _explosionShape;
        }

        public override void DeInit()
        {
            _canAttackBuilding = true;
            base.DeInit();
        }

#endregion
    }
}
