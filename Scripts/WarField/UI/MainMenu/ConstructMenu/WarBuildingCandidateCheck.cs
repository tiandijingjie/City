using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using WE = WarFieldElements;

    //在用户选择建筑位置时在建筑上临时添加一个脚本，用来做位置检测
    public class WarBuildingCandidateCheck : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        private bool _buildable; //当前位置是否可以建造
        private SpriteRenderer _bdSpriteRenderer;
        private float _radius; //建筑的body
        private bool _isBuildable;
        private BuildingConf _conf;

        private SearchArea _overlapSearcher;
        private SearchShapeDef _searchShape;

        //cover
        private Transform _cover;
        private MaterialPropertyBlock _coverRadarMat, _coverMat;
        private static int _radarX0Propoty = 0;
        private float _radarSize;
        private float _radarStep;

        //radar sprite
        private SpriteRenderer _coverRadarSprite, _coverSprite;
        private int _candidateSortOrder = 10; //让candidate显示在所有东西的前面
#endregion

#region private parameters' get set

        public bool gs_isBuildable
        {
            get { return _isBuildable; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _isBuildable = true;
            _bdSpriteRenderer = transform.Find("BdSprite").GetComponent<SpriteRenderer>();
            var trans = transform.Find("Range");
            if(trans != null)
                trans.gameObject.SetActive(false); //禁用攻击范围collider
            trans = transform.Find("ClickCollider");
            if(trans != null)
                trans.gameObject.SetActive(false); //禁用鼠标检测collider

            _bdSpriteRenderer.sortingLayerName = "TopEffect";
            _bdSpriteRenderer.sortingOrder = _candidateSortOrder;
            _radius = GetComponent<StaticBodyAuthoring>().gs_radius;
            _radarStep = 1 / 1.5f; //1.5s完整的显示一次
            _radarSize = 0;

            _coverMat = new MaterialPropertyBlock();
            _coverRadarMat = new MaterialPropertyBlock();
            _searchShape = new SearchShapeDef
            {
                p_shapeType = SearchDefines.SearchShapeType.CIRCLE,
                p_radiusSq = _radius * _radius,
            };
            _overlapSearcher = new SearchArea(0, OnOverlapFound, GetSearchShape, null, WarMapCtrl.Instance.gs_curMapId);
            _overlapSearcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WE.WarEleType.BUILDING,
                p_targetSubType = -1,
                p_includeFlags = 0,
                p_excludeFlags = 1 << (int)SpatialDefines.EntitySpecType.HIDE,
            });
            _overlapSearcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WE.WarEleType.SOLDIER,
                p_targetSubType = -1,
                p_includeFlags = 0,
                p_excludeFlags = 1 << (int)SpatialDefines.EntitySpecType.HIDE,
            });
            _overlapSearcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WE.WarEleType.OBSTACLE,
                p_targetSubType = -1,
                p_includeFlags = 0,
                p_excludeFlags = 1 << (int)SpatialDefines.EntitySpecType.HIDE,
            });
        }

        private void Update()
        {
            if (SearchManager.Instance != null)
            {
                _overlapSearcher.p_mapId = WarMapCtrl.Instance.gs_curMapId;
                SearchManager.Instance.RegisterSearch(_overlapSearcher);
            }

            if (_conf.gs_mode == WBD.BuildingMode.DEFENCE)
            {
                _radarSize += Time.deltaTime * _radarStep;
                if(_radarSize >= 1f)
                    _radarSize = 0;
                _coverRadarMat.SetFloat(_radarX0Propoty, _radarSize);
                _coverRadarSprite.SetPropertyBlock(_coverRadarMat);
            }
        }

#endregion

#region public functions

        public void InitCandidateCheck(BuildingConf buildingConf)
        {
            _conf = buildingConf;

            float range = 0;
            switch (buildingConf.gs_mode)
            {
                case WBD.BuildingMode.DEFENCE:
                    range = ((DefenceConf)buildingConf).gs_atkRange;
                    break;
                case WBD.BuildingMode.PROPBD:
                    range = ((PropBdConf)buildingConf).gs_range;
                    break;
                default:
                    break;
            }
            if(range <= 0)
                return;
            _cover = transform.Find("Cover");
            if (_cover != null)
            {
                _cover.localScale = new Vector3(range * 2, range * 2, 1);
                _coverSprite = transform.Find("Cover/CoverRange").GetComponent<SpriteRenderer>();
                _coverSprite.sortingLayerName = "Front";
                _coverSprite.sortingOrder = _candidateSortOrder - 1;
                _coverSprite.GetPropertyBlock(_coverMat);
                _coverMat.SetColor("_Color", WE.CoverColorDict["Green"]);
                _coverSprite.SetPropertyBlock(_coverMat);

                _coverRadarSprite = transform.Find("Cover/Radar").GetComponent<SpriteRenderer>();
                _coverRadarSprite.sortingLayerName = "Front";
                _coverRadarSprite.sortingOrder = _candidateSortOrder - 2;
                _coverRadarSprite.GetPropertyBlock(_coverRadarMat);
                _coverRadarMat.SetColor("_Color", WE.CoverColorDict["Green"]);
                _coverRadarSprite.SetPropertyBlock(_coverRadarMat);


                _cover.gameObject.SetActive(true);
                if (_radarX0Propoty == 0)
                    _radarX0Propoty = Shader.PropertyToID("_x0");
            }
        }
#endregion

#region private functions

        private void OnOverlapFound(List<IGridNode> targets)
        {
            bool blocked = targets != null && targets.Count > 0;
            if (blocked)
            {
                if (_isBuildable)
                {
                    _bdSpriteRenderer.color = Color.red;
                    _isBuildable = false;
                }
            }
            else
            {
                if (!_isBuildable)
                {
                    _bdSpriteRenderer.color = Color.white;
                    _isBuildable = true;
                }
            }
        }

        private SearchShapeDef GetSearchShape()
        {
            Vector3 pos = transform.position;
            _searchShape.p_centerOrStartPos = new float2(pos.x, pos.y);
            _searchShape.p_radius = _radius;
            _searchShape.p_radiusSq = _radius * _radius;
            return _searchShape;
        }

#endregion
    }
}
