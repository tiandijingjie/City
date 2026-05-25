using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SysCfgDefines
{
    public enum SectionTypes
    {
        MIN = 0,
        HOTKEY,
        IMAGE,
        SOUND,
        OTHER,
        MAX,
    }

    public enum CfgValueType
    {
        MIN = 0,
        KEYCODE,
        BOOL,
        FLOAT,
        CHOOSE,
        MAX,
    }
}

//called when config value changes
public interface ICfgCbIntf
{
    public void OnSysCfgChanged(SystemCfgItem changedItem);
}

public class SystemCfgSection
{
    public SysCfgDefines.SectionTypes p_secType;
    public List<SystemCfgItem> p_cfgItems;

    private List<ICfgCbIntf> _sectionCallbacks; //当section中任意item改变时都会调用
    private object _lock;

    public SystemCfgSection(SysCfgDefines.SectionTypes type)
    {
        p_secType = type;
        p_cfgItems = new List<SystemCfgItem>();
        _sectionCallbacks = new List<ICfgCbIntf>();
        _lock = new object();
    }

    public bool AddCfgItem(SystemCfgItem item)
    {
        lock (_lock)
        {
            if (GetCfgItem(item.p_name) == null)
            {
                p_cfgItems.Add(item);
                return true;
            }
        }

        GameLogger.LogWarning($"Already contains syscfg with name: {item.p_name}");
        return false;
    }

    public SystemCfgItem GetCfgItem(string itemName)
    {
        int cnt = p_cfgItems.Count;
        for (int i = 0; i < cnt; i++)
        {
            if(p_cfgItems[i].p_name == itemName)
                return p_cfgItems[i];
        }
        return null;
    }

    public bool AddCallback(ICfgCbIntf cb)
    {
        lock (_lock)
        {
            if (_sectionCallbacks.Contains(cb) == false)
            {
                _sectionCallbacks.Add(cb);
                return true;
            }
        }

        return false;
    }

    public void RemoveCallback(ICfgCbIntf cb)
    {
        _sectionCallbacks.Remove(cb);
    }

    public bool Save(bool needNotify)
    {
        bool saved = false;
        int cnt = p_cfgItems.Count;
        for (int i = 0; i < cnt; i++)
        {
            if (p_cfgItems[i].Save(needNotify) == true)
            {
                saved = true;
                if (needNotify == true)
                {
                    lock (_lock)
                    {
                        int cbCnt = _sectionCallbacks.Count;
                        for (int j = 0; j < cbCnt; i++)
                            _sectionCallbacks[j].OnSysCfgChanged(p_cfgItems[i]);
                    }
                }
            }
        }
        return saved;
    }

    //放弃修改
    public void AbandonChange()
    {
        int cnt = _sectionCallbacks.Count;
        for (int i = 0; i < cnt; i++)
        {
            p_cfgItems[i].AbandonChange();
        }
    }

    public void SetTobeDefault()
    {
        int cnt = _sectionCallbacks.Count;
        for (int i = 0; i < cnt; i++)
        {
            p_cfgItems[i].SetTobeDefault();
        }
    }
}

public class SystemCfgItem
{
    public string p_name;
    public SysCfgDefines.CfgValueType p_valueType;
    public SysCfgDefines.SectionTypes p_secType;

    protected string _curValue;
    protected string _defaultValue; //默认值
    protected string _tmpValue; //用户设置的临时值，当调用save之后会保存到p_value并调用_cfgCallbacks
    protected List<ICfgCbIntf> _cfgCallbacks;
    protected object _lock;

    public SystemCfgItem(string name, SysCfgDefines.SectionTypes section, string defaultValue)
    {
        p_name = name;
        p_secType = section;
        _defaultValue = defaultValue;
        _curValue = _defaultValue;
        _tmpValue = null;
        _cfgCallbacks = new List<ICfgCbIntf>();
        _lock = new object();
    }

    //as contructer only set default value, the _curValue is set tobe the same as default
    //so if _curValue different from the default, need to init after read the value from database
    public virtual void SetInitValue(string value)
    {
        _curValue = value;
    }

    public virtual object GetCfgValue()
    {
        return _curValue;
    }

    public virtual void SetTmpValue(string value)
    {
        _tmpValue = value;
    }

