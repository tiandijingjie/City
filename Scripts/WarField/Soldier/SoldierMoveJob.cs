using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    public struct SoldierMoveCmd
    {
        public float2 p_currentPos;    // 当前位置
        public int p_gridIndex;        // SpatialGrid 中自身索引
        public float p_moveSpeed;      // 当前移速
        public float p_mass;           // 质量
        public float p_radius;         // 碰撞半径
        public byte p_status;          // 当前状态 (SoldierDefines.SoldierStatus)
        public float2 p_targetPos;     // 追击目标的坐标
        public float p_homeY;          // Y轴弹性锚点
        public float p_homeYBackMul;    // 回归homeY的速度系数
        public float2 p_desiredDir;  //期望的方向, a*或者movetorival的时候在soldier里面计算的 否则是在jobs中计算的
        public byte p_moveCmd;         //SD.MoveCmd
        public int p_flowIndex;
    }

    public struct SoldierMoveFlowContext
    {
        [ReadOnly] public NativeArray<float2> p_flowFieldPool; //整个pool
        public int p_flowIndex;     // 当前单位归属的流场 ID (0:友军全局, 1:敌军全局, 2~15:局部)
        public int p_totalCells;    // 单个流场的大小 (cols * rows)

        public byte p_faction;
        public float p_flowCellSize;
        public float2 p_flowMapOrigin;
        public int p_flowCols;
        public int p_flowRows;
        public float2 p_flowMapMax;
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct SoldierMoveJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<SoldierMoveCmd> p_moveCmds;

        [ReadOnly] public NativeArray<float2> p_flowFieldPool;
        public byte p_faction;
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
            SoldierMoveCmd cmd = p_moveCmds[index];
            SoldierMoveFlowContext flow = CreateFlowContext();
            float2 driveDir = SoldierMovePhysics.ComputeAutoDriveDir(cmd, cmd.p_desiredDir, flow);
            float3 nextPos = SoldierMovePhysics.ComputeFinalPosition(cmd, driveDir, p_dynamicQuery, p_staticQuery, flow, p_deltaTime);
            if (math.isfinite(nextPos.x) && math.isfinite(nextPos.y))
                transform.position = nextPos;
        }

        private SoldierMoveFlowContext CreateFlowContext()
        {
            return new SoldierMoveFlowContext
            {
                p_flowFieldPool = p_flowFieldPool,
                p_faction = p_faction,
                p_flowCellSize = p_flowCellSize,
                p_flowMapOrigin = p_flowMapOrigin,
                p_flowCols = p_flowCols,
                p_flowRows = p_flowRows,
                p_flowMapMax = p_flowMapMax
            };
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public static class SoldierMovePhysics
    {
       public static float2 ComputeAutoDriveDir(in SoldierMoveCmd cmd, float2 baseDriveDir, in SoldierMoveFlowContext flow)
        {
            if (cmd.p_status != (byte)SD.SoldierStatus.MOVE)
                return float2.zero;

            // 直接使用主线程已经算好的宏观流场方向
            float2 flowDir = baseDriveDir;
            bool isManualMove = cmd.p_moveCmd == (byte)SD.MoveCmd.MANMOVETOPOS || cmd.p_moveCmd == (byte)SD.MoveCmd.MANMOVEATTACKTOPOS;

            if (isManualMove)
            {
                float distToTargetSq = math.distancesq(cmd.p_currentPos, cmd.p_targetPos);
                // 当距离手动终点小于 1.5 倍格子大小时，直接脱离流场及任何边界逃逸的束缚
                // 强制采用最纯粹的微观直线 Steering 引力驱动，直插圆心
                float proximityThreshold = flow.p_flowCellSize * 1.5f;
                if (distToTargetSq < proximityThreshold * proximityThreshold)
                {
                    float2 directDir = cmd.p_targetPos - cmd.p_currentPos;
                    if (math.lengthsq(directDir) > 0.001f)
                        return math.normalizesafe(directDir);
                    return float2.zero;
                }
            }

            if (cmd.p_moveCmd == (byte)SD.MoveCmd.NORMAL ||
                (cmd.p_moveCmd == (byte)SD.MoveCmd.MANMOVETOPOS && cmd.p_flowIndex >= WE.LocalFlowFieldStartId) ||
                (cmd.p_moveCmd == (byte)SD.MoveCmd.MANMOVEATTACKTOPOS && cmd.p_flowIndex >= WE.LocalFlowFieldStartId))
            {
                int cellC = (int)math.floor((cmd.p_currentPos.x - flow.p_flowMapOrigin.x) / flow.p_flowCellSize);
                int cellR = (int)math.floor((cmd.p_currentPos.y - flow.p_flowMapOrigin.y) / flow.p_flowCellSize);

                cellC = math.clamp(cellC, 0, flow.p_flowCols - 1);
                cellR = math.clamp(cellR, 0, flow.p_flowRows - 1);

                int totalCells = flow.p_flowCols * flow.p_flowRows;
                flowDir = flow.p_flowFieldPool[cmd.p_flowIndex * totalCells + cellR * flow.p_flowCols + cellC];
            }

            if (math.abs(flowDir.x) + math.abs(flowDir.y) < 0.001f)
                flowDir = FindEscapeFlowDir(cmd.p_currentPos, cmd.p_targetPos, cmd.p_flowIndex, flow);

            float2 correctiveDir = float2.zero;

            // 严格限制：只有在 NORMAL 指令下才激活 homeY 弹性路径累加受力
            if (cmd.p_moveCmd == (byte)SD.MoveCmd.NORMAL)
            {
                float deltaY = cmd.p_homeY - cmd.p_currentPos.y;

                if (math.abs(deltaY) > 0.01f) // 防止零点几微米的轻微抖动
                {
                    int cellC = (int)math.floor((cmd.p_currentPos.x - flow.p_flowMapOrigin.x) / flow.p_flowCellSize);
                    int cellR = (int)math.floor((cmd.p_currentPos.y - flow.p_flowMapOrigin.y) / flow.p_flowCellSize);
                    cellC = math.clamp(cellC, 0, flow.p_flowCols - 1);
                    cellR = math.clamp(cellR, 0, flow.p_flowRows - 1);

                    int totalCells = flow.p_flowCols * flow.p_flowRows;
                    int stepY = (int)math.sign(deltaY);
                    int nextR = cellR + stepY;

                    //方向上的紧邻下一个格子是障碍物不计算回归力
                    bool immediateObstacle = false;
                    if (nextR < 0 || nextR >= flow.p_flowRows)
                    {
                        immediateObstacle = true;
                    }
                    else
                    {
                        int nextIndex = cmd.p_flowIndex * totalCells + nextR * flow.p_flowCols + cellC;
                        if (nextIndex >= 0 && nextIndex < flow.p_flowFieldPool.Length)
                        {
                            if (math.lengthsq(flow.p_flowFieldPool[nextIndex]) < 0.001f)
                                immediateObstacle = true;
                        }
                    }

                    if (!immediateObstacle)
                    {
                        int homeCellR = (int)math.floor((cmd.p_homeY - flow.p_flowMapOrigin.y) / flow.p_flowCellSize);
                        homeCellR = math.clamp(homeCellR, 0, flow.p_flowRows - 1);

                        float totalPathResistance = 0f;
                        int currentCheckR = nextR;

                        //沿垂直路径累加计算直到 homeY 格子的综合阻力
                        while (true)
                        {
                            int checkIndex = cmd.p_flowIndex * totalCells + currentCheckR * flow.p_flowCols + cellC;
                            if (checkIndex >= 0 && checkIndex < flow.p_flowFieldPool.Length)
                            {
                                float2 cFlow = flow.p_flowFieldPool[checkIndex];
                                if (math.lengthsq(cFlow) < 0.001f)
                                {
                                    totalPathResistance += 2.5f; // 路径内部包含障碍物：加一个比较大的值
                                }
                                else
                                {
                                    // 累加流场对回归的逆向抗力
                                    float flowResistance = -cFlow.y * stepY;
                                    if (flowResistance > 0f)
                                    {
                                        totalPathResistance += flowResistance * 1.5f;
                                    }
                                }
                            }

                            if (currentCheckR == homeCellR)
                                break;

                            currentCheckR += stepY;
                            if (currentCheckR < 0 || currentCheckR >= flow.p_flowRows)
                                break;
                        }

                        float originalRegression = math.abs(deltaY) * cmd.p_homeYBackMul;
                        float netRegressionMagnitude = originalRegression - (totalPathResistance * 0.4f);

                        if (netRegressionMagnitude > 0f)
                        {
                            correctiveDir.y = stepY * netRegressionMagnitude;
                        }
                    }
                }
            }

            // 流场垂直偏角接近或超过 45 度（极速变轨）
            // 引入 -0.05f 容差，完美捕捉 45 度对角线（如 (-0.707, -0.707)）及以上的所有偏转倾向
            if (math.abs(flowDir.y) >= math.abs(flowDir.x) - 0.05f)
            {
                // 如果回归引力的方向与流场垂直引导的方向完全相反
                if (flowDir.y * correctiveDir.y < 0f)
                {
                    // 回归力绝对不能逆转或反向吞掉流场的垂直分量！
                    // 将其最大强度卡死在流场 Y 轴分量的 0.5 倍以内，确保混合后的总 Y 轴受力方向依然跟随流场
                    float maxAllowedCorrective = math.abs(flowDir.y) * 0.5f;
                    if (math.abs(correctiveDir.y) > maxAllowedCorrective)
                    {
                        correctiveDir.y = math.sign(correctiveDir.y) * maxAllowedCorrective;
                    }
                }
            }

            // 混合驱动方向并进行安全的归一化
            float2 combinedDir = flowDir + correctiveDir;
            if (math.lengthsq(combinedDir) > 0.001f)
                return math.normalizesafe(combinedDir);

            return float2.zero;
        }

        public static float3 ComputeFinalPosition(
            in SoldierMoveCmd cmd,
            float2 driveDir,
            SpatialQueryHelper dynamicQuery,
            SpatialQueryHelper staticQuery,
            in SoldierMoveFlowContext flow,
            float deltaTime)
        {
            float2 originalDriveDir = driveDir;
            float2 avoidForce = float2.zero;  //受到的斥力
            float frontMassResistance = 0f; //前方的阻力

            float myEffectiveMass = cmd.p_mass;
            if (cmd.p_status == (byte)SD.SoldierStatus.ATTACKTATGET || cmd.p_status == (byte)SD.SoldierStatus.IDLE)
                myEffectiveMass *= 2.0f;

            float searchRadius = cmd.p_radius * 3.0f;
            float searchRadiusSqr = searchRadius * searchRadius;

            bool isBlockedByDynamic = false;
            bool isBlockedByStatic = false;

            FixedList512Bytes<int> dynamicNeighbors = new FixedList512Bytes<int>();
            dynamicQuery.FindEntitiesInRange(cmd.p_currentPos, searchRadius, searchRadiusSqr, (int)WE.WarEleType.SOLDIER, -1, cmd.p_gridIndex, ref dynamicNeighbors);

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
			    // 上限设为 1.2f (重叠越大推力越大，但平滑收敛)
			    float maxAvoidForce = 1.2f;
			    float softLength = maxAvoidForce * avoidForceLength / (maxAvoidForce + avoidForceLength);
			    avoidForce = (avoidForce / avoidForceLength) * softLength;
			}

            float2 expectedMove = originalDriveDir * cmd.p_moveSpeed * deltaTime;
            float2 finalMove = (driveDir * cmd.p_moveSpeed * deltaTime * speedMultiplier) + avoidForce;

            float expectedMoveSq = math.lengthsq(expectedMove);
            float progress = math.dot(finalMove, expectedMove);
            bool isStuck = expectedMoveSq > 0.0001f && progress < (expectedMoveSq * 0.1f);
    		// 提前计算基于单位唯一ID的相位偏移，用于打破对称性死锁
    		float phaseOffset = cmd.p_gridIndex * 1.37f;

            if (isStuck)
            {
                if (isBlockedByStatic)
                {
                    float2 escapeDir = FindEscapeFlowDir(cmd.p_currentPos,cmd.p_targetPos, cmd.p_flowIndex, flow);
                    if (math.lengthsq(escapeDir) > 0.001f)
                    {
                        float2 escapeNorm = math.normalizesafe(escapeDir);
                        float avoidDot = math.dot(avoidForce, escapeNorm);
                        if (avoidDot < 0)
                            avoidForce -= avoidDot * escapeNorm;

                        finalMove = (escapeNorm * cmd.p_moveSpeed * deltaTime * 0.9f) + avoidForce;
                    }
                }
                else if (isBlockedByDynamic)
                {
			        //动态障碍物卡死时，引入唯一相位的抖动
			        float2 jitter = new float2(
			            math.sin(cmd.p_currentPos.y * 100f + phaseOffset),
			            math.cos(cmd.p_currentPos.x * 100f + phaseOffset)
			        ) * 0.01f;
                    finalMove = (originalDriveDir * cmd.p_moveSpeed * deltaTime * 0.8f) + avoidForce + jitter;
                }
            }
            else if (math.lengthsq(finalMove) < 0.001f && expectedMoveSq > 0.01f)
            {
		        // 常规微小移动卡死时，引入唯一相位的抖动
		        float2 fallbackJitter = new float2(
		            math.sin(cmd.p_currentPos.y * 50f + phaseOffset),
		            math.cos(cmd.p_currentPos.x * 50f + phaseOffset)
		        ) * 0.02f;

		        finalMove += fallbackJitter;
            }

            float3 nextPos = new float3(cmd.p_currentPos.x + finalMove.x, cmd.p_currentPos.y + finalMove.y, 0);
            nextPos.x = math.clamp(nextPos.x, flow.p_flowMapOrigin.x, flow.p_flowMapMax.x);
            nextPos.y = math.clamp(nextPos.y, flow.p_flowMapOrigin.y, flow.p_flowMapMax.y);
            nextPos.z = WarFieldUtil.GetZByY(nextPos.y, flow.p_flowMapOrigin.y); //将Y映射到z
            return nextPos;
        }

        private static float2 FindEscapeFlowDir(float2 currentPos, float2 targetPos, int flowIndex, in SoldierMoveFlowContext flow)
        {
            float2 escapeDir = float2.zero;
            int validExits = 0;
            int totalCells = flow.p_flowCols * flow.p_flowRows;

            // 提前计算好终点所在的网格坐标
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
                    // 必须读自己所属的流场,而不是固定从 0 号(友军默认)流场拿
                    int pIndex = flowIndex * totalCells + pR * flow.p_flowCols + pC;
                    float2 pFlow = float2.zero;

                    if (pIndex >= 0 && pIndex < flow.p_flowFieldPool.Length)
                        pFlow = flow.p_flowFieldPool[pIndex];
                    // 如果探查的格子就是目标终点格子，哪怕它的流场强度是 0，它也是绝对合法的出口！
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

            return (flow.p_faction == (byte)WE.FactionType.FRIENDLY) ? new float2(1, 0) : new float2(-1, 0);
        }

        private static void CalculatePushForce(in SoldierMoveCmd cmd, GridEntityData neighbor, float myEffectiveMass, ref float2 driveDir, bool isStatic,
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
                if (isStatic == false)
                {
                    byte status = (byte)(neighbor.p_subType >> 24);
                    if (status == (byte)SD.SoldierStatus.ATTACKTATGET || status == (byte)SD.SoldierStatus.IDLE)
                        neighborMass *= 2.0f;
                }

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
                            // driveDir 与 pushDir 完全反向,投影后归零。
                            // 用"士兵相对障碍物在切向上的偏移"来决定绕路方向:
                            // 已经偏在哪一侧就继续往那一侧绕,实现自然分流,避免一堆士兵被同时扭到同一侧。
                            float2 tangent = new float2(-pushDir.y, pushDir.x);
                            float2 offsetFromObstacle = cmd.p_currentPos - neighbor.p_position;
                            float tangentSide = math.dot(offsetFromObstacle, tangent);

                            // 几乎正对着(切向偏移≈0)的情况下,用 gridIndex 的奇偶性打破对称
                            if (math.abs(tangentSide) < 0.0001f)
                                tangentSide = ((cmd.p_gridIndex & 1) == 0) ? 1f : -1f;

                            driveDir = math.normalizesafe(tangentSide > 0 ? tangent : -tangent);
                        }
                    }
                }
            }
        }

        private static bool FlowFieldOkForHomeYBias(float2 f)
        {
            if (math.abs(f.x) + math.abs(f.y) < 0.001f)
                return false;
            if (math.abs(f.y) < 0.28f)
                return true;
            if (math.abs(f.x) >= math.abs(f.y) * 0.52f) // tan(27.5) 流场中横向至少是纵向的52%
                return true;
            return false;
        }
    }
}
