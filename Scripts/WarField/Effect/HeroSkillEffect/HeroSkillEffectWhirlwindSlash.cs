using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    public class HeroSkillEffectWhirlwindSlash : EffectBase
    {
#region public parameters

#endregion

#region private parameters

        public float _slashRadius = 1; //原始的动画半径

#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.HEROWHIRLWINDSLASH; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.HEROWHIRLWINDSLASH;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override void OnActiveEffect(object value = null)
        {
            float radius = (float)value;
            if (Mathf.Abs(radius - _slashRadius) > 1) //增长火焰的释放长度
            {
                float scale = radius / _slashRadius;
                _transform.localScale = new Vector3(scale, scale, 1);
            }
            _spineAnim.AnimationState.SetAnimation(0, "animation", false);
        }
#endregion
    }
}

