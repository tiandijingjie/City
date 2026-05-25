using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    //地雷
    public class Landmine : PropBaseBuilding
    {
#region public parameters

#endregion

#region private parameters

        protected DataPool<GameObject> _rivalInRange;

        private float _damage;
        private float _prepareTimeCycle; //准备时间
        private float _delayTimeCycle; //延迟爆炸时间
        private bool _beTriggered;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        override protected void Awake()
        {
            base.Awake();
            _rivalInRange = new DataPool<GameObject>(true);
        }
#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            base.InitBuilding(conf, mapId);
            _damage = _propBdConf.gs_specConfs["damage"];

            _prepareTimeCycle = Utils.CountOfFixUpdate(_propBdConf.gs_specConfs["prepareTime"]);
            _delayTimeCycle = Utils.CountOfFixUpdate(_propBdConf.gs_specConfs["delayTime"]);
            _beTriggered = false;
            _bdSpriteRenderer.sprite = Resources.Load<Sprite>("Textures/BuildingTex/Human/Landmine_Not_Active");
            _bodyCollider.enabled = false;
            return true;
        }

        public override void RivalInRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if(colType == WE.WarEleType.SOLDIER)
                AddRivalIntoList(colTarget, colType);
        }

        public override void RivalOutRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if(colType == WE.WarEleType.SOLDIER)
                DelRivalFromList(colTarget, colType);
        }
#endregion

#region private functions

        protected override void OnBdWork(float deltaTime)
        {
            base.OnBdWork(deltaTime);

            if (_prepareTimeCycle > 0)
                _prepareTimeCycle--;
            else
            {
                if (_beTriggered == false)
                {
                    if (_rivalInRange.Count > 0)
                    {
                        _bdSpriteRenderer.sprite = Resources.Load<Sprite>("Textures/BuildingTex/Human/Landmine_Active");
                        _beTriggered = true;
                    }
                }
                else
                {
                    _delayTimeCycle--;
                    if (_delayTimeCycle <= 0) //爆炸
                    {
                        //Lamda
                        _rivalInRange.ForEach(static (rival, self) =>
                        {
                            rival.GetComponent<Soldier>().BeAttacked(null, null, WE.WarEleType.SOLDIER, self._damage, false, false, out var value);
                        }, this);//Lamda

                        WarBuildingCtrl.Instance.RemoveBuilding(this, _bdConf.gs_race, _bdConf.gs_mode, _bdConf.gs_subType, _mapId);
                        gameObject.SetActive(false);
                        base.DeInit();//自我摧毁
                    }
                }
            }
        }

        protected void AddRivalIntoList(GameObject rival, WE.WarEleType type)
        {
            if(type == WE.WarEleType.SOLDIER)
                _rivalInRange.AddItem(rival);
        }

        protected void DelRivalFromList(GameObject rival, WE.WarEleType type)
        {
            if(type == WE.WarEleType.SOLDIER)
                _rivalInRange.RemoveItem(rival);
        }

        protected override void OnBdDestroy()
        {
            _rivalInRange.Clear();
        }
#endregion
    }
}
