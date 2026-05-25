using System;
using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;
    using BFD = BuffDefines;

    public class WarBuilding : WarEleParent
    {
#region public parameters

#endregion

#region private parameters

        /******** 这些是给 LoadWarBuildingPrefabs() 获取prefab类型时用的，实际不用********/
        [SerializeField] private WE.RaceType _race;
        [SerializeField] private WBD.BuildingMode _mode;
        [SerializeField] private int _subtype; //subtype
        /****************************************************************************/

        [SerializeField] protected bool _beAttack = false; //标识建筑是否处于脱战状态,在没有被攻击2s之后认为脱战
        [SerializeField] public float _curHealth;
        [SerializeField] public float _curHpInc; //conf.gs_hpInc * fixupdate time

        protected Collider2D _bodyCollider; //building's body
        protected SpriteRenderer _bdSpriteRenderer;
        protected BuildingConf _bdConf;
        protected CircleCollider2D _rangeCollider; //攻击范围

        //half of building picture size, used for calculate the bullect target position
        protected Vector2 _halfBodySize;
        protected bool _isDestroyed = false;
        protected bool _canWork;

        protected int _oriAttackCycle, _curAttackCycle; //脱战计时,不脱战不能进行升级/回血
        protected WarBuilding _upgradeTo; //升级到某个其他建筑,在DeInit不能设成null，因为可能建筑被放到pool之后还会被查询到

        //buff
        protected WarBuildingBuff[] _buffArray; //all of the buffs that warbuilding can have
        protected List<WarBuildingBuff>[] _buffsInEffect; //the buffs that is in effect
        protected List<Buff> _deactiveBuffList;

        //anim
        protected SkeletonAnimation _bdAnimator;
        protected bool _hasAnimator = false;

        //color, 建筑有时被选中会显示不同颜色
        protected Color _bdColor = Color.white;
#endregion

#region private parameters' get set
        //LoadWarBuildingPrefabs会调用
        public override WE.FactionType gs_faction
        {
            get
            {
                if (_beInited == true)
                    return _bdConf.gs_faction;
                return _faction;
            }
        }

        public WE.RaceType gs_race
        {
            get
            {
                if(_beInited == true)
                    return _bdConf.gs_race;
                return _race;
            }
        }

        public WBD.BuildingMode gs_mode
        {
            get
            {
                if (_beInited == true)
                    return _bdConf.gs_mode;
                return _mode;
            }
        }

        public int gs_subtype
        {
            get
            {
                if (_beInited == true)
                    return _bdConf.gs_subType;
                return _subtype;
            }
        }

        public Vector2 gs_bullectTargetPos
        {
            get
            {
                return _transform.position + new Vector3(_halfBodySize.x * _transform.localScale.x, _halfBodySize.y, 0);
            }
        }

        public BuildingConf gs_bdConf
        {
            get {return _bdConf;}
        }

        public bool gs_isDestroyed
        {
            get {return _isDestroyed;}
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _needBindMiniMap = true;
            _warEleType = WE.WarEleType.BUILDING;
            _bdSpriteRenderer = _transform.Find("BdSprite").GetComponent<SpriteRenderer>();
            Transform bdRange = _transform.Find("Range");
            if(bdRange != null)
                _rangeCollider = bdRange.GetComponent<CircleCollider2D>();
            _bodyCollider = GetComponent<Collider2D>();
            _canWork = false;
            _beAttack = false;
            _oriAttackCycle = _curAttackCycle = (int)Mathf.Round(Utils.CountOfFixUpdate(2)); //2s没有被攻击认为脱离被攻击状态

            //buff
            _buffArray = new WarBuildingBuff[(int)BFD.WarBuildingBuffType.MAX];
            InitBuffArray();
            _buffsInEffect = new List<WarBuildingBuff>[(int)BFD.BuffTriggerType.MAX];
            _deactiveBuffList = new List<Buff>();
            for (int i = 1; i < (int)BFD.BuffTriggerType.MAX; i++)
            {
                _buffsInEffect[i] = new List<WarBuildingBuff>();
            }

            //anim
            _bdAnimator = _transform.Find("Anim")?.GetComponent<SkeletonAnimation>(); //有些建筑没有动画
            if (_bdAnimator != null)
            {
                _bdAnimator.AnimationState.Event += OnAnimationNotification;
                _hasAnimator = true;
            }
        }

#endregion

#region public functions
        virtual public void StartWork()
        {
            _canWork = true;
        }

        public override void RunFixTask(float deltaTime)
        {
            if (_canWork == false)
                return;

            if (_curHealth <= 0)
            {
                if (_bdConf.gs_destroyFee > 0)
                    WarResCtrl.Instance.AddRes(WarResDefine.ResTypes.GOLDCOIN, Mathf.RoundToInt(_bdConf.gs_destroyFee));
                BdDestroy();
                return;
            }

            //如果被冰冻,停止所有功能
            if (_buffArray[(int)BFD.WarBuildingBuffType.FREEZE].gs_isActive == false)
            {
                if (_beAttack == true)
                {
                    _curAttackCycle--;
                    if (_curAttackCycle == 0)
                    {
                        _curAttackCycle = _oriAttackCycle;
                        _beAttack = false;
                    }
                }
                else
                {
                    //回血
                    if (_curHpInc > 0 && _curHealth < _bdConf.gs_health)
                    {
                        _curHealth += _curHpInc;
                        if (_curHealth > _bdConf.gs_health)
                            _curHealth = _bdConf.gs_health;
                    }
                }
                OnBdWork(deltaTime);
            }

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

        //return : true means building is destroyed
        public bool BeAttacked(GameObject attacker, MonoBehaviour attackScript, WE.WarEleType attackerT, float damage, out float hitValue)
        {
            _beAttack = true;
            _curAttackCycle = _oriAttackCycle; //处于被攻击状态

            return OnBeAttacked(attacker, attackScript, attackerT, damage, out hitValue);
        }

        //查询建筑是否还在，被摧毁或者升级都会导致建筑消失
        public bool IsBeDestroyedOrUpgrade(out WarBuilding upgradeTo)
        {
            upgradeTo = _upgradeTo;
            if (_upgradeTo != null) //已经升级
                return true;

            if (_curHealth <= 0)
                return true;
            return false;
        }

        //通过抽卡,界面选择等方式对建筑进行了某种属性的改变，这是长期的改变
        public void ConfChangeNotification(BuildingConf conf, string changeName, float changeValue, GD.CalDeltaType calType)
        {
            if(_beInited == false)
                return;

            if(changeName == "price") //忽略价格的变化,价格变化对当前已建造的建筑没有意义
                return;

            //保持warbuilding中的conf与原始的一致
            _bdConf.ChangeAttribute(changeName, changeValue, calType, out float calValue, out float oriValue);
            switch (changeName)
            {
                case "health":
                    _curHealth = calValue - oriValue; //加上生命的变化的值
                    if(_curHealth > _bdConf.gs_health)
                        _curHealth = _bdConf.gs_health;
                    break;
                case "hpInc":
                    _curHpInc = _bdConf.gs_hpInc * Time.fixedDeltaTime;
                    break;
                default:
                    break;
            }

            //除了warbuildingctrl里面保存的conf有订阅者之外，其他的conf不会有订阅
            OnConfUpgradeNotification(changeName, oriValue);
        }

        virtual public void RivalInRange(GameObject colTarget, WE.WarEleType type, WE.FactionType faction) { }
        virtual public void RivalOutRange(GameObject colTarget, WE.WarEleType type, WE.FactionType faction) { }
        virtual public void ParterInRange(GameObject colTarget, WE.WarEleType type) { }
        virtual public void ParterOutRange(GameObject colTarget, WE.WarEleType type) { }

        //在建筑状态/被攻击状态下无法升级
        public virtual bool CanUpgrade()
        {
            if(_beAttack == true)
                return false;
            return true;
        }

        //建筑升级，从一个低级的建筑升级到一个高等级的新建筑
        public virtual bool DoUpgrade(WarBuilding upgradeTo)
        {
            if (CanUpgrade() == true)
            {
                _upgradeTo = upgradeTo;
                _canWork = false;
                OnBdUpgrade(upgradeTo);
                WarBuildingCtrl.Instance.RemoveBuilding(this, _race, _mode, _subtype, _mapId);
                gameObject.SetActive(false);
                base.DeInit();
                return true;
            }
            return false;
        }

        public virtual bool IsPosInBuilding(Vector2 pos)
        {
            return _bdSpriteRenderer.bounds.Contains(pos);
        }

        //firstTime: 是不是同一个建筑的重复点击
        public virtual void UserSelected(bool firstTime) { }

        public bool SetBdColor(Color color)
        {
            if(_bdColor != Color.white) //只有建筑是本来颜色的时候才能设置颜色
                return false;
            _bdColor = color;
            _bdSpriteRenderer.color = _bdColor;
            if(_hasAnimator == true)
                _bdAnimator.skeleton.SetColor(color);
            return true;
        }

        public void ResetBdColor()
        {
            _bdColor = Color.white;
            _bdSpriteRenderer.color = _bdColor;
            if(_hasAnimator == true)
                _bdAnimator.skeleton.SetColor(Color.white);
        }

        public void RegisterBuff(WarBuildingBuff buff, BFD.BuffTriggerType type, BFD.WarBuildingBuffType buffType)
        {
            if (Utils.IsEnumInRange(type, BFD.BuffTriggerType.MIN, BFD.BuffTriggerType.MAX) == false)
                return;

            if (_buffsInEffect[(int)type].Contains(buff) == true) //for only kind buff, one soldier can only take one
                return;

            _buffsInEffect[(int)type].Add(buff);
        }

        public void UnregisterBuff(WarBuildingBuff buff, BFD.BuffTriggerType type)
        {
            if (Utils.IsEnumInRange(type, BFD.BuffTriggerType.MIN, BFD.BuffTriggerType.MAX) == false)
                return;
            _buffsInEffect[(int)type].Remove(buff);
        }

        //start to be affected by buff
        //value: the object want to send to buff
        public virtual bool BeAffectedByBuff<TValue>(BFD.WarBuildingBuffType type, in TValue value, BuffUnsafeCallback callback = null)
        {
            if (_curHealth <= 0)
                return false;

            bool ret = false;
            if (_buffArray[(int)type] != null)
                ret = _buffArray[(int)type].ActiveBuff(in value, callback);

            if (ret == false)
                return false;

            switch (type) //additional action of the buff
            {
                case BFD.WarBuildingBuffType.FREEZE:
                    if (_hasAnimator == true)
                        _bdAnimator.timeScale = 0; //暂停当前的动画
                    break;
                default:
                    break;
            }

            return true;
        }

        //重载
        public virtual bool BeAffectedByBuff<TValue, TRet>(BFD.WarBuildingBuffType type, in TValue value, ref TRet buffRet, BuffUnsafeCallback callback = null)
        {
            if (_curHealth <= 0)
                return false;

            bool ret = false;
            if (_buffArray[(int)type] != null)
                ret = _buffArray[(int)type].ActiveBuff(in value, ref buffRet, callback);

            if (ret == false)
                return false;

            switch (type) //additional action of the buff
            {
                case BFD.WarBuildingBuffType.FREEZE:
                    if (_hasAnimator == true)
                        _bdAnimator.timeScale = 0; //暂停当前的动画
                    break;
                default:
                    break;
            }

            return true;
        }

        //stop the buff effect
        public virtual void StopAffectedByBuff(BFD.WarBuildingBuffType type)
        {
            if (_buffArray[(int)type] != null)
                _deactiveBuffList.Add(_buffArray[(int)type]);//not deactive buff here, because this function maybe called during UpdateBuff, loop list at the same time remove item from list, may cause crash

            switch (type) //additional action of the buff
            {
                case BFD.WarBuildingBuffType.FREEZE:
                    if (_hasAnimator == true)
                        _bdAnimator.timeScale = 1; //恢复当前的动画
                    break;
                default:
                    break;
            }
        }

        //only stop part of the buff, can not do unregister buff,in it
        public virtual void StopPartOfBuff<TValue>(BFD.WarBuildingBuffType type, in TValue value)
        {
            if (_buffArray[(int)type] != null)
                _buffArray[(int)type].StopPartOfBuff(in value);
        }

        public bool CanAddBuff(BFD.WarBuildingBuffType type, object value = null)
        {
            if (_curHealth <= 0)
                return false;

            if (_buffArray[(int)type] != null)
            {
                return _buffArray[(int)type].CanAddBuff(value);
            }

            return false;
        }

        //修复建筑
        //isBaseOri : true 计算基于_bdConf.gs_health
        //            false 计算基于当前生命_curHealth
        public float BeRepaired(float value, GD.CalDeltaType calType, bool isBaseOri)
        {
            float oriValue = _curHpInc;
            if(isBaseOri == true)
                _curHealth += Utils.CalDeltaValue(_bdConf.gs_health, value, calType);
            else
                _curHealth += Utils.CalDeltaValue(_curHealth, value, calType);
            if (_curHealth > _bdConf.gs_health)
                _curHealth = _bdConf.gs_health;
            return _curHealth-oriValue;
        }

        //显示/隐藏 建筑的覆盖范围
        public virtual void SetCoverRange(bool value) { }
#endregion

#region private functions
        //can not be called outside
        //bdConf 必须在各个子类中赋值
        protected bool InitBuilding(byte mapId)
        {
            if (_bdConf == null || _bdConf.gs_race == WE.RaceType.MIN)
            {
                GameLogger.LogError("Fail to init building error conf");
                return false;
            }

            if (_beInited == true)
                return false;

            //生成entity中的subtype
            _entitySubType = WE.EncodeEntitySubType((byte)_bdConf.gs_race, (byte)_bdConf.gs_mode, (byte)_bdConf.gs_subType, 0);
            if(base.InitWarEle(mapId) == false)
                return false;

            _bdColor = Color.white;
            _bdSpriteRenderer.color = _bdColor;
            _upgradeTo = null;
            _isDestroyed = false;
            if (ReferenceEquals(_bdConf.gs_pic, null) == false)
            {
                _bdSpriteRenderer.sprite = _bdConf.gs_pic;
                _halfBodySize = _bdConf.gs_pic.rect.size / (2 * GD.PixelPerUnit); //should change to box collide2D
            }

            _transform.position = new Vector3(_transform.position.x, _transform.position.y, WarFieldUtil.GetZByY(_transform.position.y, WarMapCtrl
                .Instance.GetMapByIndex(_mapId).gs_passablePart.min.y));

            if(_faction == WE.FactionType.FRIENDLY)
            {
                gameObject.tag = "FriendlyBuilding";
                if(ReferenceEquals(_rangeCollider, null) == false)
                    _rangeCollider.gameObject.layer = LayerMask.NameToLayer("FriendlyBuildingRange");
            }
            else if(_faction == WE.FactionType.NEUTRAL)
            {
                gameObject.tag = "NeutralBuilding";
                if(ReferenceEquals(_rangeCollider, null) == false)
                    _rangeCollider.gameObject.layer = LayerMask.NameToLayer("NeutralBuildingRange");
            }
            else
            {
                gameObject.tag = "EnemyBuilding";
                if(ReferenceEquals(_rangeCollider, null) == false)
                    _rangeCollider.gameObject.layer = LayerMask.NameToLayer("EnemyBuildingRange");
            }

            _curHealth = _bdConf.gs_health;
            _curHpInc = _bdConf.gs_hpInc * Time.fixedDeltaTime;
            gameObject.SetActive(true);

            return true;
        }

        protected virtual bool OnBeAttacked(GameObject attacker, MonoBehaviour attackScript, WE.WarEleType attackerT, float damage, out float
                hitValue)
        {
            hitValue = _curHealth; //maybe health < damage
            if (attackerT == WE.WarEleType.SOLDIER)
            {
                //士兵对建筑的伤害在没有技能和卡牌的加持下是普通伤害的70%，Boss在没有技能和卡牌的加持下对建筑的伤害是普通伤害的155%
                if (attackScript != null && ((Soldier)attackScript).gs_sdLevel == SoldierDefines.SoldierLevel.BOSSLEVEL)
                    damage *= 1.55f;
                else
                    damage *= 0.7f;
            }//建筑对建筑的伤害在没有技能和卡牌的加持下是普通伤害的100%

            damage *= _bdConf.gs_beAttackDown; //通过升级或者抽卡获取的伤害减免
            damage = IndividualCalculateDamage(damage);
            _curHealth -= damage;
            if (_curHealth > 0)
            {
                hitValue = damage;
                return false;
            }
            return true;
        }

        protected virtual void InitBuffArray()
        {
            for (int i = 0; i < _buffArray.Length; i++)
            {
                switch ((BFD.WarBuildingBuffType)i)
                {
                    case BFD.WarBuildingBuffType.FREEZE:
                        _buffArray[i] = new FreezeBuffBuilding(this);
                        break;
                    default:
                        _buffArray[i] = null;
                        break;
                }
            }
        }

        //被敌人摧毁
        //己方的barrack、fortress不会真的被摧毁
        protected virtual void BdDestroy()
        {
            _isDestroyed = true;
            _canWork = false;
            OnBdDestroy();
            WarBuildingCtrl.Instance.RemoveBuilding(this, _race, _mode, _subtype, _mapId);
            gameObject.SetActive(false);
            base.DeInit();
        }

        protected override void CreateWarId()
        {
            _wfId = $"{_warEleType}_{_bdConf.gs_faction}_{_bdConf.gs_name}_{WE.GetBdIndex()}";
        }

        protected bool PlayBdAnim(int trackNum, string animName, bool loop)
        {
            if(_hasAnimator == false)
                return false;
            if(string.IsNullOrEmpty(animName) == true)
                return false;

            TrackEntry entry = _bdAnimator.AnimationState.SetAnimation(trackNum, animName, loop);
            if (entry == null)
            {
                GameLogger.LogError($"{_bdConf.gs_name} play animation {animName} failed");
                return false;
            }
            return true;
        }

        protected override void BindToMiniMapObj()
        {
            base.BindToMiniMapObj();

            var img = _miniMapObjTransform.Find("ElePic").GetComponent<Image>(); //建筑、士兵的图标
            img.sprite = _bdConf.gs_pic;
            //建筑不会去更新位置，此时需要把位置设置好
            UpdateMinimapObject();
            _miniMapObjTransform.sizeDelta = new Vector2(30, 30);
        }

        protected virtual void OnBdWork(float deltaTime) { }
        //被摧毀
        protected virtual void OnBdDestroy() { }
        //升級
        protected virtual void OnBdUpgrade(WarBuilding upgradeTo) { }

        protected virtual void OnConfUpgradeNotification(string changeName, float oriValue) { }

        //the event send from spine animation
        protected virtual void OnAnimationNotification(Spine.TrackEntry trackEntry, Spine.Event e){ }

        //一些建筑需要独立更进一步计算受到的伤害
        protected virtual float IndividualCalculateDamage(float damage)
        {
            return damage;
        }
#endregion
    }
}

