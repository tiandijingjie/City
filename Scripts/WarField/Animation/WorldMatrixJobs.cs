using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace WarField.Anim
{
    // Burst 并行 Job：仅读取父节点 world position，加上预存的 localOffset（纯平移），
    // 构造渲染用的 TRS 矩阵写入 _worldMatrixCache。
    //
    // 相比原来读取整个 localToWorldMatrix 再做 4x4 矩阵乘法，此版本：
    //   - 每元素内存读取：12 bytes（float3）vs 128 bytes（两个 Matrix4x4）
    //   - 每元素计算：1 次 float3 加法 vs 64 次乘法 + 48 次加法
    // 前提：父节点和 proxy 的 localOffset 运行时无旋转、无非 1 缩放（士兵只做平移）。
    [BurstCompile]
    public struct WorldMatrixCacheJob : IJobParallelForTransform
    {
        [ReadOnly]  public NativeArray<float3>    p_localOffsets;  // SoldierAnim 相对父节点的 localPosition
        [WriteOnly] public NativeArray<Matrix4x4> p_worldMatrices;

        public void Execute(int index, TransformAccess transform)
        {
            float3 worldPos = (float3)transform.position + p_localOffsets[index];
            // 构造纯平移矩阵（scale=1, rotation=identity），等价于 Matrix4x4.Translate(worldPos)
            float4x4 m = float4x4.TRS(worldPos, quaternion.identity, new float3(1f, 1f, 1f));
            p_worldMatrices[index] = (Matrix4x4)m;
        }
    }
}
