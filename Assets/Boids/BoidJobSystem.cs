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
        static public bool checkNaN(Quaternion v)
        {
            if (float.IsNaN(v.x) || float.IsInfinity(v.x))
            {
                return true;
            }
            if (float.IsNaN(v.y) || float.IsInfinity(v.y))
            {
                return true;
            }
            if (float.IsNaN(v.z) || float.IsInfinity(v.z))
            {
                return true;
            }
            if (float.IsNaN(v.w) || float.IsInfinity(v.w))
            {
                return true;
            }
            return false;
        }

        public static Vector3 AccululateForces(ref Boid b, ref Seperation s, ref Alignment a, ref Cohesion c, ref Wander w, ref Constrain con)
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

        static public bool checkNaN(Vector3 v)
        {
            if (float.IsNaN(v.x) || float.IsInfinity(v.x))
            {
                return true;
            }
            if (float.IsNaN(v.y) || float.IsInfinity(v.y))
            {
                return true;
            }
            if (float.IsNaN(v.z) || float.IsInfinity(v.z))
            {
                return true;
            }
            return false;
        }

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

            float deltaTime = Time.DeltaTime;
            
            Unity.Mathematics.Random ran = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000));

            // Copy entities to the native arrays             
            var copyToNativeHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(rotations)
                .ForEach((ref Translation p, ref Rotation r, ref Boid b) =>
            {
                positions[b.boidId] = p.Value;
                rotations[b.boidId] = r.Value;
            })
            .ScheduleParallel(this.Dependency);

            Dependency = JobHandle.CombineDependencies(Dependency, copyToNativeHandle);
            
            var wanderJob = new WanderJob()
            {
                wanderTypeHandle = wTHandle,
                translationTypeHandle = ttTHandle,
                rotationTypeHandle = rTHandle,
                boidTypeHandle = bTHandle,
                dT = deltaTime,
                weight = bootstrap.wanderWeight
            };

            var wanderJobHandle = wanderJob.ScheduleParallel(wanderQuery, 1, Dependency);
            Dependency = JobHandle.CombineDependencies(Dependency, wanderJobHandle);
                    
            var cfJobHandle = Entities
            .WithNativeDisableParallelForRestriction(positions)
            .WithNativeDisableParallelForRestriction(rotations)
            .ForEach((ref Translation p, ref Rotation r, ref Boid b) =>
            {
                p.Value = positions[b.boidId];
                r.Value = rotations[b.boidId];
            })
            .ScheduleParallel(boidHandle);
            Dependency = JobHandle.CombineDependencies(Dependency, cfJobHandle);            
            return;
        }
    }

    [BurstCompile]
        struct CopyTransformsToJob : IJobProcessComponentData<Position, Rotation, Boid>
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<Vector3> positions;
            [NativeDisableParallelForRestriction]
            public NativeArray<Quaternion> rotations;

            public void Execute(ref Position p, ref Rotation r, ref Boid b)
            {
                positions[b.boidId] = p.Value;
                rotations[b.boidId] = r.Value;
            }

        }

        [BurstCompile]
        struct CopyTransformsFromJob : IJobEntityBatch
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<Vector3> positions;

            [NativeDisableParallelForRestriction]
            public NativeArray<Quaternion> rotations;

            public void Execute()
            {
                p.Value = positions[b.boidId];
                r.Value = rotations[b.boidId];
            }
        }

    struct WanderJob : IJobEntityBatch
    {
        public float dT;
        public float weight;
        public Unity.Mathematics.Random ran;

        [ReadOnly] public ComponentTypeHandle<Wander> wanderTypeHandle;
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
}

