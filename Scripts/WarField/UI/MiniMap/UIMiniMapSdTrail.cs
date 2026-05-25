using System;
using System.Collections;
using System.Collections.Generic;
using UI_Spline_Renderer;
using UnityEngine;
using UnityEngine.Splines;

namespace WarField
{
    public class UIMiniMapSdTrail : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        private Spline _spline;
        private BezierKnot[] _knots;
        private RectTransform _splineRect;
        private UISplineRenderer _splineRenderer;

        private bool _isVirtualTrail; //传送门之间的trail是virtual的
        private bool _isActive;
#endregion

#region private parameters' get set

        public bool gs_isVirtualTrail
        {
            get { return _isVirtualTrail; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            SplineContainer container = GetComponent<SplineContainer>();
            _spline = container.Splines[0];
            _splineRect = GetComponent<RectTransform>();
            _knots = new BezierKnot[2];
            for (int i = 0; i < 2; i++)
            {
                _knots[i] = _spline[i];
            }

            _splineRenderer = GetComponent<UISplineRenderer>();
        }

        private void Update()
        {
            if(_isActive)
                _splineRenderer.uvOffset += new Vector2(0, Time.deltaTime * 2);
        }

#endregion

#region public functions

        //from/to are screen position
        public void SetTrail(Vector2 from, Vector2 to, bool isVirtual)
        {
            _isVirtualTrail = isVirtual;
            Vector2 localPoint = _splineRect.InverseTransformPoint(from);
            //RectTransformUtility.ScreenPointToLocalPointInRectangle(_splineRect, from, null, out var localPoint);
            _knots[0].Position = new Vector3(localPoint.x, localPoint.y, 0);
            localPoint = _splineRect.InverseTransformPoint(to);
            //RectTransformUtility.ScreenPointToLocalPointInRectangle(_splineRect, to, null, out localPoint);
            _knots[1].Position = new Vector3(localPoint.x, localPoint.y, 0);
            _spline.SetKnot(0, _knots[0]);
            _spline.SetKnot(1, _knots[1]);
            _splineRenderer.color = _isVirtualTrail ? Color.white : Color.red;
        }

        public void SetActive(bool active)
        {
            _isActive = active;
        }

#endregion

#region private functions

#endregion
    }
}

