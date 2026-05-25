using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace WarField
{
    using UD = UIDefines;
    using WE = WarFieldElements;

    // 🔥 扩展接口：实现 IoObserverIntf 消费方向键事件，实现 IoGroupChangeIntf 顺从 UiIoTask 的独占启闭控制
    public class CameraCtrl : MonoBehaviour, IoObserverIntf, IoGroupChangeIntf
    {
#region public parameters
        public static CameraCtrl Instance = null;
#endregion

#region private parameters
        [Header("Camera Control Configurations")]
        [SerializeField] private float _moveSpeed = 20f;              // 相机方向键平移速度
        [SerializeField] private float _zoomSpeed = 4f;              // 滚轮缩放感应灵敏度
        [SerializeField] private float _minOrthographicSize = 3f;    // 允许拉得最近的视野极限（防无限趋近0或反转）

        private bool _isRotate;
        private float _rotateTime = 0.2f;

        private Camera _mainCamera;
        private byte _curMapId;

        private IoObserver _ioObserver;
        private Vector2 _moveInput = Vector2.zero; // 方向键连续按压状态合成向量
        private bool _isIoEnabled = true;          // 受自 UiIoTask 支配的绝对开关标识
#endregion

#region private parameters' get set
        public byte gs_curMapId
        {
            get { return _curMapId; }
        }
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

            {
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                int width = 1920;
                int height = 1080;
                Screen.SetResolution(width, height, Screen.fullScreenMode);
            }

            _curMapId = WE.OnGroundMapIndex;
            _mainCamera = Camera.main;

            if (_mainCamera != null)
            {
                float height = _mainCamera.orthographicSize * 2f;
                float width = height * _mainCamera.aspect;
                _mainCamera.transform.position = new Vector3(width / 2f, 0f, _mainCamera.transform.position.z);
            }
        }

        private void LateUpdate()
        {
            // 铁律：一旦该组被 UiIoTask 剥夺了 Ownership 独占权，心跳控制链彻底熔断！
            if (!_isIoEnabled || _mainCamera == null)
                return;

            // 鼠标滚轮缩放
            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollDelta) > 0.001f)
            {
                HandleCameraZoom(scrollDelta);
            }

            // 方向键平移
            if (_moveInput.sqrMagnitude > 0.001f)
            {
                HandleCameraMovement();
            }
        }
#endregion

#region public functions

        public void InitCameraCtrl()
        {
            // 将自身的 IO 标识变更为相机专用的正统组（假设系统定义中包含 CAMERA 组）
            _ioObserver = new IoObserver(UD.UIEventGroupType.CAMERA);

            // 注册上下左右方向键的 KEYDOWN 与 KEYUP 双向联动状态监听
            RegisterCameraKey(KeyCode.UpArrow, "CamMoveUp");
            RegisterCameraKey(KeyCode.DownArrow, "CamMoveDown");
            RegisterCameraKey(KeyCode.LeftArrow, "CamMoveLeft");
            RegisterCameraKey(KeyCode.RightArrow, "CamMoveRight");

            // 监听IO抢占的情况,如果IO被抢占就不能够移动或者缩放camera
            UiIoTask.Instance.RegisterGroupStatusListener(UD.UIEventGroupType.CAMERA, this);
            _isIoEnabled = true;
        }

        public Camera CreateCamera(string camName, Vector2 pos, bool enable, Transform parent, RenderTexture targetTex = null)
        {
            GameObject camObj = new GameObject(camName);
            if (ReferenceEquals(parent, null) == false)
                camObj.transform.SetParent(parent);

            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = _mainCamera.fieldOfView;
            cam.nearClipPlane = _mainCamera.nearClipPlane;
            cam.farClipPlane = _mainCamera.farClipPlane;
            cam.clearFlags = _mainCamera.clearFlags;
            cam.backgroundColor = _mainCamera.backgroundColor;
            cam.cullingMask = _mainCamera.cullingMask;
            cam.orthographic = _mainCamera.orthographic;
            cam.orthographicSize = _mainCamera.orthographicSize;

            Vector2 leftCenter = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, 10));
            Vector2 offset = pos - leftCenter;
            Vector3 camPos = offset + (Vector2)cam.transform.position;
            camPos.z = -10;
            cam.transform.position = camPos;

            if (ReferenceEquals(targetTex, null) == false)
                cam.targetTexture = targetTex;

            cam.enabled = enable;
            return cam;
        }

        // 聚集main camera 到一个位置
        public void FocusCameraOnPosition(Vector2 targetPos, byte mapId)
        {
            _curMapId = mapId; // 动态刷新相机当前所属的副本地图空间

            PathFinderMap pathMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(_curMapId);
            if (pathMap == null || _mainCamera == null) return;

            Vector3 currentCamPos = _mainCamera.transform.position;
            // 瞬间切入目标点中心
            _mainCamera.transform.position = new Vector3(targetPos.x, targetPos.y, currentCamPos.z);

            // 强行施加物理反向边界夹逼，绝不露白穿帮
            ClampCameraInBounds(pathMap.gs_bounds);
        }

        //IoGroupChangeIntf
        public void OnIoEvtNotification(string keyAlias, UD.UiIoEventType evtType)
        {
            if (!_isIoEnabled) return;

            float modifier = (evtType == UD.UiIoEventType.KEYDOWN) ? 1f : -1f;

            switch (keyAlias)
            {
                case "CamMoveUp":    _moveInput.y += modifier; break;
                case "CamMoveDown":  _moveInput.y -= modifier; break;
                case "CamMoveRight": _moveInput.x += modifier; break;
                case "CamMoveLeft":  _moveInput.x -= modifier; break;
            }
        }

        // IoGroupChangeIntf
        public void OnGroupStatusChange(bool status)
        {
            _isIoEnabled = status;
            if (!_isIoEnabled)
            {
                _moveInput = Vector2.zero; // 紧急清空合成向量，防止失控无限滑行
            }
        }
