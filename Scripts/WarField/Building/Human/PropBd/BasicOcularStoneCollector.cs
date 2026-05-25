using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    //初级曈石收集器
    public class BasicOcularStoneCollector : PropBaseBuilding
    {
#region public parameters

#endregion

#region private parameters

        protected float _stoneSearchRadius, _sqrtStoneSearchRadius;
        protected CollectionTask _collectionTask;
       // protected List<IGridEntity> _ocularStoneCollection; //每一帧中新进入采集范围的曈石
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        override protected void Awake()
        {
            base.Awake();

           // _ocularStoneCollection = new List<IGridEntity>();
            _collectionTask = new CollectionTask(_transform, 64);
        }
#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            base.InitBuilding(conf, mapId);
            _stoneSearchRadius = _propBdConf.gs_range;
            _sqrtStoneSearchRadius = _stoneSearchRadius * _stoneSearchRadius;
            return true;
        }

        public override void RunNormalTask(float deltaTime)
        {
            // if (_isInfinite == true || _durationCycle > 0)
            // {
            //     SpatialGridManager.Instance.GetEntitys(WE.OnGroundMapIndex, _transform.position, _stoneSearchRadius, _sqrtStoneSearchRadius, (int)WE.WarEleType.OCULARSTONE,
            //         _ocularStoneCollection);
            //     if (_ocularStoneCollection.Count > 0)
            //         _collectionTask.AddCollectionItems(_ocularStoneCollection); //在OnUpdateStatus调用采集的任务
            //     _collectionTask.RunCollectionTask(deltaTime);
            // }
        }

#endregion

#region private functions

        protected override void OnBdWork(float deltaTime)
        {
            base.OnBdWork(deltaTime);
            if(_durationCycle > 0)
                _collectionTask.RunCollectionTask(deltaTime);
        }

        protected override bool OnTimeUp()
        {
            if (_collectionTask.RunCollectionTask(Time.fixedDeltaTime) == 0) //已经到时间了,等待已经被收集的曈石飞过来再摧毁建筑
                return true;
            return false;
        }

        protected override void OnBdDestroy()
        {
            _collectionTask?.Dispose();
        }
#endregion
    }
}
