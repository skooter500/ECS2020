using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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

    [BurstCompile]
    struct PartitionSpaceJob : IJobParallelFor
    {

        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> positions;

        //public NativeMultiHashMap<int, int> cells;
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
            //cells.Add(cell, i);
        }
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


        protected override void OnCreate()
        {
            Instance = this;
            bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();
            Enabled = false;
            Debug.Log("BoidBootstrap.MAX_BOIDS=" + BoidBootstrap.MAX_BOIDS);
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
            BoidBootstrap bootstrap = this.bootstrap;

            ComponentTypeHandle<Wander> wTHandle = GetComponentTypeHandle<Wander>();
            ComponentTypeHandle<Boid> bTHandle = GetComponentTypeHandle<Boid>();
            ComponentTypeHandle<Translation> ttTHandle = GetComponentTypeHandle<Translation>();
            ComponentTypeHandle<Rotation> rTHandle = GetComponentTypeHandle<Rotation>();
            ComponentTypeHandle<Seperation> sTHandle = GetComponentTypeHandle<Seperation>();
            ComponentTypeHandle<Cohesion> cTHandle = GetComponentTypeHandle<Cohesion>();
            ComponentTypeHandle<Alignment> aTHandle = GetComponentTypeHandle<Alignment>();
            ComponentTypeHandle<Constrain> conTHandle = GetComponentTypeHandle<Constrain>();
            
            float deltaTime = Time.DeltaTime;
            
            Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000));

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
                constrainTypeHandle = conTHandle
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
        public ComponentTypeHandle<Boid> boidTypeHandle;


        private Vector3 AccululateForces(ref Boid b, ref Seperation s, ref Alignment a, ref Cohesion c, ref Wander w, ref Constrain con)
        {
            Vector3 force = Vector3.zero;

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
                
                b.force = AccululateForces(ref b, ref s, ref a, ref c, ref w, ref con)* b.weight;

                b.force = Vector3.ClampMagnitude(b.force, b.maxForce);
                Vector3 newAcceleration = (b.force * b.weight) *   (1.0f/ b.mass);
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
                //Debug.Log(worldTarget + "\t" + pos + "\t" + localTarget + "\t" + w.distance + "\t" + w.target + "\t" + w.jitter);
                Debug.Log(p.Value);

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
}

