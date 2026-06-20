using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using WarField.Anim;

namespace WarField
{
    using GD = GlobalDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using SKD = SkillDefines;
    using BFD = BuffDefines;

    public class Soldier : WarEleParent, IProtector, IAnimInfo
    {
#region public parameters

#endregion

#region private parameters

        //为了保证_stateChangeArray不被外部引用，SoldierStateChange必须是private
        protected class SoldierStateChange
        {
            public float p_prvState;
            public float p_deltaValue;
            public GD.CalDeltaType p_calType;
        }

        // 黑名单机制
        protected struct BlacklistEntry
        {
            public GameObject p_rival;
            public float p_timer;
        }

        [SerializeField] protected WE.RaceType _race;
        [SerializeField] protected bool _inDebug; //during running can debug some soldier
        [SerializeField] protected bool _debugRange; //show soldier search/attack/skill ranges
        [SerializeField] protected string _sdName;
        [SerializeField] protected SD.SoldierStatus _curStatus;
        [SerializeField] protected GameObject _rival; //rival
        [SerializeField] protected bool _isRemote; //是不是远程攻击
        [SerializeField] protected bool _isHero = false; //这个表示的是人族的英雄
        [SerializeField] protected SD.BehaviorMode _behaviorMode;

        //可以被manipulate的soldier 不允许有search collider
        [SerializeField] protected bool _enableSearchCollider, _enableAttackCollider; //默认情况下各个collider是不是启用

        protected GD.DirDef _moveDir = GD.DirDef.NULLDIR;
        protected Vector3 _moveSpeed;
        protected SD.SoldierStatus _prvStatus;

        protected bool _isBusy;
        protected SD.TargetDetectType _rivalChooseType;  // in which way the current target was chosen, if the new target chosen priority higher than the current one, will change the target
        protected WE.WarEleType _rivalType;
        protected WarEleParent _rivalScript;
        protected bool _isForceRival; //是否被嘲讽
        protected System.Object _targetLock; //在选择rival时加锁
        protected bool _rivalChanged;

        // 每个士兵一把锁（替代原来每种状态一把锁，减少 14×N 个堆对象 → N 个，修复瓶颈4）
        protected object _stateLock;

        //_stateChangeArray在soldier init的时候会重新new，如果被其他地方引用了会引起内存泄露，禁止被外部引用 ！！！！
        protected List<SoldierStateChange>[] _stateChangeArray;

        // 静态对象池：跨所有士兵实例共享，彻底消除 AddStateChange 时的每帧堆分配
        private static readonly Queue<SoldierStateChange> s_changePool = new Queue<SoldierStateChange>(1024);

        // init in solider instance InitSoldier, e.g. Infantry.InitSoldier
        //and it not the conf get fomr soldier ctrl, but a new conf init by the conf from the soldier ctrl
        protected SoldierConf _sdConf;
        protected IndividualData _oriIndividualData, _curIndividualData;
        protected bool _sdConfBeInit; //alread get original conf, also init in solider instance InitSoldier
        [SerializeField] protected SoldierState _curState = null; //the attribute combine _sdConf with skills and attributes
        protected float _curPhyResistance;

        protected float _oriAttackInterval; // the circle count of the fix update
        protected float _nextAttackInterval;
        protected bool _isBorned = false; //born 动画播放

        //attack
        protected bool _atkValid;
        protected float _atkDamage;
        protected MonoBehaviour _atkRivalScript;
        protected WE.WarEleType _atkRivalType;

        //skill
        protected List<Skill>[] _skillTriggerArr; //the skill register self's type into this array tobe trigger
        protected Dictionary<uint, Skill> _skills; //all of the skill, no matter active or not
        [SerializeField] protected List<Skill> _curSkill = null;

        //talent
        protected List<Talent> _curTalent = null; //the talent script
        protected List<Talent>[] _talentTriggerArr; //the tanlent register self's type into this array tobe trigger

        //buff
        protected SoldierBuff[] _buffArray; //all of the buffs that soldier can have
        protected List<SoldierBuff>[] _buffsInEffect; //the buffs that is in effect
        protected List<Buff> _deactiveBuffList;

        //animation
        // 在texture2dArray中寻址所需的当前播放的帧动画
        protected int _currentDirIndex = 0; //帧动画的方向
        protected SD.SoldierAnimType _curAnimType; //the animation is playing now
        protected Skill _skillInAnim; //point to the skill of the animation is playing, call SkillAnimFinish
        protected AnimRenderProxy _animProxy;

        //half of soldier body size, used for calculate the bullect shoot out  position
        protected Vector2 _halfBodySize;

        //protector behavior mode
        protected INeedBeProtect _protectTarget; //守护的建筑
        protected Vector3 _guardPos; //守护模式下站岗的位置，不会改变
        protected object _behaviorLock;

        // 指令驱动移动
        protected SD.MoveCmd _curMoveCmd = SD.MoveCmd.MIN, _prvMoveCmd = SD.MoveCmd.MIN;
        protected Vector2 _cmdTargetPos = GD.InvalidVector2; // 指令目标坐标或方向

        //searchers
        protected SearchClosest _rivalSearcher; //用于查找距离最近的rival
        protected SearchShapeDef _rivalSearchShape; //查找rival的范围
        protected float _skillRadius; //技能半径, skill的SearchShapeDef由每个技能独立实现

        protected bool _isActive; //是否可以移动、攻击、播放动画  soldier有可能在某些情况下不能移动，并且停止动画 (e.g. 传送开始和结束)

        [SerializeField] protected float _homeY; //如果绕开障碍物,之后还会移动回到原来的y
        protected float _onGDY = -9999; //记录地面地图的y

        protected DynamicBodyAuthoring _body;

        // 防卡死与黑名单机制
        protected List<BlacklistEntry> _blacklistedRivals = new List<BlacklistEntry>();
        protected Vector2 _lastCheckPos; //上一次检查的位置
        protected float _stuckTimer = 0f;
        protected const float _stuckCheckInterval = 2.5f; // 2.5秒检测一次
        protected const float _blakeListDuration = 5.0f; // 黑名单持续5秒

        // 寻路缓存与标记
        protected List<Vector2> _aStarWaypoints = new List<Vector2>();
        protected int _currentWaypointIndex = 0;
        protected float _pathRecalculateCooldown = 0f; // A* 防抖
        protected int _currentFlowIndex; // 当前使用的流场ID，默认 friendly是0   enemy是1
        protected Vector2 _desiredMoveDir;
        protected Vector2 _lastAStarTargetPos = GD.InvalidVector2; //上一次a*计算时的_cmdTargetPos, 防止抖动

        protected Vector2 _lastFramePos;          // 记录上一帧的绝对坐标，用于每帧动能比对
        protected float _arrivalStuckTimer = 0f;  // 毫秒级微观拥挤计时器

        // 单帧位移向量的指数滑动平均(EMA), 仅用于动画朝向选择.
        // SoldierMoveJob 在障碍物边缘会产出 "撞墙→切线投影→反弹→再撞墙" 的高频方向翻转,
        // 单帧 actualMove 角度差可达 180°, 任何静态阈值/角度滞回都拦不住. 用 EMA 做时间维度低通,
        // 撞墙时正负位移自动相消, EMA 量级会塌到阈值之下, 直接锁住朝向避免贴图换面闪动.
        protected Vector2 _animMoveDirEMA = Vector2.zero;

#endregion

#region private parameters' get set

        public SoldierState gs_curState
        {
            get { return _curState; }
        }

        public SD.SoldierStatus gs_curStatus
        {
            get { return _curStatus; }
        }

        public WE.WarEleType gs_rivalType
        {
            get { return _rivalType; }
        }

        public WarEleParent gs_rivalScript
        {
            get { return _rivalScript; }
        }

        public GameObject gs_rival
        {
            get { return _rival; }
        }

        public Vector2 gs_bullectTargetPos
        {
            get
            {
                return _transform.position + new Vector3(_halfBodySize.x * _transform.localScale.x, _halfBodySize.y, 0);
            }
        }

        public WE.RaceType gs_race
        {
            get { return _race; }
        }

        public bool gs_isHero //查询是不是己方英雄
        {
            get { return _isHero; }
        }

        public SD.SoldierLevel gs_sdLevel
        {
            get { return _sdConf.p_level; }
        }

        public GD.DirDef gs_moveDir
        {
            get { return _moveDir; }
        }

        public IndividualData gs_curIndividualData
        {
            get { return _curIndividualData; }
        }

        public IndividualData gs_oriIndividualData
        {
            get { return _oriIndividualData; }
        }

        public virtual SD.TroopType gs_troopType
        {
            get { return 0; }
        }

        public virtual int gs_sdType
        {
            get { return 0; }
        }

        public float gs_homeY
        {
            get { return _homeY; }
        }

        public float gs_skillRadius
        {
            get { return _skillRadius; }
        }

        public Vector2 gs_desiredMoveDir
        {
            get { return _desiredMoveDir; }
        }

        public SD.MoveCmd gs_curMoveCmd
        {
            get { return _curMoveCmd; }
        }

        public int gs_currentFlowIndex
        {
            get { return _currentFlowIndex; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _needBindMiniMap = true;
            _warEleType = WE.WarEleType.SOLDIER;
            _moveSpeed = Vector3.zero;

            _halfBodySize = new Vector2(0.5f, 0.5f);

            _curStatus = SD.SoldierStatus.INIT; //不能调用ChangeStatusTo,因为这个时候entity还没有被创建出来
            _rivalChooseType = SD.TargetDetectType.MIN;
            _rival = null;
            _rivalType = WE.WarEleType.MIN;
            _rivalScript = null;
            _targetLock = new System.Object();

            _curPhyResistance = 0;
            _oriAttackInterval = 0;
            _nextAttackInterval = 1; //>0 make sure can call AttackPre() when status change
            _sdConfBeInit = false;

            //rival searcher
            _rivalSearcher = new SearchClosest(-1, OnClosestRivalFound, GetSearchShape, this, -1); //mapid先设置成-1, 在init的时候再赋值
            _rivalSearcher.p_getExcludeCall = GetExcludeGridIndices;
            _rivalSearchShape = new SearchShapeDef();
            _rivalSearchShape.p_shapeType = SearchDefines.SearchShapeType.CIRCLE;

            //skill
            _skillTriggerArr = new List<Skill>[(int)SKD.SkillTriggerType.MAX];
            for (var i = 0; i < _skillTriggerArr.Length; i++)
            {
                _skillTriggerArr[i] = new List<Skill>();
            }

            _skills = new Dictionary<uint, Skill>();

            var skillObj = _transform.Find("Skill");
            if (skillObj != null)
            {
                Skill[] skills = skillObj.GetComponents<Skill>();
                int count = skills.Length;
                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        uint skType = skills[i].gs_skillType;
                        _skills.Add(skType, skills[i]);
                    }
                }
            }

            //talent
            _talentTriggerArr = new List<Talent>[(int)SKD.SkillTriggerType.MAX];
            if (_faction == WE.FactionType.FRIENDLY) //只有人族有天赋
            {
                for (var i = 0; i < _talentTriggerArr.Length; i++)
                {
                    _talentTriggerArr[i] = new List<Talent>();
                }

                if (_isHero == false) //hero的天赋是在init时动态分配的
                {
                    Transform talent = _transform.Find("Talent");
                    if (talent != null)
                        _curTalent = new List<Talent>(talent.GetComponents<Talent>());
                }
            }

            //buff
            _buffArray = new SoldierBuff[(int)BFD.SoldierBuffType.MAX];
            InitBuffArray();

            _buffsInEffect = new List<SoldierBuff>[(int)BFD.BuffTriggerType.MAX];
            _deactiveBuffList = new List<Buff>();
            for (int i = 1; i < (int)BFD.BuffTriggerType.MAX; i++)
				_buffsInEffect[i] = new List<SoldierBuff>();

            _stateLock        = new object();
            _stateChangeArray = new List<SoldierStateChange>[(int)SD.StateSoldierEffectType.MAX];

            //behavior
            _behaviorLock = new object();
            _cmdTargetPos = GD.InvalidVector2;
            _body = GetComponent<DynamicBodyAuthoring>();

            //animation
            Transform animTransform = transform.Find("SoldierAnim");
            if (animTransform != null)
            {
                _animProxy = animTransform.GetComponent<AnimRenderProxy>();
                _animProxy.InitProxy(this, gameObject, (int)SD.SoldierAnimType.MAX);
            }
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            // if(_debugRange == false)
            //     return;
            //
            // Vector3 realCenter = transform.position ;
            //
            // Gizmos.color = Color.cyan;
            // Gizmos.DrawWireSphere(realCenter, _curState.p_searchRange);
            // Gizmos.color = Color.red;
            // Gizmos.DrawWireSphere(realCenter, _curState.p_attackRange);

            // if (_cmdTargetPos != GD.InvalidVector2) //绘制目标位置
            // {
            //     // 将 Vector2 包装成当前层级的 Vector3 坐标
            //     Vector3 target3D = new Vector3(_cmdTargetPos.x, _cmdTargetPos.y, transform.position.z);
            //
            //     //在点击处画一个红色的实心中心定位球
            //     Gizmos.color = Color.red;
            //     Gizmos.DrawSphere(target3D, 0.22f);
            //
            //     //外围追加一个大圆环，防止视角拉得太高时看不清
            //     Gizmos.DrawWireSphere(target3D, 0.5f);
            //
            //     //画一个标志性的 Rts 红十字准心线，彻底钉死世界坐标
            //     Gizmos.DrawLine(target3D + Vector3.left * 1.0f, target3D + Vector3.right * 1.0f);
            //     Gizmos.DrawLine(target3D + Vector3.down * 1.0f, target3D + Vector3.up * 1.0f);
            //
            //     // 从当前位置拉一条紫色的激光牵引线连接到目标点，明确当前单位正在对齐哪个指令
            //     Gizmos.color = Color.magenta;
            //     Gizmos.DrawLine(transform.position, target3D);
            // }

