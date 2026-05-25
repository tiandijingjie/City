using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;

    //记录一种资源的总存储
    public class WarResInStoreBase
    {
#region public parameters

#endregion

#region private parameters
        protected WarResDefine.ResTypes _resTypes;
        protected int _total;

        protected System.Object _lock;
        protected List<IWarResListener> _listeners;
#endregion

#region private parameters' get set

        public int gs_total
        {
            get { return _total; }
        }
#endregion

#region public functions
        //initCnt：起始数量
        public WarResInStoreBase(int initCnt)
        {
            _total = initCnt;
            _lock = new object();
            _listeners = new List<IWarResListener>();
        }

        public void RegisterListener(IWarResListener listener)
        {
            if(_listeners.Contains(listener) == false)
                _listeners.Add(listener);
        }

        public void RemoveListener(IWarResListener listener)
        {
            _listeners.Remove(listener);
        }

        public void AmountChange(int value)
        {
            if(value == 0)
                return;

            lock (_lock)
            {
                _total += value;
                for(int i = _listeners.Count - 1; i >= 0; i--)
                    _listeners[i].ResChange(_resTypes, value, _total);
            }
        }

#endregion

#region private functions

#endregion
    }
}

