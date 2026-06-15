using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace WarField
{
    public class SpatialDefines
    {
        //用于给p_spec赋值,按位赋值   p_spec |= 1 << EntitySpecType
        //最大值必须 < 8
        public enum EntitySpecType
        {
            HIDE=0, //隐身
        }

        //判断WE.WarEleType中的物体是属于静态还是动态网络
        //return 1:static   2:dynamic   不存在同时查找两个网络的情况,因为会可能出现index的重复
        static public int CheckEletypeInGrid(int eleType)
        {
            switch ((WarFieldElements.WarEleType)eleType)
            {
                case WarFieldElements.WarEleType.BUILDING:
                case WarFieldElements.WarEleType.OBSTACLE:
                    return 1;
                case WarFieldElements.WarEleType.SOLDIER:
                    return 2;
                default:
                    return 0;
            }
        }
    }

    public struct GridEntityData
    {
        public float2 p_position;
        public float p_radius; //entity的碰撞体积大小
        public uint p_subType;
        public byte p_eleType; //WarFieldElements.WarEleType
        public byte p_mapId; // 所属地图ID
        public byte p_spec; //一些特殊的状态标记位 SpatialDefines.EntitySpecType, 按位赋值
        public bool p_isDead; // 死亡标记，用于帧末尾安全清理
    }

    public struct ResEntityData
    {
        // 空间与状态数据
        public float2 p_position;
        public float p_expirationTime; // 过期时间点
        public bool p_isTargeted;      // 是否已被某个农民锁定
        public bool p_isActive;        // 是否存活（用于底层数据池的复用判定）

        // 业务属性数据
        public ushort p_weight;           // 重量（用于农民计算容量：当前负重 + weight <= maxCapacity）
        public ushort p_value;            // 价值/数量（存入仓库时增加的全局资源量）
        public byte p_type;         // 资源类型

        // 索引数据
        public int p_poolIndex;        // 记录自身在 _resPool 中的固定索引，实现 O(1) 的修改
        public int p_nextTimeoutPoolIndex; // 时间轮静态链表指针：指向同一个时间片触发的下一个资源
    }

    [BurstCompile]
    public struct SpatialQueryHelper
    {
        [ReadOnly] public NativeArray<GridEntityData> p_entities;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> p_grid; //不需要关心到底是静态还是动态

        public float2 p_mapOrigin;
        public float p_cellSize;
        public int p_cols;
        public int p_rows;

        //查询最近的entity
        //excludeIndex: 需要排除的p_spatialGrid中index,一般是传入自身的index,用来排除查询者自己
        //subType ==-1 表示查询所有类型
        //sqrtRadius = radius * radius
        public int FindClosestEntity(float2 pos, float radius, float sqrtRadius, int eleType, int subType, int excludeIndex, byte exclude = 0, byte include = 0)
        {
            int bestIndex = -1;

            GetCellRange(pos, radius, out int minCol, out int maxCol, out int minRow, out int maxRow);

            float minDistanceSqr = float.MaxValue;
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    int cellIndex = row * p_cols + col;
                    if (p_grid.TryGetFirstValue(cellIndex, out int otherIndex, out var iteratorStatic))
                    {
                        do {
                            CheckClosest(otherIndex, pos, sqrtRadius, eleType, subType, excludeIndex, ref minDistanceSqr, ref bestIndex, exclude, include);
                        } while (p_grid.TryGetNextValue(out otherIndex, ref iteratorStatic));
                    }
                }
            }
            return bestIndex;
        }

        // 高效的实现剔除多个目标的(主要是给闪电链这种需要在一个范围内的eneity中挑选一部分出来)
        // TList原生的列表结构
        public int FindClosestEntity<TList>(float2 pos, float radius, float sqrtRadius, int eleType, int subType, ref TList excludeList, byte exclude = 0, byte include = 0)
            where TList : unmanaged, INativeList<int>
        {
            int bestIndex = -1;

            GetCellRange(pos, radius, out int minCol, out int maxCol, out int minRow, out int maxRow);
            float minDistanceSqr = float.MaxValue;
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    int cellIndex = row * p_cols + col;
                    if (p_grid.TryGetFirstValue(cellIndex, out int otherIndex, out var iteratorStatic))
                    {
                        do {
                            CheckClosest(otherIndex, pos, sqrtRadius, eleType, subType, ref excludeList, ref minDistanceSqr, ref bestIndex, exclude, include);
                        } while (p_grid.TryGetNextValue(out otherIndex, ref iteratorStatic));
                    }
                }
            }

            return bestIndex;
        }

        //因为返回值是NativeList, 所以只能是低频调用
        //返回的是一个范围内全部的entity
        //results需要在外部清空
        //excludeIndex==-1 表示不用剔除
        public void FindEntitiesInRange(float2 pos, float radius, float sqrtRadius, int eleType, int subType, int excludeIndex, ref NativeList<int> results,
            byte exclude = 0, byte include = 0)
        {
            GetCellRange(pos, radius, out int minCol, out int maxCol, out int minRow, out int maxRow);

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    int cellIndex = row * p_cols + col;
                    if (p_grid.TryGetFirstValue(cellIndex, out int otherIndex, out var iterator))
                    {
                        do
                        {
                            CheckAndAdd(otherIndex, pos, sqrtRadius, eleType, subType, excludeIndex, ref results, exclude, include);
                        } while (p_grid.TryGetNextValue(out otherIndex, ref iterator));
                    }
                }
            }
        }

        //使用FixedList可以高频的调用,最多返回127个结果
        //返回固定上限的 entity（容量由 FixedList 类型决定）
        //results需要在外部清空
        //excludeIndex==-1 表示不用剔除
        public void FindEntitiesInRange(float2 pos, float radius, float sqrtRadius, int eleType, int subType, int excludeIndex, ref FixedList512Bytes<int> results,
            byte exclude = 0, byte include = 0)
        {
            GetCellRange(pos, radius, out int minCol, out int maxCol, out int minRow, out int maxRow);
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    int cellIndex = row * p_cols + col;
                    if (p_grid.TryGetFirstValue(cellIndex, out int otherIndex, out var iterator))
                    {
                        do
                        {
                            CheckAndAdd(otherIndex, pos, sqrtRadius, eleType, subType, excludeIndex, ref results, exclude, include);
                        } while (p_grid.TryGetNextValue(out otherIndex, ref iterator));
                    }
                }
            }
        }

        //获取一个方形区域的entity
        //startPos,endPos 是方形区域的中心线
        //lineRadius是边长一半
        public void FindEntitiesInSegment(
            float2 startPos,
            float2 endPos,
            float lineRadius,
            int eleType,
            int subType,
            int excludeIndex,
            ref FixedList512Bytes<int> results,
            byte exclude = 0, byte include = 0)
        {
            float minX = math.min(startPos.x, endPos.x) - lineRadius;
            float maxX = math.max(startPos.x, endPos.x) + lineRadius;
            float minY = math.min(startPos.y, endPos.y) - lineRadius;
            float maxY = math.max(startPos.y, endPos.y) + lineRadius;

            //网格的行列范围
            int minCol = math.max(0, (int)math.floor((minX - p_mapOrigin.x) / p_cellSize));
            int maxCol = math.min(p_cols - 1, (int)math.floor((maxX - p_mapOrigin.x) / p_cellSize));
            int minRow = math.max(0, (int)math.floor((minY - p_mapOrigin.y) / p_cellSize));
            int maxRow = math.min(p_rows - 1, (int)math.floor((maxY - p_mapOrigin.y) / p_cellSize));

            // 提前计算线段向量，避免在循环内重复算
            float2 lineVec = endPos - startPos;
            float lineLenSqr = math.lengthsq(lineVec);

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    int cellIndex = row * p_cols + col;
                    if (p_grid.TryGetFirstValue(cellIndex, out int otherIndex, out var iterator))
                    {
                        do
                        {
                            CheckSegmentAndAdd(otherIndex, startPos, lineVec, lineLenSqr, lineRadius, eleType, subType, excludeIndex, ref results, exclude, include);
                        } while (p_grid.TryGetNextValue(out otherIndex, ref iterator));
                    }
                }
            }
        }

        //查找一个扇形区域
        public void FindEntitiesInSector(
            float2 center,
            float radius,
            float2 forwardDir,
            float cosHalfAngle,
            int eleType, int subType, int excludeIndex,
            ref FixedList512Bytes<int> results,
            byte exclude = 0, byte include = 0)
        {
            // 1. 和圆形一样，先用半径框出一个 AABB 粗筛网格
            int minCol = math.max(0, (int)math.floor((center.x - radius - p_mapOrigin.x) / p_cellSize));
            int maxCol = math.min(p_cols - 1, (int)math.floor((center.x + radius - p_mapOrigin.x) / p_cellSize));
            int minRow = math.max(0, (int)math.floor((center.y - radius - p_mapOrigin.y) / p_cellSize));
            int maxRow = math.min(p_rows - 1, (int)math.floor((center.y + radius - p_mapOrigin.y) / p_cellSize));

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    int cellIndex = row * p_cols + col;
                    if (p_grid.TryGetFirstValue(cellIndex, out int otherIndex, out var iterator))
                    {
                        do
                        {
                            CheckSectorAndAdd(otherIndex, center, radius, forwardDir, cosHalfAngle, eleType, subType, excludeIndex, ref results, exclude, include);
                        } while (p_grid.TryGetNextValue(out otherIndex, ref iterator));
                    }
                }
            }
        }

        private void GetCellRange(float2 pos, float radius, out int minCol, out int maxCol, out int minRow, out int maxRow)
        {
            minCol = math.max(0, (int)math.floor((pos.x - radius - p_mapOrigin.x) / p_cellSize));
            maxCol = math.min(p_cols - 1, (int)math.floor((pos.x + radius - p_mapOrigin.x) / p_cellSize));
            minRow = math.max(0, (int)math.floor((pos.y - radius - p_mapOrigin.y) / p_cellSize));
            maxRow = math.min(p_rows - 1, (int)math.floor((pos.y + radius - p_mapOrigin.y) / p_cellSize));
        }

        //eleType == -1 表示忽略类型
        //subType==-1 表示忽略subtype, 这个参数在输入时候是int,但是在实际比较的时候按照uint使用
        private void CheckAndAdd(int otherIndex, float2 pos, float maxSearchDistSqr, int eleType, int subType, int excludeIndex, ref NativeList<int> results,
            byte exclude, byte include)
        {
            if (otherIndex == excludeIndex)
                return;

            GridEntityData other = p_entities[otherIndex];
            if (other.p_isDead)
                return;

            if (eleType != -1 && other.p_eleType != eleType)
                return;
            if(IsSubTypeMatch(subType, other.p_subType) == false)
                return;
            if (exclude != 0 && (other.p_spec & exclude) != 0)
                return; // 包含了任意一个被排除的标记，直接剔除

            if (include != 0 && (other.p_spec & include) == 0)
                return; // 必须包含 include 中的至少一个标记，否则剔除

            if (math.distancesq(pos, other.p_position) <= maxSearchDistSqr)
            {
                // NativeList 自动扩容，直接 Add
                results.Add(otherIndex);
            }
        }

        // 给 FixedList 用的判定逻辑：
        // 当列表满时，会保留“距离更近”的结果，替换掉当前最远结果。
        //eleType == -1 表示忽略类型
        private void CheckAndAdd(int otherIndex, float2 pos, float maxSearchDistSqr, int eleType, int subType, int excludeIndex, ref FixedList512Bytes<int> results,
            byte exclude, byte include)
        {
            if (otherIndex == excludeIndex)
                return;

            GridEntityData other = p_entities[otherIndex];

            // 【逻辑删除过滤】：如果它是上个回合死掉但还没被清出静态网格的实体，直接无视它！
            if (other.p_isDead)
                return;

            if (eleType != -1 && other.p_eleType != eleType)
                return;
            if(IsSubTypeMatch(subType, other.p_subType) == false)
                return;
            if (exclude != 0 && (other.p_spec & exclude) != 0)
                return;
            if (include != 0 && (other.p_spec & include) == 0)
                return;

            float distSq = math.distancesq(pos, other.p_position);
            if (distSq > maxSearchDistSqr)
                return;

            if (results.Length < results.Capacity)
            {
                results.Add(otherIndex);
                return;
            }

            // 列表已满：仅当新目标更近时，替换最远目标。
            int farthestSlot = -1;
            float farthestDistSq = -1f;
            for (int i = 0; i < results.Length; i++)
            {
                float d = math.distancesq(pos, p_entities[results[i]].p_position);
                if (d > farthestDistSq)
                {
                    farthestDistSq = d;
                    farthestSlot = i;
                }
            }

            if (farthestSlot >= 0 && distSq < farthestDistSq)
                results[farthestSlot] = otherIndex;
        }

        //查找最近的一个
        //eleType == -1 表示忽略类型
        private void CheckClosest(int otherIndex, float2 pos, float maxSearchDistSqr, int eleType, int subType, int excludeIndex, ref float minDistanceSqr, ref int bestIndex,
            byte exclude, byte include)
        {
            if (otherIndex == excludeIndex)
                return;

            GridEntityData other = p_entities[otherIndex];

            if (other.p_isDead)
                return;

            if (eleType != -1 && other.p_eleType != eleType)
                return;
            if(IsSubTypeMatch(subType, other.p_subType) == false)
                return;
            if (exclude != 0 && (other.p_spec & exclude) != 0)
                return;
            if (include != 0 && (other.p_spec & include) == 0)
                return;

            float distSqr = math.distancesq(pos, other.p_position);
            if (distSqr <= maxSearchDistSqr && distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                bestIndex = otherIndex;
            }
        }

        //eleType == -1 表示忽略类型
        private void CheckClosest<TList>(int otherIndex, float2 pos, float maxSearchDistSqr, int eleType, int subType, ref TList excludeList, ref float minDistanceSqr,
            ref int bestIndex, byte exclude, byte include)
            where TList : unmanaged, INativeList<int>
        {
            for (int i = 0; i < excludeList.Length; i++)
            {
                if (excludeList[i] == otherIndex)
                    return;
            }

            GridEntityData other = p_entities[otherIndex];

            if (other.p_isDead)
                return;
            if (eleType != -1 && other.p_eleType != eleType)
                return;
            if(IsSubTypeMatch(subType, other.p_subType) == false)
                return;
            if (exclude != 0 && (other.p_spec & exclude) != 0)
                return;
            if (include != 0 && (other.p_spec & include) == 0)
                return;

            float distSqr = math.distancesq(pos, other.p_position);
            if (distSqr <= maxSearchDistSqr && distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                bestIndex = otherIndex;
            }
        }

        //计算otherIndex是不是在方形区域内
        private void CheckSegmentAndAdd(
            int otherIndex, float2 startPos, float2 lineVec, float lineLenSqr, float lineRadius,
            int eleType, int subType, int excludeIndex,
            ref FixedList512Bytes<int> results, byte exclude, byte include)
        {
            if (otherIndex == excludeIndex)
                return;

            GridEntityData other = p_entities[otherIndex];
            if (other.p_isDead)
                return;
            if (eleType != -1 && other.p_eleType != eleType)
                return;
            if(IsSubTypeMatch(subType, other.p_subType) == false)
                return;
            if (exclude != 0 && (other.p_spec & exclude) != 0)
                return;
            if (include != 0 && (other.p_spec & include) == 0)
                return;

            // --- 核心数学：点到线段的最短距离 ---
            float2 pointVec = other.p_position - startPos;

            // 投影求比例 t，并限制在 [0, 1] 之间（确保不会超出线段两端）
            float t = 0;
            if (lineLenSqr > 0.0001f) // 防止起点终点重合除以0
            {
                t = math.clamp(math.dot(pointVec, lineVec) / lineLenSqr, 0f, 1f);
            }

            // 线段上距离目标最近的点
            float2 closestPoint = startPos + t * lineVec;

            // 计算距离平方
            float distSqr = math.distancesq(other.p_position, closestPoint);

            // 容差半径：技能宽度 + 目标自身的碰撞半径
            float totalRadius = lineRadius + other.p_radius;

            if (distSqr <= totalRadius * totalRadius)
            {
                if (results.Length < results.Capacity)
                {
                    results.Add(otherIndex);
                }
                else
                {
                    // 如果你希望被箭射中的人有数量上限（比如只能穿透5个人），
                    // 可以在这里写替换逻辑，优先保留距离 startPos 更近的目标。
                    // 篇幅原因省略替换逻辑，可参考你之前圆形的 CheckAndAdd 写法。
                }
            }
        }

        private void CheckSectorAndAdd(
            int otherIndex, float2 center, float radius, float2 forwardDir, float cosHalfAngle,
            int eleType, int subType, int excludeIndex,
            ref FixedList512Bytes<int> results, byte exclude, byte include)
        {
            if (otherIndex == excludeIndex)
                return;

            GridEntityData other = p_entities[otherIndex];
            if (other.p_isDead)
                return;
            if (eleType != -1 && other.p_eleType != eleType)
                return;
            if(IsSubTypeMatch(subType, other.p_subType) == false)
                return;
            if (exclude != 0 && (other.p_spec & exclude) != 0)
                return;
            if (include != 0 && (other.p_spec & include) == 0)
                return;

            // --- 1. 距离过滤 ---
            float2 vecToTarget = other.p_position - center;
            float distSq = math.lengthsq(vecToTarget);

            // 把怪物的碰撞半径算进去（只要怪物边缘碰到扇形就算命中）
            float expandedRadius = radius + other.p_radius;
            if (distSq > expandedRadius * expandedRadius)
                return;

            // 容错保护：如果怪物几乎和施法者重叠（中心点非常近），为了防止 normalize 报错或打不到贴身怪，直接算命中
            if (distSq < 0.0001f)
            {
                if (results.Length < results.Capacity) results.Add(otherIndex);
                return;
            }

            // --- 2. 角度过滤 (极致优化的点乘判定) ---
            // 归一化怪物方向
            float2 dirToTarget = math.normalizesafe(vecToTarget);

            // 点乘结果就是夹角的 cos 值
            float dotProduct = math.dot(dirToTarget, forwardDir);

            // 因为 cos 函数在 [0, 180度] 是递减的：
            // 所以如果算出来的 cos 值 >= 半角的 cos 值，说明实际夹角 <= 半角，即在扇形内部！
            // (额外优化：这里其实可以根据怪物的半径稍微放宽一点角度补偿，但在大部分 2D/2.5D 游戏里，基于中心点的角度判定配合上方基于边缘的距离判定，手感已经足够完美了)
            if (dotProduct >= cosHalfAngle)
            {
                if (results.Length < results.Capacity)
                {
                    results.Add(otherIndex);
                }
                else
                {
                    // 如果超出列表容量的替换逻辑...
                }
            }
        }

        // 0~7    race
        // 8~15   solider troop   building bdMode
        // 16~24  solider/building subtype
        // 25~31  solider status
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool IsSubTypeMatch(int targetSubType, uint otherSubType)
        {
            if (targetSubType == -1)
                return true; // -1 表示完全忽略 SubType 检查

            uint t = (uint)targetSubType;

            // 独立的高 8 位匹配 (24~31位)
            byte tB3 = (byte)((t >> 24) & 0xFF);
            if (tB3 != 0 && tB3 != 0xFF) // 0 或 0xFF 为通配符，跳过检查
            {
                byte oB3 = (byte)((otherSubType >> 24) & 0xFF);
                if (tB3 != oB3) // 如果不是通配且不相等，直接失败
                    return false;
            }

            // 0~7 位先匹配
            byte tB0 = (byte)(t & 0xFF);
            if (tB0 == 0xFF) return true; // 输入 0xFF 直接成功，不再检查后续细分类型
            byte oB0 = (byte)(otherSubType & 0xFF);
            if (tB0 != oB0) return false; // 不相等则匹配失败

            // 8~15 位再匹配
            byte tB1 = (byte)((t >> 8) & 0xFF);
            if (tB1 == 0xFF) return true; // 输入 0xFF 直接成功
            byte oB1 = (byte)((otherSubType >> 8) & 0xFF);
            if (tB1 != oB1) return false;

            // 16~23 位最后匹配
            byte tB2 = (byte)((t >> 16) & 0xFF);
            if (tB2 == 0xFF) return true; // 输入 0xFF 直接成功
            byte oB2 = (byte)((otherSubType >> 16) & 0xFF);
            if (tB2 != oB2) return false;

            return true;
        }
    }

    //每个需要放入spatial grid并且可以增删的obj都需要继承这个接口
    public interface IGridNode
    {
        int gs_gridIndex { get; set; }
    }

    // 专用于可拾取资源 (ResEntityData) 的空间查询辅助结构
    // 与 SpatialQueryHelper 并列，由 SpatialGridManager.GetResQueryHelper 构建
    public struct ResQueryHelper
    {
        [ReadOnly] public NativeArray<ResEntityData> p_resPool;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> p_resGrid;

        public float2 p_mapOrigin;
        public float p_cellSize;
        public int p_cols;
        public int p_rows;

        /// <summary>
        /// 扩展环搜索：在整张地图上找距离 queryPos 最近的可用（active 且未被锁定）资源。
        /// resType == 0 表示忽略类型，否则仅匹配 ResEntityData.p_type。
        /// 返回 _resPool 中的索引；未找到返回 -1。
        ///
        /// 算法：从 queryPos 所在格开始向外逐环扩展（Chebyshev 环）。
        /// 当下一环的最近可能距离 ≥ 当前最优距离时提前退出，保证 O(k) 而非 O(all_cells)。
        /// </summary>
        public int FindClosest(float2 queryPos, byte resType = 0)
        {
            int bestIndex = -1;
            float minDistSq = float.MaxValue;

            int centerCol = math.clamp((int)math.floor((queryPos.x - p_mapOrigin.x) / p_cellSize), 0, p_cols - 1);
            int centerRow = math.clamp((int)math.floor((queryPos.y - p_mapOrigin.y) / p_cellSize), 0, p_rows - 1);

            int maxRing = math.max(p_cols, p_rows); // 最多扩展到覆盖整张地图

            for (int ring = 0; ring <= maxRing; ring++)
            {
                // 环 ring 的格子与查询点的最近可能距离 >= (ring-1)*cellSize
                // 一旦这个下界已超过当前最优距离，后续环不可能更优，提前退出
                if (ring > 1)
                {
                    float nearestPossible = (ring - 1) * p_cellSize;
                    if (nearestPossible * nearestPossible > minDistSq)
                        break;
                }

                int rMin = centerRow - ring;
                int rMax = centerRow + ring;
                int cMin = centerCol - ring;
                int cMax = centerCol + ring;

                for (int row = math.max(0, rMin); row <= math.min(p_rows - 1, rMax); row++)
                {
                    for (int col = math.max(0, cMin); col <= math.min(p_cols - 1, cMax); col++)
                    {
                        // ring > 0 时只访问边框格子，内部已在更小的环中检查过
                        if (ring > 0 && row > rMin && row < rMax && col > cMin && col < cMax)
                            continue;

                        int cellIndex = row * p_cols + col;
                        if (!p_resGrid.TryGetFirstValue(cellIndex, out int poolIndex, out var it))
                            continue;

                        do
                        {
                            ResEntityData data = p_resPool[poolIndex];
                            if (!data.p_isActive || data.p_isTargeted)
                                continue;
                            if (resType != 0 && data.p_type != resType)
                                continue;

                            float distSq = math.distancesq(queryPos, data.p_position);
                            if (distSq < minDistSq)
                            {
                                minDistSq = distSq;
                                bestIndex = poolIndex;
                            }
                        } while (p_resGrid.TryGetNextValue(out poolIndex, ref it));
                    }
                }
            }

            return bestIndex;
        }
    }
}
