using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using System.Diagnostics;

// Mike's GDC Talk on 'A Data Oriented Approach to Using Component Systems'
// is a great reference for dissecting the Boids sample code:
// https://youtu.be/p65Yt20pw0g?t=1446
// It explains a slightly older implementation of this sample but almost all the
// information is still relevant.

// The targets (2 red fish) and enemy (1 shark) move based on the ActorAnimation tab
// in the Unity UI, so that they are moving based on key-framed animation.

namespace Samples.Boids
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class BoidSystem : SystemBase
    {
        EntityQuery m_BoidQuery;
        EntityQuery m_TargetQuery;
        EntityQuery m_EnemyQuery;

        // In this sample there are 3 total unique boid variants, one for each unique value of the
        // Boid SharedComponent (note: this includes the default uninitialized value at
        // index 0, which isnt actually used in the sample).
        List<Boid> m_UniqueTypes = new List<Boid>(3);

        // This accumulates the `positions` (separations) and `headings` (alignments) of all the boids in each cell to:
        // 1) count the number of boids in each cell
        // 2) find the nearest enemy and target to each boid cell
        // 3) track which array entry contains the accumulated values for each boid's cell
        // In this context, the cell represents the hashed bucket of boids that are near one another within cellRadius
        // floored to the nearest int3.
        // Note: `IJobNativeMultiHashMapMergedSharedKeyIndices` is a custom job to iterate safely/efficiently over the
        // NativeContainer used in this sample (`NativeMultiHashMap`). Currently these kinds of changes or additions of
        // custom jobs generally require access to data/fields that aren't available through the `public` API of the
        // containers. This is why the custom job type `IJobNativeMultiHashMapMergedSharedKeyIndicies` is declared in
        // the DOTS package (which can see the `internal` container fields) and not in the Boids sample.
        [BurstCompile]
        struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            public NativeArray<int> cellIndices;
            public NativeArray<float3> cellAlignment;
            public NativeArray<float3> cellSeparation;
            public NativeArray<int> cellEnemyPositionIndex;
            public NativeArray<float> cellEnemyDistance;
            public NativeArray<int> cellTargetPositionIndex;
            public NativeArray<int> cellCount;
            [ReadOnly] public NativeArray<float3> targetPositions;
            [ReadOnly] public NativeArray<float3> enemyPositions;

            void NearestPosition(NativeArray<float3> targets, float3 position, out int nearestPositionIndex, out float nearestDistance)
            {
                nearestPositionIndex = 0;
                nearestDistance = math.lengthsq(position - targets[0]);
                for (int i = 1; i < targets.Length; i++)
                {
                    var targetPosition = targets[i];
                    var distance = math.lengthsq(position - targetPosition);
                    var nearest = distance < nearestDistance;

                    nearestDistance = math.select(nearestDistance, distance, nearest);
                    nearestPositionIndex = math.select(nearestPositionIndex, i, nearest);
                }
                nearestDistance = math.sqrt(nearestDistance);
            }

            // Resolves the distance of the nearest enemy and target and stores the cell index.
            public void ExecuteFirst(int index)
            {
                var position = cellSeparation[index] / cellCount[index];

                int enemyPositionIndex;
                float enemyDistance;
                NearestPosition(enemyPositions, position, out enemyPositionIndex, out enemyDistance);
                cellEnemyPositionIndex[index] = enemyPositionIndex;
                cellEnemyDistance[index] = enemyDistance;

                int targetPositionIndex;
                float targetDistance;
                NearestPosition(targetPositions, position, out targetPositionIndex, out targetDistance);
                cellTargetPositionIndex[index] = targetPositionIndex;

                cellIndices[index] = index;
            }

            // Sums the alignment and separation of the actual index being considered and stores
            // the index of this first value where we're storing the cells.
            // note: these items are summed so that in `Steer` their average for the cell can be resolved.
            public void ExecuteNext(int cellIndex, int index)
            {
                cellCount[cellIndex] += 1;
                cellAlignment[cellIndex] = cellAlignment[cellIndex] + cellAlignment[index];
                cellSeparation[cellIndex] = cellSeparation[cellIndex] + cellSeparation[index];
                cellIndices[index] = cellIndex;
            }
        }

        [BurstCompile]
        public struct RaycastJob : IJobParallelFor
        {
            [ReadOnly] public CollisionWorld world;
            [ReadOnly] public NativeArray<RaycastInput> inputs;
            public NativeArray<RaycastHit> results;

            public unsafe void Execute(int index)
            {
                RaycastHit hit;
                world.CastRay(inputs[index], out hit);
                results[index] = hit;
            }
        }

        public static JobHandle ScheduleBatchRayCast(CollisionWorld world,
            NativeArray<RaycastInput> inputs, NativeArray<RaycastHit> results, JobHandle dependency)
        {
            JobHandle rcj = new RaycastJob
            {
                inputs = inputs,
                results = results,
                world = world

            }.Schedule(inputs.Length, 4, dependency);
            return rcj;
        }


        protected override void OnUpdate()
        {
            var enemyCount = m_EnemyQuery.CalculateEntityCount();
            var targetCount = m_TargetQuery.CalculateEntityCount();
            EntityManager.GetAllUniqueSharedComponentData(m_UniqueTypes);

            // Each variant of the Boid represents a different value of the SharedComponentData and is self-contained,
            // meaning Boids of the same variant only interact with one another. Thus, this loop processes each
            // variant type individually.
            for (int boidVariantIndex = 0; boidVariantIndex < m_UniqueTypes.Count; boidVariantIndex++)
            {
                var settings = m_UniqueTypes[boidVariantIndex];
                m_BoidQuery.AddSharedComponentFilter(settings);

                var boidCount = m_BoidQuery.CalculateEntityCount();

                if (boidCount == 0)
                {
                    // Early out. If the given variant includes no Boids, move on to the next loop.
                    // For example, variant 0 will always exit early bc it's it represents a default, uninitialized
                    // Boid struct, which does not appear in this sample.
                    m_BoidQuery.ResetFilter();
                    continue;
                }

                // The following calculates spatial cells of neighboring Boids
                // note: working with a sparse grid and not a dense bounded grid so there
                // are no predefined borders of the space.

                var hashMap = new NativeMultiHashMap<int, int>(boidCount, Allocator.TempJob);
                var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var cellEnemyPositionIndex = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var cellTargetPositionIndex = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var cellCount = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var cellEnemyDistance = new NativeArray<float>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var cellAlignment = new NativeArray<float3>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var cellSeparation = new NativeArray<float3>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var raycastInputs = new NativeArray<RaycastInput>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var initialRaycastResults = new NativeArray<RaycastHit>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var unobstructedDirections = new NativeArray<float3>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var copyTargetPositions = new NativeArray<float3>(targetCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var copyEnemyPositions = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                // The following jobs all run in parallel because the same JobHandle is passed for their
                // input dependencies when the jobs are scheduled; thus, they can run in any order (or concurrently).
                // The concurrency is property of how they're scheduled, not of the job structs themselves.

                // These jobs extract the relevant position, heading component
                // to NativeArrays so that they can be randomly accessed by the `MergeCells` and `Steer` jobs.
                // These jobs are defined inline using the Entities.ForEach lambda syntax.
                var initialCellAlignmentJobHandle = Entities
                    .WithSharedComponentFilter(settings)
                    .WithName("InitialCellAlignmentJob")
                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                    {
                        cellAlignment[entityInQueryIndex] = localToWorld.Forward;
                    })
                    .ScheduleParallel(Dependency);

                var initialCellSeparationJobHandle = Entities
                    .WithSharedComponentFilter(settings)
                    .WithName("InitialCellSeparationJob")
                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                    {
                        cellSeparation[entityInQueryIndex] = localToWorld.Position;
                    })
                    .ScheduleParallel(Dependency);

                var initialRaycastInputsJobHandle = Entities
                    .WithSharedComponentFilter(settings)
                    .WithName("InitialRaycastInputsJob")
                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                    {
                        raycastInputs[entityInQueryIndex] = new RaycastInput
                        {
                            Start = localToWorld.Position,
                            End = localToWorld.Position + (localToWorld.Forward * settings.OuterDetectionRadius),
                            Filter = new CollisionFilter
                            {
                                BelongsTo = ~0u,
                                CollidesWith = 1, // Environment layer
                                GroupIndex = 0
                            },
                        };
                    })
                    .ScheduleParallel(Dependency);

                var world = World.DefaultGameObjectInjectionWorld.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>().PhysicsWorld.CollisionWorld;
                var batchRaycastJobHandle = ScheduleBatchRayCast(world, raycastInputs, initialRaycastResults, initialRaycastInputsJobHandle);

                var findUnobstructedDirectionsJobHandle = Entities
                    .WithName("FindUnobstructedDirectionsJob")
                    .WithSharedComponentFilter(settings)
                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld, in DynamicBuffer<Float3BufferElement> buffer) =>
                    {
                        JobLogger.Log("In find unobstructed job");
                        float3 bestDir = float3.zero;
                        float furthestHit = 0f;
                        RaycastHit hit;
                        DynamicBuffer<float3> float3buffer = buffer.Reinterpret<float3>();
                        for (int i = 0; i < float3buffer.Length; i++)
                        {
                            float3 end = localToWorld.Position + ((math.mul(localToWorld.Value, new float4(float3buffer[i], 1)) * settings.OuterDetectionRadius)).xyz;
                            RaycastInput input = new RaycastInput()
                            {
                                Start = localToWorld.Position,
                                End = end,
                                Filter = new CollisionFilter
                                {
                                    BelongsTo = ~0u,
                                    CollidesWith = 1, // Environment layer
                                    GroupIndex = 0
                                },
                            };
                            if (world.CastRay(input, out hit))
                            {
                                var dist = math.distance(hit.Position, localToWorld.Position);
                                if (dist > furthestHit)
                                {
                                    bestDir = hit.Position - localToWorld.Position;
                                    furthestHit = dist;
                                    JobLogger.Log("Found a better way");
                                }
                            }
                            else // this direction is unobstructed, return
                            {
                                unobstructedDirections[entityInQueryIndex] = hit.Position - localToWorld.Position;
                                JobLogger.Log("Found a way");
                                return;
                            }
                        }
                        unobstructedDirections[entityInQueryIndex] = bestDir;
                    }).ScheduleParallel(batchRaycastJobHandle);

                var copyTargetPositionsJobHandle = Entities
                    .WithName("CopyTargetPositionsJob")
                    .WithAll<BoidTarget>()
                    .WithStoreEntityQueryInField(ref m_TargetQuery)
                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                    {
                        copyTargetPositions[entityInQueryIndex] = localToWorld.Position;
                    })
                    .ScheduleParallel(Dependency);

                var copyEnemyPositionsJobHandle = Entities
                    .WithName("CopyEnemyPositionsJob")
                    .WithAll<BoidObstacle>()
                    .WithStoreEntityQueryInField(ref m_EnemyQuery)
                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                    {
                        copyEnemyPositions[entityInQueryIndex] = localToWorld.Position;
                    })
                    .ScheduleParallel(Dependency);

                // Populates a hash map, where each bucket contains the indices of all Boids whose positions quantize
                // to the same value for a given cell radius so that the information can be randomly accessed by
                // the `MergeCells` and `Steer` jobs.
                // This is useful in terms of the algorithm because it limits the number of comparisons that will
                // actually occur between the different boids. Instead of for each boid, searching through all
                // boids for those within a certain radius, this limits those by the hash-to-bucket simplification.
                var parallelHashMap = hashMap.AsParallelWriter();
                var hashPositionsJobHandle = Entities
                    .WithName("HashPositionsJob")
                    .WithAll<Boid>()
                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                    {
                        var hash = (int)math.hash(new int3(math.floor(localToWorld.Position / settings.CellRadius)));
                        parallelHashMap.Add(hash, entityInQueryIndex);
                    })
                    .ScheduleParallel(Dependency);

                var initialCellCountJob = new MemsetNativeArray<int>
                {
                    Source = cellCount,
                    Value = 1
                };
                var initialCellCountJobHandle = initialCellCountJob.Schedule(boidCount, 64, Dependency);

                var initialCellBarrierJobHandle = JobHandle.CombineDependencies(initialCellAlignmentJobHandle, initialCellSeparationJobHandle, initialCellCountJobHandle);
                var copyTargetEnemyBarrierJobHandle = JobHandle.CombineDependencies(copyTargetPositionsJobHandle, copyEnemyPositionsJobHandle, findUnobstructedDirectionsJobHandle);
                var mergeCellsBarrierJobHandle = JobHandle.CombineDependencies(hashPositionsJobHandle, initialCellBarrierJobHandle, copyTargetEnemyBarrierJobHandle);

                var mergeCellsJob = new MergeCells
                {
                    cellIndices = cellIndices,
                    cellAlignment = cellAlignment,
                    cellSeparation = cellSeparation,
                    cellEnemyDistance = cellEnemyDistance,
                    cellEnemyPositionIndex = cellEnemyPositionIndex,
                    cellTargetPositionIndex = cellTargetPositionIndex,
                    cellCount = cellCount,
                    targetPositions = copyTargetPositions,
                    enemyPositions = copyEnemyPositions
                };
                var mergeCellsJobHandle = mergeCellsJob.Schedule(hashMap, 64, mergeCellsBarrierJobHandle);

                // This reads the previously calculated boid information for all the boids of each cell to update
                // the `localToWorld` of each of the boids based on their newly calculated headings using
                // the standard boid flocking algorithm.
                float deltaTime = math.min(0.05f, Time.DeltaTime);
                Random random = new Random((uint)UnityEngine.Random.Range(1, 100000));
                var steerJobHandle = Entities
                    .WithName("Steer")
                    .WithSharedComponentFilter(settings) // implies .WithAll<Boid>()
                    .WithReadOnly(cellIndices)
                    .WithReadOnly(cellCount)
                    .WithReadOnly(cellAlignment)
                    .WithReadOnly(cellSeparation)
                    .WithReadOnly(cellEnemyDistance)
                    .WithReadOnly(cellEnemyPositionIndex)
                    .WithReadOnly(cellTargetPositionIndex)
                    .WithReadOnly(copyEnemyPositions)
                    .WithReadOnly(copyTargetPositions)
                    .WithReadOnly(initialRaycastResults)
                    .WithReadOnly(unobstructedDirections)
                    .ForEach((int entityInQueryIndex, ref LocalToWorld localToWorld) =>
                    {

                        // temporarily storing the values for code readability
                        var forward = localToWorld.Forward;
                        var currentPosition = localToWorld.Position;
                        var raycastResult = initialRaycastResults[entityInQueryIndex];
                        var unobstructedDirection = unobstructedDirections[entityInQueryIndex];

                        // If our current heading is obstructed by something closer than our inner detection radius
                        // ignore other forces and head for the unobstructed direction
                        if (raycastResult.ColliderKey.Value == 0 && math.distance(raycastResult.Position, currentPosition) <= settings.InnerDetectionRadius)
                        {
                            // updates using the newly calculated heading direction1
                            var goalHeading = math.normalizesafe(forward + deltaTime * (math.normalizesafe(unobstructedDirection) - forward));
                            localToWorld = new LocalToWorld
                            {
                                Value = float4x4.TRS(
                                    new float3(localToWorld.Position + (goalHeading * settings.MoveSpeed * deltaTime)),
                                    quaternion.LookRotationSafe(goalHeading, math.up()),
                                    new float3(1.0f, 1.0f, 1.0f))
                            };
                            return;
                        }

                        // more values
                        var cellIndex = cellIndices[entityInQueryIndex];
                        var neighborCount = cellCount[cellIndex];
                        var alignment = cellAlignment[cellIndex];
                        var separation = cellSeparation[cellIndex];
                        var nearestEnemyDistance = cellEnemyDistance[cellIndex];
                        var nearestEnemyPositionIndex = cellEnemyPositionIndex[cellIndex];
                        var nearestTargetPositionIndex = cellTargetPositionIndex[cellIndex];
                        var nearestEnemyPosition = copyEnemyPositions[nearestEnemyPositionIndex];
                        var nearestTargetPosition = copyTargetPositions[nearestTargetPositionIndex];
                        var nearestTargetDiff = nearestTargetPosition - currentPosition;
                        // Setting up the directions for the three main biocrowds influencing directions adjusted based
                        // on the predefined weights:
                        // 1) alignment - how much should it move in a direction similar to those around it?
                        // note: we use `alignment/neighborCount`, because we need the average alignment in this case; however
                        // alignment is currently the summation of all those of the boids within the cellIndex being considered.
                        var alignmentResult = settings.AlignmentWeight
                            * math.normalizesafe((alignment / neighborCount) - forward);
                        // 2) separation - how close is it to other boids and are there too many or too few for comfort?
                        // note: here separation represents the summed possible center of the cell. We perform the multiplication
                        // so that both `currentPosition` and `separation` are weighted to represent the cell as a whole and not
                        // the current individual boid.
                        var separationResult = settings.SeparationWeight
                            * math.normalizesafe((currentPosition * neighborCount) - separation);
                        // 3) target - is it still towards its destination?
                        var targetHeading = settings.TargetWeight
                            * math.normalizesafe(nearestTargetDiff);

                        // wander -  apply a random displacement to the target heading depending on distance to target.
                        float3 disp = settings.WanderWeight * settings.WanderRadius * random.NextFloat3Direction();
                        targetHeading += math.lerp(float3.zero, disp, math.clamp((math.length(nearestTargetDiff) - settings.InnerDetectionRadius) / (settings.OuterDetectionRadius - settings.InnerDetectionRadius), 0f, 1f));

                        // creating the enemy avoidance vector s.t. it's pointing towards the nearest enemy
                        // but at the specified 'EnemyAversionDistance'. If this distance is greater than the
                        // current distance to the enemy, the direction becomes inverted. This simulates the
                        // idea that if `currentPosition` is too close to an enemy, the weight of this pushes
                        // the current boid to escape in the fastest direction; however, if the enemy isn't
                        // too close, the weighting denotes that the boid doesnt need to escape but will move
                        // slower if still moving in that direction (note: we end up not using this move-slower
                        // case, because of `targetForward`'s decision to not use enemy avoidance if an enemy
                        // isn't close enough).
                        var enemySteering = currentPosition - nearestEnemyPosition;
                        var avoidEnemyHeading = (nearestEnemyPosition + math.normalizesafe(enemySteering)
                            * settings.OuterDetectionRadius) - currentPosition;

                        // Interpolate between enemy avoidance and normal heading
                        var nearestEnemyDistanceFromRadius = nearestEnemyDistance - settings.OuterDetectionRadius;
                        var normalHeading = math.normalizesafe(alignmentResult + separationResult + targetHeading);
                        var targetForward = math.lerp(avoidEnemyHeading, normalHeading, math.clamp((nearestEnemyDistance - settings.InnerDetectionRadius) / (settings.OuterDetectionRadius - settings.InnerDetectionRadius), 0f, 1f));

                        // updates using the newly calculated heading direction1
                        var nextHeading = math.normalizesafe(forward + deltaTime * (targetForward - forward));
                        localToWorld = new LocalToWorld
                        {
                            Value = float4x4.TRS(
                                new float3(localToWorld.Position + (nextHeading * settings.MoveSpeed * deltaTime)),
                                quaternion.LookRotationSafe(nextHeading, math.up()),
                                new float3(1.0f, 1.0f, 1.0f))
                        };
                    }).ScheduleParallel(mergeCellsJobHandle);

                // Dispose allocated containers with dispose jobs.
                Dependency = steerJobHandle;
                var disposeJobHandle = hashMap.Dispose(Dependency);
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, cellIndices.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, cellEnemyPositionIndex.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, cellTargetPositionIndex.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, cellCount.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, cellEnemyDistance.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, cellAlignment.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, cellSeparation.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, copyEnemyPositions.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, copyTargetPositions.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, raycastInputs.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, initialRaycastResults.Dispose(Dependency));
                disposeJobHandle = JobHandle.CombineDependencies(disposeJobHandle, unobstructedDirections.Dispose(Dependency));
                Dependency = disposeJobHandle;

                // We pass the job handle and add the dependency so that we keep the proper ordering between the jobs
                // as the looping iterates. For our purposes of execution, this ordering isn't necessary; however, without
                // the add dependency call here, the safety system will throw an error, because we're accessing multiple
                // pieces of boid data and it would think there could possibly be a race condition.

                m_BoidQuery.AddDependency(Dependency);
                m_BoidQuery.ResetFilter();
            }
            m_UniqueTypes.Clear();
        }

        protected override void OnCreate()
        {
            m_BoidQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Boid>(), ComponentType.ReadWrite<LocalToWorld>() },
            });

            RequireForUpdate(m_BoidQuery);
            RequireForUpdate(m_EnemyQuery);
            RequireForUpdate(m_TargetQuery);
        }
    }
}