using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    public class EffectTransportIn : EffectBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.TRANSPORTIN; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            _effectType = ED.EffectType.TRANSPORTIN;
            base.Awake();
        }

#endregion

#region public functions

#endregion

#region private functions

#endregion
    }
}

