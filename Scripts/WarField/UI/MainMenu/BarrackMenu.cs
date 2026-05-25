using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SD = SoldierDefines;
    
    public class BarrackMenu : SubMenu
    {
#region public parameters

#endregion

#region private parameters
        
        [SerializeField] private UIBarrackSection[] _sections;
        
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

#endregion

#region private functions

        protected override void OnInit()
        {
            for (int i = 0; i < _sections.Length; i++)
            {
                if (ReferenceEquals(_sections[i], null) == false)
                    _sections[i].Init();
            }
        }

        protected override void OnShowMenu()
        {
            for (int i = 0; i < _sections.Length; i++)
            {
                if (ReferenceEquals(_sections[i], null) == false)
                    _sections[i].ShowMenu();
            }
        }

        protected override void OnHideMenu()
        {
            for (int i = 0; i < _sections.Length; i++)
            {
                if (ReferenceEquals(_sections[i], null) == false)
                    _sections[i].HideMenu();
            }
        }

#endregion
    }
}

