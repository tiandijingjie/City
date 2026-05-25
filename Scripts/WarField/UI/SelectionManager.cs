using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.EventSystems;
using Unity.Collections;

namespace WarField
{
    using UD = UIDefines;
    using HD = HeroDefines;
    using GD = GlobalDefines;
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    public class SelectionManager : MonoBehaviour, IoObserverIntf
    {
#region public parameters
        public static SelectionManager Instance;
#endregion

#region private parameters
        private enum MouseCursorStyle
        {
            Default,
            AttackCrosshair // A地板预备状态下的红十字准心
        }

        [SerializeField] private int _aStartThreshHold = 1; //是用A*寻路最多士兵的数量

        private List<Soldier> _selectedSoldiers = new List<Soldier>();
        private Hero _selectedHero = null;
        private WarBuilding _selectedBuilding = null;

        private IoObserver _ioObserver;
        private Camera _mainCamera;

        // 内部状态机变量
        private Vector2 _boxStartMousePos;
        private bool _isDrawingBox = false;
        private Vector2 _keyboardMoveInput = Vector2.zero;
        private bool _isPendingAttackMove = false;

        // 用于处理同步 Search 回调通信的影子数据
        private bool _clickHandled = false;
        private Vector2 _currentClickWorldPos;

        //F1 双击判定时序变量与判定阈值
        private float _lastF1ClickTime = 0f;
        private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
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
            _mainCamera = Camera.main;

            // 对齐 UIDefines，将自身的 IO 事件组变更为正统的 SELECTIONMANAGER 组
            _ioObserver = new IoObserver(UD.UIEventGroupType.SELECTIONMANAGER);
        }

