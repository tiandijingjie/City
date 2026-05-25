using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarLevelChooseUI
{
    public class LevelChooseUI : MonoBehaviour
    {
#region public parameters

        public static LevelChooseUI Instance = null;
#endregion

#region private parameters

        [SerializeField] private Transform _panel;
        
        private List<LevelChooseIcon> _icons;
        private List<LevelChooseLine> _lines;
        
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
            }

            Instance = this;
            
            _icons = new List<LevelChooseIcon>();
            _lines = new List<LevelChooseLine>();
            
            int cnt = _panel.childCount;
            for (int i = 0; i < cnt; i++)
            {
                var child = _panel.GetChild(i);
                if (child.GetComponent<LevelChooseIcon>() != null)
                {
                    _icons.Add(child.GetComponent<LevelChooseIcon>());
                }
                else if(child.GetComponent<LevelChooseLine>() != null)
                {
                    _lines.Add(child.GetComponent<LevelChooseLine>());
                }
            }

            foreach (var line in _lines)
            {
                var ret =  line.RegisterLineToIcon();
                if(ret == false)
                    Debug.Log("line connection error!!!");
            }
        }

#endregion

#region public functions

        public LevelChooseIcon GetLevelChooseIconPosIn(Vector2 pos)
        {
            int cnt = _icons.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (_icons[i].IsPosIn(pos) == true)
                    return _icons[i];
            }

            return null;
        }
#endregion

#region private functions

#endregion
    }
}

