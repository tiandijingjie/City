using UnityEngine;

namespace WarField
{
    // 子弹目标位置的偏移量标记组件
    // 挂载到Soldier/Building的根物体上，在prefab界面调整_offset即可手动设置子弹落点偏移
    // 最终目标世界坐标 = entity._transform.position + gs_offset
    public class BulletTargetPos : MonoBehaviour
    {
#region private parameters

        [SerializeField] private Vector2 _offset = new Vector2(0f, 0.5f);

        [SerializeField] private bool _debug = true;

#endregion

#region private parameters' get set

        public Vector2 gs_offset
        {
            get { return _offset; }
        }

#endregion

#region Unity callbacks

        private void OnDrawGizmos()
        {
            if (_debug == false)
                return;

            DrawTargetGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (_debug == false)
                return;

            DrawTargetGizmo();
        }

#endregion

#region private functions

        private void DrawTargetGizmo()
        {
            Vector3 pos = transform.position + (Vector3)_offset;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pos, 0.1f);

            Gizmos.color = Color.yellow;
            float crossSize = 0.12f;
            Gizmos.DrawLine(pos - Vector3.up * crossSize, pos + Vector3.up * crossSize);
            Gizmos.DrawLine(pos - Vector3.right * crossSize, pos + Vector3.right * crossSize);
        }

#endregion
    }
}