            // if (_aStarWaypoints != null && _aStarWaypoints.Count > 0)
            // {
            //     // 用绿色绘制路径主干线段
            //     Gizmos.color = Color.green;
            //
            //     // 绘制从当前位置到下一个拐弯点的连线
            //     if (_currentWaypointIndex < _aStarWaypoints.Count)
            //     {
            //         Vector3 currentPos = transform.position;
            //         Vector3 nextWaypoint = new Vector3(_aStarWaypoints[_currentWaypointIndex].x, _aStarWaypoints[_currentWaypointIndex].y, currentPos.z);
            //         Gizmos.DrawLine(currentPos, nextWaypoint);
            //     }
            //
            //     // 依次绘制后续各拐弯点之间的连线
            //     for (int i = _currentWaypointIndex; i < _aStarWaypoints.Count - 1; i++)
            //     {
            //         Vector3 startNode = new Vector3(_aStarWaypoints[i].x, _aStarWaypoints[i].y, transform.position.z);
            //         Vector3 endNode = new Vector3(_aStarWaypoints[i + 1].x, _aStarWaypoints[i + 1].y, transform.position.z);
            //         Gizmos.DrawLine(startNode, endNode);
            //     }
            //
            //     // 用黄色高亮球体标记所有尚未踩完的路径节点（路标点）
            //     Gizmos.color = Color.yellow;
            //     for (int i = _currentWaypointIndex; i < _aStarWaypoints.Count; i++)
            //     {
            //         Vector3 waypointPos = new Vector3(_aStarWaypoints[i].x, _aStarWaypoints[i].y, transform.position.z);
            //         Gizmos.DrawSphere(waypointPos, 0.15f); // 0.15半径的小球
            //     }
            //
            //     // 用红球单独标出 A* 终点
            //     Gizmos.color = Color.red;
            //     Vector3 finalTargetPos = new Vector3(_cmdTargetPos.x, _cmdTargetPos.y, transform.position.z);
            //     Gizmos.DrawSphere(finalTargetPos, 0.22f);
            // }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            // if(_debugRange == true)
            //     return;
            //
            // Vector3 realCenter = transform.position ;
            //
            // Gizmos.color = Color.cyan;
            // Gizmos.DrawWireSphere(realCenter, _curState.p_searchRange);
            // Gizmos.color = Color.red;
            // Gizmos.DrawWireSphere(realCenter, _curState.p_attackRange);
        }
#endif

#endregion

#region public functions

        //fix update
        public override void RunFixTask(float deltaTime)
        {
            if (_beInited == false)
                return;

            if(_isPaused == true)
                return;

            if (_isActive == false)
                return;

            if (_curStatus == SD.SoldierStatus.ERROR || _curStatus == SD.SoldierStatus.MIN)
                return;

            if (_isBusy == true)
                return;

            _isBusy = true;
            _desiredMoveDir = Vector2.zero; // 每帧默认静止，只有进入 MOVE 且有方向时才赋值
            try
            {
                // 敌人黑名单倒计时
                for (int i = _blacklistedRivals.Count - 1; i >= 0; i--)
                {
                    var entry = _blacklistedRivals[i];
                    entry.p_timer -= deltaTime;
                    if (entry.p_timer <= 0 || entry.p_rival == null)
                        _blacklistedRivals.RemoveAt(i);
                    else
                        _blacklistedRivals[i] = entry;
                }

                if (_curState.p_health < _curState.p_maxHealth)
                    _curState.p_health += _curState.p_hpInc;

                //enter some special status
                if (_curStatus != SD.SoldierStatus.BORN) //出生动画无法打断
                {
                    bool statusSet = false;
                    if (_curStatus != SD.SoldierStatus.INTERRUPT && _curStatus != SD.SoldierStatus.DIE)
                    {
                        if (_buffArray[(int)BFD.SoldierBuffType.STUN].gs_isActive == true)
                        {
                            ChangeStatusTo(SD.SoldierStatus.INTERRUPT);
                            statusSet = true;
                        }
                        else if (_buffArray[(int)BFD.SoldierBuffType.STUCK].gs_isActive == true)
                        {
                            ChangeStatusTo(SD.SoldierStatus.INTERRUPT);
                            statusSet = true;
                        }
                        else if (_buffArray[(int)BFD.SoldierBuffType.FREEZE].gs_isActive == true)
                        {
                            ChangeStatusTo(SD.SoldierStatus.INTERRUPT);
                            statusSet = true;
                        }
                        else if (_buffArray[(int)BFD.SoldierBuffType.SHAPCHANGE].gs_isActive == true)
                        {
                            ChangeStatusTo(SD.SoldierStatus.INTERRUPT);
                            statusSet = true;
                        }
                    }

                    if (statusSet == false)
                    {
                        if (_curStatus != SD.SoldierStatus.RELEASESKILL)
                        {
                            if (_skillInAnim != null)
                            {
                                ChangeStatusTo(SD.SoldierStatus.RELEASESKILL);
                                statusSet = true;
                            }
                        }
                    }
                }

                //check animation
                CheckAnimation();
                UpdateFrameAnimation(deltaTime);

                //do someting in the every status
                if (OnUpdateStatus() == true)
                {
                    if (_curStatus == SD.SoldierStatus.INIT)
                    {
                        if (_isBorned == false)
                            ChangeStatusTo(SD.SoldierStatus.BORN);
                        else
                            ChangeStatusTo(SD.SoldierStatus.IDLE);
                    }
                    else if (_curStatus == SD.SoldierStatus.BORN)
                    {
                        if (_isBorned == true)
                        {
                            ChangeStatusTo(SD.SoldierStatus.IDLE);
                        }
                        else
                        {
                            if (_animProxy.gs_hasStateAnim[(int)SD.SoldierAnimType.BORN] == true) //wait born animation finish notification
                                return; //not check buff or update skill

                            _isBorned = true;
                        }

                        return; //not check buff or update skill
                    }
                    else if (_curStatus == SD.SoldierStatus.IDLE)
                    {
                        if (_behaviorMode == SD.BehaviorMode.PROTECT)
                        {
                            if (_prvStatus != _curStatus)
                            {
                                ChangeSearchStatus(false);
                                _prvStatus = _curStatus;
                            }
                            if (_rival != null)
                            {
                                if (_rivalChooseType == SD.TargetDetectType.BEATTACK || _rivalChooseType == SD.TargetDetectType.INPROTECTRANGE) //在守护的情况下建筑被入侵或者自己被攻击了才能反击
                                {
                                    FaceToRival();
                                    ChangeStatusTo(SD.SoldierStatus.MOVE);
                                    _curMoveCmd = SD.MoveCmd.MOVETORIVAL;
                                    _behaviorMode = SD.BehaviorMode.NORMAL; //恢复成正常状态
                                    ChangeSearchStatus(true);
                                }
                            }
                        }
                        else if (_behaviorMode == SD.BehaviorMode.NORMAL)
                        {
                            if (_prvStatus != _curStatus)
                            {
                                ChangeSearchStatus(true);
                                _prvStatus = _curStatus;
                            }
                            ProcessIdleStatus();
                        }

                    }
                    else if (_curStatus == SD.SoldierStatus.MOVE)
                    {
                        if (_prvStatus != _curStatus || _prvMoveCmd != _curMoveCmd)
                        {
                            switch (_curMoveCmd)
                            {
                                case SD.MoveCmd.NORMAL:
                                case SD.MoveCmd.MOVETORIVAL:
                                case SD.MoveCmd.MANMOVEATTACKTOPOS:
                                    ChangeSearchStatus(true);
                                    break;
                                default:
                                    ChangeSearchStatus(false);
                                    break;
                            }
                            _prvStatus = _curStatus;
                            _prvMoveCmd = _curMoveCmd;

                            //防止卡住
                            _lastCheckPos = _transform.position;
                            _stuckTimer = 0f;
                        }
                        ProcessMoveStatus(deltaTime);
                    }
                    else if (_curStatus == SD.SoldierStatus.ATTACKTATGET)
                    {
                        if (_prvStatus != _curStatus)
                        {
                            ChangeSearchStatus(false);
                            _prvStatus = _curStatus;
                        }

                        ProcessAttackStatus();
                    }
                    else if (_curStatus == SD.SoldierStatus.RELEASESKILL)
                    {
                        if (_prvStatus != _curStatus)
                        {
                            ChangeSearchStatus(false);
                            _prvStatus = _curStatus;
                        }

                        ProcessReleaseSkillStatus();
                    }
                    else if (_curStatus == SD.SoldierStatus.DIE)
                    {
                        if (_prvStatus != _curStatus)
                        {
                            ChangeSearchStatus(false);
                            _prvStatus = _curStatus;
                        }
                        if (_animProxy?.gs_hasStateAnim[(int)SD.SoldierAnimType.DIE] == null)
                        {
                            TriggerDieAction();
                        }
                        OnSoldierDie();
                    }
                    else if (_curStatus == SD.SoldierStatus.INTERRUPT)
                    {
                        if (_prvStatus != _curStatus)
                        {
                            ChangeSearchStatus(false);
                            _prvStatus = _curStatus;
                        }
                        if (_buffArray[(int)BFD.SoldierBuffType.STUN].gs_isActive == false &&
                            _buffArray[(int)BFD.SoldierBuffType.STUCK].gs_isActive == false &&
                            _buffArray[(int)BFD.SoldierBuffType.FREEZE].gs_isActive == false &&
                            _buffArray[(int)BFD.SoldierBuffType.SHAPCHANGE].gs_isActive == false)
                        {
                            ProcessInterruptStatus();
                        }
                    }
                }

                if (!_skillTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER].IsNullOrEmpty())
                    SdOnSkillUpdate();

                if (!_talentTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER].IsNullOrEmpty())
                    SdOnTalentUpdate();

                //deactive buff
                int buffCnt = _deactiveBuffList.Count;
                if (buffCnt > 0) //buff
                {
                    for (int i = 0; i < buffCnt; i++)
                    {
                        _deactiveBuffList[i].DeactiveBuff(); //will unregister buff from _buffsInEffect
                    }
                }

                _deactiveBuffList.Clear();

