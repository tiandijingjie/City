using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarLevelChooseUI
{
    using GD = GlobalDefines;
    
    public class LevelChooseLine : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        private Vector2 _left, _right;
        private LevelChooseIcon _upperIcon, _bottomIcon;
        private bool _upperLevelHasPassed;
        private List<Image> _bgImage;
#endregion

#region private parameters' get set

        public bool gs_upperLevelHasPassed
        {
            get { return _upperLevelHasPassed; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _bgImage = new List<Image>();
            int cnt = transform.childCount;
            for (int i = 0; i < cnt; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == "Left")
                {
                    _left = child.position;
                }
                else if (child.name == "Right")
                {
                    _right = child.position;
                }
                else
                {
                    Image image = child.GetComponent<Image>();
                    if(image != null)
                        _bgImage.Add(image);
                }
            }
            SetColor();
            _upperLevelHasPassed = false;
        }

        public void UpperLevelPassed()
        {
            _upperLevelHasPassed = true;
            SetColor();
            _bottomIcon.UpperLevelPassed();
        }
        
        
#endregion

#region public functions

        public bool RegisterLineToIcon()
        {
            _upperIcon = LevelChooseUI.Instance.GetLevelChooseIconPosIn(_left);
            _bottomIcon = LevelChooseUI.Instance.GetLevelChooseIconPosIn(_right);
            if (_upperIcon == null || _bottomIcon == null)
            {
                return false;
            }

            _upperIcon.RegisterLine(this, GD.DirDef.DDir);
            _bottomIcon.RegisterLine(this, GD.DirDef.UDir);
            return true;
        }
#endregion

#region private functions

        private void SetColor()
        {
            if (_upperLevelHasPassed == true)
            {
                Color color = Color.white;
                int cnt = _bgImage.Count;
                for (int i = 0; i < cnt; i++)
                {
                    _bgImage[i].color = color;
                }
            }
            else
            {
                Color color = Color.gray;
                int cnt = _bgImage.Count;
                for (int i = 0; i < cnt; i++)
                {
                    _bgImage[i].color = color;
                }
            }
        }
#endregion
    }
}
