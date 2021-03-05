using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace ew
{
    public struct Boid : IComponentData
    {
        public int boidId;
        public Vector3 force;
        public Vector3 velocity;
        public Vector3 up;

        public Vector3 acceleration;
        public float mass;
        public float maxSpeed;
        public float maxForce;
        public float weight;
        public int taggedCount;

        public Vector3 fleeForce; // Have to put this here because there is a limit to the number of components in IJobProcessComponentData
        public Vector3 seekForce; // Have to put this here because there is a limit to the number of components in IJobProcessComponentData
    }

    public struct ObstacleAvoidance:IComponentData
    {
        public float forwardFeelerDepth;
        public Vector3 feeler;

        public enum ForceType
        {
            normal,
            incident,
            up,
            braking
        };

        public ForceType forceType;

        public Vector3 force;
        public Vector3 point;
        public Vector3 normal;

        public Vector3 start;
        public Vector3 end;
        
    }

    public struct Flee : IComponentData
    {
        public Vector3 force;
    }
    public struct Seek : IComponentData
    {
        public Vector3 force;
    }

    public struct Seperation : IComponentData
    {
        public Vector3 force;
    }

    public struct Constrain : IComponentData
    {
        public Vector3 force;
    }

    public struct Cohesion : IComponentData
    {
        public Vector3 force;
    }

    public struct Alignment : IComponentData
    {
        public Vector3 force;
    }

    public struct Wander : IComponentData
    {
        public Vector3 force;

        public float distance;
        public float radius;
        public float jitter;
        public Vector3 target;
    }

    public class BoidJobSystem : SystemBase
    {
        public static BoidJobSystem Instance;

        [NativeDisableParallelForRestriction]
        NativeArray<int> neighbours;

        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> speeds;

        [NativeDisableParallelForRestriction]
        public NativeMultiHashMap<int, int> cells;

        int maxNeighbours = 100;

        public BoidBootstrap bootstrap;

        EntityQuery translationsRotationsQuery;
        EntityQuery wanderQuery;
        EntityQuery boidQuery;
        EntityQuery seperationQuery;
        EntityQuery cohesionQuery;
        EntityQuery alignmentQuery;
        EntityQuery constrainQuery;
        EntityQuery obstacleQuery;

        public Vector3 CamPosition;
        public Quaternion CamRotation;

        BuildPhysicsWorld physicsWorld;
        CollisionWorld collisionWorld;

        protected override void OnCreate()
        {
            Instance = this;
            bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();
            Enabled = false;
            neighbours = new NativeArray<int>(BoidBootstrap.MAX_BOIDS * BoidBootstrap.MAX_NEIGHBOURS, Allocator.Persistent);
            positions = new NativeArray<Vector3>(BoidBootstrap.MAX_BOIDS, Allocator.Persistent);
            rotations = new NativeArray<Quaternion>(BoidBootstrap.MAX_BOIDS, Allocator.Persistent);
            speeds = new NativeArray<float>(BoidBootstrap.MAX_BOIDS, Allocator.Persistent); // Needed for the animations
            cells = new NativeMultiHashMap<int, int>(BoidBootstrap.MAX_BOIDS, Allocator.Persistent);

            translationsRotationsQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Boid>(),
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>()
                }                
            });

            wanderQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Boid>(),
                    ComponentType.ReadOnly<Wander>()
                }
            });

            boidQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Boid>(),
                    ComponentType.ReadOnly<Wander>(),
                    ComponentType.ReadOnly<Seperation>(),
                    ComponentType.ReadOnly<Alignment>(),
                    ComponentType.ReadOnly<Cohesion>(),
                    ComponentType.ReadOnly<Constrain>()
                }
            });

            seperationQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Boid>(),
                    ComponentType.ReadOnly<Seperation>()
                }
            });

            alignmentQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Boid>(),
                    ComponentType.ReadOnly<Alignment>()
                }
            });

            cohesionQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<Rotation>(),
                    ComponentType.ReadOnly<Boid>(),
                    ComponentType.ReadOnly<Cohesion>()
                }
            });

            constrainQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Constrain>(),
                    ComponentType.ReadOnly<Boid>()
                }
            });

            obstacleQuery = GetEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<ObstacleAvoidance>(),
                    ComponentType.ReadOnly<Boid>(),
                    ComponentType.ReadOnly<Translation>(),
                    ComponentType.ReadOnly<LocalToWorld>()
                }
            });

            Enabled = false;

            physicsWorld  = World.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
            collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

            // Register the gizmos callback
            //MyGizmo.OnDrawGizmos(DrawGizmos);

        }

        private void DrawGizmos()
        {
            Entities.ForEach((Boid boid, ObstacleAvoidance oa, Translation p, Rotation r) =>
            {
                Vector3 position = p.Value;
                Vector3 forward = math.mul(r.Value, Vector3.forward);
                Debug.DrawLine(oa.start, oa.end, Color.cyan);
                Debug.DrawLine(oa.point, oa.point + oa.normal * 10, Color.red);
            })
            .Run();
        }



        protected override void OnDestroy()
        {
            neighbours.Dispose();
            positions.Dispose();
            rotations.Dispose();
            speeds.Dispose();
            cells.Dispose();
        }

        protected override void OnUpdate()
        {

            physicsWorld  = World.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
            
            BoidBootstrap bootstrap = this.bootstrap;

            ComponentTypeHandle<Wander> wTHandle = GetComponentTypeHandle<Wander>();
            ComponentTypeHandle<Boid> bTHandle = GetComponentTypeHandle<Boid>();
            ComponentTypeHandle<Translation> ttTHandle = GetComponentTypeHandle<Translation>();
            ComponentTypeHandle<Rotation> rTHandle = GetComponentTypeHandle<Rotation>();
            ComponentTypeHandle<Seperation> sTHandle = GetComponentTypeHandle<Seperation>();
            ComponentTypeHandle<Cohesion> cTHandle = GetComponentTypeHandle<Cohesion>();
            ComponentTypeHandle<Alignment> aTHandle = GetComponentTypeHandle<Alignment>();
            ComponentTypeHandle<Constrain> conTHandle = GetComponentTypeHandle<Constrain>();
            ComponentTypeHandle<LocalToWorld> ltwTHandle = GetComponentTypeHandle<LocalToWorld>();
            ComponentTypeHandle<ObstacleAvoidance> oaTHandle = GetComponentTypeHandle<ObstacleAvoidance>();
            
            float deltaTime = Time.DeltaTime * bootstrap.speed;

            Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000));

            CamPosition = positions[0];
            CamRotation = rotations[0];

            //DrawGizmos();

            // Copy entities to the native arrays             
            var copyToNativeJob = new CopyTransformsToNativeJob()
            {
                positions = this.positions,
                rotations = this.rotations,
                boidTypeHandle = bTHandle,
                translationTypeHandle = ttTHandle,
                rotationTypeHandle = rTHandle
            };

            var copyToNativeHandle = copyToNativeJob.ScheduleParallel(translationsRotationsQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, copyToNativeHandle);
            
            var oaJob = new ObstacleAvoidanceJob()
            {
                boidTypeHandle = bTHandle,
                translationTypeHandle = ttTHandle,
                ltwTypeHandle = ltwTHandle,
                obstacleAvoidanceTypeHandle = oaTHandle,
                rotationTypeHandle = rTHandle,
                collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld

            };

            var oaJobHandle = oaJob.ScheduleParallel(obstacleQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, oaJobHandle);
            
            if (bootstrap.usePartitioning)
            {
                cells.Clear();
                var partitionJob = new PartitionSpaceJob()
                {
                    positions = this.positions,
                    cells = this.cells.AsParallelWriter(),
                    threedcells = bootstrap.threedcells,
                    cellSize = bootstrap.cellSize,
                    gridSize = bootstrap.gridSize
                };

                var partitionHandle = partitionJob.Schedule(bootstrap.numBoids, 50, Dependency);
                Dependency = JobHandle.CombineDependencies(Dependency, partitionHandle);
            }
            var countNeighbourJob = new CountNeighboursJob()
            {
                positions = this.positions,
                rotations = this.rotations,
                neighbours = this.neighbours,
                maxNeighbours = bootstrap.totalNeighbours,
                cells = this.cells,
                cellSize = bootstrap.cellSize,
                gridSize = bootstrap.gridSize,
                usePartitioning = bootstrap.usePartitioning,
                neighbourDistance = bootstrap.neighbourDistance,
                boidTypeHandle = bTHandle,
                translationTypeHandle = ttTHandle,
                rotationTypeHandle = rTHandle

            };
            var cnjHandle = countNeighbourJob.ScheduleParallel(translationsRotationsQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, cnjHandle);

            var constrainJob = new ConstrainJob()
            {
                positions = this.positions,
                boidTypeHandle = bTHandle,
                constrainTypeHandle = conTHandle,
                weight = bootstrap.constrainWeight,
                centre = bootstrap.transform.position,
                radius = bootstrap.radius                
            };

            var conjHandle = constrainJob.ScheduleParallel(constrainQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, conjHandle);


            var seperationJob = new SeperationJob()
            {
                positions = this.positions,
                maxNeighbours = this.maxNeighbours,
                random = random,
                neighbours = this.neighbours,
                weight = bootstrap.seperationWeight,
                seperationTypeHandle = sTHandle,
                translationTypeHandle = ttTHandle,
                boidTypeHandle = bTHandle,
            };

            var sjHandle = seperationJob.ScheduleParallel(seperationQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, sjHandle);

            var cohesionJob = new CohesionJob()
            {
                positions = this.positions,
                maxNeighbours = this.maxNeighbours,
                neighbours = this.neighbours,
                weight = bootstrap.cohesionWeight,
                cohesionTypeHandle = cTHandle,
                boidTypeHandle = bTHandle,
            };

            var cjHandle = cohesionJob.ScheduleParallel(cohesionQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, cjHandle);

            var alignmentJob = new AlignmentJob()
            {
                rotations = this.rotations,
                maxNeighbours = this.maxNeighbours,
                neighbours = this.neighbours,
                weight = bootstrap.alignmentWeight,
                alignmentTypeHandle = aTHandle,
                boidTypeHandle = bTHandle,
            };

            var ajHandle = alignmentJob.ScheduleParallel(alignmentQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, ajHandle);

            var wanderJob = new WanderJob()
            {
                wanderTypeHandle = wTHandle,
                translationTypeHandle = ttTHandle,
                rotationTypeHandle = rTHandle,
                boidTypeHandle = bTHandle,
                dT = deltaTime,
                ran = random,
                weight = bootstrap.wanderWeight
            };

            var wanderJobHandle = wanderJob.ScheduleParallel(wanderQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, wanderJobHandle);

            var boidJob = new BoidJob()
            {
                positions = this.positions,
                rotations = this.rotations,
                speeds = this.speeds,
                dT = deltaTime,
                limitUpAndDown = bootstrap.limitUpAndDown,
                banking = 0.01f,
                damping = 0.01f,
                wanderTypeHandle = wTHandle,
                boidTypeHandle = bTHandle,
                translationTypeHandle = ttTHandle,
                seperationTypeHandle = sTHandle,
                rotationTypeHandle = rTHandle,
                cohesionTypeHandle = cTHandle,
                alignmentTypeHandle = aTHandle,
                constrainTypeHandle = conTHandle,
                obstacleTypeHandle = oaTHandle
            };

            var boidJobHandle = boidJob.ScheduleParallel(boidQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, boidJobHandle);

            var copyFromNativeJob = new CopyTransformsFromNativeJob()
            {
                positions = this.positions,
                rotations = this.rotations,
                boidTypeHandle = bTHandle,
                translationTypeHandle = ttTHandle,
                rotationTypeHandle = rTHandle
            };

            var copyFromNativeHandle = copyFromNativeJob.ScheduleParallel(translationsRotationsQuery, 1, Dependency);

            Dependency = JobHandle.CombineDependencies(Dependency, copyFromNativeHandle);            

            return;
        }
    }


    #region Jobs

    [BurstCompile]
    struct CohesionJob : IJobEntityBatch
    {
        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        public ComponentTypeHandle<Cohesion> cohesionTypeHandle;

        [ReadOnly]
        public NativeArray<int> neighbours;

        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        public int maxNeighbours;
        public float weight;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var cohesionChunk = batchInChunk.GetNativeArray(cohesionTypeHandle);

            
            for (int i = 0; i < batchInChunk.Count; i++)
            {
                Cohesion c = cohesionChunk[i];
                Boid b = boidChunk[i];
                int neighbourStartIndex = maxNeighbours * b.boidId;

                Vector3 force = Vector3.zero;
                Vector3 centerOfMass = Vector3.zero;
                for (int j = 0; j < b.taggedCount; j++)
                {
                    int neighbourId = neighbours[neighbourStartIndex + j];
                    centerOfMass += positions[neighbourId];
                }
                if (b.taggedCount > 0)
                {
                    centerOfMass /= b.taggedCount;
                    // Generate a seek force
                    Vector3 toTarget = centerOfMass - positions[b.boidId];
                    Vector3 desired = toTarget.normalized * b.maxSpeed;
                    force = (desired - b.velocity).normalized;
                }
                c.force = force * weight;
                cohesionChunk[i] = c;
            }

        }
    }

    [BurstCompile]
    struct AlignmentJob : IJobEntityBatch
    {

        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        public ComponentTypeHandle<Alignment> alignmentTypeHandle;

        [ReadOnly]
        public NativeArray<int> neighbours;

        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;
        public float weight;
        public int maxNeighbours;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {

            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var alignmentChunk = batchInChunk.GetNativeArray(alignmentTypeHandle);

            for (int i = 0; i < batchInChunk.Count; i++)
            {
                Alignment a = alignmentChunk[i];
                Boid b = boidChunk[i];

                Vector3 desired = Vector3.zero;
                Vector3 force = Vector3.zero;
                int neighbourStartIndex = maxNeighbours * b.boidId;
                for (int j = 0; j < b.taggedCount; j++)
                {
                    int neighbourId = neighbours[neighbourStartIndex + j];
                    desired += rotations[neighbourId] * Vector3.forward;
                }

                if (b.taggedCount > 0)
                {
                    desired /= b.taggedCount;
                    force = desired - (rotations[b.boidId] * Vector3.forward);
                }

                a.force = force * weight;
                alignmentChunk[i] = a;
            }
        }
    }

    [BurstCompile]
    struct BoidJob : IJobEntityBatch
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> speeds;

        public float damping;
        public float banking;
        public float limitUpAndDown;

        public float dT;

        [ReadOnly] public ComponentTypeHandle<Wander> wanderTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Seperation> seperationTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Alignment> alignmentTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Cohesion> cohesionTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Constrain> constrainTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Translation> translationTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Rotation> rotationTypeHandle;
        [ReadOnly] public ComponentTypeHandle<ObstacleAvoidance> obstacleTypeHandle;
        public ComponentTypeHandle<Boid> boidTypeHandle;


        private Vector3 AccululateForces(ref Boid b, ref ObstacleAvoidance oa, ref Seperation s, ref Alignment a, ref Cohesion c, ref Wander w, ref Constrain con)
        {
            Vector3 force = Vector3.zero;


            force += oa.force;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }

            force += b.fleeForce;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }


            force += s.force;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }
            force += a.force;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }

            force += c.force;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }

            force += w.force;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }

            force += con.force;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }

            force += b.seekForce;
            if (force.magnitude >= b.maxForce)
            {
                force = Vector3.ClampMagnitude(force, b.maxForce);
                return force;
            }

            return force;
        }

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var wanderChunk = batchInChunk.GetNativeArray(wanderTypeHandle);
            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var translationsChunk = batchInChunk.GetNativeArray(translationTypeHandle);
            var rotationsChunk = batchInChunk.GetNativeArray(rotationTypeHandle);
            var seperationChunk = batchInChunk.GetNativeArray(seperationTypeHandle);
            var cohesionChunk = batchInChunk.GetNativeArray(cohesionTypeHandle);
            var alignmentChunk = batchInChunk.GetNativeArray(alignmentTypeHandle);
            var constrainChunk = batchInChunk.GetNativeArray(constrainTypeHandle);
            var oaChunk = batchInChunk.GetNativeArray(obstacleTypeHandle);

            for (int i = 0; i < batchInChunk.Count; i++)
            {

                Wander w = wanderChunk[i];
                Translation p = translationsChunk[i];
                Rotation r = rotationsChunk[i];
                Boid b = boidChunk[i];
                Seperation s = seperationChunk[i];
                Cohesion c = cohesionChunk[i];
                Alignment a = alignmentChunk[i];
                Constrain con = constrainChunk[i];
                ObstacleAvoidance oa = oaChunk[i];

                b.force = AccululateForces(ref b, ref oa, ref s, ref a, ref c, ref w, ref con) * b.weight;

                b.force = Vector3.ClampMagnitude(b.force, b.maxForce);
                Vector3 newAcceleration = (b.force * b.weight) * (1.0f / b.mass);
                newAcceleration.y *= limitUpAndDown;
                b.acceleration = Vector3.Lerp(b.acceleration, newAcceleration, dT);
                b.velocity += b.acceleration * dT;
                b.velocity = Vector3.ClampMagnitude(b.velocity, b.maxSpeed);

                float speed = b.velocity.magnitude;
                speeds[b.boidId] = speed;

                if (speed > 0)
                {
                    Vector3 tempUp = Vector3.Lerp(b.up, (Vector3.up) + (b.acceleration * banking), dT * 3.0f);
                    rotations[b.boidId] = Quaternion.LookRotation(b.velocity, tempUp);
                    b.up = rotations[b.boidId] * Vector3.up;

                    positions[b.boidId] += b.velocity * dT;
                    b.velocity *= (1.0f - (damping * dT));
                }
                b.force = Vector3.zero;
                boidChunk[i] = b;
            }
        }
    }

    [BurstCompile]
    struct WanderJob : IJobEntityBatch
    {
        public float dT;
        public float weight;
        public Unity.Mathematics.Random ran;

        public ComponentTypeHandle<Wander> wanderTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Translation> translationTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Rotation> rotationTypeHandle;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var wanderChunk = batchInChunk.GetNativeArray(wanderTypeHandle);
            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var translationsChunk = batchInChunk.GetNativeArray(translationTypeHandle);
            var rotationsChunk = batchInChunk.GetNativeArray(rotationTypeHandle);

            for (int i = 0; i < batchInChunk.Count; i++)
            {

                Wander w = wanderChunk[i];
                Translation p = translationsChunk[i];
                Rotation r = rotationsChunk[i];
                Vector3 disp = w.jitter * ran.NextFloat3Direction() * dT;
                w.target += disp;
                w.target.Normalize();
                w.target *= w.radius;

                Vector3 localTarget = (Vector3.forward * w.distance) + w.target;

                Quaternion q = r.Value;
                Vector3 pos = p.Value;
                Vector3 worldTarget = (q * localTarget) + pos;
                w.force = (worldTarget - pos) * weight;
                wanderChunk[i] = w;
            }
        }
    }

    [BurstCompile]
    struct CopyTransformsToNativeJob : IJobEntityBatch
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;
        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;

        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Translation> translationTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Rotation> rotationTypeHandle;


        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var translationsChunk = batchInChunk.GetNativeArray(translationTypeHandle);
            var rotationsChunk = batchInChunk.GetNativeArray(rotationTypeHandle);

            for (int i = 0; i < batchInChunk.Count; i++)
            {

                Translation p = translationsChunk[i];
                Rotation r = rotationsChunk[i];
                Boid b = boidChunk[i];
                positions[b.boidId] = p.Value;
                rotations[b.boidId] = r.Value;
            }
        }
    }

    [BurstCompile]
    struct CopyTransformsFromNativeJob : IJobEntityBatch
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;

        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        public ComponentTypeHandle<Translation> translationTypeHandle;
        public ComponentTypeHandle<Rotation> rotationTypeHandle;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var translationsChunk = batchInChunk.GetNativeArray(translationTypeHandle);
            var rotationsChunk = batchInChunk.GetNativeArray(rotationTypeHandle);

            for (int i = 0; i < batchInChunk.Count; i++)
            {

                Translation p = translationsChunk[i];
                Rotation r = rotationsChunk[i];
                Boid b = boidChunk[i];
                p.Value = positions[b.boidId];
                r.Value = rotations[b.boidId];
                translationsChunk[i] = p;
                rotationsChunk[i] = r;
            }
        }
    }


    [BurstCompile]
    struct CountNeighboursJob : IJobEntityBatch
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<int> neighbours;

        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<Quaternion> rotations;

        public ComponentTypeHandle<Boid> boidTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Translation> translationTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Rotation> rotationTypeHandle;

        [ReadOnly] public NativeMultiHashMap<int, int> cells;
        public float neighbourDistance;
        public int maxNeighbours;

        public int cellSize;
        public int gridSize;
        public bool threedcells;

        public bool usePartitioning;


        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var translationsChunk = batchInChunk.GetNativeArray(translationTypeHandle);
            var rotationsChunk = batchInChunk.GetNativeArray(rotationTypeHandle);


            for (int i = 0; i < batchInChunk.Count; i++)
            {
                Translation p = translationsChunk[i];
                Rotation r = rotationsChunk[i];
                Boid b = boidChunk[i];
                int neighbourStartIndex = maxNeighbours * b.boidId;
                int neighbourCount = 0;


                if (usePartitioning)
                {
                    int surroundingCellCount = (int)Mathf.Ceil(neighbourDistance / cellSize);

                    // Are we looking above and below? 
                    int sliceSurrounding = threedcells ? surroundingCellCount : 0;
                    for (int slice = -sliceSurrounding; slice <= sliceSurrounding; slice++)
                    {
                        for (int row = -surroundingCellCount; row <= surroundingCellCount; row++)
                        {
                            for (int col = -surroundingCellCount; col <= surroundingCellCount; col++)
                            {
                                Vector3 pos = positions[b.boidId] + new Vector3(col * cellSize, slice * cellSize, row * cellSize);
                                int cell = PartitionSpaceJob.PositionToCell(pos, cellSize, gridSize);

                                NativeMultiHashMapIterator<int> iterator;
                                int boidId;
                                if (cells.TryGetFirstValue(cell, out boidId, out iterator))
                                {
                                    do
                                    {
                                        if (boidId != b.boidId)
                                        {
                                            if (Vector3.Distance(positions[b.boidId], positions[boidId]) < neighbourDistance)
                                            {
                                                neighbours[neighbourStartIndex + neighbourCount] = boidId;
                                                neighbourCount++;
                                                if (neighbourCount == maxNeighbours)
                                                {
                                                    b.taggedCount = neighbourCount;
                                                    return;
                                                }
                                            }
                                        }
                                    } while (cells.TryGetNextValue(out boidId, ref iterator));
                                }
                            }
                        }

                    }                    
                }
                else
                {
                    for (int j = 0; j < positions.Length; j++)
                    {
                        if (j != b.boidId)
                        {
                            if (Vector3.Distance(positions[b.boidId], positions[j]) < neighbourDistance)
                            {
                                neighbours[neighbourStartIndex + neighbourCount] = j;
                                neighbourCount++;
                                if (neighbourCount == maxNeighbours)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
                b.taggedCount = neighbourCount;
                boidChunk[i] = b;
            }
        }
    }

    [BurstCompile]
    struct PartitionSpaceJob : IJobParallelFor
    {

        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        public NativeMultiHashMap<int, int>.ParallelWriter cells;
        public bool threedcells;
        public int cellSize;
        public int gridSize;

        public static Vector3 CellToPosition(int cell, int cellSize, int gridSize)
        {
            int row = cell / gridSize;
            int col = cell - (row * gridSize);

            return new Vector3(col * cellSize, 0, row * cellSize);
        }

        public static int PositionToCell(Vector3 pos, int cellSize, int gridSize)
        {
            return ((int)(pos.x / cellSize))
                + ((int)(pos.z / cellSize)) * gridSize;
        }
        public static int PositionToCell3D(Vector3 pos, int cellSize, int gridSize)
        {
            return ((int)(pos.x / cellSize))
                + ((int)(pos.z / cellSize)) * gridSize
                + ((int)(pos.y / cellSize)) * gridSize * gridSize;
        }

        public void Execute(int i)
        {
            int cell = threedcells
                ? PositionToCell3D(positions[i], cellSize, gridSize)
                : PositionToCell(positions[i], cellSize, gridSize);
            cells.Add(cell, i);
        }
    }

    [BurstCompile]
    struct ConstrainJob : IJobEntityBatch
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;
        public Vector3 centre;
        public float radius;
        public float weight;

        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        public ComponentTypeHandle<Constrain> constrainTypeHandle;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var constrainChunk = batchInChunk.GetNativeArray(constrainTypeHandle);

            for (int i = 0; i < batchInChunk.Count; i++)
            {
                Constrain con = constrainChunk[i];
                Boid b = boidChunk[i];
            
                Vector3 force = Vector3.zero;
                Vector3 toTarget = positions[b.boidId] - centre;
                if (toTarget.magnitude > radius)
                {
                    force = Vector3.Normalize(toTarget) * (radius - toTarget.magnitude);
                }
                /*
                float xDist = positions[b.boidId].x - centre.x;
                if (Mathf.Abs(xDist) > radius)
                {
                    force.x +=  radius - xDist;
                }
                float yDist = positions[b.boidId].y - centre.y;
                if (Mathf.Abs(yDist) > radius)
                {
                    force.y +=  radius - yDist;
                }
                float zDist = positions[b.boidId].z - centre.z;
                if (Mathf.Abs(zDist) > radius)
                {
                    force.z +=  radius - zDist;
                }
                */
                con.force = force * weight;
                constrainChunk[i] = con;
            }
        }
    }

    [BurstCompile]
    struct ObstacleAvoidanceJob:IJobEntityBatch
    {
        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Translation> translationTypeHandle;
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Rotation> rotationTypeHandle;
        public ComponentTypeHandle<ObstacleAvoidance> obstacleAvoidanceTypeHandle;
        public CollisionWorld collisionWorld;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {

            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var translationsChunk = batchInChunk.GetNativeArray(translationTypeHandle);
            var ltwChunk = batchInChunk.GetNativeArray(ltwTypeHandle);
            var obstacleAvoidanceChunk = batchInChunk.GetNativeArray(obstacleAvoidanceTypeHandle);
            var rotationChunk = batchInChunk.GetNativeArray(rotationTypeHandle);


            for (int i = 0; i < batchInChunk.Count; i++)
            {
                float3 force = Vector3.zero;
                Translation p = translationsChunk[i];
                ObstacleAvoidance oa = obstacleAvoidanceChunk[i];                
                LocalToWorld ltw = ltwChunk[i];
                Boid b = boidChunk[i];
                Rotation r = rotationChunk[i];

                oa.normal = Vector3.zero;
                oa.point = Vector3.zero;

                float3 forward = math.mul(r.Value, Vector3.forward);
                forward = math.normalize(forward);
                var input = new RaycastInput() {
                    Start = p.Value,
                    End = p.Value + (forward * oa.forwardFeelerDepth),

                    //Filter = CollisionFilter.Default                 
                    Filter = new CollisionFilter {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u,
                        GroupIndex = 0
                    }
                };

                oa.start = input.Start;
                oa.end = input.End;
                                    
                Unity.Physics.RaycastHit hit;
                if (collisionWorld.CastRay(input, out hit))
                {
                    Debug.Log("Collides");
                    float dist = math.distance(hit.Position, p.Value);
                    force += hit.SurfaceNormal * (oa.forwardFeelerDepth / dist);
                    oa.normal = hit.SurfaceNormal;
                    oa.point = hit.Position;
                }
                oa.force = force;
                obstacleAvoidanceChunk[i] = oa;       
            }
        }
    }

    [BurstCompile]
    struct SeperationJob : IJobEntityBatch
    {

        [ReadOnly] public ComponentTypeHandle<Boid> boidTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Translation> translationTypeHandle;
        public ComponentTypeHandle<Seperation> seperationTypeHandle;

        [ReadOnly]
        public NativeArray<int> neighbours;

        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;
        public float weight;
        public int maxNeighbours;
        public Unity.Mathematics.Random random;

        public int spineOffset;
        public int spineLength;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {

            var boidChunk = batchInChunk.GetNativeArray(boidTypeHandle);
            var translationsChunk = batchInChunk.GetNativeArray(translationTypeHandle);
            var seperationChunk = batchInChunk.GetNativeArray(seperationTypeHandle);

            for (int i = 0; i < batchInChunk.Count; i++)
            {
                Vector3 force = Vector3.zero;
                Translation p = translationsChunk[i];
                Seperation s = seperationChunk[i];                
                Boid b = boidChunk[i];

                for (int j = 0; j < b.taggedCount; j++)
                {
                    
                    int neighbourStartIndex = maxNeighbours * b.boidId;
                   
                    Vector3 myPosition = positions[b.boidId];


                    int neighbourId = neighbours[neighbourStartIndex + j];
                    if (neighbourId == b.boidId)
                    {
                        continue;
                    }
                    Vector3 toNeighbour = positions[b.boidId] - positions[neighbourId];
                    float mag = toNeighbour.magnitude;
                    
                    if (mag > 0) // Need this check otherwise this behaviour can return NAN
                    {
                        force += (Vector3.Normalize(toNeighbour) / mag);
                    }
                    else
                    {
                        // same position, so generate a random force
                        Vector3 f = random.NextFloat3Direction();
                        force += f * b.maxForce;
                    }
                }
                s.force = force * weight;
                seperationChunk[i] = s;
            }
        }
    }

    

    #endregion

}

