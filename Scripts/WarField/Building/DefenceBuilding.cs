using System;
using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using Event = Spine.Event;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using WD = WeaponDefines;

    public class DefenceBuilding : WarBuilding
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected GameObject _weaponPfb = null;
        [SerializeField] protected bool _canAttackBuilding = false; //绝大部份塔都不能攻击建筑
        [SerializeField] protected DefenceConf _defenceConf = null; //just the _bdConf

        [SerializeField] protected WD.WeaponId _weaponId;

        protected DataPool<GameObject>[] _rivalInRange;
        protected WE.WarEleType _rivalType;
        protected GameObject _rival;
        protected MonoBehaviour _rivalScript;
        protected bool _hasRival = false;

        //attack
        protected float _nextAttackInterval;
        protected int _oriIdelCycle, _curIdelCycle; //进入停止攻击的状态

        //cover
        protected Transform _cover;
        protected MaterialPropertyBlock _coverRadarMat, _coverMat;
        private static int _radarX0Propoty = 0; //radar的x0变量
        private SpriteRenderer _coverRadarSprite, _coverSprite;

        //anim
        protected Dictionary<WBD.DfBdAnimType, List<string>> _dfAnimType2NameDic; //WBD.DfBdAnimType -> anim name list (a type animation may has several anmiations)
        protected bool _duringAttackAnim = false;//等待攻击动画播放完成

        protected Dictionary<string, WBD.DfBdAnimType> _dfAnimName2TypeDic; //anim name -> WBD.DfBdAnimType
        protected bool[] _dfHasAnim; //是否有防御各种类型的动画

        private bool _isInCave; //己方防御塔在cave中可能有加成

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _rivalInRange = new DataPool<GameObject>[(int)WE.WarEleType.MAXRIVAL];
            for (int i = 1; i < (int)WE.WarEleType.MAXRIVAL; i++)
            {
                _rivalInRange[i] = new DataPool<GameObject>(true);
            }

            _nextAttackInterval = 0;
            _oriIdelCycle = (int)Utils.CountOfFixUpdate(2); //两秒没有攻击进入Idle状态, 播放idle动画
            _curIdelCycle = 1;

            _coverMat = new MaterialPropertyBlock();
            _coverRadarMat = new MaterialPropertyBlock();
            _isInCave = false;

            _dfHasAnim = new bool[(int)WBD.DfBdAnimType.MAX];
            for (int i = 0; i < (int)WBD.DfBdAnimType.MAX; i++)
                _dfHasAnim[i] = false;
            if (_hasAnimator == true)
            {
                _dfAnimType2NameDic = new Dictionary<WBD.DfBdAnimType, List<string>>();
                _dfAnimName2TypeDic = new Dictionary<string, WBD.DfBdAnimType>();
                _dfAnimName2TypeDic["Idle"] = WBD.DfBdAnimType.IDLE;
                _dfAnimName2TypeDic["StartAttack"] = WBD.DfBdAnimType.STARTATTACK;
                _dfAnimName2TypeDic["Attack"] = WBD.DfBdAnimType.ATTACK;
                _dfAnimName2TypeDic["StopAttack"] = WBD.DfBdAnimType.STOPATTACK;

                var skeletonData = _bdAnimator.Skeleton.Data;
                var animations = skeletonData.Animations;
                foreach (var anim in animations)
                {
                    WBD.DfBdAnimType animType = WBD.DfBdAnimType.MIN;
                    foreach (var key in _dfAnimName2TypeDic.Keys) //先精确匹配
                    {
                        if (anim.Name == key)
                        {
                            animType = _dfAnimName2TypeDic[key];
                            break;
                        }
                    }

                    if (animType == WBD.DfBdAnimType.MIN) //同一type的多个anim进行匹配
                    {
                        foreach (var key in _dfAnimName2TypeDic.Keys) //部分匹配 （e.g.  Attack2）
                        {
                            if (anim.Name.StartsWith(key))
                            {
                                animType = _dfAnimName2TypeDic[key];
                                break;
                            }
                        }
                    }

                    if (animType != WBD.DfBdAnimType.MIN)
                    {
                        if (_dfAnimType2NameDic.ContainsKey(animType) == false)
                        {
                            _dfAnimType2NameDic[animType] = new List<string>();
                            _dfHasAnim[(int)animType] = true;
                        }

                        _dfAnimType2NameDic[animType].Add(anim.Name);
                    }
                    else
                        GameLogger.LogWarning($"{name}: Find unknown anim with name {anim.Name}");
                }
            }
        }

#endregion

