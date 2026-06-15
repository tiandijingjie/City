using UnityEngine;
using Unity.Mathematics;

namespace WarField
{
    using WE = WarFieldElements;

    /// <summary>
    /// Demo: 如何通过 SearchManager 查询距离自身最近的 OcularStone
    ///
    /// 使用步骤：
    ///   1. 将此脚本挂到任意 GameObject 上
    ///   2. 运行游戏后按键盘 [Space] 触发一次查询
    ///   3. 查看 Console 输出
    /// </summary>
    public class test : MonoBehaviour, ICollectResListener
    {
        // 记录当前已锁定的目标石头索引（-1 表示没有）
        private int _lockedPoolIndex = -1;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                RequestNearestStone();
        }

        // ──────────────────────────────────────────
        // 发起查询
        // ──────────────────────────────────────────

        private void RequestNearestStone()
        {
            if (_lockedPoolIndex != -1)
            {
                Debug.Log($"[Demo] 已锁定曈石 #{_lockedPoolIndex}，先解锁再查询");
                return;
            }

            float2 myPos = new float2(transform.position.x, transform.position.y);

            // p_resType: (byte)WRD.ResTypes.OCULARSTONE 只找曈石；传 0 则接受任意类型
            byte resType = (byte)WarResDefine.ResTypes.OCULARSTONE;

            // 构造一次性查找请求，注册到 SearchManager
            // SearchManager 会在下一帧 LateUpdate 的 FinishSearchJobs 中：
            //   1. 调用 ScheduleResGridRebuild（按需重建 p_resGrid）
            //   2. 等待 BuildResGridJob 完成
            //   3. 用 ResQueryHelper.FindClosest 扩展环搜索，回调结果
            var searcher = new SearchResClosest(myPos, WE.OnGroundMapIndex, this, resType);
            SearchManager.Instance.RegisterResSearch(searcher);

            Debug.Log($"[Demo] 已提交查询，位置=({myPos.x:F1}, {myPos.y:F1})，结果将在下一帧回调");
        }

        // ──────────────────────────────────────────
        // ICollectResListener 回调
        // ──────────────────────────────────────────

        /// <summary>
        /// 找到最近的可用曈石时回调
        /// 返回 true  → SearchManager 会调用 WarResCtrl.LockRes 锁定该资源
        /// 返回 false → 不锁定（仅查看位置，不占用）
        /// </summary>
        public bool ICollectResListener_OnResourceFound(int poolIndex, Vector2 pos)
        {
            _lockedPoolIndex = poolIndex;
            Debug.Log($"[Demo] 找到曈石！poolIndex={poolIndex}  位置=({pos.x:F2}, {pos.y:F2})");

            // 演示：假装走过去捡，这里直接模拟拾取
            int energy = WarResCtrl.Instance.PickUpRes(poolIndex);
            Debug.Log($"[Demo] 拾取成功，获得能量={energy}");
            _lockedPoolIndex = -1;

            return true; // 返回 true 表示希望锁定（LockRes 会被 SearchManager 调用）
        }

        /// <summary>场上没有可用的未锁定曈石</summary>
        public void ICollectResListener_OnResourceNotFound()
        {
            Debug.Log("[Demo] 当前场上没有可采集的曈石");
        }

        /// <summary>已锁定的曈石在走过去途中意外消失（超时/被别人拾取）</summary>
        public void ICollectResListener_OnResourceDisappeared(int poolIndex)
        {
            Debug.Log($"[Demo] 曈石 #{poolIndex} 已消失，重新查询");
            _lockedPoolIndex = -1;
            RequestNearestStone();
        }

        // ──────────────────────────────────────────
        // 解锁示例（按 U 键手动解锁）
        // ──────────────────────────────────────────

        // 如果农民决定放弃走向该石头，需要主动解锁以便其他人能找到它
        private void OnGUI()
        {
            if (_lockedPoolIndex == -1) return;
            if (GUILayout.Button($"解锁曈石 #{_lockedPoolIndex}"))
            {
                WarResCtrl.Instance.UnlockPickableRes(_lockedPoolIndex, this);
                _lockedPoolIndex = -1;
                Debug.Log("[Demo] 已手动解锁曈石");
            }
        }
    }
}
