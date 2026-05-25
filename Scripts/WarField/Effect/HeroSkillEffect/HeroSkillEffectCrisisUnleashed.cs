using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    //危机降临
    public class HeroSkillEffectCrisisUnleashed : EffectBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.HEROCRISISUNLEASHED; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.HEROCRISISUNLEASHED;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions

#endregion
    }
}

