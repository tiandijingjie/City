using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //风雨交加
    public class HeroSkillEffectStormFury : EffectBase
    {
#region public parameters

#endregion

#region private parameters

        private float _radius;
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.HEROSTORMFURY; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.HEROSTORMFURY;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override void OnActiveEffect(object value = null)
        {
            float radius = (float)value;
            _curPos.z -= radius; //以下边缘来计算z
            _transform.position = _curPos;
            if (_radius != radius) //覆盖半径,默认是1
            {
                _radius = radius;
                _transform.localScale = new Vector3(_radius, _radius, 1);
            }
            _spineAnim.AnimationState.SetAnimation(0, "animation", false);
        }
#endregion
    }
}
