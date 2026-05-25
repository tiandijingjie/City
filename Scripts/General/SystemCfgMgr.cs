using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using LitJson;
using UnityEngine;

using WarArchive;
using WarField;
using SCD = SysCfgDefines;

public class SystemCfgMgr
{
#region public parameters

#endregion

#region private parameters
    static private SystemCfgMgr _instance = null;

    private string _systemFolderPath; //系统配置文件夹路径
    private string _cfgFilePath; //记录游戏配置的文件路径

    private SystemCfgSection[] _cfgSections;
    private bool _beInit;

#endregion

#region private parameters' get set

#endregion

#region public functions
    static public SystemCfgMgr Instance
    {
        get
        {
            if (_instance == null)
                _instance = new SystemCfgMgr();
            return _instance;
        }
    }

    public bool InitSysCfg()
    {
        if (_beInit == true)
        {
            GameLogger.LogError("Can not init SystemCfgMgr again");
            return false;
        }

        if (LoadDefaultCfg() == false)
        {
            GameLogger.LogError("Can not read the default SystemCfg");
            return false;
        }

        //maybe file not exist or format error,save the default value into the system cfg file
        if (LoadSystemCfg(_cfgSections) == false)
        {
            RestoreSystemCfgToDefault();
            Save();
        }

        return true;
    }

    public void RegisterCfgCb(SCD.SectionTypes sectionType, ICfgCbIntf cb)
    {
        _cfgSections[(int)sectionType].AddCallback(cb);
    }

    public void RegisterCfgCb(SCD.SectionTypes sectionType, string itemName, ICfgCbIntf cb)
    {
        _cfgSections[(int)sectionType].GetCfgItem(itemName).AddCallback(cb);
    }

    public void UnregisterCfgCb(SCD.SectionTypes sectionType, ICfgCbIntf cb)
    {
        _cfgSections[(int)sectionType].RemoveCallback(cb);
    }

    public void UnregisterCfgCb(SCD.SectionTypes sectionType, string itemName, ICfgCbIntf cb)
    {
        _cfgSections[(int)sectionType].GetCfgItem(itemName).RemoveCallback(cb);
    }

    public SystemCfgSection GetSysCfgSection(SCD.SectionTypes sectionType)
    {
        if (Utils.IsEnumInRange(sectionType, SCD.SectionTypes.MIN, SCD.SectionTypes.MAX))
            return _cfgSections[(int)sectionType];
        return null;
    }

    public SystemCfgItem GetSysCfgItem(SCD.SectionTypes sectionType, string itemName)
    {
        if (Utils.IsEnumInRange(sectionType, SCD.SectionTypes.MIN, SCD.SectionTypes.MAX))
            return _cfgSections[(int)sectionType].GetCfgItem(itemName);
        return null;
    }

    //set the cfg tobe default
    public bool RestoreSystemCfgToDefault()
    {
        for (var i = 0; i < (int)SCD.SectionTypes.MAX; i++)
        {
            if (_cfgSections[i] != null)
            {
                _cfgSections[i].SetTobeDefault();
            }
        }

        return true;
    }

    //confirm changes and save cfg into database
    public bool Save()
    {
        int len = _cfgSections.Length;
        for (int i = 0; i < len; i++)
        {
            if (_cfgSections[i] != null)
            {
                _cfgSections[i].Save(true);
            }
        }
        SaveSystemCfg(_cfgSections);
        return true;
    }

    //放弃修改
    public void AbandonChanges()
    {
        int len = _cfgSections.Length;
        for (int i = 0; i < len; i++)
        {
            if (_cfgSections[i] != null)
            {
                _cfgSections[i].AbandonChange();
            }
        }
    }

#endregion

#region private functions

    private SystemCfgMgr()
    {
        if (_instance != null)
        {
            GameLogger.LogError("Can not instance more than one SystemCfgMgr");
            return;
        }

        _instance = this;

        _systemFolderPath = Path.Combine(Application.persistentDataPath, "System");
        _cfgFilePath = Path.Combine(_systemFolderPath, "SystemCfg.json"); //if not has this file, will create it in InitSysCfg()

        if (!Directory.Exists(_systemFolderPath))
            Directory.CreateDirectory(_systemFolderPath);

        _cfgSections = new SystemCfgSection[(int)SCD.SectionTypes.MAX];

        _beInit = false;
    }

    private bool LoadDefaultCfg()
    {
        var xmlDocument = Utils.LoadXmlFile("Conf/SystemCfg");
        if (xmlDocument == null)
        {
            GameLogger.LogError("Can not Load the default SystemCfg file");
            return false;
        }

        var nodeList = xmlDocument.SelectSingleNode("systemCfgs")?.ChildNodes;
        if (nodeList != null)
            for (var i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i].NodeType == XmlNodeType.Comment)
                    continue;

                SystemCfgSection section;
                try
                {
                    SCD.SectionTypes sectionType =
                        Enum.Parse<SCD.SectionTypes>(((XmlElement)nodeList[i]).GetAttribute("type"));
                    section = _cfgSections[(int)sectionType] = new SystemCfgSection(sectionType);
                }
                catch (Exception e)
                {
                    GameLogger.LogException($"Fail to read section {nodeList[i].Name}", e);
                    return false;
                }