        protected virtual void OnDrawGizmos()
        {
            // 将 Vector2 包装成当前层级的 Vector3 坐标
            Vector3 target3D = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // A. 在点击处画一个红色的实心中心定位球
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(target3D, 0.22f);

            // B. 外围追加一个大圆环，防止视角拉得太高时看不清
            Gizmos.DrawWireSphere(target3D, 0.5f);

            // C. 画一个标志性的 Rts 红十字准心线，彻底钉死世界坐标
            Gizmos.DrawLine(target3D + Vector3.left * 1.0f, target3D + Vector3.right * 1.0f);
            Gizmos.DrawLine(target3D + Vector3.down * 1.0f, target3D + Vector3.up * 1.0f);
        }
#endregion

#region public functions
        public void InitSelectionManager()
        {
            // 注册鼠标左键按下与抬起
            _ioObserver.RegisterListener(this, KeyCode.Mouse0, "MouseLeftClick", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            _ioObserver.RegisterListener(this, KeyCode.Mouse0, "MouseLeftClick", UD.UiIoEventType.KEYUP, UD.IoEvtScheduleType.LIST);

            // 注册鼠标右键
            _ioObserver.RegisterListener(this, KeyCode.Mouse1, "MouseRightClick", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);

            // 注册 F1 快速选择英雄
            _ioObserver.RegisterListener(this, KeyCode.F1, "SelectHero", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);

            // 注册快捷按键：A 键（攻击移动）与 H 键（原地驻守）
            _ioObserver.RegisterListener(this, KeyCode.A, "AttackMoveKey", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
            _ioObserver.RegisterListener(this, KeyCode.H, "HoldKey", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);

            _ioObserver.RegisterListener(this, KeyCode.Escape, "CancelAction", UD.UiIoEventType.KEYDOWN, UD.IoEvtScheduleType.LIST);
        }

        //IoObserverIntf  IO通知
        public void OnIoEvtNotification(string keyAlias, UD.UiIoEventType evtType)
        {
            //画框的状态下不响应除esc外的事件
            if (_isDrawingBox)
            {
                // 按 ESC，紧急停笔、解除 IO 独占、清除框选痕迹、取消选中
                if (keyAlias == "CancelAction" && evtType == UD.UiIoEventType.KEYDOWN)
                {
                    _isDrawingBox = false;
                    UiDrawSelectionBox.Instance.StopDraw();
                    UiIoTask.Instance.ReleaseExclusiveOwnerShip(UD.UIEventGroupType.SELECTIONMANAGER);
                    ClearAllSelection();
                    return;
                }

                // 左键正常抬起，解除 IO 独占、交由数学裁剪矩阵结算
                if (keyAlias == "MouseLeftClick" && evtType == UD.UiIoEventType.KEYUP)
                {
                    _isDrawingBox = false;
                    UiDrawSelectionBox.Instance.StopDraw();
                    UiIoTask.Instance.ReleaseExclusiveOwnerShip(UD.UIEventGroupType.SELECTIONMANAGER);
                    SelectObjectsInBox(Input.mousePosition);
                    return;
                }

                return;
            }

            if (keyAlias == "CancelAction" && evtType == UD.UiIoEventType.KEYDOWN)
            {
                // 如果在 A地板 预备状态按 ESC，视为退回到普通走位状态
                if (_isPendingAttackMove)
                {
                    ExitAttackMovePending();
                    return;
                }

                // 常规状态下按 ESC，直接干净利落地取消当前队伍和建筑的选择
                ClearAllSelection();
                return;
            }

            // F1 快捷单选英雄
            if (keyAlias == "SelectHero" && evtType == UD.UiIoEventType.KEYDOWN)
            {
                ExitAttackMovePending();
                SelectHero();
                // 双击时序判定
                if (Time.time - _lastF1ClickTime < DOUBLE_CLICK_THRESHOLD)
                {
                    Hero hero = SoldierCtrl.Instance.gs_curHero;
                    if (hero != null)
                        CameraCtrl.Instance.FocusCameraOnPosition(hero.transform.position, hero.gs_mapId);
                }

                _lastF1ClickTime = Time.time; // 刷新时间戳影子变量
                return;
            }

            // 按下 A 键进入攻击移动预备期
            if (keyAlias == "AttackMoveKey" && evtType == UD.UiIoEventType.KEYDOWN)
            {
                if (_selectedHero != null || _selectedSoldiers.Count > 0)
                {
                    _isPendingAttackMove = true;
                    ApplyMouseCursorStyle(MouseCursorStyle.AttackCrosshair);
                }
                return;
            }

            // 按下 H 键进入原地驻守状态
            if (keyAlias == "HoldKey" && evtType == UD.UiIoEventType.KEYDOWN)
            {
                ExitAttackMovePending();
                HandleHoldOrder();
                return;
            }

            // 处理鼠标右键集结命令
            if (keyAlias == "MouseRightClick" && evtType == UD.UiIoEventType.KEYDOWN)
            {
                // 铁律二：核对右键点击是否碰到了 UI 组件（如点到了技能栏或主菜单）
                if (EventSystem.current.IsPointerOverGameObject())
                    return;

                // 反悔机制：如果在 A 过去预备期点了右键，代表撤销本次 A地板，退出并重置为右键走位
                if (_isPendingAttackMove)
                {
                    ExitAttackMovePending();
                    HandleRightClickOrder();
                    return;
                }

                HandleRightClickOrder();
                return;
            }

            // A-Move 攻击移动严格操作时序拦截大闸
            if (_isPendingAttackMove)
            {
                if (keyAlias == "MouseLeftClick" && evtType == UD.UiIoEventType.KEYDOWN)
                {
                    if (EventSystem.current.IsPointerOverGameObject())
                        return;

                    ExitAttackMovePending();

                    Vector3 screenToWorld = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    Vector2 targetPos = new Vector2(screenToWorld.x, screenToWorld.y);

                    DispatchMovementOrder(targetPos, true);
                    return;
                }

                // 按了 A 之后进行非左键按下的动作
                ExitAttackMovePending();
                return;
            }

            // 常规左键按下：单点或拉框准备
            if (keyAlias == "MouseLeftClick" && evtType == UD.UiIoEventType.KEYDOWN)
            {
                if (EventSystem.current.IsPointerOverGameObject() == false)
                {
                    _boxStartMousePos = Input.mousePosition;

                    if (CheckClickOverTargetObj(_boxStartMousePos))
                        return;

                    // 画框,开启IO抢占
                    if (UiIoTask.Instance.OccupyExclusiveOwnerShip(UD.UIEventGroupType.SELECTIONMANAGER))
                    {
                        _isDrawingBox = true;
                        UiDrawSelectionBox.Instance.StartDraw(_boxStartMousePos);
                    }
                }
                return;
            }
        }
#endregion

#region private functions

		// 鼠标右键走位
        private void HandleRightClickOrder()
        {
            Vector3 screenToWorld = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 targetPos = new Vector2(screenToWorld.x, screenToWorld.y);
            DispatchMovementOrder(targetPos, false);
        }

        // 兵团移动出口
        private void DispatchMovementOrder(Vector2 targetPos, bool isAttackMove)
        {
            // 把点击坐标提前吸附到最近的可走 cell，避免点击到障碍物内部时
            // 集群流场士兵在终点圈内脱离流场直插障碍内部。
            PathFinderMap pathMap = WarMapCtrl.Instance.GetPathFinderMapByIndex(WarMapCtrl.Instance.gs_curMapId);
            Vector2 dispatchPos = targetPos;
            if (pathMap != null)
            {
                if (!pathMap.TryFindNearestWalkableWorldPos(targetPos, out dispatchPos))
                    return; // 周围都找不到可达点，整条指令直接放弃
            }

            if (_selectedHero != null)
            {
                _selectedHero.UserManipulate(-1, dispatchPos, isAttackMove);
            }

            int soldierCount = _selectedSoldiers.Count;
            if (soldierCount > 0 && pathMap != null)
            {
                if (soldierCount <= _aStartThreshHold)
                {
                    for (int i = 0; i < soldierCount; i++)
                    {
                        _selectedSoldiers[i].UserManipulate(-1, dispatchPos, isAttackMove);
                    }
                }
                else
                {
                    int flowId = pathMap.RequestLocalFlowField(dispatchPos, soldierCount, out Vector2 snappedPos);
                    if (flowId != -1)
                    {
                        for (int i = 0; i < soldierCount; i++)
                        {
                            _selectedSoldiers[i].UserManipulate(flowId, snappedPos, isAttackMove);
                        }
                    }
                }
            }
        }

        // 局内 Hold 原地驻守处理中心（只对框选中的英雄有效）
        private void HandleHoldOrder()
        {
            if (_selectedHero != null)
            {
                _selectedHero.UserManipulate((int)HD.HeroManipulateSrc.HOLDPRESS, Vector2.zero);
            }
        }

        private bool CheckClickOverTargetObj(Vector2 mousePos)
        {
            _currentClickWorldPos = _mainCamera.ScreenToWorldPoint(mousePos);
            byte curMapId = WarMapCtrl.Instance.gs_curMapId;

            // 设定一个稍微宽泛的世界粗筛半径（比如3.0米），确保能把模型的视觉头部也罩进网格里
            float coarseSearchRadius = 3.0f;

            _clickHandled = false;

            SearchArea clickSearcher = new SearchArea(0, OnSingleClickQueryCallback,
                () => SearchShapeDef.CreateCircle(_currentClickWorldPos, coarseSearchRadius),
                null, curMapId);

            SearchConditionUtil.AddFriendlyBuildingCondition(clickSearcher);
            SearchConditionUtil.AddFriendlySoldierCondition(clickSearcher);

            SearchManager.Instance.RegisterSearch(clickSearcher);

            return _clickHandled;
        }

        //单次点击search的回调
        private void OnSingleClickQueryCallback(List<IGridNode> targets)
        {
            Vector2 mousePixelPos = Input.mousePosition;

            float lowestWorldY = float.MaxValue; // 用于记录重叠单位中，最靠前（世界Y最小）的那个
            Soldier bestCandidateSoldier = null;
            WarBuilding bestCandidateBuilding = null;

            for (int i = 0; i < targets.Count; i++)
            {
                Renderer targetRenderer = null;
                float currentWorldY = 0f;
                Soldier currentSoldier = null;
                WarBuilding currentBuilding = null;

                // 区分提取数据
                if (targets[i] is Soldier sd)
                {
                    currentSoldier = sd;
                    targetRenderer = sd.GetComponentInChildren<Renderer>(); // 动态获取它身上的真实渲染器 bounds
                    currentWorldY = sd.transform.position.y; // 它的脚底世界 Y 轴，代表排序权重
                }
                else if (targets[i] is WarBuilding bd)
                {
                    currentBuilding = bd;
                    targetRenderer = bd.GetComponentInChildren<Renderer>();
                    currentWorldY = bd.transform.position.y;
                }

                if (targetRenderer == null)
                    continue;

                // 将真实的视觉 Bounds 投影到屏幕像素空间形成 Rect
                Bounds worldBounds = targetRenderer.bounds;
                Vector2 screenMin = _mainCamera.WorldToScreenPoint(worldBounds.min);
                Vector2 screenMax = _mainCamera.WorldToScreenPoint(worldBounds.max);

                Rect visualScreenRect = Rect.MinMaxRect(
                    Mathf.Min(screenMin.x, screenMax.x),
                    Mathf.Min(screenMin.y, screenMax.y),
                    Mathf.Max(screenMin.x, screenMax.x),
                    Mathf.Max(screenMin.y, screenMax.y)
                );

                // 检查鼠标像素点是否在这个真实的视觉盒子里
                if (visualScreenRect.Contains(mousePixelPos))
                {
                    // 特殊过滤：如果是建筑，进行更精确的多边形包围判定（防止点到大建筑留白处）
                    if (currentBuilding != null && !currentBuilding.IsPosInBuilding(_currentClickWorldPos))
                        continue;

                    // 多个 bounds 内部，选择世界 Y 最小的
                    if (currentWorldY < lowestWorldY)
                    {
                        lowestWorldY = currentWorldY;
                        bestCandidateSoldier = currentSoldier;
                        bestCandidateBuilding = currentBuilding;
                    }
                }
            }

            // 最终敲定执行：分发排他性结果
            if (bestCandidateSoldier != null)
            {
                if (bestCandidateSoldier.gs_isHero)
                    SelectHero();
                else
                    SelectGroup(new List<Soldier> { bestCandidateSoldier }, null);

                _clickHandled = true;
            }
            else if (bestCandidateBuilding != null)
            {
                SelectBuilding(bestCandidateBuilding);
                _clickHandled = true;
            }
        }

        private void SelectObjectsInBox(Vector2 endMousePos)
        {
            if (Vector2.Distance(_boxStartMousePos, endMousePos) < 5f)
            {
                ClearAllSelection();
                return;
            }

            Vector3 worldStart = _mainCamera.ScreenToWorldPoint(_boxStartMousePos);
            Vector3 worldEnd = _mainCamera.ScreenToWorldPoint(endMousePos);

            float2 boxCenter = new float2((worldStart.x + worldEnd.x) * 0.5f, (worldStart.y + worldEnd.y) * 0.5f);
            float radius = math.max(math.abs(worldStart.x - worldEnd.x), math.abs(worldStart.y - worldEnd.y)) * 0.5f;

            byte curMapId = WarMapCtrl.Instance.gs_curMapId;

            float minX = Mathf.Min(_boxStartMousePos.x, endMousePos.x);
            float maxX = Mathf.Max(_boxStartMousePos.x, endMousePos.x);
            float minY = Mathf.Min(_boxStartMousePos.y, endMousePos.y);
            float maxY = Mathf.Max(_boxStartMousePos.y, endMousePos.y);
            Rect screenRect = new Rect(minX, minY, maxX - minX, maxY - minY);

            List<Soldier> tempSelectedSoldiers = new List<Soldier>();
            Hero tempSelectedHero = null;

            SearchArea boxSearcher = new SearchArea(0, (targets) =>
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] is Soldier sd && sd.gs_race == WE.RaceType.Human)
                    {
                        Vector2 sdScreenPos = _mainCamera.WorldToScreenPoint(sd.transform.position);
                        if (screenRect.Contains(sdScreenPos))
                        {
                            if (sd.gs_isHero)
                                tempSelectedHero = sd as Hero;
                            else
                                tempSelectedSoldiers.Add(sd);
                        }
                    }
                }
            }, () => SearchShapeDef.CreateCircle(boxCenter, radius), null, curMapId);

            SearchConditionUtil.AddFriendlySoldierCondition(boxSearcher);
            SearchManager.Instance.RegisterSearch(boxSearcher);

            if (tempSelectedHero != null || tempSelectedSoldiers.Count > 0)  //必须要选中了东西
                SelectGroup(tempSelectedSoldiers, tempSelectedHero);
            else
                ClearAllSelection();
        }

        //选中英雄
        private void SelectHero()
        {
            ClearAllSelection();
            if (SoldierCtrl.Instance.gs_curHero != null)
            {
                _selectedHero = SoldierCtrl.Instance.gs_curHero;
                ApplySelectionRing(_selectedHero, true);
            }
        }

        //选中一群士兵
        private void SelectGroup(List<Soldier> soldiers, Hero hero)
        {
            ClearAllSelection();
            _selectedHero = hero;
            if (_selectedHero != null) ApplySelectionRing(_selectedHero, true);

            _selectedSoldiers.AddRange(soldiers);
            for (int i = 0; i < _selectedSoldiers.Count; i++)
            {
                ApplySelectionRing(_selectedSoldiers[i], true);
            }
        }

        private void SelectBuilding(WarBuilding building)
        {
            ClearAllSelection();
            _selectedBuilding = building;
            if (_selectedBuilding != null)
            {
                _selectedBuilding.UserSelected(true);
                ApplySelectionRing(_selectedBuilding, true);
            }
        }

        private void ClearAllSelection()
        {
            if (_selectedBuilding != null)
            {
                _selectedBuilding.UserSelected(false);
                ApplySelectionRing(_selectedBuilding, false);
                _selectedBuilding = null;
            }

            if (_selectedHero != null)
            {
                ApplySelectionRing(_selectedHero, false);
            }

            // 仅仅关闭小兵脚下的高亮材质圈，不影响、不重置、不打断底层的局部流场行军！
            for (int i = 0; i < _selectedSoldiers.Count; i++)
            {
                ApplySelectionRing(_selectedSoldiers[i], false);
            }

            _selectedSoldiers.Clear();
            _selectedHero = null;
            _keyboardMoveInput = Vector2.zero;
            ExitAttackMovePending();
        }

        private void ExitAttackMovePending()
        {
            _isPendingAttackMove = false;
            ApplyMouseCursorStyle(MouseCursorStyle.Default);
        }

        //改变cursor的图标
        private void ApplyMouseCursorStyle(MouseCursorStyle style)
        {
            switch (style)
            {
                case MouseCursorStyle.Default:
                    break;
                case MouseCursorStyle.AttackCrosshair:
                    break;
            }
        }

        //士兵,hero显示选中的圈
        private void ApplySelectionRing(Soldier soldier, bool visible)
        {
            if (soldier == null)
                return;
        }

        //建筑被选中,可以弹出建筑菜单
        private void ApplySelectionRing(WarBuilding building, bool visible)
        {
            if (building == null)
                return;
        }
#endregion
    }
}
