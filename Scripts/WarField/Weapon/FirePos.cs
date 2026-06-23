using UnityEngine;

namespace WarField
{
    // 子弹/炮弹出手位置的偏移量标记组件
    // 挂载到Soldier/Building的根物体上，为每个动画方向单独配置出手偏移
    // 最终出手世界坐标 = entity._transform.position + _offsets[dirIndex]
    //
    // dirIndex 与 Soldier._currentDirIndex 保持一致（CalculateDirectionIndex 的结果）:
    //   0 = 下(S)    1 = 右下(SE)    2 = 右(E)    3 = 右上(NE)
    //   4 = 上(N)    5 = 左上(NW)    6 = 左(W)    7 = 左下(SW)
    //  对应于动画中的dir_0是正面, 然后逆时针转动
    public class FirePos : MonoBehaviour
    {
#region private parameters

        [Tooltip("8方向出手偏移，索引对应动画 dirIndex:\n0=S  1=SE  2=E  3=NE  4=N  5=NW  6=W  7=SW")]
        [SerializeField] private Vector2[] _offsets = new Vector2[8];

        [SerializeField] private bool _debug = true;

#endregion

#region public functions

        // 根据实体位置与当前动画方向索引(0-7)返回世界空间出手坐标
        public Vector2 GetFirePos(Vector3 entityPos, int dirIndex)
        {
            if (_offsets == null || _offsets.Length == 0)
                return entityPos;
            int idx = Mathf.Clamp(dirIndex, 0, _offsets.Length - 1);
            return (Vector2)entityPos + _offsets[idx];
        }

#endregion

#region Unity callbacks

        private void OnDrawGizmos()
        {
            if (_debug == false)
                return;

            DrawAllDirGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (_debug == true)
                return;

            DrawAllDirGizmos();
        }

#endregion

#region private functions

        private void DrawAllDirGizmos()
        {
            if (_offsets == null)
                return;

            // 8方向依次用颜色区分: S SE E NE N NW W SW
            Color[] dirColors = new Color[]
            {
                Color.blue,                          // 0 S
                new Color(0f,   0.5f, 1f),           // 1 SE
                Color.green,                         // 2 E
                new Color(0.5f, 1f,   0f),           // 3 NE
                Color.cyan,                          // 4 N
                new Color(0.5f, 0f,   1f),           // 5 NW
                Color.red,                           // 6 W
                new Color(1f,   0.5f, 0f),           // 7 SW
            };

            Vector3 root = transform.position;
            for (int i = 0; i < _offsets.Length && i < 8; i++)
            {
                Vector3 pos = root + (Vector3)_offsets[i];
                Gizmos.color = dirColors[i];
                Gizmos.DrawSphere(pos, 0.03f);
            }
        }

#endregion
    }
}
