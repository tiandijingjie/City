using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    //山洞
    public class CaveTran : WarBuilding
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected CaveConf _caveConf = null; //just the _bdConf

        private byte _outsideMapId = WE.InvalidMapIndex, _insideMapId = WE.InvalidMapIndex; //_outsideMapId就是地面的mapId， _insideMapId是关联的地下map的id
        private Bounds _insideMapBound;
        //是否开启洞穴，如果开启洞穴那己方士兵就会在洞穴的范围内进入洞穴内，洞穴内如果有地方巢穴就会有敌人从洞穴出来
        //洞穴一旦激活，其中的敌人被消灭完之前无法关闭
        private bool _isOpened = false;

        //cover
        private Transform _cover;
        private MaterialPropertyBlock _coverRadarMat, _coverMat;
        private static int _radarX0Propoty = 0; //radar的x0变量
        private SpriteRenderer _coverRadarSprite, _coverSprite;

        private GameObject _coverInMiniMap;
#endregion

#region private parameters' get set

        public int gs_outsideMapId
        {
            get { return _outsideMapId; }
        }

        public int gs_insideMapId
        {
            get { return _insideMapId; }
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
            _bdConf = new CaveConf(conf);
            if(base.InitBuilding(mapId) == false)
                return false;

            _caveConf = _bdConf as CaveConf;
            _rangeCollider.radius = _caveConf.gs_range;

            //set cover
            _cover = _transform.Find("Cover");
            _cover.localScale = new Vector3(_caveConf.gs_range * 2, _caveConf.gs_range * 2, 1);
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

        //设置cave关联的两个地图
        public bool AddMapToCave(byte undergroundMapId)
        {
            _outsideMapId = WE.OnGroundMapIndex;
            _insideMapId = undergroundMapId;
            return true;
        }

        //因为是NEUTRAL类型的，Friendly/Enemy都是是rival
        public override void RivalInRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if(_isOpened == true)
                return;

            if(colType == WE.WarEleType.BUILDING || faction != WE.FactionType.FRIENDLY) //只能传送己方士兵
                return;

            FriendlySoldier soldier = colTarget.GetComponent<FriendlySoldier>();
            if (soldier != null)
                soldier.StepIntoCave(this, _insideMapId);
        }

        //获取soldier从cave出来的坐标
        //friendly soldier是以cave坐标做垂直对称  pos:进入时的offset
        //enemy soldier 是按照出来的位置的y的比例，cave的覆盖范围内做垂直排列 pos：离开时世界坐标
        public Vector2 GetCaveOutPosition(WE.FactionType faction, Vector2 pos)
        {
            Vector2 cavePos = _transform.position;
            if (faction == WE.FactionType.FRIENDLY)
            {
                return new Vector2(2 * cavePos.x - pos.x, pos.y);
            }
            else
            {
                if(_insideMapBound.size == Vector3.zero)
                    _insideMapBound = WarMapCtrl.Instance.GetMapByIndex(_insideMapId).gs_passablePart;

                float yPercent = (((pos.y - _insideMapBound.min.y) / _insideMapBound.size.y) - 0.5f); //-0.5是为了让y与中心点比较
                return new Vector2(cavePos.x, cavePos.y + yPercent * _caveConf.gs_range);
            }
        }

        //计算soldier离开cave时候的y位置
        //yPercent:soldier离开map时y与map高度的比例
        public float GetStepOutYPos(float yPercent)
        {
            //-0.5是为了居中
            return ((yPercent - 0.5f) * _caveConf.gs_range) + _transform.position.y;
        }

        //获取一个在range范围内的左边的x坐标
        public float GetRandomLeftXInRange()
        {
            return _transform.position.x - (Utils.GetRandomInt() / 100.0f) * _caveConf.gs_range;
        }

        //在minimap中显示覆盖范围
        public void ShowRangeInMinimap()
        {
            _coverInMiniMap.SetActive(true);
        }

        //在minimap中隐藏覆盖范围
        public void HideRangeInMinimap()
        {
            _coverInMiniMap.SetActive(false);
        }
#endregion

#region private functions

        protected override void BindToMiniMapObj()
        {
            base.BindToMiniMapObj();
            //增加覆盖范围的显示
            _coverInMiniMap = new GameObject("Cover", typeof(RectTransform));
            _coverInMiniMap.transform.SetParent(_miniMapObjTransform, false);
            Image image = _coverInMiniMap.AddComponent<Image>();
            image.sprite = UiMiniMapCtrl.Instance.gs_rangeCoverTex;
            //设置cover的大小
            RectTransform rt = _coverInMiniMap.GetComponent<RectTransform>();
            float range = _caveConf.gs_range * Screen.height / WarMapCtrl.Instance.GetMapByIndex(_mapId).gs_passablePart.size.y; //将世界坐标下的范围转成屏幕坐标下的范围
            rt.sizeDelta = new Vector2(range * 2, range * 2);
            rt.anchoredPosition = Vector2.zero;
            HideRangeInMinimap();
        }

#endregion
    }
}

