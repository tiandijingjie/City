using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace WarField
{
    public class SearchDefines
    {
        public enum SearchShapeType : byte
        {
            MIN,
            CIRCLE,  //圆形
            SEGMENT,  // 线段/矩形（带有宽度的线）
            SECTOR, //扇形
            MAX,
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct SearchShapeDef  //共用体 节省空间
    {
        [FieldOffset(0)] public SearchDefines.SearchShapeType p_shapeType;

        // --- 共用数据 (所有形状都有起点/圆心) ---
        [FieldOffset(4)] public float2 p_centerOrStartPos;

        // 下方数据按形状互相重叠 (FieldOffset 从 12 开始)

        // 【Circle 专属】
        [FieldOffset(12)] public float p_radius;
        [FieldOffset(16)] public float p_radiusSq;

        // 【Segment 专属】
        [FieldOffset(12)] public float p_widthRadius;
        [FieldOffset(16)] public float p_widthRadiusSq;
        [FieldOffset(20)] public float2 p_endPos;

        // 【Sector 专属】
        [FieldOffset(12)] public float p_sectorRadius;
        [FieldOffset(16)] public float p_sectorRadiusSq;
        // 扇形的朝向必须是归一化的向量 (Normalized Vector)
        [FieldOffset(20)] public float2 p_forwardDir;
        // 扇形夹角一半的余弦值，例如 90度扇形，半角45度，存 cos(45度) = 0.707
        [FieldOffset(28)] public float p_cosHalfAngle;

        public static SearchShapeDef CreateCircle(float2 center, float radius)
        {
            return new SearchShapeDef
            {
                p_shapeType = SearchDefines.SearchShapeType.CIRCLE,
                p_centerOrStartPos = center,
                p_radius = radius,
                p_radiusSq = radius * radius
            };
        }

        public static SearchShapeDef CreateSegment(float2 start, float2 end, float width)
        {
            return new SearchShapeDef
            {
                p_shapeType = SearchDefines.SearchShapeType.SEGMENT,
                p_centerOrStartPos = start,
                p_endPos = end,
                p_widthRadius = width,
                p_widthRadiusSq = width * width
            };
        }

        // halfAngleCos: 扇形的总夹角一半角度的cos
        public static SearchShapeDef CreateSector(float2 center, float radius, float2 forwardDir, float halfAngleCos)
        {
            return new SearchShapeDef
            {
                p_shapeType = SearchDefines.SearchShapeType.SECTOR,
                p_centerOrStartPos = center,
                p_sectorRadius = radius,
                p_sectorRadiusSq = radius * radius,
                p_forwardDir = math.normalize(forwardDir), // 确保朝向是归一化的
                p_cosHalfAngle = halfAngleCos
            };
        }
    }

    // 纯值类型，用于 Job 过滤
    public struct SearchCondition : System.IEquatable<SearchCondition>
    {
        public byte p_targetEleType; // 必须要有具体的类型, 要不然在spatial grid那边会不知道查询哪个grid
        public int p_targetSubType; // -1 表示忽略
        public byte p_includeFlags;
        public byte p_excludeFlags;

        public bool Equals(SearchCondition other)
        {
            return p_targetEleType == other.p_targetEleType && p_targetSubType == other.p_targetSubType;
        }
        public override bool Equals(object obj)
        {
            return obj is SearchCondition other && Equals(other);
        }

        public override int GetHashCode()
        {
            // 判定依据是 EleType 和 SubType，所以 HashCode 只需要包含这两个
            return System.HashCode.Combine(p_targetEleType, p_targetSubType);
        }
    }

    // 完成查找的回调
    public delegate void OnClosestTargetFound(IGridNode target, float distance);
    public delegate void OnAreaTargetsFound(List<IGridNode> targets); //targets不能持有,必须拷贝到调用者的一个自定义的list去,targets在函数返回之后就会被clear
    //获取查找范围和形状的回调
    public delegate SearchShapeDef GetSearchShape();
    // 获取需要排除的 entity index列表 (soldier的黑名单机制)
    public delegate void GetExcludeIndices(ref Unity.Collections.FixedList64Bytes<int> excludeList);

    public class SearchBase
    {
        public bool p_isEnabled;
        public float p_duration; // 0:表示马上返回, -1:表示无限
        public List<SearchCondition> p_conditions; //最多存放十个,是SearchCmd中p_conditions的大小决定的
        public GetSearchShape p_getShapeCall; //获取查找的范围
        public GetExcludeIndices p_getExcludeCall;
        public IGridNode p_ownerNode;
        public int p_mapId;

        public SearchBase(float duration, GetSearchShape getShapeCall, IGridNode ownerNode, int mapId)
        {
            p_isEnabled = true;
            p_duration = duration;
            p_conditions = new List<SearchCondition>();
            p_getShapeCall = getShapeCall;
            p_ownerNode = ownerNode;
            p_mapId = mapId;
        }

        //必须要确保condition查找出来的结果不会有重复,因为在输出结果是没有去重, 去重比较慢
        public void AddCondition(SearchCondition cond)
        {
            int cnt = p_conditions.Count;
            if (cnt >= 10) //最多存放十个,是SearchCmd中p_conditions的大小决定的
            {
                GameLogger.LogError($"Too many conditions: {cnt}");
                return;
            }

            for (int i = 0; i < cnt; i++)
            {
                var c = p_conditions[i];
                if (c.Equals(cond))
                {
                    GameLogger.LogError($"[Search] Duplicate condition added: Type {cond.p_targetEleType}, SubType {cond.p_targetSubType}");
                    return;
                }
            }
            p_conditions.Add(cond);
        }
    }

    // 找最近的单体
    public class SearchClosest : SearchBase
    {
        private OnClosestTargetFound _callback;

        public SearchClosest(float duration, OnClosestTargetFound callback, GetSearchShape getShapeCall, IGridNode ownerNode, int mapId)
            : base(duration, getShapeCall, ownerNode, mapId)
        {
            _callback = callback;
        }

        public void TriggerCallback(IGridNode target, float distance)
        {
            _callback?.Invoke(target, distance);
        }
    }

    // 区域查找器 (找范围内所有)
    //callback 中返回的 targets不能持有,必须拷贝到调用者的一个自定义的list去,targets在函数返回之后就会被clear
    public class SearchArea : SearchBase
    {
        private OnAreaTargetsFound _callback;

        public SearchArea(float duration, OnAreaTargetsFound callback, GetSearchShape getShapeCall, IGridNode ownerNode, int mapId)
            : base(duration, getShapeCall, ownerNode, mapId)
        {
            _callback = callback;
        }

        public void TriggerCallback(List<IGridNode> targets)
        {
            _callback?.Invoke(targets);
        }
    }

    // 将 LayerMask 查询条件集中为 SearchCondition，供 RegisterSearch 使用
    public static class SearchConditionUtil
    {
        private static readonly byte HideExcludeFlag = (byte)(1 << (int)SpatialDefines.EntitySpecType.HIDE);

        public static void AddEnemySoldierConditions(SearchBase searcher)
        {
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.SOLDIER,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Oculars, (byte)0xff, (byte)0xff, (byte)0xff),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.SOLDIER,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Neutral, (byte)0xff, (byte)0xff, (byte)SoldierDefines.SoldierStatus.MOVE),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.SOLDIER,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Neutral, (byte)0xff, (byte)0xff, (byte)SoldierDefines.SoldierStatus.ATTACKTATGET),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.SOLDIER,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Neutral, (byte)0xff, (byte)0xff, (byte)SoldierDefines.SoldierStatus.INTERRUPT),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.SOLDIER,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Neutral, (byte)0xff, (byte)0xff, (byte)SoldierDefines.SoldierStatus.RELEASESKILL),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
        }

        public static void AddEnemyBuildingCondition(SearchBase searcher)
        {
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.BUILDING,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Oculars, (byte)0xff, (byte)0xff, (byte)0xff),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
        }

        public static void AddEnemySoldierAndBuildingConditions(SearchBase searcher)
        {
            AddEnemySoldierConditions(searcher);
            AddEnemyBuildingCondition(searcher);
        }

        public static void AddFriendlySoldierCondition(SearchBase searcher)
        {
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.SOLDIER,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Human, (byte)0xff, (byte)0xff, (byte)0xff),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
        }

        public static void AddFriendlyBuildingCondition(SearchBase searcher)
        {
            searcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WarFieldElements.WarEleType.BUILDING,
                p_targetSubType = (int)WarFieldElements.EncodeEntitySubType((byte)WarFieldElements.RaceType.Human, (byte)0xff, (byte)0xff, (byte)0xff),
                p_includeFlags = 0,
                p_excludeFlags = HideExcludeFlag,
            });
        }
    }
}
