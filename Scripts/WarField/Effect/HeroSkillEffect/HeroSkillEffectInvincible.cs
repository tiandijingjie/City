using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //无敌
    public class HeroSkillEffectInvincible : EffectBase
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected SpriteRenderer _shellSprite;

        protected MaterialPropertyBlock _shellMat; //设置shell的材质

#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.HEROINVINCIBLE; }
        }
#endregion

#region Unity callbacks

        protected override void Awake()
        {
            _effectType = ED.EffectType.HEROINVINCIBLE;
            base.Awake();
            _shellMat = new MaterialPropertyBlock();
            _shellSprite.GetPropertyBlock(_shellMat);
            _effectCanMove = true;
        }

#endregion

#region public functions

#endregion

#region private functions

#endregion
    }
}

