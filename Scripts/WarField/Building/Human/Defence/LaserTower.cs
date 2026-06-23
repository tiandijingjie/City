using System.Collections;
using System.Collections.Generic;
using Spine;
using UnityEngine;
using Event = UnityEngine.Event;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using ED = EffectDefines;
    using WBD = WarBuildingDefines;

    public class LaserTower : DefenceBuilding
    {
#region public parameters

#endregion

#region private parameters

        private class LaserInfo
        {
            public LineRenderer p_laser;
            public bool p_hasRival;
            public Soldier p_rival;
            public Vector3 p_endPos;
            public float p_nextAttackInterval;
        }

        [SerializeField] private GameObject _laserPfb;

        private List<LaserInfo> _laserInfos;
        private DataPool<GameObject> _rivalNotAttack; //范围内没有被攻击的目标
        private DataPool<GameObject> _rivalInAttack; //范围内正在被攻击的目标
        private int _laserMaxCount, _laserCount;
        private float _bombRange, _bombDamage;

        private object _listLock = new object();
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _laserInfos = new List<LaserInfo>();
            _rivalNotAttack = new DataPool<GameObject>(false); //不加锁，是有独立的锁_listLock
            _rivalInAttack = new DataPool<GameObject>(false);
        }

#endregion

#region public functions
        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            base.InitBuilding(conf, mapId);
            _canAttackBuilding = false;
            _laserMaxCount = (int)Mathf.Round(_defenceConf.gs_specConfs["laserCnt"]);
            _bombRange = _defenceConf.gs_specConfs["bombRange"];
            _bombDamage = _defenceConf.gs_specConfs["bombDamage"];
            for (int i = 0; i < _laserMaxCount; i++)
            {
                LaserInfo laserInfo = new LaserInfo();
                laserInfo.p_laser = Instantiate(_laserPfb, _transform).GetComponent<LineRenderer>();
                laserInfo.p_laser.enabled = false;
                laserInfo.p_rival = null;
                laserInfo.p_hasRival = false;
                laserInfo.p_nextAttackInterval = 0;
                _laserInfos.Add(laserInfo);
            }

            // Bind explosion VFX once per element type (harmless if called by multiple towers)
            EffectCtrl.Instance.BindEffectAnimWithEntity((uint)ED.EffectType.EXPLOSION, "Explosion", out _);
            _laserCount = 0;
            return true;
        }

        public override void RivalInRange(GameObject colTarget, WarFieldElements.WarEleType colType, WE.FactionType faction)
        {
            if(colType == WE.WarEleType.BUILDING)
                return;

            if(_rivalInAttack.Contains(colTarget) == true || _rivalNotAttack.Contains(colTarget) == true)
                return;

            lock (_listLock)
            {
                _rivalNotAttack.AddItem(colTarget); //在OnBdWork中添加laser
            }
        }

        public override void RivalOutRange(GameObject colTarget, WarFieldElements.WarEleType colType, WE.FactionType faction)
        {
            if(colType == WE.WarEleType.BUILDING)
                return;
            lock (_listLock)
            {
                if (_rivalInAttack.Contains(colTarget) == true)
                {
                    LaserInfo info = GetLaserInfo(colTarget.GetComponent<Soldier>());
                    if (info != null)
                        TargetRemove(info);
                    _rivalInAttack.RemoveItem(colTarget);
                }
                else
                    _rivalNotAttack.RemoveItem(colTarget);
            }
        }

#endregion