                buffCnt = _buffsInEffect[(int)BFD.BuffTriggerType.TIMETRIGGER].Count;
                if (buffCnt > 0) //buff
                {
                    for (int i = 0; i < buffCnt; i++)
                    {
                        _buffsInEffect[(int)BFD.BuffTriggerType.TIMETRIGGER][i].UpdateBuff();
                    }
                }
            }
            finally
            {
                _lastFramePos = _transform.position; // 在每一帧多线程物理移位结束后，永久记录当前坐标
                _isBusy = false;
            }
        }

        //called by friendly/enemy barracks
        public virtual bool InitSoldier(byte mapId)
        {
            if (_beInited == true)
                return false;
            if (base.InitWarEle(mapId) == false)
                return false;

            _isActive = true;
            _isBorned = false;
            _rivalChanged = false;
            _isBusy = false;
            _isForceRival = false;
            ChangeStatusTo(SD.SoldierStatus.INIT);

            //这里只有两种faction，中立类型需要在之前把faction设置成这两种之一
            if (_faction == WE.FactionType.FRIENDLY)
                SetMoveDir(GD.DirDef.RDir);
            else if (_faction == WE.FactionType.ENEMY)
                SetMoveDir(GD.DirDef.LDir);
            else
            {
                GameLogger.LogError($"{gameObject.name}: Soldier {name} not set faction to be friendly or enemy ,{_faction}");
                ChangeStatusTo(SD.SoldierStatus.ERROR);
                return false;
            }

            if (_sdConf == null)
                ChangeStatusTo(SD.SoldierStatus.ERROR);

            if (_curState == null)
                _curState = new SoldierState(_sdConf);
            else
                _curState.ReInitSoldierState(_sdConf);

            //set search range
            if (_enableSearchCollider == true && _curState.p_searchRange > 0)
            {
                float range = _curState.p_searchRange;
                _rivalSearchShape.p_radius = range;
                _rivalSearchShape.p_radiusSq = range * range;
            }
            else if (_enableAttackCollider == true && _curState.p_attackRange > 0)
            {
                float range = _curState.p_attackSpeed;
                _rivalSearchShape.p_radius = range;
                _rivalSearchShape.p_radiusSq = range * range;
            }

            _curPhyResistance = SD.GetPhysicsResistance(_curState.p_phyArmor);

            _nextAttackInterval = 0;
            _oriAttackInterval = SD.GetAttackInterval(_curState.p_attackSpeed);
            if (_oriAttackInterval < 1f) _oriAttackInterval = 1f; // 防止攻击速度过高导致间隔为0卡死

            //behavior
            _behaviorMode = SD.BehaviorMode.NORMAL;
            _protectTarget = null;

            ResetFaceDir();
            gameObject.name = _sdName + "_" + ((int)_faction).ToString() + "_" + WE.GetSdIndex(_faction).ToString();
            gameObject.SetActive(true);

            _curAnimType = SD.SoldierAnimType.MIN;
            _curMoveCmd = SD.MoveCmd.MIN;

            //path find
            //根据阵营分配默认的全局流场 ID (0 或 1)
            _currentFlowIndex = (_faction == WE.FactionType.FRIENDLY) ? WE.FriendlyFlowFieldDefaultId : WE.EnemyFlowFieldDefaultId;
            _aStarWaypoints.Clear();
            _currentWaypointIndex = 0;
            _lastFramePos = _transform.position;
            _arrivalStuckTimer = 0f; // 重置敏捷计时器
            _animMoveDirEMA = Vector2.zero; // 复位动画方向滤波器, 防止从池子里复用时拿到上次的残留方向

            _entityData.p_position = (Vector2)_transform.position;
            _transform.position = new Vector3(_transform.position.x, _transform.position.y, WarFieldUtil.GetZByY(_transform.position.y, _mapPassableBase.y));
            ChangeSearchStatus(false); //先关闭索敌
            _rivalSearcher.p_mapId = mapId;
            SearchManager.Instance.UnregisterSearch(_rivalSearcher);
            SearchManager.Instance.RegisterSearch(_rivalSearcher);
            return true;
        }

        // 获取当前需要排除的 GridIndex 列表（供底层的 Search Job 使用）
        public void GetExcludeGridIndices(ref FixedList64Bytes<int> excludeList)
        {
            excludeList.Clear();

            // 始终把自己加入剔除列表
            if (_gridIndex >= 0)
                excludeList.Add(_gridIndex);

            // 将黑名单加入剔除列表
            for (int i = 0; i < _blacklistedRivals.Count; i++)
            {
                if (excludeList.Length >= excludeList.Capacity)
                    break; // 防止黑名单过多撑爆 FixedList 容量

                if (_blacklistedRivals[i].p_rival != null)
                {
                    var rivalScript = _blacklistedRivals[i].p_rival.GetComponent<WarEleParent>();
                    if (rivalScript != null && rivalScript.gs_gridIndex >= 0)
                        excludeList.Add(rivalScript.gs_gridIndex);
                }
            }
        }

        //改变士兵行为模式为守护,只支持守护建筑
        //needProtect：需要守护的建筑
        //position：默认站岗位置
        public virtual bool BecomeProtector(INeedBeProtect needProtect, WE.WarEleType type, Vector2 position)
        {
            if (_beInited == false)
                return false;
            if (type != WE.WarEleType.BUILDING) //目前只支持守护建筑
                return false;
            lock (_behaviorLock)
            {
                if (SD.BehaviorMode.PROTECT == _behaviorMode)
                    return false;
                _behaviorMode = SD.BehaviorMode.PROTECT;
            }

            //protector don't need actively search rival
            _curState.p_searchRange = -1;
            float range = _curState.p_attackSpeed;
            _rivalSearchShape.p_radius = range;
            _rivalSearchShape.p_radiusSq = range * range;
            ChangeSearchStatus(false); //禁用查找,等待守护的建筑通知

            _protectTarget = needProtect;
            _guardPos =  new Vector3(position.x, position.y, 0);

            return true;
        }

        public void ReleaseProtector()
        {
            lock (_behaviorLock)
                _behaviorMode = SD.BehaviorMode.NORMAL;
            _protectTarget = null;
            _rivalChooseType = SD.TargetDetectType.MIN; //必须恢复_rivalChooseType，要不不能进行正常的rival查找
            _curState.p_searchRange = _sdConf.p_searchRange;
        }

        //可能并没有到达_guardPos，但是可以停止了
        //所守护的建筑通知的
        public void ReachProtectTarget()
        {
            if (_curStatus == SD.SoldierStatus.MOVE)
                ChangeStatusTo(SD.SoldierStatus.IDLE);
        }

        public virtual void ParterInSkillRange(GameObject colTarget, WE.WarEleType type)
        {
            if (_beInited == false)
                return;

            //默认不去修改队列，因为可能队列不会使用，在具体兵种内修改队列
            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER].IsNullOrEmpty())
            {
                SdOnSkillParterEnter(colTarget, type);
            }

            if (!_talentTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER].IsNullOrEmpty())
            {
                SdOnTalentParterEnter(colTarget, type);
            }
        }

        public virtual void ParterOutSkillRange(GameObject colTarget, WE.WarEleType type)
        {
            if (_beInited == false)
                return;

            //默认不去修改队列，因为可能队列不会使用，在具体兵种内修改队列
            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER].IsNullOrEmpty())
            {
                SdOnSkillParterLeave(colTarget, type);
            }

            if (!_talentTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER].IsNullOrEmpty())
            {
                SdOnTalentParterLeave(colTarget, type);
            }
        }

        //attackerT should be WE.WarEleType.WEAPON
        //return whether this soldier die. true : solider die
        //hitValue the real damage
        //attackScript: the Solider/Building
        //attacker: if ==null, means can not change rival to this target
        //triggerSkill/Buff:  will the be attack trigger skill/buff, but will trigger die skill/buff
        public virtual bool BeAttacked(GameObject attacker, MonoBehaviour attackScript, WE.WarEleType attackerT, float damage, bool triggerSkill,
            bool triggerBuff, out float hitValue)
        {
            hitValue = 0;
            if (_beInited == false)
                return true;

            if (_curStatus == SD.SoldierStatus.DIE || _curStatus == SD.SoldierStatus.MIN)
                return true;

            //magic、physical、pure attack
            {
                if (!_skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].IsNullOrEmpty())
                {
                    damage = SdOnSkillBeAttackPre(damage, attackScript, attackerT, !triggerSkill);
                }

                if (!_talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].IsNullOrEmpty())
                {
                    damage = SdOnTalentBeAttackPre(damage, attackScript, attackerT, !triggerSkill);
                }
            }

            int buffCnt = 0;
            if (triggerBuff == true)
            {
                if(damage <= 0)
                    return false;

                buffCnt = _buffsInEffect[(int)BFD.BuffTriggerType.BEATTACKTRIGGER].Count;
                if (buffCnt > 0) //buff
                {
                    for (int i = 0; i < buffCnt; i++)
                    {
                        damage = _buffsInEffect[(int)BFD.BuffTriggerType.BEATTACKTRIGGER][i].BuffBeAttackPre(damage, attackScript, attackerT);
                    }
                }
            }


            float hit = damage * (1 - _curPhyResistance);
            hitValue = _curState.p_health; //maybe _curState.p_health < hit then actually damage is _curState.p_health
            _curState.p_health -= hit;
            if (0 >= _curState.p_health) //if die
            {
                ChangeStatusTo(SD.SoldierStatus.DIE);
                if (!_skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].IsNullOrEmpty())
                {
                    SdOnSkillBeAttackPost(hitValue, true, attackScript, attackerT, !triggerSkill);
                }

                if (!_talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].IsNullOrEmpty())
                {
                    SdOnTalentBeAttackPost(hitValue, true, attackScript, attackerT, !triggerSkill);
                }

                if (buffCnt > 0) //buff
                {
                    for (int i = 0; i < buffCnt; i++)
                    {
                        _buffsInEffect[(int)BFD.BuffTriggerType.BEATTACKTRIGGER][i].BuffBeAttackPost(hitValue, true, attackScript, attackerT);
                    }
                }

                return true;
            }

            hitValue = hit;
            {
                if (!_skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].IsNullOrEmpty()) //skill
                {
                    SdOnSkillBeAttackPost(hitValue, false, attackScript, attackerT, !triggerSkill);
                }

                if (!_talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER].IsNullOrEmpty()) //talent
                {
                    SdOnTalentBeAttackPost(hitValue, false, attackScript, attackerT, !triggerSkill);
                }
            }

            if (triggerBuff == true)
            {
                if (buffCnt > 0) //buff
                {
                    for (int i = 0; i < buffCnt; i++)
                    {
                        _buffsInEffect[(int)BFD.BuffTriggerType.BEATTACKTRIGGER][i].BuffBeAttackPost(hitValue, true, attackScript, attackerT);
                    }
                }
            }

            if (attacker != null && attackScript is WarEleParent script)
            {
                TryChangeRival(script, SD.TargetDetectType.BEATTACK, false);
            }

            return false;
        }

        //not as ChangeState of hp, this will change the current hp. but ChangeState change the hp max
        //if curer == NULL, means soldier self
        //return the cured value
        public virtual float BeCure(GameObject curer, float cureValue, GD.CalDeltaType calType)
        {
            if (_beInited == false)
                return 0;

            float prvValue = _curState.p_health;
            _curState.p_health = Utils.CalDeltaValue(_curState.p_health, cureValue, calType);
            if (_curState.p_health > _curState.p_maxHealth)
                _curState.p_health = _curState.p_maxHealth;
            return (_curState.p_health - prvValue);
        }

        //some buff or attribute change then change the soldier state,just add a new state change into the the statechange list
        //return the index in the statechange list
        public virtual object AddStateChange(SD.StateSoldierEffectType stateType, float value, GD.CalDeltaType calType, out float oriValue)
        {
            oriValue = 0;

            //not check _beInited, because some skill may change state in awake
            SoldierStateChange change = RentChange(); // 从对象池取，避免每次 new
            change.p_deltaValue = value;
            change.p_calType = calType;
            if (_stateChangeArray[(int)stateType] == null)
                _stateChangeArray[(int)stateType] = new List<SoldierStateChange>();
            lock (_stateLock)
            {
                switch (stateType)
                {
                    case SD.StateSoldierEffectType.HEALTH:
                        oriValue = _curState.p_maxHealth;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_maxHealth = Utils.CalDeltaValue(_curState.p_maxHealth, value, calType); //only change the hp max
                        _curState.p_health += _curState.p_maxHealth - oriValue;
                        if (_curState.p_maxHealth <= 0)
                            _curState.p_maxHealth = 1;
                        if (_curState.p_health <= 0) //最大生命值减小并不会导致士兵的直接死亡, 士兵只有在BeAttack里面才能死亡
                            _curState.p_health = 1;
                        OnStateChanged(stateType, oriValue, _curState.p_maxHealth);
                        break;
                    case SD.StateSoldierEffectType.DAMAGE:
                        oriValue = _curState.p_damage;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_damage = Utils.CalDeltaValue(_curState.p_damage, value, calType);
                        if (_curState.p_damage <= 0)
                            _curState.p_damage = 0.00001f;
                        OnStateChanged(stateType, oriValue, _curState.p_damage);
                        break;
                    case SD.StateSoldierEffectType.ATTACKRANGE:
                        oriValue = _curState.p_attackRange;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_attackRange = Utils.CalDeltaValue(_curState.p_attackRange, value, calType);
                        if (_curState.p_attackRange > 0 && _enableAttackCollider == true)
                        {
                            if (_enableSearchCollider == false || _curState.p_searchRange == 0)
                            {
                                float range = _curState.p_attackSpeed;
                                _rivalSearchShape.p_radius = range;
                                _rivalSearchShape.p_radiusSq = range * range;
                            }
                        }
                        else
                            _curState.p_attackRange = 0;

                        OnStateChanged(stateType, oriValue, _curState.p_attackRange);
                        break;
                    case SD.StateSoldierEffectType.SEARCHRANGE:
                        oriValue = _curState.p_searchRange;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_searchRange = Utils.CalDeltaValue(_curState.p_searchRange, value, calType);
                        if (_enableSearchCollider == true && _curState.p_searchRange > 0)
                        {
                            float range = _curState.p_searchRange;
                            _rivalSearchShape.p_radius = range;
                            _rivalSearchShape.p_radiusSq = range * range;
                        }
                        else
                            _curState.p_searchRange = 0;

                        OnStateChanged(stateType, oriValue, _curState.p_searchRange);
                        break;
                    case SD.StateSoldierEffectType.ATTACKSPEED:
                        oriValue = _curState.p_attackSpeed;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_attackSpeed = Utils.CalDeltaValue(_curState.p_attackSpeed, value, calType);
                        if (_curState.p_attackSpeed < 0)
                            _curState.p_attackSpeed = 0.001f;
                        _oriAttackInterval = SD.GetAttackInterval(_curState.p_attackSpeed);
                        OnStateChanged(stateType, oriValue, _curState.p_attackSpeed);
                        break;
                    case SD.StateSoldierEffectType.MOVESPEED:
                        oriValue = _curState.p_moveSpeed;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_moveSpeed = Utils.CalDeltaValue(_curState.p_moveSpeed, value, calType);
                        if (_curState.p_moveSpeed < 0)
                            _curState.p_moveSpeed = 0f;
                        _moveSpeed = Utils.FastNormalized(_moveSpeed) * (_curState.p_moveSpeed * Time.fixedDeltaTime);
                        OnStateChanged(stateType, oriValue, _curState.p_moveSpeed);
                        break;
                    case SD.StateSoldierEffectType.PHYARMOR:
                        oriValue = _curState.p_phyArmor;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_phyArmor = Utils.CalDeltaValue(_curState.p_phyArmor, value, calType);
                        _curPhyResistance = SD.GetPhysicsResistance(_curState.p_phyArmor);
                        OnStateChanged(stateType, oriValue, _curState.p_phyArmor);
                        break;
                    case SD.StateSoldierEffectType.SPAWNSPEED:
                        oriValue = _curState.p_spawnTime;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_spawnTime = Utils.CalDeltaValue(_curState.p_spawnTime, value, calType);
                        _curState.p_spawnTimeInCycle = Utils.CountOfFixUpdate(_curState.p_spawnTime);
                        if (_curState.p_spawnTimeInCycle <= 1)
                            _curState.p_spawnTimeInCycle = 1f;
                        OnStateChanged(stateType, oriValue, _curState.p_spawnTime);
                        break;
                    case SD.StateSoldierEffectType.BODYHIDE:
                        if (_curState.p_bodyHide == true)
                            oriValue = -1;
                        else
                            oriValue = 1;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        value = Utils.CalDeltaValue(oriValue, value, calType);
                        _curState.p_bodyHide = value < 0;
                        _animProxy.ChangeAlpha(_curState.p_bodyHide ? 0.5f : 1.0f);
                        OnStateChanged(stateType, oriValue, value);
                        break;
                    case SD.StateSoldierEffectType.SKILLCOLLIDER:
                        oriValue = _skillRadius;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        float radio = Utils.CalDeltaValue(_skillRadius, value, calType);
                        if (radio > 0)
                            _skillRadius = radio;
                        else
                            _skillRadius = 0;

                        OnStateChanged(stateType, oriValue, radio);
                        break;
                    case SD.StateSoldierEffectType.HPINC:
                        oriValue = _curState.p_hpInc;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        //value是秒为单位的，需要转成fix update为时间单位
                        _curState.p_hpInc = Utils.CalDeltaValue(_curState.p_hpInc, value * Time.fixedDeltaTime, calType);
                        if (_curState.p_hpInc < 0)
                            _curState.p_hpInc = 0f;
                        OnStateChanged(stateType, oriValue, _curState.p_hpInc);
                        break;
                    case SD.StateSoldierEffectType.SKILLTIMESTEP:
                        oriValue = _curState.p_skillTimeStep;
                        change.p_prvState = oriValue;
                        _stateChangeArray[(int)stateType].Add(change);
                        _curState.p_skillTimeStep = Utils.CalDeltaValue(_curState.p_skillTimeStep, value, calType);
                        if (_curState.p_skillTimeStep < 0)
                            _curState.p_skillTimeStep = 1f;
                        OnStateChanged(stateType, oriValue, _curState.p_skillTimeStep);
                        if (_curSkill != null)
                        {
                            int cnt = _curSkill.Count;
                            for(int i = 0; i < cnt; i++)
                                _curSkill[i].SetTimeStep(_curState.p_skillTimeStep);
                        }

                        if (_curTalent != null)
                        {
                            int cnt = _curSkill.Count;
                            for (int i = 0; i < cnt; i++)
                                _curTalent[i].SetTimeStep(_curState.p_skillTimeStep);
                        }

                        break;
                    default:
                        return null;
                }
            } //lock

            return change;
        }

        //remove/modify a state change in the state change array
        //isDelete: need remove the state change struct
        //modifyValue: the value want to change the state if isDelete == false
        //calType: how to change the the value of the state if isDelete == false
        //isModyfyBaseValue: false->modifyValue will calculate SoldierStateChange.p_deltaValue, true->modifyValue will just set SoldierStateChange.p_deltaValue to be new one
        //isModyfyBaseValue: false的情况会根据之前的p_deltaValue和输入参数计算新的p_deltaValue，此时的calType影响的是p_deltaValue，true就直接将入参直接覆盖p_deltaValue
        //return the new state
        public virtual bool ModifyStateChange(SD.StateSoldierEffectType stateType, object indexStruct, bool isDelete, float modifyValue,
            GD.CalDeltaType calType, bool isModyfyBaseValue)
        {
            if (indexStruct == null)
                return false;

            //not check _beInited, because some skill may change state in awake
            List<SoldierStateChange> list = _stateChangeArray[(int)stateType];
            SoldierStateChange changeSt = (SoldierStateChange)indexStruct;
            int index = list.IndexOf(changeSt);
            float prvValue = list[index].p_prvState;
            if (isDelete == true)
            {
                ReturnChange(changeSt); // 归还对象池，消除 GC（修复瓶颈4）
                list.RemoveAt(index);
            }
            else
            {
                if (calType == GD.CalDeltaType.MIN)
                {
                    GameLogger.LogError($"{gameObject.name}: ModifyStateChange fail, not set calType");
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

            lock (_stateLock)
            {
                float oriValue = 0;
                switch (stateType)
                {
                    case SD.StateSoldierEffectType.HEALTH:
                        oriValue = _curState.p_maxHealth;
                        _curState.p_maxHealth = CalculateState(list, index, prvValue);
                        _curState.p_health += _curState.p_maxHealth - oriValue;
                        if (_curState.p_maxHealth <= 0)
                            _curState.p_maxHealth = 1;
                        if (_curState.p_health <= 0) //最大生命值减小并不会导致士兵的直接死亡, 士兵只有在BeAttack里面才能死亡
                            _curState.p_health = 1;
                        OnStateChanged(stateType, oriValue, _curState.p_maxHealth);
                        break;
                    case SD.StateSoldierEffectType.DAMAGE:
                        oriValue = _curState.p_damage;
                        _curState.p_damage = CalculateState(list, index, prvValue);
                        if (_curState.p_damage <= 0)
                            _curState.p_damage = 0.00001f;
                        OnStateChanged(stateType, oriValue, _curState.p_damage);
                        break;
                    case SD.StateSoldierEffectType.ATTACKRANGE:
                        oriValue = _curState.p_attackRange;
                        _curState.p_attackRange = CalculateState(list, index, prvValue);
                        if (_curState.p_attackRange > 0 && _enableAttackCollider == true)
                        {
                            if (_enableSearchCollider == false || _curState.p_searchRange == 0)
                            {
                                float range = _curState.p_attackSpeed;
                                _rivalSearchShape.p_radius = range;
                                _rivalSearchShape.p_radiusSq = range * range;
                            }
                        }
                        else
                            _curState.p_attackRange = 0;

                        OnStateChanged(stateType, oriValue, _curState.p_attackRange);
                        break;
                    case SD.StateSoldierEffectType.SEARCHRANGE:
                        oriValue = _curState.p_searchRange;
                        _curState.p_searchRange = CalculateState(list, index, prvValue);
                        if (_enableSearchCollider == true && _curState.p_searchRange > 0)
                        {
                            float range = _curState.p_searchRange;
                            _rivalSearchShape.p_radius = range;
                            _rivalSearchShape.p_radiusSq = range * range;
                        }
                        else
                            _curState.p_searchRange = 0;

                        OnStateChanged(stateType, oriValue, _curState.p_searchRange);
                        break;
                    case SD.StateSoldierEffectType.ATTACKSPEED:
                        oriValue = _curState.p_attackSpeed;
                        _curState.p_attackSpeed = CalculateState(list, index, prvValue);
                        if (_curState.p_attackSpeed < 0)
                            _curState.p_attackSpeed = 0.00001f;
                        _oriAttackInterval = SD.GetAttackInterval(_curState.p_attackSpeed);
                        OnStateChanged(stateType, oriValue, _curState.p_attackSpeed);
                        break;
                    case SD.StateSoldierEffectType.MOVESPEED:
                        oriValue = _curState.p_moveSpeed;
                        _curState.p_moveSpeed = CalculateState(list, index, prvValue);
                        if (_curState.p_moveSpeed < 0)
                            _curState.p_moveSpeed = 0f;
                        _moveSpeed = Utils.FastNormalized(_moveSpeed) * (_curState.p_moveSpeed * Time.fixedDeltaTime);
                        OnStateChanged(stateType, oriValue, _curState.p_moveSpeed);
                        break;
                    case SD.StateSoldierEffectType.PHYARMOR:
                        oriValue = _curState.p_phyArmor;
                        _curState.p_phyArmor = CalculateState(list, index, prvValue);
                        _curPhyResistance = SD.GetPhysicsResistance(_curState.p_phyArmor);
                        OnStateChanged(stateType, oriValue, _curState.p_phyArmor);
                        break;
                    case SD.StateSoldierEffectType.SPAWNSPEED: //not change p_spawnTime,because we don't p_spawnTime
                        oriValue = _curState.p_spawnTimeInCycle;
                        _curState.p_spawnTimeInCycle = CalculateState(list, index, prvValue);
                        if (_curState.p_spawnTimeInCycle < 0)
                            _curState.p_spawnTimeInCycle = 0.00001f;
                        OnStateChanged(stateType, oriValue, _curState.p_spawnTimeInCycle);
                        break;
                    case SD.StateSoldierEffectType.BODYHIDE:
                        if (_curState.p_bodyHide == true)
                            oriValue = -1;
                        else
                            oriValue = 1;
                        float value = CalculateState(list, index, prvValue);
                        _curState.p_bodyHide = value < 0;
                        _animProxy.ChangeAlpha(_curState.p_bodyHide ? 0.5f : 1.0f);
                        OnStateChanged(stateType, oriValue, value);
                        break;
                    case SD.StateSoldierEffectType.SKILLCOLLIDER:
                        oriValue = _skillRadius;
                        float radio = CalculateState(list, index, prvValue);
                        if (radio > 0)
                            _skillRadius = radio;
                        else
                            _skillRadius = 0;

                        OnStateChanged(stateType, oriValue, radio);
                        break;
                    case SD.StateSoldierEffectType.HPINC:
                        oriValue = _curState.p_hpInc;
                        _curState.p_hpInc = CalculateState(list, index, prvValue);
                        if (_curState.p_hpInc < 0)
                            _curState.p_hpInc = 0f;
                        OnStateChanged(stateType, oriValue, _curState.p_hpInc);
                        break;
                    case SD.StateSoldierEffectType.SKILLTIMESTEP:
                        oriValue = _curState.p_skillTimeStep;
                        _curState.p_skillTimeStep = CalculateState(list, index, prvValue);;
                        if (_curState.p_skillTimeStep < 0)
                            _curState.p_skillTimeStep = 1f;
                        OnStateChanged(stateType, oriValue, _curState.p_skillTimeStep);
                        if (_curSkill != null)
                        {
                            int cnt = _curSkill.Count;
                            for(int i = 0; i < cnt; i++)
                                _curSkill[i].SetTimeStep(_curState.p_skillTimeStep);
                        }
                        if (_curTalent != null)
                        {
                            int cnt = _curSkill.Count;
                            for (int i = 0; i < cnt; i++)
                                _curTalent[i].SetTimeStep(_curState.p_skillTimeStep);
                        }
                        break;
                    default:
                        GameLogger.LogError($"{gameObject.name}: ModifyStateChange fail for error state {stateType}");
                        return false;
                }
            } //lock

            return true;
        }

        //针对特有的state改变,由特定的soldier去实现
        //参数与AddStateChange基本一致
        public virtual object AddSpecificStateChange(string stateName, float value, GD.CalDeltaType calType, out float oriValue)
        {
            oriValue = 0;
            return null;
        }

        //针对特有的state改变,由特定的soldier去实现
        //参数与ModifyStateChange基本一致
        public virtual bool ModifySpecificStateChange(string stateName, object indexStruct, bool isDelete, float modifyValue, GD.CalDeltaType calType,
            bool isModyfyBaseValue)
        {
            return false;
        }

        //somehow target die/removed
        public virtual void TargetRemove(WE.WarEleType targetType, GameObject target)
        {
            if (_beInited == false)
                return;

            if (targetType != _rivalType || target != _rival)
                return;

            if(_inDebug)
                GameLogger.LogDebug($"{gameObject.name}: TargetRemove removed {_rival.name}");
            _rival = null;
            _rivalScript = null;
            _rivalType = WE.WarEleType.MIN;
            _rivalChooseType = SD.TargetDetectType.MIN;
            _rivalChanged = true;
        }

        //only skill animation can be triigered outside, other animation is triggered by soldier self
        public virtual void PlaySkillAnimation(Skill skill)
        {
            if (_beInited == false)
                return;

            SetNextSkillAnim(SD.SoldierAnimType.SKILL, skill);
        }

        public void RegisterSkill(Skill skill, SKD.SkillTriggerType type)
        {
            if(_isHero == true)
                _skillTriggerArr[(int)type].Add(skill);
            else //normal soldier 不能同时有多个技能
            {
                if(_skillTriggerArr[(int)type].Count > 0)
                    GameLogger.LogWarning($"{_sdName} seam want to overrode old skill {skill.name} -> {_skillTriggerArr[(int)type][0].name} ");
                _skillTriggerArr[(int)type].Add(skill);
            }
        }

        public void UnregisterSkill(Skill skill, SKD.SkillTriggerType type)
        {
            if(_isHero == true)
                _skillTriggerArr[(int)type].Remove(skill);
            else if(_skillTriggerArr[(int)type].Count > 0)
            {
                if (_skillTriggerArr[(int)type][0] != skill)
                {
                    GameLogger.LogWarning(
                        $"{_sdName} seam want to unregister wrong skill {skill.name} , current skill is {_skillTriggerArr[(int)type][0].name} ");
                    return;
                }
                _skillTriggerArr[(int)type].Clear();
            }
        }

        public void RegisterTalent(Talent talent, SKD.SkillTriggerType type)
        {
            if(_isHero == true)
                _talentTriggerArr[(int)type].Add(talent);
            else //normal soldier 不能同时有多个天赋
            {
                if(_skillTriggerArr[(int)type].Count > 0)
                    GameLogger.LogWarning($"{_sdName} seam want to overrode old skill {talent.name} -> {_skillTriggerArr[(int)type][0].name} ");
                _talentTriggerArr[(int)type].Add(talent);
            }
        }

        public void UnregisterTalent(Talent talent, SKD.SkillTriggerType type)
        {
            if(_isHero == true)
                _talentTriggerArr[(int)type].Remove(talent);
            else if(_talentTriggerArr[(int)type].Count > 0)
            {
                if (_talentTriggerArr[(int)type][0] != talent)
                {
                    GameLogger.LogWarning(
                        $"{_sdName} seam want to unregister wrong skill {talent.name} , current skill is {_skillTriggerArr[(int)type][0].name} ");
                    return;
                }
                _talentTriggerArr[(int)type].Clear();
            }
        }

        //是否可以触发主动技能
        public virtual bool CanTriggerActiveSkill()
        {
            if (_beInited == false)
                return false;

            if (Utils.IsEnumInRange(_curStatus, SD.SoldierStatus.MIN, SD.SoldierStatus.RELEASESKILL) == false)
                return false;
            // MIN < _curStatus < RELEASESKILL
            return true;
        }

        public void RegisterBuff(SoldierBuff buff, BFD.BuffTriggerType type, BFD.SoldierBuffType buffType)
        {
            if (Utils.IsEnumInRange(type, BFD.BuffTriggerType.MIN, BFD.BuffTriggerType.MAX) == false)
                return;

            if (_buffsInEffect[(int)type].Contains(buff) == true) //for only kind buff, one soldier can only take one
                return;

            int index = -1;
            switch (buffType)
            {
                case BFD.SoldierBuffType.REFLECTION: //reflection take effect before shield
                    index = _buffsInEffect[(int)type].IndexOf(_buffArray[(int)BFD.SoldierBuffType.SHIELD]); //获取shield的index，确保伤害反弹插在前面
                    break;
                case BFD.SoldierBuffType.SHIELD: //shield must after reflection
                    index = _buffsInEffect[(int)type].IndexOf(_buffArray[(int)BFD.SoldierBuffType.REFLECTION]); //获取伤害反弹的index，确保shield插在前面
                    if (index != -1)
                        index++;
                    break;
                default:
                    break;
            }

            if (index == -1)
                _buffsInEffect[(int)type].Add(buff);
            else
                _buffsInEffect[(int)type].Insert(index, buff);
        }

        public void UnregisterBuff(SoldierBuff buff, BFD.BuffTriggerType type)
        {
            if (Utils.IsEnumInRange(type, BFD.BuffTriggerType.MIN, BFD.BuffTriggerType.MAX) == false)
                return;
            _buffsInEffect[(int)type].Remove(buff);
        }

        //start to be affected by buff
        //value: the object want to send to buff
        //public virtual bool BeAffectedByBuff<T>(BFD.SoldierBuffType type, T value, Action<BFD.BuffCallBackEventType, T> callback = null)
        public virtual bool BeAffectedByBuff<TValue>(BFD.SoldierBuffType type, in TValue value, BuffUnsafeCallback callback = null)
        {
            if (Utils.IsEnumInRange(_curStatus, SD.SoldierStatus.MIN, SD.SoldierStatus.DIE) == false)
                return false;

            bool ret = false;
            if (_buffArray[(int)type] != null)
                ret = _buffArray[(int)type].ActiveBuff(in value, callback);

            if (ret == false)
                return false;

            return true;
        }

        //重载
        public virtual bool BeAffectedByBuff<TValue, TRet>(BFD.SoldierBuffType type, in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            if (Utils.IsEnumInRange(_curStatus, SD.SoldierStatus.MIN, SD.SoldierStatus.DIE) == false)
                return false;

            bool ret = false;
            if (_buffArray[(int)type] != null)
                ret = _buffArray[(int)type].ActiveBuff(in value, ref buffRet, callback);

            if (ret == false)
                return false;

            return true;
        }

        //stop the buff effect
        public virtual void StopAffectedByBuff(BFD.SoldierBuffType type)
        {
            if (_buffArray[(int)type] != null)
                _deactiveBuffList.Add(_buffArray[(int)type]);//not deactive buff here, because this function maybe called during UpdateBuff, loop list at the same time remove item from list, may cause crash
        }

        //only stop part of the buff, can not do unregister buff,in it
        public virtual void StopPartOfBuff<TValue>(BFD.SoldierBuffType type, in TValue value)
        {
            if (_buffArray[(int)type] != null)
                _buffArray[(int)type].StopPartOfBuff(in value);
        }

        public bool CanAddBuff(BFD.SoldierBuffType type, object value = null)
        {
            if (Utils.IsEnumInRange(_curStatus, SD.SoldierStatus.MIN, SD.SoldierStatus.DIE) == false)
                return false;

            if (_buffArray[(int)type] != null)
            {
                return _buffArray[(int)type].CanAddBuff(value);
            }

            return false;
        }

        public bool HasBuff(BFD.SoldierBuffType type, object value)
        {
            if (Utils.IsEnumInRange(_curStatus, SD.SoldierStatus.MIN, SD.SoldierStatus.DIE) == false)
                return false;

            if (_buffArray[(int)type] != null)
            {
                return _buffArray[(int)type].HasBuff(value);
            }

            return false;
        }

        //for ranged/magic/support.
        //called when bullet hit enemy
        //isDie: wheter rival die
        //rival: the soldier/building be hurt
        public virtual void BullectHit(float hitValue, bool isDie, WE.WarEleType rivalType, object rivalScript)
        {
            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //skill
                SdOnSkillDoAttackPost(hitValue, isDie, rivalScript, rivalType);
            if (!_talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //talent
                SdOnTalentDoAttackPost(hitValue, isDie, rivalScript, rivalType);

            int buffCnt = _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER].Count;
            if (buffCnt > 0) //buff
            {
                for (int i = 0; i < buffCnt; i++)
                {
                    _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER][i].BuffDoAttackPost(hitValue, isDie, rivalScript, rivalType);
                }
            }
        }

        //dieCOunt: 范围性攻击死亡个数
        //rivalList: 受到攻击的rivals
        public virtual void ShellHit(float hitValue, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //skill
                SdOnSkillDoAttackPost(hitValue, dieCount, rivalSdList, rivalBdList, target, type);
            if (!_talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //talent
                SdOnTalentDoAttackPost(hitValue, dieCount, rivalSdList, rivalBdList, target, type);

            int buffCnt = _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER].Count;
            if (buffCnt > 0) //buff
            {
                for (int i = 0; i < buffCnt; i++)
                {
                    _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER][i].BuffDoAttackPost(hitValue, dieCount, rivalSdList, rivalBdList);
                }
            }
        }

        //for remote attack only
        public virtual void RemoteRangedAttack(float damage, MonoBehaviour rivalScript, WE.WarEleType rivalType)
        {
        }

        //for 近战攻击
        public virtual void CloseRangeAttack(float damage, MonoBehaviour rivalScript, WE.WarEleType rivalType)
        {
            float hitValue = 0;
            bool isDie = false;
            if (damage > 0)
            {
                if (rivalType == WE.WarEleType.SOLDIER)
                    isDie = ((Soldier)rivalScript).BeAttacked(gameObject, this, WE.WarEleType.SOLDIER, damage, true, true, out hitValue);
                else if (rivalType == WE.WarEleType.BUILDING)
                    isDie = ((WarBuilding)rivalScript).BeAttacked(gameObject, this, WE.WarEleType.SOLDIER, damage, out hitValue);
                if (isDie == true)
                    TargetRemove(_rivalType, _rival);
            }

            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //skill
                SdOnSkillDoAttackPost(hitValue, isDie, rivalScript, rivalType);
            if (!_talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //talent
                SdOnTalentDoAttackPost(hitValue, isDie, rivalScript, rivalType);

            int buffCnt = _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER].Count;
            if (buffCnt > 0) //buff
            {
                for (int i = 0; i < buffCnt; i++)
                {
                    _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER][i].BuffDoAttackPost(hitValue, isDie, rivalScript, rivalType);
                }
            }
        }

        //for some skills need soldier self help to do something
        public virtual void SkillDoSomething(object value)
        {
        }

        //显示/隐藏 士兵的动画
        public void ChangeAnimRender(bool value)
        {
            _animProxy.ChangeAnimVisible(value);
        }

        //这是框选了一群士兵之后调用的
        public virtual bool UserManipulate(int flowIndex, Vector2 targetPos, bool isAttackMove)
        {
            // 如果之前正在走另一个局部流场，先注销掉旧的
            ClearCurrentLocalFlowField();

            _currentFlowIndex = flowIndex;
            _cmdTargetPos = targetPos;
            _curMoveCmd = isAttackMove ? SD.MoveCmd.MANMOVEATTACKTOPOS : SD.MoveCmd.MANMOVETOPOS;

            _arrivalStuckTimer = 0f; // 每次玩家下发新指令，清空集结卡死计时
            if (_curStatus == SD.SoldierStatus.IDLE || _curStatus == SD.SoldierStatus.ATTACKTATGET)
                ChangeStatusTo(SD.SoldierStatus.MOVE);
            return true;
        }

        //soldier leave cave
        //only friendly soldier can enter cave
        //yPercent: 在override的函数中是士兵离开的位置与地图高度的比值，用于计算士兵出cave后的y
        //          在base中就是实际的y
        public virtual void StepOutofCave(CaveTran cave, byte fromMapId, byte toMapId, float yPercent)
        {
            StepIntoMap(fromMapId, toMapId, yPercent);
        }

        //soldier有可能在某些情况下不能移动，并且停止动画 (e.g. 传送开始和结束)
        public virtual void SetSDActive(bool isActive)
        {
            _isActive = isActive;
        }

        //触发主动技能
        public void TriggerActiveSkill(string skillName)
        {
            SdOnSkillActivatedTrigger(skillName);
        }

        public override void ChangeMapId(byte mapId)
        {
            base.ChangeMapId(mapId);
            _transform.position = new Vector3(_transform.position.x, _transform.position.y, WarFieldUtil.GetZByY(_transform.position.y, _mapPassableBase.y));
        }

        //被嘲讽
        public void BeTaunt(WarEleParent rival, WE.WarEleType rivalType)
        {
            TryChangeRival(rival, SD.TargetDetectType.MIN, true);
        }

        //每个fixupdate都会去同步一次soldier相关的entity data
        //因为流场地图中只记录静态物体,所以士兵是不用更新流场地图的
        public void SyncSpatialEntity()
        {
            if (_gridIndex < 0)
                return;
            _entityData.p_position = (Vector2)_transform.position;
            _entityData.p_mapId = _mapId; //有可能地图发生了变化
            _entityData.p_spec = GetSpatialEntitySpecData(); //个性化数据
            UpdateEntityData();
        }

        // 获取回归y坐标的作用力大小系数
        public virtual float GetHomeYMul()
        {
            return 1f;
        }

        //IAnimInfo
        public uint IAnimInfo_GetEleAnimId()
        {
            //[other 25-31][sdtype 8-24][troop 4-7][race 2-3][AnimEntityType 0-1]
            return (uint)AnimDefines.AnimEntityType.SOLDIER | (uint)_race << 2 | (uint)gs_troopType << 4 | (uint)gs_sdType << 8;
        }

        public Dictionary<string, uint> IAnimInfo_GetStateId()
        {
            Dictionary<string, uint> ret = new Dictionary<string, uint>();
            ret.Add("Idle", (uint)SD.SoldierAnimType.IDLE);
            ret.Add("Move", (uint)SD.SoldierAnimType.MOVE);
            ret.Add("Attack", (uint)SD.SoldierAnimType.ATTACK);
            ret.Add("Skill", (uint)SD.SoldierAnimType.SKILL);
            ret.Add("Stun", (uint)SD.SoldierAnimType.STUN);
            ret.Add("Die", (uint)SD.SoldierAnimType.DIE);
            ret.Add("Born", (uint)SD.SoldierAnimType.BORN);
            return ret;
        }

        // animation event callback
        public void IAnimInfo_OnAnimEvent(int stateId)
        {
            switch (stateId)
            {
                case (int)SD.SoldierAnimType.ATTACK:
                    AttackPost();
                    // 对循环攻击动画的防守：若 finish 事件不触发（p_isLoop = true），攻击事件
                    // 本身也负责重置攻击间隔，确保攻击能按攻击速度持续循环。
                    // 对非循环动画：此处仅在 -1 事件到来之前提前重置，-1 事件到来时
                    // _curAnimType 已为 MIN，不会再次叠加间隔，行为正确。
                    if (_nextAttackInterval <= 0 && _oriAttackInterval > 0)
                    {
                        _nextAttackInterval = _oriAttackInterval;
                        _curAnimType = SD.SoldierAnimType.MIN; // 允许 CheckAnimation 检测到 animOver，触发 ForceReplayAnimState
                    }
                    break;
                case (int)SD.SoldierAnimType.DIE:
                    TriggerDieAction();
                    break;
                case (int)SD.SoldierAnimType.SKILL:
                    if(_skillInAnim != null)
                        _skillInAnim.SkillAnimTakeEffect("");
                    break;
                case -1: //anim finish notification
                    if (_curAnimType == SD.SoldierAnimType.DIE)
                    {
                        TriggerDieAction();
                        OnSoldierDie();
                    }
                    else if (_curAnimType == SD.SoldierAnimType.ATTACK)
                    {
                        _nextAttackInterval += _oriAttackInterval;
                        _curAnimType = SD.SoldierAnimType.MIN; // 重置等待状态机分发
                    }
                    else if (_curAnimType == SD.SoldierAnimType.SKILL)
                    {
                        _skillInAnim = null;
                        _curAnimType = SD.SoldierAnimType.MIN;
                    }
                    else if (_curAnimType == SD.SoldierAnimType.BORN)
                    {
                        _isBorned = true;
                        _curAnimType = SD.SoldierAnimType.MIN;
                    }
                    break;
                default:
                    break;
            }
        }
