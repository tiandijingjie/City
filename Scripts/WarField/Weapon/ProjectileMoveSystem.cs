using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace WarField
{
    using WD = WeaponDefines;

    [BurstCompile]
    public partial struct ProjectileMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 依赖于统一的实体销毁系统，避免在遍历中直接销毁破坏内存结构
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // 获取 EntityCommandBuffer 用于安全地添加标签或销毁实体
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // 1. 调度纯直线移动的 Job
            var linearJob = new LinearMoveJob
            {
                p_dt = dt,
                p_ecb = ecb
            };
            state.Dependency = linearJob.ScheduleParallel(state.Dependency);

            // 2. 调度贝塞尔曲线移动的 Job
            var bezierJob = new BezierMoveJob
            {
                p_dt = dt,
                p_ecb = ecb
            };
            state.Dependency = bezierJob.ScheduleParallel(state.Dependency);
        }
    }

    // 处理强力击、激光等无目标弹道
    [BurstCompile]
    public partial struct LinearMoveJob : IJobEntity
    {
        public float p_dt;
        public EntityCommandBuffer.ParallelWriter p_ecb;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref WD.ProjectilePositionComponent pos, ref WD.LinearMoveComponent move)
        {
            move.p_movedDistance = move.p_movedDistance + (move.p_speed * p_dt);
            pos.p_position = pos.p_position + (move.p_direction * move.p_speed * p_dt);

            // 直接计算角度，供表现层旋转使用
            pos.p_rotationAngle = math.degrees(math.atan2(move.p_direction.y, move.p_direction.x));

            if (move.p_movedDistance >= move.p_maxDistance)
            {
                // 到达最大射程，打上销毁标签
                p_ecb.AddComponent<WD.ProjectileDestroyTag>(chunkIndex, entity);
            }
        }
    }

    // 处理弓箭、迫击炮弹等
    [BurstCompile]
    public partial struct BezierMoveJob : IJobEntity
    {
        public float p_dt;
        public EntityCommandBuffer.ParallelWriter p_ecb;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref WD.ProjectilePositionComponent pos, ref WD.BezierMoveComponent move)
        {
            if (move.p_progress >= 1.0f)
            {
                return;
            }

            move.p_progress = move.p_progress + (move.p_speed * p_dt);

            if (move.p_progress >= 1.0f)
            {
                move.p_progress = 1.0f;
                // 抵达终点，打上销毁标签（或者触发范围伤害的 Tag）
                p_ecb.AddComponent<WD.ProjectileDestroyTag>(chunkIndex, entity);
            }

            float t = move.p_progress;
            float u = 1.0f - t;

            // 二次贝塞尔公式的控制点 P1 (起点和终点的中点，加上最大高度)
            float2 p1 = new float2((move.p_startPos.x + move.p_endPos.x) * 0.5f, (move.p_startPos.y + move.p_endPos.y) * 0.5f + move.p_maxHeight);

            // B(t) = (1-t)^2 * P0 + 2t(1-t) * P1 + t^2 * P2
            float2 nextPos = (u * u * move.p_startPos) + (2.0f * u * t * p1) + (t * t * move.p_endPos);

            float2 dir = nextPos - pos.p_position;

            // 避免起始帧的 atan2(0,0) 计算错误
            if (math.lengthsq(dir) > 0.0001f)
            {
                pos.p_rotationAngle = math.degrees(math.atan2(dir.y, dir.x));
            }

            pos.p_position = nextPos;
        }
    }
}
