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
            // Copy to local variables. Required for the lambdas
            NativeArray<int> neighbours = this.neighbours;            
            NativeArray<Vector3> positions = this.positions;
            NativeArray<Quaternion> rotations = this.rotations;
            NativeArray<float> speeds = this.speeds;
            BoidBootstrap bootstrap = this.bootstrap;
            NativeMultiHashMap<int, int>.ParallelWriter paralellCells = this.cells.AsParallelWriter();
            NativeMultiHashMap<int, int> cells = this.cells;

            float dT = Time.DeltaTime;

            float wanderWeight = bootstrap.wanderWeight;
            float limitUpAndDown = bootstrap.limitUpAndDown;
            float bondDamping = bootstrap.bondDamping;

            float neighbourDistance = bootstrap.neighbourDistance;
            int cellSize = bootstrap.cellSize;
            bool usePartitioning = bootstrap.usePartitioning;
            bool threedcells = bootstrap.threedcells;
            int gridSize = bootstrap.gridSize;
            int maxNeighbours = this.maxNeighbours;
            int spineLength = bootstrap.spineLength;
            int spineOffset = bootstrap.spineLength / 2;
            float seperationWeight = bootstrap.seperationWeight;
            float alignmentWeight = bootstrap.alignmentWeight;
            float cohesionWeight = bootstrap.cohesionWeight;
            float seekWeight = bootstrap.seekWeight;
            float fleeWeight = bootstrap.fleeWeight;
            float fleeDistance = bootstrap.fleeDistance;

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
                //cells = this.cells,
                threedcells = bootstrap.threedcells,
                cellSize = bootstrap.cellSize,
                gridSize = bootstrap.gridSize
            };

            var partitionHandle = partitionJob.Schedule(bootstrap.numBoids, 50, ctjHandle);
            
            var cnjHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(cells)
                .WithNativeDisableParallelForRestriction(neighbours)
                .ForEach((Boid b) =>
            {
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
                    for (int i = 0; i < positions.Length; i++)
                    {
                        if (i != b.boidId)
                        {
                            if (Vector3.Distance(positions[b.boidId], positions[i]) < neighbourDistance)
                            {
                                neighbours[neighbourStartIndex + neighbourCount] = i;
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
            }
            )
            .ScheduleParallel(partitionHandle);

            var seperationHandle = Entities
                .WithNativeDisableParallelForRestriction(positions)
                .WithNativeDisableParallelForRestriction(neighbours)
                .ForEach((ref Boid b, ref Seperation s) =>
            {
                Vector3 force = Vector3.zero;
                int neighbourStartIndex = maxNeighbours * b.boidId;
                int mySpineId = b.boidId * (spineLength + 1);
                Vector3 myPosition = positions[mySpineId + spineOffset];
                for (int i = 0; i < b.taggedCount; i++)
                {
                    int neighbourId = neighbours[neighbourStartIndex + i];
                    if (neighbourId == b.boidId)
                    {
                        continue;
                    }
                    int neighbourSpineId = (neighbourId * (spineLength + 1)) + spineOffset;
                    //Vector3 toNeighbour = positions[b.boidId] - positions[neighbourId];
                    Vector3 toNeighbour = myPosition - positions[neighbourSpineId];
                    float mag = toNeighbour.magnitude;
                    //force += (Vector3.Normalize(toNeighbour) / mag);


                    if (mag > 0) // Need this check otherwise this behaviour can return NAN
                    {
                        force += (Vector3.Normalize(toNeighbour) / mag);
                    }
                    else
                    {
                        // same position, so generate a random force
                        Vector3 f = ran.NextFloat3Direction();
                        force += f * b.maxForce;
                    }

                }
                s.force = force * seperationWeight;
            })
            .ScheduleParallel(cnjHandle);


            var cohesionHandle = Entities.ForEach((ref Boid b, ref Cohesion c) =>
            {
                Vector3 force = Vector3.zero;
                Vector3 centerOfMass = Vector3.zero;
                int neighbourStartIndex = maxNeighbours * b.boidId;
                for (int i = 0; i < b.taggedCount; i++)
                {
                    int neighbourId = neighbours[neighbourStartIndex + i];
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

                c.force = force * cohesionWeight;
            })
            .ScheduleParallel(seperationHandle);
            
                        
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
            .ScheduleParallel(cohesionHandle);            
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