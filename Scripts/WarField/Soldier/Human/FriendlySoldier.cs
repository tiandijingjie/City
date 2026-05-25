using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using ED = EffectDefines;

    public class FriendlySoldier : Soldier, MainEffectFinishCb
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected SD.TroopType _troopType;

        private List<CaveTran> _caveEnteredList; //进入过的山洞
        private CaveTran _curCaveEnter; //当前进入的山洞，出去也应该是这个cave
        private Vector2 _caveOffset; //进入山洞是与山洞中心的offset，用于计算出洞时的位置
        private int _spawnIndex; //对应于friendly barrack的spawnarr index

        //transport
        private SD.TransportStage _transportStage;
        private Portal _transportTargetPortal;
        private Vector3 _transportOffset; //用于计算传送完成之后的位置

        protected bool _achieveAssemblyPoint = false; //到达集合点, 到达集合点之前,寻路中y的受力会增加
        protected float _startYDis; //出身时与homeY的距离的绝对值
#endregion

#region private parameters' get set

        public override SD.TroopType gs_troopType
        {
            get { return _troopType; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            gameObject.tag = "FriendlySoldier";
            _faction = WE.FactionType.FRIENDLY;
            SearchConditionUtil.AddEnemySoldierAndBuildingConditions(_rivalSearcher);
        }

#endregion

#region public functions

        //for friendly soldier, it can change skills
        public bool InitSoldier(uint skillType, int spawnIndex, byte mapId, Vector3 spawnTargetPos)
        {
            if (skillType == 0) //not set skill
            {
                if(_curSkill.IsNullOrEmpty() == false)
                    _curSkill.Clear();
            }
            else
            {
                if (_skills.TryGetValue(skillType, out Skill sk) == false)
                {
                    GameLogger.LogError($"{_sdName} not found skill {skillType}");
                    return false;
                }

                foreach (Skill skill in _skills.Values)
                {
                    skill.InitSkillObject();
                }

                if (_curSkill == null)
                {
                    _curSkill = new List<Skill>();
                    _curSkill.Add(sk);
                }
                else if (_curSkill[0] != sk) //士兵只可能有一个skill
                {
                    _curSkill.Clear();
                    _curSkill.Add(sk);
                }
            }
            StepIntoMap(-1, mapId, spawnTargetPos.y);
            _achieveAssemblyPoint = false;
            _startYDis = Mathf.Abs(_homeY - _transform.position.y);
            _transportStage = SD.TransportStage.MIN;
            InitSoldier(spawnIndex, mapId);
            return true;
        }

        public void StepIntoCave(CaveTran cave, byte toMapId)
        {
            if (_caveEnteredList == null)
                _caveEnteredList = new List<CaveTran>();
            else if (_caveEnteredList.Contains(cave) == true) //一个士兵不能两次踏入同一个个cave
                return;

            _caveEnteredList.Add(cave);
            _curCaveEnter = cave;
            _caveOffset = _transform.position - cave.gs_transform.position;

            Vector2 pos = WarMapCtrl.Instance.GetMapByIndex(toMapId).GetEnterSdPos(_troopType, _spawnIndex); //获取新的世界坐标
            StepIntoMap(_mapId, toMapId, pos.y); //记录homeY
            SoldierCtrl.Instance.TransferSoldiderToMap(this, pos, _mapId, toMapId);
        }

        public override void StepOutofCave(CaveTran cave, byte fromMapId, byte toMapId, float yPercent)
        {
            if (_curCaveEnter == null)
            {
                GameLogger.LogError($"{_sdName} not in the cave");
                return;
            }

            if (_curCaveEnter != cave)
            {
                GameLogger.LogError($"Step out cave is not the enter one current {_mapId}  want to setout {cave.gs_insideMapId}");
            }

            Vector2 pos = (Vector2)_curCaveEnter.gs_transform.position + new Vector2(-_caveOffset.x, _caveOffset.y);
            base.StepOutofCave(cave, fromMapId, toMapId, pos.y);
            SoldierCtrl.Instance.TransferSoldiderToMap(this, pos, _mapId, toMapId);
            _curCaveEnter = null;
        }

        public override void DeInit()
        {
            base.DeInit();
            _caveEnteredList = null;
            _curCaveEnter = null;
        }

        //特效结束后的回调
        public void MainEffectFinish(string effInfo = null) //传送
        {
            if (_transportStage == SD.TransportStage.IN)
            {
                _transportStage = SD.TransportStage.OUT;
                while (ReferenceEquals(_transportTargetPortal, null) == false)
                {
                    _transportTargetPortal = _transportTargetPortal.Receive(gameObject, ref _transportOffset);
                }

                _transform.position += _transportOffset;

                EffectBase effect = EffectCtrl.Instance.AddEffectAt(_transform.position, ED.EffectType.TRANSPORTOUT, _mapId); //播放传送完成特效
                effect.AddMainEffectFinishCb(this);

                iTween.ScaleTo(_sdAnimator.gameObject, iTween.Hash(
                    "scale", Vector3.one,
                    "time", 0.3f,
                    "delay", 0.25f
                ));
            }
            else if (_transportStage == SD.TransportStage.OUT)
            {
                SetSDActive(true);
                _transportStage = SD.TransportStage.MIN;
            }
        }

        public void StartTransport(Portal targetPortal, Vector3 offset)
        {
            if(_transportStage != SD.TransportStage.MIN)
                return;

            SetSDActive(false);
            _transportStage = SD.TransportStage.IN;
            _transportTargetPortal = targetPortal;
            _transportOffset = offset;
            EffectBase effect = EffectCtrl.Instance.AddEffectAt(_transform.position, ED.EffectType.TRANSPORTIN, _mapId);
            effect.AddMainEffectFinishCb(this);

            iTween.ScaleTo(_sdAnimator.gameObject, iTween.Hash(
                "scale", Vector3.one * 0.01f,
                "time", 0.3f
            ));
        }

        public override float GetHomeYMul()
        {
            if (_achieveAssemblyPoint == true)
                return 1;
            float dis = Mathf.Abs(_homeY - _transform.position.y);
            if (dis > 0.3f)
                return 2;
            _achieveAssemblyPoint = true;
            return 1f;
        }

#endregion

#region private functions

        protected virtual bool InitSoldier(int spawnIndex, byte mapId)
        {
            //子类需要先调用自身的逻辑，再调用这个父类的函数
            //比如获取IndividualData

            if (base.InitSoldier(mapId) == false)
                return false;

            _spawnIndex = spawnIndex;
            if (_curSkill.IsNullOrEmpty() == false)
            {
                _curSkill[0].InitSkillObject();
                _curSkill[0].ActiveSkill();
                _curSkill[0].SetTimeStep(_curState.p_skillTimeStep);
            }

            if (_curTalent.IsNullOrEmpty() == false)
            {
                _curTalent[0].InitTalentObject();
                _curTalent[0].ActiveTalent();
            }

            return false;
        }

        protected override void BindToMiniMapObj()
        {
            base.BindToMiniMapObj();

            var img = _miniMapObjTransform.Find("ElePic").GetComponent<Image>(); //建筑、士兵的图标
            img.sprite = UiMiniMapCtrl.Instance.gs_soldierIcon;
            _miniMapObjTransform.sizeDelta = new Vector2(5, 5);
            img.color = Color.green; //己方是绿色
        }

#endregion
    }
}

