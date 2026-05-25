using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using PD = PropDefines;

    //背包系统
    public class WarFieldBagSystem : MonoBehaviour
    {
#region public parameters
        public static WarFieldBagSystem Instance = null;
#endregion

#region private parameters
        private class PropInBag
        {
            public PD.PropType p_propType;
            public int p_cnt;

            public PropInBag(PD.PropType propType)
            {
                p_propType = propType;
                p_cnt = 1;
            }
        }

        private Dictionary<PD.PropType, PropInBag> p_props;
        private Prop _curProp; //一个时间内只能获取一个道具

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(this);
            p_props =  new Dictionary<PD.PropType, PropInBag>();
            _curProp = null;
        }

#endregion

#region public functions
        //获取到一个新的道具
        public void ReceiveProp(PD.PropType propType)
        {
            if(p_props.ContainsKey(propType) == true)
                p_props[propType].p_cnt++;
            else
                p_props.Add(propType, new PropInBag(propType));
        }

        //从背包中获取到一个道具
        public Prop GetPropFromBag(PD.PropType propType)
        {
            if (_curProp != null)
            {
                GameLogger.LogError($"Already has prop in use {_curProp.gs_conf.gs_type}, can not get new one");
                return null;
            }
            if(p_props.ContainsKey(propType) == false)
                return null;
            if (p_props[propType].p_cnt <= 0)
            {
                p_props[propType].p_cnt = 0;
                return null;
            }
            _curProp = (Prop)Activator.CreateInstance(PropCtrl.Instance.GetPropClassByType(propType));
            p_props[propType].p_cnt--;
            _curProp.ActiveProp(); //从背包取出就立刻激活
            return _curProp;
        }

        //当前道具通知放弃使用
        public void InfoRetriveCurrentProp()
        {
            if (_curProp == null)
            {
                GameLogger.LogError($"Currently not has prop in use !!!");
                return;
            }

            var type = _curProp.gs_conf.gs_type;
            if (p_props.ContainsKey(type) == false)
            {
                p_props.Add(type, new PropInBag(type));
                return;
            }
            p_props[type].p_cnt++;
            _curProp = null; //之前的_curProp进入GC
        }

        //当前道具通知已经被使用
        public void InfoConsumeCurrentProp()
        {
            _curProp = null; //之前的_curProp进入GC
            return;
        }
#endregion

#region private functions

#endregion
    }
}

