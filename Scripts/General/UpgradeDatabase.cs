using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

using WarArchive;

namespace WarUpgrade
{
    using UD = UpgradeDefine;

    //记录了所有升级的node
    public class UpgradeDatabase
    {
#region public parameters

#endregion

#region private parameters

        //存档中的每个升级的进度
        private class UpgradeInArchive
        {
            public int p_upgradeType; //UD.UpgradeType
            public int p_level; //当前进度
        }

        static private UpgradeDatabase _instance;

        private UpgradeNode[] _upgradeItemArr; //记录所有upgrade的进度以及配置
        private List<UpgradeInArchive> _upgradeInArchives; //存档的进度
        private string _archiveFileName = "Upgrade.json"; //存档的文件名字
        private bool _beInited = false; //是否读取xml,建立_upgradeItemArr

#endregion

#region private parameters' get set

        public UpgradeNode[] gs_upgradeItemArr
        {
            get { return _upgradeItemArr; }
        }
#endregion

#region public functions

        //单例
        static public UpgradeDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new UpgradeDatabase();
                return _instance;
            }
        }

        public bool InitUpgradeDatabase(string confPath)
        {
            if (_beInited == true)
                return false;

            XmlDocument xmlDocument = Utils.LoadXmlFile(confPath);
            if (xmlDocument == null)
                return false;

            XmlNodeList nodeList = xmlDocument.SelectSingleNode("upgradeConfs").ChildNodes;
            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                XmlNode itemTypeAttr = nodeList[i].SelectSingleNode("type");
                if (itemTypeAttr == null)
                {
                    GameLogger.LogError("Fail the get item type in upgrade conf");
                    return false;
                }

                UD.UpgradeType upgradeType = Enum.Parse<UD.UpgradeType>(itemTypeAttr.Attributes["value"].Value);
                UpgradeNode upgradeNode = _upgradeItemArr[(int)upgradeType];
                if (upgradeNode.p_upgradeType != UD.UpgradeType.MIN)
                {
                    GameLogger.LogError("Duplicate item type in upgrade conf");
                    return false;
                }

                upgradeNode.p_upgradeType = upgradeType;
                var attrList = nodeList[i].ChildNodes;
                for (int j = 0; j < attrList.Count; j++)
                {
                    if(attrList[j].NodeType == XmlNodeType.Comment)
                        continue;
                    XmlElement tmp = attrList[j] as XmlElement;

                    switch (tmp.Name)
                    {
                        case "type":
                            //already set, ignore
                            break;
                        case "name":
                            upgradeNode.p_name = tmp.GetAttribute("value");
                            break;
                        case "description":
                            upgradeNode.SetRawDescription(tmp.GetAttribute("value"));
                            break;
                        case "condition":
                        {
                            List<UpgradeDefine.UpgradeType> list = tmp.GetAttribute("value").Split(',')
                                .Select(s => s.Trim())              // 去掉空格
                                .Where(s => !string.IsNullOrEmpty(s))
                                .Select(s => Enum.Parse<UpgradeDefine.UpgradeType>(s)) // 转成枚举
                                .ToList();

                            upgradeNode.p_conditions = new List<UpgradeNode>();
                            foreach (var value in list)
                            {
                                upgradeNode.AddCondition(_upgradeItemArr[(int)value]);
                            }
                        }
                            break;
                        case "maxLevel":
                            upgradeNode.p_maxLevel = int.Parse(tmp.GetAttribute("value"));
                            break;
                        case "price":
                            upgradeNode.p_price = int.Parse(tmp.GetAttribute("value"));
                            break;
                        case "specific":
                        {
                            upgradeNode.p_specifics = new Dictionary<string, string>();
                            var specList = attrList[j].ChildNodes;
                            foreach (var specItem in specList)
                            {
                                XmlElement spec = specItem as XmlElement;
                                upgradeNode.p_specifics.Add(spec.Name, spec.GetAttribute("value"));
                            }
                        }
                            break;
                        default:
                            GameLogger.LogError($"Unrecognized type in upgrade conf: {tmp.Name}");
                            return false;
                    }
                }
            }
            _beInited = true;
            return true;
        }

        //进入一个存档时读取当前进度
        //如果是新存档没有进度文件,创建进度文件
        public void LoadUpgradeByArchive()
        {
            _upgradeInArchives = Archive.Instance.LoadArchiveFile<List<UpgradeInArchive>>(_archiveFileName);
            if (_upgradeInArchives == null) //文件不存在,创建新的
            {
                _upgradeInArchives = new List<UpgradeInArchive>();
                int cnt = _upgradeItemArr.Length;
                for (int i = 1; i < cnt; i++)
                {
                    UpgradeInArchive item = new UpgradeInArchive();
                    item.p_upgradeType = (int)_upgradeItemArr[i].p_upgradeType;
                    item.p_level = 0;
                    _upgradeInArchives.Add(item);
                }
                Archive.Instance.SaveArchiveFile(_archiveFileName, _upgradeInArchives);
            }
            else
            {
                bool needWriteBack = false;
                int cnt = _upgradeInArchives.Count;
                if (cnt != _upgradeItemArr.Length - 1) //-1是因为arr中0是没有的
                {
                    GameLogger.LogError($"Upgrade list size mismatch {cnt} {_upgradeItemArr.Length}");
                    return;
                }

                for (int i = 0; i < cnt; i++)
                {
                    UpgradeInArchive item = _upgradeInArchives[i];
                    int level = item.p_level;
                    int index = item.p_upgradeType;
                    if (level > 0)
                    {
                        if (level > _upgradeItemArr[index].p_maxLevel)
                        {
                            level = _upgradeItemArr[index].p_maxLevel;
                            item.p_level = level;
                            needWriteBack = true;
                            GameLogger.LogWarning(
                                $"upgrade level in archive is too big for {_upgradeItemArr[index].p_upgradeType}, will correct it to " +
                                $"{_upgradeItemArr[index].p_maxLevel}");
                        }

                        _upgradeItemArr[index].p_curLevel = level;
                        _upgradeItemArr[index].p_isActive = true;
                    }
                }

                if(needWriteBack == true)
                    Archive.Instance.SaveArchiveFile(_archiveFileName, _upgradeInArchives);
            }
        }

        //退出存档需要reset upgrade的进度
        public void ResetUpgrade()
        {
            _upgradeInArchives = null;
            for (int i = 1; i < _upgradeItemArr.Length; i++)
                _upgradeItemArr[i].ResetNode();
        }
