using System;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;

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

        // New Settings
        public float WanderRadius;
        public float WanderWeight;
    }
}
