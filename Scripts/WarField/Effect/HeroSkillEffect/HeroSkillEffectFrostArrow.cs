using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //寒冰箭
    public class HeroSkillEffectFrostArrow : EffectBase
    {
#region public parameters

#endregion

#region private parameters
        private float _frostLength = 3.8f; //原始动画x长度
        private float _frostHight = 1f; //原始动画y高度
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.HEROFROSTARROW; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.HEROFROSTARROW;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions

        protected override void OnActiveEffect(object value = null)
        {
            Vector2 size = (Vector2)value;
            float len = size.x;
            float height = size.y;
            if (Mathf.Abs(len - _frostLength) > 1) //增长火焰的释放长度
            {
                float scaleX = len / _frostLength;
                float scaleY = height / _frostHight;
                _transform.localScale = new Vector3(scaleX, scaleY, 1);
            }
            _spineAnim.AnimationState.SetAnimation(0, "animation", false);
        }

#endregion
    }
}

