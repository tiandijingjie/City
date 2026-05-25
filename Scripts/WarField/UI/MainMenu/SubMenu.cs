using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    public class SubMenu : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        protected bool _isShowed = false;
        protected bool _beInied;
        private CanvasGroup _canvasGroup;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected virtual void Awake()
        {
            _beInied = false;
        }
        
#endregion

#region public functions

        public bool InitSubMenu()
        {
            if (_beInied == false)
            {
                OnInit();
                _canvasGroup = gameObject.GetComponent<CanvasGroup>();
                _beInied = true;
            }
            
            return _beInied;
        }
        
        public void ShowMenu()
        {
            OnShowMenu();
            _isShowed = true;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        public void HideMenu()
        {
            OnHideMenu();
            _isShowed = false;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
#endregion

#region private functions
        virtual protected void OnInit(){}
        virtual protected void OnShowMenu(){}
        virtual protected void OnHideMenu(){}
#endregion
    }
}

