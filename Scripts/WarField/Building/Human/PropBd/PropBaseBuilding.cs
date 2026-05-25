using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    //道具生成的建筑的基类
    public class PropBaseBuilding : WarBuilding
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected PropBdConf _propBdConf; //just the _bdConf

        protected CircleCollider2D _range;
        protected int _durationCycle;
        protected bool _isInfinite; //无时间限制

        //cover
        protected Transform _cover;
        protected MaterialPropertyBlock _coverRadarMat, _coverMat;
        private static int _radarX0Propoty = 0; //radar的x0变量
        private SpriteRenderer _coverRadarSprite, _coverSprite;

        private bool _isInCave; //己方建筑在cave中可能有加成
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _range = transform.Find("Range")?.GetComponent<CircleCollider2D>();
            _coverMat = new MaterialPropertyBlock();
            _coverRadarMat = new MaterialPropertyBlock();
            _isInCave = false;
        }

#endregion

#region public functions

        public virtual bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _bdConf = new PropBdConf(conf);
            if (base.InitBuilding(mapId) == false)
                return false;

            _propBdConf = _bdConf as PropBdConf;
            if (_propBdConf.gs_duration > 0)
            {
                _durationCycle = (int)Utils.CountOfFixUpdate(_propBdConf.gs_duration);
                _isInfinite = false;
            }
            else
                _isInfinite = true;

            if (_range != null && _propBdConf.gs_range > 0)
            {
                _range.enabled = true;
                _range.radius = _propBdConf.gs_range;

                //set cover
                _cover = _transform.Find("Cover");
                if (_cover != null)
                {
                    _cover.localScale = new Vector3(_propBdConf.gs_range * 2, _propBdConf.gs_range * 2, 1);
                    _coverSprite = _transform.Find("Cover/CoverRange").GetComponent<SpriteRenderer>();
                    _coverSprite.GetPropertyBlock(_coverMat);
                    _coverMat.SetColor("_Color", WE.CoverColorDict["Green"]);
                    _coverSprite.SetPropertyBlock(_coverMat);


                    _coverRadarSprite = _transform.Find("Cover/Radar").GetComponent<SpriteRenderer>();
                    _coverRadarSprite.GetPropertyBlock(_coverRadarMat);
                    _coverRadarMat.SetColor("_Color", WE.CoverColorDict["Green"]);
                    _coverRadarSprite.SetPropertyBlock(_coverRadarMat);

                    _cover.gameObject.SetActive(false);
                }

                if (_radarX0Propoty == 0)
                    _radarX0Propoty = Shader.PropertyToID("_x0");
            }
            else
                _range.enabled = false;

            if (mapId != WE.OnGroundMapIndex)
                _isInCave = true;
            return true;
        }


#endregion

#region private functions

        protected override void OnBdWork(float deltaTime)
        {
            if (_isInfinite == false)
            {
                if (_durationCycle <= 0)
                {
                    if (OnTimeUp() == true) //不能调用BdDestroy，因为不是被敌人摧毁的
                    {
                        WarBuildingCtrl.Instance.RemoveBuilding(this, _bdConf.gs_race, _bdConf.gs_mode, _bdConf.gs_subType, _mapId);
                        gameObject.SetActive(false);
                        base.DeInit();
                    }
                }
                else
                    _durationCycle--;
            }
        }

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            if (changeName == "duration" && _isInfinite == false)
            {
                _durationCycle += (int)Utils.CountOfFixUpdate(_propBdConf.gs_duration - oriValue);
            }
        }

        public override void SetCoverRange(bool value)
        {
            _cover?.gameObject.SetActive(value);
        }

        //存活的时间到了
        //return: true: destroy building
        protected virtual bool OnTimeUp()
        {
            return true;
        }

#endregion
    }
}

