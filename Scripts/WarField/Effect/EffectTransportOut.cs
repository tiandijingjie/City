using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    public class EffectTransportOut : EffectBase
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.TRANSPORTOUT; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            _effectType = ED.EffectType.TRANSPORTOUT;
            base.Awake();
        }
#endregion

#region public functions

#endregion

#region private functions

#endregion
    }
}

