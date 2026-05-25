using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;

    public abstract class CardEffection
    {
#region public parameters

#endregion

#region private parameters

        protected bool _beInit; //初始化之后就会被加入pool
        protected CD.CardCategory _category;
        protected int[] _levelCnt;//每个等级卡的数量 CardDefines.CardLevel.MAX
#endregion

#region private parameters' get set

        public CD.CardCategory gs_category
        {
            get { return _category; }
        }

        public int[] gs_levelCnt
        {
            get { return _levelCnt; }
        }

#endregion

#region public functions

        public bool InitCardEffection()
        {
            if(_beInit == true)
                return false;
            if (CanBeInit() == false)
            {
                GameLogger.LogError("Precondition not active, should not init this case");
                return false;
            }
            _beInit = true;
            return true;
        }

        //forDebug: 调试使用，忽略初始化条件
        //decrease:card减少,对于property类型的card == false
        public bool TakeAndActive(CD.CardLevel level, bool decrease, bool forDebug = false)
        {
            if(_beInit == false && forDebug == false)
                return false;

            if (_levelCnt[(int)level] == 0)
            {
                GameLogger.LogError($"Card of level {level} all consumed, can not take effect anymore");
                return false;
            }
            if(decrease == true)
                _levelCnt[(int)level] -= 1;
            return OnTakeEffect(level);
        }

        public abstract bool CanBeInit();
#endregion

#region private functions
        //take actually effection
        protected abstract bool OnTakeEffect(CD.CardLevel level);

#endregion
    }

    //CardEffection派生出来的泛型基类，用来管理Instance
    public abstract class CardEffection<T> : CardEffection where T : CardEffection<T>
    {
        public static T Instance { get; private set; }

        protected CardEffection()
        {
            if(Instance == null)
                Instance = (T)this;
            else
            {
                GameLogger.LogError($"Instance of {typeof(T).Name} already exists!");
                throw new InvalidOperationException($"Instance of {typeof(T).Name} already exists!");
            }
        }
    }
}