#region public functions

        public virtual bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _bdConf = new DefenceConf(conf);
            if (base.InitBuilding(mapId) == false)
                return false;

            _defenceConf = _bdConf as DefenceConf;
            if (_defenceConf.gs_atkRange > 0 && ReferenceEquals(_rangeCollider, null) == false)
                _rangeCollider.radius = _defenceConf.gs_atkRange;
            else if (ReferenceEquals(_rangeCollider, null) == false)
                _rangeCollider.enabled = false;

            if (ReferenceEquals(_weaponPfb, null))
                GameLogger.LogError($"{name}, not set weapon prefab");
            _nextAttackInterval = 0;

            //set cover
            _cover = _transform.Find("Cover");
            _cover.localScale = new Vector3(_defenceConf.gs_atkRange * 2, _defenceConf.gs_atkRange * 2, 1);
            _coverSprite = _transform.Find("Cover/CoverRange").GetComponent<SpriteRenderer>();
            _coverSprite.GetPropertyBlock(_coverMat);
            _coverMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverSprite.SetPropertyBlock(_coverMat);


            _coverRadarSprite = _transform.Find("Cover/Radar").GetComponent<SpriteRenderer>();
            _coverRadarSprite.GetPropertyBlock(_coverRadarMat);
            _coverRadarMat.SetColor("_Color", WE.CoverColorDict["Green"]);
            _coverRadarSprite.SetPropertyBlock(_coverRadarMat);

            _cover.gameObject.SetActive(false);
            if (_radarX0Propoty == 0)
                _radarX0Propoty = Shader.PropertyToID("_x0");

            //anim
            _duringAttackAnim = false;
            return true;
        }

        public override void ChangeMapId(byte mapId)
        {
            base.ChangeMapId(mapId);
            if (_mapId != WE.OnGroundMapIndex)
                _isInCave = true;
        }

        //somehow target die/removed
        public virtual void TargetRemove(WE.WarEleType targetType, GameObject target)
        {
            if (_beInited == false)
                return;

            if (targetType != _rivalType || target != _rival)
                return;

            DelRivalFromList(_rival, _rivalType);
            _rival = null;
            _rivalScript = null;
            _rivalType = WE.WarEleType.MIN;
            _hasRival = false;
        }

        public override void RivalInRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if (colType == WE.WarEleType.BUILDING && _canAttackBuilding == false)
                return;
            AddRivalIntoList(colTarget, colType);
        }

        public override void RivalOutRange(GameObject colTarget, WE.WarEleType colType, WE.FactionType faction)
        {
            if (colType == WE.WarEleType.BUILDING && _canAttackBuilding == false)
                return;

            if (colTarget == _rival)
                TargetRemove(colType, colTarget);
            else
                DelRivalFromList(colTarget, colType);
        }

        //called when bullet hit enemy
        //isDie: wheter rival die
        //rival: the soldier/building be hurt
        public virtual void BullectHit(float hitValue, bool isDie, WE.WarEleType type, object rivalScript)
        {
        }

        //dieCOunt: 范围性攻击死亡个数
        //rivalList: 受到攻击的rivals
        public virtual void ShellHit(float hitValue, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
        }

        public override void SetCoverRange(bool value)
        {
            _cover.gameObject.SetActive(value);
        }

        public override bool CanUpgrade()
        {
            if (base.CanUpgrade() == false)
                return false;
            if (_defenceConf.gs_subType == (int)HumanDefines.DefenceType.BASICTOWER) //只有基础塔可以升级
                return true;
            return false;
        }

        public override void UserSelected(bool firstTime)
        {
            if (firstTime == true)
                if (_faction == WE.FactionType.FRIENDLY)
                    UIBuildingManipulateBar.Instance.ShowBuildingManipulateBar(this); //显示ManipulateBar
        }

        //出售
        public void SellDefenceBd()
        {
            if (_isDestroyed == true)
                return;
            WarResCtrl.Instance.AddRes(WarResDefine.ResTypes.GOLDCOIN, _defenceConf.gs_sellPrice);
            BdDestroy();
        }

#endregion

