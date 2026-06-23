using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace WarField.EffectAnim
{
    // Burst-parallel job: reads parent world position + pre-stored local offset,
    // builds a pure-translation TRS matrix and writes it to the world-matrix cache.
    //
    // Identical logic to WarField.Anim.WorldMatrixCacheJob; kept in a separate namespace
    // to honour the design rule that EffectAnim code must not share types with the Anim system.
    [BurstCompile]
    public struct EffectAnimWorldMatrixCacheJob : IJobParallelForTransform
    {
        [ReadOnly]  public NativeArray<float3>    p_localOffsets;
        [WriteOnly] public NativeArray<Matrix4x4> p_worldMatrices;

        public void Execute(int index, TransformAccess transform)
        {
            float3 worldPos = (float3)transform.position + p_localOffsets[index];
            float4x4 m = float4x4.TRS(worldPos, quaternion.identity, new float3(1f, 1f, 1f));
            p_worldMatrices[index] = (Matrix4x4)m;
        }
    }
}
