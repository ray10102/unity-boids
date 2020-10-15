#if UNITY_EDITOR

using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Samples.Boids
{
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [ConverterVersion("macton", 5)]
    public class BoidConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((BoidAuthoring boidAuthoring) =>
            {
                var entity = GetPrimaryEntity(boidAuthoring);

                DstEntityManager.AddSharedComponentData(entity, new Boid
                {
                    CellRadius = boidAuthoring.CellRadius,
                    SeparationWeight = boidAuthoring.SeparationWeight,
                    AlignmentWeight = boidAuthoring.AlignmentWeight,
                    TargetWeight = boidAuthoring.TargetWeight,
                    OuterDetectionRadius = boidAuthoring.OuterDetectionRadius,
                    InnerDetectionRadius = boidAuthoring.InnerDetectionRadius,
                    MoveSpeed = boidAuthoring.MoveSpeed,
                    WanderRadius = boidAuthoring.WanderRadius,
                    WanderWeight = boidAuthoring.WanderWeight,
                    VisionAngle = boidAuthoring.VisionAngle,
                    NavigationRayCount = boidAuthoring.NavigationRayCount,
                });
                DynamicBuffer<Float3BufferElement> buffer = DstEntityManager.AddBuffer<Float3BufferElement>(entity);
                DynamicBuffer<float3> floatBuffer = buffer.Reinterpret<float3>();

                for (int i = 0; i < boidAuthoring.NavigationRayCount; i++)
                {
                    float turnFraction = 0.6180f;
                    float t = i / (boidAuthoring.NavigationRayCount - 1f);
                    float phi = math.acos(1f - 2f * t);
                    float theta = 2 * math.PI * turnFraction * i;

                    float x = math.sin(phi) * math.cos(theta);
                    float y = math.sin(phi) * math.sin(theta);
                    float z = math.cos(phi);
                    float3 p = new float3(x, y, z);
                    if (math.acos(math.dot(p, new float3(0, 0, 1))) < boidAuthoring.VisionAngle)
                    {
                        floatBuffer.Add(p);
                    }
                }

                // Remove default transform system components
                DstEntityManager.RemoveComponent<Translation>(entity);
                DstEntityManager.RemoveComponent<Rotation>(entity);
            });
        }
    }
}

#endif
