using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace WarField
{
    using WE = WarFieldElements;
    using FD = FarmerDefines;

    public struct FarmerMoveCmd
    {
        public float2 p_currentPos;    // 当前位置
        public int p_gridIndex;        // SpatialGrid 中自身索引
        public float p_moveSpeed;      // 当前移速
        public float p_mass;           // 质量
        public float p_radius;         // 碰撞半径
        public byte p_status;          // 当前状态 (FarmerDefines.FarmerStatus)
        public float2 p_targetPos;     // 目标坐标 (资源位置或最后已知位置)
        public float2 p_desiredDir;    // A* 路径方向 (TORES状态由主线程计算，其他状态无效)
        public int p_flowIndex;        // GOBACK 时为 HomeFlowFieldId，其余状态为 0
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct FarmerMoveJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<FarmerMoveCmd> p_moveCmds;

        [ReadOnly] public NativeArray<float2> p_flowFieldPool;
        public float p_flowCellSize;
        public float2 p_flowMapOrigin;
        public int p_flowCols;
        public int p_flowRows;
        public float2 p_flowMapMax;

        public SpatialQueryHelper p_dynamicQuery;
        public SpatialQueryHelper p_staticQuery;

        public float p_deltaTime;

        public void Execute(int index, TransformAccess transform)
        {
            FarmerMoveCmd cmd = p_moveCmds[index];
            SoldierMoveFlowContext flow = CreateFlowContext();
            float2 driveDir = FarmerMovePhysics.ComputeAutoDriveDir(cmd, cmd.p_desiredDir, flow);
            float3 nextPos = FarmerMovePhysics.ComputeFinalPosition(cmd, driveDir, p_dynamicQuery, p_staticQuery, flow, p_deltaTime);
            if (math.isfinite(nextPos.x) && math.isfinite(nextPos.y))
                transform.position = nextPos;
        }

        private SoldierMoveFlowContext CreateFlowContext()
        {
            return new SoldierMoveFlowContext
            {
                p_flowFieldPool = p_flowFieldPool,
                p_faction = (byte)WE.FactionType.FRIENDLY,
                p_flowCellSize = p_flowCellSize,
                p_flowMapOrigin = p_flowMapOrigin,
                p_flowCols = p_flowCols,
                p_flowRows = p_flowRows,
                p_flowMapMax = p_flowMapMax
            };
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public static class FarmerMovePhysics
    {
        public static float2 ComputeAutoDriveDir(in FarmerMoveCmd cmd, float2 baseDriveDir, in SoldierMoveFlowContext flow)
        {
            byte status = cmd.p_status;

            if (status == (byte)FD.FarmerStatus.TORES)
            {
                // 直接使用主线程沿 A* 路径点计算好的方向
                // 近目标的直线收尾由 Farmer.cs UpdateAStarDesiredDir 负责，不在 Job 中覆盖
                if (math.lengthsq(baseDriveDir) > 0.001f)
                    return math.normalizesafe(baseDriveDir);

                // 兜底：直线朝向目标（A* 尚未返回或路径点已全部走完）
                float2 toTarget = cmd.p_targetPos - cmd.p_currentPos;
                if (math.lengthsq(toTarget) > 0.001f)
                    return math.normalizesafe(toTarget);

                return float2.zero;
            }
            else if (status == (byte)FD.FarmerStatus.GOBACK)
            {
                // 使用 HomeFlowField 返回驻地
                int cellC = (int)math.floor((cmd.p_currentPos.x - flow.p_flowMapOrigin.x) / flow.p_flowCellSize);
                int cellR = (int)math.floor((cmd.p_currentPos.y - flow.p_flowMapOrigin.y) / flow.p_flowCellSize);
                cellC = math.clamp(cellC, 0, flow.p_flowCols - 1);
                cellR = math.clamp(cellR, 0, flow.p_flowRows - 1);

                int totalCells = flow.p_flowCols * flow.p_flowRows;
                float2 flowDir = flow.p_flowFieldPool[cmd.p_flowIndex * totalCells + cellR * flow.p_flowCols + cellC];

                if (math.abs(flowDir.x) + math.abs(flowDir.y) < 0.001f)
                    flowDir = FindEscapeFlowDir(cmd.p_currentPos, cmd.p_targetPos, cmd.p_flowIndex, flow);

                if (math.lengthsq(flowDir) > 0.001f)
                    return math.normalizesafe(flowDir);

                return float2.zero;
            }

            return float2.zero;
        }

        public static float3 ComputeFinalPosition(
            in FarmerMoveCmd cmd,
            float2 driveDir,
            SpatialQueryHelper dynamicQuery,
            SpatialQueryHelper staticQuery,
            in SoldierMoveFlowContext flow,
            float deltaTime)
        {
            float2 originalDriveDir = driveDir;
            float2 avoidForce = float2.zero;
            float frontMassResistance = 0f;

            float myEffectiveMass = cmd.p_mass;

            float searchRadius = cmd.p_radius * 3.0f;
            float searchRadiusSqr = searchRadius * searchRadius;

            bool isBlockedByDynamic = false;
            bool isBlockedByStatic = false;

            FixedList512Bytes<int> dynamicNeighbors = new FixedList512Bytes<int>();
            dynamicQuery.FindEntitiesInRange(cmd.p_currentPos, searchRadius, searchRadiusSqr, (int)WE.WarEleType.FARMER, -1, cmd.p_gridIndex, ref dynamicNeighbors);

            for (int i = 0; i < dynamicNeighbors.Length; i++)
            {
                GridEntityData neighbor = dynamicQuery.p_entities[dynamicNeighbors[i]];
                CalculatePushForce(cmd, neighbor, myEffectiveMass, ref driveDir, false, ref avoidForce, ref frontMassResistance, ref isBlockedByDynamic, ref isBlockedByStatic);
            }

            FixedList512Bytes<int> staticNeighbors = new FixedList512Bytes<int>();
            staticQuery.FindEntitiesInRange(cmd.p_currentPos, searchRadius, searchRadiusSqr, -1, -1, -1, ref staticNeighbors);

            for (int i = 0; i < staticNeighbors.Length; i++)
            {
                GridEntityData neighbor = staticQuery.p_entities[staticNeighbors[i]];
                CalculatePushForce(cmd, neighbor, myEffectiveMass, ref driveDir, true, ref avoidForce, ref frontMassResistance, ref isBlockedByDynamic, ref isBlockedByStatic);
            }

            float speedMultiplier = myEffectiveMass / (myEffectiveMass + frontMassResistance);

            float avoidForceLengthSq = math.lengthsq(avoidForce);
            if (avoidForceLengthSq > 0.0001f)
            {
                float avoidForceLength = math.sqrt(avoidForceLengthSq);
                float maxAvoidForce = 1.2f;
                float softLength = maxAvoidForce * avoidForceLength / (maxAvoidForce + avoidForceLength);
                avoidForce = (avoidForce / avoidForceLength) * softLength;
            }

            float2 expectedMove = originalDriveDir * cmd.p_moveSpeed * deltaTime;
            float2 finalMove = (driveDir * cmd.p_moveSpeed * deltaTime * speedMultiplier) + avoidForce;

            float expectedMoveSq = math.lengthsq(expectedMove);
            float progress = math.dot(finalMove, expectedMove);
            bool isStuck = expectedMoveSq > 0.0001f && progress < (expectedMoveSq * 0.1f);
            float phaseOffset = cmd.p_gridIndex * 1.37f;

            if (isStuck)
            {
                if (isBlockedByStatic)
                {
                    float2 escapeDir;
                    if (cmd.p_status == (byte)FD.FarmerStatus.GOBACK)
                    {
                        escapeDir = FindEscapeFlowDir(cmd.p_currentPos, cmd.p_targetPos, cmd.p_flowIndex, flow);
                    }
                    else
                    {
                        // TORES 卡死时沿碰撞切线滑动，而非沿撞墙方向原路推进
                        // 优先用 avoidForce 法向（更精确反映实际碰撞几何），退化时用驱动方向的切线
                        float2 referenceNormal = math.lengthsq(avoidForce) > 0.0001f
                            ? math.normalizesafe(avoidForce)
                            : originalDriveDir;

                        float2 tangent = new float2(-referenceNormal.y, referenceNormal.x);
                        // gridIndex 奇偶性打破对称，避免集群同时选同一侧卡死
                        escapeDir = ((cmd.p_gridIndex & 1) == 0) ? tangent : -tangent;
                    }

                    if (math.lengthsq(escapeDir) > 0.001f)
                    {
                        float2 escapeNorm = math.normalizesafe(escapeDir);
                        float avoidDot = math.dot(avoidForce, escapeNorm);
                        if (avoidDot < 0.0f)
                            avoidForce -= avoidDot * escapeNorm;

                        finalMove = (escapeNorm * cmd.p_moveSpeed * deltaTime * 0.9f) + avoidForce;
                    }
                }
                else if (isBlockedByDynamic)
                {
                    float2 jitter = new float2(
                        math.sin(cmd.p_currentPos.y * 100f + phaseOffset),
                        math.cos(cmd.p_currentPos.x * 100f + phaseOffset)
                    ) * 0.01f;
                    finalMove = (originalDriveDir * cmd.p_moveSpeed * deltaTime * 0.8f) + avoidForce + jitter;
                }
            }
            else if (math.lengthsq(finalMove) < 0.001f && expectedMoveSq > 0.01f)
            {
                float2 fallbackJitter = new float2(
                    math.sin(cmd.p_currentPos.y * 50f + phaseOffset),
                    math.cos(cmd.p_currentPos.x * 50f + phaseOffset)
                ) * 0.02f;
                finalMove += fallbackJitter;
            }

            float3 nextPos = new float3(cmd.p_currentPos.x + finalMove.x, cmd.p_currentPos.y + finalMove.y, 0);
            nextPos.x = math.clamp(nextPos.x, flow.p_flowMapOrigin.x, flow.p_flowMapMax.x);
            nextPos.y = math.clamp(nextPos.y, flow.p_flowMapOrigin.y, flow.p_flowMapMax.y);
            nextPos.z = WarFieldUtil.GetZByY(nextPos.y, flow.p_flowMapOrigin.y);
            return nextPos;
        }

        private static float2 FindEscapeFlowDir(float2 currentPos, float2 targetPos, int flowIndex, in SoldierMoveFlowContext flow)
        {
            float2 escapeDir = float2.zero;
            int validExits = 0;
            int totalCells = flow.p_flowCols * flow.p_flowRows;

            int targetC = (int)math.floor((targetPos.x - flow.p_flowMapOrigin.x) / flow.p_flowCellSize);
            int targetR = (int)math.floor((targetPos.y - flow.p_flowMapOrigin.y) / flow.p_flowCellSize);

            for (int i = 0; i < 4; i++)
            {
                float2 offset = float2.zero;
                if (i == 0)
                    offset = new float2(0, flow.p_flowCellSize);
                else if (i == 1)
                    offset = new float2(0, -flow.p_flowCellSize);
                else if (i == 2)
                    offset = new float2(flow.p_flowCellSize, 0);
                else
                    offset = new float2(-flow.p_flowCellSize, 0);

                float2 probePos = currentPos + offset;
                int pC = (int)math.floor((probePos.x - flow.p_flowMapOrigin.x) / flow.p_flowCellSize);
                int pR = (int)math.floor((probePos.y - flow.p_flowMapOrigin.y) / flow.p_flowCellSize);

                if (pC >= 0 && pC < flow.p_flowCols && pR >= 0 && pR < flow.p_flowRows)
                {
                    int pIndex = flowIndex * totalCells + pR * flow.p_flowCols + pC;
                    float2 pFlow = float2.zero;

                    if (pIndex >= 0 && pIndex < flow.p_flowFieldPool.Length)
                        pFlow = flow.p_flowFieldPool[pIndex];

                    bool isTargetCell = (pC == targetC && pR == targetR);
                    if (isTargetCell || math.lengthsq(pFlow) > 0.001f)
                    {
                        escapeDir += math.normalizesafe(offset) + pFlow;
                        validExits++;
                    }
                }
            }

            if (validExits > 0 && math.lengthsq(escapeDir) > 0.001f)
                return math.normalizesafe(escapeDir);

            return new float2(1, 0); // farmer 始终为 FRIENDLY 阵营，兜底朝东
        }

        private static void CalculatePushForce(in FarmerMoveCmd cmd, GridEntityData neighbor, float myEffectiveMass, ref float2 driveDir, bool isStatic,
            ref float2 avoidForce, ref float frontMassResistance, ref bool isBlockedByDynamic, ref bool isBlockedByStatic)
        {
            float2 dirFromNeighbor = cmd.p_currentPos - neighbor.p_position;
            float distSq = math.lengthsq(dirFromNeighbor);
            float minSafeDist = cmd.p_radius + neighbor.p_radius;
            float minSafeDistSq = minSafeDist * minSafeDist;

            if (distSq < minSafeDistSq && distSq > 0.000001f)
            {
                float invDist = math.rsqrt(distSq);
                float dist = distSq * invDist;
                float overlap = minSafeDist - dist;
                if (overlap > 0.01f)
                {
                    if (isStatic)
                        isBlockedByStatic = true;
                    else
                        isBlockedByDynamic = true;
                }

                float neighborMass = isStatic ? 9999f : neighbor.p_radius * 20.0f;
                float pushFactor = neighborMass / (myEffectiveMass + neighborMass);
                float2 pushDir = dirFromNeighbor * invDist;

                avoidForce += pushDir * overlap * pushFactor * 0.5f;

                if (isStatic == false)
                {
                    if (math.lengthsq(driveDir) > 0.1f)
                    {
                        float dotMatch = math.dot(driveDir, -pushDir);
                        if (dotMatch > 0.3f)
                            frontMassResistance += neighborMass * dotMatch;
                    }
                }
                else if (math.lengthsq(driveDir) > 0.001f)
                {
                    float dot = math.dot(driveDir, pushDir);
                    if (dot < 0)
                    {
                        driveDir -= dot * pushDir;
                        if (math.lengthsq(driveDir) > 0.001f)
                            driveDir = math.normalizesafe(driveDir);
                        else
                        {
                            float2 tangent = new float2(-pushDir.y, pushDir.x);

                            // offsetFromObstacle 与 pushDir 同向，dot(offsetFromObstacle, tangent) 恒为 0，
                            // 改用目标方向投影到切线：选择最接近目标的绕路侧
                            float2 toTarget = cmd.p_targetPos - cmd.p_currentPos;
                            float tangentSide = math.dot(toTarget, tangent);

                            if (math.abs(tangentSide) < 0.0001f)
                                tangentSide = ((cmd.p_gridIndex & 1) == 0) ? 1f : -1f;

                            driveDir = math.normalizesafe(tangentSide > 0 ? tangent : -tangent);
                        }
                    }
                }
            }
        }
    }
}
