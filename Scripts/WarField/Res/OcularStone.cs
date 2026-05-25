using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;
    using WE = WarFieldElements;

    //曈石 用于抽卡
    public class OcularStone : MonoBehaviour, ICollection
    {
#region public parameters
        public int p_timeWheelLap; //用来记录WarResCtrl中时间轮的圈数
#endregion

#region private parameters
        [SerializeField] private float _maxMoveSpeed = 15f; //最大移动速度

        private SpriteRenderer _renderer;
        private WRD.ResContainLevel _level;
        private bool _isTaken = false; //已经被捕获或者正飞向hero
        private bool _isTimeout = false;

        private bool _isFollowing = false; //是否正在朝着搜集者飞

        private Transform _transform;
        private int _mapId;

        private int _energyAmt;
        private Transform _gsPosition;

#endregion

#region private parameters' get set

        public bool gs_isFollowing
        {
            get { return _isFollowing; }
        }

        //IGridEntity api
        public int gs_cellIndex { get; set; }
        public sbyte gs_quadrant { get; set; }
        public int gs_entityIndex { get; set; }
        public bool gs_isValid { get; set; }

        public Vector2 gs_position
        {
            get { return _transform.position; }
        }

        public float gs_maxMoveSpeed
        {
            get { return _maxMoveSpeed; }
        }

        //ICollection api
        public Transform gs_transform
        {
            get { return _transform; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _transform = GetComponent<Transform>();
        }

#endregion

#region public function
        public void InitOcularStone(WRD.ResContainLevel level, Vector2 pos, int mapId, int energyAmt)
        {
            gs_cellIndex = -1;
            gs_quadrant = -1;
            gs_entityIndex = -1;
            gs_isValid = true;

            _level = level;
            _renderer.sprite = WarResCtrl.Instance.gs_stoneSprites[(int)_level];
            _renderer.color = Color.white;

            _isTaken = false;
            _isTimeout = false;
            _isFollowing = false;

            //Bounds mapBounds = WarMapCtrl.Instance.GetMapByIndex(mapId).gs_passablePart;
            //_transform.position = new Vector3(_transform.position.x, _transform.position.y, WarFieldUtil.GetZByY(_transform.position.y, mapBounds.min.y));

            _mapId = mapId;
            //SpatialGridManager.Instance.AddEntityIntoGrid(_mapId, (int)WE.WarEleType.OCULARSTONE, this); //加入grid
            _energyAmt = energyAmt;
            _isTimeout = false;

            _maxMoveSpeed += (Utils.GetRandomInt() * 2f - 100f) / 100f;  //最大速度有[-1,1]的抖动
        }

        //被捕获,开始向着目标飞
        public bool OnStartCollection(Transform target)
        {
            if(_isTimeout == true || _isTaken == true)
                return false;

            _isTaken = true;
            _isFollowing = true;

            gs_isValid = false; //不能再次被在grid中查询到了
            //从grid移除,不再属于任何grid cell了
            //SpatialGridManager.Instance.RemoveEntityFromGrid(_mapId, (int)WE.WarEleType.OCULARSTONE, this);
            return true;
        }

        //并不会将gem回收到内存池，gem只有在TimeOut才会一批统一加入内存池
        public void OnCompleteCollection()
        {
            WarResCtrl.Instance.AddRes(WRD.ResTypes.OCULARSTONE, _energyAmt);
            if(_isTimeout == true) //飞到目标之前就已经超时了, 直接释放stone到pool
                DeInitOcularStone();
            else //只是隐藏,等待timeout的时候真正释放, 现在释放但是时间轮中的还没有释放会出问题
                gameObject.SetActive(false);
        }

        //超时消失
        public void TimeOut()
        {
            _isTimeout = true;

            // 已经完成采集
            if (gameObject.activeInHierarchy == false)
            {
                DeInitOcularStone();
                return;
            }

            // 正在飞向采集目标
            if (_isFollowing == true)
                return; // 不处理, TakeEnergyGem 会释放

            _isTimeout = true;
            gs_isValid = false; //不能再次被在grid中查询到了
            //SpatialGridManager.Instance.RemoveEntityFromGrid(_mapId, (int)WE.WarEleType.OCULARSTONE, this); //从grid移除
            DeInitOcularStone();
        }

#endregion

#region private functions

        private void DeInitOcularStone()
        {
            gameObject.SetActive(false);
            WarResCtrl.Instance.ReleaseOcularStoneToPool(this);
            _mapId = -1;
        }

#endregion
    }
}

