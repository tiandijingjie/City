using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    //单体效果的释放指示，直接修改鼠标图片
    public class SingleEffectIndicator : EffectIndicator
    {
#region public parameters

#endregion

#region private parameters
        private Vector2 _prvMousePos;
        private Vector2 _curCenter;

        private Vector2 _hotSpot =  Vector2.zero;//光标实际位置在图片中的相对位置
        private string _cursorTexPath;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions
        public bool SetSingleEffectIndicator(string indicatorPath)
        {
            _cursorTexPath = indicatorPath;
            return true;
        }
#endregion

#region private functions

        protected override bool OnActive()
        {
            if (string.IsNullOrEmpty(_cursorTexPath) == true)
            {
                GameLogger.LogError("Not set texture path");
                return false;
            }

            Texture2D tex = Resources.Load<Texture2D>(_cursorTexPath);
            if(tex == null)
                return false;
            Cursor.SetCursor(tex, _hotSpot, CursorMode.Auto); //设置鼠标图片

            _prvMousePos = Input.mousePosition;
            Vector2 mousePos = _mainCamera.ScreenToWorldPoint(_prvMousePos);
            _curCenter = mousePos; //显示在最上层,不用设置z

            _transform.position = _curCenter;
            return true;
        }

        protected override void OnUpdate()
        {
            Vector2 delta = _prvMousePos - (Vector2)Input.mousePosition;
            if(Mathf.Abs(delta.x) < 5f && Mathf.Abs(delta.y) < 5f) //鼠标移动距离太短
                return;

            _prvMousePos = Input.mousePosition;
            Vector2 mousePos = _mainCamera.ScreenToWorldPoint(_prvMousePos);
            _curCenter = mousePos;

            _transform.position = _curCenter;
            ((SingleEffectIndicatorCb)_scrIndicatorCb).CheckPosition(_curCenter);
        }

        protected override void OnMouseRightDown()
        {
            ((SingleEffectIndicatorCb)_scrIndicatorCb).TriggerEffect(_curCenter);
        }

        protected override void OnDeactive()
        {
            _cursorTexPath = "";
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

#endregion
    }
}
