using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    public class UITip : MonoBehaviour
    {
#region public parameters
        static public UITip Instance = null;
#endregion

#region private parameters
        [SerializeField] private Text _text;
        [SerializeField] private RectTransform _rectTransform;

        private object _lock;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _lock = new object();
        }

#endregion

#region public functions

        public void ShowTip(string text, Vector2 pos)
        {
            float pivotx = 0, pivoty = 0;
            if (pos.x < Screen.width * 0.3f)
                pivotx = 0.2f;
            else if (pos.x > Screen.width * 0.7f)
                pivotx = 0.8f;
            else
                pivotx = 0.5f;
            
            if(pos.y > Screen.height * 0.7f)
                pivoty = 1;
            _rectTransform.pivot = new Vector2(pivotx, pivoty);
            _rectTransform.position = pos;
            if (_text.text != text)
            {
                _text.text = text;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);
            }
        }

        public void HideTip()
        {
            _rectTransform.position = new Vector3(0, -10000, 0);
        }
#endregion

#region private functions

#endregion
    }
}

