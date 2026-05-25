using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using WE = WarFieldElements;
    using WRD = WarResDefine;

    //宝石矿
    public class GemMine : WarBuilding
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected GemMineConf _gemMineConf = null; //just the _bdConf

        private List<Soldier> _protectors;

        private bool _isOccupied;

        //cover
        private Transform _cover;
        private MaterialPropertyBlock _coverRadarMat, _coverMat;
        private static int _radarX0Propoty = 0; //radar的x0变量
        private SpriteRenderer _coverRadarSprite, _coverSprite;
#endregion

#region private parameters' get set
        public bool gs_isOcuppied //获取占领状态，只有占领和未占领两种
        {
            get { return _isOccupied; }
        }
#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _coverMat = new MaterialPropertyBlock();
            _coverRadarMat = new MaterialPropertyBlock();
        }

#endregion

#region public functions
        public virtual bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _bdConf = new GemMineConf(conf);
            if(base.InitBuilding(mapId) == false)
                return false;

            _gemMineConf = _bdConf as GemMineConf;
            _rangeCollider.radius = _gemMineConf.gs_range;

            //set cover
            _cover = _transform.Find("Cover");
            _cover.localScale = new Vector3(_gemMineConf.gs_range * 2, _gemMineConf.gs_range * 2, 1);
            _coverSprite = _transform.Find("Cover/CoverRange").GetComponent<SpriteRenderer>();
            _coverSprite.GetPropertyBlock(_coverMat);
            _coverMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverSprite.SetPropertyBlock(_coverMat);

            _coverRadarSprite = _transform.Find("Cover/Radar").GetComponent<SpriteRenderer>();
            _coverRadarSprite.GetPropertyBlock(_coverRadarMat);
            _coverRadarMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverRadarSprite.SetPropertyBlock(_coverRadarMat);

            _cover.gameObject.SetActive(true);
            if(_radarX0Propoty == 0)
                _radarX0Propoty = Shader.PropertyToID("_x0");
            _bdSpriteRenderer.color = Color.gray;
            return true;
        }

        //因为是NEUTRAL类型的，Friendly/Enemy都是是rival
        public override void RivalInRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if(_isOccupied == true)
                return;

            if(colType == WE.WarEleType.BUILDING)
                return;

            if(faction == WE.FactionType.FRIENDLY)
            {
                _isOccupied = true;
                WarResCtrl.Instance.OccupyGemMine(_gemMineConf.gs_gemInMine);
                BdDestroy();
            }
        }

#endregion

#region private functions

#endregion
    }
}