                if (LoadDefaultCfgInSection(section, nodeList[i]) == false)
                {
                    GameLogger.LogError($"Fail to add items into section {nodeList[i].Name}");
                    return false;
                }
            }

        return true;
    }

    private bool LoadDefaultCfgInSection(SystemCfgSection section, XmlNode parentNode)
    {
        var nodeList = parentNode.ChildNodes;
        for (var i = 0; i < nodeList.Count; i++)
        {
            if (nodeList[i].NodeType == XmlNodeType.Comment)
                continue;

            if (nodeList[i].Name != "cfgItem")
            {
                GameLogger.LogError($"Unknown cfg node name {nodeList[i].Name}");
                return false;
            }

            if (nodeList[i] is XmlElement elem)
            {
                SystemCfgItem item;
                var name = elem.GetAttribute("name");
                SCD.CfgValueType valueType = Enum.Parse<SCD.CfgValueType>(elem.GetAttribute("type"));
                string defaultValue = elem.GetAttribute("value");
                switch (valueType)
                {
                    case SCD.CfgValueType.KEYCODE:
                    {
                        item = new SystemCfgKeyItem(name, section.p_secType, defaultValue);
                    }
                        break;
                    case SCD.CfgValueType.BOOL:
                    {
                        item = new SystemCfgBoolItem(name, section.p_secType, defaultValue);
                    }
                        break;
                    case SCD.CfgValueType.FLOAT:
                    {
                        item = new SystemCfgFloatItem(name, section.p_secType, defaultValue);
                    }
                        break;
                    case SCD.CfgValueType.CHOOSE:
                    {
                        string option = elem.GetAttribute("options");
                        if (string.IsNullOrEmpty(option))
                        {
                            GameLogger.LogError("Can not get options in the choose item !!!");
                            return false;
                        }

                        List<string> v = new List<string>(option.Split(';'));
                        item = new SystemCfgChooseItem(name, section.p_secType, defaultValue, new List<string>(option.Split(';')));
                    }
                        break;
                    default:
                        GameLogger.LogError($"Unknown value type {valueType}");
                        return false;
                }

                section.AddCfgItem(item);
            }
        }

        return true;
    }

    //把配置存到json文件，覆盖整个文件
    private bool SaveSystemCfg(SystemCfgSection[] cfgSections)
    {
        var root = new JsonData();
        root.SetJsonType(JsonType.Object);

        foreach (var section in cfgSections)
        {
            if (section == null) continue;

            var sectionJson = new JsonData();
            sectionJson.SetJsonType(JsonType.Object);

            foreach (var cfgItem in section.p_cfgItems)
            {
                var itemJson = new JsonData();
                itemJson.SetJsonType(JsonType.Object);

                itemJson["type"] = cfgItem.p_valueType.ToString();
                itemJson["value"] = cfgItem.GetCfgValue().ToString();

                if (cfgItem is SystemCfgChooseItem chooseItem)
                {
                    var optionsJson = new JsonData();
                    optionsJson.SetJsonType(JsonType.Array);
                    foreach (var opt in chooseItem.p_options)
                        optionsJson.Add(opt);

                    itemJson["options"] = optionsJson;
                }

                sectionJson[cfgItem.p_name] = itemJson;
            }

            root[section.p_secType.ToString()] = sectionJson;
        }

        return JsonUtils.SaveJsonData(_systemFolderPath, "SystemCfg.json", root);
    }

    public bool LoadSystemCfg(SystemCfgSection[] cfgSections)
        {
            var jsonData = JsonUtils.LoadJsonData(_cfgFilePath);
            if (jsonData == null || !jsonData.IsObject)
            {
                GameLogger.LogWarning("Failed to read SystemCfg.json or the format is invalid.");
                return false;
            }

            foreach (var sectionKey in jsonData.Keys)
            {
                var sectionJson = jsonData[sectionKey];
                if (!sectionJson.IsObject)
                {
                    GameLogger.LogError($"Section {sectionKey} is not a JSON object");
                    continue;
                }

                var sectionType = Enum.Parse<SCD.SectionTypes>(sectionKey);
                var　section = cfgSections[(int)sectionType] = new SystemCfgSection(sectionType);

                foreach (var itemKey in sectionJson.Keys)
                {
                    SystemCfgItem cfgItem = null;

                    var itemJson = sectionJson[itemKey];
                    if (!itemJson.IsObject || !itemJson.Keys.Contains("value"))
                    {
                        GameLogger.LogError($"Config item {itemKey} has an incorrect format, missing value");
                        continue;
                    }

                    var name = itemKey;
                    var defaultValue = itemJson["value"].ToString();
                    var typeStr = itemJson["type"].ToString();

                    if (!Enum.TryParse<SCD.CfgValueType>(typeStr, true, out var valueType))
                    {
                        GameLogger.LogError($"Config item {itemKey} has unknown type: {typeStr}");
                        continue;
                    }

                    switch (valueType)
                    {
                        case SCD.CfgValueType.KEYCODE:
                        {
                            cfgItem = new SystemCfgKeyItem(name, section.p_secType, defaultValue);
                        }
                            break;
                        case SCD.CfgValueType.BOOL:
                        {
                            cfgItem = new SystemCfgBoolItem(name, section.p_secType, defaultValue);
                        }
                            break;
                        case SCD.CfgValueType.FLOAT:
                        {
                            cfgItem = new SystemCfgFloatItem(name, section.p_secType, defaultValue);
                        }
                            break;
                        case SCD.CfgValueType.CHOOSE:
                        {
                            if (itemJson.Keys.Contains("options"))
                            {
                                var optionsJson = itemJson["options"];
                                if (optionsJson.IsArray)
                                {
                                    var option = new List<string>();
                                    foreach (JsonData opt in optionsJson)
                                        option.Add(opt.ToString());
                                    cfgItem = new SystemCfgChooseItem(name, section.p_secType, defaultValue, option);
                                }
                            }
                        }
                            break;
                        default:
                            GameLogger.LogError($"Unknown value type {valueType}");
                            return false;
                    }

                    section.AddCfgItem(cfgItem);
                }
            }

            return true;
        }
#endregion
}
