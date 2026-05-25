using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace WarField
{
	using UD = UIDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //只负责画框
    public class UiDrawSelectionBox : MonoBehaviour, ITask
    {
#region public parameters

        public static UiDrawSelectionBox Instance;

#endregion

#region private parameters

        private Vector2 _startPos;
        private Image _selectionBox;
        private bool _isDrawing = false;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject); return;
            }
            Instance = this;
            _selectionBox = gameObject.GetComponent<Image>();
        }

        private void Start()
        {
            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
            StopDraw(); // 初始挂起
        }

#endregion

#region public functions

        public void StartDraw(Vector2 startMousePos)
        {
            _startPos = startMousePos;
            _isDrawing = true;
            _selectionBox.enabled = true;
            // 激活每帧拉伸的轮询任务
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
        }

        public void StopDraw()
        {
            _isDrawing = false;
            if (_selectionBox != null) _selectionBox.enabled = false;
            WarFieldGameManager.Instance.SuspendTask(this, WE.TaskType.NORMAL);
        }

        // 每帧无脑拉伸方框
        public void RunNormalTask(float deltaTime)
        {
            if (!_isDrawing) return;

            Vector2 currentMousePos = Input.mousePosition;
            var height = Mathf.Abs(_startPos.y - currentMousePos.y);
            var width = Mathf.Abs(_startPos.x - currentMousePos.x);

            _selectionBox.rectTransform.position = (_startPos + currentMousePos) / 2;
            _selectionBox.rectTransform.sizeDelta = new Vector2(width, height);
        }

        public void RunFixTask(float deltaTime)
        {
            throw new System.NotImplementedException();
        }
#endregion

#region private functions

#endregion
    }
}

