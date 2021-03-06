#if UNITY_EDITOR

using Samples.Boids;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[AddComponentMenu("DOTS Samples/Boids/Boid")]
public class BoidAuthoring : MonoBehaviour
{
    public float CellRadius = 8.0f;
    public float SeparationWeight = 1.0f;
    public float AlignmentWeight = 1.0f;
    public float TargetWeight = 2.0f;
    public float OuterDetectionRadius = 30.0f;
    public float InnerDetectionRadius = 15.0f;
    public float MoveSpeed = 25.0f;
    public float WanderRadius = 10f;
    public float WanderWeight = 1.0f;
    public float VisionAngle = (float) Math.PI / 4f;
    public int NavigationRayCount = 200;
}

#endif
