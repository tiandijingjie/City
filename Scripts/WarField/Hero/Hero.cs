using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

using HeroGeneral;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using SKD = SkillDefines;
    using BFD = BuffDefines;
    using HD = HeroDefines;

    public class Hero : Soldier
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected SD.TroopType _troopType;
        [SerializeField] protected HumanDefines.HeroType _sdType;
        [SerializeField] protected bool _debugPickStoneRange = false;

        private IndividualData _genericData;

        protected Vector2 _skillPosOffset; //指向性技能的起点的相对位置,不能用transform.position  因为看起来有偏差
        protected int _rebornTimeCycle = 0; //死亡时重生时间

        //specific states
        protected float _stoneSearchRadius, _sqrtStoneSearchRadius;
        protected List<SoldierStateChange> _stoneRadiusChangeList;
        protected object _stoneSearchRadiusLock;

        //collect ocular stone
        protected CollectionTask _collectionTask;
#endregion

#region private parameters' get set

        public override SD.TroopType gs_troopType
        {
            get { return _troopType; }
        }

        public override int gs_sdType
        {
            get { return (int)_sdType; }
        }

        public IndividualData gs_genericData
        {
            get { return _genericData; }
        }

        public Vector2 gs_skillPos
        {
            get { return _skillPosOffset + (Vector2)_transform.position; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            gameObject.tag = "FriendlySoldier";
            _faction = WE.FactionType.FRIENDLY;
            _enableSearchCollider = false; //hero not need to search rivals
            _isHero = true;
            _skillPosOffset = _transform.Find("SkillPos").localPosition;
        }

        private void OnDestroy()
        {
            if (_collectionTask != null)
                _collectionTask.Dispose();
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (_debugPickStoneRange == true)
            {
                Gizmos.color = Color.yellow;
                Handles.color = Color.yellow;
                if (_transform != null)
                    Handles.DrawWireDisc(_transform.position, Vector3.forward, _stoneSearchRadius);
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = Color.yellow;
            Handles.color = Color.yellow;
            if (_transform != null)
                Handles.DrawWireDisc(_transform.position, Vector3.forward, _stoneSearchRadius);
        }
#endif

#endregion

#region public functions

        public virtual bool InitHero(byte mapId, HeroGenericIndividualData.IndividualDataType[] talents)
        {
            _genericData = SoldierCtrl.Instance.GetHeroGenericIndividualData();
            _sdConf = new SoldierConf(SoldierCtrl.Instance.GetHeroConf(_sdType));
            _sdConfBeInit = true;

            _entitySubType = WE.EncodeEntitySubType((byte)_race, (byte)_troopType, (byte)_sdType, 0);
            if (base.InitSoldier(mapId) == false)
                return false;

            _stoneSearchRadius = _curState.p_specConfs["stoneSearchRadius"];
            _sqrtStoneSearchRadius = _stoneSearchRadius * _stoneSearchRadius;
            _stoneRadiusChangeList = new List<SoldierStateChange>();
            _stoneSearchRadiusLock = new object();

            InitGenericSkill();
            InitGenericTalent(talents);

            _collectionTask = new CollectionTask(_transform, 64);
            return true;
        }

        //SelectionManager 的键盘控制移动与 Hold 驻守控制链
        public bool UserManipulate(int src, Vector2 targetPos)
        {
            if (_curStatus == SD.SoldierStatus.DIE || _curStatus == SD.SoldierStatus.MIN || _curStatus == SD.SoldierStatus.ERROR)
                return false;

            HD.HeroManipulateSrc manipSrc = (HD.HeroManipulateSrc)src;

            switch (manipSrc)
            {
                case HD.HeroManipulateSrc.HOLDPRESS:
                    // H 键原地死守：强制清空一切可能残留的走位或 A* 路径，抹除当前追击仇恨，原地进入绝对防守
                    _curMoveCmd = SD.MoveCmd.MIN;
                    _cmdTargetPos = GD.InvalidVector2;
                    _aStarWaypoints.Clear();
                    _lastAStarTargetPos = GD.InvalidVector2;

                    if (_rival != null)
                        TargetRemove(_rivalType, _rival);

                    ChangeStatusTo(SD.SoldierStatus.IDLE);
                    break;

                case HD.HeroManipulateSrc.UNSELECT:
                    break;
            }

            return true;
        }

        //鼠标右键与 A-move
        public override bool UserManipulate(int flowIndex, Vector2 targetPos, bool isAttackMove)
        {
            if (_curStatus == SD.SoldierStatus.DIE || _curStatus == SD.SoldierStatus.MIN || _curStatus == SD.SoldierStatus.ERROR)
                return false;

            ClearCurrentLocalFlowField(); // 注销潜在的流场占用

            _currentFlowIndex = flowIndex; // 接收来自控制层的 -1 信号，宣告不采纳流场，启动纯单体 A* 寻路 pipeline
            _cmdTargetPos = targetPos;

            // 核心修复：重置影子终点变量与防抖冷却，允许主线程在接下来的 ExecuteGenericMove 中瞬间开启高层 A* 重新拉线
            _lastAStarTargetPos = GD.InvalidVector2;
            _aStarWaypoints.Clear();
            _pathRecalculateCooldown = 0f;

            // 根据是否是 A地板，切入 MANMOVEATTACKTOPOS（沿途拦截反击）或 MANMOVETOPOS（死心眼赶路）
            _curMoveCmd = isAttackMove ? SD.MoveCmd.MANMOVEATTACKTOPOS : SD.MoveCmd.MANMOVETOPOS;

            if (_curStatus == SD.SoldierStatus.IDLE || _curStatus == SD.SoldierStatus.ATTACKTATGET)
                ChangeStatusTo(SD.SoldierStatus.MOVE);

            return true;
        }

        public override object AddSpecificStateChange(string stateName, float value, GD.CalDeltaType calType, out float oriValue)
        {
            oriValue = 0;

            //not check _beInited, because some skill may change state in awake
            SoldierStateChange change = new SoldierStateChange();
            change.p_deltaValue = value;
            change.p_calType = calType;

            switch (stateName)
            {
                case "stoneSearchRadius":
                    lock (_stoneSearchRadiusLock)
                    {
                        oriValue = _curState.p_specConfs["stoneSearchRadius"];
                        change.p_prvState = oriValue;
                        _stoneRadiusChangeList.Add(change);
                        _curState.p_specConfs["stoneSearchRadius"] = Utils.CalDeltaValue(oriValue, value, calType); //only change the hp max
                        if (_curState.p_specConfs["stoneSearchRadius"] <= 0)
                            _curState.p_specConfs["stoneSearchRadius"] = 0;
                        _stoneSearchRadius = _curState.p_specConfs["stoneSearchRadius"];
                        _sqrtStoneSearchRadius = _stoneSearchRadius * _stoneSearchRadius;
                    }

                    break;
                default:
                    break;
            }
            return change;
        }

        public override bool ModifySpecificStateChange(string stateName, object indexStruct, bool isDelete, float modifyValue,
            GD.CalDeltaType calType, bool isModyfyBaseValue)
        {
            if (indexStruct == null)
                return false;
            //not check _beInited, because some skill may change state in awake
            List<SoldierStateChange> list = null;
            switch (stateName)
            {
                case "stoneSearchRadius":
                    list = _stoneRadiusChangeList;
                    break;
                default:
                    break;
            }
            if(list == null)
                return false;

            SoldierStateChange changeSt = (SoldierStateChange)indexStruct;
            int index = list.IndexOf(changeSt);
            float prvValue = list[index].p_prvState;
            if (isDelete == true)
                list.RemoveAt(index);
            else
            {
                if (calType == GD.CalDeltaType.MIN)
                {
                    GameLogger.LogError($"{this._sdName}: ModifyStateChange fail, not set calType");
                    return false;
                }

                if(isModyfyBaseValue == false) //change SoldierStateChange.p_deltaValue
                    changeSt.p_deltaValue = Utils.CalDeltaValue(changeSt.p_deltaValue, modifyValue, calType); //modify the change value ,then recalculate state
                else
                {
                    changeSt.p_deltaValue = modifyValue;
                    changeSt.p_calType = calType;
                }
            }

            float oriValue = 0;
            switch (stateName)
            {
                case "stoneSearchRadius":
                    lock (_stoneSearchRadiusLock)
                    {
                        oriValue = _curState.p_specConfs["stoneSearchRadius"];
                        _curState.p_specConfs["stoneSearchRadius"] = CalculateState(list, index, prvValue);
                        if (_curState.p_specConfs["stoneSearchRadius"] <= 0)
                            _curState.p_specConfs["stoneSearchRadius"] = 0;
                        _stoneSearchRadius = _curState.p_specConfs["stoneSearchRadius"];
                        _sqrtStoneSearchRadius = _stoneSearchRadius * _stoneSearchRadius;
                    }
                    break;
                default:
                    return false;
            }
            return true;
        }

        public override void RunNormalTask(float deltaTime)
        {
            // SpatialGridManager.Instance.GetEntitys(WE.OnGroundMapIndex, _transform.position, _stoneSearchRadius, _sqrtStoneSearchRadius, (int)WE.WarEleType.OCULARSTONE, _ocularStoneCollection);
            // if (_ocularStoneCollection.Count > 0)
            //     _collectionTask.AddCollectionItems(_ocularStoneCollection);  //在OnUpdateStatus调用采集的任务
        }
#endregion

#region private functions

        /// <summary>
        /// 核心重写：覆盖基类的 Idle 状态机。死守驻守职责，拒绝无脑推线
        /// </summary>
        protected override void ProcessIdleStatus()
        {
            // 如果尚有玩家在按 WASD 或点击鼠标下达的强制移动指令，换去 MOVE 状态执行
            if (_curMoveCmd == SD.MoveCmd.MANMOVETOPOS || _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
            {
                ChangeStatusTo(SD.SoldierStatus.MOVE);
                return;
            }

            // 自动化反击：只有当敌人【真正踏入我的攻击射程内】时，才在原地直接进入战打断，开始迎敌输出
            if (IsRivalValid())
            {
                if (_rivalChooseType == SD.TargetDetectType.INATTACKRANGE)
                {
                    ChangeStatusTo(SD.SoldierStatus.ATTACKTATGET);
                    return;
                }
            }

            // 铁律：没有强制位移指令，且周边没有进入射程的活物时，英雄死守 IDLE 驻守，绝对不转入 NORMAL 全局推线或胡乱追击
            _curMoveCmd = SD.MoveCmd.MIN;
        }

        //当玩家命令（A地板、右键走位）抵达终点或取消时触发
        protected override void OnMoveCmdFinished()
        {
            ClearCurrentLocalFlowField();
            _curMoveCmd = SD.MoveCmd.MIN; // 卸载当前的移动状态标识
            _currentFlowIndex = (_faction == WE.FactionType.FRIENDLY) ? WE.FriendlyFlowFieldDefaultId : WE.EnemyFlowFieldDefaultId;
            _aStarWaypoints.Clear();
            _lastAStarTargetPos = GD.InvalidVector2; // 清空影子变量锁

            ChangeStatusTo(SD.SoldierStatus.IDLE); // 战术目的达成，英雄安稳回归 IDLE 原地守卫
        }

        protected override void OnSoldierDie()
        {
            GameLogger.LogWarning("Hero die");

            float rebornTime = _curState.p_spawnTime;

            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.REBORNTRIGGER].IsNullOrEmpty()) //skill
            {
                for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.REBORNTRIGGER].Count; i++)
                {
                    rebornTime = _skillTriggerArr[(int)SKD.SkillTriggerType.REBORNTRIGGER][i].SkillRebornTrigger(rebornTime);
                }
            }

            if (!_talentTriggerArr[(int)SKD.SkillTriggerType.REBORNTRIGGER].IsNullOrEmpty()) //talent
            {
                for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.REBORNTRIGGER].Count; i++)
                {
                    rebornTime = _talentTriggerArr[(int)SKD.SkillTriggerType.REBORNTRIGGER][i].TalentRebornTrigger(rebornTime);
                }
            }

            if (rebornTime <= 0)
                rebornTime = 1; //最少等待1s

            _rebornTimeCycle = (int)Utils.CountOfFixUpdate(rebornTime);
            ChangeStatusTo(SD.SoldierStatus.REBORN);
        }

        protected override bool OnUpdateStatus()
        {
            if (_curStatus == SD.SoldierStatus.DIE)
            {
                _curState.p_health = _curState.p_maxHealth; //此时把hp回满,免得触发skill/talent
            }
            else if (_curStatus == SD.SoldierStatus.REBORN)
            {
                if (_rebornTimeCycle > 0)
                    _rebornTimeCycle--;
                else
                {
                    ChangeStatusTo(SD.SoldierStatus.IDLE);
                }

                return false;
            }
            else
                _collectionTask.RunCollectionTask(Time.fixedDeltaTime);
            return true;
        }

        protected virtual void InitGenericSkill()
        {
            var skillTransform = _transform.Find("Skill");
            if (skillTransform == null)
            {
                GameLogger.LogError("not find Skill GameObject！");
                return;
            }

            var skillGo = skillTransform.gameObject;

            Skill invincibleSkill = skillGo.AddComponent<HeroInvincibleSkill>();
            invincibleSkill.InitSkillObject();
            invincibleSkill.ActiveSkill();

            Skill hasteSkill = skillGo.AddComponent<HeroHasteSkill>();
            hasteSkill.InitSkillObject();
            hasteSkill.ActiveSkill();
        }

        protected virtual void InitGenericTalent(HeroGenericIndividualData.IndividualDataType[] talents)
        {
            var talentTransform = _transform.Find("Talent");
            if (talentTransform == null)
            {
                GameLogger.LogError("not find Talent GameObject！");
                return;
            }

            var talentGo = talentTransform.gameObject;
            for (int i = 0; i < talents.Length; i++)
            {
                Talent talent = null;
                switch (talents[i])
                {
                    case HeroGenericIndividualData.IndividualDataType.QUICKATTACK:
                        talent = talentGo.AddComponent<HeroQuickAttackTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.HEAVYATTACK:
                        talent = talentGo.AddComponent<HeroHeavyAttackTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.DOUBLEDAMAGE:
                        talent = talentGo.AddComponent<HeroDoubleDamageTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.ALLIEDATTACKBOOST:
                        talent = talentGo.AddComponent<HeroAlliedAttackBoostTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.ALLIEDATTACKSPEEDBOOST:
                        talent = talentGo.AddComponent<HeroAlliedAttackSpeedBoostTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.ALLIEDSKILLBOOST:
                        talent = talentGo.AddComponent<HeroAlliedSkillBoostTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.SUICIDESQUAD:
                        talent = talentGo.AddComponent<HeroSuicideSquadTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.SELFDESTRUCT:
                        talent = talentGo.AddComponent<HeroSelfDestructTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.STRONG:
                        talent = talentGo.AddComponent<HeroStrongTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.COUNTERATTACK:
                        talent = talentGo.AddComponent<HeroCounterattackTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.REVERSAL:
                        talent = talentGo.AddComponent<HeroReversalTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.REJUVENATION:
                        talent = talentGo.AddComponent<HeroRejuvenationTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.REBIRTH:
                        talent = talentGo.AddComponent<HeroRebirthTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.GUARDIANINSTINCT:
                        talent = talentGo.AddComponent<HeroGuardianInstinctTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.HOSTILEINSTINCT:
                        talent = talentGo.AddComponent<HeroHostileInstinctTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.NEWCHANCE:
                        talent = talentGo.AddComponent<HeroNewChanceTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.FLEE:
                        talent = talentGo.AddComponent<HeroFleeTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.LASTBREATH:
                        talent = talentGo.AddComponent<HeroLastBreathTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.FLEETFOOT:
                        talent = talentGo.AddComponent<HeroFleetFootTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.PROSPERITY:
                        talent = talentGo.AddComponent<HeroProsperityTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.BOUNTY:
                        talent = talentGo.AddComponent<HeroBountyTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.ALCHEMICALMASTERY:
                        talent = talentGo.AddComponent<HeroAlchemecalMasteryTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.COLLECTOR:
                        talent = talentGo.AddComponent<HeroCollectorTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.EXPERIENCE:
                        talent = talentGo.AddComponent<HeroExperienceTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.SWIFTREBIRTH:
                        talent = talentGo.AddComponent<HeroSwiftRebirthTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.GAMBLER:
                        talent = talentGo.AddComponent<HeroGamblerTalent>();
                        break;
                    case HeroGenericIndividualData.IndividualDataType.DECEPTION:
                        talent = talentGo.AddComponent<HeroDeceptionTalent>();
                        break;
                    default:
                        GameLogger.LogError($"not has talent {talents[i]} !!");
                        continue;
                }

                if (talent == null)
                {
                    GameLogger.LogError($"Fail to add talent {talents[i]} to Hero !!");
                    continue;
                }

                talent.InitTalentObject();
                talent.ActiveTalent();
            }
        }

        protected override void SdOnSkillUpdate()
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER][i].SkillUpdate();
            }
        }

        protected override float SdOnSkillBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].Count; i++)
            {
                if(damage == 0)
                    return 0;
                damage = _skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][i].SkillBeAttackPre(damage, rival, rivalType, isByPass);
            }

            return damage;
        }

        protected override void SdOnSkillBeAttackPost(float damage, bool isDead, MonoBehaviour rivalScript, WE.WarEleType rivalType, bool isByPass)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][i]
                    .SkillBeAttackPost(damage, isDead, rivalScript, rivalType, isByPass);
            }
        }

        protected override bool SdOnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WE.WarEleType rivalType,
            out float damage)
        {
            damage = hit; // 初始值就是基础伤害
            bool ret = true;

            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].Count; i++)
            {
                float newDamage;
                ret = _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][i].SkillDoAttackPre(damage, rivalScript, rivalType, out newDamage);

                if (ret == false)
                    return false;

                damage = newDamage; // 传递给下一个技能
            }

            return true;
        }

        protected override void SdOnSkillDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][i].SkillDoAttackPost(hit, isDead, rivalScript, rivalType);
            }
        }

        protected override void SdOnSkillDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList,
            List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][i]
                    .SkillDoAttackPost(hit, dieCount, rivalSdList, rivalBdList, target, type);
            }
        }

        protected override void SdOnSkillDie()
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER][i].SkillDie();
            }
        }

        protected override void SdOnSkillParterEnter(GameObject parter, WE.WarEleType type)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER][0].SkillParterEnter(parter, type);
            }
        }

        protected override void SdOnSkillParterLeave(GameObject parter, WE.WarEleType type)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER][i].SkillParterLeave(parter, type);
            }
        }

        protected override void SdOnSkillRivalEnter(GameObject rival, WE.WarEleType type)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER][i].SkillRivalEnter(rival, type);
            }
        }

        protected override void SdOnSkillRivalLeave(GameObject rival, WE.WarEleType type)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER][i].SkillRivalLeave(rival, type);
            }
        }

        protected override void SdOnSkillActivatedTrigger(string name)
        {
            for (int i = 0; i < _skillTriggerArr[(int)SKD.SkillTriggerType.ACTIVETRIGGER].Count; i++)
            {
                _skillTriggerArr[(int)SKD.SkillTriggerType.ACTIVETRIGGER][i].SkillActivatedTrigger(name);
            }
        }

        protected override void SdOnTalentUpdate()
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER][i].TalentUpdate();
            }
        }

        protected override float SdOnTalentBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].Count; i++)
            {
                if(damage <= 0)
                    return 0;
                damage = _talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][i]
                    .TalentBeAttackPre(damage, rival, rivalType, isByPass);
            }

            return damage;
        }

        protected override void SdOnTalentBeAttackPost(float damage, bool isDead, MonoBehaviour rivalScript, WE.WarEleType rivalType, bool isByPass)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][i]
                    .TalentBeAttackPost(damage, isDead, rivalScript, rivalType, isByPass);
            }
        }

        protected override bool SdOnTalentDoAttackPre(float hit, MonoBehaviour rivalScript, WE.WarEleType rivalType,
            out float damage)
        {
            damage = hit; // 初始值就是基础伤害
            bool ret = true;

            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].Count; i++)
            {
                float newDamage;
                ret = _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][i].TalentDoAttackPre(damage, rivalScript, rivalType, out newDamage);

                if (ret == false)
                    return false;

                damage = newDamage; // 传递给下一个技能
            }

            return true;
        }

        protected override void SdOnTalentDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][i].TalentDoAttackPost(hit, isDead, rivalScript, rivalType);
            }
        }

        protected override void SdOnTalentDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList,
            List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][i]
                    .TalentDoAttackPost(hit, dieCount, rivalSdList, rivalBdList, target, type);
            }
        }

        protected override void SdOnTalentDie()
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER][i].TalentDie();
            }
        }

        protected override void SdOnTalentParterEnter(GameObject parter, WE.WarEleType type)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER][0].TalentParterEnter(parter, type);
            }
        }

        protected override void SdOnTalentParterLeave(GameObject parter, WE.WarEleType type)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER][i].TalentParterLeave(parter, type);
            }
        }

        protected override void SdOnTalentRivalEnter(GameObject rival, WE.WarEleType type)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER][i].TalentRivalEnter(rival, type);
            }
        }

        protected override void SdOnTalentRivalLeave(GameObject rival, WE.WarEleType type)
        {
            for (int i = 0; i < _talentTriggerArr[(int)SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER].Count; i++)
            {
                _talentTriggerArr[(int)SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER][i].TalentRivalLeave(rival, type);
            }
        }

        protected override void BindToMiniMapObj()
        {
            base.BindToMiniMapObj();

            _miniMapObj.name = gameObject.name;
            var img = _miniMapObjTransform.Find("ElePic").GetComponent<Image>(); //建筑、士兵的图标
            img.sprite = UiMiniMapCtrl.Instance.gs_soldierIcon;
            _miniMapObjTransform.sizeDelta = new Vector2(5, 5);
            img.color = Color.blue; //英雄暂时用蓝色
        }
#endregion
    }
}

