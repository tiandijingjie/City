using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace  WarLevelChooseUI
{
    using GD = GlobalDefines;
    
    public class LevelChooseIcon : MonoBehaviour
    {
#region public parameters
        
#endregion

#region private parameters

        private List<LevelChooseLine> _inLines, _outLines;
        private RectTransform _rectTransform;
        
        private Image _iconBg; //back ground
        private Image _reward;
        private bool _hasPassed;
        private Button _chooseBt;
#endregion

#region private parameters' get set

        public bool gs_hasPassed
        {
            get { return _hasPassed; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _inLines = new List<LevelChooseLine>();
            _outLines = new List<LevelChooseLine>();
            _iconBg = transform.Find("Background").GetComponent<Image>();
            _reward = transform.Find("Reward").GetComponent<Image>();
            _hasPassed = false;
            _chooseBt = GetComponent<Button>();
            _chooseBt.interactable = false;
            CheckButtonInteractable();
            SetImage();
        }
        
        public void ButtonEvent()
        {
            _hasPassed = true;
            SetImage();
            foreach (var line in _outLines)
            {
                line.UpperLevelPassed();
            }
        }

#endregion

#region public functions

        public void RegisterLine(LevelChooseLine line, GD.DirDef dir)
        {
            if (dir == GD.DirDef.UDir)
            {
                _inLines.Add(line);
                CheckButtonInteractable();
            }
            else
                _outLines.Add(line);
        }

        public bool IsPosIn(Vector2 screenPos)
        {
            bool isInside = RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, screenPos, null);
            return isInside;
        }

        public void UpperLevelPassed()
        {
            CheckButtonInteractable();
        }
#endregion

#region private functions

        private void CheckButtonInteractable()
        {
            foreach (var line in _inLines)
            {
                if (line.gs_upperLevelHasPassed == false)
                {
                    _chooseBt.interactable = false;  //as line register is slow, 
                    return;
                }
            }

            _chooseBt.interactable = true;
        }
        
        private void SetImage()
        {
            if (_hasPassed == true)
            {
                _iconBg.color = Color.white;
                if(_reward != null)
                    _reward.gameObject.SetActive(false);
            }
            else
            {
                _iconBg.color = Color.gray;
                if(_reward != null)
                    _reward.gameObject.SetActive(true);
            }
        }
#endregion
    }
}

