using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Entrance
{
    public class PopOutWindow : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        protected bool _isShowed = false;
        protected bool _beInited = false;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public virtual void InitPopOutWindow(MonoBehaviour parentUI)
        {
            if(_beInited == true)
                return;

            _isShowed = false;
            _beInited = true;
        }

        public virtual void ShowWindow()
        {

        }

        public virtual void CloseWindow()
        {

        }
#endregion

#region private functions

#endregion
    }
}
