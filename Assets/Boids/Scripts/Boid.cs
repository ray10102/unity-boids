using System;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

namespace Samples.Boids
{
    [Serializable]
    [WriteGroup(typeof(LocalToWorld))]
    public struct Boid : ISharedComponentData
    {
        public float CellRadius;
        public float SeparationWeight;
        public float AlignmentWeight;
        public float TargetWeight;
        public float OuterDetectionRadius;
        public float InnerDetectionRadius;
        public float MoveSpeed;
        public float WanderRadius;
        public float WanderWeight;
        public float VisionAngle;
        public int NavigationRayCount;
        public NativeArray<float3> SearchPoints;
    }
}
