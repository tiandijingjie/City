using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE =WarFieldElements;
    using WBD = WarBuildingDefines;

    public class TargetDummyProp : Prop
    {
        private TargetDummyConf _thisConf;

        private SearchArea _tauntSearcher;
        private SearchShapeDef _searchShape;
        private Vector2 _searchCenter;
        private WarBuilding _tauntBuilding;

        public TargetDummyProp()
        {
            _thisConf = new TargetDummyConf();
            _conf = _thisConf;
            _tauntSearcher = new SearchArea(0, OnTauntTargetsFound, GetSearchShape, null, 0);
            _searchShape = new SearchShapeDef { p_shapeType = SearchDefines.SearchShapeType.CIRCLE };
            SearchConditionUtil.AddEnemySoldierConditions(_tauntSearcher);
        }

        public override void ActiveProp()
        {
            UIConstructTask.Instance.StartConstructTask(WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.PROPBD, (int)
                HumanDefines.PropBdType.TARGETDUMMY));
            UIConstructTask.Instance.RegisterConstructCallback(Callback);
        }

        public override bool UseProp()
        {
            Debug.Log("UseProp");
            return base.UseProp();
        }

        private void Callback(WarBuilding bd, bool success, Vector2 position)
        {
            if (success == true && bd != null)
            {
                UseProp();
                _searchCenter = position;
                _tauntBuilding = bd;
                _tauntSearcher.p_mapId = bd.gs_mapId;
                SearchManager.Instance.RegisterSearch(_tauntSearcher);
            }
            else
                base.GiveupProp();
        }

        private void OnTauntTargetsFound(List<IGridNode> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Soldier sd = targets[i] as Soldier;
                if (sd == null)
                    continue;
                sd.BeTaunt(_tauntBuilding, WE.WarEleType.BUILDING);
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            float radius = _thisConf.p_range;
            _searchShape.p_centerOrStartPos = new float2(_searchCenter.x, _searchCenter.y);
            _searchShape.p_radius = radius;
            _searchShape.p_radiusSq = radius * radius;
            return _searchShape;
        }
}
}