#endregion

#region private functions

        private UpgradeDatabase()
        {
            _upgradeItemArr = new UpgradeNode[(int)UD.UpgradeType.MAX];
            for (int i = 1; i < (int)UD.UpgradeType.MAX; i++)
            {
                _upgradeItemArr[i] = new UpgradeNode();
            }
        }

#endregion
    }

    //读取UpgradeConf.xml获取一种upgrade的详细信息
    //读取Upgrade.json获取当前upgrade的进度
    public class UpgradeNode
    {
        public UD.UpgradeType p_upgradeType;
        public int p_maxLevel;
        public string p_name;
        public List<UpgradeNode> p_conditions; //前置条件
        public int p_price;
        public Dictionary<string, string> p_specifics;
        public bool p_isActive;
        public int p_curLevel;

        private string _description;
        //占位符"{ }"正则匹配
        private static readonly Regex _placeholderRegex = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        public UpgradeNode()
        {
            p_upgradeType = UD.UpgradeType.MIN;
            p_conditions = null;
            p_isActive = false;
            p_curLevel = 0;
        }

        //description是包含"{}"占位符的raw data
        public void SetRawDescription(string description)
        {
            _description = description;
        }

        public bool AddCondition(UpgradeNode condition)
        {
            if(p_conditions == null)
                p_conditions = new List<UpgradeNode>();

            if(condition == this)
                return false;
            if(p_conditions.Contains(condition) == true)
                return false;

            p_conditions.Add(condition);
            return true;
        }

        public void ResetNode()
        {
            p_isActive = false;
            p_curLevel = 0;
        }

        public bool CanActiveNode()
        {
            if (p_conditions.IsNullOrEmpty() == true)
                return true;
            int cnt = p_conditions.Count;
            for (int i = 0; i < cnt; i++)
            {
                if(p_conditions[i].p_isActive == false)
                    return false;
            }
            return true;
        }

        public string GetDescription()
        {
            if (string.IsNullOrEmpty(_description) == true)
                return null;

            return _placeholderRegex.Replace(_description, match =>
            {
                var key = match.Groups[1].Value;

                if (p_specifics != null && p_specifics.TryGetValue(key, out var value))
                    return value;

                GameLogger.LogError($"[DescriptionResolver] Cannot find placeholder key '{key}' in <specific> dictionary. Raw='{_description}'");
                return match.Value;
            });
        }

        //return negative value means fail
        public float ConvertSpecificToNum(string key)
        {
            if (p_specifics == null)
                return -1;
            if (p_specifics.TryGetValue(key, out var value) == false)
            {
                GameLogger.LogError($"Fail to get key {key} in upgrade {p_upgradeType}");
                return -1;
            }

            return Utils.ParseStringToNumber(value);
        }
    }

    public class UpgradeDefine
    {
        //定义了所有所有upgrade的类型
        public enum UpgradeType
        {
            MIN = 0,

            ADDSOLDIERHPINC, //增加士兵回血速度
            ADDHEROHPMAX, //英雄增加100HP
            ADDSOLDIERMOVEINCAVE, //占领洞穴中士兵移动速度增加
            UNLOCKRANGEDHERO, //解锁远程英雄
            UNLOCKWHIRLWINDSLASH, //解锁旋风斩
            UNLOCKCANNONEER, //解锁火炮手
            UNLOCKARROWRAIN, //解锁箭雨
            ADDHEROHPINC, //增加英雄回血速度
            UNLOCKSHIELDSOLDIER, //解锁盾兵
            UNLOCKSUDDENDEMISE, //解锁暴毙
            UNLOCKCRISISUNLEASHED, //解锁危机降临
            UNLOCKMAGICHERO, //解锁魔法英雄
            UNLOCKSHAMAN, //解锁萨满
            UNLOCKSTORMFURY, //解锁风雨交加
            UNLOCKFROZENSEAL, //解锁冰封

            ADDSTARTGOLD, //增加初始金币
            ADDGOLDPERSEC, //增加每秒金币收入
            ADDCAVEPRODUCE, //增加占领的洞穴中矿场的收入
            KILLNEUTRALGETGOLD, //击杀中立单位获得金钱
            UNLOCKNEUTRALSELL, //解锁中立单位购买
            CAMPREFRESHNEUTRAL, //刷新中立单位
            KILLGETGOLD, //击杀获取金币

            ONEMORECARD, //多抽一张卡
            STARTDRAW, //开局时获取抽卡机会
            ADDCARDLOCK, //增加一张锁定卡牌
            CARDPRICEDOWN, //卡牌价格降低
            ENLARGEBAG, //扩大背包
            DOWNDRAWNEEDEYE, //减少抽卡所需的眼石
            DOWNREFRESHPRICE, //减少刷新卡牌的价格
            GETFREEREFRESH, //获取免费刷新插排次数

            ADDMELEEBARRACKSPAWNSPOT, //近战兵营获取一个额外的生产位置
            ADDRANGEDBARRACKSPAWNSPOT, //远程兵营获取一个额外的生产位置
            ADDMAGICBARRACKSPAWNSPOT, //魔法兵营获取一个额外的生产位置
            UNLOCKFROSTTOWER, //解锁冰霜塔
            AUTOREPAIR,  //建筑自动修复
            UNLOCKFLAMETOWER, //解锁燃烧塔
            UNLOCKSIEGETOWER, //解锁攻城塔
            DESTROYGETFEEBACK, //己方建筑被摧毁时获得部分金钱返还
            ADDTOWERDAMAGEINCAVE, //增加防御塔在洞穴中的攻击力
            DESTROYGETGOLD, //摧毁敌人建筑获取金币
            SELLGETFEEBACK, //出售己方建筑获取金钱返还
            UNLOCKLASERTOWER,  //解锁激光塔
            UNLOCKARCTOWER, //解锁闪电塔

            MAX,
        }
    }
}