#region private functions

        protected override void OnBdWork(float deltaTime)
        {
            if (_laserCount == 0 && _rivalInAttack.Count == 0 && _rivalNotAttack.Count == 0)
            {
                if (_curIdelCycle > 0)
                {
                    _curIdelCycle--;
                    if (_curIdelCycle == 0)
                    {
                        if (_dfHasAnim[(int)WBD.DfBdAnimType.STOPATTACK] == true)
                            PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.STOPATTACK][0], false); //播放StopAttack动画
                        else if (_dfHasAnim[(int)WBD.DfBdAnimType.IDLE] == true)
                            PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.IDLE][0], true); //没有StopAttack动画,播放idle动画
                    }
                }
                return;
            }

            if (_curIdelCycle == 0) //认为此时_nextAttackInterval<=0
            {
                _curIdelCycle = _oriIdelCycle; //恢复idle计时
                if (_dfHasAnim[(int)WBD.DfBdAnimType.STARTATTACK] == true)
                {
                    PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.STARTATTACK][0], false); //播放StartAttack动画
                    _duringAttackAnim = true;
                }
                else if (_dfHasAnim[(int)WBD.DfBdAnimType.ATTACK])
                    PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.ATTACK][0], true); //laser tower的攻击动画是loop的
            }

            if(_duringAttackAnim == true) //等待STARTATTACK播放完成才能开始攻击
                return;

            for (int i = _laserMaxCount - 1; i >= 0; i--)
            {
                LaserInfo laserInfo = _laserInfos[i];
                if (laserInfo.p_hasRival == true)
                {
                    laserInfo.p_nextAttackInterval--;
                    if (laserInfo.p_nextAttackInterval <= 0)
                    {
                        Attack(laserInfo);
                        laserInfo.p_nextAttackInterval += _defenceConf.gs_atkSpeedCycle;
                    }

                    if (laserInfo.p_endPos != laserInfo.p_rival.gs_transform.position)
                    {
                        laserInfo.p_endPos = laserInfo.p_rival.gs_transform.position;
                        laserInfo.p_laser.SetPosition(1, laserInfo.p_laser.transform.InverseTransformPoint(laserInfo.p_endPos));
                    }
                }
                else
                {
                    if (_laserCount < _laserMaxCount && _rivalNotAttack.Count > 0)
                    {
                        lock (_listLock)
                        {
                            laserInfo = GetNotWorkLaser();
                            laserInfo.p_hasRival = true;
                            laserInfo.p_rival = _rivalNotAttack.GetByIndex(_rivalNotAttack.Count - 1).GetComponent<Soldier>();
                            _laserCount++;
                            _rivalNotAttack.RemoveItemAt(_rivalNotAttack.Count - 1);
                            _rivalInAttack.AddItem(laserInfo.p_rival.gameObject);
                        }
                        laserInfo.p_endPos = laserInfo.p_rival.gs_transform.position;
                        laserInfo.p_laser.SetPosition(1, laserInfo.p_laser.transform.InverseTransformPoint(laserInfo.p_endPos));
                        laserInfo.p_laser.enabled = true;
                    }
                }
            }
        }

        private void Attack(LaserInfo laserInfo)
        {
            bool isDie = laserInfo.p_rival.BeAttacked(gameObject, this, WE.WarEleType.BUILDING, _defenceConf.gs_damage, true, true,
                out float hitValue);
            if(isDie == true)
                TargetRemove(laserInfo);
        }

        private void TargetRemove(LaserInfo laserInfo)
        {
            if(_beInited == false)
                return;

            if(laserInfo.p_rival.gs_curStatus == SD.SoldierStatus.DIE || laserInfo.p_rival.gs_curStatus == SD.SoldierStatus.MIN)
                Explode(laserInfo.p_rival);  //rival被打死了 爆炸
            laserInfo.p_laser.enabled = false;
            laserInfo.p_rival = null;
            laserInfo.p_nextAttackInterval = 0;
            _laserCount--;
        }

        private LaserInfo GetLaserInfo(Soldier rival)
        {
            //can not add lock
            int count = _laserInfos.Count;
            for (int i = 0; i < count; i++)
            {
                if (_laserInfos[i].p_hasRival == true)
                {
                    if (_laserInfos[i].p_rival == rival)
                        return _laserInfos[i];
                }
            }

            return null;
        }

        private LaserInfo GetNotWorkLaser()
        {
            //can not add lock
            int count = _laserInfos.Count;
            for (int i = 0; i < count; i++)
            {
                if (_laserInfos[i].p_hasRival == false)
                    return _laserInfos[i];
            }

            return null;
        }

        private void Explode(Soldier rivalSd)
        {
            Vector3 pos = rivalSd.gs_transform.position;

            CircleAreaEffectAnim ca = EffectCtrl.Instance.AcquireCircleAreaEffect();
            ca.p_effectAnimId   = (uint)ED.EffectType.EXPLOSION;
            ca.p_worldPos       = pos;
            ca.p_searchRadius   = _bombRange;
            ca.p_mapId          = _mapId;
            ca.p_targetFaction  = WE.FactionType.ENEMY;
            ca.p_searchTarget   = CircleAreaEffectAnim.SearchTarget.Soldier;
            ca.p_onTargetsFound = OnExplosionTargets;
            ca.p_onEvent        = OnExplosionEvent;
            ca.p_searchTime = SearchManager.Instance.GetTimeForASyncSearchCnt(1); //异步查询一次

            EffectHandle handle = EffectCtrl.Instance.AddEffectAt(pos, ca);
            ca.Activate(handle);
        }

        private void OnExplosionTargets(List<IGridNode> targets, CircleAreaEffectAnim anim)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is Soldier sd)
                    sd.BeAttacked(null, null, WE.WarEleType.MIN, _bombDamage, false, false, out _);
            }
        }

        private void OnExplosionEvent(int eventId, CircleAreaEffectAnim anim)
        {
            if(eventId != -1)
                anim.StartSearch();
        }

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            switch (changeName)
            {
                case "bombRange":
                    _bombRange = _defenceConf.gs_specConfs["bombRange"];
                    break;
                case "laserCnt": //不加锁，因为只会添加，不会减少，同时先加list，再改变_laserMaxCount
                {
                    int lCnt = (int)Mathf.Round(_defenceConf.gs_specConfs["laserCnt"]);
                    int d = lCnt - _laserInfos.Count;
                    if (d > 0)
                    {
                        for (int i = 0; i < d; i++)
                        {
                            LaserInfo laserInfo = new LaserInfo();
                            laserInfo.p_laser = Instantiate(_laserPfb, _transform).GetComponent<LineRenderer>();
                            laserInfo.p_laser.enabled = false;
                            laserInfo.p_rival = null;
                            laserInfo.p_nextAttackInterval = 0;
                            _laserInfos.Add(laserInfo);
                        }
                    }

                    _laserMaxCount = lCnt;
                }
                    break;
                default:
                    break;
            }
        }

        protected override void OnAnimationNotification(TrackEntry trackEntry, Spine.Event e)
        {
            switch (e.Data.Name)
            {
                case "Finish":
                    if (_curIdelCycle <= 0) //StopAttack动画播放完
                    {
                        if(_dfHasAnim[(int)WBD.DfBdAnimType.IDLE] == true)
                            PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.IDLE][0], true); //播放idle动画
                    }
                    else //StartAttack or Attack finish
                    {
                        if (_dfHasAnim[(int)WBD.DfBdAnimType.ATTACK])
                            PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.ATTACK][0], true); //laser tower的攻击动画是loop的
                        _duringAttackAnim = false; //StartAttack 动画播放完成可以开始攻击了
                    }

                    break;
                default:
                    break;
            }
        }
#endregion
    }
}