#endregion

#region private functions
        private void RegisterCameraKey(KeyCode key, string alias)
        {
            _ioObserver.RegisterListener(this, key, alias, UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            _ioObserver.RegisterListener(this, key, alias, UD.UiIoEventType.KEYUP, UD.IoEvtScheduleType.LIST);
        }

        private void HandleCameraZoom(float scroll)
        {
            PathFinderMap pathMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(_curMapId);
            if (pathMap == null) return;

            Bounds mapBounds = pathMap.gs_bounds;

            // 计算缩放企图
            float newSize = _mainCamera.orthographicSize - scroll * _zoomSpeed;

            // 下限防护：防止看太近
            newSize = Mathf.Max(newSize, _minOrthographicSize);

            // 极致动态反向上限剪裁：看见的范围绝对不能超过地图本身的长宽总大小！
            float maxHeightSize = mapBounds.size.y / 2f;
            float maxWidthSize = (mapBounds.size.x / _mainCamera.aspect) / 2f;
            float absoluteMaxSize = Mathf.Min(maxHeightSize, maxWidthSize);

            newSize = Mathf.Min(newSize, absoluteMaxSize);
            _mainCamera.orthographicSize = newSize;

            // 缩放由于改变了视廊的宽高半径，边缘可能瞬间发生穿帮，必须立刻重新对齐物理夹逼
            ClampCameraInBounds(mapBounds);
        }

        private void HandleCameraMovement()
        {
            PathFinderMap pathMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(_curMapId);
            if (pathMap == null) return;

            // 合成并归一化帧步进平移向量
            Vector3 delta = new Vector3(_moveInput.x, _moveInput.y, 0f).normalized * (_moveSpeed * Time.deltaTime);
            _mainCamera.transform.position += delta;

            // 硬卡死防护
            ClampCameraInBounds(pathMap.gs_bounds);
        }

        // 反向夹逼裁剪：确保任何缩放或位移下，主相机的视廊边缘死死贴在地图内部
        private void ClampCameraInBounds(Bounds mapBounds)
        {
            float camHalfHeight = _mainCamera.orthographicSize;
            float camHalfWidth = camHalfHeight * _mainCamera.aspect;

            Vector3 pos = _mainCamera.transform.position;

            // 核心几何：用地图外围Bounds缩进一个相机的视口视廊半径，得到相机中心点的绝对合法移动范围
            float clampedX = Mathf.Clamp(pos.x, mapBounds.min.x + camHalfWidth, mapBounds.max.x - camHalfWidth);
            float clampedY = Mathf.Clamp(pos.y, mapBounds.min.y + camHalfHeight, mapBounds.max.y - camHalfHeight);

            // 极端防御机制：若地图配置范围小于相机的最小视野尺寸，直接牢牢死锁在地图正中心
            if (mapBounds.size.x < camHalfWidth * 2f) clampedX = mapBounds.center.x;
            if (mapBounds.size.y < camHalfHeight * 2f) clampedY = mapBounds.center.y;

            _mainCamera.transform.position = new Vector3(clampedX, clampedY, pos.z);
        }
#endregion
    }
}