    //return true: value changed
    public virtual bool Save(bool needNotify)
    {
        if (_tmpValue != null)
        {
            if (_tmpValue != _curValue) //用户可能多次修改，最终又改回原始值
            {
                _curValue = _tmpValue;

                if (needNotify == true)
                {
                    lock (_lock)
                    {
                        int cnt = _cfgCallbacks.Count;
                        for (int i = 0; i < cnt; i++)
                        {
                            _cfgCallbacks[i].OnSysCfgChanged(this);
                        }
                    }
                }

                return true;
            }
            _tmpValue = null;
        }

        return false;
    }

    public virtual bool AddCallback(ICfgCbIntf cb)
    {
        lock (_lock)
        {
            if (_cfgCallbacks.Contains(cb) == false)
            {
                _cfgCallbacks.Add(cb);
                return true;
            }
        }

        return false;
    }

    public virtual void RemoveCallback(ICfgCbIntf cb)
    {
        lock (_lock)
            _cfgCallbacks.Remove(cb);
    }

    //恢复默认值，默认值定义在xml中
    public virtual void SetTobeDefault()
    {
        _curValue = _defaultValue;
        _tmpValue = null;
    }

    //放弃修改
    public virtual void AbandonChange()
    {
        _tmpValue = null;
    }
}

public class SystemCfgKeyItem : SystemCfgItem
{
    public SystemCfgKeyItem(string name, SysCfgDefines.SectionTypes section, string defaultValue) : base(name, section, defaultValue)
    {
        p_valueType = SysCfgDefines.CfgValueType.KEYCODE;
    }

    public override object GetCfgValue()
    {
        return Enum.Parse<KeyCode>(_curValue);
    }

    public new void SetTmpValue(KeyCode value)
    {
        base.SetTmpValue(value.ToString());
    }
}

public class SystemCfgBoolItem: SystemCfgItem
{
    public SystemCfgBoolItem(string name, SysCfgDefines.SectionTypes section, string defaultValue) : base(name, section, defaultValue)
    {
        p_valueType = SysCfgDefines.CfgValueType.BOOL;
    }

    public override object GetCfgValue()
    {
        return bool.Parse(_curValue);
    }

    public new void SetTmpValue(bool value)
    {
        base.SetTmpValue(value.ToString());
    }
}

public class SystemCfgFloatItem : SystemCfgItem
{
    public SystemCfgFloatItem(string name, SysCfgDefines.SectionTypes section, string defaultValue) : base(name, section, defaultValue)
    {
        p_valueType = SysCfgDefines.CfgValueType.FLOAT;
    }

    public override object GetCfgValue()
    {
        return float.Parse(_curValue);
    }

    public new void SetTmpValue(int value)
    {
        base.SetTmpValue(value.ToString());
    }
}

public class SystemCfgChooseItem : SystemCfgItem
{
    public List<string> p_options;
    public int p_selIndex; //_curValue's index in p_options

    public SystemCfgChooseItem(string name, SysCfgDefines.SectionTypes section, string defaultValue, List<string> options) : base(name, section,
        defaultValue)
    {
        p_valueType = SysCfgDefines.CfgValueType.CHOOSE;
        p_options = new List<string>(options);
        int cnt = p_options.Count;
        for (int i = 0; i < cnt; i++)
        {
            if (_curValue == p_options[i])
            {
                p_selIndex = i;
                return;
            }
        }
        GameLogger.LogError("Can not find current value in the option list");
    }

    public override void SetInitValue(string value)
    {
        base.SetInitValue(value);
        int cnt = p_options.Count;
        for (int i = 0; i < cnt; i++)
        {
            if (_curValue == p_options[i])
            {
                p_selIndex = i;
                return;
            }
        }
        GameLogger.LogError("Can not find current value in the option list");
    }

    public override bool Save(bool needNotify)
    {
        if (base.Save(needNotify) == true)
        {
            int cnt = p_options.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (_curValue == p_options[i])
                {
                    p_selIndex = i;
                    return true;
                }
            }
            GameLogger.LogError("Can not find current value in the option list");
            return true;
        }
        return false;
    }

    public override void SetTobeDefault()
    {
        base.SetTobeDefault();
        int cnt = p_options.Count;
        for (int i = 0; i < cnt; i++)
        {
            if (_curValue == p_options[i])
            {
                p_selIndex = i;
                return;
            }
        }
        GameLogger.LogError("Can not find default value in the option list");
    }
}


