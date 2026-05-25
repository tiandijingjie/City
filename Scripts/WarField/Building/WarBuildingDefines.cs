using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;

    public class WarBuildingDefines
    {
        public enum BuildingMode
        {
            MIN = 0,
            FORTRESS,
            BARRACK,  //spawn soldier
            DEFENCE,
            PORTAL, //传送阵
            GOLDMINE, //金矿
            GEMMINE, //宝石矿
            CAVE, //洞穴
            PROPBD, //道具生成的建筑
            MAX,
        }

        //适用于敌方建筑，建筑不同的触发出兵的方式
        public enum BarrackTriggerStage
        {
            MIN = 0,
            FIRST,
            SECOND,
            LAST, //the building body been attacked
            MAX,
        }

        //human occupy some building process
        public enum OccupyStatus
        {
            MIN = 0,
            NOOCCUPY,
            OCCUPYING,
            OCCUPIED,
            MAX,
        }

        //defence building animation types
        public enum DfBdAnimType
        {
            MIN = 0,
            IDLE,
            STARTATTACK,
            ATTACK,
            STOPATTACK,
            MAX,
        }

        static public int MaxSpawnSpotNum = 6;

        //the attack time inverval compare to the fix update time
        static public float GetAttackInterval(float attackSpeed)
        {
            return Utils.CountOfFixUpdate(1 / attackSpeed) ;
        }
    }

    //data from configuration file
    [System.Serializable]
    public class BuildingConf
    {
        [SerializeField] protected string _name;
        [SerializeField] protected WE.FactionType _faction;
        [SerializeField] protected WarBuildingDefines.BuildingMode _mode;
        [SerializeField] protected WE.RaceType _race = WE.RaceType.MIN;
        [SerializeField] protected int _subType; //e.g.  HumanDefines.BarrackType.MELEE
        [SerializeField] protected float _health;
        [SerializeField] protected Sprite _pic = null;
        [SerializeField] protected string _description = null;

        //only some building need build and price
        [SerializeField] protected float _buildTime;
        [SerializeField] protected int _price;
        [SerializeField] protected bool _enabled; //是否可以建造，除了一些防御塔需要抽卡获得，其他建筑默认可以建筑
        [SerializeField] protected bool _isUlocked; //是否解锁,通过upgrade解锁,只有_enabled和_isUlocked都为true才能建造
        [SerializeField] protected float _beAttackDown; //受到的伤害减免，这个减免不包括默认的伤害变动（默认对建筑的伤害计算：士兵70%，英雄155%，建筑100%）
        [SerializeField] protected float _hpInc; //自动回血
        [SerializeField] protected float _destroyFee; //敌方建筑被摧毁时获得的金币

        protected DataPool<WarBuilding> _subcribers = null; //the buildings use subscribe this config, 不需要复制到其他实例中去
        //get apis
        public string gs_name => _name;
        public WE.FactionType gs_faction => _faction;
        public WarBuildingDefines.BuildingMode gs_mode => _mode;
        public WE.RaceType gs_race => _race;
        public int gs_subType => _subType;
        public float gs_health => _health;
        public float gs_beAttackDown => _beAttackDown;
        public Sprite gs_pic => _pic;
        public string gs_description => _description;
        public float gs_buildTime => _buildTime;
        public int gs_price => _price;
        //public bool gs_enabled => _enabled; 调用IsContructable()查询
        public float gs_hpInc => _hpInc;
        public float gs_destroyFee => _destroyFee;

        public DataPool<WarBuilding> gs_subcribers => _subcribers;

        public BuildingConf(XmlNode confNode)
        {
            if (confNode == null)
            {
                GameLogger.LogError("Invalid node");
                return;
            }
            _faction = Enum.Parse<WE.FactionType>(((XmlElement)confNode).GetAttribute("faction"), true);
            _race = Enum.Parse<WE.RaceType>(((XmlElement)confNode).GetAttribute("race"), true);
            _mode = Enum.Parse<WarBuildingDefines.BuildingMode>(((XmlElement)confNode).GetAttribute("mode"), true);
            _subType = int.Parse(((XmlElement)confNode).GetAttribute("subtype"));
            _beAttackDown = 1; //默认不减免
            _enabled = true;  //默认可以建造
            _isUlocked = false; //默认锁定
            _hpInc = 0; //配置文件可能没有
            _destroyFee = 0; //配置文件没有

            var nodeList = confNode.ChildNodes;
            int cnt = nodeList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = nodeList[i] as XmlElement;
                switch (tmp.Name)
                {
                    case "health":
                        _health = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "build":
                        _buildTime = float.Parse(tmp.GetAttribute("buildTime"));
                        _price = int.Parse(tmp.GetAttribute("price"));
                        break;
                    case "name":
                        _name = tmp.GetAttribute("value");
                        break;
                    case "picPath":
                        string picPath = tmp.GetAttribute("value");
                        if (!string.IsNullOrEmpty(picPath))
                            _pic = Resources.Load<Sprite>($"Textures/BuildingTex/{_race}/{picPath}");
                        if(_pic == null)
                            GameLogger.LogError($"Fail to load pic at {picPath}");
                        break;
                    case "description":
                        _description = tmp.GetAttribute("value");
                        break;
                    case "enabled": //有些防御塔不能直接建造，同时也需要通过抽卡获取
                        _enabled = bool.Parse(tmp.GetAttribute("value"));
                        break;
                    case "hpInc":
                        _hpInc = float.Parse(tmp.GetAttribute("value"));
                        break;
                    default:
                        break;
                }
            }
        }

        public BuildingConf(BuildingConf src)
        {
            _name = src._name;
            _faction = src._faction;
            _mode = src._mode;
            _health = src._health;
            _price = src._price;
            _buildTime = src._buildTime;
            _subType = src._subType;
            _race = src._race;
            _pic = src._pic;
            _enabled = src._enabled;
            _beAttackDown = src._beAttackDown;
            _isUlocked = src._isUlocked;
            _hpInc = src._hpInc;
            _destroyFee = src._destroyFee;
        }

        public virtual void ReInitBuildingConf(BuildingConf src)
        {
            _name = src._name;
            _faction = src._faction;
            _mode = src._mode;
            _health = src._health;
            _price = src._price;
            _buildTime = src._buildTime;
            _subType = src._subType;
            _race = src._race;
            _pic = src._pic;
            _enabled = src._enabled;
            _beAttackDown = src._beAttackDown;
            _isUlocked = src._isUlocked;
            _hpInc = src._hpInc;
            _destroyFee = src._destroyFee;
        }

        public virtual string GetDataInfo()
        {
            string ret = $"HP:{_health}\n" +
                         $"建造时间:{_buildTime}";
            return ret;
        }

        //只能在WarBuildingCtrl里调用
        public bool AddSubscriber(WarBuilding sub)
        {
            if (_subcribers == null)//_subcribers里面加锁了
                _subcribers = new DataPool<WarBuilding>(true);
            return _subcribers.AddItem(sub);
        }

        //只能在WarBuildingCtrl里调用
        public void RemoveSubscriber(WarBuilding sub)
        {
            if(_subcribers != null) //_subcribers里面加锁了
                _subcribers.RemoveItem(sub);
        }

        //查询是否有资格建造
        public bool IsContructable()
        {
            if(_enabled == true && _isUlocked == true)
                return true;
            return false;
        }

        //info building that some attribute changed
        public void NotifySubscribers(string changeName, float value, GD.CalDeltaType calType)
        {
            if (_subcribers != null)
            {
                var stateValue = (sender: this, changeName, value, calType);
                _subcribers.ForEach(static (subcriber, state) =>
                {
                    subcriber.ConfChangeNotification(state.sender, state.changeName, state.value, state.calType);
                }, stateValue);
            }
        }

        //change the building's attribute permanently
        public virtual bool ChangeAttribute(string changeName, float changeValue, GD.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            //不需要判断_enabled
            valueAfterCal = 0;
            oriValue = 0;
            bool ret = true;
            switch (changeName)
            {
                case "health":
                    oriValue = _health;
                    _health = Utils.CalDeltaValue(_health, changeValue, changeCal);
                    valueAfterCal = _health;
                    break;
                case "price":
                    oriValue = _price;
                    _price = (int)Utils.CalDeltaValue(_price, changeValue, changeCal);
                    valueAfterCal = _price;
                    break;
                case "buildTime":
                    oriValue = _buildTime;
                    _buildTime = Utils.CalDeltaValue(_buildTime, changeValue, changeCal);
                    valueAfterCal = _buildTime;
                    break;
                case "beAttackDown":
                    oriValue = _beAttackDown;
                    _beAttackDown = Utils.CalDeltaValue(_beAttackDown, changeValue, changeCal);
                    valueAfterCal = _beAttackDown;
                    break;
                case "enabled": //有些建筑需要通过抽卡激活,另外的建筑是默认激活的
                    oriValue = 0;
                    valueAfterCal = 0;
                    if (_enabled == true)
                        ret = false;
                    else
                        _enabled = true;
                    break;
                case "isUlocked": //通过upgrade解锁, 所有建筑默认是锁定的
                    oriValue = 0;
                    valueAfterCal = 0;
                    if(_isUlocked == true)
                        ret = false;
                    else
                        _isUlocked = true;
                    break;
                case "hpInc":
                    oriValue = _hpInc;
                    _hpInc = Utils.CalDeltaValue(_hpInc, changeValue, changeCal);
                    valueAfterCal = _hpInc;
                    break;
                case "destroyFee":
                    oriValue = _destroyFee;
                    _destroyFee = Utils.CalDeltaValue(_destroyFee, changeValue, changeCal);
                    valueAfterCal = _destroyFee;
                    break;
                default:
                    ret = false;
                    break;
            }
            return ret;
        }
    }

    [System.Serializable]
    public class FortressConf : BuildingConf
    {
        public FortressConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.FORTRESS;
        }

        public FortressConf(BuildingConf src) : base(src)
        {
            FortressConf conf = src as FortressConf;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            FortressConf conf = src as FortressConf;
        }

        //valueAfterCal:计算之后的新的值
        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)

        {
            bool ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);
            if (ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL); //直接相等就行了
            return ret;
        }
    }

    [System.Serializable]
    public class BarrackConf : BuildingConf
    {
        [SerializeField] protected int _doubleChance; //一次生产两个士兵的概率
        [SerializeField] protected int _tripleChance; //一次生产3个士兵概率
        [SerializeField] protected int _quadrupleChance; //一次生产4个士兵概率
        [SerializeField] protected float _spwawnTimeDown; //减少生产时间
        [SerializeField] protected int _spwawnTimeDownChance; //减少生产时间的概率
        [SerializeField] protected float _spawnPriceDown; //减少生产所消耗资源
        [SerializeField] protected int _spawnPriceDownChance; //减少生产所消耗资源的概率
        [SerializeField] protected int _spawnSpotNum; //默认生产位置

        //get apis
        public int gs_doubleChance => _doubleChance;
        public int gs_tripleChance => _tripleChance;
        public int gs_quadrupleChance => _quadrupleChance;
        public float gs_spwawnTimeDown => _spwawnTimeDown;
        public int gs_spwawnTimeDownChance => _spwawnTimeDownChance;
        public float gs_spawnPriceDown => _spawnPriceDown;
        public int gs_spawnPriceDownChance => _spawnPriceDownChance;
        public int gs_spawnSpotNum => _spawnSpotNum;

        public BarrackConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.BARRACK;
            _doubleChance = 0;
            _tripleChance = 0;
            _quadrupleChance = 0;
            _spwawnTimeDown = 1 - 0; //没有减少 0%
            _spwawnTimeDownChance = (int)(0.3f * 100); //30%概率
            _spawnPriceDown = 1 - 0; //没有减少 0%
            _spawnPriceDownChance = (int)(0.2f * 100); //20%概率
            _spawnSpotNum = 1; //默认1个
        }

        public BarrackConf(BuildingConf src) : base(src)
        {
            BarrackConf conf = src as BarrackConf;
            _doubleChance = conf._doubleChance;
            _tripleChance = conf._tripleChance;
            _quadrupleChance = conf._quadrupleChance;
            _spwawnTimeDown = conf._spwawnTimeDown;
            _spwawnTimeDownChance = conf._spwawnTimeDownChance;
            _spawnPriceDown = conf._spawnPriceDown;
            _spawnPriceDownChance = conf._spawnPriceDownChance;
            _spawnSpotNum = conf._spawnSpotNum;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            BarrackConf conf = src as BarrackConf;
            _doubleChance = conf._doubleChance;
            _tripleChance = conf._tripleChance;
            _quadrupleChance = conf._quadrupleChance;
            _spwawnTimeDown = conf._spwawnTimeDown;
            _spwawnTimeDownChance = conf._spwawnTimeDownChance;
            _spawnPriceDown = conf._spawnPriceDown;
            _spawnPriceDownChance = conf._spawnPriceDownChance;
            _spawnSpotNum = conf._spawnSpotNum;
        }

        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            bool ret = true;
            oriValue = 0;
            switch (changeName)
            {
                case "doubleChance":
                    oriValue = _doubleChance;
                    _doubleChance = (int)Utils.CalDeltaValue(_doubleChance, changeValue, changeCal);
                    valueAfterCal = _doubleChance;
                    break;
                case "tripleChance":
                    oriValue = _tripleChance;
                    _tripleChance = (int)Utils.CalDeltaValue(_tripleChance, changeValue, changeCal);
                    valueAfterCal = _tripleChance;
                    break;
                case "quadrupleChance":
                    oriValue = _quadrupleChance;
                    _quadrupleChance = (int)Utils.CalDeltaValue(_quadrupleChance, changeValue, changeCal);
                    valueAfterCal = _quadrupleChance;
                    break;
                case "spwawnTimeDown":
                    oriValue = _spwawnTimeDown;
                    _spwawnTimeDown = Utils.CalDeltaValue(_spwawnTimeDown, changeValue, changeCal);
                    valueAfterCal = _spwawnTimeDown;
                    break;
                case "spwawnTimeDownChance":
                    oriValue = _spwawnTimeDownChance;
                    _spwawnTimeDownChance = Mathf.RoundToInt(Utils.CalDeltaValue(_spwawnTimeDownChance, changeValue, changeCal));
                    valueAfterCal = _spwawnTimeDownChance;
                    break;
                case "spawnPriceDown":
                    oriValue = _spawnPriceDown;
                    _spawnPriceDown = Utils.CalDeltaValue(_spawnPriceDown, changeValue, changeCal);
                    valueAfterCal = _spawnPriceDown;
                    break;
                case "spawnPriceDownChance":
                    oriValue = _spawnPriceDownChance;
                    _spawnPriceDownChance = Mathf.RoundToInt(Utils.CalDeltaValue(_spawnPriceDownChance, changeValue, changeCal));
                    valueAfterCal = _spawnPriceDownChance;
                    break;
                case "spawnSpotNum":
                    oriValue = _spawnSpotNum;
                    _spawnSpotNum = Mathf.RoundToInt(Utils.CalDeltaValue(_spawnSpotNum, changeValue, changeCal));
                    valueAfterCal = _spawnSpotNum;
                    break;
                default:
                    ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);
                    break;
            }

            if (ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL);
            return ret;
        }
    }

    [System.Serializable]
    public class DefenceConf : BuildingConf
    {
        [SerializeField] protected float _damage;
        [SerializeField] protected float _atkSpeed;
        [SerializeField] protected float _atkSpeedCycle;
        [SerializeField] protected float _atkRange;
        [SerializeField] protected WeaponDefines.ProjectileTypes _weaponType;
        [SerializeField] protected Dictionary<string, float> _specConfs = null; //一些特有的属性
        //for shell/no target bullet
        [SerializeField] protected float _weaponRange; //炮弹的覆盖范围 or no target bullet的飞行距离
        [SerializeField] protected float _caveAttackAdd; //洞穴中攻击力提升, 加在_damage上, 配置文件中可能没有
        [SerializeField] protected int _sellPrice; //出售价格

        //get apis
        public float gs_damage => _damage;
        public float gs_atkSpeed => _atkSpeed;
        public float gs_atkSpeedCycle => _atkSpeedCycle;
        public float gs_atkRange => _atkRange;
        public WeaponDefines.ProjectileTypes gs_weaponType => _weaponType;
        public Dictionary<string, float> gs_specConfs => _specConfs;
        public float gs_weaponRange => _weaponRange;
        public float gs_caveAttackAdd => _caveAttackAdd;
        public int gs_sellPrice => _sellPrice;

        public DefenceConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.DEFENCE;
            var nodeList = confNode.ChildNodes;
            int cnt = nodeList.Count;
            _caveAttackAdd = 0;
            _sellPrice = Mathf.RoundToInt(_price * 0.2f);  //出售防御塔时默认收回20%建造费用

            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = nodeList[i] as XmlElement;
                switch (tmp.Name)
                {
                    case "damage":
                        _damage = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "atkSpeed":
                        _atkSpeed = float.Parse(tmp.GetAttribute("value"));
                        _atkSpeedCycle = WarBuildingDefines.GetAttackInterval(_atkSpeed);
                        break;
                    case "atkRange":
                        _atkRange = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "weaponType":
                        _weaponType = Enum.Parse<WeaponDefines.ProjectileTypes>(tmp.GetAttribute("value"), true);
                        break;
                    case "weaponRange":
                        _weaponRange = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "specific":
                    {
                        var specNodeList = nodeList[i].ChildNodes;
                        int specCnt = specNodeList.Count;
                        for (int j = 0; j < specCnt; j++)
                        {
                            if(specNodeList[j].NodeType == XmlNodeType.Comment)
                                continue;
                            XmlElement specTmp = specNodeList[j] as XmlElement;
                            if(_specConfs == null)
                                _specConfs = new Dictionary<string, float>();
                            _specConfs.Add(specTmp.Name, float.Parse(specTmp.GetAttribute("value")));
                        }
                    }
                        break;
                    default:
                        break;
                }
            }
        }

        public DefenceConf(BuildingConf src) : base(src)
        {
            DefenceConf defenceConf = src as DefenceConf;
            _damage = defenceConf._damage;
            _atkSpeed = defenceConf._atkSpeed;
            _atkSpeedCycle = WarBuildingDefines.GetAttackInterval(_atkSpeed);
            _atkRange = defenceConf._atkRange;
            _weaponType = defenceConf._weaponType;
            _weaponRange = defenceConf._weaponRange;
            _caveAttackAdd = defenceConf._caveAttackAdd;
            _sellPrice = defenceConf._sellPrice;
            if(defenceConf._specConfs != null)
                _specConfs = new Dictionary<string, float>(defenceConf._specConfs);
            else
                _specConfs = null;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            DefenceConf defenceConf = src as DefenceConf;
            _damage = defenceConf._damage;
            _atkSpeed = defenceConf._atkSpeed;
            _atkSpeedCycle = WarBuildingDefines.GetAttackInterval(_atkSpeed);
            _atkRange = defenceConf._atkRange;
            _weaponType = defenceConf._weaponType;
            _weaponRange = defenceConf._weaponRange;
            _caveAttackAdd = defenceConf._caveAttackAdd;
            _sellPrice = defenceConf._sellPrice;
            if (defenceConf._specConfs != null)
                _specConfs = new Dictionary<string, float>(defenceConf._specConfs);
            else
                _specConfs = null;
        }

        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            valueAfterCal = 0;
            bool ret = true;
            oriValue = 0;
            switch (changeName)
            {
                case "damage":
                    oriValue = _damage;
                    _damage = Utils.CalDeltaValue(_damage, changeValue, changeCal);
                    valueAfterCal = _damage;
                    break;
                case "atkSpeed":
                    oriValue = _atkSpeed;
                    _atkSpeed = Utils.CalDeltaValue(_atkSpeed, changeValue, changeCal);
                    _atkSpeedCycle = WarBuildingDefines.GetAttackInterval(_atkSpeed);
                    valueAfterCal = _atkSpeed;
                    break;
                case "atkRange":
                    oriValue = _atkRange;
                    _atkRange = Utils.CalDeltaValue(_atkRange, changeValue, changeCal);
                    valueAfterCal = _atkRange;
                    break;
                case "caveAtkAdd": //洞穴攻击加成
                    oriValue = _caveAttackAdd;
                    _caveAttackAdd = Utils.CalDeltaValue(_caveAttackAdd, changeValue, changeCal);
                    valueAfterCal = _caveAttackAdd;
                    break;
                case "sellPrice":
                    oriValue = _sellPrice;
                    _sellPrice = Mathf.RoundToInt(Utils.CalDeltaValue(_sellPrice, changeValue, changeCal));
                    valueAfterCal = _sellPrice;
                    break;
                default:
                    ret = false;
                    break;
            }

            if(ret == false)
                ret = ChangeSpecificAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);

            if(ret == false)
                ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);

            if(ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL);
            return ret;
        }

        //根据防御建筑具体的类型进行升级,仅对人族有效
        private bool ChangeSpecificAttribute(string changeName, float changeValue, GD.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            valueAfterCal = 0;
            oriValue = 0;
            if (_race != WE.RaceType.Human)
                return false;

            bool ret = true;
            if (_subType == (int)HumanDefines.DefenceType.VOLLEYTOWER)
            {
                switch (changeName)
                {
                    case "targetNum":
                        oriValue = _specConfs["targetNum"];
                        _specConfs["targetNum"] = Utils.CalDeltaValue(_specConfs["targetNum"], changeValue, changeCal);
                        valueAfterCal = _specConfs["targetNum"];
                        break;
                    default:
                        ret = false;
                        break;
                }
            }
            else if (_subType == (int)HumanDefines.DefenceType.FROSTTOWER)
            {
                switch (changeName)
                {
                    case "frostDamage":
                        oriValue = _specConfs["frostDamage"];
                        _specConfs["frostDamage"] = Utils.CalDeltaValue(_specConfs["frostDamage"], changeValue, changeCal);
                        valueAfterCal = _specConfs["frostDamage"];
                        break;
                    case "moveDown": //移动速度降低
                        float v = 0;
                        //不能让移动速度减小 >=100%  || <=0
                        if (Utils.CalDeltaValueInRange(_specConfs["moveDown"], changeValue, changeCal, 0, 1, ref v) == false)
                        {
                            GameLogger.LogError($"Value out of range (0,1), {v}");
                            ret = false;
                            break;
                        }
                        oriValue = _specConfs["moveDown"];
                        _specConfs["moveDown"] = v;
                        valueAfterCal = v;
                        break;
                    case "duration":
                        oriValue = _specConfs["duration"];
                        _specConfs["duration"] = Utils.CalDeltaValue(_specConfs["duration"], changeValue, changeCal);
                        valueAfterCal = _specConfs["duration"];
                        break;
                    default:
                        ret = false;
                        break;
                }
            }
            else if (_subType == (int)HumanDefines.DefenceType.FLAMETOWER)
            {
                switch (changeName)
                {
                    case "flameDamage":
                        oriValue = _specConfs["flameDamage"];
                        _specConfs["flameDamage"] = Utils.CalDeltaValue(_specConfs["flameDamage"], changeValue, changeCal);
                        valueAfterCal = _specConfs["flameDamage"];
                        break;
                    case "flameDuration":
                        oriValue = _specConfs["flameDuration"];
                        _specConfs["flameDuration"] = Utils.CalDeltaValue(_specConfs["flameDuration"], changeValue, changeCal);
                        valueAfterCal = _specConfs["flameDuration"];
                        break;
                    default:
                        ret = false;
                        break;
                }
            }
            else if (_subType == (int)HumanDefines.DefenceType.LASERTOWER)
            {
                switch (changeName)
                {
                    case "laserCnt":
                        oriValue = _specConfs["laserCnt"];
                        _specConfs["laserCnt"] = Utils.CalDeltaValue(_specConfs["laserCnt"], changeValue, changeCal);
                        valueAfterCal = _specConfs["laserCnt"];
                        break;
                    case "bombRange":
                        oriValue = _specConfs["bombRange"];
                        _specConfs["bombRange"] = Utils.CalDeltaValue(_specConfs["bombRange"], changeValue, changeCal);
                        valueAfterCal = _specConfs["bombRange"];
                        break;
                    case "bombDamage":
                        oriValue = _specConfs["bombDamage"];
                        _specConfs["bombDamage"] = Utils.CalDeltaValue(_specConfs["bombDamage"], changeValue, changeCal);
                        valueAfterCal = _specConfs["bombDamage"];
                        break;
                    default:
                        ret = false;
                        break;
                }
            }
            else if (_subType == (int)HumanDefines.DefenceType.ARCTOWER)
            {
                switch (changeName)
                {
                    case "targetNum":
                        oriValue = _specConfs["targetNum"];
                        _specConfs["targetNum"] = Utils.CalDeltaValue(_specConfs["targetNum"], changeValue, changeCal);
                        valueAfterCal = _specConfs["targetNum"];
                        break;
                    default:
                        ret = false;
                        break;
                }
            }
            else if (_subType == (int)HumanDefines.DefenceType.SIEGETOWER)
            {
                switch (changeName)
                {
                    case "siegeDamageUp":
                        oriValue = _specConfs["siegeDamageUp"];
                        _specConfs["siegeDamageUp"] = Utils.CalDeltaValue(_specConfs["siegeDamageUp"], changeValue, changeCal);
                        valueAfterCal = _specConfs["siegeDamageUp"];
                        break;
                    default:
                        ret = false;
                        break;
                }
            }
            else
                ret = false;

            return ret;
        }
    }

    //special类型的建筑，是按照每一个建筑定义Conf class
    //传送阵
    [System.Serializable]
    public class PortalConf : BuildingConf
    {
        [SerializeField] protected float _range;
        [SerializeField] protected float _transmitInterval; //多久传送一次
        [SerializeField] protected float _occupyTime; //需要多长时间占据

        //get apis
        public float gs_range => _range;
        public float gs_transmitInterval => _transmitInterval; //多久传送一次
        public float gs_occupyTime => _occupyTime;

        public PortalConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.PORTAL;

            var nodeList = confNode.ChildNodes;
            int cnt = nodeList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = nodeList[i] as XmlElement;
                switch (tmp.Name)
                {
                    case "range":
                        _range = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "transmitInterval":
                        _transmitInterval = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "occupyTime":
                        _occupyTime = float.Parse(tmp.GetAttribute("value"));
                        break;
                    default:
                        break;
                }
            }
        }

        public PortalConf(BuildingConf src) : base(src)
        {
            PortalConf portalConf = src as PortalConf;
            _range = portalConf._range;
            _transmitInterval = portalConf._transmitInterval;
            _occupyTime = portalConf._occupyTime;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            PortalConf portalConf = src as PortalConf;
            _range = portalConf._range;
            _transmitInterval = portalConf._transmitInterval;
            _occupyTime = portalConf._occupyTime;
        }

        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            valueAfterCal = 0;
            bool ret = true;
            oriValue = 0;
            switch (changeName)
            {
                case "range":
                    oriValue = _range;
                    _range = Utils.CalDeltaValue(_range, changeValue, changeCal);
                    valueAfterCal = _range;
                    break;
                case "occupyTime":
                    oriValue = _occupyTime;
                    _occupyTime = Utils.CalDeltaValue(_occupyTime, changeValue, changeCal);
                    valueAfterCal = _occupyTime;
                    break;
                case "transmitInterval":
                    oriValue = _transmitInterval;
                    _transmitInterval = Utils.CalDeltaValue(_transmitInterval, changeValue, changeCal);
                    valueAfterCal = _transmitInterval;
                    break;
                default:
                    ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);
                    break;
            }

            if (ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL);
            return ret;
        }
    }

    //金矿
    [System.Serializable]
    public class GoldMineConf : BuildingConf
    {
        [SerializeField] protected float _range; //检查范围
        [SerializeField] protected int _goldAddPerSec; //每秒收益
        [SerializeField] protected int _sellPrice; //出售价格
        [SerializeField] protected int[] _protectorLevelCnt; //守护怪物各个等级的数量
        [SerializeField] protected float _occupyTime; //需要多长时间占据
        [SerializeField] protected float _caveProduceAdd; //在已占领的洞穴中产量增加

        //get apis
        public float gs_range => _range;
        public int gs_sellPrice => _sellPrice;
        public int gs_goldAddPerSec => _goldAddPerSec;
        public int[] gs_protectorLevelCnt => _protectorLevelCnt;
        public float gs_occupyTime => _occupyTime;
        public float gs_caveProduceAdd => _caveProduceAdd;

        public GoldMineConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.GOLDMINE;
            var nodeList = confNode.ChildNodes;
            int cnt = nodeList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = nodeList[i] as XmlElement;
                switch (tmp.Name)
                {
                    case "range":
                        _range = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "goldAddPerSec":
                        _goldAddPerSec = int.Parse(tmp.GetAttribute("value"));
                        break;
                    case "sellPrice":
                        _sellPrice = int.Parse(tmp.GetAttribute("value"));
                        break;
                    case "protector":
                        _protectorLevelCnt = tmp.GetAttribute("value").Split(',').Select(x=>int.Parse(x)).ToArray();
                        break;
                    case "occupyTime":
                        _occupyTime = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "caveProduceAdd": //配置文件中没有,默认没有洞穴加成
                        _caveProduceAdd = float.Parse(tmp.GetAttribute("value"));
                        break;
                    default:
                        break;
                }
            }
        }

        public GoldMineConf(BuildingConf src) : base(src)
        {
            GoldMineConf conf = src as GoldMineConf;
            _range = conf._range;
            _goldAddPerSec = conf._goldAddPerSec;
            _sellPrice = conf._sellPrice;
            _protectorLevelCnt = conf._protectorLevelCnt;
            _occupyTime = conf._occupyTime;
            _caveProduceAdd = conf._caveProduceAdd;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            GoldMineConf conf = src as GoldMineConf;
            _range = conf._range;
            _goldAddPerSec = conf._goldAddPerSec;
            _sellPrice = conf._sellPrice;
            _protectorLevelCnt = conf._protectorLevelCnt;
            _occupyTime = conf._occupyTime;
            _caveProduceAdd = conf._caveProduceAdd;
        }

        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            bool ret = true;
            oriValue = 0;
            switch (changeName)
            {
                case "goldAddPerSec":
                    oriValue = _goldAddPerSec;
                    _goldAddPerSec = Mathf.RoundToInt(Utils.CalDeltaValue(_goldAddPerSec, changeValue, changeCal));
                    valueAfterCal = _goldAddPerSec;
                    break;
                case "sellPrice":
                    oriValue = _sellPrice;
                    _sellPrice = Mathf.RoundToInt(Utils.CalDeltaValue(_sellPrice, changeValue, changeCal));
                    valueAfterCal = _sellPrice;
                    break;
                case "caveProduceAdd": //配置文件中没有
                    oriValue = _caveProduceAdd;
                    _caveProduceAdd = Utils.CalDeltaValue(_caveProduceAdd, changeValue, changeCal);
                    valueAfterCal = _caveProduceAdd;
                    break;
                default:
                    ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);
                    break;
            }

            if(ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL);
            return ret;
        }
    }

    //宝石矿
    [System.Serializable]
    public class GemMineConf : BuildingConf
    {
        [SerializeField] protected float _range; //领地范围，用来生成中立生物的
        [SerializeField] protected int _gemInMine; //占领之后获取的宝石
        [SerializeField] protected int[] _protectorLevelCnt; //守护怪物各个等级的数量

        //get apis
        public float gs_range => _range;
        public int gs_gemInMine => _gemInMine;
        public int[] gs_protectorLevelCnt => _protectorLevelCnt;

        public GemMineConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.GEMMINE;
            var nodeList = confNode.ChildNodes;
            int cnt = nodeList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = nodeList[i] as XmlElement;
                switch (tmp.Name)
                {
                    case "range":
                        _range = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "gemInMine":
                        _gemInMine = int.Parse(tmp.GetAttribute("value"));
                        break;
                    case "protector":
                        _protectorLevelCnt = tmp.GetAttribute("value").Split(',').Select(x=>int.Parse(x)).ToArray();
                        break;
                    default:
                        break;
                }
            }
        }

        public GemMineConf(BuildingConf src) : base(src)
        {
            GemMineConf conf = src as GemMineConf;
            _range = conf._range;
            _gemInMine = conf._gemInMine;
            _protectorLevelCnt = conf._protectorLevelCnt;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            GemMineConf conf = src as GemMineConf;
            _range = conf._range;
            _gemInMine = conf._gemInMine;
            _protectorLevelCnt = conf._protectorLevelCnt;
        }

        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            bool ret = true;
            oriValue = 0;
            switch (changeName)
            {
                case "gemInMine":
                    oriValue = _gemInMine;
                    _gemInMine = (int)Mathf.Round(Utils.CalDeltaValue(_gemInMine, changeValue, changeCal));
                    valueAfterCal = _gemInMine;
                    break;
                default:
                    ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);
                    break;
            }

            if (ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL);
            return ret;
        }
    }

    //洞穴
    [System.Serializable]
    public class CaveConf : BuildingConf
    {
        [SerializeField] protected float _range; //检查范围

        //get apis
        public float gs_range => _range;

        public CaveConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.CAVE;
            var nodeList = confNode.ChildNodes;
            int cnt = nodeList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = nodeList[i] as XmlElement;
                switch (tmp.Name)
                {
                    case "range":
                        _range = float.Parse(tmp.GetAttribute("value"));
                        break;
                    default:
                        break;
                }
            }
        }

        public CaveConf(BuildingConf src) : base(src)
        {
            CaveConf conf = src as CaveConf;
            _range = conf._range;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            CaveConf conf = src as CaveConf;
            _range = conf._range;
        }

        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            bool ret = true;
            oriValue = 0;
            switch (changeName)
            {
                default:
                    ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);
                    break;
            }

            if (ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL);
            return ret;
        }
    }

    //道具生成的建筑
    [System.Serializable]
    public class PropBdConf : BuildingConf
    {
        [SerializeField] protected float _duration; //道具召唤出来的建筑一般有时间限制，-1：无时间限制,单位 秒
        [SerializeField] protected float _range; //建筑的影响范围的半径，-1：无影响范围
        [SerializeField] protected Dictionary<string, float> _specConfs = null; //一些特有的属性

        //get apis
        public float gs_duration => _duration;
        public float gs_range => _range;
        public Dictionary<string, float> gs_specConfs => _specConfs;

        public PropBdConf(XmlNode confNode) : base(confNode)
        {
            _mode = WarBuildingDefines.BuildingMode.PROPBD;
            var nodeList = confNode.ChildNodes;
            int cnt = nodeList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlElement tmp = nodeList[i] as XmlElement;
                switch (tmp.Name)
                {
                    case "duration":
                        _duration = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "range":
                        _range = float.Parse(tmp.GetAttribute("value"));
                        break;
                    case "specific":
                    {
                        var specNodeList = nodeList[i].ChildNodes;
                        int specCnt = specNodeList.Count;
                        for (int j = 0; j < specCnt; j++)
                        {
                            if(specNodeList[j].NodeType == XmlNodeType.Comment)
                                continue;
                            XmlElement specTmp = specNodeList[j] as XmlElement;
                            if(_specConfs == null)
                                _specConfs = new Dictionary<string, float>();
                            _specConfs.Add(specTmp.Name, float.Parse(specTmp.GetAttribute("value")));
                        }
                    }
                        break;
                    default:
                        break;
                }
            }
        }

        public PropBdConf(BuildingConf src) : base(src)
        {
            PropBdConf propBdConf = src as PropBdConf;
            _duration = propBdConf._duration;
            _range = propBdConf._range;
            if(propBdConf._specConfs != null)
                _specConfs = new Dictionary<string, float>(propBdConf._specConfs);
            else
                _specConfs = null;
        }

        public override void ReInitBuildingConf(BuildingConf src)
        {
            base.ReInitBuildingConf(src);
            PropBdConf propBdConf = src as PropBdConf;
            _duration = propBdConf._duration;
            _range = propBdConf._range;
            if(propBdConf._specConfs != null)
                _specConfs = new Dictionary<string, float>(propBdConf._specConfs);
            else
                _specConfs = null;
        }

        public override bool ChangeAttribute(string changeName, float changeValue, GlobalDefines.CalDeltaType changeCal, out float valueAfterCal,
            out float oriValue)
        {
            valueAfterCal = 0;
            bool ret = true;
            oriValue = 0;
            switch (changeName)
            {
                case "duration":
                    oriValue = _duration;
                    _duration = Utils.CalDeltaValue(_duration, changeValue, changeCal);
                    valueAfterCal = _duration;
                    break;
                case "range":
                    oriValue = _range;
                    _range = Utils.CalDeltaValue(_range, changeValue, changeCal);
                    valueAfterCal = _range;
                    break;
                default:
                    ret = false;
                    break;
            }

            //目前道具建筑不支持升级特有属性
            // if(ret == false)
            //     ret = ChangeSpecificAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);

            if(ret == false)
                ret = base.ChangeAttribute(changeName, changeValue, changeCal, out valueAfterCal, out oriValue);

            if(ret == true)
                NotifySubscribers(changeName, valueAfterCal, GD.CalDeltaType.EQUAL);
            return ret;
        }
    }
}

