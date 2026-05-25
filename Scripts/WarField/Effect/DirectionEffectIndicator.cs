using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    //直线型效果的释放方向指示
    public class DirectionEffectIndicator : EffectIndicator
    {
#region public parameters

#endregion

#region private parameters

        private Transform _srcPos;
        private float _angle;
        private Vector2 _prvMousePos;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _radarSprite.sprite = Resources.Load<Sprite>("Textures/EffectTex/EffectIndicator/DirectionEffect");
        }

#endregion

#region public functions
        //radarSpeed: radar scan speed, e.g. 2表示扫描1s完成两次扫描
        //sise:对于diraction是 (x长度,y长度)
        public bool SetDirectionEffectIndicator(Color raderColor, float radarSpeed, Transform srcTransform, Vector2 size)
        {
            _transform.localScale = new Vector3(size.x, size.y, 1); //因为spriterender draw mode是sliced(1,1), 所以不用考虑图片本身大小
            _radarMat.SetColor("_Color", raderColor);
            _radarMat.SetFloat("_Speed", radarSpeed);
            _radarMat.SetFloat("_isLoop", 1f);
            _radarSprite.SetPropertyBlock(_radarMat);
            _srcPos = srcTransform;
            _transform.position = _srcPos.position; //显示在最上层,不用设置z,  同时将使用的图片设置pivot为左边中点

            _prvMousePos = Input.mousePosition;
            Vector2 mousePos = _mainCamera.ScreenToWorldPoint(_prvMousePos);
            _angle = Utils.AngleBetweenVector(mousePos, _srcPos.position, Vector2.right);
            _transform.rotation = Quaternion.Euler(0, 0, _angle);
            return true;
        }
#endregion

#region private functions

        protected override void OnUpdate()
        {
            if(_srcPos.hasChanged == true)
                _transform.position = _srcPos.position;
            Vector2 delta = _prvMousePos - (Vector2)_srcPos.position;
            if(Mathf.Abs(delta.x) < 10f && Mathf.Abs(delta.y) < 5f) //鼠标移动距离太短
                return;

            _prvMousePos = Input.mousePosition;
            Vector2 mousePos = _mainCamera.ScreenToWorldPoint(_prvMousePos);
            _angle = Utils.AngleBetweenVector(mousePos, _srcPos.position, Vector2.right);
            _transform.rotation = Quaternion.Euler(0, 0, _angle);
        }

        protected override void OnMouseRightDown()
        {
            ((DirectionEffectIndicatorCb)_scrIndicatorCb).TriggerEffect(_angle);
        }

#endregion
    }
}


