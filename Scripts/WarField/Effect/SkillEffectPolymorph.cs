using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //变形术的效果
    public class SkillEffectPolymorph : SkillEffect
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private SpriteRenderer _renderer;
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.POLYMORPH; }
        }
#endregion

#region Unity callbacks

        protected override void Awake()
        {
            _effectType = ED.EffectType.POLYMORPH;
            base.Awake();
            _renderer.enabled = false;
        }

#endregion

#region public functions

#endregion

#region private functions

        protected override void OnActiveEffect(object value = null)
        {
            _renderer.enabled = true;
        }

        protected override void OnDeactiveEffect()
        {
            _renderer.enabled = false;
        }

#endregion
    }
}

