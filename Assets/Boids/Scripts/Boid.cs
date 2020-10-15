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

        //public bool Equals(Boid other)
        //{
        //    return CellRadius == other.CellRadius
        //        && AlignmentWeight == other.AlignmentWeight
        //        && SeparationWeight == other.SeparationWeight
        //        && TargetWeight == other.TargetWeight
        //        && OuterDetectionRadius == other.OuterDetectionRadius
        //        && InnerDetectionRadius == other.InnerDetectionRadius
        //        && MoveSpeed == other.MoveSpeed
        //        && WanderRadius == other.WanderRadius
        //        && WanderWeight == other.WanderWeight
        //        && VisionAngle == other.VisionAngle
        //        && NavigationRayCount == other.NavigationRayCount;
        //}

        //public override int GetHashCode()
        //{
        //    return base.GetHashCode();
        //}
    }
}
