using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WRD = WarResDefine;

    public class PickableResBase : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        protected SpriteRenderer _renderer;
        protected Transform _transform;
        protected int _mapId;
        protected bool _isTimeout = false;
        protected int _amount;
        protected WRD.ResContainLevel _level;

#endregion

#region private parameters' get set

        // 记录它在 ResourceGrid 内存池中的索引
        public int gs_entityIndex { get; set; }
        public bool gs_isValid { get; set; }

        public Vector2 gs_position
        {
            get { return _transform.position; }
        }

        public int gs_amount
        {
            get { return _amount; }
        }

#endregion

#region Unity callbacks
        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _transform = GetComponent<Transform>();
        }
#endregion

#region public functions

        public virtual void InitPickableResBase(WRD.ResContainLevel level, Vector2 pos, int mapId, int amount)
        {
            gs_entityIndex = -1;
            gs_isValid = true;

            _level = level;
            _isTimeout = false;
            _mapId = mapId;
            _amount = amount;
        }

        public virtual void PickUp() { }
        public virtual void TimeOut() { }
#endregion

#region private functions

#endregion
    }
}

