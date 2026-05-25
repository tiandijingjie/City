using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    //范围型技能的释放位置指示
    public class AreaEffectIndicator : EffectIndicator
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private SpriteRenderer _centerSprite;

        private Transform _srcPos;
        private float _maxDistance; //最远距离的平方
        private float _maxDistanceSqrt; //最远距离
        private float _radius; //半径
        private Vector2 _curCenter;

        private SpriteRenderer _bgSprite;
        private string _centerTexPath = "";
        private bool _isTexForCursor; //_centerTexPath是修改光标还是生成一个跟随光标的图片

        private Color _radarColor, _bgColor, _centerColor;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            _bgSprite = GetComponent<SpriteRenderer>();
            _radarSprite.sprite = Resources.Load<Sprite>("Textures/EffectTex/EffectIndicator/AreaEffect");
        }
#endregion

#region public functions

        //changeCursor:是否是修改光标的图片
        public bool SetCenterTex(string indicatorPath, bool isChangeCursor)
        {
            _centerTexPath = indicatorPath;
            _isTexForCursor = isChangeCursor;
            _centerColor = Color.white;
            return true;
        }

        //radarSpeed: radar scan speed, e.g. 2表示扫描1s完成两次扫描
        //size : 对于area是 (最远距离,覆盖半径)
        public bool SetAreaEffectIndicator(Color raderColor, float radarSpeed, Transform srcTransform, Vector2 size)
        {
            _maxDistanceSqrt = size.x;
            _maxDistance = size.x * size.x;
            _radius = size.y;

            //因为spriterender draw mode是sliced(2,2), 所以不用考虑图片本身大小, (2,2)是因为考虑到size.y是半径
            _transform.localScale = new Vector3(_radius, _radius, 1);
            _radarMat.SetColor("_Color", raderColor);
            _radarMat.SetFloat("_Speed", radarSpeed);
            _radarMat.SetFloat("_isLoop", 1f);
            _radarSprite.SetPropertyBlock(_radarMat);
            _srcPos = srcTransform;
            _radarColor = raderColor;

            return true;
        }

        //设置背景图，也可以没有
        public void SetAreaEffectIndicatorBg(string path, Color color)
        {
            _bgSprite.sprite = Resources.Load<Sprite>(path);
            _bgSprite.color = color;
            _bgColor = color;
        }

        protected override bool OnActive()
        {
            if (string.IsNullOrEmpty(_centerTexPath) == false)
            {
                if (_isTexForCursor == false)
                {
                    _centerSprite.enabled = true;
                    _centerSprite.sprite = Resources.Load<Sprite>(_centerTexPath);
                }
                else
                {
                    Texture2D tex = Resources.Load<Texture2D>(_centerTexPath);
                    if (tex == null)
                        return false;
                    Cursor.SetCursor(tex, new Vector2(tex.width * 0.5f, tex.height * 0.5f), CursorMode.Auto); //设置鼠标图片
                }
            }

            Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            if (_maxDistanceSqrt > 0)
            {
                float distance = (mousePos - (Vector2)_srcPos.position).sqrMagnitude;
                if (distance > _maxDistance)
                {
                    float sqrtDis = Mathf.Sqrt(distance);
                    float t = _maxDistanceSqrt / sqrtDis;
                    _curCenter = Vector2.Lerp(_srcPos.position, mousePos, t);
                }
                else
                    _curCenter = mousePos; //显示在最上层,不用设置z
            }
            else //无限距离
                _curCenter = mousePos; //显示在最上层,不用设置z

            _transform.position = _curCenter;
            return true;
        }

        //激活时设置颜色
        public void SetBgColor(Color color)
        {
            _bgSprite.color = color;
        }

        public void SetRadarColor(Color color)
        {
            _radarMat.SetColor("_Color", color);
            _radarSprite.SetPropertyBlock(_radarMat);
        }

        public void SetCenterColor(Color color)
        {
            if(_isTexForCursor == true) //光标无法改变颜色
                return;
            _centerSprite.color = color;
        }

        public void ResetBgColor()
        {
            _bgSprite.color = _bgColor;
        }

        public void ResetRadarColor()
        {
            _radarMat.SetColor("_Color", _radarColor);
            _radarSprite.SetPropertyBlock(_radarMat);
        }

        public void ResetCenterColor()
        {
            if(_isTexForCursor == true) //光标无法改变颜色
                return;
            _centerSprite.color = _centerColor;
        }
#endregion

#region private functions

        protected override void OnUpdate()
        {
            Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            if (_maxDistanceSqrt > 0)
            {
                float distance = (mousePos - (Vector2)_srcPos.position).sqrMagnitude;
                if (distance > _maxDistance)
                {
                    float sqrtDis = Mathf.Sqrt(distance);
                    float t = _maxDistanceSqrt / sqrtDis;
                    _curCenter = Vector2.Lerp(_srcPos.position, mousePos, t);
                }
                else
                    _curCenter = mousePos; //显示在最上层,不用设置z
            }
            else  //无限距离
                _curCenter = mousePos;

            _transform.position = _curCenter;
            ((AreaEffectIndicatorCb)_scrIndicatorCb).CheckPosition(_curCenter);
        }

        protected override void OnMouseRightDown()
        {
            ((AreaEffectIndicatorCb)_scrIndicatorCb).TriggerEffect(_curCenter);
        }

        protected override void OnDeactive()
        {
            _bgSprite.sprite = null;
            _bgSprite.color = Color.white;
            _centerSprite.enabled = false;
            _centerTexPath = "";
            ResetBgColor();
            ResetCenterColor();
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

#endregion
    }
}

