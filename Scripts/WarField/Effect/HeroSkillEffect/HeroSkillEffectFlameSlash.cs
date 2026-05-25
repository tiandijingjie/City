using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //火焰斩
    public class HeroSkillEffectFlameSlash : EffectBase
    {
#region public parameters

#endregion

#region private parameters
        private float _slashLength = 4; //原始动画x长度
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.HEROFLAMESLASH; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.HEROFLAMESLASH;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions

        protected override void OnActiveEffect(object value = null)
        {
            float len = (float)value;
            if (Mathf.Abs(len - _slashLength) > 1) //增长火焰的释放长度
            {
                float scale = len / _slashLength;
                _transform.localScale = new Vector3(scale, 1, 1);
            }
            _spineAnim.AnimationState.SetAnimation(0, "animation", false);
        }

#endregion
    }
}