#region private functions

        protected override void OnBdWork(float deltaTime)
        {
            if (_nextAttackInterval > 0)
                _nextAttackInterval--;

            if (_hasRival == false)
            {
                if (_curIdelCycle > 0)
                {
                    _curIdelCycle--;
                    if (_curIdelCycle == 0)
                    {
                        if (_dfHasAnim[(int)WBD.DfBdAnimType.STOPATTACK] == true)
                        {
                            PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.STOPATTACK][0], false); //播放StopAttack动画
                        }
                        else if (_dfHasAnim[(int)WBD.DfBdAnimType.IDLE] == true)
                        {
                            PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.IDLE][0], true); //没有StopAttack动画,播放idle动画
                        }
                    }
                }

                ChooseRival();
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
                {
                    PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.ATTACK][0], false); //播放Attack动画
                    _duringAttackAnim = true;
                }
                else
                    Attack(); //没有动画
            }
            else if (_duringAttackAnim == false && _nextAttackInterval <= 0)
            {
                if (_dfHasAnim[(int)WBD.DfBdAnimType.ATTACK])
                {
                    PlayBdAnim(0, _dfAnimType2NameDic[WBD.DfBdAnimType.ATTACK][0], false); //播放Attack动画
                    _duringAttackAnim = true;
                }
                else
                    Attack(); //没有动画
            }
        }

        protected virtual void Attack()
        {
            _nextAttackInterval += _defenceConf.gs_atkSpeedCycle;

            float damage = _defenceConf.gs_damage;
            if (_isInCave == true)
                damage += _defenceConf.gs_caveAttackAdd;

            if (damage <= 0)
                return;

            Vector2 startPos = _firePos != null ? _firePos.GetFirePos(_transform.position, _currentDirIndex) : (Vector2)_transform.position;
            int targetGridIndex = ((WarEleParent)_rivalScript).gs_gridIndex;

            if (_defenceConf.gs_weaponType == WeaponDefines.ProjectileTypes.BULLET)
            {
                Vector2 targetPos = Vector2.zero;
                if (_rivalType == WE.WarEleType.SOLDIER)
                    targetPos = ((Soldier)_rivalScript).gs_bullectTargetPos;
                else if (_rivalType == WE.WarEleType.BUILDING)
                    targetPos = ((WarBuilding)_rivalScript).gs_bullectTargetPos;

                WeaponCtrl.Instance.FireBullet(
                    _weaponId, _defenceConf.gs_faction, damage,
                    _mapId, (int)WE.WarEleType.BUILDING, gs_gridIndex,
                    (int)_rivalType, targetGridIndex, true,
                    startPos, targetPos, _weaponPfb);
            }
            else if (_defenceConf.gs_weaponType == WeaponDefines.ProjectileTypes.SHELL)
            {
                Vector2 targetPos = (Vector2)_rivalScript.transform.position;

                WeaponCtrl.Instance.FireBezierShell(
                    _weaponId, _defenceConf.gs_faction, damage,
                    _mapId, (int)WE.WarEleType.BUILDING, gs_gridIndex,
                    (int)_rivalType, targetGridIndex, true,
                    startPos, targetPos,
                    _defenceConf.gs_weaponRange, damage, _weaponPfb);
            }
            //not has NoTargetBullet for now
        }

        //choose a new rival
        protected virtual bool ChooseRival()
        {
            //优先选择士兵
            bool isFound = _rivalInRange[(int)WE.WarEleType.SOLDIER].FindFirst(static (curRival, self) =>
            {
                var rivalScript = curRival.GetComponent<Soldier>();
                self._rival = curRival;
                self._rivalScript = rivalScript;
                self._rivalType = WE.WarEleType.SOLDIER;
                self._hasRival = true;
                return true;
            }, this, out var result);

            if(isFound == true)
                return true;
            if(_canAttackBuilding == false) //如果可以攻击建筑
                return false;

            isFound = _rivalInRange[(int)WE.WarEleType.BUILDING].FindFirst(static (curRival, self) =>
            {
                var rivalScript = curRival.GetComponent<WarBuilding>();
                self._rival = curRival;
                self._rivalScript = rivalScript;
                self._rivalType = WE.WarEleType.BUILDING;
                self._hasRival = true;
                return true;
            }, this, out result);

            return isFound;
        }


        protected void AddRivalIntoList(GameObject rival, WE.WarEleType type)
        {
            _rivalInRange[(int)type].AddItem(rival);
        }

        protected void DelRivalFromList(GameObject rival, WE.WarEleType type)
        {
            _rivalInRange[(int)type].RemoveItem(rival);
        }

        protected override void OnBdDestroy()
        {
            _rival = null;
            _rivalScript = null;
            _rivalType = WE.WarEleType.MIN;
            _hasRival = false;
            for (int i = 1; i < (int)WE.WarEleType.MAXRIVAL; i++)
            {
                _rivalInRange[i].Clear();
            }
        }

        protected override void OnAnimationNotification(TrackEntry trackEntry, Event e)
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
                        _duringAttackAnim = false;
                        Attack();
                    }

                    break;
                default:
                    break;
            }
        }

#endregion
    }
}

