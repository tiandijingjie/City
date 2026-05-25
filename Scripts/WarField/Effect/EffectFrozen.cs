using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //冰冻
    public class EffectFrozen : EffectBase
    {
#region public parameters

#endregion

#region private parameters

        private float _height;
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.FROZEN; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.FROZEN;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override void OnActiveEffect(object value = null)
        {
            float height = (float)value;
            if (_height != height) //需要冰冻的高度
            {
                _height = height;
                _transform.localScale = new Vector3(_height, _height, 1);
            }
            _spineAnim.AnimationState.SetAnimation(0, "animation", false);
        }
#endregion
    }
}
