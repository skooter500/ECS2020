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
            NativeArray<int> neighbours = this.neighbours;
            
            NativeArray<Vector3> positions = this.positions;
            NativeArray<Quaternion> rotations = this.rotations;
            NativeArray<float> speeds = this.speeds;
            BoidBootstrap bootstrap = this.bootstrap;
            float dT = Time.DeltaTime;

            float wanderWeight = bootstrap.wanderWeight;
            float limitUpAndDown = bootstrap.limitUpAndDown;
            float bondDamping = bootstrap.bondDamping;

            Unity.Mathematics.Random ran = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000));

            // Copy entities to the native arrays             
            var ctjHandle = Entities
            .WithNativeDisableParallelForRestriction(positions)
            .WithNativeDisableParallelForRestriction(rotations)
            .ForEach((ref Translation p, ref Rotation r, ref Boid b) =>
            {
                positions[b.boidId] = p.Value;
                rotations[b.boidId] = r.Value;
            })
            .ScheduleParallel(this.Dependency);

            cells.Clear();
            var partitionJob = new PartitionSpaceJob()
            {
                positions = this.positions,
                cells = this.cells.AsParallelWriter(),
                threedcells = bootstrap.threedcells,
                cellSize = bootstrap.cellSize,
                gridSize = bootstrap.gridSize
            };

            var partitionHandle = partitionJob.Schedule(bootstrap.numBoids, 50, ctjHandle);

                        
            var wjHandle = Entities.ForEach((ref Boid b, ref Wander w, ref Translation p, ref Rotation r) =>
            {
                Vector3 disp = w.jitter * ran.NextFloat3Direction() * dT;
                w.target += disp;
                w.target.Normalize();
                w.target *= w.radius;

                Vector3 localTarget = (Vector3.forward * w.distance) + w.target;

                Quaternion q = r.Value;
                Vector3 pos = p.Value;
                Vector3 worldTarget = (q * localTarget) + pos;
                w.force = (worldTarget - pos) * wanderWeight;
            })
            .ScheduleParallel(partitionHandle);            
            // Integrate the forces
            
            var boidHandle = Entities
            .WithNativeDisableParallelForRestriction(positions)
            .WithNativeDisableParallelForRestriction(rotations)
            .WithNativeDisableParallelForRestriction(speeds)
            .ForEach((ref Boid b, ref Seperation s, ref Alignment a, ref Cohesion c, ref Wander w, ref Constrain con) =>
            {
                float banking = 0.1f;
                b.force = AccululateForces(ref b, ref s, ref a, ref c, ref w, ref con) * b.weight;

                b.force = Vector3.ClampMagnitude(b.force, b.maxForce);
                Vector3 newAcceleration = (b.force * b.weight) / b.mass;
                newAcceleration.y *= limitUpAndDown;
                b.acceleration = Vector3.Lerp(b.acceleration, newAcceleration, dT);
                b.velocity += b.acceleration * dT;
                b.velocity = Vector3.ClampMagnitude(b.velocity, b.maxSpeed);
                //b.velocity.y *= 0.8f;
                float speed = b.velocity.magnitude;
                speeds[b.boidId] = speed;
                if (speed > 0)
                {
                    Vector3 tempUp = Vector3.Lerp(b.up, (Vector3.up) + (b.acceleration * banking), dT * 3.0f);
                    rotations[b.boidId] = Quaternion.LookRotation(b.velocity, tempUp);
                    b.up = rotations[b.boidId] * Vector3.up;

                    positions[b.boidId] += b.velocity * dT;
                    b.velocity *= (1.0f - (bondDamping * dT));
                }
            })
            .ScheduleParallel(wjHandle);
            
            var cfJobHandle = Entities
            .WithNativeDisableParallelForRestriction(positions)
            .WithNativeDisableParallelForRestriction(rotations)
            .ForEach((ref Translation p, ref Rotation r, ref Boid b) =>
            {
                p.Value = positions[b.boidId];
                r.Value = rotations[b.boidId];
            })
            .ScheduleParallel(boidHandle);
            Dependency = cfJobHandle;
            return;
        }
    }
}