#endregion

#region private functions

        protected virtual void ProcessIdleStatus()
        {
            //玩家控制移动
            if(_curMoveCmd == SD.MoveCmd.MANMOVETOPOS || _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
                ChangeStatusTo(SD.SoldierStatus.MOVE);
            else if (IsRivalValid() == true)
            {
                if (_rivalChooseType == SD.TargetDetectType.INATTACKRANGE)
                    ChangeStatusTo(SD.SoldierStatus.ATTACKTATGET);
                else
                {
                    _curMoveCmd = SD.MoveCmd.MOVETORIVAL; //hero 不会进入MOVETORIVAL
                    ChangeStatusTo(SD.SoldierStatus.MOVE);
                }
            }
            else
            {
                _curMoveCmd = SD.MoveCmd.NORMAL;
                ChangeStatusTo(SD.SoldierStatus.MOVE);
            }
        }

        protected virtual void ProcessMoveStatus(float deltaTime)
        {
            //计算防卡死机制
            if (_curMoveCmd == SD.MoveCmd.MOVETORIVAL ||
                _curMoveCmd == SD.MoveCmd.MANMOVETOPOS ||
                _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
            {
                _stuckTimer += deltaTime;
                if (_stuckTimer >= _stuckCheckInterval)
                {
                    float distSq = math.distancesq(_lastCheckPos, (Vector2)_transform.position);
                    float threshold = _curState.p_moveSpeed * _stuckCheckInterval * 0.25f;

                    if (distSq < threshold * threshold)
                    {
                        if (_inDebug)
                            GameLogger.LogWarning($"{gameObject.name} stuck! Cmd: {_curMoveCmd}");

                        // 卡死后的恢复逻辑区分
                        if (_curMoveCmd == SD.MoveCmd.MOVETORIVAL)
                        {
                            _blacklistedRivals.Add(new BlacklistEntry { p_rival = _rival, p_timer = _blakeListDuration });
                            TargetRemove(_rivalType, _rival);
                            ResetFaceDir();
                            _curMoveCmd = SD.MoveCmd.NORMAL; // 追赶失败，回去推线
                        }
                        else if (_curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
                        {
                            if (_rival != null)
                            {
                                _blacklistedRivals.Add(new BlacklistEntry { p_rival = _rival, p_timer = _blakeListDuration });
                                TargetRemove(_rivalType, _rival);
                            }
                            _aStarWaypoints.Clear();  //可能是A*寻路卡住了,重新寻路
                            _pathRecalculateCooldown = 0f;

                            float distToTargetSq = math.distancesq((Vector2)_transform.position, _cmdTargetPos);
                            // 只要距离终点在 2.5 米以内（平方为 6.25f），由于地形或合围无法更近，直接判定送达！
                            if (distToTargetSq < 6.25f)
                            {
                                GameLogger.LogWarning($"[防卡死救援] 单位:{gameObject.name} 在集群流场控制下({_curMoveCmd})于终点死角卡死(剩余距离平方:{distToTargetSq:F4})，强行判定送达，卸载指令。");
                                OnMoveCmdFinished();
                                return; // 成功脱离状态机，直接返回
                            }
                        }
                        else
                        {
                            _aStarWaypoints.Clear();  //可能是A*寻路卡住了,重新寻路
                            _pathRecalculateCooldown = 0f;

                            float distToTargetSq = math.distancesq((Vector2)_transform.position, _cmdTargetPos);
                            // 只要距离终点在 2.5 米以内（平方为 6.25f），由于地形或合围无法更近，直接判定送达！
                            if (distToTargetSq < 6.25f)
                            {
                                GameLogger.LogWarning($"[防卡死救援] 单位:{gameObject.name} 在集群流场控制下({_curMoveCmd})于终点死角卡死(剩余距离平方:{distToTargetSq:F4})，强行判定送达，卸载指令。");
                                OnMoveCmdFinished();
                                return; // 成功脱离状态机，直接返回
                            }
                        }
                    }

                    _lastCheckPos = (Vector2)_transform.position;
                    _stuckTimer = 0f;
                }
            }

            if (_rival != null && _curMoveCmd != SD.MoveCmd.MANMOVETOPOS)
            {
                if (_curMoveCmd == SD.MoveCmd.NORMAL)
                    _curMoveCmd = SD.MoveCmd.MOVETORIVAL; // 推线路上发现敌人，转入追击

                if (_rivalChooseType == SD.TargetDetectType.INATTACKRANGE)
                {
                    ChangeStatusTo(SD.SoldierStatus.ATTACKTATGET);
                    _rivalChanged = false;
                }
                else
                {
                    if (IsRivalValid() == false)
                    {
                        ResetFaceDir();
                        if (_curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
                            _rival = null;
                        else
                            OnMoveCmdFinished(); // 强杀或追赶的目标死了，任务完成
                    }
                    else
                    {
                        FaceToRival();
                        ExecuteGenericMove(deltaTime); // 继续追击移动
                    }
                }
            }
            else
            {
                if (_curMoveCmd == SD.MoveCmd.MANMOVETOPOS || _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
                {
                    if (CheckCmdFinished())
                        OnMoveCmdFinished();
                    else
                    {
                        FaceToMoveCmdDir();
                        ExecuteGenericMove(deltaTime);
                    }
                }
                else if (_curMoveCmd == SD.MoveCmd.NORMAL)
                {
                    FaceToMoveCmdDir();
                    ExecuteGenericMove(deltaTime); // 流场推线
                }
            }
        }

        protected virtual void ProcessAttackStatus()
        {
            if (_rivalChanged == true)
            {
                if (IsRivalValid() == false)
                {
                    ResetFaceDir();
                    ChangeStatusTo(SD.SoldierStatus.MOVE);
                }
                else
                {
                    FaceToRival();
                    if (_rivalChooseType == SD.TargetDetectType.INATTACKRANGE)
                    {
                        //continue attack the new rival
                    }
                    else //不在攻击范围内，就向着目标移动
                    {
                        ChangeStatusTo(SD.SoldierStatus.MOVE);
                        _curMoveCmd = SD.MoveCmd.MOVETORIVAL;
                    }
                }

                _rivalChanged = false;
            }
            else
            {
                FaceToRival(); //因为可能被挤开,所以每一帧都需要调整动画的方向
                if (_curState.p_damage > 0 && _nextAttackInterval > 0) //some soldier can not attack (p_damage==0)
                {
                    _nextAttackInterval--;
                    if (_nextAttackInterval <= 0)
                        AttackPre();
                }
            }
        }

        protected virtual void ProcessReleaseSkillStatus()
        {
            if (_skillInAnim == null)
            {
                if (IsRivalValid() == false)
                {
                    ResetFaceDir();
                    if (_curMoveCmd == SD.MoveCmd.MANMOVETOPOS || _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
                        ChangeStatusTo(SD.SoldierStatus.MOVE);
                    else
                        OnMoveCmdFinished();
                }
                else
                {
                    FaceToRival();
                    if (_rivalChooseType == SD.TargetDetectType.INATTACKRANGE)
                        ChangeStatusTo(SD.SoldierStatus.ATTACKTATGET);
                    else
                        ChangeStatusTo(SD.SoldierStatus.MOVE);
                }
                _rivalChanged = false;
            }
        }

        protected virtual void ProcessInterruptStatus()
        {
            if (IsRivalValid() == false)
            {
                ResetFaceDir();
                if (_curMoveCmd == SD.MoveCmd.MANMOVETOPOS || _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
                    ChangeStatusTo(SD.SoldierStatus.MOVE);
                else
                    OnMoveCmdFinished();
            }
            else
            {
                FaceToRival();
                if (_rivalChooseType == SD.TargetDetectType.INATTACKRANGE)
                    ChangeStatusTo(SD.SoldierStatus.ATTACKTATGET);
                else
                    ChangeStatusTo(SD.SoldierStatus.MOVE);
            }
        }

        //统一的移动执行出口，根据 Cmd 决定底层的寻路策略
        protected virtual void ExecuteGenericMove(float deltaTime)
        {
            Vector2 desiredDir = Vector2.zero;
            PathFinderMap pathMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(_mapId);

            switch (_curMoveCmd)
            {
                case SD.MoveCmd.NORMAL:
                    //在jobs中计算
                    break;

                case SD.MoveCmd.MOVETORIVAL:
                    // 追击敌人：退化为微观物理直线追击，绕路依靠 SpatialGrid 排斥
                    if (_rival != null)
                        desiredDir = ((Vector2)_rival.transform.position - (Vector2)_transform.position).normalized;
                    break;
                case SD.MoveCmd.MANMOVETOPOS:
                case SD.MoveCmd.MANMOVEATTACKTOPOS:
                    if (_currentFlowIndex >= WE.LocalFlowFieldStartId)
                    {
                        //在jobs中计算
                    }
                    else
                    {
                        // 单体移动：走 A* 寻路
                        if (pathMap != null)
                        {
                            _pathRecalculateCooldown -= deltaTime;
                            Vector2 currentPos = _transform.position;

                            bool isTargetChanged = _lastAStarTargetPos == GD.InvalidVector2 || math.distancesq(_cmdTargetPos, _lastAStarTargetPos) > 0.1f;

                            // 如果是新目标点，立即拉线计算
                            if (isTargetChanged)
                            {
                                _aStarWaypoints = pathMap.FindPathAStar(currentPos, _cmdTargetPos); //在主线程中计算A*寻路

                                // 将终点同步成BSF的计算结果,这样如果点击到不可达的位置也不会出现卡住一直挤的情况了
                                // 这样当路径走完时，CheckCmdFinished() 就能精准对齐该可达点，从而命令英雄停步！
                                if (_aStarWaypoints.Count > 0)
                                {
                                    _cmdTargetPos = _aStarWaypoints[_aStarWaypoints.Count - 1];
                                }

                                _lastAStarTargetPos = _cmdTargetPos;
                                _currentWaypointIndex = 0;
                                _pathRecalculateCooldown = 0.3f; // 为该目标点重置 300ms 防抖冷却

                                if (_aStarWaypoints.Count == 0)
                                {
                                    GameLogger.LogError($"[A* 寻路失败] 目标点不可达！位置: {currentPos}, 鼠标点击目标点: {_cmdTargetPos}。请检查点击处是否为障碍物内部。");
                                }
                            }
                            // 如果目标点没变，但路径点确实踩完了，在冷却完毕后尝试重新拉线
                            else if (_aStarWaypoints.Count > 0 && _currentWaypointIndex >= _aStarWaypoints.Count)
                            {
                                if (_pathRecalculateCooldown <= 0f)
                                {
                                    _aStarWaypoints = pathMap.FindPathAStar(currentPos, _cmdTargetPos);
                                    _currentWaypointIndex = 0;
                                    _pathRecalculateCooldown = 0.3f;
                                }
                            }

                            // 沿着 Waypoints走
                            if (_aStarWaypoints.Count > 0 && _currentWaypointIndex < _aStarWaypoints.Count)
                            {
                                Vector2 nextTargetPos = _aStarWaypoints[_currentWaypointIndex];

                                // 到了当前拐点，换下一个拐点
                                if (math.distancesq(currentPos, nextTargetPos) < 0.1f)
                                {
                                    _currentWaypointIndex++;
                                    if (_currentWaypointIndex < _aStarWaypoints.Count)
                                        nextTargetPos = _aStarWaypoints[_currentWaypointIndex];
                                }

                                if (_currentWaypointIndex < _aStarWaypoints.Count)
                                {
                                    Vector2 offset = nextTargetPos - currentPos;
                                    if (offset.sqrMagnitude > 0.001f)
                                        desiredDir = offset.normalized;
                                }
                                else
                                {
                                    // 路径点恰好被“提前吃掉”时，继续朝命令终点直走，避免进入 MOVE 但实际静止。
                                    Vector2 offset = _cmdTargetPos - currentPos;
                                    if (offset.sqrMagnitude > 0.001f)
                                        desiredDir = offset.normalized;
                                }
                            }
                            else
                            {
                                // A* 失败或到达终点最后一步，直线返回默认寻路
                                Vector2 offset = _cmdTargetPos - currentPos;
                                if (offset.sqrMagnitude > 0.001f)
                                    desiredDir = offset.normalized;
                            }
                        }
                    }
                    break;
            }

            _desiredMoveDir = desiredDir;
        }

        // 判断坐标移动是否抵达终点
        protected virtual bool CheckCmdFinished()
        {
            if (_curMoveCmd == SD.MoveCmd.MANMOVETOPOS || _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
            {
                Vector2 currentPos = _transform.position;
                float distanceSq = math.distancesq(currentPos, _cmdTargetPos);

                // 硬性抵达圈
                // 集群流场下 N 个单位共享一个目标点，0.95m 半径只装得下 1 个单位，其他单位永远进不来。
                // 扩到 1.5m（恰好一个 cell 的尺寸），单中心周围可容纳 6~7 个单位，普通框选无须等待兜底。
                float rigidThresholdSq = (_currentFlowIndex >= WE.LocalFlowFieldStartId) ? 2.25f : 0.04f;
                if (distanceSq < rigidThresholdSq)
                {
                    _arrivalStuckTimer = 0f;
                    return true;
                }

                // 已经走到了终点集结圈的大体范围内（距离终点 2.5 米内，平方小于 6.25f）
                if (distanceSq < 6.25f)
                {
                    // 计算这一帧相对于上一帧的真实物理位移平方
                    float frameDisSq = math.distancesq(currentPos, _lastFramePos);

                    // 把"卡死"门槛改为相对值：单帧实际位移小于满速期望位移的 35% 即视为被推挤顶死。
                    // 这样不论士兵设定移速多少，都能精准捕捉集群挤压下的真停滞状态，而不会漏判 3~5 cm/帧的轻微滑动。
                    float expectedFrameStep = _curState.p_moveSpeed * Time.fixedDeltaTime;
                    float stuckFrameThresholdSq = expectedFrameStep * expectedFrameStep * 0.1225f; // (0.35)^2
                    if (frameDisSq < stuckFrameThresholdSq)
                    {
                        _arrivalStuckTimer += Time.fixedDeltaTime;

                        // 连续 0.15 秒位移不达期望 → 立刻判定送达
                        if (_arrivalStuckTimer >= 0.15f)
                            return true;
                    }
                    else
                    {
                        _arrivalStuckTimer = 0f; // 动能正常，还在前进，重置计时器
                    }
                }
                else
                {
                    _arrivalStuckTimer = 0f;
                }
            }
            return false;
        }

        protected virtual void OnMoveCmdFinished()
        {
            ClearCurrentLocalFlowField(); // 退出局部流场
            // 当手动控制顺利抵达终点后，将士兵当前落脚点的 Y 坐标重置为全新的常规推线弹性基准线
            _homeY = _transform.position.y;
            // A* 单体移动使用 flowIndex=-1；若不在此恢复默认流场，下一帧 NORMAL 推线会在 Job 中以负索引读流场池
            _currentFlowIndex = (_faction == WE.FactionType.FRIENDLY) ? WE.FriendlyFlowFieldDefaultId : WE.EnemyFlowFieldDefaultId;
            _curMoveCmd = SD.MoveCmd.NORMAL;
            _aStarWaypoints.Clear();
            // 清掉指令目标坐标，避免 OnDrawGizmos 继续画到旧终点的连接线/红十字
            _cmdTargetPos = GD.InvalidVector2;
            _lastAStarTargetPos = GD.InvalidVector2;
            ChangeStatusTo(SD.SoldierStatus.MOVE); // 士兵回归推线，Hero会在子类重写回IDLE
        }

        //到达玩家指点的目标或者被再次操控 将自己从局部流场中删除掉
        protected void ClearCurrentLocalFlowField()
        {
            if (_currentFlowIndex >= WE.LocalFlowFieldStartId)
            {
                PathFinderMap pathMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(_mapId);
                if (pathMap != null)
                {
                    pathMap.ReleaseUnitFromFlowField(_currentFlowIndex);
                }
                // 恢复为全局默认流场
                _currentFlowIndex = (_faction == WE.FactionType.FRIENDLY) ? WE.FriendlyFlowFieldDefaultId : WE.EnemyFlowFieldDefaultId;
            }
        }

        //prepare attck, the attack damage calculation will be done ine attack post , called by animation notification
        //isRemote:是否是远程攻击
        //true: 远程攻击，不会立刻获取到攻击结果
        protected virtual void AttackPre()
        {
            if (_beInited == false)
                return;

            if (_curStatus != SD.SoldierStatus.ATTACKTATGET)
                return;

            MonoBehaviour rivalScript = null;
            rivalScript = _rivalScript;

            bool doAttack = true;
            if (ReferenceEquals(rivalScript, null) == false)
            {
                float damage = _curState.p_damage;
                if (!_skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //skill
                    doAttack = SdOnSkillDoAttackPre(damage, rivalScript, _rivalType, out damage);

                if (!_talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER].IsNullOrEmpty()) //talent
                    doAttack = SdOnTalentDoAttackPre(damage, rivalScript, _rivalType, out damage);

                int buffCnt = _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER].Count;
                if (buffCnt > 0) //buff
                {
                    for (int i = 0; i < buffCnt; i++)
                    {
                        damage = _buffsInEffect[(int)BFD.BuffTriggerType.ATTACKTRIGGER][i].BuffDoAttackPre(damage, rivalScript, _rivalType);
                    }
                }

                //not attack, skill do somethine
                if (doAttack == false)
                    return;

                _atkValid = true;
                _atkDamage = damage;
                _atkRivalScript = rivalScript;
                _atkRivalType = _rivalType;
            }
        }

        //call by animation notification
        protected virtual void AttackPost()
        {
            if (_beInited == false)
                return;

            if (_atkValid == true)
            {
                if (_isRemote == false) //近战攻击
                {
                    CloseRangeAttack(_atkDamage, _atkRivalScript, _atkRivalType);
                }
                else //远程，远程攻击post skill在BulletHit内
                {
                    RemoteRangedAttack(_atkDamage, _atkRivalScript, _atkRivalType);
                }

                _atkValid = false;
            }
        }

        //计算需要使用的动画的方向
        protected int CalculateDirectionIndex(Vector2 moveDir)
        {
            if (moveDir.sqrMagnitude < 0.001f)
                return _currentDirIndex; // 静止时保持上一帧的方向

            // Atan2 返回 -180 到 180，顺应 0 面向正下方的设定作垂直角归一化偏移
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            float normalizedAngle = angle + 90f;
            if (normalizedAngle < 0f) normalizedAngle += 360f;

            // 逆时针 45 度划分为 8 个扇区（0~7）
            return Mathf.RoundToInt(normalizedAngle / 45f) % 8;
        }

        // 带角度滞回 (hysteresis) 的方向计算: 仅用于持续移动状态下根据实际位移推朝向.
        // 8 扇区每个 ±22.5°, 在堵塞/避让位置 actualMove 方向噪声常常恰好骑在扇区边界, 朴素 RoundToInt
        // 会把方向索引在邻接扇区间反复弹跳, 直接体现成贴图换面闪动.
        // 此处叠加 8° 滞回区: 当前方向中心 30.5° 范围内不切扇区, 必须真的越过这一窗口才切换,
        // 既保留正常方向迁移, 又把单帧噪声压在窗口内不影响渲染.
        protected int CalculateDirectionIndexWithHysteresis(Vector2 moveDir, int currentIndex)
        {
            if (moveDir.sqrMagnitude < 0.001f)
                return currentIndex;

            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            float normalizedAngle = angle + 90f;
            if (normalizedAngle < 0f) normalizedAngle += 360f;

            float currentSectorCenter = currentIndex * 45f;
            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(currentSectorCenter, normalizedAngle));

            const float hysteresisHalfWidth = 22.5f + 8f; // 22.5°为天然扇区半宽, 8°是滞回额外阈值
            if (deltaAngle < hysteresisHalfWidth)
                return currentIndex;

            return Mathf.RoundToInt(normalizedAngle / 45f) % 8;
        }

        //rival searcher callback, 找到最近的rival
        protected virtual void OnClosestRivalFound(IGridNode target, float distance)
        {
            if (target != null)
            {
                if (distance <= _curState.p_attackRange)
                    TryChangeRival((WarEleParent)target, SD.TargetDetectType.INATTACKRANGE, false);
                else
                    TryChangeRival((WarEleParent)target, SD.TargetDetectType.INDETECTRANGE, false);
            }
        }

        protected virtual SearchShapeDef GetSearchShape()
        {
            _rivalSearchShape.p_centerOrStartPos = (Vector2)_transform.position;
            return _rivalSearchShape;
        }

        //isForce: 被目标嘲讽，除非目标死亡，要不然只能攻击目标
        //targetT == MIN 必须配合isForce一起用, 动态判断detectT
        protected virtual bool TryChangeRival(WarEleParent script, SD.TargetDetectType detectT, bool isForce)
        {
            if (_beInited == false)
                return false;

            GameObject target = script.gameObject;
            if (!isForce)
            {
                for (int i = 0; i < _blacklistedRivals.Count; i++)
                {
                    if (_blacklistedRivals[i].p_rival == target)
                        return false; // 目标在黑名单中
                }
            }
            WE.WarEleType targetT = script.gs_warEleType;
            if(_isForceRival == true && target != _rival) //被嘲讽状态下无法改变目标
                return false;

            if (isForce == false)
            {
                if (_curStatus == SD.SoldierStatus.ATTACKTATGET)
                    return false;

                if (_behaviorMode == SD.BehaviorMode.PROTECT)
                {
                    //only BEATTACK, INATTACKRANGE take into effect
                    //如果是入侵建筑的敌人，必须打击这个rival，在rival死亡之前不能因为任何原因改变目标
                    //入侵建筑的敌人进入攻击范围,或者敌人在范围外攻击到了protector
                    if (target == _rival && detectT == SD.TargetDetectType.INATTACKRANGE)
                    {
                        if (_rivalChooseType == SD.TargetDetectType.INPROTECTRANGE || _rivalChooseType == SD.TargetDetectType.BEATTACK)
                        {
                            if (_inDebug)
                                GameLogger.LogWarning($"{gameObject.name} {_behaviorMode}: Change detect type {_rival.name} {_rivalChooseType} -> {detectT} ");
                            _rivalChooseType = detectT;
                            _rivalChanged = true; //触发movetarget->attack需要这个
                            return true;
                        }
                    }
                    else if (_curStatus == SD.SoldierStatus.IDLE || _curStatus == SD.SoldierStatus.MOVE) //在建筑没有被入侵的情况下被攻击了
                    {
                        if (detectT != SD.TargetDetectType.BEATTACK)
                            return false;

                        if (targetT == WE.WarEleType.SOLDIER)
                        {
                            Soldier sd = target.GetComponent<Soldier>();
                            if (sd.gs_curState.p_bodyHide == true) //隐身
                                return false;
                            _rivalScript = sd;
                        }
                        else if (targetT == WE.WarEleType.BUILDING)
                        {
                            _rivalScript = target.GetComponent<WarBuilding>();
                        }

                        _rival = target;
                        _rivalType = targetT;
                        if (_inDebug)
                            GameLogger.LogWarning(
                                $"{gameObject.name} {_behaviorMode}: Change target to {_rival.name} because {_rivalChooseType} -> {detectT}");
                        _rivalChooseType = detectT;
                        _rivalChanged = true;
                        return true;
                    }
                }
                else if (_behaviorMode == SD.BehaviorMode.NORMAL)
                {
                    if (target == _rival) //target just is the rival, but detect in another way, update the choose type
                    {
                        if (_rivalChooseType < detectT)
                        {
                            if (_inDebug)
                                GameLogger.LogDebug($"{gameObject.name}: Change detect type {_rival.name} {_rivalChooseType} -> {detectT} ");
                            _rivalChooseType = detectT;
                            _rivalChanged = true; //触发movetarget->attack需要这个
                            return true;
                        }

                        return false;
                    }

                    lock (_targetLock)
                    {
                        if (_rivalChooseType < detectT)
                        {
                            if (targetT == WE.WarEleType.WEAPON)
                            {
                                //get the rival from weapon gameobject
                                //currently weapon prefabs not set tag, so will not enter this judgement
                            }
                            else
                            {
                                if (targetT == WE.WarEleType.SOLDIER)
                                {
                                    Soldier sd = target.GetComponent<Soldier>();
                                    if (sd.gs_curState.p_bodyHide == true) //隐身
                                        return false;
                                    _rivalScript = sd;
                                }
                                else if (targetT == WE.WarEleType.BUILDING)
                                {
                                    _rivalScript = target.GetComponent<WarBuilding>();
                                }

                                _rival = target;
                                _rivalType = targetT;
                            }

                            if (_inDebug)
                                GameLogger.LogDebug($"{gameObject.name}: Change target to {_rival.name} because {_rivalChooseType} -> {detectT}");
                            _rivalChooseType = detectT;
                            _rivalChanged = true;
                            return true;
                        }
                    }
                }
            }
            else //被嘲讽 在嘲讽的情况，不管什么情况必须改变攻击目标
            {
                _isForceRival = true;
                _rival = target;
                float distance = math.distancesq(_transform.position, _rival.transform.position);
                if(distance <= math.pow(_curState.p_attackRange, 2))
                    _rivalChooseType = SD.TargetDetectType.INATTACKRANGE;
                else
                    _rivalChooseType = SD.TargetDetectType.BEATTACK;

                _rivalScript = target.GetComponent<WarEleParent>();
                _rivalType = targetT;
                _rivalChanged = true; //触发movetarget->attack需要这个
                if (_inDebug)
                    GameLogger.LogDebug($"{gameObject.name} : Forced to change attack target to {_rival.name}");
                return true;
            }

            return false;
        }

        protected void SetMoveDir(GD.DirDef dir)
        {
            _moveDir = dir;
        }

        //handle die skill and buff
        protected virtual void TriggerDieAction()
        {
            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER].IsNullOrEmpty()) //skill
                SdOnSkillDie();

            if (!_talentTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER].IsNullOrEmpty()) //talent
                SdOnTalentDie();

            int buffCnt = _buffsInEffect[(int)BFD.BuffTriggerType.DIETRIGGER].Count;
            if (buffCnt > 0) //buff
            {
                for (int i = 0; i < buffCnt; i++)
                {
                    _buffsInEffect[(int)BFD.BuffTriggerType.DIETRIGGER][i].BuffDie();
                }
            }
        }

        protected virtual void OnSoldierDie()
        {
            if (_behaviorMode == SD.BehaviorMode.PROTECT && _protectTarget != null)
                _protectTarget.ProtectorDie(this); //通知所守护的建筑守护者死亡

            //计算击杀奖励
            if (_faction != WE.FactionType.FRIENDLY)
            {
                if (_sdConf.p_dieRewardChance > 0 && _sdConf.p_dieReward > 0)
                {
                    int chance = Utils.GetRandomInt();
                    if(chance <= _sdConf.p_dieRewardChance)
                        WarResCtrl.Instance.AddRes(WarResDefine.ResTypes.GOLDCOIN, Mathf.RoundToInt(_sdConf.p_dieReward));
                }
            }
            DeInitSoldier();
        }

        protected virtual void DeInitSoldier()
        {
            if (_beInited == false)
                return;

            ClearCurrentLocalFlowField(); // 士兵死亡，注销对流场的占用
            _blacklistedRivals.Clear();
            _stuckTimer = 0f;
            ChangeSearchStatus(false);
            SearchManager.Instance.UnregisterSearch(_rivalSearcher);
            base.DeInit();
            _onGDY = -9999;
            _lastAStarTargetPos = GD.InvalidVector2;
            gameObject.SetActive(false); //need change other way instead of SetActive
            ChangeStatusTo(SD.SoldierStatus.MIN); //将status恢复成无效值
            if (_curSkill != null)
            {
                int cnt = _curSkill.Count;
                for (int i = 0; i < cnt; i++)
                {
                    _curSkill[i].DeactiveSkill();
                    _curSkill[i].DeInitSkill();
                }
            }

            if (_curTalent != null)
            {
                int cnt = _curTalent.Count;
                for (int i = 0; i < cnt; i++)
                {
                    _curTalent[i].DeactiveTalent();
                    _curTalent[i].DeInitTalent();
                }
            }

            DeInitBuffs();

            _rival = null;
            _rivalScript = null;
            _moveSpeed = Vector3.zero;
            _rivalChooseType = SD.TargetDetectType.MIN;

            for (int i = 0; i < (int)SD.StateSoldierEffectType.MAX; i++)
            {
                var stList = _stateChangeArray[i];
                if (stList == null) continue; // 懒初始化：该状态类型本次生命周期从未被 Buff，直接跳过
                for (int j = 0; j < stList.Count; j++)
                    ReturnChange(stList[j]);
                stList.Clear();
            }

            _curPhyResistance = 0;
            _oriAttackInterval = 0;
            _nextAttackInterval = 0;
            _animProxy?.DeinitProxy();
        }


        protected virtual void ChangeStatusTo(SD.SoldierStatus status)
        {
            if (_curStatus == SD.SoldierStatus.DIE && status != SD.SoldierStatus.MIN) //already die, can not channge status except to MIN
                return;

            if (_curStatus == status)
                return;

            if (_inDebug)
                GameLogger.LogDebug(gameObject.name + ": ChangeStatusTo from " + _curStatus + " to " + status);

            if (status == SD.SoldierStatus.ATTACKTATGET)
            {
                _nextAttackInterval = 1; //设为1，确保能够call AttackPre在update时第一次进入ATTACK分支，如果设为0会导致第一次攻击动画但是没有调用AttackPre，没有攻击效果
                _blacklistedRivals.Clear();
                _stuckTimer = 0f;
            }

            _prvStatus = _curStatus;
            _curStatus = status;
            _entityData.p_subType = (_entityData.p_subType & 0x00FFFFFF) | (uint)_curStatus << 24;
        }

        //just set next animation, not change _curStatus
        //after the animation finished play, soldier will keep do will it did before
        protected virtual void SetNextSkillAnim(SD.SoldierAnimType type, Skill skill)
        {
            if (_inDebug)
                GameLogger.LogDebug($"{gameObject.name}: Set skill anim to {type}");
            _skillInAnim = skill;
        }

        protected virtual bool IsRivalValid()
        {
            bool ret = false;
            if (_rival != null)
            {
                if (_rivalType == WE.WarEleType.BUILDING)
                {
                    if (((WarBuilding)_rivalScript).IsBeDestroyedOrUpgrade(out WarBuilding upgradeTo) == true) //building has been destroyed
                    {
                        if (upgradeTo != null) //建筑升级了,士兵仍然把这个新的建筑当成目标
                        {
                            _rivalScript = upgradeTo;
                            _rival = upgradeTo.gameObject;
                            ret = true;
                        }
                    }
                    else
                        ret = true;
                }
                else if (_rivalType == WE.WarEleType.SOLDIER)
                {
                    switch (((Soldier)_rivalScript).gs_curStatus)
                    {
                        case SD.SoldierStatus.MIN:
                        case SD.SoldierStatus.INIT:
                        case SD.SoldierStatus.DIE:
                        case SD.SoldierStatus.ERROR:
                        case SD.SoldierStatus.MAX:
                            break;
                        default:
                            if (((Soldier)_rivalScript).gs_curState.p_bodyHide != true) //隐身不能当成有效rival
                                ret = true;
                            break;
                    }
                }
            }

            if (ret == false) //rival 不存在了
            {
                _isForceRival = false; //可能之前被嘲讽
                _rival = null;
                _rivalScript = null;
                _rivalChooseType = SD.TargetDetectType.MIN;
            }
            return ret;
        }

        //return true :start play a new animation
        protected virtual bool CheckAnimation()
        {
		    SD.SoldierAnimType nextAnim = SD.SoldierAnimType.MIN;

            //check status match the animation is playing, not match most happen when skill break up the cuurent animation
            switch (_curStatus)
            {
                case SD.SoldierStatus.INIT:
                case SD.SoldierStatus.IDLE:
                    nextAnim = SD.SoldierAnimType.IDLE;
                    break;
                case SD.SoldierStatus.BORN:
                    nextAnim = SD.SoldierAnimType.BORN;
                    break;
                case SD.SoldierStatus.MOVE:
                    nextAnim = SD.SoldierAnimType.MOVE;
                    break;
                case SD.SoldierStatus.ATTACKTATGET:
                    nextAnim = SD.SoldierAnimType.ATTACK;
                    break;
                case SD.SoldierStatus.RELEASESKILL:
                    nextAnim = SD.SoldierAnimType.SKILL;
                    break;
                case SD.SoldierStatus.DIE:
                    nextAnim = SD.SoldierAnimType.DIE;
                    break;
                case SD.SoldierStatus.INTERRUPT:
                    nextAnim = SD.SoldierAnimType.STUN;
                    break;
                default:
                    return true;
            }

		    // 计算 animOver 状态
		    // 如果 _curAnimType 被 OnAnimFinish 重置为了 MIN，说明非循环动画已播完
		    // 如果当前是 MOVE/IDLE 等循环动画，它永远不会自然完结，animOver 严格为 false
		    bool animOver = (_curAnimType == SD.SoldierAnimType.MIN);

            if (nextAnim != _curAnimType) //the animation want to play not equal to the current playing
            {
                if (animOver == false) //animation not over, need to break up the current animation
                {
                    if (_curAnimType == SD.SoldierAnimType.SKILL && _skillInAnim != null) //skill be interrupted
                    {
                        _skillInAnim.SkillAnimInterrupted(nextAnim); //info skill animation finished
                        _skillInAnim = null;
                    }
                    else if (_curAnimType == SD.SoldierAnimType.ATTACK) //攻击被打断，重新开始攻击记时
                    {
                        _nextAttackInterval = _oriAttackInterval;
                    }
                }

                if (nextAnim == SD.SoldierAnimType.ATTACK && _nextAttackInterval > 0)
                    return false; //计时没到，不触发动画
            }
            else //next animation is just current one
            {
                if (animOver == false) //the current animation not finished
                {
                    //during the skill animation, will keep enter this branch and return false
                    //must wait the current animation finish to do next attack or skill
                    if (nextAnim == SoldierDefines.SoldierAnimType.SKILL) //previous skill animation not finished ,can not play a new one, should happen
                        return false;
                    else if (nextAnim == SoldierDefines.SoldierAnimType.ATTACK) //attack faster then anim, ignore the new one
                        return false;
                    else if (nextAnim == SD.SoldierAnimType.DIE) //die anim can only play once
                        return false;
                    else if (nextAnim == SD.SoldierAnimType.BORN) //born anim can only play once
                        return false;
                    else if (nextAnim == SD.SoldierAnimType.ATTACK) //攻击动画不能被自己打断
                        return false;
                    else
                        return false; //for move, idle, stun animation
                }
                else
                {
                    if (nextAnim == SD.SoldierAnimType.ATTACK)
                    {
                        if (_nextAttackInterval > 0)
                            return false; //计时没到，不触发动画
                    }
                }
            }

            //play a new animation, different from the current
            if (_inDebug == true)
                GameLogger.LogDebug(_sdName + ": play animation " + nextAnim + "  status:" + _curStatus + " current:" + _curAnimType);
            _curAnimType = nextAnim; //record _curAnimType

		    // 注意 C# 优先级: ! 高于 ==。曾经写成 `!_animProxy.gs_hasStateAnim[...] == false`
		    // 实际等价于 `_animProxy.gs_hasStateAnim[...] == true`，把所有有动画的 state 都误判为没动画并 reset，
		    // 导致 ChangeAnimState/ChangeDirection 永远调不到、动画不播、方向锁在默认值。
		    if (_animProxy.gs_hasStateAnim[(int)_curAnimType] == false)
		    {
		        _curAnimType = SD.SoldierAnimType.MIN;
                return false;
            }

            // 当 proxy 已处于同一状态时（非循环动画完成后需要重新触发），强制重播
            // 否则 ChangeAnimState 因 stateId 未变而是 no-op，ECS 不重置动画，攻击动画卡在最后一帧
            if (_animProxy.gs_curAnimStateId == (uint)_curAnimType)
                _animProxy.ForceReplayAnimState((int)_curAnimType);
            else
                _animProxy.ChangeAnimState((int)_curAnimType);
		    return true;
		}

        // 计算方向和帧率
        protected void UpdateFrameAnimation(float deltaTime)
        {
            if (_curAnimType == SD.SoldierAnimType.MIN)
                return;

            // 考虑动画本身的播放速率调节（例如攻击速度、移动速度加成影响表现层）
            float speedModifier = 1.0f;
            if (_curAnimType == SD.SoldierAnimType.MOVE)
                speedModifier = _curState.p_moveSpeed;
            else if (_curAnimType == SD.SoldierAnimType.ATTACK)
                speedModifier = _curState.p_attackSpeed;
            _animProxy.ChangeAnimRate(speedModifier);

            // 锁定朝向
            if (_curStatus == SD.SoldierStatus.MOVE) //因为_desiredMoveDir是在jobs中计算的,会被每帧清空
            {
                Vector2 actualMoveVector = (Vector2)_transform.position - _lastFramePos;

                // 1) 时间维度滤波: EMA 把"撞墙→反弹"的高频反转拉直成净位移趋势.
                //    α=0.25 大约等价 4 帧窗口 (50Hz fixed update 下 ~80ms), 噪声压下来又不会明显拖尾.
                const float emaAlpha = 0.25f;
                _animMoveDirEMA = Vector2.Lerp(_animMoveDirEMA, actualMoveVector, emaAlpha);

                // 2) 净位移闸门: 滤波后的方向向量量级需要达到 "期望步长的 20%" 才更新方向.
                //    撞墙对称反弹时 EMA 量级被相消到接近 0, 自然落到闸门下方, 直接锁住上一帧朝向.
                float expectedStep = _curState.p_moveSpeed * Time.fixedDeltaTime;
                float minStep = Mathf.Max(expectedStep * 0.2f, 0.015f);
                if (_animMoveDirEMA.sqrMagnitude > minStep * minStep)
                {
                    // 3) 空间维度滞回: EMA 量级刚过闸门时方向角仍可能骑在扇区边界, 再做一次 30.5° 角度滞回.
                    _currentDirIndex = CalculateDirectionIndexWithHysteresis(_animMoveDirEMA, _currentDirIndex);
                }
            }
            else
            {
                // 退出 MOVE 状态时清空滤波器累积, 下次进入 MOVE 不会拿到陈旧方向.
                _animMoveDirEMA = Vector2.zero;

                if (_rival != null)
                {
                    Vector2 lookDir = _rival.transform.position - _transform.position;
                    _currentDirIndex = CalculateDirectionIndex(lookDir);
                }
            }
            _animProxy.ChangeDirection(_currentDirIndex);
        }

        protected virtual void InitBuffArray()
        {
            for (int i = 0; i < _buffArray.Length; i++)
            {
                switch ((BFD.SoldierBuffType)i)
                {
                    case BFD.SoldierBuffType.STUN:
                        _buffArray[i] = new StunBuff(this);
                        break;
                    case BFD.SoldierBuffType.STATE:
                        _buffArray[i] = new StateBuff(this);
                        break;
                    case BFD.SoldierBuffType.SHIELD:
                        _buffArray[i] = new ShieldBuff(this);
                        break;
                    case BFD.SoldierBuffType.REFLECTION:
                        _buffArray[i] = new ReflectionBuff(this);
                        break;
                    case BFD.SoldierBuffType.DURATIONDAMAGE:
                        _buffArray[i] = new DurativeDamageBuff(this);
                        break;
                    case BFD.SoldierBuffType.STUCK:
                        _buffArray[i] = new StuckBuff(this);
                        break;
                    case BFD.SoldierBuffType.FREEZE:
                        _buffArray[i] = new FreezeBuff(this);
                        break;
                    case BFD.SoldierBuffType.SHAPCHANGE:
                        _buffArray[i] = new ShapeChangeBuff(this);
                        break;
                    case BFD.SoldierBuffType.ATTACKLOST:
                        _buffArray[i] = new AttackLostBuff(this);
                        break;
                    default:
                        _buffArray[i] = null;
                        break;
                }
            }
        }

        protected virtual void DeInitBuffs()
        {
            int len = _buffArray.Length;
            for (int i = 0; i < len; i++)
            {
                if (_buffArray[i] != null)
                {
                    _buffArray[i].DeactiveBuff();
                }
            }
        }

        //重置动画的角度
        protected void ResetFaceDir()
        {
            _currentDirIndex = (_faction == WE.FactionType.FRIENDLY) ? 2 : 6;
        }

        //改变动画角度；可操控单位在 MOVE 时面向操控方向，仅在 IDLE/ATTACK 时面向敌人
        protected virtual void FaceToRival()
        {
            if (_rival == null)
                return;

            Vector2 dir = _rival.transform.position - _transform.position;
            SetFaceDirByVector(dir);
        }

        // 面向目标坐标点,在玩家控制移动时调用
        protected virtual void FaceToMoveCmdDir()
        {
            if (_curMoveCmd == SD.MoveCmd.MANMOVETOPOS || _curMoveCmd == SD.MoveCmd.MANMOVEATTACKTOPOS)
            {
                if (_cmdTargetPos != GD.InvalidVector2)
                {
                    Vector2 dir = _cmdTargetPos - (Vector2)_transform.position;
                    if (dir.sqrMagnitude >= 0.01f)
                        SetFaceDirByVector(dir);
                    return;
                }
            }

            if (_moveDir != GD.DirDef.NULLDIR && _moveDir != GD.DirDef.CENTER &&
                GD.DirVector.TryGetValue(_moveDir, out Vector2 moveVec) && moveVec.sqrMagnitude >= 0.01f)
                SetFaceDirByVector(moveVec);
        }

        protected void SetFaceDirByVector(Vector2 dir)
        {
            _currentDirIndex = CalculateDirectionIndex(dir);
        }

        //calculate state when _stateChange add or remove a item
        //index in the list
        protected float CalculateState(List<SoldierStateChange> list, int startFrom, float oriState)
        {
            int cnt = list.Count;
            if (startFrom >= cnt) //error  or maybe no item in the list
                return oriState;

            for (int i = startFrom; i < cnt; i++)
            {
                SoldierStateChange change = list[i];
                change.p_prvState = oriState;
                oriState = Utils.CalDeltaValue(oriState, change.p_deltaValue, change.p_calType);
            }

            return oriState;
        }

        //士兵进入另一个地图需要跟新_homeY
        //targetY 进入新地图后需要到达的y
        protected virtual void StepIntoMap(int fromMap, int toMap, float targetY)
        {
            if (fromMap == WE.OnGroundMapIndex)
                _onGDY = _homeY;
            if(toMap == WE.OnGroundMapIndex && _onGDY > -1000) //去往地面,而且以前去过地面
                _homeY = _onGDY;
            else
                _homeY = targetY;

            SearchManager.Instance.UnregisterSearch(_rivalSearcher);
            _rivalSearcher.p_mapId = toMap;
            SearchManager.Instance.RegisterSearch(_rivalSearcher);
            if (!_skillTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER].IsNullOrEmpty()) //skill
                SdOnSkillMapChangeTrigger(fromMap, toMap);
        }

        //打开/关闭索敌功能
        protected void ChangeSearchStatus(bool value)
        {
            _rivalSearcher.p_isEnabled = value;
        }

        //when state changed, some soldier may need to do some special operation
        //oriValue: value before changed
        //newValue: value after changed
        protected virtual void OnStateChanged(SD.StateSoldierEffectType stateType, float oriValue, float newValue){ }


        //在更新status时做一些额外的操作
        //return true:继续处理status
        //       false:不处理status
        protected virtual bool OnUpdateStatus()
        {
            return true;
        }

        protected override void CreateWarId()
        {
            _wfId = $"{_warEleType}_{_faction}_{_sdName}_{WE.GetSdIndex(_faction)}";
        }

        protected override void OnPause() { }

        protected override void OnResume() { }

        protected virtual void SdOnSkillUpdate()
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER][0].SkillUpdate();
        }

        protected virtual float SdOnSkillBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            if(damage <= 0)
                return 0;
            return _skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][0].SkillBeAttackPre(damage, rival, rivalType, isByPass);
        }

        protected virtual void SdOnSkillBeAttackPost(float damage, bool isDead, MonoBehaviour rivalScript, WE.WarEleType rivalType, bool isByPass)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][0].SkillBeAttackPost(damage, isDead, rivalScript, rivalType, isByPass);
        }

        protected virtual bool SdOnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WE.WarEleType rivalType,
            out float damage)
        {
            return _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][0].SkillDoAttackPre(hit, rivalScript, rivalType, out damage);
        }

        protected virtual void SdOnSkillDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][0].SkillDoAttackPost(hit, isDead, rivalScript, rivalType);
        }

        protected virtual void SdOnSkillDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList,
            List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][0].SkillDoAttackPost(hit, dieCount, rivalSdList, rivalBdList, target, type);
        }

        protected virtual void SdOnSkillDie()
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER][0].SkillDie();
        }

        protected virtual void SdOnSkillParterEnter(GameObject parter, WE.WarEleType type)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER][0].SkillParterEnter(parter, type);
        }

        protected virtual void SdOnSkillParterLeave(GameObject parter, WE.WarEleType type)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER][0].SkillParterLeave(parter, type);
        }

        protected virtual void SdOnSkillRivalEnter(GameObject rival, WE.WarEleType type)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER][0].SkillRivalEnter(rival, type);
        }

        protected virtual void SdOnSkillRivalLeave(GameObject rival, WE.WarEleType type)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER][0].SkillRivalLeave(rival, type);
        }

        protected virtual void SdOnSkillActivatedTrigger(string name)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.ACTIVETRIGGER][0].SkillActivatedTrigger(name);
        }

        protected virtual void SdOnSkillMapChangeTrigger(int fromMap, int toMap)
        {
            _skillTriggerArr[(int)SKD.SkillTriggerType.MAPCHANGE][0].SkillMapChange(fromMap, toMap);
        }

        protected virtual void SdOnTalentUpdate()
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.TIMETRIGGER][0].TalentUpdate();
        }

        protected virtual float SdOnTalentBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            if(damage <= 0)
                return 0;
            return _talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][0].TalentBeAttackPre(damage, rival, rivalType, isByPass);
        }

        protected virtual void SdOnTalentBeAttackPost(float damage, bool isDead, MonoBehaviour rivalScript, WE.WarEleType rivalType, bool isByPass)
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.BEATTACKTRIGGER][0].TalentBeAttackPost(damage, isDead, rivalScript, rivalType, isByPass);
        }

        protected virtual bool SdOnTalentDoAttackPre(float hit, MonoBehaviour rivalScript, WE.WarEleType rivalType,
            out float damage)
        {
            return _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][0].TalentDoAttackPre(hit, rivalScript, rivalType, out damage);
        }

        protected virtual void SdOnTalentDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][0].TalentDoAttackPost(hit, isDead, rivalScript, rivalType);
        }

        protected virtual void SdOnTalentDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList,
            List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.ATTACKTRIGGER][0].TalentDoAttackPost(hit, dieCount, rivalSdList, rivalBdList, target, type);
        }

        protected virtual void SdOnTalentDie()
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.DIETRIGGER][0].TalentDie();
        }

        protected virtual void SdOnTalentParterEnter(GameObject parter, WE.WarEleType type)
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER][0].TalentParterEnter(parter, type);
        }

        protected virtual void SdOnTalentParterLeave(GameObject parter, WE.WarEleType type)
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER][0].TalentParterLeave(parter, type);
        }

        protected virtual void SdOnTalentRivalEnter(GameObject rival, WE.WarEleType type)
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER][0].TalentRivalEnter(rival, type);
        }

        protected virtual void SdOnTalentRivalLeave(GameObject rival, WE.WarEleType type)
        {
            _talentTriggerArr[(int)SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER][0].TalentRivalLeave(rival, type);
        }

        //从静态statechange pool中获取一个
        private static SoldierStateChange RentChange()
        {
            lock (s_changePool) {
                return s_changePool.Count > 0 ? s_changePool.Dequeue() : new SoldierStateChange();
            }
        }

        //归还一个到静态pool中
        private static void ReturnChange(SoldierStateChange sc)
        {
            sc.p_prvState = 0f;
            sc.p_deltaValue = 0f;
            lock (s_changePool) {
                s_changePool.Enqueue(sc);
            }
        }
#endregion
    }
}
