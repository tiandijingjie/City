using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //自爆特效
    public class EffectSelfExplosion : EffectBase
    {
#region public parameters

#endregion

#region private parameters
        private float _scale = 1; //原始动画x长度
#endregion

#region private parameters' get set

        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.SELFEXPLOSION; }
        }

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.SELFEXPLOSION;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions

        protected override void OnActiveEffect(object value = null)
        {
            float scale = (float)value;
            if (_scale != scale) //增长火焰的释放长度
            {
                _scale = scale;
                _transform.localScale = new Vector3(_scale, _scale, 1);
            }
            _spineAnim.AnimationState.SetAnimation(0, "animation", false);
        }

#endregion
    }

}
